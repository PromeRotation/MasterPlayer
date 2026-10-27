using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>X/Z 平面上的轴对齐边界盒。</summary>
public readonly record struct GeometryBounds(float MinX, float MinZ, float MaxX, float MaxZ)
{
    public float Width => MaxX - MinX;

    public float Height => MaxZ - MinZ;

    public Vector2 Center => new((MinX + MaxX) * 0.5f, (MinZ + MaxZ) * 0.5f);

    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => point.X >= MinX - epsilon &&
           point.X <= MaxX + epsilon &&
           point.Z >= MinZ - epsilon &&
           point.Z <= MaxZ + epsilon;

    public GeometryBounds Expand(float amount)
        => new(MinX - amount, MinZ - amount, MaxX + amount, MaxZ + amount);

    public static GeometryBounds FromCenterRadius(Vector3 center, float radius)
        => new(center.X - radius, center.Z - radius, center.X + radius, center.Z + radius);

    public static GeometryBounds FromPoints(IReadOnlyList<Vector3> points)
    {
        if (points.Count == 0)
        {
            throw new ArgumentException("至少需要一个点才能计算边界盒。", nameof(points));
        }

        var minX = points[0].X;
        var minZ = points[0].Z;
        var maxX = points[0].X;
        var maxZ = points[0].Z;

        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
            minX = MathF.Min(minX, point.X);
            minZ = MathF.Min(minZ, point.Z);
            maxX = MathF.Max(maxX, point.X);
            maxZ = MathF.Max(maxZ, point.Z);
        }

        return new GeometryBounds(minX, minZ, maxX, maxZ);
    }
}
