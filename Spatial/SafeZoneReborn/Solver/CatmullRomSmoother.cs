using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// Catmull-Rom 路径平滑：对连续移动段生成曲线，按固定距离重采样并重算时间。
/// <para>平滑不跨越等待节点：移动→等待→移动 视为两个独立移动段分别平滑。</para>
/// <para>本类只产出平滑后的路径，安全验证与降强度重试循环由 <see cref="SafeZoneSolver"/> 编排。</para>
/// </summary>
internal static class CatmullRomSmoother
{
    /// <summary>
    /// 对路径做 Catmull-Rom 平滑。
    /// </summary>
    /// <param name="path">已简化 / 直线化的路径（含可能的等待节点）。</param>
    /// <param name="speed">规划移动速度（yalm/秒），重采样后重算到达时间用。</param>
    /// <param name="strength">平滑强度 <c>[0,1]</c>。0 退化为原路径，1 最弯。</param>
    /// <param name="sampleSpacing">曲线重采样间距（yalm）。</param>
    public static IReadOnlyList<TimedWaypoint> Smooth(
        IReadOnlyList<TimedWaypoint> path,
        float speed,
        float strength,
        float sampleSpacing)
    {
        if (path.Count < 3 || strength <= 0f)
        {
            return path;
        }

        var spacing = MathF.Max(0.1f, sampleSpacing);
        var clampedStrength = Math.Clamp(strength, 0f, 1f);

        // 按等待节点切分为多个连续移动段。每段独立平滑，等待节点原样保留在段之间。
        var output = new List<TimedWaypoint> { path[0] };

        var segment = new List<TimedWaypoint> { path[0] };
        for (var i = 1; i < path.Count; i++)
        {
            var wp = path[i];
            if (wp.IsWaitPoint)
            {
                // 结束当前移动段（平滑后追加，去掉与 output 末尾重复的首点），再追加等待节点。
                FlushSegment(output, segment, clampedStrength, spacing);
                output.Add(wp);
                segment = new List<TimedWaypoint> { wp };
            }
            else
            {
                segment.Add(wp);
            }
        }

        FlushSegment(output, segment, clampedStrength, spacing);

        // 平滑改变了路径长度，重算时间。
        return RecomputeTimes(output, speed);
    }

    /// <summary>对一个连续移动段平滑并追加到 output（跳过与 output 末尾重复的段首点）。</summary>
    private static void FlushSegment(List<TimedWaypoint> output, List<TimedWaypoint> segment, float strength, float spacing)
    {
        if (segment.Count < 2)
        {
            return;
        }

        if (segment.Count == 2)
        {
            // 两点段无可平滑，直接接末点。
            AppendSkippingFirst(output, segment);
            return;
        }

        var pts = new List<Vector3>(segment.Count);
        foreach (var wp in segment)
        {
            pts.Add(wp.Position);
        }

        var sampled = SampleCatmullRom(pts, strength, spacing);

        // sampled[0] == segment[0].Position（已在 output 末尾），从 1 开始追加。
        for (var i = 1; i < sampled.Count; i++)
        {
            output.Add(new TimedWaypoint(sampled[i], default, default, IsWaitPoint: false, SpeedMultiplier: 1f));
        }
    }

    private static void AppendSkippingFirst(List<TimedWaypoint> output, List<TimedWaypoint> segment)
    {
        for (var i = 1; i < segment.Count; i++)
        {
            output.Add(segment[i]);
        }
    }

    /// <summary>对一组控制点生成 Catmull-Rom 曲线并按固定间距重采样。端点用镜像虚拟点。strength 缩放切线。</summary>
    private static List<Vector3> SampleCatmullRom(List<Vector3> control, float strength, float spacing)
    {
        var result = new List<Vector3> { control[0] };

        for (var i = 0; i + 1 < control.Count; i++)
        {
            var p0 = i > 0 ? control[i - 1] : control[i] + (control[i] - control[i + 1]);
            var p1 = control[i];
            var p2 = control[i + 1];
            var p3 = i + 2 < control.Count ? control[i + 2] : control[i + 1] + (control[i + 1] - control[i]);

            var segLength = GeometryHelper.DistanceXZ(p1, p2);
            var steps = Math.Max(1, (int)MathF.Ceiling(segLength / spacing));

            for (var s = 1; s <= steps; s++)
            {
                var t = (float)s / steps;
                result.Add(CatmullRom(p0, p1, p2, p3, t, strength));
            }
        }

        return result;
    }

    /// <summary>标准 Catmull-Rom 插值。strength 缩放切线强度：1 标准，0 退化为线性。Y 取 p1/p2 线性插值（平面只用 X/Z）。</summary>
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t, float strength)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        // 切线（Catmull-Rom：0.5*(p2-p0) 与 0.5*(p3-p1)），按 strength 缩放。
        var m1 = (p2 - p0) * 0.5f * strength;
        var m2 = (p3 - p1) * 0.5f * strength;

        var a = (2f * t3) - (3f * t2) + 1f;
        var b = t3 - (2f * t2) + t;
        var c = (-2f * t3) + (3f * t2);
        var d = t3 - t2;

        var pos = (a * p1) + (b * m1) + (c * p2) + (d * m2);

        // 高度按线性插值，几何只在 X/Z 平面，Y 不参与曲率。
        pos.Y = p1.Y + ((p2.Y - p1.Y) * t);
        return pos;
    }

    /// <summary>按重采样后的路径长度 / 速度重算时间，保留等待节点的等待时长。</summary>
    private static List<TimedWaypoint> RecomputeTimes(List<TimedWaypoint> path, float speed)
    {
        if (path.Count == 0)
        {
            return path;
        }

        var safeSpeed = MathF.Max(speed, 0.1f);
        var result = new List<TimedWaypoint>(path.Count) { path[0] };
        var cursor = path[0].PlannedDepartureTime;

        for (var i = 1; i < path.Count; i++)
        {
            var src = path[i];
            var prev = result[result.Count - 1];

            if (src.IsWaitPoint)
            {
                var waitSeconds = (src.PlannedDepartureTime - src.PlannedArrivalTime).TotalSeconds;
                if (waitSeconds < 0d)
                {
                    waitSeconds = 0d;
                }

                var arrival = cursor;
                var departure = arrival + TimeSpan.FromSeconds(waitSeconds);
                result.Add(new TimedWaypoint(src.Position, arrival, departure, IsWaitPoint: true, src.SpeedMultiplier));
                cursor = departure;
                continue;
            }

            var dist = GeometryHelper.DistanceXZ(prev.Position, src.Position);
            var arrivalTime = cursor + TimeSpan.FromSeconds(dist / safeSpeed);
            result.Add(new TimedWaypoint(src.Position, arrivalTime, arrivalTime, IsWaitPoint: false, src.SpeedMultiplier));
            cursor = arrivalTime;
        }

        return result;
    }
}
