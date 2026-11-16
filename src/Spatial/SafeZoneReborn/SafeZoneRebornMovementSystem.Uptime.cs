using System;
using System.Linq;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

namespace MasterPlayer.Spatial.SafeZoneReborn;

internal sealed partial class SafeZoneRebornMovementSystem
{
    // In addition to DangerStartSafetyTime, leave room for the next frame and movement startup.
    // 斗兽实测 0.15s 太紧：几只小怪的直线（蛛网屏）叠在一起、加上网络与动画延迟时会走晚被扫到，留到 0.6s。
    internal static double DepartureReserveSeconds = 0.6; // 离线模拟扫参数时可改

    // kano 11-08: "躲技能没卡极限". Every early exit below makes MP leave at once; publish which
    // one fired (PromeRadar.GetState EscapeWaitBlock) so captures show why a dodge did not wait.
    internal string EscapeWaitBlock { get; private set; } = string.Empty;
    internal DateTime? EscapeWaitBlockAtUtc { get; private set; }

    private bool Block(string reason, DateTime now)
    {
        EscapeWaitBlock = reason;
        EscapeWaitBlockAtUtc = now;
        return false;
    }

    private void ClearEscapeWaitBlock()
    {
        EscapeWaitBlock = string.Empty;
        EscapeWaitBlockAtUtc = null;
    }

    private bool ShouldWaitForEscape(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        ClearEscapeWaitBlock();
        if (_setting is null || _setting.AttackUptimeExpiresAt <= now) return Block("未开启躲避卡极限", now);
        if (_hasControl) return false;
        if (_setting.RuntimeNoEscapeDelay) return Block("机制要求立即撤离", now);
        var wait = CanDelayEscape(solution, player, snapshot, now, DepartureReserveSeconds);
        if (wait) ClearEscapeWaitBlock();
        return wait;
    }

    private bool CanDelayEscape(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, double delay)
    {
        if (!solution.IsSuccess || solution.ExecutablePath.Count < 2) return Block("无可执行路线", now);
        // run2182/2186: "时序规划路线" blocked 12-13 of ~74 dispatches (曼提克/牛头魔). A temporal plan
        // without intermediate waits can be delayed like any other route: IsTemporalPhaseSafe below walks
        // it from the delayed start, checks every leg against all known windows and refuses a delay that
        // would reach the final wait point after its PlannedDepartureTime.
        if (solution.UsesTemporalPlanning && solution.ExecutablePath.SkipLast(1).Any(p => p.IsWaitPoint))
            return Block("时序规划路线（中途有等待点）", now);
        if (solution.ReducedDangerStartSafetyTime.HasValue) return Block("已压缩安全余量", now);
        if (player.CurrentSpeed <= 0.1f || !double.IsFinite(delay) || delay <= 0) return Block("速度/延迟无效", now);
        if (IsPointUnsafeNow(snapshot, player.Position, now)) return Block("当前位置已危险", now);

        // Negative-priority goals are optional attack preferences. Mechanic destinations
        // and opaque constraints may encode knockbacks, binds, or other preparation deadlines.
        foreach (var goal in snapshot.GoalAreas)
            foreach (var window in goal.GoalWindows)
                if (window.Priority >= 0 && window.IsActiveAt(now)) return Block("有机制站位目标", now);

        // ldt1 replay (run2181 b3, A = multi-wave only, -2.5 melee): with staggered waves allowed, MP also
        // "waited" at the dark-ray bait corner (17y out) and 6.4y outside melee, delaying the walk back.
        // Holding an escape only pays while the player can attack: require being inside an active
        // optional attack goal (FollowController's melee ring, priority < 0) when one exists.
        var attackGoal = false;
        var inAttackGoal = false;
        foreach (var goal in snapshot.GoalAreas)
            foreach (var window in goal.GoalWindows)
                if (window.Priority < 0 && window.IsActiveAt(now))
                {
                    attackGoal = true;
                    if (goal.Geometry.Contains(player.Position)) inAttackGoal = true;
                }
        if (attackGoal && !inAttackGoal) return Block("不在输出位置", now);

        var mustEscape = false;
        DateTime? firstActivation = null;
        var staggered = false;
        var lastEnd = now;
        foreach (var danger in snapshot.DangerAreas)
        {
            foreach (var window in danger.DangerWindows)
            {
                if (window.DangerFrom == DateTime.MaxValue ||
                    window.DangerUntil is { } end && (end - now).TotalSeconds < -_options.DangerEndSafetyTime)
                    continue;
                if (danger.Geometry is not (CircleGeometry or DonutGeometry or FanGeometry or RectangleGeometry))
                    return Block($"非基础形状 {danger.Geometry.GetType().Name}", now);
                if (window.DangerFrom > now)
                {
                    // kano 11-08 / run2182+2186 EscapeWaitBlock: staggered waves used to abort the wait
                    // outright (6 of ~74 dispatches). The delayed route below is already checked against
                    // every known window; additionally hold the endpoint through the last known wave.
                    // A newly revealed wave is re-evaluated next frame and can only make MP leave earlier.
                    if (firstActivation is { } first && Math.Abs((window.DangerFrom - first).TotalSeconds) > 0.1)
                        staggered = true;
                    firstActivation ??= window.DangerFrom;
                    var until = window.DangerUntil ?? window.DangerFrom;
                    if (until > lastEnd) lastEnd = until;
                }
                if (window.DangerFrom > now && danger.Geometry.Contains(player.Position))
                    mustEscape = true;
            }
        }
        if (!mustEscape) return Block("人不在将判定的危险内", now);

        var departure = now.AddSeconds(delay);
        // Recheck the actual remaining polyline at the proposed departure time. This
        // includes intermediate hazards and their end margins, not just the final point
        // or the first circle's exit. No path search runs on the framework thread.
        if (!IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, departure))
            return Block("原地再等会被打", now);
        if (!IsTemporalPhaseSafe(solution, player, snapshot, departure))
            return Block("晚走路线来不及", now);
        if (staggered && !solution.ExecutablePath[^1].IsWaitPoint)
        {
            var endpoint = solution.ExecutablePath[^1].Position;
            if (!IsTemporalSegmentSafe(snapshot, endpoint, endpoint, departure, lastEnd.AddSeconds(_options.DangerEndSafetyTime)))
                return Block("多波：终点后续波不安全", now);
        }
        return true;
    }
}
