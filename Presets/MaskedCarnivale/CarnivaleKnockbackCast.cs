using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record CarnivaleKnockbackCast(long Generation, DungeonPendingCast Cast, float Distance,
    bool StopAtWall, GeometryTemplate? EffectShape, bool Forward = false, bool Pull = false);
