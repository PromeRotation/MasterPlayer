using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Fates;

internal sealed record CrescentFateCast(long Id, CrescentFateActor Source, CrescentFateRule Rule,
    uint Action, Vector3 Origin, float Rotation, DateTime Activation, DateTime StartedAt,
    GeometryTemplate? ShapeOverride = null, int Order = 0, bool Risky = true, bool Danger = false,
    bool AoeColor = false)
{
    internal GeometryTemplate? Shape => ShapeOverride ?? Rule.Shape;
}
