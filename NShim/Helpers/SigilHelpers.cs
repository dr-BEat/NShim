using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Sigil;
using Sigil.NonGeneric;

namespace NShim.Helpers
{
    internal static class SigilHelpers
    {
        private static readonly FieldInfo ReturnType =
            typeof(Emit).GetField("ReturnType", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ParameterTypes =
            typeof(Emit).GetField("ParameterTypes", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly FieldInfo InnerEmit = 
            typeof(Emit).GetField("InnerEmit", BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly PropertyInfo DynMethod = 
            InnerEmit.FieldType.GetProperty("DynMethod", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Type TransitionWrapperType = typeof(Emit).Assembly.GetType("Sigil.Impl.TransitionWrapper");

        private static readonly MethodInfo TransitionWrapperGet =
            TransitionWrapperType.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);

        private static readonly Type StackTransitionType = typeof(Emit).Assembly.GetType("Sigil.Impl.StackTransition");
        private static readonly MethodInfo StackTransitionPush =
            StackTransitionType.GetMethod("Push", BindingFlags.Static | BindingFlags.Public, null, new []{typeof(Type)}, null);

        private static readonly MethodInfo UpdateState = InnerEmit.FieldType.GetMethod("UpdateState",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] {typeof(OpCode), typeof(ConstructorInfo), TransitionWrapperType}, null);

        public static DynamicMethod CreateDynamicMethod(this Emit emit)
        {
            var returnType = (Type)ReturnType.GetValue(emit);
            var parameterTypes = (Type[]) ParameterTypes.GetValue(emit);
            var delegateType = Expression.GetDelegateType(parameterTypes.Concat(new[] { returnType }).ToArray());

            //Force initialization of the DynMethod field
            emit.CreateDelegate(delegateType);

            var innerEmit = InnerEmit.GetValue(emit);
            var dynamicMethod = (DynamicMethod) DynMethod.GetValue(innerEmit);
            return dynamicMethod;
        }

        /// <summary>Push a constant RuntimeMethodHandle onto the stack.</summary>
        public static Emit LoadConstant(this Emit emit, ConstructorInfo constructor)
        {
            if (constructor == null)
                throw new ArgumentNullException(nameof(constructor));
            var stackTransitions = StackTransitionPush.Invoke(null, new object[] {typeof(RuntimeMethodHandle)});
            var wrapper = TransitionWrapperGet.Invoke(null, new[] { nameof(LoadConstant), stackTransitions});
            UpdateState.Invoke(InnerEmit.GetValue(emit), new[] {OpCodes.Ldtoken, constructor, wrapper});
            return emit;
        }

        /// <summary>Push a constant RuntimeMethodHandle onto the stack.</summary>
        public static Emit<T> LoadConstant<T>(this Emit<T> emit, ConstructorInfo constructor)
        {
            if (constructor == null)
                throw new ArgumentNullException(nameof(constructor));
            var stackTransitions = StackTransitionPush.Invoke(null, new object[] { typeof(RuntimeMethodHandle) });
            var wrapper = TransitionWrapperGet.Invoke(null, new[] { stackTransitions, nameof(LoadConstant) });
            UpdateState.Invoke(emit, new[] { OpCodes.Ldtoken, constructor, wrapper });
            return emit;
        }
    }
}
