using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>月环几何区域。<see cref="GeometryShape.Position"/> 表示圆心。</summary>
public sealed class DonutGeometry : GeometryShape
{
    /// <summary>创建月环几何区域。</summary>
    public DonutGeometry(Vector3 position, float innerRadius, float outerRadius)
        : base(position)
    {
        if (innerRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(innerRadius), "内半径不能为负数。");
        }

        if (outerRadius <= 0f || outerRadius <= innerRadius)
        {
            throw new ArgumentOutOfRangeException(nameof(outerRadius), "外半径必须大于内半径，并且必须大于 0。");
        }

        InnerRadius = innerRadius;
        OuterRadius = outerRadius;
    }

    public override GeometryKind Kind => GeometryKind.Donut;

    /// <summary>内半径，点距离小于该值时不属于月环。</summary>
    public float InnerRadius { get; }

    /// <summary>外半径，点距离大于该值时不属于月环。</summary>
    public float OuterRadius { get; }

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var distanceSquared = GeometryHelper.DistanceSquaredXZ(Position, point);
        var inner = MathF.Max(0f, InnerRadius - epsilon);
        var outer = OuterRadius + epsilon;
        return distanceSquared >= inner * inner && distanceSquared <= outer * outer;
    }

    public override bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (Contains(start, epsilon) || Contains(end, epsilon))
        {
            return true;
        }

        var minDistanceSquared = GeometryHelper.DistanceSquaredPointToSegmentXZ(Position, start, end);
        var maxDistanceSquared = MathF.Max(
            GeometryHelper.DistanceSquaredXZ(Position, start),
            GeometryHelper.DistanceSquaredXZ(Position, end));
        var inner = MathF.Max(0f, InnerRadius - epsilon);
        var outer = OuterRadius + epsilon;

        return minDistanceSquared <= outer * outer && maxDistanceSquared >= inner * inner;
    }

    public override GeometryBounds GetBounds() => GeometryBounds.FromCenterRadius(Position, OuterRadius);

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        segments = Math.Max(8, segments);
        var outer = new List<Vector3>(segments);
        var inner = new List<Vector3>(segments);

        for (var i = 0; i < segments; i++)
        {
            var angle = 2f * MathF.PI * i / segments;
            outer.Add(GeometryHelper.PointFromRotationDistanceXZ(Position, angle, OuterRadius));
            inner.Add(GeometryHelper.PointFromRotationDistanceXZ(Position, angle, InnerRadius));
        }

        return new IReadOnlyList<Vector3>[] { outer, inner };
    }
}
