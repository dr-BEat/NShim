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
                if (s.Original == original && (s.Instance == null || s.Instance == instance))
                {
                    shim = s;
                    return true;
                }
            }
            shim = null;
            return false;
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
                target = ILRewriter.Rewrite(original, this);
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
                target = ILRewriter.Rewrite(original, this);
                _cache.Add(original, target);
            }
            replacement = target;
            replacementInstance = null;
            return false;
        }
    }
}
