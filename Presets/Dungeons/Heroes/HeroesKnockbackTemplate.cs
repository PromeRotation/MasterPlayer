using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record HeroesKnockbackTemplate(Vector3 Origin, IGeometryShape LandingArena) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new HeroesKnockbackGeometry(this);
    internal Vector3 Landing(Vector3 point)
    {
        var delta = point - Origin; delta.Y = 0;
        return point + (delta.LengthSquared() > .000001f ? Vector3.Normalize(delta) * 15 : Vector3.Zero);
    }
    internal bool Unsafe(Vector3 point, float epsilon)
    {
        var landing = Landing(point);
        return !LandingArena.Contains(landing, epsilon)
            || Vector2.DistanceSquared(new(landing.X, landing.Z), new(738, 482)) <= 56.25f
            || Vector2.DistanceSquared(new(landing.X, landing.Z), new(762, 482)) <= 56.25f;
    }
}
