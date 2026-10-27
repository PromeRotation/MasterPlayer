using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>圆形几何区域。<see cref="GeometryShape.Position"/> 表示圆心。</summary>
public sealed class CircleGeometry : GeometryShape
{
    /// <summary>创建圆形几何区域。</summary>
    public CircleGeometry(Vector3 position, float radius)
        : base(position)
    {
        if (radius <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "半径必须大于 0。");
        }

        Radius = radius;
    }

    public override GeometryKind Kind => GeometryKind.Circle;

    /// <summary>圆半径，单位为游戏世界距离。</summary>
    public float Radius { get; }

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => GeometryHelper.DistanceSquaredXZ(Position, point) <= (Radius + epsilon) * (Radius + epsilon);

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
        => GeometryHelper.DistanceSquaredPointToSegmentXZ(Position, start, end) <= (Radius + epsilon) * (Radius + epsilon);

    public override GeometryBounds GetBounds() => GeometryBounds.FromCenterRadius(Position, Radius);

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        segments = Math.Max(8, segments);
        var points = new List<Vector3>(segments);
        for (var i = 0; i < segments; i++)
        {
            var angle = 2f * MathF.PI * i / segments;
            points.Add(GeometryHelper.PointFromRotationDistanceXZ(Position, angle, Radius));
        }

        return SingleContour(points);
    }
}
