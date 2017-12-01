using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.ILReader
{
    public abstract class ILInstruction
    {
        internal ILInstruction(int offset, OpCode opCode)
        {
            Offset = offset;
            OpCode = opCode;
        }

        public int Offset { get; }

        public OpCode OpCode { get; }

        public abstract void Accept(ILInstructionVisitor vistor);
    }

    public class InlineNoneInstruction : ILInstruction
    {
        internal InlineNoneInstruction(int offset, OpCode opCode)
            : base(offset, opCode)
        {
        }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineNoneInstruction(this);
        }
    }

    public class InlineBrTargetInstruction : ILInstruction
    {
        internal InlineBrTargetInstruction(int offset, OpCode opCode, int delta)
            : base(offset, opCode)
        {
            Delta = delta;
        }

        public int Delta { get; }

        public int TargetOffset => Offset + Delta + 1 + 4;

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineBrTargetInstruction(this);
        }
    }

    public class ShortInlineBrTargetInstruction : ILInstruction
    {
        internal ShortInlineBrTargetInstruction(int offset, OpCode opCode, sbyte delta)
            : base(offset, opCode)
        {
            Delta = delta;
        }

        public sbyte Delta { get; }

        public int TargetOffset => Offset + Delta + 1 + 1;

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitShortInlineBrTargetInstruction(this);
        }
    }

    public class InlineSwitchInstruction : ILInstruction
    {
        private IReadOnlyList<int> _targetOffsets;

        internal InlineSwitchInstruction(int offset, OpCode opCode, int[] deltas)
            : base(offset, opCode)
        {
            Deltas = deltas;
        }

        public IReadOnlyList<int> Deltas { get; }

        public IReadOnlyList<int> TargetOffsets
        {
            get
            {
                if (_targetOffsets == null)
                {
                    var cases = Deltas.Count;
                    var itself = 1 + 4 + 4 * cases;
                    var targetOffsets = new int[cases];
                    for (var i = 0; i < cases; i++)
                        targetOffsets[i] = Offset + Deltas[i] + itself;
                    _targetOffsets = targetOffsets;
                }
                return _targetOffsets;
            }
        }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineSwitchInstruction(this);
        }
    }

    public class InlineIInstruction : ILInstruction
    {
        internal InlineIInstruction(int offset, OpCode opCode, int value)
            : base(offset, opCode)
        {
            Operand = value;
        }

        public int Operand { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineIInstruction(this);
        }
    }

    public class InlineI8Instruction : ILInstruction
    {
        internal InlineI8Instruction(int offset, OpCode opCode, long value)
            : base(offset, opCode)
        {
            Operand = value;
        }

        public long Operand { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineI8Instruction(this);
        }
    }

    public class ShortInlineIInstruction : ILInstruction
    {
        internal ShortInlineIInstruction(int offset, OpCode opCode, byte value)
            : base(offset, opCode)
        {
            Operand = value;
        }

        public byte Operand { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitShortInlineIInstruction(this);
        }
    }

    public class InlineRInstruction : ILInstruction
    {
        internal InlineRInstruction(int offset, OpCode opCode, double value)
            : base(offset, opCode)
        {
            Operand = value;
        }

        public double Operand { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineRInstruction(this);
        }
    }

    public class ShortInlineRInstruction : ILInstruction
    {
        internal ShortInlineRInstruction(int offset, OpCode opCode, float value)
            : base(offset, opCode)
        {
            Operand = value;
        }

        public float Operand { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitShortInlineRInstruction(this);
        }
    }

    public class InlineFieldInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private FieldInfo _field;

        internal InlineFieldInstruction(ITokenResolver resolver, int offset, OpCode opCode, int token)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public FieldInfo Operand
        {
            get
            {
                if (_field == null)
                {
                    _field = _resolver.AsField(Token);
                }
                return _field;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineFieldInstruction(this);
        }
    }

    public class InlineMethodInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private MethodBase _method;

        internal InlineMethodInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public MethodBase Method
        {
            get
            {
                if (_method == null)
                {
                    _method = _resolver.AsMethod(Token);
                }
                return _method;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineMethodInstruction(this);
        }
    }

    public class InlineTypeInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private Type _type;

        internal InlineTypeInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public Type Operand
        {
            get
            {
                if (_type == null)
                {
                    _type = _resolver.AsType(Token);
                }
                return _type;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineTypeInstruction(this);
        }
    }

    public class InlineSigInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private byte[] _signature;

        internal InlineSigInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public byte[] Signature
        {
            get
            {
                if (_signature == null)
                {
                    _signature = _resolver.AsSignature(Token);
                }
                return _signature;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineSigInstruction(this);
        }
    }

    public class InlineTokInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private MemberInfo _member;

        internal InlineTokInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public MemberInfo Member
        {
            get
            {
                if (_member == null)
                {
                    _member = _resolver.AsMember(Token);
                }
                return _member;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineTokInstruction(this);
        }
    }

    public class InlineStringInstruction : ILInstruction
    {
        private readonly ITokenResolver _resolver;
        private string _string;

        internal InlineStringInstruction(int offset, OpCode opCode, int token, ITokenResolver resolver)
            : base(offset, opCode)
        {
            _resolver = resolver;
            Token = token;
        }

        public string Operand
        {
            get
            {
                if (_string == null) _string = _resolver.AsString(Token);
                return _string;
            }
        }

        public int Token { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineStringInstruction(this);
        }
    }

    public class InlineVarInstruction : ILInstruction
    {
        internal InlineVarInstruction(int offset, OpCode opCode, ushort ordinal)
            : base(offset, opCode)
        {
            Ordinal = ordinal;
        }

        public ushort Ordinal { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitInlineVarInstruction(this);
        }
    }

    public class ShortInlineVarInstruction : ILInstruction
    {
        internal ShortInlineVarInstruction(int offset, OpCode opCode, byte ordinal)
            : base(offset, opCode)
        {
            Ordinal = ordinal;
        }

        public byte Ordinal { get; }

        public override void Accept(ILInstructionVisitor vistor)
        {
            vistor.VisitShortInlineVarInstruction(this);
        }
    }
}