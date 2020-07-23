using System;
using System.Collections.Generic;
namespace NTPUwork
{

    public partial class NTPUCPU
    {
        private const UInt32 ROM_BEGIN = 0xFE0000;

        private enum NTPUMode
        {
            System,
            User,
        }

        private UInt16[] _memory;

        private UInt16[] _rom;

        private UInt16[] _registers;

        // Segment selectors, allows code to have an exclusively 16-bit
        // address space in the 24-bit enviornment.
        // The 16-bit address space for code is split up into 8 sections, 
        // which can freely map to any of the 2048 possible segments.
        // The upper 5 bits are always zero, and writes to them are ignored.
        private UInt16[] _user_seg_selectors;
        private UInt16[] _sys_seg_selectors;

        private UInt16 _pc;

        private NTPUMode _sys_curr_mode;

        // Controls if the system will recognize interrupts. If interrupts are
        // enabeled
        private bool _interrupt_enable;

        private bool _seg_enable;

        private byte _active_interrupt;

        private UInt16 _syscall_entry;

        private bool _flag_neg;
        private bool _flag_zero;
        private bool _flag_overflow;
        private bool _flag_carry;

        private UInt32 _last_reset_addr;

        private List<Char> _print_buf;

        public bool Halted;

        public NTPUCPU(uint ram_size, UInt16[] rom)
        {
            _memory = new UInt16[ram_size];
            _rom = rom;
            _registers = new UInt16[8]; // PC stored seperately.
            _pc = 0x0000;
            _interrupt_enable = false;
            _seg_enable = true;
            _user_seg_selectors = new UInt16[8];
            _sys_seg_selectors = new UInt16[8];
            _sys_seg_selectors[0] = 0x7F0; // Maps the beginning of ROM to 0x0000
            _sys_seg_selectors[7] = 0x7F8; // Maps the beginning of control space to 0xE000
            _sys_curr_mode = NTPUMode.System;
            Halted = false;
            _active_interrupt = 0;
            _flag_neg = false;
            _flag_zero = false;
            _flag_carry = false;
            _flag_overflow = false;
            _print_buf = new List<char>();
            _last_reset_addr = 0;

        }

        public UInt16 ReadMemory(UInt32 address)
        {

            UInt16 data;
            if (address < _memory.Length)
            {
                data = _memory[address];
            }
            else if (address >= ROM_BEGIN && address < _rom.Length + ROM_BEGIN)
            {
                data = _rom[address - ROM_BEGIN];
            }
            else if (address >= 0xFF0000)
            {
                switch (address - 0xFF0000)
                {
                    case UInt32 n when (n >= 0 && n < 8):
                        data = _sys_seg_selectors[n];
                        break;
                    case UInt32 n when (n >= 8 && n < 16):
                        data = _user_seg_selectors[n - 8];
                        break;
                    case 16:
                        data = _pc;
                        break;
                    case 17:
                        data = _syscall_entry;
                        break;

                    default:
                        data = 0;
                        break;
                }
            }
            else
            {
                data = 0;
            }
            Console.WriteLine("Memory read: {1:x4} at {0:x6}", address, data);
            return data;
        }

        public void WriteMemory(UInt32 address, UInt16 data)
        {
            Console.WriteLine("Memory write: {0:x4} to {1:x6}", data, address);
            if (address < _memory.Length)
            {
                _memory[address] = data;
            }
            else if (address >= ROM_BEGIN && address < _rom.Length + ROM_BEGIN)
            {
                // Nothing happens.
            }
            else if (address >= 0xFF0000)
            {
                switch (address - 0xFF0000)
                {
                    case UInt32 n when (n >= 0 && n < 8):
                        _sys_seg_selectors[n] = data;
                        break;
                    case UInt32 n when (n >= 8 && n < 16):
                        _user_seg_selectors[n - 8] = data;
                        break;
                    case 16:
                        break;
                    case 17:
                        _syscall_entry = data;
                        break;
                }
            }
            else
            {
                // Even less happens.
            }
        }

