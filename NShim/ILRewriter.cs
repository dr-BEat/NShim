using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Mono.Reflection;
using NShim.Helpers;

namespace NShim
{
    internal class ILRewriter
    {
        public static MethodInfo Rewrite(MethodBase method, ShimContext context)
        {
            var parameterTypes = new List<Type>();
            if (!method.IsStatic)
            {
                parameterTypes.Add(method.IsForValueType() ? 
                    method.DeclaringType.MakeByRefType() : 
                    method.DeclaringType);
            }

            parameterTypes.AddRange(method.GetParameters().Select(p => p.ParameterType));
            parameterTypes.Add(typeof(ShimContext));

            var returnType = method.IsConstructor ? typeof(void) : ((MethodInfo) method).ReturnType;

            var dynamicMethod = new DynamicMethod(
                string.Format("dynamic_{0}_{1}", method.DeclaringType, method.Name),
                returnType,
                parameterTypes.ToArray(),
                typeof(ILRewriter).Module,
                true);

            var ilGenerator = dynamicMethod.GetILGenerator();

            var targetInstructions = new Dictionary<int, Label>();

            foreach (var local in method.GetMethodBody().LocalVariables)
            {
                ilGenerator.DeclareLocal(local.LocalType, local.IsPinned);
            }

            var instructions = method.GetInstructions();
            foreach (var instruction in instructions)
            {
                switch (instruction.Operand)
                {
                    case Instruction target:
                        targetInstructions.TryAdd(target.Offset, ilGenerator.DefineLabel);
                        break;
                    case Instruction[] targets:
                        foreach (var target in targets)
                        {
                            targetInstructions.TryAdd(target.Offset, ilGenerator.DefineLabel);
                        }
                        break;
                }
            }

            foreach (var instruction in instructions)
            {
                if (targetInstructions.TryGetValue(instruction.Offset, out var label))
                    ilGenerator.MarkLabel(label);

                switch (instruction.OpCode.OperandType)
                {
                    case OperandType.InlineNone:
                        ilGenerator.Emit(instruction.OpCode);
                        break;
                    case OperandType.InlineI:
                        ilGenerator.Emit(instruction.OpCode, (int) instruction.Operand);
                        break;
                    case OperandType.InlineI8:
                        ilGenerator.Emit(instruction.OpCode, (long) instruction.Operand);
                        break;
                    case OperandType.ShortInlineI:
                        if (instruction.OpCode == OpCodes.Ldc_I4_S)
                            ilGenerator.Emit(instruction.OpCode, (sbyte) instruction.Operand);
                        else
                            ilGenerator.Emit(instruction.OpCode, (byte) instruction.Operand);
                        break;
                    case OperandType.InlineR:
                        ilGenerator.Emit(instruction.OpCode, (double) instruction.Operand);
                        break;
                    case OperandType.ShortInlineR:
                        ilGenerator.Emit(instruction.OpCode, (float) instruction.Operand);
                        break;
                    case OperandType.InlineString:
                        ilGenerator.Emit(instruction.OpCode, (string) instruction.Operand);
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        EmitILForInlineBrTarget(ilGenerator, instruction, targetInstructions);
                        break;
                    case OperandType.InlineSwitch:
                        EmitILForInlineSwitch(ilGenerator, instruction, targetInstructions);
                        break;
                    case OperandType.ShortInlineVar:
                    case OperandType.InlineVar:
                        EmitILForInlineVar(ilGenerator, instruction, method.IsStatic);
                        break;
                    case OperandType.InlineTok:
                    case OperandType.InlineType:
                    case OperandType.InlineField:
                    case OperandType.InlineMethod:
                        EmitILForInlineMember(ilGenerator, instruction, context, parameterTypes.Count - 1);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            return dynamicMethod;
        }

        private static void EmitILForInlineBrTarget(ILGenerator ilGenerator,
            Instruction instruction, Dictionary<int, Label> targetInstructions)
        {
            var targetLabel = targetInstructions[((Instruction) instruction.Operand).Offset];
            
            // Offset values could change and not be short form anymore
            if (instruction.OpCode == OpCodes.Br_S)
                ilGenerator.Emit(OpCodes.Br, targetLabel);
            else if (instruction.OpCode == OpCodes.Brfalse_S)
                ilGenerator.Emit(OpCodes.Brfalse, targetLabel);
            else if (instruction.OpCode == OpCodes.Brtrue_S)
                ilGenerator.Emit(OpCodes.Brtrue, targetLabel);
            else if (instruction.OpCode == OpCodes.Beq_S)
                ilGenerator.Emit(OpCodes.Beq, targetLabel);
            else if (instruction.OpCode == OpCodes.Bge_S)
                ilGenerator.Emit(OpCodes.Bge, targetLabel);
            else if (instruction.OpCode == OpCodes.Bgt_S)
                ilGenerator.Emit(OpCodes.Bgt, targetLabel);
            else if (instruction.OpCode == OpCodes.Ble_S)
                ilGenerator.Emit(OpCodes.Ble, targetLabel);
            else if (instruction.OpCode == OpCodes.Blt_S)
                ilGenerator.Emit(OpCodes.Blt, targetLabel);
            else if (instruction.OpCode == OpCodes.Bne_Un_S)
                ilGenerator.Emit(OpCodes.Bne_Un, targetLabel);
            else if (instruction.OpCode == OpCodes.Bge_Un_S)
                ilGenerator.Emit(OpCodes.Bge_Un, targetLabel);
            else if (instruction.OpCode == OpCodes.Bgt_Un_S)
                ilGenerator.Emit(OpCodes.Bgt_Un, targetLabel);
            else if (instruction.OpCode == OpCodes.Ble_Un_S)
                ilGenerator.Emit(OpCodes.Ble_Un, targetLabel);
            else if (instruction.OpCode == OpCodes.Blt_Un_S)
                ilGenerator.Emit(OpCodes.Blt_Un, targetLabel);
            else if (instruction.OpCode == OpCodes.Leave_S)
                ilGenerator.Emit(OpCodes.Leave, targetLabel);
            else
                ilGenerator.Emit(instruction.OpCode, targetLabel);
        }

        private static void EmitILForInlineSwitch(ILGenerator ilGenerator,
            Instruction instruction, Dictionary<int, Label> targetInstructions)
        {
            var switchInstructions = (Instruction[]) instruction.Operand;
            var targetLabels = new Label[switchInstructions.Length];
            for (var i = 0; i < switchInstructions.Length; i++)
                targetLabels[i] = targetInstructions[switchInstructions[i].Offset];
            ilGenerator.Emit(instruction.OpCode, targetLabels);
        }

        private static void EmitILForInlineVar(ILGenerator ilGenerator, Instruction instruction, bool isStatic)
        {
            var index = 0;
            switch (instruction.Operand)
            {
                case LocalVariableInfo variableInfo:
                    index = variableInfo.LocalIndex;
                    break;
                case ParameterInfo parameterInfo:
                    index = parameterInfo.Position;
                    if (!isStatic)
                    {
                        index++;
                    }
                    break;
            }

            if (instruction.OpCode.OperandType == OperandType.ShortInlineVar)
                ilGenerator.Emit(instruction.OpCode, (byte) index);
            else
                ilGenerator.Emit(instruction.OpCode, (short) index);
        }

        private static void EmitILForConstructor(ILGenerator ilGenerator, Instruction instruction,
            ConstructorInfo constructorInfo, int contextParamIndex)
        {
            /*if (PoseContext.StubCache.TryGetValue(constructorInfo, out DynamicMethod stub))
            {
                ilGenerator.Emit(OpCodes.Ldtoken, constructorInfo);
                ilGenerator.Emit(OpCodes.Ldtoken, constructorInfo.DeclaringType);
                ilGenerator.Emit(OpCodes.Call, stub);
                return;
            }*/

            var methodBody = constructorInfo.GetMethodBody();
            if (methodBody == null)
            {
                ilGenerator.Emit(instruction.OpCode, constructorInfo);
                return;
            }

            if (instruction.OpCode != OpCodes.Newobj && instruction.OpCode != OpCodes.Call)
            {
                ilGenerator.Emit(instruction.OpCode, constructorInfo);
                return;
            }

            var stub = StubGenerator.GenerateStub(constructorInfo, instruction.OpCode == OpCodes.Newobj);

            ilGenerator.Emit(OpCodes.Ldarg, contextParamIndex);
            ilGenerator.Emit(OpCodes.Call, stub);
            
            //PoseContext.StubCache.TryAdd(constructorInfo, stub);
        }

        private static void EmitILForMethod(ILGenerator ilGenerator, Instruction instruction, MethodInfo methodInfo, ShimContext context, int contextParamIndex)
        {
            /*if (context.StubCache.TryGetValue(methodInfo, out DynamicMethod stub))
            {
                ilGenerator.Emit(OpCodes.Ldtoken, methodInfo);
                ilGenerator.Emit(OpCodes.Ldtoken, methodInfo.DeclaringType);
                ilGenerator.Emit(OpCodes.Call, stub);
                return;
            }*/

            var methodBody = methodInfo.GetMethodBody();
            if (methodBody == null)
            {
                ilGenerator.Emit(instruction.OpCode, methodInfo);
                return;
            }

            MethodInfo stub = null;
            if (instruction.OpCode == OpCodes.Call)
            {
                stub = StubGenerator.GenerateStubForMethod(methodInfo);
            }
            else if(instruction.OpCode == OpCodes.Callvirt)
            {
                stub = StubGenerator.GenerateStubForVirtualMethod(methodInfo);
            }
            /*else if (instruction.OpCode == OpCodes.Ldftn)
            {
                stub = StubGenerator.GenerateStubForMethodPointer(methodInfo);
            }
            else if (instruction.OpCode == OpCodes.Ldvirtftn)
            {
                stub = StubGenerator.GenerateStubForMethodPointer(methodInfo);
            }*/
            else
            {
                ilGenerator.Emit(instruction.OpCode, methodInfo);
                return;
            }

            ilGenerator.Emit(OpCodes.Ldarg, contextParamIndex);
            ilGenerator.Emit(OpCodes.Call, stub);
            //PoseContext.StubCache.TryAdd(methodInfo, stub);
        }

        private static void EmitILForInlineMember(ILGenerator ilGenerator, Instruction instruction, ShimContext context, int contextParamIndex)
        {
            switch (instruction.Operand)
            {
                case FieldInfo fieldInfo:
                    ilGenerator.Emit(instruction.OpCode, fieldInfo);
                    break;
                case TypeInfo typeInfo:
                    ilGenerator.Emit(instruction.OpCode, typeInfo);
                    break;
                case ConstructorInfo constructorInfo:
                    EmitILForConstructor(ilGenerator, instruction, constructorInfo, contextParamIndex);
                    break;
                case MethodInfo methodInfo:
                    EmitILForMethod(ilGenerator, instruction, methodInfo, context, contextParamIndex);
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}