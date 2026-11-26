using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaCast(long Generation, BozjaRule Rule, BozjaActor Source,
    Vector3 Origin, float Rotation, DateTime Activation)
{
    internal GeometryTemplate? ShapeOverride { get; init; }
    internal uint Action { get; init; }
    internal GeometryTemplate Shape => ShapeOverride ?? Rule.Shape!;
    internal bool Drawable => Shape is not RayRectangleTemplate { Length: <= 0 }
        && Shape is not BozjaMeteorTemplate { Geometry.HasDisplay: false };
    internal bool Contains(Vector3 point)
        => Shape is BozjaGauntletArenaTemplate gauntletArena ? gauntletArena.Check(point)
            : Shape is BozjaOrderTemplate order ? order.Check(point)
            : Shape is BozjaMeteorTemplate meteor ? meteor.Geometry.Contains(point)
            : Shape is BozjaCapsuleTemplate capsule ? capsule.Contains(Origin, Rotation, point)
            : Shape is RayRectangleTemplate ray ? ContainsRay(ray, point)
            : Drawable ? DungeonSourceGeometry.Contains(Shape, Origin, Rotation, point)
            : new BozjaZone("", Rule.Component, Activation, Shape, Origin, Rotation).Distance(point.X, point.Z) <= 0;

    private bool ContainsRay(RayRectangleTemplate ray, Vector3 point)
    {
        // Raw BMR Check uses Angle.ToDirection (double SinCos narrowed to
        // float), inclusive dot bounds and no display epsilon. Keep it separate
        // from the host's normalized rectangle used for navigation/drawing.
        var (sin, cos) = Math.SinCos(Rotation);
        var x = point.X - Origin.X;
        var z = point.Z - Origin.Z;
        var along = x * (float)sin + z * (float)cos;
        var side = x * (float)cos - z * (float)sin;
        return along >= 0 && along <= ray.Length && side >= ray.MinLocalX && side <= ray.MaxLocalX;
    }
    // Open-world towers capture all OID0 original actor references lazily,
    // independently per tower, rather than following later party/world changes.
    internal HashSet<BozjaActor>? AllowedSoakers;
    internal BozjaActor? Target;
}
