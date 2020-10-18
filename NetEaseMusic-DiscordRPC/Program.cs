using DiscordRPC;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace NetEaseMusic_DiscordRPC
{
    class Program
    {
        const string ApplicationId = "750224620476694658";

        static void Main()
        {
            // check run once
            _ = new Mutex(true, "NetEase Cloud Music RPC", out var allow);
            if (!allow)
            {
                MessageBox.Show("NetEase Cloud Music RPC is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(-1);
            }

            // Auto Startup
            if (Properties.Settings.Default.IsFirstTime)
            {
                AutoStart.Set();
                Properties.Settings.Default.IsFirstTime = false;
                Properties.Settings.Default.Save();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Menu setup
            var notifyMenu = new ContextMenu();
            var actiButton = new MenuItem("启用");
            var exitButton = new MenuItem("退出");
            var autoButton = new MenuItem("开机自启");
            var themeMenu = new MenuItem("个性化");
            var settingMenu = new MenuItem("设置");
            var defDark = new MenuItem("Dark");
            var defDiscord = new MenuItem("Discord");
            var defNetease = new MenuItem("Netease Red");
            var defWhite = new MenuItem("Netease White");
            var playingStats = new MenuItem("显示暂停状态");
            var settingFullscreen = new MenuItem("在全屏状态下显示");
            var settingWhitelists = new MenuItem("忽略白名单");

            // Main menu
            notifyMenu.MenuItems.Add(actiButton);
            notifyMenu.MenuItems.Add("-");
            notifyMenu.MenuItems.Add(themeMenu);
            notifyMenu.MenuItems.Add(settingMenu);
            notifyMenu.MenuItems.Add("-");
            notifyMenu.MenuItems.Add(exitButton);

            // Settings submenu
            settingMenu.MenuItems.Add(autoButton);
            settingMenu.MenuItems.Add(settingFullscreen);
            settingMenu.MenuItems.Add(settingWhitelists);

            // Theme submenu
            themeMenu.MenuItems.Add(defDark);
            themeMenu.MenuItems.Add(defDiscord);
            themeMenu.MenuItems.Add(defNetease);
            themeMenu.MenuItems.Add(defWhite);
            themeMenu.MenuItems.Add("-");
            themeMenu.MenuItems.Add(playingStats);


            // Check the saved option
            actiButton.Checked = Properties.Settings.Default.DefActive;
            if (Properties.Settings.Default.DefSkin == "default_dark")
            {
                defDark.Checked = true;
            }
            if (Properties.Settings.Default.DefSkin == "default_discord")
            {
                defDiscord.Checked = true;
            }
            if (Properties.Settings.Default.DefSkin == "default_netease")
            {
                defNetease.Checked = true;
            }
            if (Properties.Settings.Default.DefSkin == "default_white")
            {
                defWhite.Checked = true;
            }
            autoButton.Checked = AutoStart.Check();
            settingFullscreen.Checked = Properties.Settings.Default.FullscreenRun;
            settingWhitelists.Checked = Properties.Settings.Default.WhitelistsRun;
            playingStats.Checked = Properties.Settings.Default.showPaused;

            // Icon
            var notifyIcon = new NotifyIcon()
            {
                BalloonTipIcon = ToolTipIcon.Info,
                ContextMenu = notifyMenu,
                Text = "NetEase Cloud Music RPC",
                Icon = Properties.Resources.icon,
                Visible = true,
            };



            // Button clicked action
            exitButton.Click += (sender, args) =>
            {
                Properties.Settings.Default.Save();
                notifyIcon.Visible = false;
                Thread.Sleep(100);
                Environment.Exit(0);
            };

            autoButton.Click += (sender, args) =>
            {
                var x = AutoStart.Check();
                if (x)
                {
                    AutoStart.Remove();
                }
                else
                {
                    AutoStart.Set();
                }

                autoButton.Checked = !x;
            };

            actiButton.Click += (sender, args) =>
            {
                actiButton.Checked = !actiButton.Checked;
                Properties.Settings.Default.Save();
            };

            settingFullscreen.Click += (sender, args) =>
            {
                settingFullscreen.Checked = !settingFullscreen.Checked;
                Properties.Settings.Default.FullscreenRun = settingFullscreen.Checked;
                Properties.Settings.Default.Save();
            };

            settingWhitelists.Click += (sender, args) =>
            {
                settingWhitelists.Checked = !settingWhitelists.Checked;
                Properties.Settings.Default.WhitelistsRun = settingWhitelists.Checked;
                Properties.Settings.Default.Save();
            };

            defDark.Click += (sender, args) =>
            {
                defDark.Checked = true;
                Properties.Settings.Default.DefSkin = "default_dark";
                Properties.Settings.Default.Save();
                defDiscord.Checked = false;
                defNetease.Checked = false;
                defWhite.Checked = false;
            };

            defDiscord.Click += (sender, args) =>
            {
                defDiscord.Checked = true;
                Properties.Settings.Default.DefSkin = "default_discord";
                Properties.Settings.Default.Save();
                defDark.Checked = false;
                defNetease.Checked = false;
                defWhite.Checked = false;
            };

            defNetease.Click += (sender, args) =>
            {
                defNetease.Checked = true;
                Properties.Settings.Default.DefSkin = "default_netease";
                Properties.Settings.Default.Save();
                defDark.Checked = false;
                defDiscord.Checked = false;
                defWhite.Checked = false;
            };

            defWhite.Click += (sender, args) =>
            {
                defWhite.Checked = true;
                Properties.Settings.Default.DefSkin = "default_white";
                Properties.Settings.Default.Save();
                defDark.Checked = false;
                defDiscord.Checked = false;
                defNetease.Checked = false;
            };

            playingStats.Click += (sender, args) =>
            {
                playingStats.Checked = !playingStats.Checked;
                Properties.Settings.Default.showPaused = playingStats.Checked;
                Properties.Settings.Default.Save();
            };



            // Run
            Task.Run(() =>
            {
                using var discord = new DiscordRpcClient(ApplicationId);
                discord.Initialize();

                var playerState = false;
                var currentSong = string.Empty;
                var currentSing = string.Empty;
                var currentRate = 0.0;
                var maxSongLens = 0.0;
                var lastRate = -0.01;
                var diffRate = 0.01;

                while (true)
                {
                    // 用户就喜欢超低内存占用
                    // 但是实际上来说并没有什么卵用
                    GC.Collect();
                    GC.WaitForFullGCComplete();

                    Thread.Sleep(5000);

                    lastRate = currentRate;
                    var lastLens = maxSongLens;

                    Debug.Print($"{currentRate} | {lastRate} | {(diffRate)}");

                    if (!Win32Api.User32.GetWindowTitle("OrpheusBrowserHost", out var title, out var pid) || pid == 0)
                    {
                        Debug.Print($"player is not running");
                        playerState = false;
                        goto done;
                    }

                    // load memory

                    MemoryUtil.LoadMemory(pid, ref currentRate, ref maxSongLens);
                    diffRate = currentRate - lastRate;

                    if (diffRate == 0) //currentRate.Equals(lastRate) 0.0416
                    {
                        Debug.Print($"Music pause? {currentRate} | {lastRate} | {(diffRate)}");
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
                    else if (Math.Abs(diffRate) < 0.0416)
                    {
                        // skip playing
                        Debug.Print($"Skip Rpc {Math.Abs(diffRate)}");
                        continue;
                    }


                done:
                    Debug.Print($"playerState -> {playerState} | Equals {maxSongLens} | {lastLens}");


                    // User setting
                    var isfullScreen = false;
                    var iswhiteLists = false;

                    if (!settingFullscreen.Checked && Win32Api.User32.IsFullscreenAppRunning())
                    {
                        isfullScreen = true;
                    }
                    if (!settingWhitelists.Checked && Win32Api.User32.IsWhitelistAppRunning())
                    {
                        iswhiteLists = true;
                    }

                    // update
                    var smallImgPlaying = "";
                    var smallImgText = "";
                    var timeNow = new Timestamps(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)), DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)).Add(TimeSpan.FromSeconds(maxSongLens)));

                    if (playingStats.Checked && !playerState && actiButton.Checked && !isfullScreen && !iswhiteLists) // Show paused?
                    {
                        if (defDark.Checked)
                            smallImgPlaying = "dark_inactive";
                        if (defDiscord.Checked)
                            smallImgPlaying = "discord_inactive";
                        if (defNetease.Checked)
                            smallImgPlaying = "netease_inactive";
                        if (defWhite.Checked)
                            smallImgPlaying = "white_inactive";
                        smallImgText = "Paused";
                        timeNow = new Timestamps(); // Clear timestamps only
                        goto Update;
                    }
                    else if (!playerState || !actiButton.Checked || isfullScreen || iswhiteLists)
                    {
                        discord.ClearPresence();
                        Debug.Print("Clear Rpc");
                        continue;
                    }
                    else
                    {
                        timeNow = new Timestamps(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)), DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(currentRate)).Add(TimeSpan.FromSeconds(maxSongLens)));
                    }

                    if (playingStats.Checked)
                    {
                        if (defDark.Checked)
                            smallImgPlaying = "dark_active";
                        if (defDiscord.Checked)
                            smallImgPlaying = "discord_active";
                        if (defNetease.Checked)
                            smallImgPlaying = "netease_active";
                        if (defWhite.Checked)
                            smallImgPlaying = "white_active";

                        smallImgText = "Playing";
                    }

                Update:
                    discord.SetPresence(new RichPresence
                    {
                        Assets = new Assets
                        {
                            LargeImageKey = Properties.Settings.Default.DefSkin,
                            LargeImageText = "Netease Cloud Music",
                            SmallImageKey = smallImgPlaying,
                            SmallImageText = smallImgText,
                        },
                        Timestamps = timeNow,
                        Details = currentSong,
                        State = $"By: {currentSing}",
                    });
                    Debug.Print("Update Rpc");
                }
            });

            Application.Run();
        }
    }
}
