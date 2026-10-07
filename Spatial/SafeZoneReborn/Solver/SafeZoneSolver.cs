using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// SafeZone 解算器。
/// <para>管线：光栅化 → 空间搜索；无长期安全落点时用安全时间区间搜索验证转场，并仅执行下一次等待前的路段。</para>
/// <para>A* 验证原始路线的安全余量；简化/平滑后重新检查网格与危险时序，不安全时保留原始路线。</para>
/// <para>危险窗口只读 <see cref="DangerWindow.DangerFrom"/>/<see cref="DangerWindow.DangerUntil"/>，绝不读 <c>TelegraphFrom</c>。</para>
/// </summary>
public sealed class SafeZoneSolver
{
    /// <summary>
    /// 同步解算。
    /// </summary>
    /// <param name="snapshot">主线程构造的不可变快照。</param>
    /// <param name="player">主线程捕获的玩家状态。</param>
    /// <param name="capability">移动能力（速度按 BaseSpeed × EffectiveMultiplier 线性估算）。</param>
    /// <param name="options">解算选项。</param>
    /// <param name="solveTime">路径时间计算基准（绝对 UTC，通常为 DateTime.UtcNow）。</param>
    /// <param name="previousSolution">上一份解算结果（可选），用于目标内落点锁复用等稳定性判断。</param>
    public SafeZoneSolution Solve(
        SafeZoneSnapshot? snapshot,
        PlayerState player,
        MovementCapability capability,
        SolveOptions options,
        DateTime solveTime,
        SafeZoneSolution? previousSolution = null)
    {
        options ??= SolveOptionsDefaults.Default;

        if (snapshot is null)
        {
            return SafeZoneSolution.Failed(SolutionStatus.SnapshotInvalid, 0, solveTime, solveTime);
        }

        // Keep the chosen preparation budget while an emergency escape is still
        // leaving pending damage. Restoring the larger cushion on the next frame
        // can make the executor stop the very route it has just accepted.
        if (previousSolution is { IsSuccess: true, ReducedDangerStartSafetyTime: { } retainedSafety }
            && options.DangerStartSafetyTime > retainedSafety
            && HasPendingDangerAtPlayer(snapshot, player.Position, solveTime))
        {
            var continued = Solve(snapshot, player, capability,
                options with { DangerStartSafetyTime = retainedSafety, EnablePathSmoothing = false }, solveTime, previousSolution);
            if (continued.IsSuccess)
                continued.ReducedDangerStartSafetyTime = retainedSafety;
            return continued;
        }

        var version = snapshot.Version;
        var solvedAt = solveTime;

        var map = RasterizedMap.Build(snapshot, player.Position, options, solveTime, capability.CurrentSpeed);

        // 玩家所在格判定。出界且有边界 → PlayerOutsideArena；网格内无问题。
        var (startCol, startRow) = map.WorldToCell(player.Position);
        if (!map.InBounds(startCol, startRow))
        {
            if (snapshot.ArenaBoundary is not null && !snapshot.ArenaBoundary.Contains(player.Position))
            {
                return SafeZoneSolution.Failed(SolutionStatus.PlayerOutsideArena, version, solveTime, solvedAt);
            }
            // 网格构造时已外扩保证玩家在内，理论不会到这。
            return SafeZoneSolution.Failed(SolutionStatus.PlayerOutsideArena, version, solveTime, solvedAt);
        }

        var playerInDangerNow = IsPlayerInDangerNow(snapshot, player.Position, solveTime);

        if (!playerInDangerNow &&
            TryReuseSettledGoalLock(snapshot, map, player.Position, previousSolution, solveTime, out var lockedTarget))
        {
            return SafeZoneSolution.Succeeded(
                version,
                solveTime,
                solvedAt,
                targetPosition: null,
                executablePath: null,
                settledTargetPosition: lockedTarget);
        }

        // playerGeometricallySafe：玩家几何上不在任何激活危险内（真几何判定，不读光栅 margin）。
        // 为 true 时让 A* 抬升被 HardSafetyMargin 膨胀毒化的起点基线，避免擦危险边缘时卡住不动。
        var result = BossModStyleAStar.Search(
            map,
            player.Position,
            capability.CurrentSpeed,
            solveTime,
            capability.MovementStartDelay,
            options,
            playerGeometricallySafe: !playerInDangerNow,
            requireSafePath: map.HasFiniteDanger && !playerInDangerNow);

        // A race must cross periodic obstacles to reach its waypoint. A safe
        // pocket before a hammer is only a staging point, not a completed goal.
        var timedGoal = options.GoalDirectedTemporalPlanning && map.MaxGoalPriority > 0 && result.BestScore < BossModStyleAStar.Score.SafeMaxPrio;
        if ((result.BestScore < BossModStyleAStar.Score.Safe || timedGoal) && map.HasFiniteDanger &&
            SafeIntervalPathfinder.Search(map, player.Position, capability.CurrentSpeed, solveTime,
                capability.MovementStartDelay, options, timedGoal) is { } temporal)
        {
            if (temporal.BestScore == BossModStyleAStar.Score.JustBad)
            {
                // A late reaction can miss the ordinary 0.5s preparation cushion
                // while still having a valid route before the hit. Retry once,
                // retaining 0.25s, instead of stopping until the hit activates.
                const float emergencyStartSafety = 0.25f;
                if (options.DangerStartSafetyTime > emergencyStartSafety &&
                    HasPendingDangerAtPlayer(snapshot, player.Position, solveTime))
                {
                    var emergency = Solve(snapshot, player, capability,
                        options with { DangerStartSafetyTime = emergencyStartSafety, EnablePathSmoothing = false }, solveTime, previousSolution);
                    if (emergency.IsSuccess && emergency.ExecutablePath.Count >= 2)
                    {
                        emergency.ReducedDangerStartSafetyTime = emergencyStartSafety;
                        return emergency;
                    }
                }
                return SafeZoneSolution.Failed(SolutionStatus.NoTemporalPath, version, solveTime, solvedAt);
            }

            // GreenMove 不消费时间戳。只下发首次等待前的连续移动，后续路段由周期重解在解禁后接续。
            // 时间搜索的路径不做平滑或重计时，避免跨过危险窗口或切入危险几何。
            var phase = new List<TimedWaypoint>();
            if (temporal.HasPath)
            {
                foreach (var waypoint in temporal.Path)
                {
                    phase.Add(waypoint);
                    if (waypoint.IsWaitPoint)
                        break;
                }
            }
            if (options.PreferStraightPaths && phase.Count > 2)
            {
                // Keep absolute arrival/departure times and every wait. GreenMove
                // should not spend a frame advancing through each collinear cell.
                for (var i = phase.Count - 2; i > 0; --i)
                {
                    var a = phase[i].Position - phase[i - 1].Position; a.Y = 0;
                    var b = phase[i + 1].Position - phase[i].Position; b.Y = 0;
                    if (!phase[i].IsWaitPoint && a.LengthSquared() > .001f && b.LengthSquared() > .001f
                        && Vector3.Dot(Vector3.Normalize(a), Vector3.Normalize(b)) > .99999f)
                        phase.RemoveAt(i);
                }
            }
            return new SafeZoneSolution(SolutionStatus.Success, version, solveTime, solvedAt,
                phase.Count >= 2 ? phase[^1].Position : null, phase, BuildDebug(options, temporal, map))
                { UsesTemporalPlanning = true, IsPartialEscape = temporal.IsPartialEscape };
        }

        // 无路径（起点就是 best）→ 无需移动空成功。
        if (!result.HasPath)
        {
            if (playerInDangerNow || HasPendingDangerAtPlayer(snapshot, player.Position, solveTime))
                return SafeZoneSolution.Failed(SolutionStatus.NoReachableSafeCell, version, solveTime, solvedAt);

            var settledTarget = !playerInDangerNow &&
                                IsPositionInAnyActiveGoal(snapshot, player.Position, solveTime) &&
                                IsPointSafeOnMap(map, player.Position)
                ? player.Position
                : result.Destination;

            return SafeZoneSolution.Succeeded(
                version,
                solveTime,
                solvedAt,
                result.Destination,
                null,
                BuildDebug(options, result, map),
                settledTargetPosition: settledTarget);
        }

        // 逃离路径保留搜索时验证过的线段，避免平滑切回已退出的区域。
        var appliedSmoothing = false;
        var processed = playerInDangerNow
            ? result.Path
            : PostProcess(result.Path, capability.CurrentSpeed, options, out appliedSmoothing);
        var executable = processed.Count >= 2 ? processed : result.Path;
        if (!ReferenceEquals(executable, result.Path) && !IsProcessedPathSafe(map, executable, solveTime))
        {
            executable = result.Path;
            appliedSmoothing = false;
        }

        // Grid scoring can select a bent route even on an open straight bridge.
        // Prefer continuous input only when the shorter segment passes the same
        // buffered cells, boundary edges, obstacles and damage-time checks.
        if (options.PreferStraightPaths && !playerInDangerNow && executable.Count > 2 && options.Teleporters.Count == 0)
        {
            var start = executable[0];
            var end = executable[^1];
            var arrival = start.PlannedDepartureTime.AddSeconds(
                GeometryHelper.DistanceXZ(start.Position, end.Position) / MathF.Max(.1f, capability.CurrentSpeed));
            TimedWaypoint[] direct = [start, TimedWaypoint.Pass(end.Position, arrival, end.SpeedMultiplier)];
            if (IsProcessedPathSafe(map, direct, solveTime))
            {
                executable = direct;
                appliedSmoothing = false;
            }
        }

        // 目标内落点安顿：玩家已在激活目标区内 + 当前几何安全 + A* 残余移动 ≤ 安顿容差 → 视为已安顿。
        // 安顿容差 = max(ArrivalRadius, 1.5×map.Resolution)：覆盖一个对角格，避免同分候选间小幅差异触发反复移动。
        // 落点保存到 SettledTargetPosition，后续重解可通过 TryReuseSettledGoalLock 复用。
        // 玩家在危险核心里（playerInDangerNow）时不短路——再小的逃离移动也必须下发。
        var settleTolerance = MathF.Max(options.ArrivalRadius, map.Resolution * 1.5f);
        if (!playerInDangerNow &&
            IsPointSafeOnMap(map, player.Position) &&
            IsPositionInAnyActiveGoal(snapshot, player.Position, solveTime) &&
            TotalPathLengthXZ(executable) <= settleTolerance)
        {
            var settledTarget = executable[executable.Count - 1].Position;
            return SafeZoneSolution.Succeeded(
                version,
                solveTime,
                solvedAt,
                targetPosition: null,
                executablePath: null,
                settledTargetPosition: settledTarget);
        }

        // 计算 EscapeDeadline + EscapeDuration 供 MovementSystem.ShouldWaitForCast 判断读条余量。
        // 路径永远从立即出发开始，是否等读条完全由 MovementSystem 端决策。
        var (escapeDeadline, escapeDuration) = ComputeEscapeBudget(snapshot, executable, player.Position, solveTime, options);

        var target = executable[executable.Count - 1].Position;

        return new SafeZoneSolution(
            SolutionStatus.Success,
            version,
            solveTime,
            solvedAt,
            target,
            executable,
            BuildDebug(options, result, map, appliedSmoothing),
            escapeDeadline,
            escapeDuration)
        {
            MaxArrivalTolerance = playerInDangerNow ? map.Resolution * 0.45f : float.PositiveInfinity,
        };
    }

