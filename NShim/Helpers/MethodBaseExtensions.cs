using System;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.Helpers
{
    internal static class MethodBaseExtensions
    {
        private static readonly MethodInfo GetMethodDescriptor =
            typeof(DynamicMethod).GetMethod("GetMethodDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);

        public static bool IsForValueType(this MethodBase methodBase) => methodBase.DeclaringType.IsSubclassOf(typeof(ValueType));

        public static IntPtr GetMethodPointer(this MethodBase methodBase)
        {
            if (methodBase is DynamicMethod method)
            {
                return ((RuntimeMethodHandle)GetMethodDescriptor.Invoke(method, null)).GetFunctionPointer();
            }
            return methodBase.MethodHandle.GetFunctionPointer();
        }
    }
}
