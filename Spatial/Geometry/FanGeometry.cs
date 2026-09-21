using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>扇形几何区域。Position 表示圆心和射线起点，Rotation 表示扇形中心朝向。</summary>
public sealed class FanGeometry : GeometryShape
{
    /// <summary>创建扇形几何区域。</summary>
    public FanGeometry(Vector3 position, float rotation, float radius, float angleRadians)
        : base(position, rotation)
    {
        if (radius <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), "半径必须大于 0。");
        }

        if (angleRadians <= 0f || angleRadians > 2f * MathF.PI)
        {
            throw new ArgumentOutOfRangeException(nameof(angleRadians), "扇形张角必须位于 (0, 2π] 范围内。");
        }

        Radius = radius;
        AngleRadians = angleRadians;
    }

    public override GeometryKind Kind => GeometryKind.Fan;

    /// <summary>扇形半径。</summary>
    public float Radius { get; }

    /// <summary>扇形总张角，单位为弧度。</summary>
    public float AngleRadians { get; }

    /// <summary>扇形半张角，单位为弧度。</summary>
    public float HalfAngleRadians => AngleRadians * 0.5f;

    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var distanceSquared = GeometryHelper.DistanceSquaredXZ(Position, point);
        if (distanceSquared > (Radius + epsilon) * (Radius + epsilon))
        {
            return false;
        }

        if (distanceSquared <= epsilon * epsilon || AngleRadians >= 2f * MathF.PI - epsilon)
        {
            return true;
        }

        var angleToPoint = GeometryHelper.AngleTo(Position, point);
        var delta = MathF.Abs(MathHelper.NormalizeAngle(angleToPoint - Rotation));
        return delta <= HalfAngleRadians + epsilon;
    }

    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        segments = Math.Max(2, segments);
        var arcSegments = Math.Max(2, (int)MathF.Ceiling(segments * AngleRadians / (2f * MathF.PI)));
        var points = new List<Vector3>(arcSegments + 2) { Position };
        var startAngle = Rotation - HalfAngleRadians;
        var step = AngleRadians / arcSegments;

        for (var i = 0; i <= arcSegments; i++)
        {
            points.Add(GeometryHelper.PointFromRotationDistanceXZ(Position, startAngle + step * i, Radius));
        }

        return SingleContour(points);
    }
}
