using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// <c>SafeZoneSolver</c> 的输出。仅保存 MovementSystem 消费所需的最小信息。
/// <para><see cref="Status"/> 合并成功/失败，避免 <c>Success + FailureReason</c> 双字段产生矛盾态。</para>
/// </summary>
public sealed class SafeZoneSolution
{
    private static readonly IReadOnlyList<TimedWaypoint> EmptyPath = Array.Empty<TimedWaypoint>();

    public SafeZoneSolution(
        SolutionStatus status,
        int sourceSnapshotVersion,
        DateTime solveTime,
        DateTime solvedAt,
        Vector3? targetPosition,
        IReadOnlyList<TimedWaypoint> executablePath,
        SolveDebugData? debugData = null,
        DateTime? escapeDeadline = null,
        TimeSpan? escapeDuration = null,
        Vector3? settledTargetPosition = null)
    {
        Status = status;
        SourceSnapshotVersion = sourceSnapshotVersion;
        SolveTime = solveTime;
        SolvedAt = solvedAt;
        TargetPosition = targetPosition;
        ExecutablePath = executablePath;
        DebugData = debugData;
        EscapeDeadline = escapeDeadline;
        EscapeDuration = escapeDuration;
        SettledTargetPosition = settledTargetPosition;
    }

    /// <summary>成功或明确失败原因。</summary>
    public SolutionStatus Status { get; }

    /// <summary>结果对应的快照版本。</summary>
    public int SourceSnapshotVersion { get; }

    /// <summary>路径时间计算基准（UTC）。</summary>
    public DateTime SolveTime { get; }

    /// <summary>完成解算的时间（UTC）。</summary>
    public DateTime SolvedAt { get; }

    /// <summary>最终目标点；失败或无需移动时为空。</summary>
    public Vector3? TargetPosition { get; }

    /// <summary>
    /// 目标区内已经安顿并锁定的落点，供下次解算复用以避免同分候选间反复追点。
    /// 不代表需要继续移动，也不参与 GreenMove 下发。
    /// </summary>
    public Vector3? SettledTargetPosition { get; }

    /// <summary>经验证、可交给 MovementSystem 的路径；失败或无需移动时为空。</summary>
    public IReadOnlyList<TimedWaypoint> ExecutablePath { get; }

    /// <summary>该路径是已验证时间路线的当前阶段，必须到等待点停下并重解，不能按普通目标路线延用。</summary>
    internal bool UsesTemporalPlanning { get; init; }
    internal bool IsPartialEscape { get; init; }

    // A failed ordinary solve or dispatch may spend part of its early-start cushion.
    // The actual damage window and all spatial/end margins remain unchanged.
    internal float? ReducedDangerStartSafetyTime { get; set; }

    internal SafeZoneSolution WithReducedDangerStartSafetyTime(float safety)
    {
        var copy = (SafeZoneSolution)MemberwiseClone();
        copy.ReducedDangerStartSafetyTime = safety;
        return copy;
    }

    internal SafeZoneSolution WithExecutablePath(IReadOnlyList<TimedWaypoint> path)
        => new(Status, SourceSnapshotVersion, SolveTime, SolvedAt, TargetPosition, path, DebugData,
            EscapeDeadline, EscapeDuration, SettledTargetPosition)
        {
            UsesTemporalPlanning = UsesTemporalPlanning, IsPartialEscape = IsPartialEscape,
            ReducedDangerStartSafetyTime = ReducedDangerStartSafetyTime, MaxArrivalTolerance = MaxArrivalTolerance,
        };

    /// <summary>逃离时的到达容差上限，保证停止位置仍位于搜索验证过的终点格内。</summary>
    internal float MaxArrivalTolerance { get; init; } = float.PositiveInfinity;

    /// <summary>可选调试数据。仅 <c>SolveOptions.GenerateDebugData=true</c> 时填充。</summary>
    public SolveDebugData? DebugData { get; }

    /// <summary>
    /// 踩点逃截止时刻：玩家当前位置的最早未来危险有效激活时刻（<c>DangerFrom - DangerStartSafetyTime</c>）。
    /// <para>null 表示当前位置无未来要激活的强制危险。</para>
    /// <para>MovementSystem 用此判断读条余量：<c>castNeedsStill + EscapeDuration &lt;= EscapeDeadline - now</c> 时才继续读条。</para>
    /// </summary>
    public DateTime? EscapeDeadline { get; }

    /// <summary>
    /// 沿当前路径走出所有强制危险几何所需的累计时长。
    /// <para>null 表示无未来要激活的强制危险（与 <see cref="EscapeDeadline"/> 同步）。</para>
    /// </summary>
    public TimeSpan? EscapeDuration { get; }

