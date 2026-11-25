using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PalaceOfTheDead;

internal sealed record PalaceLandingTemplate(Vector3 Origin, IGeometryShape[] Dangers, float HitboxRadius = .5f) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new PalaceLandingGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        var direction = point - Origin;
        direction.Y = 0;
        if (direction.LengthSquared() == 0) return point;
        direction = Vector3.Normalize(direction);
        var destination = point + direction * 25;
        // The source subtracts the transformed player's hitbox correction even near a wall.
        var correction = MathF.Max(.001f, HitboxRadius - .499f);
        if (PalaceArena.Boundary.Contains(destination, 0))
            return PalaceArena.Boundary.Contains(destination + direction * correction, 0) ? destination : WallLanding();
        return WallLanding();

        Vector3 WallLanding()
        {
            // Valfodr stops at walls: retain the last point inside the actual clipped arena.
            var lo = 0f;
            var hi = 25f + correction;
            for (var i = 0; i < 24; ++i)
            {
                var mid = (lo + hi) / 2;
                if (PalaceArena.Boundary.Contains(point + direction * mid, 0)) lo = mid;
                else hi = mid;
            }
            return point + direction * MathF.Min(25, lo - correction);
        }
    }
}
