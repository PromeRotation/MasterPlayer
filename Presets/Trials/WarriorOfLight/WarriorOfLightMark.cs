namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record WarriorOfLightMark(DungeonActor Target, uint Action, DateTime At, int Key,
    bool Destroyed = false, ulong ForbiddenPlayer = 0);
