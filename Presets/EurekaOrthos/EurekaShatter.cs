using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record EurekaShatter(long Generation, DungeonPendingCast Trigger, AlphaActorState Crystal,
    Vector3 Origin, float Rotation, DateTime PreparedAt, DateTime Activation, GeometryTemplate Shape);
