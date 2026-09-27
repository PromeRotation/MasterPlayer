using MasterPlayer.Spatial.SafeZone;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using ECommons.DalamudServices;

namespace MasterPlayer.Spatial.SafeZoneReborn;

/// <summary>
/// Reborn 安全区移动系统：把 <see cref="SafeZoneSolution.ExecutablePath"/> 转换为 <see cref="GreenMoveSystem"/> 的移动请求。
/// <para>
/// 由 MasterPlayer 主循环在脚本和跟随更新之后调用。GreenMove 使用宿主公开接口；
/// 新路线立即下发，后续逐帧推进由宿主负责。
/// </para>
/// <para>职责：判断 Solution 是否仍有效 → 失效则后台解算 → 主线程接收/丢弃结果 → 按设置缓存或下发 → 读条等待/打断决策。</para>
/// <para>读条等待不依赖 <see cref="GreenMovePoint.MaxCastWaitMs"/> 静态参数，而是结合当前 Solution、最新危险区与读条剩余时间动态判断。</para>
/// </summary>
internal sealed partial class SafeZoneRebornMovementSystem : ISafeZoneRebornMovementStatus, IDisposable
{
    /// <summary>Solution 周期重解 TTL（秒）。版本号不变时用于跟上跟随危险与限时危险的时间轴推进。</summary>
    private const double ResolveTtlSeconds = 0.1;

    /// <summary>紧急场景下的最短重解 TTL：不等普通 TTL，但避免危险中每帧刷后台。</summary>
    private const double UrgentResolveTtlSeconds = 0.05;

    /// <summary>新 Solution 的最小生存时间，避免刚接收就因当前危险连续自触发。</summary>
    private const double MinUrgentSolutionLifetimeSeconds = 0.02;

    /// <summary>路径最终计划到达时间超过该值且玩家仍未到达时，视为计划明显过期。</summary>
    private const double PlannedPathOverdueSeconds = 0.12;

    /// <summary>计划过期时，玩家距离最终目标超过该值才触发紧急重解，避免到点容差内反复刷。</summary>
    private const float PlannedPathMissDistance = 0.6f;

    /// <summary>刚从当前危险逃出后，候选路线若直接回目标区，需要先稳定这一小段时间，避免追逐危险造成逃逸/回目标循环。</summary>
    private const double GoalReentryStableSeconds = 0.25;

    /// <summary>下发 <see cref="GreenMovePoint"/> 的最小到达容差（yalm），GreenMove 校验要求 Tolerance &gt; 0。</summary>
    private const float MinArrivalTolerance = 0.1f;

    private readonly SafeZoneSetting _setting;
    private readonly SafeZoneManager _manager;
    private readonly Func<bool> _externalMovementEnabled;
    private readonly Func<bool> _externalHasControl;
    private readonly SafeZoneSolver _solver = new();
    private readonly SolveOptions _options = CreateMovementOptions();

    /// <summary>注入解算传送边（机制导航桥发布，仅主线程）。</summary>
    public void SetTeleporters(System.Collections.Generic.IReadOnlyList<Solver.SolverTeleporter> teleporters)
        => _options.Teleporters = teleporters;

    private IGreenMoveSystem? _greenMove;
    private SafeZoneSolution? _latestSolution;
    private Task<AsyncSolveResult>? _pendingSolveTask;
    private CancellationTokenSource? _pendingSolveCts;
    private int _pendingSolveSnapshotVersion;
    private int _asyncSolveStartedCount;
    private int _asyncSolveAcceptedCount;
    private int _asyncSolveCancelledCount;
    private int _asyncSolveDiscardedCount;

    /// <summary>已经下发给 GreenMove 的那份 Solution。引用未变则不重复 MoveTo，避免每帧 reset GreenMove 运行时状态。</summary>
    private SafeZoneSolution? _dispatchedSolution;

    /// <summary>本系统是否下发过移动；释放时还须检查宿主安全区是否已经接管。</summary>
    private bool _hasControl;
    private bool _disposed;

    /// <summary>累计向 GreenMove 下发 MoveTo 的次数（每次实质重发 +1）。用于诊断是否存在盲目重发。</summary>
    private int _dispatchCount;

    /// <summary>上次实质下发 MoveTo 的 UTC 时间（用于算重发间隔）。</summary>
    private DateTime _lastDispatchUtc;

    /// <summary>玩家上次处于当前激活危险/障碍/边界外的 UTC 时间。用于短暂抑制"刚逃出又立刻回目标区"。</summary>
    private DateTime _lastUnsafeUtc = DateTime.MinValue;

    /// <summary>本帧调度后的语义阶段，供 Debug 状态行显示。</summary>
    private DispatchPhase _phase = DispatchPhase.Idle;

