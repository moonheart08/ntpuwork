using System;
namespace NTPUwork
{
	public partial class NTPUCPU
	{
        private void InstrAdd(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val + dest_val);
            var sresult = (Int16)src_val + (Int16)dest_val;
            _flag_carry = ((uint)src_val + (uint)dest_val) > ushort.MaxValue;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrSub(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val - dest_val);
            var sresult = (Int16)src_val - (Int16)dest_val;
            // this probably doesn't work. TODO: test.
            _flag_carry = ((src_val - dest_val) & 0xFFFF0000) != 0;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrAnd(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val & dest_val);
        }

        private void InstrOr(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val | dest_val);
        }

        private void InstrXor(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val ^ dest_val);
        }

        private void InstrShl(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val << (dest_val & 15));
            _flag_carry = ((ushort)(src_val << (dest_val - 1 & 15)) & 0x8000) != 0;
            _flag_overflow = dest_val > 15;
        }

        private void InstrShr(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = (ushort)(src_val >> (dest_val & 15));
            _flag_carry = ((ushort)(src_val >> (dest_val - 1 & 15)) & 0x1) != 0;
            _flag_overflow = dest_val > 15;
        }

        private void InstrCmp(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = dest_val;
            _flag_zero |= (dest_val - src_val) == 0;
            _flag_neg |= (dest_val - src_val) >= 0x7FFF;
            var sresult = (Int16)src_val - (Int16)dest_val;
            // this probably doesn't work. TODO: test.
            _flag_carry = ((src_val - dest_val) & 0xFFFF0000) != 0;
            _flag_overflow = (sresult < -0x8000) | (sresult > 0x7FFF);
        }

        private void InstrTst(ushort src_val, ushort dest_val, out ushort dest_write)
        {
            dest_write = dest_val;
            _flag_zero |= (dest_val & src_val) == 0;
            _flag_neg |= (dest_val & src_val) >= 0x7FFF;
        }

        private void InstrLdpr(ushort instr)
        {
            byte prsel = (byte)((instr & 0x1F00) >> 8);
            byte src = (byte)((instr & 0xE000) >> 13);
            byte opcode = (byte)(instr & 0xFF);
            if (opcode == 0xFA)
            {
                var data = _registers[src];
                Console.WriteLine("Writing PR {0}", prsel);
                switch (prsel)
                {
                    case 0: // sptrl
                        _sys_page_table_r = (_sys_page_table_r & 0xFF0000) | data;
                        break;
                    case 1: // sptrh
                        _sys_page_table_r = (_sys_page_table_r & 0x00FFFF) | ((uint)(data & 0xFF) << 16);
                        break;
                    case 2: // sptwl
                        _sys_page_table_w = (_sys_page_table_r & 0xFF0000) | data;
                        break;
                    case 3: // sptwh
                        _sys_page_table_w = (_sys_page_table_r & 0x00FFFF) | ((uint)(data & 0xFF) << 16);
                        break;
                    case 4: // uptrl
                        _user_page_table_r = (_sys_page_table_r & 0xFF0000) | data;
                        break;
                    case 5: // uptrh
                        _user_page_table_r = (_sys_page_table_r & 0x00FFFF) | ((uint)(data & 0xFF) << 16);
                        break;
                    case 6: // uptwl
                        _user_page_table_w = (_sys_page_table_r & 0xFF0000) | data;
                        break;
                    case 7: // uptwh
                        _user_page_table_w = (_sys_page_table_r & 0x00FFFF) | ((uint)(data & 0xFF) << 16);
                        break;
                    case 8:
                    case 9:
                        // No, you can't write to lrst
                        break;
                    case 10:
                        _syscall_entry = data;
                        break;
                    case 11:
                        Console.WriteLine("b");
                        _pt_enable = (data & 0x1) != 0;
                        break;
                    case 12: // itrpt not writable.
                        break;
                    case 31: // flags.
                        _flag_zero = (data & 0x1) != 0;
                        _flag_neg = (data & 0x2) != 0;
                        _flag_carry = (data & 0x4) != 0;
                        _flag_overflow = (data & 0x8) != 0;
                        break;
                }
            }
            else if (opcode == 0xFB)
            {
                ushort data_out;
                switch (prsel)
                {
                    case 0:
                        data_out = (ushort)(_sys_page_table_r & 0xFFFF);
                        break;
                    case 1:
                        data_out = (ushort)((_sys_page_table_r & 0xFF0000) >> 16);
                        break;
                    case 2:
                        data_out = (ushort)(_sys_page_table_w & 0xFFFF);
                        break;
                    case 3:
                        data_out = (ushort)((_sys_page_table_w & 0xFF0000) >> 16);
                        break;
                    case 4:
                        data_out = (ushort)(_user_page_table_r & 0xFFFF);
                        break;
                    case 5:
                        data_out = (ushort)((_user_page_table_r & 0xFF0000) >> 16);
                        break;
                    case 6:
                        data_out = (ushort)(_user_page_table_w & 0xFFFF);
                        break;
                    case 7:
                        data_out = (ushort)((_user_page_table_w & 0xFF0000) >> 16);
                        break;
                    case 8:
                        data_out = (ushort)(_last_reset_addr & 0xFFFF);
                        break;
                    case 9:
                        data_out = (ushort)((_last_reset_addr & 0xFF0000) >> 16);
                        break;
                    case 10:
                        data_out = _syscall_entry;
                        break;
                    case 11:
                        data_out = (ushort)(_pt_enable ? 1 : 0);
                        break;
                    case 12:
                        data_out = _active_interrupt; // how tf does this work but other casts don't
                        break;
                    case 31:
                        data_out = 0;
                        data_out |= (ushort)(_flag_zero ? 0x1 : 0x0);
                        data_out |= (ushort)(_flag_neg ? 0x2 : 0x0);
                        data_out |= (ushort)(_flag_carry ? 0x4 : 0x0);
                        data_out |= (ushort)(_flag_overflow ? 0x8 : 0x0);
                        break;
                    default:
                        data_out = 0;
                        break;
                }
                _registers[src] = data_out;
            }
            else
            {
                throw new InvalidOperationException(); // Impossible to reach here unless ExecuteInstruction is bugged.
            }
        }

        private void InstrCall(byte dest)
        {
            ushort imm = ReadMemoryPaged((ushort)(_pc + 1));
            ushort jmp = (ushort)(_pc + imm + 2);
            Console.WriteLine("CALL: {0:x4}", jmp);

            _registers[dest] -= 1; //sub
            WriteMemoryPaged(_registers[dest], (ushort)(_pc + 2)); // push
            _pc = jmp;
        }
        private void InstrRet(byte dest)
        {
            ushort retaddr = ReadMemoryPaged(_registers[dest]);
            _registers[dest] += 1;
            _pc = retaddr;
            Console.WriteLine("RET: {0:x4}", retaddr);
        }

        private void InstrJCC(byte opcode, byte upbyte, byte src)
        {
            ushort target_address;
            if (opcode == 0xFD)
            {
                var imm = ReadMemoryPaged((ushort)(_pc + 1));
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
                _pc = (ushort)(target_address);
            }
        }
	}
}
