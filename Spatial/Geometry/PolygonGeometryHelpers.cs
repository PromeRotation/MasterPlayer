using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

internal static class PolygonGeometryHelpers
{
    public static IReadOnlyList<Vector2> NormalizeAndValidate(
        IEnumerable<Vector2> points,
        string parameterName,
        float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var list = points.ToList();
        if (list.Count > 1 && GeometryHelper.DistanceSquared2D(list[0], list[^1]) <= epsilon * epsilon)
        {
            list.RemoveAt(list.Count - 1);
        }

        if (list.Count < 3)
        {
            throw new ArgumentException("多边形至少需要 3 个有效顶点。", parameterName);
        }

        if (list.Any(point => !float.IsFinite(point.X) || !float.IsFinite(point.Y)))
        {
            throw new ArgumentException("多边形顶点必须是有限值。", parameterName);
        }

        for (var i = 0; i < list.Count; i++)
        {
            var current = list[i];
            var next = list[(i + 1) % list.Count];
            if (GeometryHelper.DistanceSquared2D(current, next) <= epsilon * epsilon)
            {
                throw new ArgumentException("多边形存在相邻重复顶点。", parameterName);
            }
        }

        var signedArea = SignedArea(list);
        if (!float.IsFinite(signedArea) || MathF.Abs(signedArea) <= epsilon)
        {
            throw new ArgumentException("多边形面积必须大于 0，所有顶点不能共线。", parameterName);
        }

        EnsureNoSelfIntersections(list, parameterName, epsilon);
        return list.AsReadOnly();
    }

    public static float SignedArea(IReadOnlyList<Vector2> points)
    {
        if (points.Count < 3)
        {
            return 0f;
        }

        var origin = points[0];
        var sum = 0f;
        for (var i = 1; i < points.Count - 1; i++)
        {
            sum += GeometryHelper.Cross2D(
                points[i] - origin,
                points[i + 1] - origin);
        }

        return sum * 0.5f;
    }

    public static Vector3 ComputeCentroid(IReadOnlyList<Vector3> points)
    {
        var sum = Vector3.Zero;
        foreach (var point in points)
        {
            sum += point;
        }

        return sum / points.Count;
    }

    private static void EnsureNoSelfIntersections(
        IReadOnlyList<Vector2> points,
        string parameterName,
        float epsilon)
    {
        for (var i = 0; i < points.Count; i++)
        {
            var a1 = points[i];
            var a2 = points[(i + 1) % points.Count];

            for (var j = i + 1; j < points.Count; j++)
            {
                if (AreAdjacentEdges(i, j, points.Count))
                {
                    continue;
                }

                var b1 = points[j];
                var b2 = points[(j + 1) % points.Count];
                if (GeometryHelper.SegmentsIntersect2D(a1, a2, b1, b2, epsilon))
                {
                    throw new ArgumentException("多边形边不能自相交。", parameterName);
                }
            }
        }
    }

    private static bool AreAdjacentEdges(int firstEdgeIndex, int secondEdgeIndex, int count)
        => Math.Abs(firstEdgeIndex - secondEdgeIndex) == 1 ||
           firstEdgeIndex == 0 && secondEdgeIndex == count - 1;
}
