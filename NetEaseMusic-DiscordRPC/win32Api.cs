using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NetEaseMusic_DiscordRPC.Win32Api
{
    public class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowRect(IntPtr hwnd, out RECT rc);

        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static bool IsFullscreenAppRunning()
        {
            var desktopHandle = GetDesktopWindow();
            var shellHandle = GetShellWindow();
            var window = GetForegroundWindow();

            if (window != IntPtr.Zero)
            {
                if (!(window.Equals(desktopHandle) || window.Equals(shellHandle)))
                {
                    GetWindowRect(window, out var appBounds);
                    System.Drawing.Rectangle screenBounds = Screen.FromHandle(window).Bounds;
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

            using var sr = new StreamReader(Application.StartupPath + "\\windows.txt", Encoding.UTF8);

            while ((window_name = sr.ReadLine()) != null)
            {
                // TrimString
                window_name = window_name.Trim();

                if (window_name.StartsWith("//") || window_name.Length < 3)
                {
                    // ignore comments or spacer
                    continue;
                }

                var window = FindWindow(window_name, null);
                if (window != IntPtr.Zero)
                {
                    //Console.WriteLine("FindWindow {0}", window_name);
                    return true;
                }
            }

            return false;
        }
    }
}