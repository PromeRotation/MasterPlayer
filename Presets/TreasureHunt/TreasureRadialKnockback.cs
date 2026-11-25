using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

// Cast retains source/generation, locked origin/direction, activation and actual-end lifetime.
internal sealed record TreasureRadialKnockback(DungeonPendingCast Cast, float Distance,
    bool StopAtWall, GeometryTemplate? EffectShape, bool TowardsOrigin = false, float MinDistance = 0);