        public UInt32 CalculateFullAddress(UInt16 address)
        {
            var selector = address >> 13;
            UInt32 segment;
            if (_sys_curr_mode == NTPUMode.User)
            {
                segment = (UInt32)_user_seg_selectors[selector] << 13;
            }
            else
            {
                segment = (UInt32)_sys_seg_selectors[selector] << 13;
            }
            return segment | ((UInt32)address & 8191);
        }

        public UInt16 ReadMemorySegmented(UInt16 address)
        {
            var fulladdr = CalculateFullAddress(address);
            return ReadMemory(fulladdr);
        }

        public void WriteMemorySegmented(UInt16 address, UInt16 data)
        {
            var fulladdr = CalculateFullAddress(address);
            WriteMemory(fulladdr, data);
        }

        private static byte SextR7Offs(byte r7offs)
        {
            return (byte)(r7offs < 8 ? r7offs : r7offs | 0xF0);
        }

        public void ExecuteInstruction()
        {
            var instr = ReadMemorySegmented(_pc);
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

            Console.WriteLine("Dest: {0:x1}, ctrl: {1:x1}, r7offs: {2:x1}, src: {3:x1}", dest, ctrl, r7offs, src);
            // Ew. handles if an instr should use the normal load/store setup.
            switch (opcode) {
                case byte n when (n <= 0x24 && ((n & 0x01) == 0)):
                    standard_encode_1 = true;
                    break;
                case byte n when (n <= 0x24 && ((n & 0x01) == 1)):
                    standard_encode_2 = true;
                    break;
                case 0xFC: // Jcc Rn
                case 0xFD: // Jcc imm
                case 0xFE: // rst vector
                case 0xFF: // hlt code
                    // neither of the normal encodes.
                    break;
                

            }

            UInt16 src_val;
            UInt16 dest_val;
            UInt16 dest_write = 0;
            UInt16 imm = 0; // Yes, globalizing this is nasty. But it removes
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
                    src_val = ReadMemorySegmented(addr);
                }
                else
                {
                    var addr = (UInt16)(_registers[7] + (UInt16)SextR7Offs(r7offs));
                    src_val = ReadMemorySegmented(addr);
                }
            } else if (standard_encode_2)
            {
                if (ctrl == 0)
                {
                    // Immediate modes
                    imm = ReadMemorySegmented((UInt16)(_pc + 1));
                    _pc += 1;
                    switch (src)
                    {
                        case 0:
                            dest_val = _registers[dest];
                            src_val = imm;
                            break;
                        case 1:
                            src_val = ReadMemorySegmented(imm);
                            dest_val = _registers[dest];
                            break;
                        case 2:
                            src_val = _registers[dest]; // swap mode.
                            dest_val = ReadMemorySegmented(imm);
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
                        dest_val = ReadMemorySegmented(_registers[dest]);
                    }
                    else
                    {
                        var addr = (UInt16)(_registers[7] + (UInt16)SextR7Offs(r7offs));
                        dest_val = ReadMemorySegmented(addr);
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
                case 0xFC:
                case 0xFD:
                    InstrJCC(opcode, upbyte, src);
                    calc_flags = false;
                    break;
                case 0xFF:
                    calc_flags = false;
                    Halted = true;
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
                            WriteMemorySegmented(imm, dest_write);
                            break;

                    }
                }
                else if (ctrl == 1)
                {
                    WriteMemorySegmented(_registers[dest], dest_write);
                }
                else
                {
                    var addr = (UInt16)(_registers[7] + (UInt16)SextR7Offs(r7offs));
                    WriteMemorySegmented(addr, dest_write);
                }
            }

            if (calc_flags)
            {
                _flag_zero |= dest_write == 0;
                _flag_neg |= dest_write >= 0x7FFF;
            }

            _pc += 1;
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