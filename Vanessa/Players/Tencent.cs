using System;
using System.Diagnostics;
using System.Text;
using Kxnrl.Vanessa.Models;
using Kxnrl.Vanessa.Win32Api;

namespace Kxnrl.Vanessa.Players;

internal sealed class Tencent : IMusicPlayer
{
    // 如何更新Pattern:
    // 1. 在 QQMusic.dll 里搜字符串 "Tencent Technology (Shenzhen) Company Limited", 并且找到引用的函数（理论上只有一个引用）
    // 2. 引用到字符串的是一个初始化的函数，在引用到字符串的地方往上找类似这样的伪代码，理论上来讲这个伪代码会重复3次
    // byte_xxxxx = 0;
    // dword_xxxxx+0x10 = 0;
    // dword_xxxxx+0x14 = 15;
    // 这个是初始化 std::string, 我们要找的是第一个，这个是当前播放的歌的名字
    // 3. 选中然后在汇编页面生成Pattern
    private const string CurrentSongInfoPattern
        = "A2 ? ? ? ? A3 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? A2 ? ? ? ? A3 ? ? ? ? C7 05 ? ? ? ? ? ? ? ? A2 ? ? ? ? A3";

    private const int StdStringSize = 0x18;

    private readonly nint          _currentSongInfoAddress;

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

            if (Memory.FindPattern(CurrentSongInfoPattern, pid, address, out var patternAddress))
            {
                var currentSongAddress = process.ReadInt32(patternAddress, 1);
                _currentSongInfoAddress = currentSongAddress;
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
            throw new EntryPointNotFoundException("_currentSongInfoAddress is 0");
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
        => _process.ReadUInt32(_currentSongInfoAddress, StdStringSize * 4);

    private int GetSongDuration()
        => _process.ReadInt32(_currentSongInfoAddress, (StdStringSize * 4) + 8 /*跳过Id跟一个4字节大小的玩意*/);

    private int GetSongSchedule()
        => _process.ReadInt32(_currentSongInfoAddress, (StdStringSize * 4) + 12);

    private string GetSongName()
        => ReadStdString(_currentSongInfoAddress);

    private string GetArtistName()
        => ReadStdString(_currentSongInfoAddress + StdStringSize);

    private string GetAlbumName()
        => ReadStdString(_currentSongInfoAddress + (StdStringSize * 2));

    private string GetAlbumThumbnailUrl()
        => ReadStdString(_currentSongInfoAddress + (StdStringSize * 3));

    private string ReadStdString(nint address)
    {
        var strLength = _process.ReadInt32(address, 0x10);

        if (strLength == 0)
        {
            return string.Empty;
        } // small string optimization

        byte[] strBuffer;

        if (strLength <= 15)
        {
            strBuffer = _process.ReadBytes(address, strLength);
        }
        else
        {
            var strAddress = _process.ReadInt32(address);
            strBuffer = _process.ReadBytes(strAddress, strLength);
        }

        return Encoding.UTF8.GetString(strBuffer);
    }

    private bool IsPaused()
        => _process.ReadInt32(_currentSongInfoAddress, (StdStringSize * 4) + 16) == 0;
}
