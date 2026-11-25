using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaExcaliburAoe(long Generation, GeometryTemplate Shape, Vector3 Origin,
    float Rotation, DateTime Activation, DateTime PreparedAt, DungeonPendingCast Cast);
