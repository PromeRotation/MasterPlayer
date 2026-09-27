using MasterPlayer.Spatial.SafeZone;
using System.Collections.Generic;

namespace MasterPlayer.Spatial.SafeZoneReborn.Solver;

/// <summary>
/// 解算选项。承载光栅化、A* 搜索、平滑、验证所需的全部可调参数。
/// <para>默认值集中在 <see cref="SolveOptionsDefaults"/>；单次机制可创建一份新的 <see cref="SolveOptions"/> 覆盖其中若干项。</para>
/// </summary>
public sealed record SolveOptions
{
    // ───────────────────────── 到达 / 兜底候选 ─────────────────────────

    /// <summary>到达容差半径（yalm）。对接 <c>GreenMovePoint.Tolerance</c>。</summary>
    public float ArrivalRadius { get; init; } = 0.5f;

    /// <summary>放射状候选目标的角度步进（度）。例如 45 表示 8 个方向。</summary>
    public float CandidateAngleStepDegrees { get; init; } = 45f;

    /// <summary>放射状候选目标的距离档位（yalm），按从近到远依次尝试。</summary>
    public IReadOnlyList<float> CandidateDistances { get; init; } = SolveOptionsDefaults.DefaultCandidateDistances;

    // ───────────────────────── 时间安全余量 ─────────────────────────

    /// <summary>
    /// 危险开始安全余量（秒）。将每个危险的开始时间向前提前该值参与解算（<c>EffectiveDangerFrom = DangerFrom - DangerStartSafetyTime</c>）。
    /// <para>同一值也用于 <see cref="SafeZoneRebornMovementSystem"/> 的读条等待/打断与踩点逃起步时刻判断。</para>
    /// </summary>
    public float DangerStartSafetyTime { get; init; } = 0.5f;

    /// <summary>
    /// 危险结束安全余量（秒）。计划在危险结束后再进入某区域时，将结束时间向后延长该值（<c>EffectiveDangerUntil = DangerUntil + DangerEndSafetyTime</c>）。
    /// </summary>
    public float DangerEndSafetyTime { get; init; } = 0.3f;

    // ───────────────────────── 光栅化 ─────────────────────────

    /// <summary>空间网格分辨率（yalm/格）。一次解算中途不可改变。</summary>
    public float GridResolution { get; init; } = 0.5f;

    /// <summary>
    /// 无 <c>ArenaBoundary</c> 时，以玩家为中心生成方盒网格的半边长（yalm）。默认 25 → 50×50 盒。
    /// 盒外的危险/目标会被忽略；有边界时改用 <c>boundary.GetBounds()</c>，该值不生效。
    /// </summary>
    public float FallbackBoxRadius { get; init; } = 25f;

    /// <summary>强制安全余量（yalm）。危险区按该距离向外扩张，副本边界/障碍按该距离收缩；任何路径都不得违反。</summary>
    public float HardSafetyMargin { get; init; } = 0.5f;

    /// <summary>舒适安全余量（yalm）。不禁止通行，仅在靠近危险边缘时增加路径代价、降低目标评分。</summary>
    public float ComfortSafetyMargin { get; init; } = 2.0f;

    // ───────────────────────── 时间感知搜索 ─────────────────────────

    /// <summary>时间离散步长（秒）。决定原地等待动作的粒度，也作为 (格子, 时间) 状态的去重时间桶大小。</summary>
    public float TimeResolution { get; init; } = 0.1f;

    /// <summary>规划时间范围（秒）。本次解算最多规划到未来多长时间。</summary>
    public float PlanningHorizon { get; init; } = 10f;

    /// <summary>单条路径允许原地等待的累计时间上限（秒）。超过视为不可接受。</summary>
    public float MaximumWaitTime { get; init; } = 8f;

    // ───────────────────────── 速度需求 ─────────────────────────

    /// <summary>请求移速时在 RequiredSpeed 之上预留的余量（yalm/秒），避免刚好卡在理论最低值。</summary>
    public float SpeedReserve { get; init; } = 0.5f;

    // ───────────────────────── 路径平滑 ─────────────────────────

    /// <summary>是否启用 Catmull-Rom 平滑。</summary>
    public bool EnablePathSmoothing { get; init; } = true;

    /// <summary>平滑强度 <c>[0,1]</c>。1 最弯，0 退化为直线。</summary>
    public float SmoothingStrength { get; init; } = 0.5f;

    /// <summary>平滑验证失败时每次降低强度的乘性比例。</summary>
    public float SmoothingStrengthFalloff { get; init; } = 0.5f;

    /// <summary>平滑曲线按固定距离重采样的间距（yalm）。</summary>
    public float SmoothingSampleSpacing { get; init; } = 0.75f;

    // ───────────────────────── 失败降级 ─────────────────────────

    /// <summary>找不到严格安全路径时，是否允许返回风险最小的 BestEffort 路径（仅在有显式目标区域时有意义）。</summary>
    public bool AllowBestEffort { get; init; } = false;

    // ───────────────────────── 读条滑步 ─────────────────────────

    /// <summary>
    /// 读条尾段可滑步窗口（秒）。用于判断是否值得为读条多等：该窗口内的移动视为不必真正打断读条。
    /// </summary>
    public float CastSlideWindow { get; init; } = 0.2f;

    /// <summary>是否生成 <c>SolveDebugData</c>（原始/简化路径等）。默认 false 以避免运行时分配，Debug 面板手动解算时开启。</summary>
    public bool GenerateDebugData { get; init; } = false;

    // ───────────────────────── 传送边 ─────────────────────────

    /// <summary>解算传送边（机制模块 AIHints.Teleporters 经 SafeZoneRuntime.SetTeleporters 发布）。仅主线程读写。</summary>
    public IReadOnlyList<SolverTeleporter> Teleporters { get; set; } = [];
}
