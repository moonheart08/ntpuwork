using System;
using System.IO;


namespace NTPUwork
{
    class Program
    {
        public static NTPUCPU cpu;

        static void Main(string[] args)
        {
            FileStream ostrm;
            StreamWriter writer;
            TextWriter oldOut = Console.Out;
            try
            {
                ostrm = new FileStream("./emulog.txt", FileMode.OpenOrCreate, FileAccess.Write);
                writer = new StreamWriter(ostrm);
            }
            catch (Exception e)
            {
                Console.WriteLine("Cannot open Redirect.txt for writing");
                Console.WriteLine(e.Message);
                return;
            }
            Console.SetOut(writer);
            byte[] buffer = {0x01, 0xe0, 0x00, 0x84, 0xf8, 0xe0, 0x03, 0x00, 0xf8, 0xe0, 0x04, 0x00, 0xff, 0x00, 0xf8, 0xe0,
0x01, 0x00, 0xf9, 0xe0, 0x00, 0x0f, 0x15, 0x00, 0x09, 0x00, 0xfd, 0x40, 0x11, 0x00, 0xf8, 0xe0,
0xf6, 0xff, 0xf9, 0xe0
};
            ushort[] rom = new ushort[buffer.Length / 2];

            for (int n = 0; n < buffer.Length; n += 2)
            {
                rom[n/2] = (ushort)(buffer[n] | buffer[n + 1] << 8);
            }

            cpu = new NTPUCPU(8192, rom);

            while (!cpu.Halted)
            {
                cpu.Tick();
            }
            Console.SetOut(oldOut);
            writer.Close();
            ostrm.Close();
        }
    }
}
