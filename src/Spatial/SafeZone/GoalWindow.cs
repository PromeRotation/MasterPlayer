using System;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// 目标窗口。
/// <para>时间一律使用 <see cref="DateTime.UtcNow"/> 为基准的绝对 UTC 时间；相对秒数的转换由 <c>SafeZoneRegistry</c> 在写入阶段统一完成。</para>
/// <para>区间语义为闭区间：<c>now &gt;= GoalFrom &amp;&amp; (GoalUntil == null || now &lt;= GoalUntil)</c>。</para>
/// </summary>
public readonly record struct GoalWindow(
    DateTime GoalFrom,
    DateTime? GoalUntil,
    int Priority,
    PositionPreference Preference)
{
    /// <summary>机制明确安全区可让危险外扩余量让位；攻击范围等可选偏好必须关闭。</summary>
    public bool OverrideDangerMargin { get; init; } = true;
    /// <summary>Center 偏好的评分锚点；目标几何和危险余量保持不变。</summary>
    public Vector3? PreferredPosition { get; init; }
    /// <summary>机制站位在等待解算或原地等待时仍需暂停宿主；普通任务导航应关闭。</summary>
    public bool RequiresMovementHandoff { get; init; } = true;

    /// <summary>创建一个永久目标窗口（无结束时间）。</summary>
    public static GoalWindow Persistent(DateTime goalFrom, int priority = 0, PositionPreference preference = PositionPreference.Center)
        => new(goalFrom, null, priority, preference);

    /// <summary>判断窗口在指定 UTC 时间是否处于激活状态。</summary>
    public bool IsActiveAt(DateTime utcNow)
        => utcNow >= GoalFrom && (GoalUntil is null || utcNow <= GoalUntil.Value);
}
