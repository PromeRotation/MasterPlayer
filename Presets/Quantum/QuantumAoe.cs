using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record QuantumAoe(long Generation, QuantumActor Source, uint Action, GeometryTemplate Shape,
    Vector3 Position, float Rotation, DateTime Activation);
