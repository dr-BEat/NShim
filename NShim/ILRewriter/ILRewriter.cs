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

            var steps = new IILRewriterStep[]
            {
                new RemoveShortBranchsStep(),
                new RemoveConstrainedStep(),
                new OptimizeBranchesStep(), 
            };

            foreach (var step in steps)
            {
                step.Rewriter(processor);
            }
            
            foreach (var instruction in processor.Instructions)
            {
                switch (instruction)
                {
                    case OperandInstruction<ILProcessorInstruction> branchInstruction:
                        targetInstructions.TryAdd(branchInstruction.Operand, ilGenerator.DefineLabel);
                        break;
                    case OperandInstruction<IReadOnlyList<ILProcessorInstruction>> switchInstruction:
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
                    case NoneInstruction _:
                        ilGenerator.Emit(opCode);
                        break;

                    case OperandInstruction<byte> bi when bi.OpCode.OperandType == OperandType.ShortInlineVar:
                    case OperandInstruction<ushort> ui when ui.OpCode.OperandType == OperandType.InlineVar:
                        EmitILForInlineVar(ilGenerator, instruction, method.IsStatic);
                        break;
                    
                    case OperandInstruction<byte> i:
                        if (instruction.OpCode == OpCodes.Ldc_I4_S)
                            ilGenerator.Emit(opCode, (sbyte)i.Operand);
                        else
                            ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<short> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<int> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<long> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<float> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<double> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<string> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<FieldInfo> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    case OperandInstruction<Type> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;
                    /*case OperandInstruction<MemberInfo> i:
                        ilGenerator.Emit(opCode, i.Operand);
                        break;*/

                    case OperandInstruction<ILProcessorInstruction> i:
                        ilGenerator.Emit(i.OpCode, targetInstructions[i.Operand]);
                        break;
                    case OperandInstruction<IReadOnlyList<ILProcessorInstruction>> i:
                        ilGenerator.Emit(i.OpCode, i.Operand.Select(t => targetInstructions[t]).ToArray());
                        break;

                    case OperandInstruction<MethodBase> i:
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

        private static void EmitILForInlineVar(ILGenerator ilGenerator, ILProcessorInstruction instruction, bool isStatic)
        {
            var index = (instruction as OperandInstruction<ushort>)?.Operand ??
                        (instruction as OperandInstruction<byte>)?.Operand ??
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

        private static void EmitILForConstructor(ILGenerator ilGenerator, OperandInstruction<MethodBase> instruction,
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

        private static void EmitILForMethod(ILGenerator ilGenerator, OperandInstruction<MethodBase> instruction, MethodInfo methodInfo, ShimContext context, int contextParamIndex)
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