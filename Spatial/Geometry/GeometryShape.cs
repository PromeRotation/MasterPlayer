using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>大多数 Geometry 图形的抽象基类，提供默认线段相交和边界盒计算。</summary>
public abstract class GeometryShape : IGeometryShape
{
    /// <summary>创建带世界基准位置和朝向的几何图形。</summary>
    protected GeometryShape(Vector3 position, float rotation = 0f)
    {
        Position = position;
        Rotation = rotation;
    }

    /// <inheritdoc />
    public abstract GeometryKind Kind { get; }

    /// <inheritdoc />
    public Vector3 Position { get; }

    /// <inheritdoc />
    public float Rotation { get; }

    /// <inheritdoc />
    public abstract bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon);

    /// <inheritdoc />
    public virtual bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (Contains(start, epsilon) || Contains(end, epsilon))
        {
            return true;
        }

        foreach (var contour in GetContours())
        {
            if (ContourIntersectsSegment(contour, start, end, epsilon))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public virtual GeometryBounds GetBounds()
    {
        var points = new List<Vector3>();
        foreach (var contour in GetContours())
        {
            points.AddRange(contour);
        }

        return points.Count == 0
            ? GeometryBounds.FromCenterRadius(Position, 0f)
            : GeometryBounds.FromPoints(points);
    }

    /// <inheritdoc />
    public abstract IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64);

    /// <summary>将当前图形的局部坐标转换为世界坐标。</summary>
    protected Vector3 LocalToWorld(Vector2 localPoint) => GeometryHelper.LocalToWorld(localPoint, Position, Rotation);

    /// <summary>将世界坐标转换为当前图形的局部坐标。</summary>
    protected Vector2 WorldToLocal(Vector3 worldPoint) => GeometryHelper.WorldToLocal(worldPoint, Position, Rotation);

    protected static IReadOnlyList<IReadOnlyList<Vector3>> SingleContour(IReadOnlyList<Vector3> points)
        => new IReadOnlyList<Vector3>[] { points };

    internal static bool ContourIntersectsSegment(
        IReadOnlyList<Vector3> contour,
        Vector3 start,
        Vector3 end,
        float epsilon)
    {
        if (contour.Count < 2)
        {
            return false;
        }

        for (var i = 0; i < contour.Count; i++)
        {
            var a = contour[i];
            var b = contour[(i + 1) % contour.Count];
            if (GeometryHelper.SegmentsIntersectXZ(start, end, a, b, epsilon))
            {
                return true;
            }
        }

        return false;
    }
}
