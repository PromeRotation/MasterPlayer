using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

public static class PolygonTriangulator
{
    public static bool TryTriangulate(
        IReadOnlyList<Vector3> points,
        out int[] triangleIndices,
        float epsilon = GeometryHelper.DefaultEpsilon)
    {
        triangleIndices = [];
        if (!float.IsFinite(epsilon) || epsilon < 0f || points.Count < 3)
        {
            return false;
        }

        var planePoints = new Vector2[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (!float.IsFinite(point.X) ||
                !float.IsFinite(point.Y) ||
                !float.IsFinite(point.Z))
            {
                return false;
            }

            planePoints[i] = point.ToPlaneXZ();
        }

        var signedArea = PolygonGeometryHelpers.SignedArea(planePoints);
        if (!float.IsFinite(signedArea) || MathF.Abs(signedArea) <= epsilon)
        {
            return false;
        }

        var winding = signedArea > 0f ? 1f : -1f;
        var remaining = Enumerable.Range(0, points.Count).ToList();
        RemoveCollinearVertices(planePoints, remaining, epsilon);
        if (remaining.Count < 3)
        {
            return false;
        }

        var indices = new List<int>((remaining.Count - 2) * 3);
        while (remaining.Count > 3)
        {
            var earFound = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                var current = remaining[i];
                var next = remaining[(i + 1) % remaining.Count];
                if (winding * GeometryHelper.Cross2D(
                        planePoints[previous],
                        planePoints[current],
                        planePoints[next]) <= epsilon ||
                    ContainsOtherPoint(
                        planePoints,
                        remaining,
                        previous,
                        current,
                        next,
                        winding,
                        epsilon))
                {
                    continue;
                }

                indices.Add(previous);
                indices.Add(current);
                indices.Add(next);
                remaining.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound)
            {
                return false;
            }
        }

        if (winding * GeometryHelper.Cross2D(
                planePoints[remaining[0]],
                planePoints[remaining[1]],
                planePoints[remaining[2]]) <= epsilon)
        {
            return false;
        }

        indices.Add(remaining[0]);
        indices.Add(remaining[1]);
        indices.Add(remaining[2]);
        triangleIndices = indices.ToArray();
        return true;
    }

    private static void RemoveCollinearVertices(
        IReadOnlyList<Vector2> points,
        List<int> remaining,
        float epsilon)
    {
        var removed = true;
        while (removed && remaining.Count > 3)
        {
            removed = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                var current = remaining[i];
                var next = remaining[(i + 1) % remaining.Count];
                if (MathF.Abs(GeometryHelper.Cross2D(
                        points[previous],
                        points[current],
                        points[next])) <= epsilon &&
                    GeometryHelper.IsPointOnSegment2D(
                        points[current],
                        points[previous],
                        points[next],
                        epsilon))
                {
                    remaining.RemoveAt(i);
                    removed = true;
                    break;
                }
            }
        }
    }

    private static bool ContainsOtherPoint(
        IReadOnlyList<Vector2> points,
        IReadOnlyList<int> remaining,
        int a,
        int b,
        int c,
        float winding,
        float epsilon)
    {
        foreach (var index in remaining)
        {
            if (index == a || index == b || index == c)
            {
                continue;
            }

            var point = points[index];
            if (winding * GeometryHelper.Cross2D(points[a], points[b], point) >= -epsilon &&
                winding * GeometryHelper.Cross2D(points[b], points[c], point) >= -epsilon &&
                winding * GeometryHelper.Cross2D(points[c], points[a], point) >= -epsilon)
            {
                return true;
            }
        }

        return false;
    }
}
