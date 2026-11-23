namespace MasterPlayer.Replay;

public readonly record struct ReplayDeepDungeonItem(uint PomanderId, byte Count, byte Flags)
{
    public bool Usable => (Flags & 1) != 0;
    public bool Active => (Flags & 2) != 0;
}
