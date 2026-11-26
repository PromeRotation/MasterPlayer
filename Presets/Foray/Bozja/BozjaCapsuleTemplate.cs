using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Keep the analytic source distance separate from the bridge's 24-segment
// semicircles. Both display and navigation consume that polygon approximation.
internal sealed record BozjaCapsuleTemplate(float Radius, float Length) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal bool Contains(Vector3 origin, float rotation, Vector3 point)
    {
        var (sin, cos) = Math.SinCos(rotation);
        var dx = (float)sin;
        var dz = (float)cos;
        var along = Math.Clamp((point.X - origin.X) * dx + (point.Z - origin.Z) * dz, 0, Length);
        var px = point.X - (origin.X + along * dx);
        var pz = point.Z - (origin.Z + along * dz);
        return px * px + pz * pz <= Radius * Radius;
    }

    internal float Distance(Vector3 origin, float rotation, Vector3 point)
    {
        var (sin, cos) = Math.SinCos(rotation);
        var dx = (float)sin;
        var dz = (float)cos;
        var along = Math.Clamp((point.X - origin.X) * dx + (point.Z - origin.Z) * dz, 0, Length);
        var px = point.X - (origin.X + along * dx);
        var pz = point.Z - (origin.Z + along * dz);
        return MathF.Sqrt(px * px + pz * pz) - Radius;
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
    {
        var (sin, cos) = Math.SinCos(rotation);
        var direction = Vector2.Normalize(new((float)sin, (float)cos));
        var angle = MathF.Atan2(direction.X, direction.Y);
        var end = position + new Vector3(direction.X * Length, 0, direction.Y * Length);
        var points = new List<Vector3>();
        Append(end, angle - MathF.PI / 2);
        Append(position, angle + MathF.PI / 2);
        return new WorldPolygonGeometry(points, position);

        void Append(Vector3 center, float start)
        {
            for (var i = 0; i <= 24; ++i)
                points.Add(GeometryHelper.PointFromRotationDistanceXZ(center, start + MathF.PI * i / 24, Radius));
        }
    }
}
