using System;
using System.Numerics;

namespace MasterPlayer.Spatial.Geometry;

/// <summary>
/// PromeRotation 通用数学工具。
/// <para>
/// 这里保留纯数学、单位换算和旧兼容方法；X/Z 平面、Rotation、局部坐标和脚本几何辅助请使用 <see cref="GeometryHelper"/>。
/// </para>
/// </summary>
public static class MathHelper
{
    /// <summary>默认浮点容差，用于近似比较和几何边界判定。</summary>
    public const float DefaultEpsilon = 1e-5f;

    /// <summary>旧式 ACT/XY 方位角函数。保留用于兼容旧调用。</summary>
    public static double θ(float PosX1, float PosY1, float PosX2, float PosY2)
    {
        return Math.Atan2(PosX1 - PosX2, PosY1 - PosY2);
    }

    /// <summary>
    /// 旧式 ACT/XY 方位角函数。计算 <paramref name="Pos1"/> 面向 <paramref name="Pos2"/> 的方向角。
    /// </summary>
    /// <param name="Pos1">ACT/XY 平面起点。</param>
    /// <param name="Pos2">ACT/XY 平面终点。</param>
    public static double θVector2(Vector2 Pos1, Vector2 Pos2)
    {
        return Math.Atan2(Pos1.X - Pos2.X, Pos1.Y - Pos2.Y);
    }

    /// <summary>将角度标准化到 (-π, π] 范围内。</summary>
    public static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2 * Math.PI;
        }

        while (angle <= -Math.PI)
        {
            angle += 2 * Math.PI;
        }

        return angle;
    }

    /// <summary>将角度标准化到 (-π, π] 范围内。</summary>
    public static float NormalizeAngle(float angle)
    {
        while (angle > MathF.PI)
        {
            angle -= 2 * MathF.PI;
        }

        while (angle <= -MathF.PI)
        {
            angle += 2 * MathF.PI;
        }

        return angle;
    }

    /// <summary>将角度制转换为弧度制。Rotation 入参默认都应使用弧度。</summary>
    public static float DegreesToRadians(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>将弧度制转换为角度制，主要用于调试显示。</summary>
    public static float RadiansToDegrees(float radians) => radians * 180f / MathF.PI;

    /// <summary>判断两个浮点数是否在指定容差内近似相等。</summary>
    public static bool NearlyEquals(float a, float b, float epsilon = DefaultEpsilon)
        => MathF.Abs(a - b) <= epsilon;

    /// <summary>取模并保证结果落在 [0, <paramref name="modulo"/>)。</summary>
    public static float PositiveModulo(float value, float modulo)
    {
        var result = value % modulo;
        return result < 0f ? result + modulo : result;
    }

    /// <summary>对世界坐标进行小数位四舍五入。</summary>
    public static Vector3 Round(this Vector3 vector, int dot)
    {
        return new Vector3((float)Math.Round(vector.X, dot),
                           (float)Math.Round(vector.Y, dot),
                           (float)Math.Round(vector.Z, dot));
    }
}
