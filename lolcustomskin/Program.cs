using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using LoLCustomSharp;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace lolcustomskin
{
    class Program
    {
        static void Main(string[] args)
        {
            string exePath = Assembly.GetEntryAssembly().Location;
            string configPath = Path.GetDirectoryName(exePath) + "/lolcustomskin-sharp.bin";
            OverlayPatcher patcher = new OverlayPatcher();

            Console.WriteLine($"Overlay: {patcher.Prefix}");
            Console.WriteLine($"Offsets: 0x{patcher.Checksum:X08} 0x{patcher.FileProviderListOffset:X08} 0x{patcher.PMethArrayOffset:X08}");
            Console.WriteLine("Waiting for league to start...");

            patcher.Start(args.Length > 0 ? args[0] : "MOD/", null, null);
        }
    }
}
