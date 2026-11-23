namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record InnocenceBait(DungeonActor Source, DungeonActor Target, uint Action, DateTime At, int Key,
    bool SourceDestroyed = false, bool TargetDestroyed = false);