    internal static bool HasPendingDangerAtPlayer(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
    {
        foreach (var danger in snapshot.DangerAreas)
            if (danger.Geometry.Contains(position))
                foreach (var window in danger.DangerWindows)
                    // If the ordinary route already misses its preparation deadline,
                    // act now. Waiting for an arbitrary countdown can consume the
                    // only interval in which the reduced-margin route is feasible.
                    if (window.DangerFrom > now)
                        return true;
        return false;
    }

    private static bool IsProcessedPathSafe(RasterizedMap map, IReadOnlyList<TimedWaypoint> path, DateTime now)
    {
        var previousCell = -1;
        var previousTime = 0f;
        for (var i = 1; i < path.Count; ++i)
        {
            var from = path[i - 1];
            var to = path[i];
            var duration = (float)(to.PlannedArrivalTime - from.PlannedDepartureTime).TotalSeconds;
            var steps = Math.Max(1, (int)MathF.Ceiling(GeometryHelper.DistanceXZ(from.Position, to.Position) / (map.Resolution * 0.25f)));
            for (var step = 0; step <= steps; ++step)
            {
                var fraction = (float)step / steps;
                var point = Vector3.Lerp(from.Position, to.Position, fraction);
                var (col, row) = map.WorldToCell(point);
                if (!map.InBounds(col, row)) return false;
                var cell = row * map.Columns + col;
                if (map.PixelMaxGAt(col, row) < 0 || previousCell >= 0 && !map.CanTraverse(previousCell, cell)) return false;
                var time = (float)(from.PlannedDepartureTime - now).TotalSeconds + duration * fraction;
                if (!CellSafe(cell, previousCell < 0 ? time : previousTime, time)) return false;
                if (previousCell >= 0 && previousCell % map.Columns != col && previousCell / map.Columns != row)
                {
                    var sideX = previousCell / map.Columns * map.Columns + col;
                    var sideY = row * map.Columns + previousCell % map.Columns;
                    if (!map.CanTraverse(previousCell, sideX) || !map.CanTraverse(sideX, cell) ||
                        !map.CanTraverse(previousCell, sideY) || !map.CanTraverse(sideY, cell) ||
                        !CellSafe(sideX, previousTime, time) || !CellSafe(sideY, previousTime, time)) return false;
                }
                previousCell = cell;
                previousTime = time;
            }
        }
        return true;

        bool CellSafe(int cell, float from, float until)
        {
            foreach (var danger in map.DangerIntervalsAt(cell))
                if (until >= danger.From && from <= danger.Until) return false;
            return true;
        }
    }

    /// <summary>
    /// 路径后处理：简化（去重 + 合并共线）+ 可选 Catmull-Rom 平滑。调用方验证处理后的路线。
    /// </summary>
    private static IReadOnlyList<TimedWaypoint> PostProcess(
        IReadOnlyList<TimedWaypoint> rawPath,
        float speed,
        SolveOptions options,
        out bool appliedSmoothing)
    {
        appliedSmoothing = false;
        IReadOnlyList<TimedWaypoint> simplified;
        try
        {
            simplified = PathSimplifier.Simplify(rawPath, speed, options);
        }
        catch
        {
            return rawPath;
        }

        if (options.EnablePathSmoothing && !options.PreferStraightPaths && simplified.Count >= 3)
        {
            try
            {
                var smoothed = CatmullRomSmoother.Smooth(simplified, speed, options.SmoothingStrength, options.SmoothingSampleSpacing);
                appliedSmoothing = true;
                return smoothed;
            }
            catch
            {
                appliedSmoothing = false;
            }
        }

        return simplified;
    }

    /// <summary>
    /// 计算 EscapeDeadline + EscapeDuration，不平移路径。
    /// <list type="bullet">
    ///   <item><description><c>EscapeDeadline</c> = 覆盖玩家位置的未来才激活危险中最早的 <c>EffectiveDangerFrom = DangerFrom - DangerStartSafetyTime</c>。当前已激活的危险不计（应立即逃）。</description></item>
    ///   <item><description><c>EscapeDuration</c> = 沿路径走到第一个脱离所有强制危险几何路点的累计行程时间。</description></item>
    /// </list>
    /// <para>无未来才激活的强制危险时返回 (null, null)。只读 <c>DangerFrom</c>/<c>DangerUntil</c>，不读 <c>TelegraphFrom</c>。</para>
    /// </summary>
    private static (DateTime? EscapeDeadline, TimeSpan? EscapeDuration) ComputeEscapeBudget(
        SafeZoneSnapshot snapshot,
        IReadOnlyList<TimedWaypoint> path,
        Vector3 playerPosition,
        DateTime solveTime,
        SolveOptions options)
    {
        if (path.Count < 2)
        {
            return (null, null);
        }

        var startSafety = TimeSpan.FromSeconds(MathF.Max(0f, options.DangerStartSafetyTime));

        DateTime? escapeDeadline = null;
        var forcingGeometries = new List<IGeometryShape>();
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(playerPosition))
            {
                continue;
            }

            DateTime? earliestFutureFrom = null;
            foreach (var window in danger.DangerWindows)
            {
                if (window.IsActiveAt(solveTime))
                {
                    // 当前激活 → 整个危险区不算"未来威胁"，应立即逃。
                    earliestFutureFrom = null;
                    break;
                }

                var effectiveFrom = window.DangerFrom - startSafety;
                if (effectiveFrom > solveTime && (earliestFutureFrom is null || effectiveFrom < earliestFutureFrom.Value))
                {
                    earliestFutureFrom = effectiveFrom;
                }
            }

            if (earliestFutureFrom is null)
            {
                continue;
            }

            forcingGeometries.Add(danger.Geometry);
            if (escapeDeadline is null || earliestFutureFrom.Value < escapeDeadline.Value)
            {
                escapeDeadline = earliestFutureFrom.Value;
            }
        }

