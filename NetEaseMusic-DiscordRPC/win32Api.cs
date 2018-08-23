using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NetEaseMusic_DiscordRPC.Win32Api
{
    public class User32
    {
        public const uint SW_HIDE = 0;
        public const uint SW_SHOW = 1;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hwnd, uint nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern void GetClassName(IntPtr hwnd, StringBuilder sb, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowRect(IntPtr hwnd, out RECT rc);

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static bool IsFullscreenAppRunning()
        {
            IntPtr desktopHandle = GetDesktopWindow();
            IntPtr shellHandle = GetShellWindow();

            IntPtr window = GetForegroundWindow();

            if (window != IntPtr.Zero && !window.Equals(IntPtr.Zero))
            {
                if (!(window.Equals(desktopHandle) || window.Equals(shellHandle)))
                {
                    GetWindowRect(window, out RECT appBounds);
                    System.Drawing.Rectangle screenBounds = System.Windows.Forms.Screen.FromHandle(window).Bounds;
                    if ((appBounds.Bottom - appBounds.Top) == screenBounds.Height && (appBounds.Right - appBounds.Left) == screenBounds.Width)
                    {
                        // In full screen.
                        return true;
                    }
                }
            }

            return false;
        }

        private static string window_name = null;
        public static bool IsWhitelistAppRunning()
        {
            // VisualStudioAppManagement
            // VSCodeCrashServiceWindow
            // Valve001
            // UnrealWindow
            // Rainbow Six

            if (!File.Exists(Application.StartupPath + "\\windows.txt"))
            {
                // Config file doesn't exits...
                return false;
            }

            using (StreamReader sr = new StreamReader(Application.StartupPath + "\\windows.txt", Encoding.UTF8))
            {
                while ((window_name = sr.ReadLine()) != null)
                {
                    // TrimString
                    window_name = window_name.Trim();

                    if (window_name.StartsWith("//") || window_name.Length < 3)
                    {
                        // ignore comments or spacer
                        continue;
                    }

                    IntPtr window = FindWindow(window_name, null);
                    if (window != IntPtr.Zero)
                    {
                        //Console.WriteLine("FindWindow {0}", window_name);
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public class Registry
    {
        public static void SetAutoStartup()
        {
            RegistryKey baseKey = null;
            try
            {
                // Open Base Key.
                baseKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64).OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);

                if (baseKey == null)
                {
                    // wtf?
                    Console.WriteLine(@"Cannot find HKCU\Software\Microsoft\Windows\CurrentVersion\Run");
                    return;
                }

                bool check = false;

                string[] runList = baseKey.GetValueNames();

                foreach (string item in runList)
                {
                    if (item.Equals("NCM-DiscordRpc", StringComparison.OrdinalIgnoreCase))
                    {
                        // already.
                        Console.WriteLine("AutoStartup already set.");
                        check = true;
                        break;
                    }
                }

                if (check)
                {
                    // we are done.
                    return;
                }

                // set
                baseKey.SetValue("NCM-DiscordRpc", Assembly.GetEntryAssembly().Location);

                // done.
                Console.WriteLine("AutoStartup has been set.");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to set autostartup: " + e.ToString());
            }
            finally
            {
                if (baseKey != null)
                {
                    // dispose
                    baseKey.Close();
                    baseKey.Dispose();
                }
            }
        }
    }
}
