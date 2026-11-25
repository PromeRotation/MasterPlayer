namespace MasterPlayer.Mechanics.Dungeons;

// Zoom In has one independent last-start-wins slot. Its origin is the module's
// current primary position, rather than the charge source or its locked location.
internal sealed record CarnivaleChargeKnockback(long Generation, DungeonPendingCast Cast, float Distance = 20);
