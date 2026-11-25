namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasurePlayerGaze(DungeonPendingCast Cast, DungeonActor? Target, bool Removed = false);
