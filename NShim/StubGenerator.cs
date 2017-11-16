using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using NShim.Helpers;

namespace NShim
{
    internal class StubGenerator
    {
        private static readonly MethodInfo ShimContextGetMethod =
            typeof(ShimContext).GetMethod(nameof(ShimContext.GetMethod));
        private static readonly MethodInfo MethodBaseGetMethodPointer =
            typeof(MethodBaseExtensions).GetMethod(nameof(MethodBaseExtensions.GetMethodPointer), BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo MethodBaseGetMethodFromHandle = 
            typeof(MethodBase).GetMethod(nameof(MethodBase.GetMethodFromHandle), new [] { typeof(RuntimeMethodHandle), typeof(RuntimeTypeHandle)});
        private static readonly MethodInfo ObjectGetType = typeof(object).GetMethod(nameof(GetType));
        private static readonly MethodInfo TypeGetTypeFromHandle = typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle));
        private static readonly MethodInfo GetUnitializedObject = typeof(FormatterServices).GetMethod(nameof(FormatterServices.GetUninitializedObject));
        private static readonly MethodInfo StubGeneratorGetRuntimeMethodForVirtual = 
            typeof(StubGenerator).GetMethod(nameof(GetRuntimeMethodForVirtual), BindingFlags.Static | BindingFlags.NonPublic);
        
        public static MethodInfo GenerateStubForMethod(MethodInfo info)
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

            // ILGenerator
            var stub = new DynamicMethod(
                string.Format("stub_{0}_{1}", info.DeclaringType, info.Name),
                info.ReturnType,
                parameterTypes,
                typeof(StubGenerator).Module,
                true);

            var ilGenerator = stub.GetILGenerator();

            ilGenerator.DeclareLocal(typeof(MethodBase));               //Actual method
            
            ilGenerator.Emit(OpCodes.Ldarg, parameterTypes.Length - 1); //Load ShimContext on stack

            ilGenerator.Emit(OpCodes.Ldtoken, info);
            ilGenerator.Emit(OpCodes.Ldtoken, info.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodFromHandle);

            ilGenerator.Emit(info.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, ShimContextGetMethod);
            ilGenerator.Emit(OpCodes.Stloc_0);