    /// <summary>是否解算成功。</summary>
    public bool IsSuccess => Status == SolutionStatus.Success;

    /// <summary>是否为"当前已安全、无需移动"的空成功。</summary>
    public bool IsNoMovement => IsSuccess && ExecutablePath.Count == 0;

    /// <summary>
    /// 一句话描述本次解算结果：失败 / 无需移动 / 移动路径长度，附带 A* Score 档位。
    /// </summary>
    public string DescribeOutcome()
    {
        if (!IsSuccess)
        {
            return $"失败({Status})";
        }

        var scoreTag = DebugData is { } dbg ? $"·{dbg.BestScore}" : string.Empty;

        if (IsNoMovement)
        {
            var settledTag = SettledTargetPosition is not null ? "·已安顿" : string.Empty;
            return $"无需移动{settledTag}·空成功[A*{scoreTag}]";
        }

        return $"{(ReducedDangerStartSafetyTime.HasValue ? "紧急避险" : "移动成功")}[A*{scoreTag}]·路点{ExecutablePath.Count}";
    }

    /// <summary>构造一个成功结果。<paramref name="executablePath"/> 为空表示当前已安全、无需移动。</summary>
    public static SafeZoneSolution Succeeded(
        int sourceSnapshotVersion,
        DateTime solveTime,
        DateTime solvedAt,
        Vector3? targetPosition,
        IReadOnlyList<TimedWaypoint>? executablePath,
        SolveDebugData? debugData = null,
        DateTime? escapeDeadline = null,
        TimeSpan? escapeDuration = null,
        Vector3? settledTargetPosition = null)
        => new(
            SolutionStatus.Success,
            sourceSnapshotVersion,
            solveTime,
            solvedAt,
            targetPosition,
            executablePath ?? EmptyPath,
            debugData,
            escapeDeadline,
            escapeDuration,
            settledTargetPosition);

    /// <summary>构造一个失败结果（无目标、空路径）。</summary>
    public static SafeZoneSolution Failed(
        SolutionStatus status,
        int sourceSnapshotVersion,
        DateTime solveTime,
        DateTime solvedAt)
        => new(status, sourceSnapshotVersion, solveTime, solvedAt, null, EmptyPath);
}

/// <summary>
/// 可选解算调试数据。仅在 <c>SolveOptions.GenerateDebugData=true</c> 时生成。
/// </summary>
public sealed class SolveDebugData
{
    public SolveDebugData(
        BossModStyleAStar.Score bestScore,
        float pathLeeway,
        bool isNoMovementSuccess,
        IReadOnlyList<TimedWaypoint> rawPath,
        IReadOnlyList<TimedWaypoint> simplifiedPath,
        bool appliedSmoothing,
        bool searchBudgetExceeded,
        int searchExpansions,
        int gridColumns,
        int gridRows,
        float gridResolution)
    {
        BestScore = bestScore;
        PathLeeway = pathLeeway;
        IsNoMovementSuccess = isNoMovementSuccess;
        RawPath = rawPath;
        SimplifiedPath = simplifiedPath;
        AppliedSmoothing = appliedSmoothing;
        SearchBudgetExceeded = searchBudgetExceeded;
        SearchExpansions = searchExpansions;
        GridColumns = gridColumns;
        GridRows = gridRows;
        GridResolution = gridResolution;
    }

    /// <summary>本次搜索得到的 best 节点 Score 档位（9 档评级）。</summary>
    public BossModStyleAStar.Score BestScore { get; }

    /// <summary>best 节点的 PathLeeway（路径累计最小余量）。正=全程安全有余量；0=临界；负=路径中有"赶不上"段。</summary>
    public float PathLeeway { get; }

    /// <summary>是否为"当前已安全、无需移动"的空成功。</summary>
    public bool IsNoMovementSuccess { get; }

    /// <summary>A* 输出的原始路径（简化前）。</summary>
    public IReadOnlyList<TimedWaypoint> RawPath { get; }

    /// <summary>简化后的路径（去重 + 合并共线）。</summary>
    public IReadOnlyList<TimedWaypoint> SimplifiedPath { get; }

    /// <summary>最终路径是否应用了 Catmull-Rom 平滑。</summary>
    public bool AppliedSmoothing { get; }

    /// <summary>搜索是否因展开上限提前终止。</summary>
    public bool SearchBudgetExceeded { get; }

    /// <summary>搜索展开的节点数。</summary>
    public int SearchExpansions { get; }

    public int GridColumns { get; }
    public int GridRows { get; }
    public float GridResolution { get; }
}
