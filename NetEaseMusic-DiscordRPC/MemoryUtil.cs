using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetEaseMusic_DiscordRPC
{
    static class MemoryUtil
    {
        private static int    ProcessId;
        private static IntPtr EntryPoint;
        private static IntPtr BaseAddress;

        public static void LoadMemory(int pid, ref double rate, ref double lens)
        {
            if (ProcessId != pid)
            {
                EntryPoint = OpenProcess(0x10, IntPtr.Zero, pid);
                BaseAddress = IntPtr.Zero;

                using var process = Process.GetProcessById(pid);

                //Debug.Print($"Process Hadnle {process.Id}");

                foreach (ProcessModule module in process.Modules)
                {
                    //Debug.Print($"Find module {module.ModuleName}");
                    if ("cloudmusic.dll".Equals(module.ModuleName))
                    {
                        BaseAddress = module.BaseAddress;
                        //Debug.Print($"Match module address {module.BaseAddress}");
                        break;
                    }
                }
            }

            if (EntryPoint == IntPtr.Zero || BaseAddress == IntPtr.Zero)
            {
                //Debug.Print($"Null handle");
                return;
            }

            ProcessId = pid;

            var buffer = new byte[sizeof(double) + 1];

            if (!ReadProcessMemory(EntryPoint, BaseAddress + 0x8ADA70, buffer, sizeof(double), IntPtr.Zero))
            {
                //Debug.Print($"Failed to load memory at 0x{(BaseAddress + 0x8ADA70).ToString("X")}");
                return;
            }
            var current = BitConverter.ToDouble(buffer, 0);

            if (!ReadProcessMemory(EntryPoint, BaseAddress + 0x8CDF88, buffer, sizeof(double), IntPtr.Zero))
            {
                //Debug.Print($"Failed to load memory at 0x{(BaseAddress + 0x8CDF88).ToString("X")}");
                return;
            }
            var maxlens = BitConverter.ToDouble(buffer, 0);

            //Debug.Print($"Current value {current} | {maxlens} | {process.MainWindowTitle}");

            rate = current;
            lens = maxlens;
            //text = process.MainWindowTitle;
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, IntPtr bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr pHandle, IntPtr Address, byte[] Buffer, int Size, IntPtr NumberofBytesRead);

    }
}
