using System;

namespace NTPUwork
{
    class Program
    {
        public static NTPUCPU cpu;

        static void Main(string[] args)
        {
            UInt16[] rom = { 0x2200, 0x0001, 0x0002, 0x0003, 0xfffe, 0x30fd, 0x0008, 0x00ff, 0x01ff };
            cpu = new NTPUCPU(8192, rom);

            while (!cpu.Halted)
            {
                cpu.Tick();
            }
        }
    }
}
