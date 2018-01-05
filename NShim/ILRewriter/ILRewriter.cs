using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NShim.Helpers;
using NShim.ILRewriter.Steps;

namespace NShim.ILRewriter
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

            var targetInstructions = new Dictionary<ILProcessorInstruction, Label>();

            foreach (var local in method.GetMethodBody().LocalVariables)
            {
                ilGenerator.DeclareLocal(local.LocalType, local.IsPinned);
            }

            var ilReader = new ILReader.ILReader(method);
            var instructions = ilReader.ToList();
            var processor = ILProcessor.FromILInstructions(instructions);

            var steps = new[]
            {
                new RemoveConstrainedStep(),
            };

            foreach (var step in steps)
            {
                step.Rewriter(processor);
            }
            
            foreach (var instruction in processor.Instructions)
            {
                switch (instruction)
                {
                    case OperandProcessorInstruction<ILProcessorInstruction> branchInstruction:
                        targetInstructions.TryAdd(branchInstruction.Operand, ilGenerator.DefineLabel);
                        break;
                    case OperandProcessorInstruction<IReadOnlyList<ILProcessorInstruction>> switchInstruction:
                        foreach (var target in switchInstruction.Operand)
                        {
                            targetInstructions.TryAdd(target, ilGenerator.DefineLabel);
                        }
                        break;
                }
            }

            foreach (var instruction in processor.Instructions)
            {
                if (targetInstructions.TryGetValue(instruction, out var label))
                    ilGenerator.MarkLabel(label);
                var opCode = instruction.OpCode;
                switch (instruction)
                {
                    case NoneProcessorInstruction _:
                        ilGenerator.Emit(opCode);
                        break;

                    case OperandProcessorInstruction<byte> i when i.OpCode.OperandType == OperandType.ShortInlineVar:
                        EmitILForInlineVar(ilGenerator, instruction, method.IsStatic);
                        break;
                    case OperandProcessorInstruction<ushort> i when i.OpCode.OperandType == OperandType.InlineVar:
                        EmitILForInlineVar(ilGenerator, instruction, method.IsStatic);
                        break;
                    
                    case OperandProcessorInstruction<byte> i:
                        if (instruction.OpCode == OpCodes.Ldc_I4_S)
                            ilGenerator.Emit(opCode, (sbyte)i.Operand);
                        else
                            ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<short> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<int> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<long> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<float> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<double> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<string> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<FieldInfo> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandProcessorInstruction<Type> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    /*case OperandProcessorInstruction<MemberInfo> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;*/

                    case OperandProcessorInstruction<ILProcessorInstruction> i:
                        EmitILForInlineBrTarget(ilGenerator, i, targetInstructions);
                        break;
                    case OperandProcessorInstruction<IReadOnlyList<ILProcessorInstruction>> i:
                        ilGenerator.Emit(i.OpCode, i.Operand.Select(t => targetInstructions[t]).ToArray());
                        break;

                    case OperandProcessorInstruction<MethodBase> i:
                        if (i.Operand is ConstructorInfo constructorInfo)
                            EmitILForConstructor(ilGenerator, i, constructorInfo, parameterTypes.Count - 1);
                        else
                            EmitILForMethod(ilGenerator, i, (MethodInfo)i.Operand, context, parameterTypes.Count - 1);
                        break;

                    /*case InlineTokInstruction tok:
                        ilGenerator.Emit(instruction.OpCode, tok.Token);
                        break;*/
                    default:
                        throw new NotSupportedException();
                }
            }

            return dynamicMethod;
        }

        private static void EmitILForInlineBrTarget(ILGenerator ilGenerator,
            OperandProcessorInstruction<ILProcessorInstruction> instruction, Dictionary<ILProcessorInstruction, Label> targetInstructions)
        {
            var targetLabel = targetInstructions[instruction.Operand];
            
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

        private static void EmitILForInlineVar(ILGenerator ilGenerator, ILProcessorInstruction instruction, bool isStatic)
        {
            var index = (instruction as OperandProcessorInstruction<ushort>)?.Operand ??
                        (instruction as OperandProcessorInstruction<byte>)?.Operand ??
                        throw new ArgumentException();

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

        private static void EmitILForConstructor(ILGenerator ilGenerator, OperandProcessorInstruction<MethodBase> instruction,
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

        private static void EmitILForMethod(ILGenerator ilGenerator, OperandProcessorInstruction<MethodBase> instruction, MethodInfo methodInfo, ShimContext context, int contextParamIndex)
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