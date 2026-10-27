using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

public sealed class WorldPolygonGeometry : IGeometryShape
{
    public WorldPolygonGeometry(IEnumerable<Vector3> worldPoints)
    {
        var points = worldPoints.ToList();
        if (points.Any(point =>
                !float.IsFinite(point.X) ||
                !float.IsFinite(point.Y) ||
                !float.IsFinite(point.Z)))
        {
            throw new ArgumentException("多边形顶点必须是有限值。", nameof(worldPoints));
        }

        var planePoints = PolygonGeometryHelpers.NormalizeAndValidate(
            points.Select(point => point.ToPlaneXZ()),
            nameof(worldPoints));

        WorldPoints = points.Count > 1 && GeometryHelper.DistanceSquared2D(points[0].ToPlaneXZ(), points[^1].ToPlaneXZ()) <= GeometryHelper.DefaultEpsilon * GeometryHelper.DefaultEpsilon
            ? points.Take(points.Count - 1).ToList().AsReadOnly()
            : points.AsReadOnly();

        PlanePoints = planePoints;
        Position = PolygonGeometryHelpers.ComputeCentroid(WorldPoints);
    }

    public GeometryKind Kind => GeometryKind.WorldPolygon;

    public Vector3 Position { get; }

    public float Rotation => 0f;

    public IReadOnlyList<Vector3> WorldPoints { get; }

    private IReadOnlyList<Vector2> PlanePoints { get; }

    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => GeometryHelper.PointInPolygon2D(point.ToPlaneXZ(), PlanePoints, epsilon);

    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (Contains(start, epsilon) || Contains(end, epsilon))
        {
            return true;
        }

        return GeometryShape.ContourIntersectsSegment(WorldPoints, start, end, epsilon);
    }

    public GeometryBounds GetBounds() => GeometryBounds.FromPoints(WorldPoints);

    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
        => new IReadOnlyList<Vector3>[] { WorldPoints };
}
