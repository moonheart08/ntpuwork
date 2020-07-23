using System;
namespace NTPUwork
{
	public partial class NTPUCPU
	{
        private void InstrAdd(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val + dest_val);
            var sresult = (Int16)src_val + (Int16)dest_val;
            _flag_carry = ((UInt32)src_val + (UInt32)dest_val) > UInt16.MaxValue;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrSub(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val - dest_val);
            var sresult = (Int16)src_val - (Int16)dest_val;
            // this probably doesn't work. TODO: test.
            _flag_carry = ((src_val - dest_val) & 0xFFFF0000) != 0;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrAnd(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val & dest_val);
        }

        private void InstrOr(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val | dest_val);
        }

        private void InstrXor(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val ^ dest_val);
        }

        private void InstrShl(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val << (dest_val & 15));
            _flag_carry = ((UInt16)(src_val << (dest_val - 1 & 15)) & 0x8000) != 0;
            _flag_overflow = dest_val > 15;
        }

        private void InstrShr(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = (UInt16)(src_val >> (dest_val & 15));
            _flag_carry = ((UInt16)(src_val >> (dest_val - 1 & 15)) & 0x1) != 0;
            _flag_overflow = dest_val > 15;
        }

        private void InstrCmp(UInt16 src_val, UInt16 dest_val, out UInt16 dest_write)
        {
            dest_write = dest_val;
            var sresult = (Int16)src_val - (Int16)dest_val;
            // this probably doesn't work. TODO: test.
            _flag_carry = ((src_val - dest_val) & 0xFFFF0000) != 0;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrJCC(byte opcode, byte upbyte, byte src)
        {
            UInt16 target_address;
            if (opcode == 0xFD)
            {
                var imm = ReadMemorySegmented((UInt16)(_pc + 1));
                _pc += 1;
                target_address = imm;
            }
            else
            {
                target_address = _registers[src];
            }
            bool will_jmp = false;
            switch ((upbyte & 0xF0) >> 4)
            {
                case 0:
                    will_jmp = true;
                    break;
                case 1:
                    will_jmp |= _flag_carry;
                    break;
                case 2:
                    will_jmp |= !_flag_carry;
                    break;
                case 3:
                    will_jmp |= _flag_zero;
                    break;
                case 4:
                    will_jmp |= !_flag_zero;
                    break;
                case 5:
                    will_jmp |= _flag_overflow;
                    break;
                case 6:
                    will_jmp |= !_flag_overflow;
                    break;
                case 7:
                    will_jmp |= _flag_neg;
                    break;
                case 8:
                    will_jmp |= !_flag_neg;
                    break;
                case 9:
                    will_jmp |= _flag_neg | _flag_zero;
                    break;
                case 10:
                    will_jmp |= !(_flag_neg | _flag_zero);
                    break;
                case 11:
                    will_jmp |= _flag_carry | _flag_zero;
                    break;
                case 12:
                    will_jmp |= !(_flag_carry | _flag_zero);
                    break;
                default:
                    will_jmp = false;
                    break;
            }

            if (will_jmp)
            {
                Console.WriteLine("JMP: {0:x4}", target_address);
                _pc = (UInt16)(target_address - 1);
            }
        }
	}
}
