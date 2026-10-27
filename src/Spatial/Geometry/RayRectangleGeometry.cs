using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>射线矩形几何区域。Position 表示后边中心，图形向前方延伸。</summary>
public sealed class RayRectangleGeometry : GeometryShape
{
    /// <summary>创建左右对称的射线矩形。</summary>
    public RayRectangleGeometry(Vector3 position, float rotation, float width, float length)
        : this(position, rotation, -width * 0.5f, width * 0.5f, length)
    {
    }

    /// <summary>创建可指定左右局部边界的射线矩形。</summary>
    public RayRectangleGeometry(Vector3 position, float rotation, float minLocalX, float maxLocalX, float length)
        : base(position, rotation)
    {
        if (maxLocalX <= minLocalX)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLocalX), "最大 LocalX 必须大于最小 LocalX。");
        }

        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "长度必须大于 0。");
        }

        MinLocalX = minLocalX;
        MaxLocalX = maxLocalX;
        Length = length;
    }

    public override GeometryKind Kind => GeometryKind.RayRectangle;

    /// <summary>射线矩形左侧局部边界。</summary>
    public float MinLocalX { get; }

    /// <summary>射线矩形右侧局部边界。</summary>
    public float MaxLocalX { get; }

    /// <summary>射线矩形向前延伸的长度。</summary>
    public float Length { get; }

    /// <summary>射线矩形总宽度。</summary>
    public float Width => MaxLocalX - MinLocalX;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var local = WorldToLocal(point);
        return local.X >= MinLocalX - epsilon &&
               local.X <= MaxLocalX + epsilon &&
               local.Y >= -Length - epsilon &&
               local.Y <= epsilon;
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        var points = new List<Vector3>
        {
            LocalToWorld(new Vector2(MinLocalX, 0f)),
            LocalToWorld(new Vector2(MaxLocalX, 0f)),
            LocalToWorld(new Vector2(MaxLocalX, -Length)),
            LocalToWorld(new Vector2(MinLocalX, -Length))
        };

        return SingleContour(points);
    }
}
