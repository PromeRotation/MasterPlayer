using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>Resolved polygon islands and holes, with a separate triangulation for filling and clipping.</summary>
internal sealed class PolygonRegionGeometry : GeometryShape
{
    private readonly Vector2[][] exteriors;
    private readonly Vector2[][] holes;
    private readonly IReadOnlyList<IReadOnlyList<Vector3>> contours;
    internal IReadOnlyList<IReadOnlyList<Vector3>> FillPolygons { get; }

    internal PolygonRegionGeometry(Vector3 center, Vector2[][] exteriors, Vector2[][] holes, Vector2[][] triangles,
        IReadOnlyList<IReadOnlyList<Vector3>>? boundaryContours = null) : base(center)
    {
        Vector3[] World(Vector2[] points) => points.Select(p => center + new Vector3(p.X, 0, p.Y)).ToArray();
        // Boolean clipping can produce valid slivers below the public polygon
        // constructor's area threshold. Keep resolved pieces in local coordinates
        // instead of rounding them to world floats before containment tests.
        this.exteriors = exteriors;
        this.holes = holes;
        contours = boundaryContours ?? exteriors.Concat(holes).Select(p => (IReadOnlyList<Vector3>)World(p)).ToArray();
        FillPolygons = triangles.Select(p => (IReadOnlyList<Vector3>)World(p)).ToArray();
    }

    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var local = new Vector2(point.X - Position.X, point.Z - Position.Z);
        // Resolved parts can contain an island inside another part's hole
        // (a disc surrounded by a disconnected ring). Count signed coverage
        // rather than subtracting every hole from every exterior.
        return exteriors.Count(p => GeometryHelper.PointInPolygon2D(local, p, epsilon))
            > holes.Count(p => GeometryHelper.PointInPolygon2D(local, p, epsilon) && !OnBoundary(p, local, epsilon));
    }

    private static bool OnBoundary(IReadOnlyList<Vector2> polygon, Vector2 p, float epsilon)
    {
        for (var i = 0; i < polygon.Count; ++i)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            var edge = b - a;
            var t = edge.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, edge) / edge.LengthSquared(), 0, 1) : 0;
            if (Vector2.DistanceSquared(p, a + edge * t) <= epsilon * epsilon) return true;
        }
        return false;
    }
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => contours;
}
