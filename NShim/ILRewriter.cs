using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NShim.Helpers;
using NShim.ILReader;

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

            var ilReader = new ILReader.ILReader(method);
            foreach (var instruction in ilReader)
            {
                switch (instruction)
                {
                    case ShortInlineBrTargetInstruction brTargetInstruction:
                        targetInstructions.TryAdd(brTargetInstruction.TargetOffset, ilGenerator.DefineLabel);
                        break;
                    case InlineBrTargetInstruction brTargetInstruction:
                        targetInstructions.TryAdd(brTargetInstruction.TargetOffset, ilGenerator.DefineLabel);
                        break;
                    case InlineSwitchInstruction switchInstruction:
                        foreach (var offset in switchInstruction.TargetOffsets)
                        {
                            targetInstructions.TryAdd(offset, ilGenerator.DefineLabel);
                        }
                        break;
                }
            }

            foreach (var instruction in ilReader)
            {
                if (targetInstructions.TryGetValue(instruction.Offset, out var label))
                    ilGenerator.MarkLabel(label);
                switch (instruction)
                {
                    case InlineNoneInstruction none:
                        ilGenerator.Emit(none.OpCode);
                        break;
                    case InlineIInstruction i:
                        ilGenerator.Emit(instruction.OpCode, i.Operand);
                        break;
                    case InlineI8Instruction i8:
                        ilGenerator.Emit(instruction.OpCode, i8.Operand);
                        break;
                    case ShortInlineIInstruction shortI:
                        if (instruction.OpCode == OpCodes.Ldc_I4_S)
                            ilGenerator.Emit(instruction.OpCode, (sbyte)shortI.Operand);
                        else
                            ilGenerator.Emit(instruction.OpCode, shortI.Operand);
                        break;
                    case InlineRInstruction r:
                        ilGenerator.Emit(instruction.OpCode, r.Operand);
                        break;
                    case ShortInlineRInstruction shortR:
                        ilGenerator.Emit(instruction.OpCode, shortR.Operand);
                        break;
                    case InlineStringInstruction str:
                        ilGenerator.Emit(instruction.OpCode, str.Operand);
                        break;
                    case ShortInlineBrTargetInstruction _:
                    case InlineBrTargetInstruction _:
                        EmitILForInlineBrTarget(ilGenerator, instruction, targetInstructions);
                        break;
                    case InlineSwitchInstruction switchInstruction:
                        EmitILForInlineSwitch(ilGenerator, switchInstruction, targetInstructions);
                        break;
                    case ShortInlineVarInstruction _:
                    case InlineVarInstruction _:
                        EmitILForInlineVar(ilGenerator, instruction, method.IsStatic);
                        break;
                    case InlineTokInstruction tok:
                        ilGenerator.Emit(instruction.OpCode, tok.Token);
                        break;
                    case InlineMethodInstruction methodInstruction:
                        if(methodInstruction.Method is ConstructorInfo constructorInfo)
                            EmitILForConstructor(ilGenerator, instruction, constructorInfo, parameterTypes.Count - 1);
                        else
                            EmitILForMethod(ilGenerator, instruction, (MethodInfo)methodInstruction.Method, context, parameterTypes.Count - 1);
                        break;
                    case InlineFieldInstruction field:
                        ilGenerator.Emit(instruction.OpCode, field.Operand);
                        break;
                    case InlineTypeInstruction type:
                        ilGenerator.Emit(instruction.OpCode, type.Operand);
                        break;
                    default:
                        throw new NotSupportedException();
                }
            }

            return dynamicMethod;
        }

        private static void EmitILForInlineBrTarget(ILGenerator ilGenerator,
            ILInstruction instruction, Dictionary<int, Label> targetInstructions)
        {
            var targetOffset = (instruction as ShortInlineBrTargetInstruction)?.TargetOffset ??
                               ((InlineBrTargetInstruction) instruction).TargetOffset;
            var targetLabel = targetInstructions[targetOffset];
            
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
            InlineSwitchInstruction instruction, Dictionary<int, Label> targetInstructions)
        {
            var targetLabels = new Label[instruction.TargetOffsets.Count];
            for (var i = 0; i < instruction.TargetOffsets.Count; i++)
                targetLabels[i] = targetInstructions[instruction.TargetOffsets[i]];
            ilGenerator.Emit(instruction.OpCode, targetLabels);
        }

        private static void EmitILForInlineVar(ILGenerator ilGenerator, ILInstruction instruction, bool isStatic)
        {
            var index = (instruction as ShortInlineVarInstruction)?.Ordinal ??
                               ((InlineVarInstruction)instruction).Ordinal;

            if (!isStatic &&
                (instruction.OpCode == OpCodes.Ldarg ||
                 instruction.OpCode == OpCodes.Ldarg_0 ||
                 instruction.OpCode == OpCodes.Ldarg_1 ||
                 instruction.OpCode == OpCodes.Ldarg_2 ||
                 instruction.OpCode == OpCodes.Ldarg_3 ||
                 instruction.OpCode == OpCodes.Ldarg_S ||
                 instruction.OpCode == OpCodes.Ldarga ||
                 instruction.OpCode == OpCodes.Ldarga_S ||
                 instruction.OpCode == OpCodes.Starg ||
                 instruction.OpCode == OpCodes.Starg_S
                ))
            {
                index++;
            }
            
            if (instruction.OpCode.OperandType == OperandType.ShortInlineVar)
                ilGenerator.Emit(instruction.OpCode, (byte) index);
            else
                ilGenerator.Emit(instruction.OpCode, (short) index);
        }

        private static void EmitILForConstructor(ILGenerator ilGenerator, ILInstruction instruction,
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

        private static void EmitILForMethod(ILGenerator ilGenerator, ILInstruction instruction, MethodInfo methodInfo, ShimContext context, int contextParamIndex)
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

            MethodInfo stub;
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
    }
}