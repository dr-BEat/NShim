using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace NShim.ILRewriter.Steps
{
    /// <summary>
    /// A step that tries to replace branch instructions with there short form where possible.
    /// </summary>
    internal class OptimizeBranchesStep : IILRewriterStep
    {
        private static readonly Dictionary<OpCode, OpCode> OpCodesMap = new Dictionary<OpCode, OpCode>
        {
            { OpCodes.Br, OpCodes.Br_S },
            { OpCodes.Brfalse, OpCodes.Brfalse_S },
            { OpCodes.Brtrue, OpCodes.Brtrue_S },
            { OpCodes.Beq, OpCodes.Beq_S },
            { OpCodes.Bge, OpCodes.Bge_S },
            { OpCodes.Bgt, OpCodes.Bgt_S },
            { OpCodes.Ble, OpCodes.Ble_S },
            { OpCodes.Blt, OpCodes.Blt_S },
            { OpCodes.Bne_Un, OpCodes.Bne_Un_S },
            { OpCodes.Bge_Un, OpCodes.Bge_Un_S },
            { OpCodes.Bgt_Un, OpCodes.Bgt_Un_S },
            { OpCodes.Ble_Un, OpCodes.Ble_Un_S },
            { OpCodes.Blt_Un, OpCodes.Blt_Un_S },
            { OpCodes.Leave, OpCodes.Leave_S },
        };

        public void Rewriter(ILProcessor processor)
        {
            var branches = processor.Instructions
                                    .OfType<OperandInstruction<ILProcessorInstruction>>()
                                    .Where(i => OpCodesMap.ContainsKey(i.OpCode))
                                    .ToList();
            if(!branches.Any())
                return;

            var offsets = new Dictionary<ILProcessorInstruction, int>();
            var curOffset = 0;
            foreach (var instruction in processor.Instructions)
            {
                offsets.Add(instruction, curOffset);
                curOffset += GetInstructionSize(instruction);
            }

            while (branches.Any())
            {
                var branch = branches.FirstOrDefault(i =>
                {
                    var deltaOffset = offsets[i.Operand] - offsets[i.Next];
                    return deltaOffset >= sbyte.MinValue && deltaOffset <= sbyte.MaxValue;
                });
                if(branch == null)
                    break;
            
                var shortFormOpCode = OpCodesMap[branch.OpCode];
                var shortBranch = OperandInstruction.Create(shortFormOpCode, branch.Operand);
                processor.Replace(branch, shortBranch);

                //Update offsets
                ILProcessorInstruction loop = shortBranch;
                curOffset = offsets[branch];
                while (loop != null)
                {
                    offsets[loop] = curOffset;
                    curOffset += GetInstructionSize(loop);

                    loop = loop.Next;
                }

                offsets.Remove(branch);
                branches.Remove(branch);
            }
        }

        private static int GetInstructionSize(ILProcessorInstruction instruction)
        {
            var size = instruction.OpCode.Size;
            switch (instruction.OpCode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    size += 1;
                    break;
                case OperandType.InlineVar:
                    size += 2;
                    break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineI:
                case OperandType.ShortInlineR:
                case OperandType.InlineString:
                case OperandType.InlineSig:
                case OperandType.InlineMethod:
                case OperandType.InlineField:
                case OperandType.InlineType:
                case OperandType.InlineTok:
                    size += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    size += 8;
                    break;
                case OperandType.InlineSwitch:
                    size += 4 + 4*((OperandInstruction<IReadOnlyList<ILProcessorInstruction>>)instruction).Operand.Count;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return size;
        }
    }
}
