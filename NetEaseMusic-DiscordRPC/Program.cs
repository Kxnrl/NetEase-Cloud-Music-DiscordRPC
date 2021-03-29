using System;
using System.Windows.Forms;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using DiscordRPC;

namespace NetEaseMusic_DiscordRPC
{
    internal class Program
    {
        private const string ApplicationId = "481562643958595594";

        private static void Main()
        {
            // check run once
            _ = new Mutex(true, "NetEase Cloud Music DiscordRPC", out var allow);
            if (!allow)
            {
                MessageBox.Show("NetEase Cloud Music DiscordRPC is already running.", "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // Auto Startup
            if (Properties.Settings.Default.IsFirstTime)
            {
                AutoStart.Set();
                Properties.Settings.Default.IsFirstTime = false;
                Properties.Settings.Default.Save();
            }

            Task.Run(() =>
            {
                using var discord = new DiscordRpcClient(ApplicationId);
                discord.Initialize();

                var playerState = false;
                var currentSong = string.Empty;
                var currentSing = string.Empty;
                var currentRate = 0.0;
                var maxSongLens = 0.0;

                while (true)
                    try
                    {
                        // 用户就喜欢超低内存占用
                        // 但是实际上来说并没有什么卵用
                        GC.Collect();
                        GC.WaitForFullGCComplete();

                        Thread.Sleep(TimeSpan.FromMilliseconds(250));

                        var lastRate = currentRate;
                        var lastLens = maxSongLens;
                        var skipThis = false;

                        if (!Win32Api.User32.GetWindowTitle("OrpheusBrowserHost", out var title, out var pid) ||
                            pid == 0)
                        {
                            Debug.Print($"player is not running");
                            playerState = false;
                            goto update;
                        }

                        // load memory
                        MemoryUtil.LoadMemory(pid, ref currentRate, ref maxSongLens);

                        var diffRate = currentRate - lastRate;

                        if (currentRate == 0.0 && maxSongLens == 0.0)
                        {
                            Debug.Print($"invalid? {currentRate} | {lastRate} | {diffRate}");
                            playerState = false;
                        }
                        //          magic hacks? //currentRate != 0.109 && 
                        else if ((currentRate > 0.109 || currentRate == 0) && diffRate < 0.001 && diffRate >= 0 &&
                                 maxSongLens == lastLens) //currentRate.Equals(lastRate)
                        {
                            Debug.Print(
                                $"Music pause? {currentRate} | {lastRate} | {maxSongLens} | {lastLens} | {diffRate}");
                            playerState = false;
                        }
                        else if (!playerState || !maxSongLens.Equals(lastLens))
                        {
                            var match = title.Replace(" - ", "\t").Split('\t');
                            if (match.Length > 1)
                            {
                                currentSong = match[0];
                                currentSing = match[1]; // like spotify
                            }
                            else
                            {
                                currentSong = title;
                                currentSing = string.Empty;
                            }

                            playerState = true;
                        }
                        // check
                        else if (Math.Abs(diffRate) < 1.0 && discord.CurrentPresence != null)
                        {
                            // skip playing
                            Debug.Print($"Skip Rpc {currentRate} | {lastRate} | {Math.Abs(diffRate)}");
                            skipThis = true;
                        }

                        Debug.Print($"playerState -> {playerState} | Equals {maxSongLens} | {lastLens}");

                        update:
                        // update
#if DEBUG
                    if (!playerState)
#else
                        if (Win32Api.User32.IsFullscreenAppRunning() || Win32Api.User32.IsWhitelistAppRunning() ||
                            !playerState)
#endif
                        {
                            Debug.Print(
                                $"Try clear Rpc {Win32Api.User32.IsFullscreenAppRunning()} | {Win32Api.User32.IsWhitelistAppRunning()}");
                            if (discord.CurrentPresence != null)
                            {
                                discord.ClearPresence();
                                Debug.Print("Clear Rpc");
                            }

                            continue;
                        }

                        if (skipThis)
                            // skip
                            continue;

                        discord.SetPresence(new RichPresence
                        {
                            Assets = new Assets
                            {
                                LargeImageKey = "timg",
                                LargeImageText = "Netease Cloud Music"
                            },
                            Timestamps = new Timestamps(
                                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)),
                                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate))
                                    .Add(TimeSpan.FromSeconds(maxSongLens))),
                            Details = currentSong,
                            State = $"by {currentSing}"
                        });

                        Debug.Print("Update Rpc");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Exception while listening: {e}");
                    }
            });


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var notifyMenu = new ContextMenu();
            var exitButton = new MenuItem("Exit");
            var autoButton = new MenuItem("AutoStart" + "    " + (AutoStart.Check() ? "√" : "✘"));
            notifyMenu.MenuItems.Add(0, autoButton);
            notifyMenu.MenuItems.Add(1, exitButton);

            var notifyIcon = new NotifyIcon()
            {
                BalloonTipIcon = ToolTipIcon.Info,
                ContextMenu = notifyMenu,
                Text = "NetEase Cloud Music DiscordRPC",
                Icon = Properties.Resources.icon,
                Visible = true
            };

            exitButton.Click += (sender, args) =>
            {
                notifyIcon.Visible = false;
                Thread.Sleep(100);
                Environment.Exit(0);
            };
            autoButton.Click += (sender, args) =>
            {
                var x = AutoStart.Check();
                if (x)
                    AutoStart.Remove();
                else
                    AutoStart.Set();

                autoButton.Text = "AutoStart" + "    " + (AutoStart.Check() ? "√" : "✘");
            };

            Application.Run();
        }
    }
}