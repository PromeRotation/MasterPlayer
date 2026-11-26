using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaGauntletWindTemplate(Vector3 Center, Vector3 Origin, float Rotation,
    bool Away, BozjaCast[] Aoes) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal float Distance(float x, float z)
    {
        float dx;
        float dz;
        if (Away)
        {
            var ox = x - Origin.X;
            var oz = z - Origin.Z;
            var length = MathF.Sqrt(ox * ox + oz * oz);
            var inverse = length > 0 ? 1f / length : 0;
            dx = 15f * (ox * inverse);
            dz = 15f * (oz * inverse);
        }
        else
        {
            var (sin, cos) = Math.SinCos(Rotation);
            dx = 40f * (float)sin;
            dz = 40f * (float)cos;
        }
        var projected = new Vector3(x + dx, Center.Y, z + dz);
        var half = Away ? 19f : 22f;
        if (!(MathF.Abs(projected.X - Center.X) <= half && MathF.Abs(projected.Z - Center.Z) <= half)) return 0;
        foreach (var aoe in Aoes)
        {
            if (Away)
            {
                var ax = projected.X - aoe.Origin.X;
                var az = projected.Z - aoe.Origin.Z;
                if (ax * ax + az * az <= 81f) return 0;
            }
            else if (aoe.Contains(projected)) return 0;
        }
        return 1;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Gauntlet knockback predicates require the finite navigation sampler.");
}
