using Kxnrl.Vanessa.Models;

namespace Kxnrl.Vanessa;

internal interface IMusicPlayer
{
    bool Validate(int pid);

    PlayerInfo? GetPlayerInfo();
}
