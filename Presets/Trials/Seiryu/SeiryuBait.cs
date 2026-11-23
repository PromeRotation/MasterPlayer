namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record SeiryuBait(DungeonActor Source, DungeonActor Target, DateTime At, int Key, uint Action,
    bool SourceDestroyed = false, bool TargetDestroyed = false, ulong Forbidden = 0);
