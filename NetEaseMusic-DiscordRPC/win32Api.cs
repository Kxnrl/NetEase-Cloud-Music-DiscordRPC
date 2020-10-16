using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void GetClassName(IntPtr hwnd, StringBuilder sb, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int pid);

        private static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hwnd, sb, 256);
            return sb.ToString();
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            var length = GetWindowTextLength(hwnd);
            var sb = new StringBuilder(256);
            GetWindowText(hwnd, sb, length + 1);
            return sb.ToString();
        }

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

                if (window_name.EndsWith(".exe"))
                {
                    return Process.GetProcessesByName(window_name.Replace(".exe", "")).Length > 0;
                }
                else
                {
                    var window = FindWindow(window_name, null);
                    if (window != IntPtr.Zero)
                    {
                        Debug.WriteLine($"FindWindow {window_name}");
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool GetWindowTitle(string match, out string text, out int pid)
        {
            var title = string.Empty;
            var processId = 0;

            EnumWindows
            (
                delegate (IntPtr handle, int param)
                {
                    var classname = GetClassName(handle);

                    if (match.Equals(classname) && GetWindowThreadProcessId(handle, out var xpid) != 0 && xpid != 0)
                    {
                        title = GetWindowTitle(handle); 
                        processId = xpid;
                    }

                    return true;
                },
                IntPtr.Zero
            );

            text = title;
            pid = processId;
            return !string.IsNullOrEmpty(title) && pid > 0;
        }
    }
}