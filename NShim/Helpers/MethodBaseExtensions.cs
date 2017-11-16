using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.Helpers
{
    internal static class MethodBaseExtensions
    {
        public static bool IsForValueType(this MethodBase methodBase) => methodBase.DeclaringType.IsSubclassOf(typeof(ValueType));

        public static IntPtr GetMethodPointer(this MethodBase methodBase)
        {
            if (methodBase is DynamicMethod method)
            {
                var methodDescriptior = typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
                return ((RuntimeMethodHandle)methodDescriptior.Invoke(method, null)).GetFunctionPointer();
            }
            return methodBase.MethodHandle.GetFunctionPointer();
        }
    }
}