            for (var i = 0; i < signatureParamTypes.Length; i++)
                ilGenerator.Emit(OpCodes.Ldarg, i);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodPointer);

            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, info.ReturnType, signatureParamTypes, null);

            ilGenerator.Emit(OpCodes.Ret);
            return stub;
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

            var stub = new DynamicMethod(
                string.Format("stub_ctor_{0}_{1}", info.DeclaringType, info.Name),
                newObject ? info.DeclaringType : typeof(void),
                parameterTypes,
                typeof(StubGenerator).Module,
                true);

            var ilGenerator = stub.GetILGenerator();
            
            var constructorLabel = ilGenerator.DefineLabel();

            ilGenerator.DeclareLocal(info.DeclaringType);
            ilGenerator.DeclareLocal(typeof(MethodBase));               //Actual method

            ilGenerator.Emit(OpCodes.Ldarg, parameterTypes.Length - 1); //Load ShimContext on stack

            ilGenerator.Emit(OpCodes.Ldtoken, info);
            ilGenerator.Emit(OpCodes.Ldtoken, info.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodFromHandle);
            
            ilGenerator.Emit(OpCodes.Ldnull);
            ilGenerator.Emit(OpCodes.Call, ShimContextGetMethod);
            ilGenerator.Emit(OpCodes.Stloc_1);

            ilGenerator.Emit(OpCodes.Ldloc_1);
            ilGenerator.Emit(OpCodes.Isinst, typeof(ConstructorInfo));
            
            ilGenerator.Emit(OpCodes.Brtrue_S, constructorLabel);

            for (var i = 0; i < parameterTypes.Length - 1; i++)
                ilGenerator.Emit(OpCodes.Ldarg, i);
            ilGenerator.Emit(OpCodes.Ldloc_1);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodPointer);

            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, info.DeclaringType, signatureParamTypes.Skip(1).ToArray(), null);
            ilGenerator.Emit(OpCodes.Ret);

            ilGenerator.MarkLabel(constructorLabel);

            if (newObject)
            {
                if (forValueType)
                {
                    ilGenerator.Emit(OpCodes.Ldloca, 0);
                    ilGenerator.Emit(OpCodes.Initobj, info.DeclaringType);
                }
                else
                {
                    ilGenerator.Emit(OpCodes.Ldtoken, info.DeclaringType);
                    ilGenerator.Emit(OpCodes.Call, TypeGetTypeFromHandle);
                    ilGenerator.Emit(OpCodes.Call, GetUnitializedObject);
                    ilGenerator.Emit(OpCodes.Stloc_0);
                }
            }
            
            int count = signatureParamTypes.Length;
            if (newObject)
            {
                if (forValueType)
                    ilGenerator.Emit(OpCodes.Ldloca, 0);
                else
                    ilGenerator.Emit(OpCodes.Ldloc_0);
                count = count - 1;
            }
            for (int i = 0; i < count; i++)
                ilGenerator.Emit(OpCodes.Ldarg, i);
            ilGenerator.Emit(OpCodes.Ldloc_1);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodPointer);
            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(void), signatureParamTypes, null);
            
            if (newObject)
                ilGenerator.Emit(OpCodes.Ldloc_0);

            ilGenerator.Emit(OpCodes.Ret);
            return stub;
        }

        public static MethodInfo GenerateStubForVirtualMethod(MethodInfo info)
        {
            //if(!info.IsVirtual)
            //    throw new ArgumentException("Only virtual method allowed!", nameof(info));
            var methodParameters = info.GetParameters();
            var signatureParamTypes = new Type[methodParameters.Length + 1];
            signatureParamTypes[0] = info.IsForValueType()
                    ? info.DeclaringType.MakeByRefType()
                    : info.DeclaringType;
            for (var i = 0; i < methodParameters.Length; i++)
            {
                signatureParamTypes[i + 1] = methodParameters[i].ParameterType;
            }
            var parameterTypes = new Type[signatureParamTypes.Length + 1];
            signatureParamTypes.CopyTo(parameterTypes, 0);
            parameterTypes[parameterTypes.Length - 1] = typeof(ShimContext);

            // ILGenerator
            var stub = new DynamicMethod(
                string.Format("stub_virt_{0}_{1}", info.DeclaringType, info.Name),
                info.ReturnType,
                parameterTypes,
                typeof(StubGenerator).Module,
                true);

            var ilGenerator = stub.GetILGenerator();

            ilGenerator.DeclareLocal(typeof(MethodBase));               //Actual method

            ilGenerator.Emit(OpCodes.Ldarg, parameterTypes.Length - 1); //Load ShimContext on stack

            ilGenerator.Emit(OpCodes.Ldtoken, info);
            ilGenerator.Emit(OpCodes.Ldtoken, info.DeclaringType);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodFromHandle);
            ilGenerator.Emit(OpCodes.Stloc_0);

            //Load this and find the actual type to look up the actual called method
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, ObjectGetType);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Call, StubGeneratorGetRuntimeMethodForVirtual);

            ilGenerator.Emit(info.IsStatic ? OpCodes.Ldnull : OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Call, ShimContextGetMethod);
            ilGenerator.Emit(OpCodes.Stloc_0);

            for (var i = 0; i < signatureParamTypes.Length; i++)
                ilGenerator.Emit(OpCodes.Ldarg, i);
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Call, MethodBaseGetMethodPointer);

            ilGenerator.EmitCalli(OpCodes.Calli, CallingConventions.Standard, info.ReturnType, signatureParamTypes, null);

            ilGenerator.Emit(OpCodes.Ret);
            return stub;
        }

        public static MethodInfo GenerateStubForMethodPointer(MethodInfo methodInfo)
        {
            throw new NotImplementedException();
        }

        private static MethodInfo GetRuntimeMethodForVirtual(Type type, MethodInfo methodInfo)
        {
            var bindingFlags = BindingFlags.Instance | (methodInfo.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic);
            var types = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();
            return type.GetMethod(methodInfo.Name, bindingFlags, null, types, null);
        }
    }
}
