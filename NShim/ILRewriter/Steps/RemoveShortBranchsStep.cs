using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace NShim.ILRewriter.Steps
{
    /// <summary>
    /// Offset values could change and not be short form anymore
    /// </summary>
    internal class RemoveShortBranchsStep : IILRewriterStep
    {
        private static readonly Dictionary<OpCode, OpCode> OpCodesMap = new Dictionary<OpCode, OpCode>
        {
            { OpCodes.Br_S, OpCodes.Br },
            { OpCodes.Brfalse_S, OpCodes.Brfalse },
            { OpCodes.Brtrue_S, OpCodes.Brtrue },
            { OpCodes.Beq_S, OpCodes.Beq },
            { OpCodes.Bge_S, OpCodes.Bge },
            { OpCodes.Bgt_S, OpCodes.Bgt },
            { OpCodes.Ble_S, OpCodes.Ble },
            { OpCodes.Blt_S, OpCodes.Blt },
            { OpCodes.Bne_Un_S, OpCodes.Bne_Un },
            { OpCodes.Bge_Un_S, OpCodes.Bge_Un },
            { OpCodes.Bgt_Un_S, OpCodes.Bgt_Un },
            { OpCodes.Ble_Un_S, OpCodes.Ble_Un },
            { OpCodes.Blt_Un_S, OpCodes.Blt_Un },
            { OpCodes.Leave_S, OpCodes.Leave },
        };

        public void Rewriter(ILProcessor processor)
        {
            var branches = processor.Instructions
                                    .OfType<OperandInstruction<ILProcessorInstruction>>()
                                    .Where(i => OpCodesMap.ContainsKey(i.OpCode))
                                    .ToList();

            foreach (var instruction in branches)
            {
                var longFormOpCode = OpCodesMap[instruction.OpCode];
                processor.Replace(instruction, OperandInstruction.Create(longFormOpCode, instruction.Operand));
            }
        }
    }
}
