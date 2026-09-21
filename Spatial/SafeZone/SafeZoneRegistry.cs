using System;
using System.Collections.Generic;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZone;

/// <summary>
/// SafeZone 注册便利层。
/// <para>面向外部插件、机制脚本、Timeline 与 PTL 的调用友好入口：把脚本侧习惯的"相对秒数"在写入阶段一次性转换为绝对 UTC 时间，再委托 <see cref="SafeZoneManager"/> 完成实际注册。</para>
/// <para>本类型不持有任何 Geometry / RuntimeArea / 时间窗口可变状态；所有数据所有权都在 <see cref="SafeZoneManager"/>。Registry 只做"相对秒数 → 绝对 UTC + 模板/绑定构造 + 转发"。</para>
/// <para>所有方法仅供主线程调用，与 <see cref="SafeZoneManager"/> 的线程约束一致。</para>
/// </summary>
public sealed class SafeZoneRegistry
{
    /// <summary>当未显式指定单次命中判定持续时间时使用的默认值（秒）。</summary>
    public const float DefaultHitDurationSeconds = 0.6f;

    private readonly SafeZoneManager _manager;

    /// <summary>使用目标 <see cref="SafeZoneManager"/> 创建注册层。</summary>
    public SafeZoneRegistry(SafeZoneManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    // ============================================================
    // 危险区
    // ============================================================

    /// <summary>注册一个从现在开始、永不结束的持续危险区。</summary>
    public SafeZoneAreaHandle AddPersistentDanger(GeometryTemplate template, TransformBinding binding)
    {
        var now = DateTime.UtcNow;
        return _manager.AddDangerArea(template, binding, new[] { new DangerWindow(now, null) });
    }

    /// <summary>注册一个从现在开始、持续 <paramref name="durationSeconds"/> 秒的危险区。</summary>
    public SafeZoneAreaHandle AddTimedDanger(GeometryTemplate template, TransformBinding binding, float durationSeconds)
    {
        var now = DateTime.UtcNow;
        return _manager.AddDangerArea(
            template,
            binding,
            new[] { new DangerWindow(now, now + TimeSpan.FromSeconds(durationSeconds)) });
    }

    /// <summary>
    /// 注册一个延迟 <paramref name="delaySeconds"/> 秒后判定、判定持续 <paramref name="hitDurationSeconds"/> 秒的单次命中危险区。
    /// <para>典型场景：AOE 预告读条结束的那一下命中。</para>
    /// <para>预告段为 <c>[now, now+delay)</c>：该期间区域尚不致命，仅供绘制层做充能渐变；致命区间仍只是 <c>[now+delay, now+delay+hit]</c>。</para>
    /// </summary>
    public SafeZoneAreaHandle AddDelayedHit(
        GeometryTemplate template,
        TransformBinding binding,
        float delaySeconds,
        float hitDurationSeconds = DefaultHitDurationSeconds)
    {
        var now = DateTime.UtcNow;
        var from = now + TimeSpan.FromSeconds(delaySeconds);
        return _manager.AddDangerArea(
            template,
            binding,
            new[] { new DangerWindow(from, from + TimeSpan.FromSeconds(hitDurationSeconds), now) });
    }

    /// <summary>
    /// 注册一个延迟 <paramref name="delaySeconds"/> 秒后开始的危险区。
    /// <para><paramref name="durationSeconds"/> 小于等于 0 表示开始后永不结束。</para>
    /// <para>预告段为 <c>[now, now+delay)</c>，仅供绘制层做充能渐变；危险事实区间仍只由 DangerFrom/DangerUntil 决定。</para>
    /// </summary>
    public SafeZoneAreaHandle AddDelayedDanger(
        GeometryTemplate template,
        TransformBinding binding,
        float delaySeconds,
        float durationSeconds = 0f)
    {
        var now = DateTime.UtcNow;
        var from = now + TimeSpan.FromSeconds(delaySeconds);
        DateTime? until = durationSeconds > 0f ? from + TimeSpan.FromSeconds(durationSeconds) : null;
        return _manager.AddDangerArea(template, binding, new[] { new DangerWindow(from, until, now) });
    }

    /// <summary>
    /// 在同一几何 + 绑定上注册一组依次出现的危险窗口。
    /// <para>每个元组为 <c>(相对开始秒数, 持续秒数)</c>；持续秒数小于等于 0 表示该段永不结束。</para>
    /// <para>每段的预告起点统一设为注册时刻 <c>now</c>（整组在注册瞬间即对玩家可见），仅供绘制层渐变；致命区间仍只由各段 DangerFrom/DangerUntil 决定。</para>
    /// </summary>
    public SafeZoneAreaHandle AddDangerSequence(
        GeometryTemplate template,
        TransformBinding binding,
        IEnumerable<(float DelaySeconds, float DurationSeconds)> windows)
    {
        if (windows is null) throw new ArgumentNullException(nameof(windows));

        var now = DateTime.UtcNow;
        var resolved = new List<DangerWindow>();
        foreach (var (delaySeconds, durationSeconds) in windows)
        {
            var from = now + TimeSpan.FromSeconds(delaySeconds);
            DateTime? until = durationSeconds > 0f ? from + TimeSpan.FromSeconds(durationSeconds) : null;
            DateTime? telegraph = from > now ? now : null;
            resolved.Add(new DangerWindow(from, until, telegraph));
        }

        return _manager.AddDangerArea(template, binding, resolved);
    }

    // ============================================================
    // 目标区（SafeArea 语义即 GoalArea）
    // ============================================================

    /// <summary>注册一个从现在开始、永不结束的持续目标区。</summary>
    public SafeZoneAreaHandle AddPersistentSafeArea(
        GeometryTemplate template,
        TransformBinding binding,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
    {
        var now = DateTime.UtcNow;
        return _manager.AddGoalArea(template, binding, new[] { new GoalWindow(now, null, priority, preference) });
    }

    /// <summary>注册一个从现在开始、持续 <paramref name="durationSeconds"/> 秒的目标区。</summary>
    public SafeZoneAreaHandle AddTimedSafeArea(
        GeometryTemplate template,
        TransformBinding binding,
        float durationSeconds,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
    {
        var now = DateTime.UtcNow;
        return _manager.AddGoalArea(
            template,
            binding,
            new[] { new GoalWindow(now, now + TimeSpan.FromSeconds(durationSeconds), priority, preference) });
    }

    /// <summary>
    /// 注册一个延迟 <paramref name="delaySeconds"/> 秒后开始的目标区。
    /// <para><paramref name="durationSeconds"/> 小于等于 0 表示开始后永不结束。</para>
    /// </summary>
    public SafeZoneAreaHandle AddDelayedSafeArea(
        GeometryTemplate template,
        TransformBinding binding,
        float delaySeconds,
        float durationSeconds = 0f,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
    {
        var now = DateTime.UtcNow;
        var from = now + TimeSpan.FromSeconds(delaySeconds);
        DateTime? until = durationSeconds > 0f ? from + TimeSpan.FromSeconds(durationSeconds) : null;
        return _manager.AddGoalArea(template, binding, new[] { new GoalWindow(from, until, priority, preference) });
    }

    /// <summary>
    /// 在同一几何 + 绑定上注册一组依次出现的目标窗口。
    /// <para>每个元组为 <c>(相对开始秒数, 持续秒数)</c>；持续秒数小于等于 0 表示该段永不结束。</para>
    /// <para>同一序列内所有窗口共用同一 <paramref name="priority"/> 与 <paramref name="preference"/>。</para>
    /// </summary>
    public SafeZoneAreaHandle AddSafeAreaSequence(
        GeometryTemplate template,
        TransformBinding binding,
        IEnumerable<(float DelaySeconds, float DurationSeconds)> windows,
        int priority = 0,
        PositionPreference preference = PositionPreference.Center)
    {
        if (windows is null) throw new ArgumentNullException(nameof(windows));

        var now = DateTime.UtcNow;
        var resolved = new List<GoalWindow>();
        foreach (var (delaySeconds, durationSeconds) in windows)
        {
            var from = now + TimeSpan.FromSeconds(delaySeconds);
            DateTime? until = durationSeconds > 0f ? from + TimeSpan.FromSeconds(durationSeconds) : null;
            resolved.Add(new GoalWindow(from, until, priority, preference));
        }

        return _manager.AddGoalArea(template, binding, resolved);
    }

    // ============================================================
    // 障碍 / 边界
    // ============================================================

    /// <summary>注册一个硬障碍。障碍不带时间窗口，Solver 路径全程不得穿越。</summary>
    public SafeZoneAreaHandle AddObstacle(GeometryTemplate template, TransformBinding binding)
        => _manager.AddObstacle(template, binding);

    /// <summary>设置副本边界。当前只接受静态 <see cref="IGeometryShape"/>。</summary>
    public void SetArenaBoundary(IGeometryShape boundary)
        => _manager.SetArenaBoundary(boundary);

    /// <summary>清除副本边界。</summary>
    public void ClearArenaBoundary()
        => _manager.ClearArenaBoundary();

    // ============================================================
    // 删除 / 清空
    // ============================================================

    /// <summary>按句柄移除任意类型的区域。返回是否实际移除。</summary>
    public bool Remove(SafeZoneAreaHandle handle)
        => _manager.RemoveArea(handle);

    /// <summary>清空所有危险区。</summary>
    public void ClearDangerAreas()
        => _manager.ClearDangerAreas();

    /// <summary>清空所有目标区。</summary>
    public void ClearGoalAreas()
        => _manager.ClearGoalAreas();

    /// <summary>清空所有障碍。</summary>
    public void ClearObstacles()
        => _manager.ClearObstacles();

    /// <summary>清空所有运行时注册内容（危险区、目标区、障碍、边界）。</summary>
    public void ClearAll()
        => _manager.ClearAllAreas();
}
