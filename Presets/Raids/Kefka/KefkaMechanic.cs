using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record KefkaMechanic(long Generation, KefkaActor Source, uint Action,
    Vector3 Origin, float Rotation, DateTime Activation, GeometryTemplate? Shape = null,
    KefkaActor? Target = null, KefkaDisplacement? Displacement = null, ulong TargetId = 0);