    /// <summary>调度阶段：当前帧 MovementSystem 对 GreenMove 做了什么。仅供 Debug 观测。</summary>
    internal enum DispatchPhase
    {
        /// <summary>功能关闭 / 无玩家 / 未开自动移动：未接管。</summary>
        Idle,

        /// <summary>正在下发路径并接管移动。</summary>
        Moving,

        /// <summary>已到临时落脚点，保持原地等待危险结束后重解接续。</summary>
        HoldingForDeparture,

        /// <summary>余量充足，等读条读完再走。</summary>
        WaitingForCast,

        WaitingForEscape,

        /// <summary>解算失败 / 无需移动：已松手。</summary>
        Released,

        YieldingToHost,
    }

    /// <summary>累计 MoveTo 下发次数。</summary>
    public int DispatchCount => _dispatchCount;

    /// <summary>上次实质下发 MoveTo 的 UTC 时间。</summary>
    public DateTime LastDispatchUtc => _lastDispatchUtc;

    /// <summary>当前调度阶段（供 Debug 状态行显示）。</summary>
    internal DispatchPhase Phase => _phase;

    /// <summary>当前调度阶段的中文短描述（供 Debug 状态行显示）。</summary>
    public string PhaseText => _phase switch
    {
        DispatchPhase.Idle => "空闲",
        DispatchPhase.Moving => "下发中",
        DispatchPhase.HoldingForDeparture => "等待危险结束",
        DispatchPhase.WaitingForCast => "等读条",
        DispatchPhase.WaitingForEscape => "等待撤离时机",
        DispatchPhase.Released => "已松手",
        DispatchPhase.YieldingToHost => "让位宿主自动移动",
        _ => "?",
    };

    /// <summary>后台解算是否仍在运行。仅供 Debug 观测。</summary>
    public bool IsAsyncSolveRunning => _pendingSolveTask is { IsCompleted: false };

    /// <summary>后台解算启动次数。仅供 Debug 观测。</summary>
    public int AsyncSolveStartedCount => _asyncSolveStartedCount;

    /// <summary>后台解算被主线程接收次数。仅供 Debug 观测。</summary>
    public int AsyncSolveAcceptedCount => _asyncSolveAcceptedCount;

    /// <summary>后台解算取消标记次数。仅供 Debug 观测。</summary>
    public int AsyncSolveCancelledCount => _asyncSolveCancelledCount;

    /// <summary>后台解算完成后因过期/取消/异常被丢弃次数。仅供 Debug 观测。</summary>
    public int AsyncSolveDiscardedCount => _asyncSolveDiscardedCount;

    public SafeZoneRebornMovementSystem(
        SafeZoneSetting setting,
        SafeZoneManager manager,
        Func<bool> externalMovementEnabled,
        Func<bool> externalHasControl)
    {
        _setting = setting ?? throw new ArgumentNullException(nameof(setting));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _externalMovementEnabled = externalMovementEnabled ?? throw new ArgumentNullException(nameof(externalMovementEnabled));
        _externalHasControl = externalHasControl ?? throw new ArgumentNullException(nameof(externalHasControl));
    }

    /// <summary>注入宿主的移动执行接口。</summary>
    public void Attach(IGreenMoveSystem greenMove)
    {
        _greenMove = greenMove ?? throw new ArgumentNullException(nameof(greenMove));
    }

    /// <summary>最近一次解算结果（含 EnableAutoMovement=false 时的只缓存场景），供 Debug 可视化读取。</summary>
    public SafeZoneSolution? LatestSolution => _latestSolution;

    /// <summary>本系统当前是否正在接管 GreenMove。</summary>
    public bool HasControl => _hasControl;

    public void Reset()
    {
        CancelPendingSolve();
        ReleaseControl();
        _latestSolution = null;
        _dispatchedSolution = null;
        _lastUnsafeUtc = DateTime.MinValue;
        _phase = DispatchPhase.Idle;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPendingSolve();
        DisposeCompletedPendingSolve();
        _pendingSolveCts?.Dispose();
        _pendingSolveCts = null;
        _pendingSolveTask = null;

        // 卸载前若仍在接管，停下 GreenMove，避免残留路线在 GreenMove 释放前继续跑。
        ReleaseControl();
        _greenMove = null;
        _latestSolution = null;
        _dispatchedSolution = null;
    }

