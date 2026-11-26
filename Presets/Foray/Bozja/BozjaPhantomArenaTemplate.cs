using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaPhantomArenaTemplate(Vector3 Origin) : GeometryTemplate
{
    // The upstream Phantom shape is one static AOEShapeCustom shared by both
    // difficulties. Polygon and Distance each lock their own first query
    // origin; a new component or encounter does not reset either cache.
    private static Vector3? polygonOrigin;
    private static Vector3? distanceOrigin;

    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => Geometry(position, polygonOrigin ??= position with { Y = 0 });

    internal IEnumerable<IGeometryShape> Convert(float y)
    {
        var baseline = polygonOrigin ??= Origin with { Y = 0 };
        var lockedOrigin = distanceOrigin ??= Origin with { Y = 0 };
        return Geometry(lockedOrigin with { Y = y }, baseline).GetContours()
            .Select(c => (IGeometryShape)new WorldPolygonGeometry(c));
    }

    private static IGeometryShape Geometry(Vector3 origin, Vector3 baseline)
    {
        var source = (PolygonRegionGeometry)DelubrumArenas.PhantomHazard(origin.Y);
        var center = source.Position + (origin - baseline) with { Y = origin.Y };
        Vector2[] Local(IReadOnlyList<Vector3> points)
            => points.Select(p => new Vector2(p.X - source.Position.X, p.Z - source.Position.Z)).ToArray();
        return new PolygonRegionGeometry(center, source.GetContours().Select(Local).ToArray(), [],
            source.FillPolygons.Select(Local).ToArray());
    }
}
