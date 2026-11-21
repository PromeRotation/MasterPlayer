using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceLandingTemplate(Vector3 Origin, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new PalaceLandingGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        var direction = point - Origin;
        direction.Y = 0;
        if (direction.LengthSquared() < .0001f) return point;
        var destination = point + Vector3.Normalize(direction) * 25;
        if (PalaceArena.Boundary.Contains(destination)) return destination;
        // Valfodr stops at walls: retain the last point inside the actual clipped arena.
        var lo = 0f;
        var hi = 1f;
        for (var i = 0; i < 16; ++i)
        {
            var mid = (lo + hi) / 2;
            if (PalaceArena.Boundary.Contains(Vector3.Lerp(point, destination, mid), -.02f)) lo = mid;
            else hi = mid;
        }
        return Vector3.Lerp(point, destination, lo);
    }
}
