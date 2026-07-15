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

    // Issue #44: state-driven presence updates.
    // Transmit immediately when the song / pause state / timeline actually
    // changes, plus a periodic resync as a safety net, instead of
    // unconditionally every loop (which defeated the library's
    // SkipIdenticalPresence via jittering UtcNow-based timestamps and flooded
    // the Discord client, which only picks up refreshes every ~2-3s).
    private const int    ClearDebounceLoops   = 6;    // ~1.4s at 233ms/loop: absorbs transient nulls while the player rewrites playingList on song switch
    private const double SeekThresholdSeconds = 3.0;  // playback position drift beyond this means the user sought / track wrapped / long rebuffer
    private const int    ResyncIntervalMs     = 5000; // periodic resync while nothing changed: self-heals dropped updates and accumulated drift

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

        // Snapshot of the presence Discord is currently displaying.
        // sentIdentity == null means nothing is displayed (cleared or never set).
        string? sentIdentity = null;
        double  sentSchedule = 0;
        double  sentDuration = 0;
        long    lastSendTick = Environment.TickCount64 - ResyncIntervalMs;
        var     nullStreak   = 0;

        while (true)
        {
            try
            {
                IMusicPlayer?     player    = null;
                DiscordRpcClient? rpcClient = null;

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

                lastInstance = player;

                // Player switched (NetEase <-> QQMusic): the old presence lives on a
                // different client (different AppId / pipe) and would linger forever.
                // Clear it, then reset tracking so the new player sends immediately.
                if (lastRpcClient is not null && rpcClient is not null && !ReferenceEquals(rpcClient, lastRpcClient))
                {
                    lastRpcClient.ClearPresence();

                    sentIdentity  = null;
                    lastRpcClient = null;
                }

                var pi = player?.GetPlayerInfo();

                Debug.Print(pi is not null ? JsonSerializer.Serialize(pi) : "null");

                if (pi is not { } info || rpcClient is null)
                {
                    // Song switches produce transient nulls while the player rewrites
                    // playingList asynchronously; only clear after the signal has been
                    // gone for several consecutive loops (player exited / stopped),
                    // never on a single glitch.
                    nullStreak = Math.Min(nullStreak + 1, ClearDebounceLoops);

                    if (nullStreak >= ClearDebounceLoops && sentIdentity is not null)
                    {
                        lastRpcClient?.ClearPresence();

                        sentIdentity  = null;
                        lastRpcClient = null;
                    }

                    continue;
                }

                nullStreak = 0;

                var now = Environment.TickCount64;

                if (info.Pause)
                {
                    // Pause means "show nothing": clear immediately (a state change
                    // flushes right away); repeated paused loops are no-ops.
                    if (sentIdentity is not null)
                    {
                        rpcClient.ClearPresence();

                        sentIdentity = null;
                        lastSendTick = now;
                    }
                }
                else
                {
                    // Transmit immediately when the presence no longer matches reality;
                    // otherwise resync every few seconds as a safety net so a dropped
                    // update or accumulated drift heals itself.
                    var songChanged     = !string.Equals(sentIdentity, info.Identity, StringComparison.Ordinal);
                    var durationChanged = !songChanged && Math.Abs(info.Duration - sentDuration) > 1.0;

                    // Seek detection: playback advances linearly, so predict the position
                    // from the last send; drift beyond the threshold means the user sought,
                    // the same song restarted (single-track repeat), or a long rebuffer.
                    var predicted = sentSchedule + ((now - lastSendTick) / 1000.0);
                    var seeked    = !songChanged && Math.Abs(info.Schedule - predicted) > SeekThresholdSeconds;

                    if (!songChanged && !durationChanged && !seeked && now - lastSendTick < ResyncIntervalMs)
                    {
                        continue;
                    }
                    rpcClient.Update(rpc =>
                    {
                        rpc.Details = $"🎵 {info.Title}".TruncateByUtf8Bytes(MaxRpcTextBytes);
                        rpc.State   = $"🎤 {info.Artists}".TruncateByUtf8Bytes(MaxRpcTextBytes);
                        rpc.Type    = ActivityType.Listening;

                        // single UtcNow sample so end - start == Duration exactly
                        var start = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(info.Schedule));

                        rpc.Timestamps = new Timestamps(start, start.Add(TimeSpan.FromSeconds(info.Duration)));

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

                    sentIdentity  = info.Identity;
                    sentSchedule  = info.Schedule;
                    sentDuration  = info.Duration;
                    lastSendTick  = now;
                    lastRpcClient = rpcClient;
                }
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
