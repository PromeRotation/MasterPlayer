namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureCastBait(long Id, DungeonActor Source, DungeonActor Target,
    DungeonPendingCast Cast, bool TargetRemoved, bool SameActor, bool SourceRemoved = false);
