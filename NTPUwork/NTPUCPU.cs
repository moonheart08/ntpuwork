using System;
using System.Collections.Generic;
namespace NTPUwork
{

    public partial class NTPUCPU
    {
        private const uint ROM_BEGIN = 0xFE0000;

        private enum NTPUMode
        {
            System,
            User,
        }

        private ushort[] _memory;

        private ushort[] _rom;

        private ushort[] _registers;

        private uint _user_page_table_r;
        private uint _user_page_table_w;
        private uint _sys_page_table_r;
        private uint _sys_page_table_w;

        private ushort _pc;

        private NTPUMode _sys_curr_mode;

        // Controls if the system will recognize interrupts. If interrupts are
        // enabeled
        private bool _interrupt_enable;

        private bool _pt_enable;

        private byte _active_interrupt;
        private uint _interrupt_stack;

        private ushort _syscall_entry;

        private bool _flag_neg;
        private bool _flag_zero;
        private bool _flag_overflow;
        private bool _flag_carry;

        private bool _encountered_bad_mem_access;

        private uint _last_reset_addr;
        private ushort _last_reset_addr_virt;


        public bool Halted;

        public NTPUCPU(uint ram_size, ushort[] rom)
        {
            _memory = new ushort[ram_size];
            _rom = rom;
            _registers = new ushort[8]; // PC stored seperately.
            _pc = 0x0000;
            _interrupt_enable = false;
            _pt_enable = false;
            _sys_curr_mode = NTPUMode.System;
            Halted = false;
            _active_interrupt = 0;
            _flag_neg = false;
            _flag_zero = false;
            _flag_carry = false;
            _flag_overflow = false;
            _last_reset_addr = 0;
            _interrupt_stack = 0;
        }

        public ushort ReadMemory(uint address)
        {

            ushort data;
            if (address > 0x007FFF && address < _memory.Length + 0x007FFF)
            {
                data = _memory[address - 0x007FFF];
            }
            else if (address <= 0x007FFF && address < _rom.Length)
            {
                data = _rom[address];
            }
            else
            {
                data = 0;
            }
            Console.WriteLine("Memory read: {1:x4} at {0:x6}", address, data);
            return data;
        }

        public void WriteMemory(uint address, ushort data)
        {
            Console.WriteLine("Memory write: {0:x4} to {1:x6}", data, address);
            if (address > 0x7FFF && address < _memory.Length + 0x7FFF)
            {
                _memory[address - 0x007FFF] = data;
            }
            else if (address <= 0x007FFF)
            {
                // Nothing happens. ROM.
            }
            else
            {
                // Even less happens.
            }
        }

        public uint CalculateFullAddress(ushort address, uint ptab)
        {
            if (_pt_enable)
            {
                uint psel = (uint)(address & 0xFF00) >> 8;
                uint dsel = (uint)address & 0xFF;
                uint addr_upper = ReadMemory(ptab + psel);

                if (addr_upper == 0xFFFF)
                {
                    _encountered_bad_mem_access = true; // page 0xFFFF is guarenteed to be filled with 0x0000.
                    return 0; // return 0 anyways just in case.
                }
                Console.WriteLine("Paged addr: {0} | P {1} | D {2}", (addr_upper << 8) & dsel, psel, dsel);
                return (addr_upper << 8) | dsel;
            }
            else
            {
                return address;
            }
        }

        public void RaiseInvalidInstructionException()
        {
            //TODO
        }

        public ushort ReadMemoryPaged(ushort address)
        {
            var fulladdr = CalculateFullAddress(address, (_sys_curr_mode == NTPUMode.System) ? _sys_page_table_r : _user_page_table_r);
            return ReadMemory(fulladdr);
        }

        public void WriteMemoryPaged(ushort address, ushort data)
        {
            var fulladdr = CalculateFullAddress(address, (_sys_curr_mode == NTPUMode.System) ? _sys_page_table_w : _user_page_table_w);
            WriteMemory(fulladdr, data);
        }

