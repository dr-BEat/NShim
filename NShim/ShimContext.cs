using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

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

        public MethodBase GetMethod(MethodBase original, object instance)
        {
            if (TryGetMatchingShim(original, instance, out var shim))
                return shim.Target;
            if(_cache.TryGetValue(original, out var target))
                return target;
            return original;
        }
    }
}