    public void Update(PlayerState? currentPlayer, MovementCapability capability, DateTime now, bool allowMovement = true)
    {
        if (_disposed || _greenMove is null)
        {
            return;
        }

        // 功能关闭或玩家不存在：松手（仅当曾接管才 Stop），不解算、不下发。
        if (!_setting.EnableSafeZone || currentPlayer is null)
        {
            Reset();
            return;
        }

        // 保留危险结束余量内的窗口，否则回收会让解算器提前认为刚判定的区域已经可进入。
        _manager.PruneExpiredAreas(now - TimeSpan.FromSeconds(MathF.Max(0f, _options.DangerEndSafetyTime)));

        var player = currentPlayer.Value;

        // 每帧解析一次快照：既供重解，也供 ShouldWaitForCast 读取最新危险区。
        var snapshot = _manager.BuildSnapshot(now);
        CompletePendingSolve(snapshot);
        if (IsPointUnsafeNow(snapshot, player.Position, now))
        {
            _lastUnsafeUtc = now;
        }

        if (!IsSolutionValid(_latestSolution, snapshot, player, now))
        {
            if (CanSkipSolve(snapshot, player.Position, now))
            {
                CancelPendingSolve();
                _latestSolution = SafeZoneSolution.Succeeded(
                    snapshot.Version,
                    now,
                    now,
                    targetPosition: null,
                    executablePath: null,
                    debugData: null);
            }
            else
            {
                // 传入上一份解算结果作为稳定性参考，即便已过期也用于偏好接近旧目标。
                StartPendingSolve(snapshot, player, capability, now, _latestSolution);
            }
        }

        var solution = IsSolutionUsableForDispatch(_latestSolution, snapshot) ? _latestSolution : null;

        // 未开启自动移动：只缓存 Solution 供可视化，不下发；若之前接管过则松手。
        if (!_setting.EnableAutoMovement || !allowMovement)
        {
            _phase = DispatchPhase.Idle;
            ReleaseControl();
            return;
        }

        Dispatch(solution, player, snapshot, now);
    }

    /// <summary>判断缓存的 Solution 是否仍然可直接复用。</summary>
    private bool IsSolutionValid(SafeZoneSolution? solution, SafeZoneSnapshot snapshot, PlayerState player, DateTime now)
    {
        if (solution is null || !solution.IsSuccess)
        {
            return false;
        }

        // 区域集合变化（注册/移除/清空/边界）→ 版本号自增 → 立即重解。
        if (solution.SourceSnapshotVersion != _manager.Version)
        {
            return false;
        }

        // Keep a validated temporary refuge until the next event opens a route.
        // Repeatedly extending its safe interval can chase successive waves across
        // the whole arena and leave too much distance for the eventual return.
        if (solution.IsPartialEscape && IsTemporalPhaseSafe(solution, player, snapshot, now))
            return true;

        var ageSeconds = (now - solution.SolvedAt).TotalSeconds;

        // 周期重解：覆盖版本号不变时的限时/延迟危险窗口推进与跟随实体的危险区移动。
        if (ageSeconds > ResolveTtlSeconds)
        {
            return false;
        }

        // 新解刚接收的头几个毫秒不做 urgent 检查，避免"危险中接收 → 立刻再开一解"。
        if (ageSeconds <= MinUrgentSolutionLifetimeSeconds)
        {
            return true;
        }

        if (ShouldUrgentlyResolve(solution, snapshot, player, now))
        {
            return ageSeconds <= UrgentResolveTtlSeconds;
        }

        return true;
    }

