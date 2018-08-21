using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NetEaseMusic_DiscordRPC
{
    public class Win32Api
    {
        public const uint SW_HIDE = 0;
        public const uint SW_SHOW = 1;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("User32.Dll")]
        public static extern void GetClassName(IntPtr hwnd, StringBuilder sb, int nMaxCount);

        [DllImport("user32.dll", EntryPoint = "ShowWindow", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hwnd, uint nCmdShow);
    }
}
