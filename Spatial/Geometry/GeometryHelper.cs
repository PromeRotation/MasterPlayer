using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>
/// Geometry 和脚本层共用的几何工具。
/// <para>
/// 默认使用 Dalamud/游戏世界坐标：<c>Vector3.X</c> 和 <c>Vector3.Z</c> 为地面平面，
/// <c>Vector3.Y</c> 为高度。
/// </para>
/// <para>
/// Rotation 是水平旋转弧度：0 朝世界 +Z，π/2 朝世界 +X。
/// 传入未标准化角度也可以工作，例如 10 弧度会自然等价于标准化后的方向。
/// </para>
/// </summary>
public static class GeometryHelper
{
    /// <summary>默认几何容差，用于浮点比较、边界判定和共线判断。</summary>
    public const float DefaultEpsilon = MathHelper.DefaultEpsilon;

    /// <summary>
    /// 将顺时针角度制旋转量转换为 Rotation 弧度增量。
    /// <para>FFXIV 方向角按逆时针增加，因此顺时针 42.39° 等于 -42.39°。</para>
    /// </summary>
    public static float ClockwiseDegreesToRadians(float degrees) => -MathHelper.DegreesToRadians(degrees);

    /// <summary>将逆时针角度制旋转量转换为 Rotation 弧度增量。</summary>
    public static float CounterClockwiseDegreesToRadians(float degrees) => MathHelper.DegreesToRadians(degrees);

    /// <summary>将世界坐标投影到地面 X/Z 平面，返回 <c>(X, Z)</c>。</summary>
    public static Vector2 ToPlaneXZ(this Vector3 vector) => new(vector.X, vector.Z);

