using System;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// 危险窗口。
/// <para>时间一律使用 <see cref="DateTime.UtcNow"/> 为基准的绝对 UTC 时间；相对秒数的转换由 <c>SafeZoneRegistry</c> 在写入阶段统一完成。</para>
/// <para>区间语义为闭区间：<c>now &gt;= DangerFrom &amp;&amp; (DangerUntil == null || now &lt;= DangerUntil)</c>。</para>
/// <para>
/// <see cref="TelegraphFrom"/> 是**渲染专用**字段：仅供绘制层做"预告充能 → 命中"的视觉渐变，
/// 表示该危险何时开始向玩家预告（充能开始）。Solver 与一切危险/安全判定**必须忽略它**——
/// 危险的事实区间永远只由 <see cref="DangerFrom"/> / <see cref="DangerUntil"/> 决定。
/// 在 <see cref="DangerFrom"/> 之前该区域不致命，玩家仍可站立。
/// </para>
/// </summary>
public readonly record struct DangerWindow(DateTime DangerFrom, DateTime? DangerUntil, DateTime? TelegraphFrom = null)
{
    // A swept moving body already includes sampling/trajectory uncertainty.
    // Applying cast preparation time to every slice blocks positions it has left.
    internal bool HasMotionEnvelope { get; init; }
    internal DangerWindow Buffered(float start, float end) => HasMotionEnvelope ? this
        : new(DangerFrom.AddSeconds(-MathF.Max(0, start)), DangerUntil?.AddSeconds(MathF.Max(0, end)), TelegraphFrom);
    /// <summary>创建一个永久危险窗口（无结束时间，无预告）。</summary>
    public static DangerWindow Persistent(DateTime dangerFrom) => new(dangerFrom, null, null);

    /// <summary>判断窗口在指定 UTC 时间是否处于激活（致命）状态。仅由 DangerFrom / DangerUntil 决定，与 <see cref="TelegraphFrom"/> 无关。</summary>
    public bool IsActiveAt(DateTime utcNow)
        => utcNow >= DangerFrom && (DangerUntil is null || utcNow <= DangerUntil.Value);

    /// <summary>
    /// 判断危险区间 <c>[DangerFrom, DangerUntil]</c> 是否与给定时间区间 <c>[fromUtc, toUtc]</c> 有重叠。
    /// <para>
    /// 用于路径线段的时间安全检查：玩家通过某条会穿越危险几何的线段时，占用的是一段时间区间，
    /// 单点 <see cref="IsActiveAt"/> 无法表达，故需要区间重叠判定。
    /// </para>
    /// <para>与 <see cref="IsActiveAt"/> 一样，**只看 <see cref="DangerFrom"/> / <see cref="DangerUntil"/>，绝不读 <see cref="TelegraphFrom"/>**。</para>
    /// <para>入参顺序若颠倒会自动归一；<see cref="DangerUntil"/> 为空表示危险持续到正无穷。</para>
    /// </summary>
    public bool OverlapsInterval(DateTime fromUtc, DateTime toUtc)
    {
        var lo = fromUtc <= toUtc ? fromUtc : toUtc;
        var hi = fromUtc <= toUtc ? toUtc : fromUtc;

        // 区间 [lo, hi] 与 [DangerFrom, DangerUntil] 重叠：hi >= DangerFrom 且 (无上界 || lo <= DangerUntil)。
        return hi >= DangerFrom && (DangerUntil is null || lo <= DangerUntil.Value);
    }

    /// <summary>
    /// 计算指定 UTC 时间下的预告充能进度，取值 <c>[0, 1]</c>。
    /// <para>纯渲染辅助：无 <see cref="TelegraphFrom"/> 或已不早于 <see cref="DangerFrom"/> 时返回 1（无预告段，直接视为已就绪）。</para>
    /// </summary>
    public float TelegraphProgressAt(DateTime utcNow)
    {
        if (TelegraphFrom is null || TelegraphFrom.Value >= DangerFrom)
        {
            return 1f;
        }

        if (utcNow <= TelegraphFrom.Value)
        {
            return 0f;
        }

        if (utcNow >= DangerFrom)
        {
            return 1f;
        }

        var total = (DangerFrom - TelegraphFrom.Value).TotalSeconds;
        return total <= 0d ? 1f : (float)((utcNow - TelegraphFrom.Value).TotalSeconds / total);
    }
}
