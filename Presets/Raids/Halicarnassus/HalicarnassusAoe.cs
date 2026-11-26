using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HalicarnassusAoe(long Generation, ulong Source, uint Action,
    GeometryTemplate Shape, Vector3 Origin, float Rotation, DateTime Activation);
