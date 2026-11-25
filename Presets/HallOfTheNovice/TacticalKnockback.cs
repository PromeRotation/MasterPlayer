namespace MasterPlayer.Mechanics.Dungeons;

// Source SimpleKnockbacks: radial away, no wall stop, immunity checked at each original activation.
internal sealed record TacticalKnockback(DungeonPendingCast Cast, float Distance, float? Radius);
