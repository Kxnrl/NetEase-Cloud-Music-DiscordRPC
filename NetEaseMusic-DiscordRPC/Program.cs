using DiscordRPC;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetEaseMusic_DiscordRPC
{
    static class Program
    {
        private const string NeteaseAppId = "481562643958595594";
        private const string TencentAppId = "903485504899665990";

        private static async Task Main()
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

            await GetOffsetsAsync();

            var neteaseRpc = new DiscordRpcClient(NeteaseAppId);
            var tencentRpc = new DiscordRpcClient(TencentAppId);
            neteaseRpc.Initialize();
            tencentRpc.Initialize();

            if (!neteaseRpc.IsInitialized || !tencentRpc.IsInitialized)
            {
                MessageBox.Show("Failed to init rpc client.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            _ = Task.Run(() =>
            {
                var playerState = false;
                var currentSong = string.Empty;
                var currentSing = string.Empty;
                var currentRate = 0.0;
                var maxSongLens = 0.0;
                var lastPlaying = -1;

                while (true)
                    try
                    {
                        // 用户就喜欢超低内存占用
                        // 但是实际上来说并没有什么卵用
                        GC.Collect();
                        GC.WaitForFullGCComplete();


                        Thread.Sleep(TimeSpan.FromMilliseconds(250));

                        string title;
                        int pid;

                        var lastRate = currentRate;
                        var lastLens = maxSongLens;
                        var skipThis = false;

                        if (!Win32Api.User32.GetWindowTitle("OrpheusBrowserHost", out title, out pid) && !Win32Api.User32.GetWindowTitle("QQMusic_Daemon_Wnd", out title))
                        {
                            Debug.Print($"player is not running");
                            playerState = false;
                            goto update;
                        }

                        if (pid > 0)
                        {
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
                                var match = title.Replace("\r", "").Replace("\n", "").Replace(" - ", "\t").Split('\t');
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
                            else if (Math.Abs(diffRate) < 1.0 && neteaseRpc.CurrentPresence != null)
                            {
                                // skip playing
                                Debug.Print($"Skip Rpc {currentRate} | {lastRate} | {Math.Abs(diffRate)}");
                                skipThis = true;
                            }
                        }
                        else if (pid == 0)
                        {
                            // mark as playing and always update
                            playerState = true;
                            skipThis = false;

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
                        }

                        Debug.Print($"playerState -> {playerState} | Equals {maxSongLens} | {lastLens}");

                        if (lastPlaying != pid)
                        {
                            // player changed
                            skipThis = false;
                        }

                        lastPlaying = pid;

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
                            if (neteaseRpc.CurrentPresence != null)
                            {
                                neteaseRpc.ClearPresence();
                                Debug.Print("Clear netease rpc");
                            }
                            if (tencentRpc.CurrentPresence != null)
                            {
                                tencentRpc.ClearPresence();
                                Debug.Print("Clear tencent rpc");
                            }
                            continue;
                        }

                        if (skipThis)
                            // skip
                            continue;

                        if (pid > 0)
                        {
                            tencentRpc.ClearPresence();
                            neteaseRpc.SetPresence(new RichPresence
                            {
                                Details = $"🎵　{currentSong}",
                                State = $"🎤　{currentSing}",

                                Timestamps = new Timestamps(
                                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)),
                                DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate))
                                    .Add(TimeSpan.FromSeconds(maxSongLens))),

                                Assets = new Assets
                                {
                                    LargeImageKey = "timg",
                                    LargeImageText = "Netease Cloud Music"
                                }
                            });
                        }
                        else if (pid == 0)
                        {
                            neteaseRpc.ClearPresence();
                            tencentRpc.SetPresence(new RichPresence
                            {
                                Details = $"🎵　{currentSong}",
                                State = $"🎤　{currentSing}",

                                Assets = new Assets
                                {
                                    LargeImageKey = "qimg",
                                    LargeImageText = "QQMusic"
                                }
                            });
                        }

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

        private static async Task GetOffsetsAsync()
        {
        retry:
            try
            {
                using var client = new HttpClient()
                {
                    BaseAddress = new Uri("https://api.kxnrl.com/NCM-Rpc/"),
                    Timeout = TimeSpan.FromMinutes(1)
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync("GetOffsets/v1/");

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                MemoryUtil.Offsets = JsonConvert.DeserializeObject<List<MemoryOffset>>(json);
            }
            catch (Exception e)
            {
                var r = MessageBox.Show(e.Message, "Failed to get offsets", MessageBoxButtons.RetryCancel,
                    MessageBoxIcon.Error);
                if (r == DialogResult.Retry)
                    goto retry;

#if !DEBUG
                Environment.Exit(-1);
#endif
            }
        }
    }
}