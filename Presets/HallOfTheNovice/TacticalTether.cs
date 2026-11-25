namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TacticalTether(long Id, DungeonActor Source, DungeonActor Target, DateTime Activation,
    bool SourceRemoved = false, bool TargetRemoved = false);