    /// <summary>计算二维平面两点距离的平方。</summary>
    public static float DistanceSquared2D(Vector2 a, Vector2 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>计算二维平面两点距离。</summary>
    public static float Distance2D(Vector2 a, Vector2 b) => MathF.Sqrt(DistanceSquared2D(a, b));

    /// <summary>计算世界坐标在 X/Z 平面上的距离平方，忽略高度 Y。</summary>
    public static float DistanceSquaredXZ(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dz = a.Z - b.Z;
        return dx * dx + dz * dz;
    }

    /// <summary>计算世界坐标在 X/Z 平面上的距离，忽略高度 Y。</summary>
    public static float DistanceXZ(Vector3 a, Vector3 b) => MathF.Sqrt(DistanceSquaredXZ(a, b));

    /// <summary>
    /// 根据 Rotation 得到 X/Z 平面的前方向量。
    /// <para>Rotation = 0 时返回 (0, 1)，表示世界 +Z。</para>
    /// </summary>
    public static Vector2 ForwardFromRotation(float rotation)
        => new(MathF.Sin(rotation), MathF.Cos(rotation));

    /// <summary>
    /// 根据 Rotation 得到 X/Z 平面的右方向量。
    /// <para>该方向对应 Geometry 局部坐标中的 <c>LocalX &gt; 0</c>。</para>
    /// </summary>
    public static Vector2 RightFromRotation(float rotation)
        => new(-MathF.Cos(rotation), MathF.Sin(rotation));

    /// <summary>根据 Rotation 得到世界坐标中的前方向量，Y 固定为 0。</summary>
    public static Vector3 ForwardVector3FromRotation(float rotation)
    {
        var forward = ForwardFromRotation(rotation);
        return new Vector3(forward.X, 0f, forward.Y);
    }

    /// <summary>
    /// 计算从 <paramref name="from"/> 指向 <paramref name="to"/> 的 Rotation。
    /// </summary>
    public static float AngleTo(Vector3 from, Vector3 to)
        => MathF.Atan2(to.X - from.X, to.Z - from.Z);

    /// <summary>
    /// 计算二维平面从 <paramref name="from"/> 指向 <paramref name="to"/> 的 Rotation。
    /// <para>这里 Vector2.Y 表示平面第二轴，等价于世界 Z。</para>
    /// </summary>
    public static float AngleTo(Vector2 from, Vector2 to)
        => MathF.Atan2(to.X - from.X, to.Y - from.Y);

    /// <summary>
    /// 计算从 <paramref name="fromRotation"/> 到 <paramref name="toRotation"/> 的最短有符号角度差。
    /// </summary>
    public static float RelativeAngle(float fromRotation, float toRotation)
        => MathHelper.NormalizeAngle(toRotation - fromRotation);

    /// <summary>
    /// 判断 <paramref name="rotation"/> 是否位于从 <paramref name="startRotation"/> 到 <paramref name="endRotation"/> 的逆时针区间内。
    /// </summary>
    public static bool IsAngleBetween(float rotation, float startRotation, float endRotation)
    {
        var value = MathHelper.PositiveModulo(rotation + MathF.PI, 2f * MathF.PI);
        var start = MathHelper.PositiveModulo(startRotation + MathF.PI, 2f * MathF.PI);
        var end = MathHelper.PositiveModulo(endRotation + MathF.PI, 2f * MathF.PI);
        return start <= end ? start <= value && value <= end : start <= value || value <= end;
    }

    /// <summary>从起点沿指定 Rotation 前进指定距离，返回世界坐标。高度 Y 保持起点高度。</summary>
    public static Vector3 PointFromRotationDistanceXZ(Vector3 origin, float rotation, float distance)
    {
        var forward = ForwardFromRotation(rotation);
        return new Vector3(
            origin.X + forward.X * distance,
            origin.Y,
            origin.Z + forward.Y * distance);
    }

    /// <summary>
    /// 从中心点沿绝对 Rotation 偏移指定距离，返回世界坐标。
    /// <para>Rotation = 0 表示 +Z，π/2 表示 +X。</para>
    /// </summary>
    public static Vector3 PointFromPolarRadiansXZ(Vector3 center, float distance, float rotation)
        => PointFromRotationDistanceXZ(center, rotation, distance);

    /// <summary>
    /// 从中心点沿绝对角度偏移指定距离，返回世界坐标。
    /// <para>0° 表示 +Z，90° 表示 +X，-90° 表示 -X，180°/-180° 表示 -Z。</para>
    /// </summary>
    public static Vector3 PointFromPolarDegreesXZ(Vector3 center, float distance, float degrees)
        => PointFromPolarRadiansXZ(center, distance, MathHelper.DegreesToRadians(degrees));

    /// <summary>
    /// 从中心点沿基础朝向加相对角度偏移指定距离，返回世界坐标。
    /// <para><paramref name="deltaDegrees"/> 正数表示逆时针，负数表示顺时针。</para>
    /// </summary>
    public static Vector3 PointFromRelativePolarDegreesXZ(
        Vector3 center,
        float baseRotation,
        float distance,
        float deltaDegrees)
        => PointFromPolarRadiansXZ(center, distance, baseRotation + MathHelper.DegreesToRadians(deltaDegrees));

    /// <summary>
    /// 将局部二维坐标转换为世界坐标。
    /// <para>局部 <c>LocalY &lt; 0</c> 是图形前方，局部 <c>LocalX &gt; 0</c> 是图形右侧。</para>
    /// </summary>
    public static Vector3 LocalToWorld(Vector2 localPoint, Vector3 position, float rotation)
    {
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);

        return new Vector3(
            position.X - localPoint.X * cos - localPoint.Y * sin,
            position.Y,
            position.Z + localPoint.X * sin - localPoint.Y * cos);
    }

    /// <summary>将世界坐标转换为某个 Geometry 的局部二维坐标。</summary>
    public static Vector2 WorldToLocal(Vector3 worldPoint, Vector3 position, float rotation)
    {
        var dx = worldPoint.X - position.X;
        var dz = worldPoint.Z - position.Z;
        var sin = MathF.Sin(rotation);
        var cos = MathF.Cos(rotation);

        return new Vector2(
            -cos * dx + sin * dz,
            -sin * dx - cos * dz);
    }

    /// <summary>
    /// 将二维点围绕中心按 Rotation 增量旋转。
    /// <para>正值表示逆时针，顺时针请传负值或使用 <see cref="ClockwiseDegreesToRadians(float)"/>。</para>
    /// </summary>
    public static Vector2 RotatePointAroundCenter(Vector2 point, Vector2 center, float rotationDelta)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var sin = MathF.Sin(rotationDelta);
        var cos = MathF.Cos(rotationDelta);

