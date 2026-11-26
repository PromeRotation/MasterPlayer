using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Source SDIntersection of inverted 2x2 rectangles, consumed through the
// existing finite forbidden-zone sampler rather than converted into goals.
internal sealed record BozjaPhantomShelterTemplate(Vector3[] Positions) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal float Distance(float x, float z)
    {
        var distance = float.MinValue;
        foreach (var p in Positions)
        {
            var next = MathF.Min(1 - MathF.Abs(x - p.X), 1 - MathF.Abs(z - p.Z));
            if (next > distance) distance = next;
        }
        return distance;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Phantom shelter intersection requires finite navigation sampling.");
}
