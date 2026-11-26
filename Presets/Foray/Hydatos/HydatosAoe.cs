using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hydatos;

internal sealed record HydatosAoe(long Id, HydatosRule Rule, HydatosActor Source, GeometryTemplate? Shape,
    Vector3 Origin, float Rotation, DateTime Activation, DateTime StartedAt, HydatosActor? Target = null,
    bool Risky = true, bool Danger = false, int Remaining = 0, float MinDistance = 0);
