using System;
using System.Reflection.Emit;

namespace NShim.ILRewriter
{
    public abstract class ILProcessorInstruction
    {
        internal ILProcessorInstruction(OpCode opCode)
        {
            OpCode = opCode;
        }
        
        public OpCode OpCode { get; }

        public ILProcessorInstruction Prev { get; internal set; }
        public ILProcessorInstruction Next { get; internal set; }
    }

    public class NoneProcessorInstruction : ILProcessorInstruction
    {
        internal NoneProcessorInstruction(OpCode opCode)
            : base(opCode)
        {
        }
    }

    public abstract class OperandProcessorInstruction : ILProcessorInstruction
    {
        public Type OperandType { get; }
        
        internal OperandProcessorInstruction(OpCode opCode, Type operandType)
            : base(opCode)
        {
            OperandType = operandType;
        }

        internal static OperandProcessorInstruction<T> Create<T>(OpCode opCode, T operand) =>
            new OperandProcessorInstruction<T>(opCode, operand);
    }

    public class OperandProcessorInstruction<T> : OperandProcessorInstruction
    {
        //public override object Operand { get; internal set; }
        public T Operand { get; internal set; }

        internal OperandProcessorInstruction(OpCode opCode, T operand)
            : base(opCode, typeof(T))
        {
            Operand = operand;
        }
    }
}