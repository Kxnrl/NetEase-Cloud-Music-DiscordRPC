using System;
using System.Windows.Forms;
using System.Threading;
using System.Text;
using System.Diagnostics;
using System.Net;

namespace NetEaseMusic_DiscordRPC
{
    static class info
    {
        public static readonly string ApplicationName = "NetEase Cloud Music";
        public static readonly string ApplicationId   = "481562643958595594";
    }

    static class global
    {
        public static DiscordRpc.RichPresence presence = new DiscordRpc.RichPresence();
        public static DiscordRpc.EventHandlers handlers = new DiscordRpc.EventHandlers();
        public static WebClient webclient = new WebClient();
    }

    static class player
    {
        public static string currentPlaying = null;
        public static bool loadingApi = false;
    }

    static class tray
    {
        public static ContextMenu notifyMenu;
        public static NotifyIcon notifyIcon;
        public static MenuItem exitButton;
    }

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // check run once
            Mutex self = new Mutex(true, "NetEase Cloud Music DiscordRPC", out bool allow);
            if (!allow)
            {
                MessageBox.Show("NetEase Cloud Music DiscordRPC is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // start new thread to hook tray icon.
            new Thread(
                delegate ()
                {
                    tray.notifyMenu = new ContextMenu();
                    tray.exitButton = new MenuItem("Exit");
                    tray.notifyMenu.MenuItems.Add(0, tray.exitButton);

                    tray.notifyIcon = new NotifyIcon()
                    {
                        BalloonTipIcon = ToolTipIcon.Info,
                        ContextMenu = tray.notifyMenu,
                        Text = "NetEase Cloud Music DiscordRPC",
                        Icon = Properties.Resources.icon,
                        Visible = true,
                    };

                    tray.exitButton.Click += new EventHandler(ApplicationHandler_TrayIcon);

                    Application.Run();
                }
            ).Start();

            // sleep 1 sec
            Thread.Sleep(1000);

            // Discord Api
            DiscordRpc.Initialize(info.ApplicationId, ref global.handlers, false, null);

            // Show notification
            tray.notifyIcon.BalloonTipTitle = "NetEase Cloud Music DiscordRPC";
            tray.notifyIcon.BalloonTipText = "External Plugin Started!";
            tray.notifyIcon.ShowBalloonTip(5000);

            // Hide window
            Win32Api.ShowWindow(Process.GetCurrentProcess().MainWindowHandle, Win32Api.SW_HIDE);

            // web client event
            global.webclient.DownloadStringCompleted += HttpRequestCompleted;

            // start new thread to update status.
            new Thread(
                delegate ()
                {
                    while (true)
                    {
                        //   明明对你念念不忘        思前想后愈发紧张
                        //                   Yukiim
                        //         想得不可得        是最难割舍的

                        UpdateStatus();
                    }
                }
            ).Start();
        }

        private static void HttpRequestCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            player.loadingApi = false;
            global.presence.endTimestamp = (long)Convert.ToDouble(e.Result) + global.presence.startTimestamp;
            DiscordRpc.UpdatePresence(global.presence);
        }

        private static void ApplicationHandler_TrayIcon(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if (item == tray.exitButton)
            {
                //UpdateStatus();
                DiscordRpc.Shutdown();
                tray.notifyIcon.Visible = false;
                tray.notifyIcon.Dispose();
                Thread.Sleep(50);
                Environment.Exit(0);
            }
        }

        private static string currentPlaying = null;
        private static void UpdateStatus()
        {
            // Check Player
            Win32Api.EnumWindows
            (
                delegate (IntPtr hWnd, int lParam)
                {
                    StringBuilder str = new StringBuilder(256);
                    Win32Api.GetClassName(hWnd, str, 256);

                    if (str.ToString() == "OrpheusBrowserHost")
                    {
                        int length = Win32Api.GetWindowTextLength(hWnd);
                        StringBuilder builder = new StringBuilder(length);
                        Win32Api.GetWindowText(hWnd, builder, length + 1);
                        currentPlaying = builder.ToString();
                    }

                    return true;

                },
                IntPtr.Zero
            );

            // Is Playing?
            if (!String.IsNullOrWhiteSpace(currentPlaying))
            {
                // RPC
                string[] text = currentPlaying.Replace(" - ", "\t").Split('\t');
                if(text.Length > 1)
                {
                    global.presence.details = text[0];
                    global.presence.state   = "by " + text[1]; // like spotify
                }
                else
                {
                    global.presence.details = currentPlaying;
                    //global.presence.state = "VA";
                }

                global.presence.largeImageKey = "timg";
                global.presence.largeImageText = "NetEaseMusic";
                global.presence.startTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                global.presence.endTimestamp = 0;

                // Update?
                if (!currentPlaying.Equals(player.currentPlaying))
                {
                    // loading timeleft
                    if (!player.loadingApi && !global.webclient.IsBusy)
                    {
                        player.loadingApi = true;
                        global.webclient.DownloadStringAsync(new Uri("https://music.kxnrl.com/api/v3/?engine=netease&format=string&data=length&song=" + currentPlaying));
                    }

                    // strcopy
                    player.currentPlaying = currentPlaying;

                    // Update status
                    DiscordRpc.UpdatePresence(global.presence);
                }
            }

            // Block thread.
            Thread.Sleep(1000);
        }
    }
}
