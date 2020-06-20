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
        public static readonly string ApplicationName = "NetEase Music";
        public static readonly string ApplicationId   = "724001455685632101";
    }

    static class global
    {
        public static DiscordRpc.EventHandlers events = new DiscordRpc.EventHandlers();
        public static WebClient webclient = new WebClient();

        public static DiscordRpc.RichPresence presence = null;
    }

    static class player
    {
        public static string currentPlaying = null;
        public static bool loadingApi = false;
        public static bool requireUpdate = false;
        public static long startPlaying = 0;
        public static long endPlaying = 0;
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
            // Hide window
            //Win32Api.User32.ShowWindow(Process.GetCurrentProcess().MainWindowHandle, Win32Api.User32.SW_HIDE);

            // check run once
            Mutex self = new Mutex(true, "NetEase Cloud Music DiscordRPC", out bool allow);
            if (!allow)
            {
                MessageBox.Show("NetEase Cloud Music DiscordRPC is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // Check Rpc Dll
            if (!System.IO.File.Exists(Application.StartupPath + "\\discord-rpc.dll"))
            {
                MessageBox.Show("discord-rpc.dll does not exists!", "Error");
                if (MessageBox.Show("Do you want to download the missing files?", "Rpc Client", MessageBoxButtons.YesNo) != DialogResult.Yes)
                {
                    Environment.Exit(-1);
                }

                try
                {
                    using (WebClient web = new WebClient())
                    {
                        web.DownloadFile("http://build.kxnrl.com/_Raw/NetEaseMusicDiscordRpc/discord-rpc.dll", Application.StartupPath + "\\discord-rpc.dll");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to download discord-rpc.dll !", "Fatal Error");
                    Environment.Exit(-1);
                }
            }

            // Auto Startup
            Win32Api.Registry.SetAutoStartup();

            // web client event
            global.webclient.DownloadStringCompleted += HttpRequestCompleted;

            // Discord event
            global.events.readyCallback += DiscordRpc_Connected;

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

            // Show notification
            tray.notifyIcon.BalloonTipTitle = "NetEase Cloud Music DiscordRPC";
            tray.notifyIcon.BalloonTipText = "External Plugin Started!";
            tray.notifyIcon.ShowBalloonTip(5000);

            // Run
            Application.Run();
        }

        private static void DiscordRpc_Connected(ref DiscordRpc.DiscordUser connectedUser)
        {
            Console.WriteLine("Discord Connected: " + Environment.NewLine + connectedUser.userId + Environment.NewLine + connectedUser.username + Environment.NewLine + connectedUser.avatar + Environment.NewLine + connectedUser.discriminator);
        }

        private static void HttpRequestCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            player.loadingApi = false;
            player.endPlaying = (long)Math.Round(Convert.ToDouble(e.Result), MidpointRounding.AwayFromZero) + player.startPlaying;

            if (global.presence == null)
            {
                // RPC exited.
                return;
            }

            global.presence.endTimestamp = player.endPlaying;
            DiscordRpc.UpdatePresence(global.presence);
        }

        private static void ApplicationHandler_TrayIcon(object sender, EventArgs e)
        {
            MenuItem item = (MenuItem)sender;
            if (item == tray.exitButton)
            {
                ClearStatus();
                tray.notifyIcon.Visible = false;
                tray.notifyIcon.Dispose();
                Thread.Sleep(50);
                Environment.Exit(0);
            }
        }

        private static string currentPlaying = null;
        private static StringBuilder strbuilder = new StringBuilder(256);
        private static bool playerRunning = false; 
        private static void UpdateStatus()
        {
            // Block thread.
            Thread.Sleep(1000);

            // clear data
            strbuilder.Clear();

            // set flag
            playerRunning = false;

            // Check Player
            Win32Api.User32.EnumWindows
            (
                delegate (IntPtr hWnd, int lParam)
                {
                    Win32Api.User32.GetClassName(hWnd, strbuilder, 256);

                    if (strbuilder.ToString().Equals("OrpheusBrowserHost"))
                    {
                        // clear data
                        strbuilder.Clear();
                        int length = Win32Api.User32.GetWindowTextLength(hWnd);
                        Win32Api.User32.GetWindowText(hWnd, strbuilder, length + 1);
                        currentPlaying = strbuilder.ToString();
                        playerRunning = true;
                    }

                    return true;
                },
                IntPtr.Zero
            );

            // maybe application has been exited ?
            if (!playerRunning)
            {
                // fresh status.
                ClearStatus();
                Console.WriteLine("Player exited!");
                return;
            }

            // Has resultes?
            if (String.IsNullOrWhiteSpace(currentPlaying))
            {
                // fresh status.
                ClearStatus();
                Console.WriteLine("Fatal ERROR!");
                return;
            }

            Console.WriteLine("try to update new result");

            // new song?
            if (!currentPlaying.Equals(player.currentPlaying))
            {
                // strcopy
                player.currentPlaying = currentPlaying;
                player.startPlaying = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                // loading timeleft
                if (!player.loadingApi && !global.webclient.IsBusy)
                {
                    player.loadingApi = true;
                    global.webclient.DownloadStringAsync(new Uri("https://music.kxnrl.com/api/v3/?engine=netease&format=string&data=length&song=" + currentPlaying));
                }

                player.requireUpdate = true;
            }

            // Runing full screen Application?
            if (Win32Api.User32.IsFullscreenAppRunning())
            {
                // fresh status.
                ClearStatus();
                player.requireUpdate = true;
                Console.WriteLine("Runing fullscreen Application.");
                return;
            }
                
            // Running whitelist Application?
            if (Win32Api.User32.IsWhitelistAppRunning())
            {
                // fresh status.
                ClearStatus();
                player.requireUpdate = true;
                Console.WriteLine("Running whitelist Application.");
                return;
            }

            // check discord
            CheckRpc();

            // Update?
            if (player.requireUpdate)
            {
                // RPC data
                string[] text = currentPlaying.Replace(" - ", "\t").Split('\t');
                if (text.Length > 1)
                {
                    global.presence.details = text[0];
                    global.presence.state = "by " + text[1]; // like spotify
                }
                else
                {
                    global.presence.details = currentPlaying;
                    global.presence.state = string.Empty;
                }

                global.presence.largeImageKey = "neteasemusic_white";
                global.presence.largeImageText = "NetEase Music";
                global.presence.startTimestamp = player.startPlaying;
                global.presence.endTimestamp = player.endPlaying;

                // Update status
                DiscordRpc.UpdatePresence(global.presence);

                // logging
                Console.WriteLine("updated new result");
            }
        }

        private static void ClearStatus()
        {
            if (global.presence == null)
            {
                // uninitialized
                return;
            }

            global.presence.FreeMem();
            global.presence = null;

            DiscordRpc.ClearPresence();
            DiscordRpc.Shutdown();
        }

        private static void CheckRpc()
        {
            if (global.presence != null)
            {
                // Initialized
                return;
            }

            global.presence = new DiscordRpc.RichPresence();
            //global.events = new DiscordRpc.EventHandlers();

            // Discord Api initializing...
            DiscordRpc.Initialize(info.ApplicationId, ref global.events, false, null);
        }
    }
}
