using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaKaliyaAoe(long Generation, string Kind, DungeonPendingCast Cast,
    GeometryTemplate Shape, Vector3 Origin, float Rotation, DateTime Activation);
