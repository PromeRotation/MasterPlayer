namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TacticalMark(long Id, uint Icon, DungeonActor Target, DateTime Activation, bool Removed = false);
