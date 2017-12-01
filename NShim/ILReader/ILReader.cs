using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace NShim.ILReader
{
    public sealed class ILReader : IEnumerable<ILInstruction>
    {
        #region Static members

        private static readonly Type RuntimeMethodInfoType = Type.GetType("System.Reflection.RuntimeMethodInfo");

        private static readonly Type RuntimeConstructorInfoType =
            Type.GetType("System.Reflection.RuntimeConstructorInfo");

        private static readonly OpCode[] OneByteOpCodes;
        private static readonly OpCode[] TwoByteOpCodes;

        static ILReader()
        {
            OneByteOpCodes = new OpCode[0x100];
            TwoByteOpCodes = new OpCode[0x100];

            foreach (var fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var opCode = (OpCode) fi.GetValue(null);
                var value = (ushort) opCode.Value;
                if (value < 0x100)
                {
                    OneByteOpCodes[value] = opCode;
                }
                else if ((value & 0xff00) == 0xfe00)
                {
                    TwoByteOpCodes[value & 0xff] = opCode;
                }
            }
        }

        #endregion

        private int _position;
        private readonly ITokenResolver _resolver;
        private readonly byte[] _byteArray;

        public ILReader(MethodBase method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            var rtType = method.GetType();
            if (rtType != RuntimeMethodInfoType && rtType != RuntimeConstructorInfoType)
            {
                throw new ArgumentException(
                    "method must be RuntimeMethodInfo or RuntimeConstructorInfo for this constructor.");
            }

            var ilProvider = new MethodBaseILProvider(method);
            _resolver = new ModuleScopeTokenResolver(method);
            _byteArray = ilProvider.GetByteArray();
            _position = 0;
        }

        public ILReader(IILProvider ilProvider, ITokenResolver tokenResolver)
        {
            if (ilProvider == null)
            {
                throw new ArgumentNullException("ilProvider");
            }

            _resolver = tokenResolver;
            _byteArray = ilProvider.GetByteArray();
            _position = 0;
        }

        public IEnumerator<ILInstruction> GetEnumerator()
        {
            while (_position < _byteArray.Length)
                yield return Next();

            _position = 0;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private ILInstruction Next()
        {
            var offset = _position;
            OpCode opCode;
            int token;

            // read first 1 or 2 bytes as opCode
            var code = ReadByte();
            if (code != 0xFE)
            {
                opCode = OneByteOpCodes[code];
            }
            else
            {
                code = ReadByte();
                opCode = TwoByteOpCodes[code];
            }

            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    return new InlineNoneInstruction(offset, opCode);

                //The operand is an 8-bit integer branch target.
                case OperandType.ShortInlineBrTarget:
                    var shortDelta = ReadSByte();
                    return new ShortInlineBrTargetInstruction(offset, opCode, shortDelta);

                //The operand is a 32-bit integer branch target.
                case OperandType.InlineBrTarget:
                    var delta = ReadInt32();
                    return new InlineBrTargetInstruction(offset, opCode, delta);

                //The operand is an 8-bit integer: 001F  ldc.i4.s, FE12  unaligned.
                case OperandType.ShortInlineI:
                    var int8 = ReadByte();
                    return new ShortInlineIInstruction(offset, opCode, int8);

                //The operand is a 32-bit integer.
                case OperandType.InlineI:
                    var int32 = ReadInt32();
                    return new InlineIInstruction(offset, opCode, int32);

                //The operand is a 64-bit integer.
                case OperandType.InlineI8:
                    var int64 = ReadInt64();
                    return new InlineI8Instruction(offset, opCode, int64);

                //The operand is a 32-bit IEEE floating point number.
                case OperandType.ShortInlineR:
                    var float32 = ReadSingle();
                    return new ShortInlineRInstruction(offset, opCode, float32);

                //The operand is a 64-bit IEEE floating point number.
                case OperandType.InlineR:
                    var float64 = ReadDouble();
                    return new InlineRInstruction(offset, opCode, float64);

                //The operand is an 8-bit integer containing the ordinal of a local variable or an argument
                case OperandType.ShortInlineVar:
                    var index8 = ReadByte();
                    return new ShortInlineVarInstruction(offset, opCode, index8);

                //The operand is 16-bit integer containing the ordinal of a local variable or an argument.
                case OperandType.InlineVar:
                    var index16 = ReadUInt16();
                    return new InlineVarInstruction(offset, opCode, index16);
                
                //The operand is a 32-bit metadata string token.
                case OperandType.InlineString:
                    token = ReadInt32();
                    return new InlineStringInstruction(offset, opCode, token, _resolver);

                //The operand is a 32-bit metadata signature token.
                case OperandType.InlineSig:
                    token = ReadInt32();
                    return new InlineSigInstruction(offset, opCode, token, _resolver);

                //The operand is a 32-bit metadata token.
                case OperandType.InlineMethod:
                    token = ReadInt32();
                    return new InlineMethodInstruction(offset, opCode, token, _resolver);

                //The operand is a 32-bit metadata token.
                case OperandType.InlineField:
                    token = ReadInt32();
                    return new InlineFieldInstruction(_resolver, offset, opCode, token);

                //The operand is a 32-bit metadata token.
                case OperandType.InlineType:
                    token = ReadInt32();
                    return new InlineTypeInstruction(offset, opCode, token, _resolver);

                //The operand is a FieldRef, MethodRef, or TypeRef token.
                case OperandType.InlineTok:
                    token = ReadInt32();
                    return new InlineTokInstruction(offset, opCode, token, _resolver);

                //The operand is the 32-bit integer argument to a switch instruction.
                case OperandType.InlineSwitch:
                    var cases = ReadInt32();
                    var deltas = new int[cases];
                    for (var i = 0; i < cases; i++)
                        deltas[i] = ReadInt32();
                    return new InlineSwitchInstruction(offset, opCode, deltas);
                
                default:
                    throw new BadImageFormatException("unexpected OperandType " + opCode.OperandType);
            }
        }

        public void Accept(ILInstructionVisitor visitor)
        {
            if (visitor == null)
                throw new ArgumentNullException("argument 'visitor' can not be null");

            foreach (var instruction in this)
            {
                instruction.Accept(visitor);
            }
        }

        #region read in operands

        private byte ReadByte()
        {
            return _byteArray[_position++];
        }

        private sbyte ReadSByte()
        {
            return (sbyte) ReadByte();
        }

        private ushort ReadUInt16()
        {
            var pos = _position;
            _position += 2;
            return BitConverter.ToUInt16(_byteArray, pos);
        }

        private uint ReadUInt32()
        {
            var pos = _position;
            _position += 4;
            return BitConverter.ToUInt32(_byteArray, pos);
        }

        private ulong ReadUInt64()
        {
            var pos = _position;
            _position += 8;
            return BitConverter.ToUInt64(_byteArray, pos);
        }

        private int ReadInt32()
        {
            var pos = _position;
            _position += 4;
            return BitConverter.ToInt32(_byteArray, pos);
        }

        private long ReadInt64()
        {
            var pos = _position;
            _position += 8;
            return BitConverter.ToInt64(_byteArray, pos);
        }

        private float ReadSingle()
        {
            var pos = _position;
            _position += 4;
            return BitConverter.ToSingle(_byteArray, pos);
        }

        private double ReadDouble()
        {
            var pos = _position;
            _position += 8;
            return BitConverter.ToDouble(_byteArray, pos);
        }

        #endregion
    }
}