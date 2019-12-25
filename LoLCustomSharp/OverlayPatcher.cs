using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace LoLCustomSharp
{
    public class OverlayPatcher
    {
        public const string CONFIG_FILE = "lolcustomskin-sharp.bin";
        public const uint VERSION = 1;

        // Please, please for the love of god do not attempt to use File/Build version of .exe
        // Those are not reliable 
        // Please!!
        public uint Checksum { get; private set; }
        public uint PMethArrayOffset { get; private set; }
        public uint FileProviderListOffset { get; private set; }
        public string Prefix { get; set; }

        private byte[] _prefixBytes
        {
            get
            {
                string str = this.Prefix;
                if (string.IsNullOrEmpty(str))
                {
                    return new byte[256];
                }
                if (!str.EndsWith("/") && !str.EndsWith("\\"))
                {
                    str += "/";
                }
                byte[] bytes = Encoding.ASCII.GetBytes(str);
                byte[] buffer = new byte[256];
                Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
                return buffer;
            }
        }
        private Thread _thread;

        private static readonly SigScanner PATERN_PMETH_ARRAY = new SigScanner("68 ?? ?? ?? ?? 6A 04 6A 12 8D 44 24 ?? 68 ?? ?? ?? ?? 50 E8 ?? ?? ?? ?? 83 C4 14 85 C0", 14);
        private static readonly SigScanner PATERN_FILE_PROVIDER_LIST = new SigScanner("56 8B 74 24 08 B8 ?? ?? ?? ?? 33 C9 0F 1F 40 00", 6);

        public OverlayPatcher(string configLocation = CONFIG_FILE)
        {
            if (File.Exists(configLocation))
            {
                ReadConfig();
            }
        }

        public void Start(string overlayFolder)
        {
            this.Prefix = overlayFolder;

            this._thread = new Thread(delegate ()
            {
                while (true)
                {
                    foreach (Process process in Process.GetProcessesByName("League of Legends"))
                    {
                        if (!IsLeague(process))
                        {
                            break;
                        }

                        bool offsetsUpdated = false;
                        using (LeagueProcess league = new LeagueProcess(process))
                        {
                            if (NeedsUpdate(league))
                            {
                                while (process.MainWindowTitle != "League of Legends (TM) Client")
                                {
                                    Thread.Sleep(10);
                                    process.Refresh();
                                }

                                UpdateOffsets(league);
                                offsetsUpdated = true;
                            }

                            Patch(league);
                        }

                        if (offsetsUpdated)
                        {
                            WriteConfig();
                        }

                        process.WaitForExit();
                        break;
                    }
                    Thread.Sleep(1000);
                }
            });
            this._thread.IsBackground = true; //Thread needs to be background so it closes when the parent process dies
            this._thread.Start();
        }
        public void Stop()
        {
            this._thread.Abort();
        }

        public void Patch(Process process, out bool offsetsUpdated)
        {
            using (LeagueProcess league = new LeagueProcess(process))
            {
                if (NeedsUpdate(league))
                {
                    // TODO: throw an exception if this takes too long or league dies before
                    while (process.MainWindowTitle != "League of Legends (TM) Client")
                    {
                        Thread.Sleep(10);
                        process.Refresh();
                    }

                    UpdateOffsets(league);
                    offsetsUpdated = true;
                }
                else
                {
                    offsetsUpdated = false;
                }

                Patch(league);
            }
        }
        public void Patch(LeagueProcess league)
        {
            uint codePointer = league.AllocateMemory(0x900);
            uint codeVerifyPointer = codePointer + 0x000;
            uint codePrefixFnPointer = codePointer + 0x100;
            uint codeOpenPointer = codePointer + 0x200;
            uint codeCheckAccessPointer = codePointer + 0x300;
            uint codeCreateIteratorPointer = codePointer + 0x400;
            uint codeVectorDeleterPointer = codePointer + 0x500;
            uint codeIsRadsPointer = codePointer + 0x600;

            league.WriteMemory(codeVerifyPointer, new byte[]
            {
                0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            });
            league.WriteMemory(codePrefixFnPointer, new byte[]
            {
                0x57, 0x56, 0x8b, 0x54, 0x24, 0x0c, 0x8b, 0x74,
                0x24, 0x14, 0x89, 0xd7, 0xac, 0xaa, 0x84, 0xc0,
                0x75, 0xfa, 0x8b, 0x74, 0x24, 0x10, 0x83, 0xef,
                0x01, 0xac, 0xaa, 0x84, 0xc0, 0x75, 0xfa, 0x5e,
                0x89, 0xd0, 0x5f, 0xc3, 0x90, 0x90, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            });
            league.WriteMemory(codeOpenPointer, new byte[]
            {
                0x56, 0x53, 0x81, 0xec, 0x14, 0x02, 0x00, 0x00,
                0x8b, 0x41, 0x04, 0x8b, 0x58, 0x08, 0x8b, 0x03,
                0x8b, 0x30, 0x8d, 0x41, 0x0c, 0x89, 0x44, 0x24,
                0x08, 0x8b, 0x84, 0x24, 0x20, 0x02, 0x00, 0x00,
                0x89, 0x44, 0x24, 0x04, 0x8d, 0x44, 0x24, 0x10,
                0x89, 0x04, 0x24, 0xff, 0x51, 0x08, 0x8b, 0x94,
                0x24, 0x24, 0x02, 0x00, 0x00, 0x89, 0xd9, 0x89,
                0x04, 0x24, 0x89, 0x54, 0x24, 0x04, 0xff, 0xd6,
                0x83, 0xec, 0x08, 0x81, 0xc4, 0x14, 0x02, 0x00,
                0x00, 0x5b, 0x5e, 0xc2, 0x08, 0x00, 0x90, 0x90,
            });
            league.WriteMemory(codeCheckAccessPointer, new byte[]
            {
                0x56, 0x53, 0x81, 0xec, 0x14, 0x02, 0x00, 0x00,
                0x8b, 0x41, 0x04, 0x8b, 0x58, 0x08, 0x8b, 0x03,
                0x8b, 0x70, 0x04, 0x8d, 0x41, 0x0c, 0x89, 0x44,
                0x24, 0x08, 0x8b, 0x84, 0x24, 0x20, 0x02, 0x00,
                0x00, 0x89, 0x44, 0x24, 0x04, 0x8d, 0x44, 0x24,
                0x10, 0x89, 0x04, 0x24, 0xff, 0x51, 0x08, 0x8b,
                0x94, 0x24, 0x24, 0x02, 0x00, 0x00, 0x89, 0xd9,
                0x89, 0x04, 0x24, 0x89, 0x54, 0x24, 0x04, 0xff,
                0xd6, 0x83, 0xec, 0x08, 0x81, 0xc4, 0x14, 0x02,
                0x00, 0x00, 0x5b, 0x5e, 0xc2, 0x08, 0x00, 0x90,
            });
            league.WriteMemory(codeCreateIteratorPointer, new byte[]
            {
                0x31, 0xc0, 0xc2, 0x08, 0x00, 0x90, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            });
            league.WriteMemory(codeVectorDeleterPointer, new byte[]
            {
                0x89, 0xc8, 0xc2, 0x04, 0x00, 0x90, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            });
            league.WriteMemory(codeIsRadsPointer, new byte[]
            {
                0x31, 0xc0, 0xc3, 0x90, 0x90, 0x90, 0x90, 0x90,
                0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90, 0x90,
            });

            // Mark shellcode executable
            // League AC will trip over if we allocate ReadWriteExecutable in one go
            // So page can only be either ReadWrite or Executable
            league.MarkMemoryExecutable(codePointer, 0x900);

            uint modifiedPMethPointer = league.Allocate<EVP_PKEY_METHOD>();
            uint orgignalPMethArrayPointer = this.PMethArrayOffset + league.Base;

            // Read first pointer when it gets initialized(tnx pacman)
            uint originalPMethFirstPointer = league.WaitPointerNonZero(orgignalPMethArrayPointer);

            // Read first PKEY_METHOD
            EVP_PKEY_METHOD originalPMeth = league.Read<EVP_PKEY_METHOD>(originalPMethFirstPointer);

            // Change verify function pointer
            originalPMeth.verify = codeVerifyPointer;

            // Write our new PKEY_METHOD
            league.Write(modifiedPMethPointer, originalPMeth);

            // Write the pointer to out PKEY_METHOD into pointer array
            league.Write(orgignalPMethArrayPointer, modifiedPMethPointer);

            uint orginalFileProviderListPointer = this.FileProviderListOffset + league.Base;

            // Those get deallocated upon exit so we need separate pages
            uint modifiedFileProviderPointer = league.Allocate<FileProvider>();
            uint modifiedFileProviderVtablePointer = league.Allocate<FileProviderVtable>();
            league.Write(modifiedFileProviderPointer, new FileProvider
            {
                vtable = modifiedFileProviderVtablePointer,
                list = orginalFileProviderListPointer,
                prefixFn = codePrefixFnPointer,
                prefix = _prefixBytes,
            });
            league.Write(modifiedFileProviderVtablePointer, new FileProviderVtable
            {
                Open = codeOpenPointer,
                CheckAccess = codeCheckAccessPointer,
                CreateIterator = codeCreateIteratorPointer,
                VectorDeleter = codeVectorDeleterPointer,
                IsRads = codeIsRadsPointer,
            });

            // Wait until providers have been registerd(first pointer turns non-0)
            league.WaitPointerNonZero(orginalFileProviderListPointer);
            FileProviderList originalFileProviderList = league.Read<FileProviderList>(orginalFileProviderListPointer);
            league.Write(orginalFileProviderListPointer, new FileProviderList
            {
                fileProviderPointer0 = modifiedFileProviderPointer,
                fileProviderPointer1 = originalFileProviderList.fileProviderPointer0,
                fileProviderPointer2 = originalFileProviderList.fileProviderPointer1,
                fileProviderPointer3 = originalFileProviderList.fileProviderPointer2,
                size = originalFileProviderList.size + 1,
            });
        }

        public bool NeedsUpdate(LeagueProcess league)
        {
            byte[] dataPE = league.ReadMemory(league.Base, 0x1000);
            uint actualChecksum = LeagueProcess.ExtractChecksum(dataPE);
            return actualChecksum != this.Checksum || this.PMethArrayOffset == 0 || this.FileProviderListOffset == 0;
        }
        public void UpdateOffsets(LeagueProcess league)
        {
            byte[] data = league.Dump();
            uint checksum = LeagueProcess.ExtractChecksum(data);
            int pmethArrayOffsetIndex = PATERN_PMETH_ARRAY.Find(data);
            int fileProviderListOffsetIndex = PATERN_FILE_PROVIDER_LIST.Find(data);
            if (pmethArrayOffsetIndex > 0 && fileProviderListOffsetIndex > 0)
            {
                this.Checksum = checksum;
                this.PMethArrayOffset = BitConverter.ToUInt32(data, pmethArrayOffsetIndex) - league.Base;
                this.FileProviderListOffset = BitConverter.ToUInt32(data, fileProviderListOffsetIndex) - league.Base;
            }
            else
            {
                throw new IOException("Failed to update offsets!");
            }
        }

        public static bool IsLeague(Process league)
        {
            if (league.ProcessName == "League of Legends")
            {
                try
                {
                    return league.MainModule.ModuleName == "League of Legends.exe";
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private void ReadConfig(string configLocation = CONFIG_FILE)
        {
            using (BinaryReader br = new BinaryReader(File.OpenRead(configLocation)))
            {
                uint version = br.ReadUInt32();
                if (version != VERSION)
                {
                    //Don't need to read rest of config, let the patcher update itself
                    return;
                }

                this.Checksum = br.ReadUInt32();
                this.PMethArrayOffset = br.ReadUInt32();
                this.FileProviderListOffset = br.ReadUInt32();
            }
        }
        private void WriteConfig(string configLocation = CONFIG_FILE)
        {
            using (BinaryWriter bw = new BinaryWriter(File.Create(configLocation)))
            {
                bw.Write(VERSION);
                bw.Write(this.Checksum);
                bw.Write(this.PMethArrayOffset);
                bw.Write(this.FileProviderListOffset);
            }
        }

        struct EVP_PKEY_METHOD
        {
            int pkey_id;
            int flags;
            int init;
            int copy;
            int cleanup;
            int paramgen_init;
            int paramgen;
            int keygen_init;
            int keygen;
            int sign_init;
            int sign;
            int verify_init;
            public uint verify;
            int verify_recover_init;
            int verify_recover;
            int signctx_init;
            int signctx;
            int verifyctx_init;
            int verifyctx;
            int encrypt_init;
            int encrypt;
            int decrypt_init;
            int decrypt;
            int derive_init;
            int derive;
            int ctrl;
            int ctrl_str;
            int digestsign;
            int digestverify;
            int check;
            int public_check;
            int param_check;
            int digest_custom;
        };
        struct FileProvider
        {
            public uint vtable;
            public uint list;
            public uint prefixFn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public byte[] prefix;
        };
        struct FileProviderVtable
        {
            public uint Open;
            public uint CheckAccess;
            public uint CreateIterator;
            public uint VectorDeleter;
            public uint IsRads;
        };
        struct FileProviderList
        {
            public uint fileProviderPointer0;
            public uint fileProviderPointer1;
            public uint fileProviderPointer2;
            public uint fileProviderPointer3;
            public uint size;
        };
    }
}
