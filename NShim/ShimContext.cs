using System.Collections.Generic;
using System.Reflection;

namespace NShim
{
    internal class ShimContext
    {
        private readonly Shim[] _shims;
        private readonly Dictionary<MethodBase, MethodInfo> _cache = new Dictionary<MethodBase, MethodInfo>();

        public ShimContext(Shim[] shims = null)
        {
            _shims = shims ?? new Shim[0];
        }

        public bool TryGetMatchingShim(MethodBase original, object instance, out Shim shim)
        {
            foreach (var s in _shims)
            {
                //First check if the instance matches
                if (s.Instance != null && !ReferenceEquals(s.Instance, instance))
                    continue;
                //Now check if the method is the same or overriden in a subclass
                if (s.Original == original ||
                    IsOverride(s.Original, original))
                {
                    shim = s;
                    return true;
                }
            }
            shim = null;
            return false;
        }

        /// <summary>
        /// Returns true if the overrideBase MethodBase is a MethodInfo for a method that overrides the 
        /// method in shimMethodBase
        /// </summary>
        /// <param name="shimMethodBase"></param>
        /// <param name="overrideBase"></param>
        /// <returns></returns>
        private static bool IsOverride(MethodBase shimMethodBase, MethodBase overrideBase)
        {
            return shimMethodBase is MethodInfo shimInfo &&
                   overrideBase is MethodInfo info &&
                   shimInfo.GetBaseDefinition() == info.GetBaseDefinition() &&
                   shimInfo.DeclaringType.IsAssignableFrom(info.DeclaringType);
        }

        public MethodBase GetReplacement(MethodBase original, object instance, 
                        out object replacementInstance)
        {
            if (TryGetMatchingShim(original, instance, out var shim))
            {
                replacementInstance = shim.Replacement.Target;
                return shim.Replacement.Method;
            }
            replacementInstance = null;
            if (!_cache.TryGetValue(original, out var target))
            {
                target = ILRewriter.ILRewriter.Rewrite(original, this);
                _cache.Add(original, target);
            }
            return target;
        }

        public bool TryGetReplacement(MethodBase original, object instance,
                                      out MethodBase replacement, out object replacementInstance)
        {
            if (TryGetMatchingShim(original, instance, out var shim))
            {
                replacement = shim.Replacement.Method;
                replacementInstance = shim.Replacement.Target;
                return true;
            }
            if (!_cache.TryGetValue(original, out var target))
            {
                target = ILRewriter.ILRewriter.Rewrite(original, this);
                _cache.Add(original, target);
            }
            replacement = target;
            replacementInstance = null;
            return false;
        }
    }
}
