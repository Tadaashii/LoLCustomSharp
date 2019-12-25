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
            OverlayPatcher patcher = OverlayPatcher.Load();

            patcher.Prefix = args.Length > 0 ? args[0] : "MOD/";
            Console.WriteLine($"Overlay: {patcher.Prefix}");
            Console.WriteLine($"Offsets: 0x{patcher.Checksum:X08} 0x{patcher.FileProviderListOffset:X08} 0x{patcher.PMethArrayOffset:X08}");

            Console.WriteLine("Waiting for league to start...");
            for(;;)
            {
                foreach(Process process in Process.GetProcessesByName("League of Legends"))
                {
                    if(!OverlayPatcher.IsLeague(process))
                    {
                        break;
                    }
                    Console.WriteLine("Found league!");
                    bool offsetsUpdated = false;

                    try
                    {
                        using (LeagueProcess league = new LeagueProcess(process))
                        {
                            if (patcher.NeedsUpdate(league))
                            {
                                Console.WriteLine("Offsets need to be updated!");
                                while (process.MainWindowTitle != "League of Legends (TM) Client")
                                {
                                    Thread.Sleep(10);
                                    process.Refresh();
                                }
                                Console.WriteLine("Updating offsets...");
                                patcher.UpdateOffsets(league);
                                offsetsUpdated = true;
                            }
                            else
                            {
                                offsetsUpdated = false;
                            }
                            Console.WriteLine("Patching offsets!");
                            patcher.Patch(league);
                        }
                    }
                    catch(Exception error)
                    {
                        Console.WriteLine(error.StackTrace);
                        Console.WriteLine("Press enter to exit...");
                        Console.ReadLine();
                        return;
                    }

                    if(offsetsUpdated)
                    {
                        Console.WriteLine("Offsets have been updated!");
                        Console.WriteLine($"Offsets: 0x{patcher.Checksum:X08} 0x{patcher.FileProviderListOffset:X08} 0x{patcher.PMethArrayOffset:X08}");
                        try
                        {
                            using (FileStream file = new FileStream(configPath, FileMode.OpenOrCreate))
                            {
                                BinaryFormatter formatter = new BinaryFormatter();
                                formatter.Serialize(file, patcher);
                            }
                        }
                        catch (Exception error)
                        {
                            Console.WriteLine(error.StackTrace);
                            Console.WriteLine("Press enter to exit...");
                            Console.ReadLine();
                            return;
                        }
                    }
                    Console.WriteLine("Waiting for league to exit...");
                    process.WaitForExit();
                    Console.WriteLine("Waiting for league to start...");
                    break;
                }
                Thread.Sleep(1000);
            }
        }
    }
}
