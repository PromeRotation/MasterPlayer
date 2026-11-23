namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DungeonActionRequest(uint Action, ulong Target, DateTime Until);
