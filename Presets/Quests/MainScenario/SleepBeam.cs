namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record SleepBeam(ulong Id, uint Type, ulong RawSource, ulong RawTarget,
    DungeonActor Source, DungeonActor Target, DateTime Activation, bool SourceRemoved = false, bool TargetRemoved = false);
