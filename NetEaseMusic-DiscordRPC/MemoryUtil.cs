using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace NetEaseMusic_DiscordRPC
{
    class MemoryUtil
    {
        public static void LoadMemory(ref string text, ref double rate, ref double lens)
        {
            var process = Process.GetProcessesByName("cloudmusic").FirstOrDefault(p => p.MainWindowTitle.Length > 0);
            if (process == null)
            {
                // not found
                return;
            }

            //Debug.Print($"Process Hadnle {process.Id}");

            var address = IntPtr.Zero;
            foreach (ProcessModule module in process.Modules)
            {
                //Debug.Print($"Find module {module.ModuleName}");
                if ("cloudmusic.dll".Equals(module.ModuleName))
                {
                    address = module.BaseAddress;
                    //Debug.Print($"Match module address {module.BaseAddress}");
                }
            }

            if (address == IntPtr.Zero)
            {
                //Debug.Print($"Null address");
                return;
            }

            var handle = OpenProcess(0x10, IntPtr.Zero, process.Id);
            if (handle == IntPtr.Zero)
            {
                //Debug.Print($"Null handle");
                return;
            }

            var buffer = new byte[sizeof(double) + 1];

            if (!ReadProcessMemory(handle, address + 0x8ADA70, buffer, sizeof(double), IntPtr.Zero))
            {
                //Debug.Print($"Failed to load memory at 0x{(address + 0x8ADA70).ToString("X")}");
            }
            var current = BitConverter.ToDouble(buffer, 0);

            if (!ReadProcessMemory(handle, address + 0x8CDF88, buffer, sizeof(double), IntPtr.Zero))
            {
                //Debug.Print($"Failed to load memory at 0x{(address + 0x8CDF88).ToString("X")}");
            }
            var maxlens = BitConverter.ToDouble(buffer, 0);

            //Debug.Print($"Current value {current} | {maxlens} | {process.MainWindowTitle}");

            rate = current;
            lens = maxlens;
            text = process.MainWindowTitle;
        }

        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr OpenProcess(int dwDesiredAccess, IntPtr bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr pHandle, IntPtr Address, byte[] Buffer, int Size, IntPtr NumberofBytesRead);

    }
}