    private void StartPendingSolve(
        SafeZoneSnapshot snapshot,
        PlayerState player,
        MovementCapability capability,
        DateTime solveTime,
        SafeZoneSolution? previousSolution)
    {
        if (_pendingSolveTask is { IsCompleted: false })
        {
            // Moving hazards change the registry every frame. Let this bounded solve
            // finish, then validate its route against the newest snapshot before use.
            return;
        }

        DisposeCompletedPendingSolve();

        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _pendingSolveCts = cts;
        _pendingSolveSnapshotVersion = snapshot.Version;
        _asyncSolveStartedCount++;

        _pendingSolveTask = Task.Run(() =>
        {
            try
            {
                if (token.IsCancellationRequested)
                {
                    return AsyncSolveResult.Cancelled(snapshot.Version);
                }

                var solution = _solver.Solve(snapshot, player, capability, _options, solveTime, previousSolution);
                return AsyncSolveResult.Completed(snapshot.Version, solution, token.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                return AsyncSolveResult.Failed(snapshot.Version, ex);
            }
        }, token);
    }

    private void CompletePendingSolve(SafeZoneSnapshot currentSnapshot)
    {
        var task = _pendingSolveTask;
        if (task is null || !task.IsCompleted)
        {
            return;
        }

        AsyncSolveResult? result = null;
        if (task.IsCanceled)
        {
            _asyncSolveDiscardedCount++;
        }
        else if (task.IsFaulted)
        {
            _asyncSolveDiscardedCount++;
            Svc.Log.Warning(task.Exception!, "[MasterPlayer SafeZone] 后台解算任务异常。");
        }
        else
        {
            result = task.Result;
        }

        var wasCancelled = _pendingSolveCts?.IsCancellationRequested == true;
        DisposeCompletedPendingSolve();

        if (result is null)
        {
            return;
        }

        if (result.Exception is not null)
        {
            _asyncSolveDiscardedCount++;
            Svc.Log.Warning(result.Exception, "[MasterPlayer SafeZone] 后台解算失败。");
            return;
        }

        if (_disposed ||
            !_setting.EnableSafeZone ||
            wasCancelled || result.WasCancelled ||
            result.Solution is null ||
            !IsSolutionUsableForDispatch(result.Solution, currentSnapshot))
        {
            _asyncSolveDiscardedCount++;
            return;
        }

        _latestSolution = result.Solution;
        _asyncSolveAcceptedCount++;
    }

    private void CancelPendingSolve()
    {
        if (_pendingSolveCts is null || _pendingSolveCts.IsCancellationRequested)
        {
            return;
        }

        _pendingSolveCts.Cancel();
        _asyncSolveCancelledCount++;
    }

    private void DisposeCompletedPendingSolve()
    {
        if (_pendingSolveTask is { IsCompleted: false })
        {
            return;
        }

        _pendingSolveTask = null;
        _pendingSolveCts?.Dispose();
        _pendingSolveCts = null;
        _pendingSolveSnapshotVersion = 0;
    }

    private static bool IsSolutionUsableForDispatch(SafeZoneSolution? solution, SafeZoneSnapshot snapshot)
        => solution is not null && (solution.SourceSnapshotVersion == snapshot.Version
            || solution.IsSuccess && solution.ExecutablePath.Count >= 2);

    private void Dispatch(SafeZoneSolution? solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (_externalMovementEnabled() || _externalHasControl())
        {
            _phase = DispatchPhase.YieldingToHost;
            ReleaseControl();
            return;
        }

        if (solution is null || !solution.IsSuccess)
        {
            if (TryContinueDispatchedRoute(player, snapshot, now))
                return;
            _phase = DispatchPhase.Released;
            ReleaseControl();
            return;
        }

        if ((solution.SourceSnapshotVersion != snapshot.Version || solution.UsesTemporalPlanning || !IsPointUnsafeNow(snapshot, player.Position, now)) &&
            !IsTemporalPhaseSafe(solution, player, snapshot, now))
        {
            // 后台候选从旧位置起算，其前几个路点可能已在玩家身后。
            // 候选过期不等于正在执行的路线失效；从当前路点按最新危险窗复核后继续。
            if (TryContinueDispatchedRoute(player, snapshot, now))
                return;
            if (!TryUseEmergencyRoute(ref solution, player, snapshot, now))
            {
                _latestSolution = null;
                _phase = DispatchPhase.Released;
                ReleaseControl();
                return;
            }
        }

        if (solution.UsesTemporalPlanning && solution.ExecutablePath.Count > 0 &&
            solution.ExecutablePath[^1] is { IsWaitPoint: true } wait &&
            (solution.ExecutablePath.Count == 1 || GeometryHelper.DistanceXZ(player.Position, wait.Position) <= MinArrivalTolerance))
        {
            if (_phase != DispatchPhase.HoldingForDeparture)
            {
                // 首次接管可能已有跟随路线；用当前位置替换路线后才停止，确保停的是自己拥有的移动。
                if (!_hasControl)
                {
                    _greenMove!.MoveTo(new GreenMovePoint(player.Position, Tolerance: MinArrivalTolerance));
                    _hasControl = true;
                    _dispatchCount++;
                    _lastDispatchUtc = now;
                }
                _greenMove!.Stop();
                _dispatchedSolution = null;
            }
            _phase = DispatchPhase.HoldingForDeparture;
            return;
        }

        var shouldMove = solution.IsSuccess && solution.ExecutablePath.Count >= 2;

        // Waiting is allowed only while the delayed route still meets every damage window.
        if (shouldMove && !solution.UsesTemporalPlanning && ShouldWaitForCast(solution, player, snapshot, now))
        {
            _phase = DispatchPhase.WaitingForCast;
            shouldMove = false;
        }
        else if (shouldMove && ShouldWaitForEscape(solution, player, snapshot, now))
        {
            _phase = DispatchPhase.WaitingForEscape;
            shouldMove = false;
        }

        // 玩家真正无所事事时松手，避免膨胀环里的视觉抖动。
        if (shouldMove && CanSkipSolve(snapshot, player.Position, now))
        {
            _phase = DispatchPhase.Released;
            shouldMove = false;
        }

        if (!shouldMove)
        {
            if (_phase is not (DispatchPhase.WaitingForCast or DispatchPhase.WaitingForEscape))
            {
                _phase = DispatchPhase.Released;
            }

            ReleaseControl();
            return;
        }

        if (ShouldKeepCurrentRoute(solution, player, snapshot, now))
        {
            // TTL 重解可能产出"更近的新目标"，但旧 GreenMove 路线仍然有效。
            // 保持旧路线，避免每次 MoveTo 重置 GreenMove 索引造成目标跳变/拉直/原地停。
            _latestSolution = _dispatchedSolution;
            _phase = DispatchPhase.Moving;
            _hasControl = true;
            return;
        }

        if (!solution.UsesTemporalPlanning && ShouldDelayGoalReentry(solution, player, snapshot, now))
        {
            _phase = DispatchPhase.Released;
            ReleaseControl();
            return;
        }

        // 只在 Solution 实质变化时重新下发，避免每帧 MoveTo 重置 GreenMove 运行时状态。
        if (!ReferenceEquals(_dispatchedSolution, solution))
        {
            _greenMove!.MoveTo(BuildGreenMovePoints(solution, snapshot, now));
            _dispatchedSolution = solution;
            _dispatchCount++;
            _lastDispatchUtc = now;
        }

        _phase = DispatchPhase.Moving;
        _hasControl = true;
    }

    private bool TryContinueDispatchedRoute(PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (!_hasControl || _greenMove is not { IsRunning: true } ||
            _dispatchedSolution is not { IsSuccess: true } previous ||
            !IsTemporalPhaseSafe(previous, player, snapshot, now))
            return false;

        // 后续重算继续沿用已执行路线的目标和紧急余量，不继承被拒绝候选的状态。
        _latestSolution = previous;
        _phase = DispatchPhase.Moving;
        return true;
    }

    private bool TryUseEmergencyRoute(ref SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        const float emergencyStartSafety = 0.25f;
        if (solution.UsesTemporalPlanning || solution.ExecutablePath.Count < 2 || solution.ReducedDangerStartSafetyTime.HasValue
            || _options.DangerStartSafetyTime <= emergencyStartSafety
            || !SafeZoneSolver.HasPendingDangerAtPlayer(snapshot, player.Position, now)
            || !IsTemporalPhaseSafe(solution, player, snapshot, now, emergencyStartSafety))
            return false;

        // Publication latency can spend the ordinary cushion after a successful
        // solve. Recheck this route now, rather than losing another worker cycle.
        // Copy the result: an in-flight solve may still read the original.
        var emergency = solution.WithReducedDangerStartSafetyTime(emergencyStartSafety);
        if (ReferenceEquals(solution, _dispatchedSolution))
            _dispatchedSolution = emergency;
        solution = emergency;
        _latestSolution = emergency;
        return true;
    }

    private bool IsTemporalPhaseSafe(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, float? startSafety = null)
    {
        startSafety ??= solution.ReducedDangerStartSafetyTime;
        var path = solution.ExecutablePath;
        if (path.Count == 0)
            return IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, now, startSafety);
        if (path.Count == 1 && GeometryHelper.DistanceXZ(player.Position, path[0].Position) > MinArrivalTolerance)
            return false;

        var nextIndex = 1;
        if (ReferenceEquals(solution, _dispatchedSolution) && _greenMove is not null)
            nextIndex = Math.Clamp(_greenMove.GetStatus().CurrentIndex + 1, 1, path.Count);
        var position = player.Position;
        var time = now;
        // A non-temporal escape may begin inside persistent damage. Permit only
        // the initial exit from those shapes, never re-entry or an unsafe endpoint.
        HashSet<IGeometryShape>? escaping = null;
        if (!solution.UsesTemporalPlanning)
        {
            escaping = [];
            foreach (var danger in snapshot.DangerAreas)
                if (danger.Geometry.Contains(position))
                    foreach (var window in danger.DangerWindows)
                        if (window.IsActiveAt(now)) { escaping.Add(danger.Geometry); break; }
        }
        for (var i = nextIndex; i < path.Count; i++)
        {
            var arrival = time.AddSeconds(GeometryHelper.DistanceXZ(position, path[i].Position) / MathF.Max(0.1f, player.CurrentSpeed));
            if (!IsTemporalSegmentSafe(snapshot, position, path[i].Position, time, arrival, startSafety, escaping))
                return false;
            position = path[i].Position;
            time = arrival;
        }

        var end = path[^1];
        if (end.IsWaitPoint)
        {
            // 后台解算期间消耗掉等待余量时，不能再执行基于旧时间轴的转场。
            if (time > end.PlannedDepartureTime)
                return false;
            return IsTemporalSegmentSafe(snapshot, position, position, time, end.PlannedDepartureTime, startSafety);
        }
        return IsTemporalSegmentSafe(snapshot, position, position, time, time, startSafety);
    }

