using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NetEaseMusic_DiscordRPC
{
    static class MemoryUtil
    {
        private static int ProcessId;
        private static IntPtr EntryPoint;
        private static IntPtr BaseAddress;
        private static string Version;

        public static List<MemoryOffset> Offsets;

        public static void LoadNetEaseMemory(int pid, ref double rate, ref double lens, ref string title, ref string album, ref string artists, ref string cover, ref string url, out bool extra)
        {
            extra = false;

            if (ProcessId != pid)
            {
                EntryPoint = OpenProcess(0x10, IntPtr.Zero, pid);
                BaseAddress = IntPtr.Zero;

                using var process = Process.GetProcessById(pid);

                foreach (ProcessModule module in process.Modules)
                {
                    if ("cloudmusic.dll".Equals(module.ModuleName))
                    {
                        BaseAddress = module.BaseAddress;
                        break;
                    }
                }

                Version = process.MainModule?.FileVersionInfo.ProductVersion;
            }

            if (EntryPoint == IntPtr.Zero || BaseAddress == IntPtr.Zero || string.IsNullOrEmpty(Version))
            {
                return;
            }

            ProcessId = pid;

            var offset = Offsets.FirstOrDefault(x => x.Version == Version);
            if (offset == null)
            {
                return;
            }

            // create buffer
            var buffer = new byte[64];

            if (!ReadProcessMemory(EntryPoint, BaseAddress + offset.Offsets.Schedule, buffer, sizeof(double), IntPtr.Zero))
            {
                Debug.Print($"Failed to load memory at 0x{(BaseAddress + offset.Offsets.Schedule).ToString("X")}");
                return;
            }
            var current = BitConverter.ToDouble(buffer, 0);

            if (!ReadProcessMemory(EntryPoint, BaseAddress + offset.Offsets.Length, buffer, sizeof(double), IntPtr.Zero))
            {
                Debug.Print($"Failed to load memory at 0x{(BaseAddress + offset.Offsets.Length).ToString("X")}");
                return;
            }
            var length = BitConverter.ToDouble(buffer, 0);

            rate = current;
            lens = length;

            if (offset.Offsets.CachePointer > 0)
            {
                try
                {
                    // offset +8 (28 8f 0b050043 04 88)
                    // 28 8f 0b050043 04 88 31 00 39 00 39 00 38 00 36 00 39 00 36 00 34 00 34 00 33 00 5f 00 31 00 5f 00 38  00 34 00 33 00 35 00360039 00 36 00 31 00 37 00 33 00 00
                    // op code
                    // cloudmusic.dll+709868:
                    // 7A1A985A - 0F82 68030000 - jb cloudmusic.dll + 709BC8
                    // 7A1A9860 - 0FBA 25 284F547A 01 - bt[cloudmusic.dll + AA4F28],01
                    // 7A1A9868 - 73 07 - jae cloudmusic.dll + 709871 <<
                    // 7A1A986A - F3 A4 - repe movsb
                    // 7A1A986C - E9 17030000 - jmp cloudmusic.dll + 709B88
                    if (!ReadProcessMemory(EntryPoint, BaseAddress + offset.Offsets.CachePointer, buffer, sizeof(uint), IntPtr.Zero))
                    {
                        Debug.Print("Error read cache array pointer for %LocalAppData%/NetEase/CloudMusic/webdata/file");
                        return;
                    }

                    var add = BitConverter.ToUInt32(buffer, 0);
                    var ptr = new IntPtr(add);
                    if (!ReadProcessMemory(EntryPoint, ptr, buffer, int.MaxValue.ToString().Length * 2, IntPtr.Zero))
                    {
                        Debug.Print("Error read pointer for %LocalAppData%/NetEase/CloudMusic/webdata/file");
                        return;
                    }

                    /* {tid}_{fid}_{data} */
                    var id = Encoding.Unicode.GetString(buffer).Trim().Split('_')[0];

                    var sound = NetEaseCacheManager.GetSoundInfo(int.Parse(id));
                    if (sound == null)
                    {
                        Debug.Print($"Sound {id} not found in cache");
                        return;
                    }

                    title = sound.Track.Name;
                    album = sound.Track.Album.Name;
                    artists = string.Join(", ", sound.Track.Artists.Select(x => x.Name));
                    cover = sound.Track.Album.Cover;
                    lens = sound.Track.Duration * 0.001;
                    url = $"https://music.163.com/song?id={sound.Id}";
                    extra = true;
                }
                catch (Exception e)
                {
                    Debug.Print(e.ToString());
                }
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, IntPtr bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr pHandle, IntPtr Address, byte[] Buffer, int Size, IntPtr NumberofBytesRead);

    }
}
