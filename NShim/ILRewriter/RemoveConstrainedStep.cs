using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.ILRewriter
{
    internal class RemoveConstrainedStep : IILRewriterStep
    {
        public void Rewriter(ILProcessor processor)
        {
            var constraineds = processor.Instructions
                                .OfType<OperandProcessorInstruction<Type>>()
                                .Where(i => i.OpCode == OpCodes.Constrained)
                                .ToList();

            foreach (var constrained in constraineds)
            {
                var methodCall = (OperandProcessorInstruction<MethodBase>)constrained.Next;

                var numParameters = methodCall.Operand.GetParameters().Length;

                if (true /*this value type and implements method*/)
                {
                    //call
                    processor.Replace(methodCall, OperandProcessorInstruction.Create(OpCodes.Call, methodCall.Operand));
                }
                else if (true /*this is value type and does not implement method*/)
                {
                    //deref
                    //box
                    //callvirt
                }
                else //this is reference typed
                {
                    //deref
                    //callvirt
                }

                processor.Remove(constrained);


            }
        }
    }
}
