using MasterPlayer.RemoteControlTests;

namespace MasterPlayer;

public sealed class MasterPlayerPlugin
{
    public static MasterPlayerPlugin Instance { get; } = new();
    public ReplayState Replay { get; } = new();
}
