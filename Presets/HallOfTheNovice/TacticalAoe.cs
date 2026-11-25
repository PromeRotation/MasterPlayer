using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TacticalAoe(long Id, DungeonPendingCast Cast, GeometryTemplate Shape,
    Vector3 Position, float Rotation, DateTime Activation, DateTime PreparedAt);
