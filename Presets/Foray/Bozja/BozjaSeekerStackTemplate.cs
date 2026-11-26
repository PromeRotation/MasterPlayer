using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaSeekerStackTemplate(BozjaCast[] Lines) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    internal float Distance(float x, float z)
    {
        var minimum = float.MaxValue;
        var inside = 0;
        foreach (var cast in Lines)
        {
            var distance = -new BozjaZone("", "", default, cast.Shape, cast.Origin, cast.Rotation).Distance(x, z);
            if (distance >= 0) ++inside;
            var absolute = MathF.Abs(distance);
            if (absolute < minimum) minimum = absolute;
        }
        return Lines.Length == 1 ? -new BozjaZone("", "", default, Lines[0].Shape, Lines[0].Origin, Lines[0].Rotation).Distance(x, z)
            : inside == 1 ? minimum : -minimum;
    }

    internal IEnumerable<IGeometryShape> Convert(float y)
    {
        // The old bridge converts both SDInvertedRect and SDOutsideOfUnion
        // into the constituent rectangle goals. Keep the raw SDF separate.
        foreach (var cast in Lines)
        {
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var direction = Vector3.Normalize(new((float)sin, 0, (float)cos));
            var shape = (RayRectangleTemplate)cast.Shape;
            yield return new RectangleGeometry((cast.Origin + direction * (shape.Length * .5f)) with { Y = y },
                MathF.Atan2(direction.X, direction.Z), shape.MaxLocalX - shape.MinLocalX, shape.Length);
        }
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Seeker line stack requires complement navigation conversion.");
}
