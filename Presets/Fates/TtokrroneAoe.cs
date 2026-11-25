using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record TtokrroneAoe(long Id, DungeonPendingCast Cast, GeometryTemplate Shape,
    Vector3 Position, float Rotation, DateTime Activation, int Stage = 0, bool DangerColor = false);
