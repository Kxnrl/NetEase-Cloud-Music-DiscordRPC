using System;
using Kxnrl.Vanessa.Models;

namespace Kxnrl.Vanessa.Players;

internal sealed class Tencent : IMusicPlayer
{
    public Tencent(int pid)
        => throw new NotImplementedException();

    public bool Validate(int pid)
        => throw new System.NotImplementedException();

    public PlayerInfo? GetPlayerInfo()
        => throw new System.NotImplementedException();
}