    private bool IsTemporalSegmentSafe(SafeZoneSnapshot snapshot, Vector3 from, Vector3 to, DateTime start, DateTime end, float? startSafety = null, HashSet<IGeometryShape>? escaping = null)
    {
        if (snapshot.ArenaBoundary is { } boundary && !IsArenaSegmentSafe(boundary, from, to))
            return false;
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(from) || obstacle.Geometry.Contains(to) || obstacle.Geometry.IntersectsSegment(from, to))
                return false;
        }
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(from) && !danger.Geometry.Contains(to) && !danger.Geometry.IntersectsSegment(from, to))
                continue;
            foreach (var window in danger.DangerWindows)
            {
                var buffered = new DangerWindow(window.DangerFrom.AddSeconds(-MathF.Max(0f, startSafety ?? _options.DangerStartSafetyTime)),
                    window.DangerUntil?.AddSeconds(MathF.Max(0f, _options.DangerEndSafetyTime)));
                if (buffered.OverlapsInterval(start, end))
                {
                    var steps = Math.Max(1, (int)MathF.Ceiling(GeometryHelper.DistanceXZ(from, to) / 0.1f));
                    for (var i = 0; i < steps; ++i)
                    {
                        var a = Vector3.Lerp(from, to, (float)i / steps);
                        var b = Vector3.Lerp(from, to, (float)(i + 1) / steps);
                        var t0 = start.AddTicks((end - start).Ticks * i / steps);
                        var t1 = start.AddTicks((end - start).Ticks * (i + 1) / steps);
                        if (buffered.OverlapsInterval(t0, t1) &&
                            (danger.Geometry.Contains(a) || danger.Geometry.Contains(b) || danger.Geometry.IntersectsSegment(a, b)))
                        {
                            if (escaping?.Contains(danger.Geometry) != true || !danger.Geometry.Contains(a))
                                return false;
                            if (!danger.Geometry.Contains(b)) escaping.Remove(danger.Geometry);
                        }
                    }
                }
            }
        }
        return true;
    }

    private static bool IsArenaSegmentSafe(IGeometryShape boundary, Vector3 from, Vector3 to)
    {
        if (!boundary.Contains(from) || !boundary.Contains(to))
            return false;
        if (boundary is CircleGeometry or RectangleGeometry || GeometryHelper.DistanceXZ(from, to) < 0.0001f)
            return true;
        if (boundary is DonutGeometry donut)
            return GeometryHelper.DistanceSquaredPointToSegmentXZ(donut.Position, from, to) >= donut.InnerRadius * donut.InnerRadius;
        // Endpoints can both be inside a concave arena while the segment crosses a notch.
        foreach (var contour in boundary.GetContours())
            if (GeometryShape.ContourIntersectsSegment(contour, from, to, GeometryHelper.DefaultEpsilon))
                return false;
        return true;
    }

    private bool ShouldUrgentlyResolve(SafeZoneSolution solution, SafeZoneSnapshot snapshot, PlayerState player, DateTime now)
    {
        // 玩家已经处于当前激活危险/障碍/边界外：不等普通 TTL，但仍受 UrgentResolveTtlSeconds 节流。
        if (IsPointUnsafeNow(snapshot, player.Position, now))
        {
            return true;
        }

        // 当前 Solution 的剩余路径已被当前危险/障碍/边界覆盖：立即进入更短 TTL。
        if (IsSolutionRouteUnsafeNow(solution, snapshot, player.Position, now))
        {
            return true;
        }

        // 计划时间明显过期且玩家仍没到达：当前路径时间轴已失真，应更快重算。
        return IsSolutionPlanOverdue(solution, player.Position, now);
    }

    private bool IsSolutionRouteUnsafeNow(SafeZoneSolution solution, SafeZoneSnapshot snapshot, Vector3 playerPosition, DateTime now)
    {
        var path = solution.ExecutablePath;
        if (path.Count < 2)
        {
            return false;
        }

        var nextPathIndex = 1;
        if (ReferenceEquals(solution, _dispatchedSolution) && _greenMove is not null)
        {
            var status = _greenMove.GetStatus();
            if (status.IsRunning)
            {
                // GreenMove route 去掉了 path[0] 起点，因此 CurrentIndex 0 对应 path[1]。
                nextPathIndex = Math.Clamp(status.CurrentIndex + 1, 1, path.Count - 1);
            }
        }

        var previous = playerPosition;
        for (var i = nextPathIndex; i < path.Count; i++)
        {
            var current = path[i].Position;
            if (IsPointUnsafeNow(snapshot, current, now) ||
                IsSegmentUnsafeNow(snapshot, previous, current, now))
            {
                return true;
            }

            previous = current;
        }

        return false;
    }

    private static bool IsSolutionPlanOverdue(SafeZoneSolution solution, Vector3 playerPosition, DateTime now)
    {
        var path = solution.ExecutablePath;
        if (path.Count < 2)
        {
            return false;
        }

        var final = path[path.Count - 1];
        if ((now - final.PlannedArrivalTime).TotalSeconds <= PlannedPathOverdueSeconds)
        {
            return false;
        }

        var dx = final.Position.X - playerPosition.X;
        var dz = final.Position.Z - playerPosition.Z;
        return ((dx * dx) + (dz * dz)) > PlannedPathMissDistance * PlannedPathMissDistance;
    }

    private static SolveOptions CreateMovementOptions()
    {
#if DEBUG
        // Debug 构建开启解算来源/网格信息记录，方便可视化诊断；Release 保持默认低分配。
        return SolveOptionsDefaults.Default with { GenerateDebugData = true };
#else
        return SolveOptionsDefaults.Default;
#endif
    }

    private bool ShouldKeepCurrentRoute(SafeZoneSolution newSolution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (!_hasControl || _dispatchedSolution is null || _greenMove is null)
        {
            return false;
        }

        if (!newSolution.IsSuccess || newSolution.ExecutablePath.Count < 2)
        {
            return false;
        }

        if (newSolution.SourceSnapshotVersion != _dispatchedSolution.SourceSnapshotVersion)
        {
            return false;
        }

        if (newSolution.UsesTemporalPlanning || _dispatchedSolution.UsesTemporalPlanning)
        {
            if (newSolution.UsesTemporalPlanning != _dispatchedSolution.UsesTemporalPlanning ||
                newSolution.TargetPosition is not { } next || _dispatchedSolution.TargetPosition is not { } previous ||
                GeometryHelper.DistanceXZ(next, previous) > MinArrivalTolerance ||
                IsSolutionPlanOverdue(_dispatchedSolution, player.Position, now))
                return false;
        }

        if (IsPointUnsafeNow(snapshot, player.Position, now))
        {
            return false;
        }

        var status = _greenMove.GetStatus();
        if (!status.IsRunning || status.FinalPoint is not { } finalPoint)
        {
            return false;
        }

        return !IsPointUnsafeNow(snapshot, finalPoint.Position, now) &&
               !IsDispatchedRouteUnsafeNow(snapshot, _dispatchedSolution, player.Position, status.CurrentIndex, now) &&
               IsTemporalPhaseSafe(_dispatchedSolution, player, snapshot, now);
    }

    private bool ShouldDelayGoalReentry(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if ((now - _lastUnsafeUtc).TotalSeconds >= GoalReentryStableSeconds)
        {
            return false;
        }

        // 当前仍在危险里时，绝不能因为滞回挡住逃逸路线。
        if (IsPointUnsafeNow(snapshot, player.Position, now))
        {
            return false;
        }

        if (solution.TargetPosition is not { } target ||
            !IsPositionInAnyActiveGoal(snapshot, target, now))
        {
            return false;
        }

        // 如果候选路线已经不是去目标区（例如 Solver 给了 GoalStandby），不拦。
        return true;
    }

    private static bool IsDispatchedRouteUnsafeNow(
        SafeZoneSnapshot snapshot,
        SafeZoneSolution dispatchedSolution,
        Vector3 playerPosition,
        int greenMoveCurrentIndex,
        DateTime now)
    {
        var path = dispatchedSolution.ExecutablePath;
        if (path.Count < 2)
        {
            return false;
        }

        var nextPathIndex = Math.Clamp(greenMoveCurrentIndex + 1, 1, path.Count - 1);
        var previous = playerPosition;
        for (var i = nextPathIndex; i < path.Count; i++)
        {
            var current = path[i].Position;
            if (IsPointUnsafeNow(snapshot, current, now) ||
                IsSegmentUnsafeNow(snapshot, previous, current, now))
            {
                return true;
            }

            previous = current;
        }

        return false;
    }

    private static bool IsSegmentUnsafeNow(SafeZoneSnapshot snapshot, Vector3 start, Vector3 end, DateTime now)
    {
        if (snapshot.ArenaBoundary is not null &&
            (!snapshot.ArenaBoundary.Contains(start) || !snapshot.ArenaBoundary.Contains(end)))
        {
            return true;
        }

        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.IntersectsSegment(start, end))
            {
                return true;
            }
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.IntersectsSegment(start, end))
            {
                continue;
            }

            var windows = danger.DangerWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>把 Solution 路径（去掉起点）转换为 GreenMove 路点。MaxCastWaitMs 恒为 0，读条等待由 <see cref="ShouldWaitForCast"/> 动态决策。</summary>
    private GreenMovePoint[] BuildGreenMovePoints(SafeZoneSolution solution, SafeZoneSnapshot snapshot, DateTime now)
    {
        var path = solution.ExecutablePath;
        var tolerance = solution.UsesTemporalPlanning ? MinArrivalTolerance : MathF.Max(MinArrivalTolerance, _options.ArrivalRadius);
        tolerance = MathF.Min(tolerance, solution.MaxArrivalTolerance);
        var destination = path[path.Count - 1].Position;
        foreach (var goal in snapshot.GoalAreas)
        {
            if (goal.Geometry is not CircleGeometry circle)
                continue;
            var clearance = circle.Radius - GeometryHelper.DistanceXZ(circle.Position, destination);
            if (clearance <= 0f)
                continue;
            foreach (var window in goal.GoalWindows)
            {
                if (!window.IsActiveAt(now))
                    continue;
                // 把格心偏差计入到达误差，整个停止容差圈都要在目标内。
                tolerance = MathF.Min(tolerance, clearance * 0.9f);
                break;
            }
        }

        // path[0] 是玩家当前位置，不下发；只下发 path[1..]。
        var points = new GreenMovePoint[path.Count - 1];
        for (var i = 1; i < path.Count; i++)
        {
            var wp = path[i];
            var speedMultiplier = wp.SpeedMultiplier > 0f ? wp.SpeedMultiplier : 1f;
            points[i - 1] = new GreenMovePoint(
                Position: wp.Position,
                Tolerance: tolerance,
                SpeedMultiplier: speedMultiplier,
                IgnoreY: true,
                MaxCastWaitMs: 0,
                WaitForQueuedCast: false);
        }

        return points;
    }

    /// <summary>读条可完成且延后出发的整条路线仍安全时等待；危险区域的实际退出时间决定余量。</summary>
    private bool ShouldWaitForCast(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (!player.IsCasting || player.CastTimeRemaining <= _options.CastSlideWindow)
            return false;
        return CanDelayEscape(solution, player, snapshot, now,
            player.CastTimeRemaining - _options.CastSlideWindow + DepartureReserveSeconds);
    }

    /// <summary>玩家当前位置此刻是否已处于激活危险区（几何覆盖 + 窗口 now 激活）。只读 DangerFrom/DangerUntil，不读 TelegraphFrom。</summary>
    private static bool IsCurrentPositionDangerousNow(PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(player.Position))
            {
                continue;
            }

            var windows = danger.DangerWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPointUnsafeNow(SafeZoneSnapshot snapshot, Vector3 point, DateTime now)
    {
        if (snapshot.ArenaBoundary is not null && !snapshot.ArenaBoundary.Contains(point))
        {
            return true;
        }

        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(point))
            {
                return true;
            }
        }

        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(point))
            {
                continue;
            }

            var windows = danger.DangerWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPositionInAnyActiveGoal(SafeZoneSnapshot snapshot, Vector3 position, DateTime now)
    {
        foreach (var goal in snapshot.GoalAreas)
        {
            if (!goal.Geometry.Contains(position))
            {
                continue;
            }

            var windows = goal.GoalWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].IsActiveAt(now))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 判断玩家"真正无所事事"：当前几何安全 + 当前位置无未来激活的危险 + 整个 snapshot 无激活目标区。
    /// 三者全满足才允许跳过 Solve 调用。
    /// <para>只读 <see cref="DangerWindow.DangerFrom"/>/<see cref="DangerWindow.DangerUntil"/>，不读 <c>TelegraphFrom</c>。</para>
    /// </summary>
    private static bool CanSkipSolve(SafeZoneSnapshot snapshot, Vector3 playerPosition, DateTime now)
    {
        if (IsPointUnsafeNow(snapshot, playerPosition, now))
        {
            return false;
        }

        // 玩家当前位置存在未来要激活的危险窗口 → 必须走 Solve（覆盖脚下延迟命中场景）。
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(playerPosition))
            {
                continue;
            }

            var windows = danger.DangerWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                var w = windows[i];
                if (w.DangerFrom > now && (w.DangerUntil is null || w.DangerUntil.Value > now))
                {
                    return false;
                }
            }
        }

        // 存在任何激活的目标区 → 必须走 Solve，让 A* 用 PixelPriority 把玩家带向目标。
        foreach (var goal in snapshot.GoalAreas)
        {
            var windows = goal.GoalWindows;
            for (var i = 0; i < windows.Count; i++)
            {
                if (windows[i].IsActiveAt(now))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>若本系统正在接管 GreenMove 则停止；否则不动（不抢占其它来源的 GreenMove）。</summary>
    private void ReleaseControl()
    {
        if (!_hasControl)
        {
            return;
        }

        // 宿主安全区可能已在本帧替换路线，此时只撤销自己的控制记录。
        if (!_externalHasControl())
            _greenMove?.Stop();
        _hasControl = false;
        _dispatchedSolution = null;
    }

    private sealed class AsyncSolveResult
    {
        private AsyncSolveResult(
            int snapshotVersion,
            SafeZoneSolution? solution,
            bool wasCancelled,
            Exception? exception)
        {
            SnapshotVersion = snapshotVersion;
            Solution = solution;
            WasCancelled = wasCancelled;
            Exception = exception;
        }

        public int SnapshotVersion { get; }

        public SafeZoneSolution? Solution { get; }

        public bool WasCancelled { get; }

        public Exception? Exception { get; }

        public static AsyncSolveResult Completed(
            int snapshotVersion,
            SafeZoneSolution solution,
            bool wasCancelled)
            => new(snapshotVersion, solution, wasCancelled, null);

        public static AsyncSolveResult Cancelled(int snapshotVersion)
            => new(snapshotVersion, null, true, null);

        public static AsyncSolveResult Failed(int snapshotVersion, Exception exception)
            => new(snapshotVersion, null, false, exception);
    }
}
