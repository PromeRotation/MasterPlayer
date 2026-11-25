namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TreasureDelayedHit(ulong Source, uint Action, DateTime StartedAt, ulong TargetId,
    int CapturedSlot, DateTime Activation);
