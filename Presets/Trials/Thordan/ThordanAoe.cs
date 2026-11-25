using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record ThordanAoe(long Generation, ThordanComponent Component, ThordanActor Source,
    uint Action, Vector3 Origin, float Rotation, DateTime Activation, GeometryTemplate Shape, bool Risky = true);
