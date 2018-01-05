using System;
using System.Collections.Generic;
using System.Linq;
using NShim.ILReader;

namespace NShim.ILRewriter
{
    internal class ILProcessor
    {
        private readonly List<ILProcessorInstruction> _instructions;

        public ILProcessor(List<ILProcessorInstruction> instructions)
        {
            _instructions = instructions;
        }

        public static ILProcessor FromILInstructions(IReadOnlyList<ILInstruction> instructions)
        {
            return new ILProcessor(TranslateInstructions(instructions));
        }

        public IReadOnlyList<ILProcessorInstruction> Instructions => _instructions;

        private static List<ILProcessorInstruction> TranslateInstructions(IReadOnlyList<ILInstruction> instructions)
        {
            //Convert all instructions into processor instructions
            var offsetMap = new Dictionary<int, ILProcessorInstruction>();
            var newInstructions = new List<ILProcessorInstruction>();

            ILProcessorInstruction lastInstruction = null;
            foreach (var instruction in instructions)
            {
                var processorInstruction = TranslateInstruction(instruction);
                newInstructions.Add(processorInstruction);
                offsetMap.Add(instruction.Offset, processorInstruction);

                if (lastInstruction != null)
                    lastInstruction.Next = processorInstruction;
                processorInstruction.Prev = lastInstruction;
                lastInstruction = processorInstruction;
            }

            //Set the correct TargetInstruction for Branch and Switches
            foreach (var (instruction, oldInstruction) in newInstructions.Zip(instructions,
                (i, oi) => (Instruction: i, OldInstruction:oi)))
            {
                switch (instruction)
                {
                    case OperandInstruction<ILProcessorInstruction> branchInstruction:
                        var targetOffset = (oldInstruction as InlineBrTargetInstruction)?.TargetOffset ??
                                           (oldInstruction as ShortInlineBrTargetInstruction)?.TargetOffset ??
                                           throw new ArgumentException();
                        branchInstruction.Operand = offsetMap[targetOffset];
                        break;
                    case OperandInstruction<IReadOnlyList<ILProcessorInstruction>> switchInstruction:
                        var targetOffsets = (oldInstruction as InlineSwitchInstruction)?.TargetOffsets ??
                                            throw new ArgumentException();
                        switchInstruction.Operand = targetOffsets.Select(o => offsetMap[o]).ToList();
                        break;
                }
            }
            return newInstructions;

            ILProcessorInstruction TranslateInstruction(ILInstruction instruction)
            {
                var opCode = instruction.OpCode;
                switch (instruction)
                {
                    case InlineNoneInstruction _:
                        return new NoneInstruction(opCode);
                    case ShortInlineBrTargetInstruction _:
                    case InlineBrTargetInstruction _:
                        return OperandInstruction.Create(opCode, (ILProcessorInstruction)null);
                    case InlineSwitchInstruction _:
                        return OperandInstruction.Create(opCode, (IReadOnlyList<ILProcessorInstruction>)null);
                    case InlineFieldInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case InlineI8Instruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case InlineIInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case InlineMethodInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Method);
                    case InlineRInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case InlineSigInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Signature);
                    case InlineStringInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case InlineTokInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Member);
                    case InlineTypeInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);

                    case ShortInlineVarInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Ordinal);
                    case InlineVarInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Ordinal);

                    case ShortInlineIInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    case ShortInlineRInstruction matched:
                        return OperandInstruction.Create(opCode, matched.Operand);
                    default:
                        throw new ArgumentException("Unknown instruction!", nameof(instruction));
                }
            }
        }
        
        public void InsertBefore(ILProcessorInstruction target, ILProcessorInstruction instruction)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            var index = _instructions.IndexOf(target);
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(target));

            _instructions.Insert(index, instruction);
            
            instruction.Prev = target.Prev;
            instruction.Next = target;

            if (instruction.Prev != null)
                instruction.Prev.Next = instruction;
            if (instruction.Next != null)
                instruction.Next.Prev = instruction;
        }

        public void InsertAfter(ILProcessorInstruction target, ILProcessorInstruction instruction)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            var index = _instructions.IndexOf(target);
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(target));

            _instructions.Insert(index + 1, instruction);

            instruction.Prev = target;
            instruction.Next = target.Next;

            if (instruction.Prev != null)
                instruction.Prev.Next = instruction;
            if (instruction.Next != null)
                instruction.Next.Prev = instruction;
        }

        public void Append(ILProcessorInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            _instructions.Add(instruction);

            instruction.Prev = _instructions.ElementAtOrDefault(_instructions.Count - 2);

            if (instruction.Prev != null)
                instruction.Prev.Next = instruction;
        }

        public void Replace(ILProcessorInstruction target, ILProcessorInstruction instruction)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));

            var index = _instructions.IndexOf(target);
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(target));
            
            _instructions[index] = instruction;

            instruction.Prev = target.Prev;
            instruction.Next = target.Next;

            if (instruction.Prev != null)
                instruction.Prev.Next = instruction;
            if (instruction.Next != null)
                instruction.Next.Prev = instruction;

            FixupBranchAndSwitch(target, instruction);
        }

        public void Remove(ILProcessorInstruction instruction)
        {
            if (instruction == null)
                throw new ArgumentNullException(nameof(instruction));
            var index = _instructions.IndexOf(instruction);
            if (index == -1)
                throw new ArgumentOutOfRangeException(nameof(instruction));
            _instructions.RemoveAt(index);

            if (instruction.Prev != null)
                instruction.Prev.Next = instruction.Next;
            if (instruction.Next != null)
                instruction.Next.Prev = instruction.Prev;

            FixupBranchAndSwitch(instruction, _instructions.ElementAtOrDefault(index) ??
                                              _instructions.ElementAtOrDefault(index - 1));
        }

        private void FixupBranchAndSwitch(ILProcessorInstruction oldInstruction, ILProcessorInstruction newInstruction)
        {
            //Fix up any branch or switch instructions
            foreach (var inst in _instructions)
            {
                switch (inst)
                {
                    case OperandInstruction<ILProcessorInstruction> branchInstruction
                    when branchInstruction.Operand == oldInstruction:
                        branchInstruction.Operand = newInstruction;
                        break;
                    case OperandInstruction<IReadOnlyList<ILProcessorInstruction>> switchInstruction
                    when switchInstruction.Operand.Contains(oldInstruction):
                        switchInstruction.Operand = switchInstruction.Operand
                                         .Select(i => i != oldInstruction ? i : newInstruction)
                                         .ToList();
                        break;
                }
            }
        }
    }
}