        private static byte SextR7Offs(byte r7offs)
        {
            return (byte)(r7offs < 8 ? r7offs : r7offs | 0xF0);
        }

        public void ExecuteInstruction()
        {
            var instr = ReadMemoryPaged(_pc);
            if (instr == 0x0000)
            {
                Halted = true;
                return;
            }
            byte opcode = (byte)(instr & 0xFF);
            byte upbyte = (byte)((instr & 0xFF00) >> 8);
            Console.WriteLine("Executing instr {0:X4}", instr);
            byte src = (byte)((instr & 0x700) >> 8);
            byte ctrl = (byte)((instr & 0x1800) >> 11);
            byte r7offs = (byte)((instr & 0xF000) >> 12);
            byte dest = (byte)((instr & 0xE000) >> 13);
            bool standard_encode_1 = false;
            bool standard_encode_2 = false;
            bool calc_flags = true;
            bool do_pc_increment = true;

            Console.WriteLine("Dest: {0:x1}, ctrl: {1:x1}, r7offs: {2:x1}, src: {3:x1}", dest, ctrl, r7offs, src);
            // Ew. handles if an instr should use the normal load/store setup.
            switch (opcode) {
                case byte n when (n <= 0x20 && ((n & 0x01) == 0)):
                    standard_encode_1 = true;
                    break;
                case byte n when (n <= 0x20 && ((n & 0x01) == 1)):
                    standard_encode_2 = true;
                    break;
                case 0xFC: // Jcc Rn
                case 0xFD: // Jcc imm
                case 0xFE: // rst vector
                case 0xFF: // hlt code
                    // neither of the normal encodes.
                    break;
            }

            ushort src_val;
            ushort dest_val;
            ushort dest_write = 0;
            ushort imm = 0; // Yes, globalizing this is nasty. But it removes
                            // prevents a spurious memory read that shouldn't
                            // occur
            if (standard_encode_1)
            {
                dest_val = _registers[dest];
                if (ctrl == 0) {
                    src_val = _registers[src];
                }
                else if (ctrl == 2)
                {
                    var addr = _registers[src];
                    src_val = ReadMemoryPaged(addr);
                }
                else
                {
                    var addr = (ushort)(_registers[7] + (ushort)SextR7Offs(r7offs));
                    src_val = ReadMemoryPaged(addr);
                }
            } else if (standard_encode_2)
            {
                if (ctrl == 0)
                {
                    // Immediate modes
                    imm = ReadMemoryPaged((ushort)(_pc + 1));
                    _pc += 1;
                    switch (src)
                    {
                        case 0:
                            dest_val = _registers[dest];
                            src_val = imm;
                            break;
                        case 1:
                            src_val = ReadMemoryPaged(imm);
                            dest_val = _registers[dest];
                            break;
                        case 2:
                            src_val = _registers[dest]; // swap mode.
                            dest_val = ReadMemoryPaged(imm);
                            break;
                        default:
                            _active_interrupt = 1; // Invalid instr
                            return;
                    }
                }
                else
                {
                    src_val = _registers[src];
                    if (ctrl == 1)
                    {
                        dest_val = ReadMemoryPaged(_registers[dest]);
                    }
                    else
                    {
                        var addr = (ushort)(_registers[7] + (ushort)SextR7Offs(r7offs));
                        dest_val = ReadMemoryPaged(addr);
                    }
                }
            }
            else
            {
                src_val = 0;
                dest_val = 0;
            }

            switch (opcode)
            {
                case 0x00:
                case 0x01:
                    calc_flags = false;
                    dest_write = src_val;
                    break;
                case 0x02:
                case 0x03:
                    InstrAdd(src_val, dest_val, out dest_write);
                    break;
                case 0x04:
                case 0x05:
                    InstrSub(src_val, dest_val, out dest_write);
                    break;
                case 0x06:
                case 0x07:
                    InstrAnd(src_val, dest_val, out dest_write);
                    break;
                case 0x08:
                case 0x09:
                    InstrOr(src_val, dest_val, out dest_write);
                    break;
                case 0x0A:
                case 0x0B:
                    InstrXor(src_val, dest_val, out dest_write);
                    break;
                case 0x0C:
                case 0x0D:
                    InstrShl(src_val, dest_val, out dest_write);
                    break;
                case 0x0E:
                case 0x0F:
                    InstrShr(src_val, dest_val, out dest_write);
                    break;
                case 0x14:
                case 0x15:
                    InstrCmp(src_val, dest_val, out dest_write);
                    break;
                case 0x16:
                case 0x17:
                    InstrTst(src_val, dest_val, out dest_write);
                    break;
                case 0xF8:
                    do_pc_increment = false;
                    calc_flags = false;
                    InstrCall(dest);
                    break;
                case 0xF9:
                    do_pc_increment = false;
                    calc_flags = false;
                    InstrRet(dest);
                    break;
                case 0xFA:
                case 0xFB:
                    InstrLdpr(instr);
                    calc_flags = false;
                    break;
                case 0xFC:
                case 0xFD:
                    do_pc_increment = false;
                    InstrJCC(opcode, upbyte, src);
                    calc_flags = false;
                    break;
                case 0xFF:
                    if (_sys_curr_mode == NTPUMode.User)
                    {
                        goto case 0xFE; //would be fallthrough, but C#.
                    }
                    calc_flags = false;
                    Halted = true;
                    do_pc_increment = false;
                    _last_reset_addr = CalculateFullAddress(_pc, (_sys_curr_mode == NTPUMode.System) ? _sys_page_table_r : _user_page_table_r);
                    _last_reset_addr_virt = _pc;
                    break;
                case 0xFE:
                    calc_flags = false;
                    do_pc_increment = false;
                    _last_reset_addr = CalculateFullAddress(_pc, (_sys_curr_mode == NTPUMode.System) ? _sys_page_table_r : _user_page_table_r);
                    _last_reset_addr_virt = _pc;
                    if (_sys_curr_mode == NTPUMode.User)
                    {
                        _sys_curr_mode = NTPUMode.System;
                       
                        _pc = _syscall_entry; // Go to wherever the system says to.
                    }
                    else
                    {
                        _pc = 0;
                        _pt_enable = false;
                        _interrupt_enable = false;
                    }
                    break;
                default:
                    break;
            }

            if (standard_encode_1)
            {
                _registers[dest] = dest_write;
            }
            else if (standard_encode_2)
            {
                if (ctrl == 0)
                {
                    switch (src)
                    {
                        case 0: // LD_1
                        case 1:
                            _registers[dest] = dest_write;
                            break;
                        case 2:
                            WriteMemoryPaged(imm, dest_write);
                            break;

                    }
                }
                else if (ctrl == 1)
                {
                    WriteMemoryPaged(_registers[dest], dest_write);
                }
                else
                {
                    var addr = (ushort)(_registers[7] + (ushort)SextR7Offs(r7offs));
                    WriteMemoryPaged(addr, dest_write);
                }
            }

            if (calc_flags)
            {
                _flag_zero |= dest_write == 0;
                _flag_neg |= dest_write >= 0x7FFF;
            }

            if (do_pc_increment)
                _pc += 1;
            if (_pc > 513)
            {
                Halted = true;
            }
        }

        public void Tick()
        {
            if (Halted)
            {

                return;
            }
            Console.WriteLine("R0: {0:x4} R1: {1:x4} R2: {2:x4} R3: {3:x4} R4:{4:x4} R5: {5:x4} R6: {6:x4} R7: {7:x4}", _registers[0], _registers[1], _registers[2], _registers[3], _registers[4], _registers[5], _registers[6], _registers[7]);
            Console.WriteLine("Z {0} N {1} V {2} S {3} | PC {4:x4}", _flag_zero, _flag_neg, _flag_carry, _flag_overflow, _pc);
            ExecuteInstruction();
        }

    }


}