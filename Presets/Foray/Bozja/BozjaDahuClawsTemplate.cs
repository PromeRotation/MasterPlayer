using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Keep the raw union. The host first tries to convert all members directly;
// only if any member fails does it sample the whole union.
internal sealed record BozjaDahuClawsTemplate(Vector3 Center, Vector3[] Claws) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    private IEnumerable<BozjaZone> Zones()
    {
        foreach (var claw in Claws)
        {
            var delta = claw - Center;
            var half = (float)Math.Asin(8f / MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z));
            yield return new("", "", default, new FanTemplate(100, 2 * half),
                Center, MathF.Atan2(delta.X, delta.Z));
        }
    }

    internal float Distance(float x, float z)
    {
        var distance = float.MaxValue;
        foreach (var zone in Zones())
        {
            var next = zone.Distance(x, z);
            if (next < distance) distance = next;
        }
        return distance;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
    {
        var result = new List<IGeometryShape>();
        foreach (var zone in Zones())
        {
            var cone = (FanTemplate)zone.Shape!;
            if (!float.IsFinite(cone.AngleRadians) || zone.TryDirectCone(cone, center.Y) is not { } direct)
                return GeometryGridSampler.Sample(center, radius, Distance);
            result.Add(direct);
        }
        return result;
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Dahu claw union must use the finite navigation sampler.");
}
