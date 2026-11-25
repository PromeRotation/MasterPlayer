namespace MasterPlayer.Scripting;

[Flags]
public enum ScriptTimelineHint
{
    None = 0,
    GroupWithNext = 1 << 0,
    BossCastStart = 1 << 1,
    BossCastEnd = 1 << 2,
    Tankbuster = 1 << 3,
    Raidwide = 1 << 4,
    Knockback = 1 << 5,
    DowntimeStart = 1 << 6,
    DowntimeEnd = 1 << 7,
    PositioningStart = 1 << 8,
    PositioningEnd = 1 << 9,
    VulnerableStart = 1 << 10,
    VulnerableEnd = 1 << 11,
}