        if (escapeDeadline is null || forcingGeometries.Count == 0)
        {
            return (null, null);
        }

        // 沿路径走到第一个脱离所有强制危险几何路点的累计行程时间。
        var startDeparture = path[0].PlannedDepartureTime;
        for (var i = 1; i < path.Count; i++)
        {
            var pos = path[i].Position;
            var stillInside = false;
            for (var g = 0; g < forcingGeometries.Count; g++)
            {
                if (forcingGeometries[g].Contains(pos))
                {
                    stillInside = true;
                    break;
                }
            }

            if (!stillInside)
            {
                return (escapeDeadline, path[i].PlannedArrivalTime - startDeparture);
            }
        }

        // 路径全程未脱离强制危险几何（异常）：视为无可踩点，立即走。
        return (null, null);
    }

    /// <summary>路径在 X/Z 平面上的累计长度（yalm）。用于到达对齐判定。</summary>
    private static float TotalPathLengthXZ(IReadOnlyList<TimedWaypoint> path)
    {
        var total = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var dx = path[i].Position.X - path[i - 1].Position.X;
            var dz = path[i].Position.Z - path[i - 1].Position.Z;
            total += MathF.Sqrt((dx * dx) + (dz * dz));
        }
        return total;
    }

    /// <summary>
    /// 复用上一轮已经安顿的目标内落点：玩家仍在激活目标内 + 当前格安全 + 旧落点仍属于激活目标并保持安全时，
    /// 沿用旧落点，避免因目标中心/同分候选的小幅变化重新追点。
    /// </summary>
    private static bool TryReuseSettledGoalLock(
        SafeZoneSnapshot snapshot,
        RasterizedMap map,
        Vector3 playerPosition,
        SafeZoneSolution? previousSolution,
        DateTime now,
        out Vector3 lockedTarget)
    {
        lockedTarget = default;

        if (previousSolution is null ||
            !previousSolution.IsNoMovement ||
            previousSolution.SettledTargetPosition is not { } previousTarget)
        {
            return false;
        }

        if (!IsPositionInAnyActiveGoal(snapshot, playerPosition, now) ||
            !IsPositionInAnyActiveGoal(snapshot, previousTarget, now))
        {
            return false;
        }

        if (!IsPointSafeOnMap(map, playerPosition) ||
            !IsPointSafeOnMap(map, previousTarget))
        {
            return false;
        }

        lockedTarget = previousTarget;
        return true;
    }

    private static bool IsPointSafeOnMap(RasterizedMap map, Vector3 position)
    {
        var (col, row) = map.WorldToCell(position);
        return map.InBounds(col, row) && map.PixelMaxGAt(col, row) >= float.MaxValue;
    }

    /// <summary>玩家几何上是否在任一硬障碍或激活危险区内（不含光栅 margin 膨胀）。</summary>
    private static bool IsPlayerInDangerNow(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
    {
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(position))
                return true;
        }
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(position))
            {
                continue;
            }
            for (var i = 0; i < danger.DangerWindows.Count; i++)
            {
                if (danger.DangerWindows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 指定位置几何上是否在任一激活目标区内。只读几何 <see cref="IGeometryShape.Contains"/> + <see cref="GoalWindow.IsActiveAt"/>，不读光栅 margin。
    /// </summary>
    private static bool IsPositionInAnyActiveGoal(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
    {
        foreach (var goal in snapshot.GoalAreas)
        {
            if (!goal.Geometry.Contains(position))
            {
                continue;
            }
            for (var i = 0; i < goal.GoalWindows.Count; i++)
            {
                if (goal.GoalWindows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static SolveDebugData? BuildDebug(SolveOptions options, BossModStyleAStar.Result result, RasterizedMap map, bool appliedSmoothing = false)
    {
        if (!options.GenerateDebugData)
        {
            return null;
        }

        return new SolveDebugData(
            bestScore: result.BestScore,
            pathLeeway: result.PathLeeway,
            isNoMovementSuccess: !result.HasPath,
            rawPath: result.Path,
            simplifiedPath: result.Path,
            appliedSmoothing: appliedSmoothing,
            searchBudgetExceeded: result.BudgetExceeded,
            searchExpansions: result.Expansions,
            gridColumns: map.Columns,
            gridRows: map.Rows,
            gridResolution: map.Resolution);
    }
}