        return new Vector2(
            center.X + dx * cos + dy * sin,
            center.Y - dx * sin + dy * cos);
    }

    /// <summary>
    /// 将世界坐标点围绕中心在 X/Z 平面旋转。返回值保留原点自身的 Y 高度。
    /// </summary>
    public static Vector3 RotatePointAroundCenterXZ(Vector3 point, Vector3 center, float rotationDelta)
    {
        var rotated = RotatePointAroundCenter(point.ToPlaneXZ(), center.ToPlaneXZ(), rotationDelta);
        return new Vector3(rotated.X, point.Y, rotated.Y);
    }

    /// <summary>将世界坐标点围绕中心逆时针旋转指定角度。</summary>
    public static Vector3 RotatePointAroundCenterDegreesXZ(Vector3 point, Vector3 center, float degrees)
        => RotatePointAroundCenterXZ(point, center, MathHelper.DegreesToRadians(degrees));

    /// <summary>将世界坐标点围绕中心顺时针旋转指定角度。</summary>
    public static Vector3 RotatePointClockwiseDegreesXZ(Vector3 point, Vector3 center, float degrees)
        => RotatePointAroundCenterXZ(point, center, ClockwiseDegreesToRadians(degrees));

    /// <summary>
    /// 计算目标点投影到射线方向上的有符号距离。
    /// <para>结果为负表示投影点位于射线起点后方。</para>
    /// </summary>
    public static float ProjectDistanceXZ(Vector3 source, float rotation, Vector3 target)
    {
        var dx = target.X - source.X;
        var dz = target.Z - source.Z;
        return dx * MathF.Sin(rotation) + dz * MathF.Cos(rotation);
    }

    /// <summary>
    /// 计算目标点到射线中心线的有符号横向距离。
    /// <para>正值表示目标位于射线左侧，负值表示目标位于射线右侧。</para>
    /// </summary>
    public static float ProjectSignedHeightXZ(Vector3 source, float rotation, Vector3 target)
    {
        var dx = target.X - source.X;
        var dz = target.Z - source.Z;
        return dx * MathF.Cos(rotation) - dz * MathF.Sin(rotation);
    }

    /// <summary>计算目标点到射线中心线的绝对横向距离。</summary>
    public static float ProjectHeightXZ(Vector3 source, float rotation, Vector3 target)
        => MathF.Abs(ProjectSignedHeightXZ(source, rotation, target));

    /// <summary>判断目标点是否位于从 source 出发、指定半宽的无限射线矩形内。</summary>
    public static bool IsPointInRayXZ(Vector3 source, float rotation, float halfWidth, Vector3 target)
        => ProjectDistanceXZ(source, rotation, target) >= 0f &&
           ProjectHeightXZ(source, rotation, target) <= halfWidth;

    /// <summary>计算二维叉积。</summary>
    public static float Cross2D(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    /// <summary>计算以 <paramref name="origin"/> 为共同起点的二维叉积。</summary>
    public static float Cross2D(Vector2 origin, Vector2 a, Vector2 b)
        => Cross2D(a - origin, b - origin);

    /// <summary>计算二维点积。</summary>
    public static float Dot2D(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>判断点是否位于线段上，包含端点。</summary>
    public static bool IsPointOnSegment2D(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd,
        float epsilon = DefaultEpsilon)
    {
        if (MathF.Abs(Cross2D(segmentStart, segmentEnd, point)) > epsilon)
        {
            return false;
        }

        return point.X >= MathF.Min(segmentStart.X, segmentEnd.X) - epsilon &&
               point.X <= MathF.Max(segmentStart.X, segmentEnd.X) + epsilon &&
               point.Y >= MathF.Min(segmentStart.Y, segmentEnd.Y) - epsilon &&
               point.Y <= MathF.Max(segmentStart.Y, segmentEnd.Y) + epsilon;
    }

    /// <summary>判断两条二维线段是否相交，包含端点接触和共线重叠。</summary>
    public static bool SegmentsIntersect2D(
        Vector2 aStart,
        Vector2 aEnd,
        Vector2 bStart,
        Vector2 bEnd,
        float epsilon = DefaultEpsilon)
    {
        var d1 = Cross2D(aStart, aEnd, bStart);
        var d2 = Cross2D(aStart, aEnd, bEnd);
        var d3 = Cross2D(bStart, bEnd, aStart);
        var d4 = Cross2D(bStart, bEnd, aEnd);

        if (((d1 > epsilon && d2 < -epsilon) || (d1 < -epsilon && d2 > epsilon)) &&
            ((d3 > epsilon && d4 < -epsilon) || (d3 < -epsilon && d4 > epsilon)))
        {
            return true;
        }

        return IsPointOnSegment2D(bStart, aStart, aEnd, epsilon) ||
               IsPointOnSegment2D(bEnd, aStart, aEnd, epsilon) ||
               IsPointOnSegment2D(aStart, bStart, bEnd, epsilon) ||
               IsPointOnSegment2D(aEnd, bStart, bEnd, epsilon);
    }

    /// <summary>判断两条世界坐标线段在 X/Z 平面投影后是否相交。</summary>
    public static bool SegmentsIntersectXZ(
        Vector3 aStart,
        Vector3 aEnd,
        Vector3 bStart,
        Vector3 bEnd,
        float epsilon = DefaultEpsilon)
        => SegmentsIntersect2D(aStart.ToPlaneXZ(), aEnd.ToPlaneXZ(), bStart.ToPlaneXZ(), bEnd.ToPlaneXZ(), epsilon);

    /// <summary>计算点到二维线段的最短距离平方。</summary>
    public static float DistanceSquaredPointToSegment2D(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        var segment = segmentEnd - segmentStart;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= DefaultEpsilon * DefaultEpsilon)
        {
            return DistanceSquared2D(point, segmentStart);
        }

        var t = Math.Clamp(Dot2D(point - segmentStart, segment) / lengthSquared, 0f, 1f);
        var projection = segmentStart + segment * t;
        return DistanceSquared2D(point, projection);
    }

    /// <summary>计算世界坐标点到世界坐标线段在 X/Z 平面上的最短距离平方。</summary>
    public static float DistanceSquaredPointToSegmentXZ(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
        => DistanceSquaredPointToSegment2D(point.ToPlaneXZ(), segmentStart.ToPlaneXZ(), segmentEnd.ToPlaneXZ());

    /// <summary>
    /// 判断二维点是否位于多边形内。
    /// <para>边界点视为在多边形内。</para>
    /// </summary>
    public static bool PointInPolygon2D(
        Vector2 point,
        IReadOnlyList<Vector2> vertices,
        float epsilon = DefaultEpsilon)
    {
        if (vertices.Count < 3)
        {
            return false;
        }

        var inside = false;
        for (var i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];

            if (IsPointOnSegment2D(point, a, b, epsilon))
            {
                return true;
            }

            var crosses = (a.Y > point.Y) != (b.Y > point.Y);
            if (!crosses)
            {
                continue;
            }

            var xAtY = (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X;
            if (point.X < xAtY)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// 将弧度方向映射到等分方向编号。
    /// <para>正 divisions 表示北方是一个编号点，例如 4 表示 N/W/S/E。</para>
    /// <para>负 divisions 表示北方位于两个编号点中间，例如 -4 表示 NW/SW/SE/NE。</para>
    /// </summary>
    public static float RadToDir(float rotation, int divisions, int digits = 0)
    {
        if (divisions == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisions), "方向等分数不能为 0。");
        }

        var segmentCount = Math.Abs(divisions);
        var dir = (rotation / MathF.PI + 1f) * 0.5f * segmentCount;
        if (divisions < 0)
        {
            dir -= 0.5f;
        }

        dir = MathHelper.PositiveModulo(dir + 0.5f, segmentCount) - 0.5f;
        return digits >= 0 ? MathF.Round(dir, digits) : dir;
    }

    /// <summary>旧名称兼容：等价于 <see cref="RadToDir(float, int, int)"/>。</summary>
    public static float RoundDir(float rotation, int divisions, int digits = 0)
        => RadToDir(rotation, divisions, digits);

    /// <summary>将方向向量映射到等分方向编号。Vector2.Y 表示平面第二轴，等价于世界 Z。</summary>
    public static float VecToDir(Vector2 vector, int divisions, int digits = 0)
        => RadToDir(MathF.Atan2(vector.X, vector.Y), divisions, digits);

    /// <summary>将从 <paramref name="from"/> 指向 <paramref name="to"/> 的方向映射到等分方向编号。</summary>
    public static float VecToDir(Vector3 from, Vector3 to, int divisions, int digits = 0)
        => RadToDir(AngleTo(from, to), divisions, digits);

    /// <summary>将等分方向编号还原为 Rotation 弧度。</summary>
    public static float DirToRad(float direction, int divisions)
    {
        if (divisions == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(divisions), "方向等分数不能为 0。");
        }

        var segmentCount = Math.Abs(divisions);
        if (divisions < 0)
        {
            direction += 0.5f;
        }

        return -MathF.PI + 2f * MathF.PI * MathHelper.PositiveModulo(direction / segmentCount, 1f);
    }
}
