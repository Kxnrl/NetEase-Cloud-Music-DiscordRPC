using System;
using System.Diagnostics;
using System.Text;
using Kxnrl.Vanessa.Models;
using Kxnrl.Vanessa.Win32Api;

namespace Kxnrl.Vanessa.Players;

internal sealed class Tencent : IMusicPlayer
{
    private const string CurrentSOngInfoPattern
        = "B9 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? E8 ? ? ? ? B9";

    private const string AlbumThumbnailPattern
        = "A1 ? ? ? ? 6A ? 51 8B CC 51 89 01 8B C4 8D 4D ? C7 00 ? ? ? ? FF 15 ? ? ? ? A1 ? ? ? ? 6A ? 51 8B CC 51 89 01 8B C4 8D 4D ? C7 00 ? ? ? ? FF 15 ? ? ? ? 6A ? FF 35 ? ? ? ? 51 8B C4 8D 4D ? C7 00 ? ? ? ? FF 15 ? ? ? ? 6A ? FF 35 ? ? ? ? 51 8B C4 8D 4D ? C7 00 ? ? ? ? FF 15 ? ? ? ? 6A ? FF 35 ? ? ? ? 51 8B C4 C7 00";

    private const string IsPausedPattern
        = "0F 29 05 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? C7 05 ? ? ? ? ? ? ? ? C7 05 ? ? ? ? ? ? ? ? E8";

    private readonly nint          _currentSongInfoAddress;
    private readonly nint          _isPausedAddress;
    private readonly nint          _albumThumbnailAddress;

    private readonly int           _pid;
    private readonly ProcessMemory _process;

    public Tencent(int pid)
    {
        _pid = pid;
        using var p = Process.GetProcessById(pid);

        foreach (ProcessModule module in p.Modules)
        {
            if (!"QQMusic.dll".Equals(module.ModuleName))
            {
                continue;
            }

            var process = new ProcessMemory(pid);
            var address = module.BaseAddress;

            if (Memory.FindPattern(CurrentSOngInfoPattern, pid, address, out var patternAddress))
            {
                var currentSongAddress = process.ReadInt32(patternAddress, 1);
                _currentSongInfoAddress = currentSongAddress;
            }

            if (Memory.FindPattern(IsPausedPattern, pid, address, out patternAddress))
            {
                var isPausedAddress = process.ReadInt32(patternAddress, 3);
                _isPausedAddress = isPausedAddress + 4;
            }

            if (Memory.FindPattern(AlbumThumbnailPattern, pid, address, out patternAddress))
            {
                var albumThumbnailAddress = process.ReadInt32(patternAddress, 1);
                _albumThumbnailAddress = albumThumbnailAddress;
            }

            _process = process;

            break;
        }

        if (_process is null)
        {
            throw new EntryPointNotFoundException("Failed to find process");
        }

        if (_currentSongInfoAddress == 0)
        {
            throw new EntryPointNotFoundException("_currentSongAddress is 0");
        }
    }

    public bool Validate(int pid)
        => _pid == pid;

    public PlayerInfo? GetPlayerInfo()
    {
        var id = GetSongIdentity();

        if (id == 0)
        {
            return null;
        }

        return new PlayerInfo
        {
            Identity = id.ToString(),
            Title    = GetSongName(),
            Artists  = GetArtistName(),
            Album    = GetAlbumName(),
            Cover    = GetAlbumThumbnailUrl(),
            Schedule = GetSongSchedule() * 0.001,
            Duration = GetSongDuration() * 0.001,
            Pause    = IsPaused(),

            // lock
            Url = $"https://y.qq.com/n/ryqq/songDetail/{id}",
        };
    }

    private uint GetSongIdentity()
        => _process.ReadUInt32(_currentSongInfoAddress);

    private int GetSongDuration()
        => _process.ReadInt32(_currentSongInfoAddress, 0x10);

    private int GetSongSchedule()
        => _process.ReadInt32(_currentSongInfoAddress, 0xC);

    private string GetSongName()
    {
        var address = _process.ReadInt32(_currentSongInfoAddress);
        var bytes   = _process.ReadBytes(address, 512);

        var result = Encoding.Unicode.GetString(bytes);

        return result[..result.IndexOf('\0')];
    }

    private string GetArtistName()
    {
        var address = _process.ReadInt32(_currentSongInfoAddress, 4);
        var bytes   = _process.ReadBytes(address, 512);

        var result = Encoding.Unicode.GetString(bytes);

        return result[..result.IndexOf('\0')];
    }

    private string GetAlbumName()
    {
        var address = _process.ReadInt32(_currentSongInfoAddress, 8);
        var bytes   = _process.ReadBytes(address, 512);

        var result = Encoding.Unicode.GetString(bytes);

        return result[..result.IndexOf('\0')];
    }

    private string GetAlbumThumbnailUrl()
    {
        var address = _process.ReadInt32(_albumThumbnailAddress);
        var bytes   = _process.ReadBytes(address, 512);

        var result = Encoding.Unicode.GetString(bytes);

        return result[..result.IndexOf('\0')];
    }

    private bool IsPaused()
        => _process.ReadInt32(_isPausedAddress) == 1;
}
