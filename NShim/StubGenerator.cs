using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NShim.Helpers;
using Sigil.NonGeneric;

namespace NShim
{
    internal class StubGenerator
    {
        private static readonly MethodInfo ShimContextGetReplacement =
            typeof(ShimContext).GetMethod(nameof(ShimContext.GetReplacement));
        private static readonly MethodInfo ShimContextTryGetReplacement =
            typeof(ShimContext).GetMethod(nameof(ShimContext.TryGetReplacement));
        private static readonly MethodInfo MethodBaseGetMethodPointer =
            typeof(MethodBaseExtensions).GetMethod(nameof(MethodBaseExtensions.GetMethodPointer), BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo MethodBaseGetMethodFromHandle = 
            typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), new [] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)});
        private static readonly MethodInfo ObjectGetType = typeof(object).GetMethod(nameof(GetType));
        private static readonly MethodInfo TypeGetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
        private static readonly MethodInfo GetUnitializedObject = typeof(FormatterServices).GetMethod(nameof(FormatterServices.GetUninitializedObject));
        private static readonly MethodInfo StubGeneratorGetRuntimeMethodForVirtual = 
            typeof(StubGenerator).GetMethod(nameof(GetRuntimeMethodForVirtual), BindingFlags.Static | BindingFlags.NonPublic);
        
        public static MethodInfo GenerateStubForMethod(MethodInfo info, bool isVirtual = false)
        {
            var methodParameters = info.GetParameters();
            var offset = info.IsStatic ? 0 : 1;
            var signatureParamTypes = new Type[offset + methodParameters.Length];
            
            if (!info.IsStatic)
            {
                signatureParamTypes[0] = info.IsForValueType()
                    ? info.DeclaringType.MakeByRefType()
                    : info.DeclaringType;
            }
            for (var i = 0; i < methodParameters.Length; i++)
            {
                signatureParamTypes[i + offset] = methodParameters[i].ParameterType;
            }
            var parameterTypes = new Type[signatureParamTypes.Length + 1];
            signatureParamTypes.CopyTo(parameterTypes, 0);
            parameterTypes[parameterTypes.Length - 1] = typeof(ShimContext);
            
            var stub = Emit.NewDynamicMethod(info.ReturnType, parameterTypes,
                string.Format("stub_{0}_{1}", info.DeclaringType, info.Name));
            
            stub.DeclareLocal<MethodBase>(out var localMethod, "method");
            stub.DeclareLocal<object>(out var localInstance, "instance");
            
            stub.LoadArgument((ushort) (parameterTypes.Length - 1));//Load ShimContext on stack

            if (!isVirtual)
            {
                stub.LoadConstant(info);
                stub.LoadConstant(info.DeclaringType);
                stub.Call(MethodBaseGetMethodFromHandle);
            }
            else
            {
                //Load this and find the actual type to look up the actual called method
                stub.LoadArgument(0);
                stub.Call(ObjectGetType);

                stub.LoadConstant(info);
                stub.LoadConstant(info.DeclaringType);
                stub.Call(MethodBaseGetMethodFromHandle);

                stub.CastClass<MethodInfo>();

                stub.Call(StubGeneratorGetRuntimeMethodForVirtual);
            }

            if (info.IsStatic || info.DeclaringType.IsValueType)
            {
                stub.LoadNull();
            }
            else
            {
                stub.LoadArgument(0);
            }
            //Instance parameter for GetReplacement

            stub.LoadLocalAddress(localMethod);
            stub.LoadLocalAddress(localInstance); //Replacement instance out parameter
            stub.Call(ShimContextTryGetReplacement);

            stub.DefineLabel(out var noReplacementFound);

            stub.BranchIfFalse(noReplacementFound);

            //Branch if we do not have an instance for our replacement
            stub.DefineLabel(out var staticReplacement);
            stub.LoadLocal(localInstance);
            stub.BranchIfFalse(staticReplacement);


            stub.LoadLocal(localInstance);
            for (ushort i = 0; i < signatureParamTypes.Length; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.HasThis, info.ReturnType, signatureParamTypes);
            stub.Return();

            stub.MarkLabel(staticReplacement);

            for (ushort i = 0; i < signatureParamTypes.Length; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.Standard, info.ReturnType, signatureParamTypes);
            stub.Return();

            stub.MarkLabel(noReplacementFound);

            for (ushort i = 0; i < parameterTypes.Length; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.Standard, info.ReturnType, parameterTypes);
            stub.Return();
            return stub.CreateDynamicMethod();
        }

        public static MethodInfo GenerateStub(ConstructorInfo info, bool newObject = true)
        {
            var parameters = info.GetParameters();
            var signatureParamTypes = new Type[parameters.Length + 1];
            var forValueType = info.IsForValueType();
            signatureParamTypes[0] = forValueType
                ? info.DeclaringType.MakeByRefType()
                : info.DeclaringType;
            for (var i = 0; i < parameters.Length; i++)
            {
                signatureParamTypes[i + 1] = parameters[i].ParameterType;
            }
            Type[] parameterTypes;
            if (newObject)
            {
                parameterTypes = new Type[parameters.Length + 1];
                Array.Copy(signatureParamTypes, 1, parameterTypes, 0, parameterTypes.Length - 1);
            }
            else
            {
                parameterTypes = new Type[signatureParamTypes.Length + 1];
                Array.Copy(signatureParamTypes, parameterTypes, parameterTypes.Length - 1);
            }
            parameterTypes[parameterTypes.Length - 1] = typeof(ShimContext);
            
            var stub = Emit.NewDynamicMethod(newObject ? info.DeclaringType : typeof(void), 
                parameterTypes,
                string.Format("stub_ctor_{0}_{1}", info.DeclaringType, info.Name));

            stub.DeclareLocal(info.DeclaringType, out var localType, "type");
            stub.DeclareLocal<MethodBase>(out var localMethod, "method");
            stub.DeclareLocal<object>(out var localInstance, "instance");
            
            stub.LoadArgument((ushort)(parameterTypes.Length - 1)); //Load ShimContext on stack

            stub.LoadConstant(info);
            stub.LoadConstant(info.DeclaringType);
            stub.Call(MethodBaseGetMethodFromHandle);

            stub.LoadNull();

            stub.LoadLocalAddress(localMethod);
            stub.LoadLocalAddress(localInstance); //Replacement instance out parameter
            stub.Call(ShimContextTryGetReplacement);

            stub.DefineLabel(out var noReplacementFound);

            stub.BranchIfFalse(noReplacementFound);
            
            //Branch if we do not have an instance for our replacement
            stub.LoadLocal(localInstance);
            stub.DefineLabel(out var staticReplacement);
            stub.BranchIfFalse(staticReplacement);

            stub.LoadLocal(localInstance);
            for (ushort i = 0; i < parameterTypes.Length - 1; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.HasThis, info.DeclaringType, parameterTypes.Take(parameterTypes.Length - 1).ToArray());
            if (!newObject)
                stub.Pop();
            stub.Return();

            stub.MarkLabel(staticReplacement);

            for (ushort i = 0; i < parameterTypes.Length - 1; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.Standard, info.DeclaringType, parameterTypes.Take(parameterTypes.Length - 1).ToArray());
            if (!newObject)
                stub.Pop();
            stub.Return();


            stub.MarkLabel(noReplacementFound);
            
            if (newObject)
            {
                if (forValueType)
                {
                    stub.LoadLocalAddress(localType);
                    stub.InitializeObject(info.DeclaringType);
                }
                else
                {
                    stub.LoadConstant(info.DeclaringType);
                    stub.Call(TypeGetTypeFromHandle);
                    stub.Call(GetUnitializedObject);
                    stub.CastClass(info.DeclaringType);
                    stub.StoreLocal(localType);
                }
            }

            var paramTypes = parameterTypes;
            if (newObject)
            {
                if (forValueType)
                    stub.LoadLocalAddress(localType);
                else
                    stub.LoadLocal(localType);
                paramTypes = new Type[parameterTypes.Length + 1];
                Array.Copy(parameterTypes, 0, paramTypes, 1, parameterTypes.Length);
                paramTypes[0] = signatureParamTypes[0];
            }
            for (ushort i = 0; i < parameterTypes.Length; i++)
                stub.LoadArgument(i);
            stub.LoadLocal(localMethod);
            stub.Call(MethodBaseGetMethodPointer);
            stub.CallIndirect(CallingConventions.Standard, typeof(void), paramTypes);

            if (newObject)
                stub.LoadLocal(localType);

            stub.Return();
            return stub.CreateDynamicMethod();
        }

        public static MethodInfo GenerateStubForVirtualMethod(MethodInfo info)
        {
            return GenerateStubForMethod(info, true);
        }

        public static MethodInfo GenerateStubForMethodPointer(MethodInfo info)
        {
            var parameterTypes = new[] {typeof(ShimContext)};

            var stub = Emit.NewDynamicMethod(typeof(IntPtr),
                parameterTypes,
                string.Format("stub_ftn_{0}_{1}", info.DeclaringType, info.Name));

            stub.DeclareLocal<object>(out var localInstance, "instance");

            stub.LoadArgument((ushort)(parameterTypes.Length - 1)); //Load ShimContext on stack

            stub.LoadConstant(info);
            stub.LoadConstant(info.DeclaringType);
            stub.Call(MethodBaseGetMethodFromHandle);

            stub.LoadNull();
            stub.LoadLocalAddress(localInstance);
            stub.Call(ShimContextGetReplacement);

            stub.Call(MethodBaseGetMethodPointer);

            stub.Return();

            return stub.CreateDynamicMethod();
        }

        private static MethodInfo GetRuntimeMethodForVirtual(Type type, MethodInfo methodInfo)
        {
            var bindingFlags = BindingFlags.Instance | (methodInfo.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic);
            var types = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            return type.GetMethod(methodInfo.Name, bindingFlags, null, types, null);
        }
    }
}
