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

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var a = WorldToLocal(start); var d = WorldToLocal(end) - a;
        var enter = 0f; var exit = 1f;
        return Clip(a.X, d.X, Width * .5f + epsilon) && Clip(a.Y, d.Y, Length * .5f + epsilon);
        bool Clip(float origin, float direction, float extent)
        {
            if (extent < 0) return false;
            if (MathF.Abs(direction) < 1e-7f) return MathF.Abs(origin) <= extent;
            var p = (-extent - origin) / direction; var q = (extent - origin) / direction;
            enter = MathF.Max(enter, MathF.Min(p, q)); exit = MathF.Min(exit, MathF.Max(p, q));
            return enter <= exit;
        }
    }

    public override GeometryBounds GetBounds()
    {
        var c = MathF.Abs(MathF.Cos(Rotation)); var s = MathF.Abs(MathF.Sin(Rotation));
        var x = (c * Width + s * Length) / 2; var z = (s * Width + c * Length) / 2;
        return new(Position.X - x, Position.Z - z, Position.X + x, Position.Z + z);
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
