using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record AlteRoiteMechanic(long Generation, AlteRoiteActor Source, uint Action,
    Vector3 Origin, float Rotation, DateTime Activation, GeometryTemplate? Shape = null);
