using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// Original cast and actor identity remain separate from the cached visibility projection.
internal sealed record CarnivaleLineOfSightCast(long Generation, AlphaActorState Caster,
    DungeonPendingCast Cast, Vector3 Origin, Vector2[][] BlockerContours, IGeometryShape Visibility);
