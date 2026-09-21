using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>中心矩形几何区域。Position 表示矩形中心。</summary>
public sealed class RectangleGeometry : GeometryShape
{
    /// <summary>创建中心矩形几何区域。</summary>
    public RectangleGeometry(Vector3 position, float rotation, float width, float length)
        : base(position, rotation)
    {
        if (width <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "宽度必须大于 0。");
        }

        if (length <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "长度必须大于 0。");
        }

        Width = width;
        Length = length;
    }

    public override GeometryKind Kind => GeometryKind.Rectangle;

    /// <summary>矩形总宽度，对应局部 LocalX 方向。</summary>
    public float Width { get; }

    /// <summary>矩形总长度，对应局部 LocalY 方向。</summary>
    public float Length { get; }

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var local = WorldToLocal(point);
        return MathF.Abs(local.X) <= Width * 0.5f + epsilon &&
               MathF.Abs(local.Y) <= Length * 0.5f + epsilon;
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        var halfWidth = Width * 0.5f;
        var halfLength = Length * 0.5f;
        var points = new List<Vector3>
        {
            LocalToWorld(new Vector2(-halfWidth, -halfLength)),
            LocalToWorld(new Vector2(halfWidth, -halfLength)),
            LocalToWorld(new Vector2(halfWidth, halfLength)),
            LocalToWorld(new Vector2(-halfWidth, halfLength))
        };

        return SingleContour(points);
    }
}
