using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using DiscordRPC;
using DiscordRPC.Helper;
using Kxnrl.Vanessa.Players;
using Button = DiscordRPC.Button;

namespace Kxnrl.Vanessa;

internal class Program
{
    private const string NetEaseAppId = "481562643958595594";
    private const string TencentAppId = "903485504899665990";

    // Discord RPC limits: Details/State/LargeImageText max 128 UTF-8 bytes, image keys max 256 (see DiscordRPC.RichPresence)
    private const int MaxRpcTextBytes     = 128;
    private const int MaxRpcImageKeyBytes = 256;

    private static async Task Main()
    {
        // check run once
        _ = new Mutex(true, "MusicDiscordRpc", out var allow);

        if (!allow)
        {
            MessageBox.Show("MusicDiscordRpc is already running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

            Environment.Exit(-1);

            return;
        }

        if (Constants.GlobalConfig.IsFirstLoad)
        {
            Win32Api.AutoStart.Set(true);
        }

        var netEase = new DiscordRpcClient(NetEaseAppId);
        var tencent = new DiscordRpcClient(TencentAppId);
        netEase.Initialize();
        tencent.Initialize();

        if (!netEase.IsInitialized || !tencent.IsInitialized)
        {
            MessageBox.Show("Failed to init rpc client.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(-1);
        }

        // TODO Online Signatures
        await Task.CompletedTask;

        var notifyMenu = new ContextMenuStrip();

        var exitButton = new ToolStripMenuItem("Exit");
        var autoButton = new ToolStripMenuItem("AutoStart" + "    " + (Win32Api.AutoStart.Check() ? "√" : "✘"));
        notifyMenu.Items.Add(autoButton);
        notifyMenu.Items.Add(exitButton);

        var notifyIcon = new NotifyIcon()
        {
            BalloonTipIcon   = ToolTipIcon.Info,
            ContextMenuStrip = notifyMenu,
            Text             = "NetEase Cloud Music DiscordRPC",
            Icon             = AppResource.icon,
            Visible          = true,
        };

        exitButton.Click += (_, _) =>
        {
            notifyIcon.Visible = false;
            Thread.Sleep(100);
            Environment.Exit(0);
        };

        autoButton.Click += (_, _) =>
        {
            var x = Win32Api.AutoStart.Check();

            Win32Api.AutoStart.Set(!x);

            autoButton.Text = "AutoStart" + "    " + (Win32Api.AutoStart.Check() ? "√" : "✘");
        };

        _ = Task.Run(async () => await UpdateThread(netEase, tencent));
        Application.Run();
    }

    private static async Task UpdateThread(DiscordRpcClient netEase, DiscordRpcClient tencent)
    {
        IMusicPlayer?     lastInstance  = null;
        DiscordRpcClient? lastRpcClient = null;

        while (true)
        {
            try
            {
                IMusicPlayer     player;
                DiscordRpcClient rpcClient;

                if (Win32Api.User32.GetWindowTitle("OrpheusBrowserHost", out _, out var netEaseProcessId))
                {
                    player = lastInstance is null
                        ? new NetEase(netEaseProcessId)
                        : lastInstance.Validate(netEaseProcessId)
                            ? lastInstance
                            : new NetEase(netEaseProcessId);

                    rpcClient = netEase;
                }
                else if (Win32Api.User32.GetWindowTitle("QQMusic_Daemon_Wnd", out _, out var tencentId))
                {
                    player = lastInstance is null
                        ? new Tencent(tencentId)
                        : lastInstance.Validate(tencentId)
                            ? lastInstance
                            : new Tencent(tencentId);

                    rpcClient = tencent;
                }
                else
                {
                    lastInstance = null;

                    continue;
                }

                lastInstance = player;

                var pi = player.GetPlayerInfo();

                Debug.Print(pi is not null ? JsonSerializer.Serialize(pi) : "null");

                if (pi is not { } info)
                {
                    lastRpcClient?.ClearPresence();
                    lastRpcClient = null;

                    continue;
                }

                if (info.Pause)
                {
                    rpcClient.ClearPresence();
                }
                else
                {
                    rpcClient.Update(rpc =>
                    {
                        rpc.Details = $"🎵 {info.Title}".TruncateByUtf8Bytes(MaxRpcTextBytes);
                        rpc.State   = $"🎤 {info.Artists}".TruncateByUtf8Bytes(MaxRpcTextBytes);
                        rpc.Type    = ActivityType.Listening;

                        rpc.Timestamps = new Timestamps(DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(info.Schedule)),
                                                        DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(info.Schedule))
                                                                .Add(TimeSpan.FromSeconds(info.Duration)));

                        rpc.Assets = new Assets
                        {
                            // fall back to Discord's default artwork when the cover url exceeds the limit
                            LargeImageKey  = info.Cover.WithinLength(MaxRpcImageKeyBytes) ? info.Cover : string.Empty,
                            LargeImageText = $"💿 {info.Album}".TruncateByUtf8Bytes(MaxRpcTextBytes),
                            SmallImageKey  = "timg",
                            SmallImageText = "NetEase CloudMusic",
                        };

                        rpc.Buttons =
                        [
                            new Button
                            {
                                Label = "🎧 Listen",
                                Url   = info.Url,
                            },
                            new Button
                            {
                                Label = "👏 View App on GitHub",
                                Url   = "https://github.com/Kxnrl/NetEase-Cloud-Music-DiscordRPC",
                            },
                        ];
                    });
                }

                lastRpcClient = rpcClient;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // 用户就喜欢超低内存占用
                // 但是实际上来说并没有什么卵用
                GC.Collect();
                GC.WaitForFullGCComplete();

                await Task.Delay(TimeSpan.FromMilliseconds(233));
            }
        }
    }
}
