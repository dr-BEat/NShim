using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.ILRewriter.Steps
{
    /// <summary>
    /// Remove Constrained followed by CallVirt instructions by transforming them into
    /// regular Call or CallVirt
    /// See https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.opcodes.constrained
    /// </summary>
    internal class RemoveConstrainedStep : IILRewriterStep
    {
        public void Rewriter(ILProcessor processor)
        {
            var constraineds = processor.Instructions
                                .OfType<OperandInstruction<Type>>()
                                .Where(i => i.OpCode == OpCodes.Constrained)
                                .ToList();

            foreach (var constrained in constraineds)
            {
                var constrainedType = constrained.Operand;
                var methodCall = (OperandInstruction<MethodBase>)constrained.Next;

                //Remove the Constrained Instruction
                processor.Remove(constrained);

                //Check for a quick fix
                if (constrainedType.IsValueType)
                {
                    //Check if the value type directly implements the method or not
                    var realMethod =
                        StubGenerator.GetRuntimeMethodForVirtual(constrainedType, (MethodInfo) methodCall.Operand);
                    if (realMethod.DeclaringType == constrainedType)
                    {
                        //the method is directly implemented, just replace the CallVirt with a normal Call
                        processor.Replace(methodCall,
                            OperandInstruction.Create<MethodBase>(OpCodes.Call, realMethod));

                        //For this case we are done!
                        continue;
                    }
                }

                //Find were the this pointer is pushed on the stack
                var thisInstruction = FindThisInstruction(methodCall);

                if (constrainedType.IsValueType) //the method is not directly implemented
                {
                    //ldobj struct
                    //box struct
                    //call virt

                    processor.InsertAfter(thisInstruction, new NoneInstruction(OpCodes.Box));
                    processor.InsertAfter(thisInstruction, new NoneInstruction(OpCodes.Ldobj));
                }
                else //this is a reference type
                {
                    //ldind_ref
                    //call virt
                    
                    processor.InsertAfter(thisInstruction, new NoneInstruction(OpCodes.Ldind_Ref));
                }
            }
        }

        /// <summary>
        /// Runs backwards through the instruction stream to find which instructions pushed the this pointer for this methodcall to the stack
        /// </summary>
        /// <param name="methodCall"></param>
        /// <returns></returns>
        private static ILProcessorInstruction FindThisInstruction(OperandInstruction<MethodBase> methodCall)
        {
            var numParameters = methodCall.Operand.GetParameters().Length;
            var stackPos = numParameters;
            var currentInstruction = methodCall.Prev;

            while (stackPos > 0 && currentInstruction != null)
            {
                stackPos += GetStackDelta(currentInstruction);

                currentInstruction = currentInstruction.Prev;
            }
            return currentInstruction;
        }

        /// <summary>
        /// Returns the overall effect of the given instruction on the stack.
        /// Negative numbers means the stack shrinks, positive the stack grows
        /// </summary>
        /// <param name="instruction"></param>
        /// <returns></returns>
        private static int GetStackDelta(ILProcessorInstruction instruction)
        {
            var method = (instruction as OperandInstruction<MethodBase>)?.Operand;
            var stackDelta = 0;
            switch (instruction.OpCode.StackBehaviourPop)
            {
                case StackBehaviour.Pop0:
                    break;
                case StackBehaviour.Pop1:
                case StackBehaviour.Popi:
                case StackBehaviour.Popref:
                    stackDelta -= 1;
                    break;
                case StackBehaviour.Pop1_pop1:
                case StackBehaviour.Popi_pop1:
                case StackBehaviour.Popi_popi:
                case StackBehaviour.Popi_popi8:
                case StackBehaviour.Popi_popr4:
                case StackBehaviour.Popi_popr8:
                case StackBehaviour.Popref_pop1:
                case StackBehaviour.Popref_popi:
                    stackDelta -= 2;
                    break;
                case StackBehaviour.Popi_popi_popi:
                case StackBehaviour.Popref_popi_pop1:
                case StackBehaviour.Popref_popi_popi:
                case StackBehaviour.Popref_popi_popi8:
                case StackBehaviour.Popref_popi_popr4:
                case StackBehaviour.Popref_popi_popr8:
                case StackBehaviour.Popref_popi_popref:
                    stackDelta -= 3;
                    break;
                case StackBehaviour.Varpop:
                    if (method?.IsStatic == false)
                        stackDelta -= 1;
                    stackDelta -= method?.GetParameters().Length ?? 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (instruction.OpCode.StackBehaviourPush)
            {
                case StackBehaviour.Push0:
                    break;
                case StackBehaviour.Push1:
                case StackBehaviour.Pushi:
                case StackBehaviour.Pushi8:
                case StackBehaviour.Pushr4:
                case StackBehaviour.Pushr8:
                case StackBehaviour.Pushref:
                    stackDelta += 1;
                    break;
                case StackBehaviour.Push1_push1:
                    stackDelta += 2;
                    break;
                case StackBehaviour.Varpush:
                    if ((method as MethodInfo)?.ReturnType != typeof(void))
                        stackDelta += 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return stackDelta;
        }
    }
}
