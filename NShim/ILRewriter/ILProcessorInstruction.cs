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

    public class NoneInstruction : ILProcessorInstruction
    {
        internal NoneInstruction(OpCode opCode)
            : base(opCode)
        {
        }
    }

    public abstract class OperandInstruction : ILProcessorInstruction
    {
        public Type OperandType { get; }
        
        internal OperandInstruction(OpCode opCode, Type operandType)
            : base(opCode)
        {
            OperandType = operandType;
        }

        internal static OperandInstruction<T> Create<T>(OpCode opCode, T operand) =>
            new OperandInstruction<T>(opCode, operand);
    }

    public class OperandInstruction<T> : OperandInstruction
    {
        //public override object Operand { get; internal set; }
        public T Operand { get; internal set; }

        internal OperandInstruction(OpCode opCode, T operand)
            : base(opCode, typeof(T))
        {
            Operand = operand;
        }
    }
}