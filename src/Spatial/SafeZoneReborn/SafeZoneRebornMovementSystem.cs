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
    internal Func<Vector3, Vector3, bool>? CheckTerrainTraversal { get; set; }

    private Func<Func<AsyncSolveResult>, CancellationToken, Task<AsyncSolveResult>> _scheduleSolve =
        (solve, token) => Task.Run(solve, token);

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
    public double LastSolveQueueMilliseconds { get; private set; }
    public double LastSolveWorkMilliseconds { get; private set; }
    public double LastSolvePublishMilliseconds { get; private set; }
    private DateTime _lastSolveStartedAt;
    private int _lastSolveVersion = -1;

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
    public SafeZoneSolution? LatestSolution => _hasControl && _dispatchedSolution != null ? _dispatchedSolution : _latestSolution;

    /// <summary>本系统当前是否正在接管 GreenMove。</summary>
    public bool HasControl => _hasControl;

    internal bool ShouldRetainTerrain(IGeometryShape boundary, Vector3 position)
    {
        if (!_hasControl || !_setting.EffectiveEnableAutoMovement || _externalMovementEnabled() || _externalHasControl()
            || _phase != DispatchPhase.Moving || handoffDangers.Count == 0 || _directTarget != null
            || _dispatchedSolution is not { IsSuccess: true } route
            || _greenMove?.GetStatus() is not { IsRunning: true, CurrentPoint: { } current, FinalPoint: { } final } status)
            return false;
        var path = route.ExecutablePath;
        var next = status.CurrentIndex + 1;
        if (next < 1 || next >= path.Count || status.TotalCount != path.Count - 1
            || current.Position != path[next].Position || final.Position != path[^1].Position)
            return false;
        for (var i = next; i < path.Count; ++i)
        {
            if (!ArenaBoundaryTraversal.ContainsSegment(boundary, position, path[i].Position)) return false;
            position = path[i].Position;
        }
        return true;
    }

    private Vector3? _jumpLanding;
    private DateTime _jumpCommittedAt;
    private Vector3? _directTarget;
    private Vector3? _directOrigin;
    private DateTime _directUntil;
    private IReadOnlyList<Vector3> _directPath = [];
    private int _directGeneration, _directDispatchedGeneration = -1, _directRejectedGeneration = -1;
    private float _directHeadingTolerance;
    private DirectTraversalMotion? _directMotion;
    private DateTime? _directDeadline;
    private int _directProgress, _directOffset, _directValidationVersion;
    private DateTime _directNextValidation;
    private bool _directInputAcquired;
    private IReadOnlyList<Vector3> _directAcceptedPath = [];
    private DirectTraversalMotion? _directAcceptedMotion;
    private int _directAcceptedOffset, _directAcceptedProgress;
    private int _directLockedGeneration = -1, _directLockedIndex = -1;
    private DateTime _directInputLockedUntil;
    private sealed record DirectPhysicalTail(int Generation, int Index, Vector3 From, Vector3 To, DateTime Until);
    private DirectPhysicalTail? _directPhysicalTail;
    private sealed record DirectSuffixAmendment(int Generation, int CurrentIndex, int AbsoluteIndex,
        Vector3 CurrentPoint, Vector3 Target, Vector3[] Path, DirectTraversalMotion Motion, bool ConsumedCurrent = false);
    private DirectSuffixAmendment? _directSuffixAmendment;
    private DirectDangerIndex? _directDangerIndex;
    private readonly Dictionary<(IDirectTraversalTiming Timing, Vector3 From, Vector3 To, float Speed), IDirectTraversalClock> _directClocks = [];

    internal DirectTraversalExecution GetDirectTraversalExecution(DateTime now)
    {
        var status = _greenMove?.GetStatus();
        var running = status is { IsRunning: true };
        // GreenMove can consume the native waypoint at the end of a slide
        // before the physical slide has finished. Keep reporting the route as
        // controlled during that bounded lease so the planner can amend it
        // without turning the consumed point into a movement stop.
        var slideLease = !running && _directMotion is { InputLockActive: true }
            && now < _directInputLockedUntil
            && _directProgress >= 0 && _directProgress < _directPath.Count
            && _directMotion.Locked(_directProgress);
        var controlled = _hasControl && _directGeneration == _directDispatchedGeneration
            && (running || slideLease);
        var index = running ? _directAcceptedOffset + status!.Value.CurrentIndex : _directAcceptedProgress;
        return new(_directAcceptedPath, index, controlled, _directTarget != null && _directRejectedGeneration == _directGeneration,
            now < _directInputLockedUntil, _directAcceptedMotion);
    }

    internal void SetDirectTraversal(Vector3? target, DateTime until, IReadOnlyList<Vector3>? path = null,
        float headingTolerance = 0, DirectTraversalMotion? motion = null, DateTime? sampledAt = null, DateTime? deadline = null)
    {
        // Deadline budgets currently support ordinary walking only. Native motion
        // has physical lock leases and must not silently bypass the budget.
        if (deadline.HasValue && motion != null)
            throw new ArgumentException("A direct traversal deadline requires ordinary walking.", nameof(deadline));
        _directDeadline = target.HasValue ? deadline : null;
        path ??= target is { } p ? [p] : [];
        // Planned locked points include the approach to ice. Only a sampled
        // active game status proves that steering is currently irreversible.
        // A clear without a status sample preserves the bounded physical lease.
        if (motion is { InputLockActive: false })
        {
            _directInputLockedUntil = default;
            _directLockedGeneration = _directLockedIndex = -1;
            _directPhysicalTail = null;
        }
        if (_directMotion != null && motion != null)
            _directMotion = _directMotion with { InputLockActive = motion.InputLockActive };
        var motionChanged = motion != null && _directMotion != null && !motion.Matches(_directMotion, path);
        if (_directTarget != target || !_directPath.SequenceEqual(path) || motionChanged)
        {
            if (target is { } amendedTarget && _hasControl && _directGeneration == _directDispatchedGeneration
                && _greenMove?.GetStatus() is { } status)
            {
                var consumedCurrent = !status.IsRunning && _directMotion is { InputLockActive: true }
                    && _directInputAcquired && _directLockedGeneration == _directGeneration
                    && _directProgress == _directPath.Count - 1 && _directLockedIndex == _directProgress
                    && sampledAt is { } observedAt && observedAt < _directInputLockedUntil;
                var absolute = status.IsRunning ? _directOffset + status.CurrentIndex : _directProgress;
                if (absolute >= 0 && absolute < _directPath.Count && _directMotion != null && motion != null)
                {
                    _directUntil = until;
                    _directSuffixAmendment = null;
                    var shared = 0;
                    while (shared < Math.Min(_directPath.Count, path.Count) && _directPath[shared] == path[shared]
                        && motion != null && _directMotion.Speed(shared, 0) == motion.Speed(shared, 0)
                        && Equals(_directMotion.Timing, motion.Timing)
                        && _directMotion.Locked(shared) == motion.Locked(shared)
                        && _directMotion.Landing(shared, _directPath[shared]) == motion.Landing(shared, path[shared])
                        && _directMotion.ArrivalTolerance(shared) == motion.ArrivalTolerance(shared)) ++shared;
                    if ((status.IsRunning && status.CurrentPoint?.Position == _directPath[absolute] || consumedCurrent)
                        && shared > absolute && path.Count > absolute + 1
                        && path[^1] == amendedTarget && motion != null)
                        _directSuffixAmendment = new(_directGeneration, status.CurrentIndex, absolute,
                            _directPath[absolute], amendedTarget, path.ToArray(), motion, consumedCurrent);
                    // Ordinary motion can also keep its safe current point.
                    // A locked point cannot accept a whole-route replacement.
                    if (_directSuffixAmendment != null
                        || status.IsRunning && _directMotion.InputLockActive && _directMotion.Locked(absolute)) return;
                }
            }
            // A walking shortcut is optional until UpdateDirectTraversal validates and dispatches it.
            // Offering one (or withdrawing one never dispatched) must not discard the solver's running
            // escape: run1888-b4 B26 lost its validated exit from a chasing puddle when the staging
            // shortcut appeared, then the shortcut's deadline rejected it and the player stood still.
            var keepSolverRoute = motion == null && _directMotion == null
                && (_directTarget == null ? target != null : target == null && _directDispatchedGeneration != _directGeneration);
            _directSuffixAmendment = null;
            ++_directGeneration;
            _directProgress = _directOffset = 0; _directNextValidation = default;
            _directInputAcquired = false;
            _directPhysicalTail = null;
            _directRejectedGeneration = -1;
            _directOrigin = null;
            if (!keepSolverRoute)
            {
                CancelPendingSolve();
                _latestSolution = _dispatchedSolution = null;
                if (target == null || _directTarget == null) ReleaseControl();
            }
        }
        else _directSuffixAmendment = null;
        _directTarget = target; _directUntil = until;
        if (!_directPath.SequenceEqual(path)) _directPath = path.ToArray();
        _directHeadingTolerance = headingTolerance;
        _directMotion = motion;
        if (target == null) _directClocks.Clear();
    }

    private bool UpdateDirectTraversal(PlayerState player, SafeZoneSnapshot snapshot, DateTime now, bool allowed)
    {
        if (_directTarget is not { } target) return false;
        if (now > _directUntil)
        { SetDirectTraversal(null, now); return false; }
        if (_directRejectedGeneration == _directGeneration)
        {
            ReleaseControl();
            _phase = DispatchPhase.Released;
            return _directMotion != null;
        }
        if (_directMotion != null) CancelPendingSolve();
        if (!allowed || !_setting.EffectiveEnableAutoMovement || _externalMovementEnabled() || _externalHasControl())
        { ReleaseControl(); return true; }
        if (_directRejectedGeneration == _directGeneration)
        { ReleaseControl(); _phase = DispatchPhase.Released; return true; }
        ApplyDirectSuffixAmendment(player, snapshot, now);
        target = _directTarget.Value;
        var running = _directGeneration == _directDispatchedGeneration && _hasControl && _greenMove is { IsRunning: true };
        if (running)
        {
            var progress = Math.Clamp(_directOffset + _greenMove!.GetStatus().CurrentIndex, 0, _directPath.Count);
            if (progress != _directProgress)
            {
                _directPhysicalTail = CommittedPreviousDirectRay(progress, now);
                // Arrival tolerance can advance the queue before a slide ends.
                // Carry its commitment across locked seams, but walking toward
                // the ice does not prove that the first slide has acquired input.
                _directInputAcquired = _directInputAcquired && _directMotion?.InputLockActive == true
                    && _directMotion.Locked(_directProgress)
                    && _directMotion?.Locked(progress) == true;
                _directProgress = progress;
                _directAcceptedProgress = progress;
                _directNextValidation = default;
            }
            else if (player.IsMoving) _directInputAcquired = true;
        }
        var next = _directProgress;
        if (!running && _directMotion is { InputLockActive: true } && now < _directInputLockedUntil)
        {
            // The native waypoint may have been consumed while its physical
            // slide is still in flight. GreenMove is already idle here, so a
            // release would count as a slide stop and discard the continuation
            // opportunity. Hold the lease until the planner appends/replaces
            // the suffix or the bounded physical window expires.
            if (_directProgress >= 0 && _directProgress < _directPath.Count
                && _directMotion.Locked(_directProgress))
            { _phase = DispatchPhase.Moving; return true; }
            ReleaseControl(); _phase = DispatchPhase.Released; return true;
        }
        var finalTolerance = next >= 0 && next < _directPath.Count
            ? _directMotion?.ArrivalTolerance(next) ?? .03f : .03f;
        if (next >= _directPath.Count || next == _directPath.Count - 1
            && GeometryHelper.DistanceXZ(player.Position, _directMotion?.Landing(next, target) ?? target) <= finalTolerance)
        {
            // Keep ownership until GreenMove consumes its final point. A
            // physical tolerance can be wider than the native arrival radius.
            if (running && GeometryHelper.DistanceXZ(player.Position, target) > .03f)
            { _phase = DispatchPhase.Moving; return true; }
            ReleaseControl(); _phase = DispatchPhase.Idle; return true;
        }
        if (running && _directMotion != null && !IsWithinDirectCorridor(player.Position, next, now))
        {
            _directRejectedGeneration = _directGeneration; ReleaseControl(); _phase = DispatchPhase.Released; return true;
        }
        // The game ignores steering and release during an acquired slide.
        // Keep its input active; validated amendments above can update future
        // points while retaining the native current target.
        if (running && _directInputAcquired && _directMotion?.InputLockActive == true && _directMotion.Locked(next))
        {
            if (_directLockedGeneration != _directGeneration || _directLockedIndex != next
                || now >= _directInputLockedUntil)
            {
                _directLockedGeneration = _directGeneration; _directLockedIndex = next;
                // A native waypoint may be consumed before the physical 5m
                // slide ends. Keep a bounded lock lease even after release.
                _directInputLockedUntil = now.AddSeconds(5.7 / MathF.Max(.1f, _directMotion.Speed(next, 3)) + .2);
            }
            _phase = DispatchPhase.Moving; return true;
        }
        // A completion deadline is a live budget, unlike cached geometry. A speed
        // drop or tightened deadline must stop this frame, not after the 50ms cache.
        if (_directDeadline is {} deadlineBudget)
        {
            var arrival = now.AddSeconds(player.IsMoving || running && _directInputAcquired ? 0 : .1);
            var origin = player.Position;
            for(var i=next;i<_directPath.Count;i++)
            {
                var end = _directMotion?.Landing(i,_directPath[i]) ?? _directPath[i];
                var speed = _directMotion?.Speed(i,player.CurrentSpeed) ?? player.CurrentSpeed;
                arrival=arrival.AddSeconds(DirectTravelDuration(_directMotion,i,origin,end,speed));
                origin=end;
            }
            if(arrival>deadlineBudget)
            {
                if (_directMotion == null) return RejectWalkingShortcut();
                _phase=DispatchPhase.Released; ReleaseControl(); return true;
            }
        }
        if (running && now < _directNextValidation && _directValidationVersion == snapshot.Version) return true;
        _directNextValidation = now.AddSeconds(.05); _directValidationVersion = snapshot.Version;
        // Validate the complete run, including up to 100ms to acquire native input.
        // No intermediate wait/partial escape or background publication delay.
        var delay = player.IsMoving || running && _directInputAcquired ? 0 : .1;
        var safe = IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, now.AddSeconds(delay), .2f, endSafety: .1f);
        var points = new List<TimedWaypoint> { TimedWaypoint.Pass(player.Position, now) };
        var from = player.Position; var time = now;
        var validationEnd = running && _directMotion != null ? Math.Min(_directPath.Count, next + 1) : _directPath.Count;
        for (var i = next; safe && i < _directPath.Count; ++i)
        {
            var to = _directPath[i];
            var landing = _directMotion?.Landing(i, to) ?? to;
            var speed = _directMotion?.Speed(i, player.CurrentSpeed) ?? player.CurrentSpeed;
            var end = time.AddSeconds(DirectTravelDuration(_directMotion, i, from, landing, speed));
            for (var offset = 0.0; i < validationEnd && safe && offset <= delay + .001; offset += .025)
                safe = IsDirectSegmentSafe(snapshot, _directMotion, i, from, landing, speed, time.AddSeconds(offset), end.AddSeconds(offset),
                    i == _directPath.Count - 1 ? .1 : 0);
            // Before native input locks the arrow, a frame of rotation can
            // change the entire ray. Verify that corridor before starting.
            if (safe && _directHeadingTolerance > 0 && _directPath.Count == 1)
            {
                var d = to - from; var angle = MathF.Atan2(d.X, d.Z);
                for (var sign = -1; sign <= 1 && safe; sign += 2)
                {
                    var a = angle + sign * _directHeadingTolerance;
                    var ray = from + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * (d.Z / MathF.Cos(a));
                    var arrival = time.AddSeconds(GeometryHelper.DistanceXZ(from, ray) / MathF.Max(.1f, player.CurrentSpeed));
                    for (var offset = 0.0; safe && offset <= delay + .001; offset += .025)
                        safe = IsTemporalSegmentSafe(snapshot, from, ray, time.AddSeconds(offset), arrival.AddSeconds(offset), .2f, endSafety: .1f);
                }
            }
            if (_directDeadline is {} deadline && end.AddSeconds(delay) > deadline) safe = false;
            points.Add(TimedWaypoint.Pass(to, end)); from = landing; time = end;
        }
        if (!safe)
        {
            if (_directMotion == null) return RejectWalkingShortcut();
            if (_directMotion != null) _directRejectedGeneration = _directGeneration;
            _phase = DispatchPhase.Released; ReleaseControl(); return true;
        }
        CancelPendingSolve();
        var solution = new SafeZoneSolution(SolutionStatus.Success, snapshot.Version, now, now, target,
            points)
            { UsesTemporalPlanning = true, MaxArrivalTolerance = .03f, ReducedDangerStartSafetyTime = .2f };
        _latestSolution = solution;
        if (!_hasControl || _directGeneration != _directDispatchedGeneration || _greenMove?.IsRunning != true)
        {
            var queue = BuildGreenMovePoints(solution, snapshot, now);
            if (!ValidateTerrainPath(player.Position, queue))
            {
                _latestSolution = null;
                _directRejectedGeneration = _directGeneration;
                _phase = DispatchPhase.Released;
                ReleaseControl();
                return true;
            }
            for (var i = 0; i < queue.Length; ++i)
                if (_directMotion?.Locked(next + i) == true)
                    queue[i] = queue[i] with { Tolerance = _directMotion.ArrivalTolerance(next + i) };
            if (!PrepareMovementHandoff(snapshot, player, now, true))
            {
                ReleaseControl();
                return true;
            }
            _greenMove!.MoveTo(queue);
            _directOrigin ??= player.Position;
            _directOffset = next;
            _directAcceptedOffset = next; _directAcceptedProgress = next;
            _directAcceptedPath = Array.AsReadOnly(_directPath.ToArray());
            _directAcceptedMotion = _directMotion;
            _dispatchedSolution = solution;
            _directDispatchedGeneration = _directGeneration;
            _dispatchCount++; _lastDispatchUtc = now;
        }
        _hasControl = true; _phase = DispatchPhase.Moving;
        return true;
    }

    private bool RejectWalkingShortcut()
    {
        // An unsafe walking shortcut is optional. Leave an ordinary escape worker
        // and its validated route running; only withdraw a route we sent directly.
        if (_directDispatchedGeneration >= 0)
        {
            if (ReferenceEquals(_latestSolution, _dispatchedSolution)) _latestSolution = null;
            ReleaseControl();
            _directDispatchedGeneration = -1;
        }
        return false;
    }

    private void ApplyDirectSuffixAmendment(PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (_directSuffixAmendment is not { } amendment) return;
        _directSuffixAmendment = null;
        var status = _greenMove!.GetStatus();
        if (!_hasControl || amendment.Generation != _directGeneration) return;
        if (amendment.ConsumedCurrent)
        {
            if (status.IsRunning || !_directInputAcquired || _directMotion is not { InputLockActive: true }
                || now >= _directInputLockedUntil || _directLockedIndex != amendment.AbsoluteIndex
                || _directProgress != amendment.AbsoluteIndex) return;
        }
        else if (!status.IsRunning || status.CurrentIndex != amendment.CurrentIndex
            || status.CurrentPoint?.Position != amendment.CurrentPoint
            || _directOffset + status.CurrentIndex != amendment.AbsoluteIndex) return;
        if (!IsWithinDirectCorridor(player.Position, amendment.AbsoluteIndex, now))
            return;
        var previousTail = amendment.ConsumedCurrent
            ? new DirectPhysicalTail(_directGeneration, amendment.AbsoluteIndex,
                amendment.AbsoluteIndex > 0 ? _directMotion!.Landing(amendment.AbsoluteIndex - 1, _directPath[amendment.AbsoluteIndex - 1])
                    : _directOrigin ?? player.Position,
                _directMotion!.Landing(amendment.AbsoluteIndex, amendment.CurrentPoint), _directInputLockedUntil)
            : CommittedPreviousDirectRay(amendment.AbsoluteIndex, now);
        var points = new List<TimedWaypoint> { TimedWaypoint.Pass(player.Position, now) };
        var from = player.Position; var time = now;
        var acquiredCurrent = _directInputAcquired && _directMotion?.InputLockActive == true
            && _directMotion.Locked(_directProgress) && amendment.Motion.InputLockActive
            && amendment.Motion.Locked(amendment.AbsoluteIndex);
        for (var i = amendment.AbsoluteIndex; i < amendment.Path.Length; ++i)
        {
            var to = amendment.Path[i];
            var landing = amendment.Motion.Landing(i, to);
            var speed = amendment.Motion.Speed(i, player.CurrentSpeed);
            var end = time.AddSeconds(DirectTravelDuration(amendment.Motion, i, from, landing, speed));
            // Only an acquired native current target is irreversible. Retained
            // future prefix points must also be safe in the latest snapshot.
            if (i > amendment.AbsoluteIndex || !acquiredCurrent)
                for (var offset = 0.0; offset <= .10001; offset += .025)
                    if (!IsDirectSegmentSafe(snapshot, amendment.Motion, i, from, landing, speed, time.AddSeconds(offset), end.AddSeconds(offset),
                        i == amendment.Path.Length - 1 ? .1 : 0))
                        return;
            points.Add(TimedWaypoint.Pass(to, end));
            from = landing; time = end;
        }
        var solution = new SafeZoneSolution(SolutionStatus.Success, snapshot.Version, now, now, amendment.Target, points)
            { UsesTemporalPlanning = true, MaxArrivalTolerance = .03f, ReducedDangerStartSafetyTime = .2f };
        var queue = BuildGreenMovePoints(solution, snapshot, now);
        for (var i = 0; i < queue.Length; ++i)
            if (amendment.Motion.Locked(amendment.AbsoluteIndex + i))
                queue[i] = queue[i] with { Tolerance = amendment.Motion.ArrivalTolerance(amendment.AbsoluteIndex + i) };
        status = _greenMove.GetStatus();
        if (amendment.ConsumedCurrent)
        {
            if (status.IsRunning || now >= _directInputLockedUntil) return;
        }
        else if (!status.IsRunning || status.CurrentIndex != amendment.CurrentIndex
            || status.CurrentPoint?.Position != amendment.CurrentPoint) return;
        // These synchronous calls preserve the native current point and input.
        // It can already be the next target while the previous 5m slide runs.
        if (amendment.ConsumedCurrent)
        {
            // The old native queue is already idle; enqueue only its new
            // successor. The game keeps the acquired physical ray until its
            // seam, even though the next native target is now armed.
            _greenMove.MoveTo(queue.Skip(1).ToArray());
            _dispatchCount++; _lastDispatchUtc = now;
        }
        else
        {
            _greenMove.ClearQueue();
            _greenMove.Append(queue.Skip(1).ToArray());
        }
        _directInputAcquired = _directInputAcquired && (_directProgress == amendment.AbsoluteIndex
            || _directMotion?.InputLockActive == true && amendment.Motion.InputLockActive
                && _directMotion.Locked(_directProgress) && amendment.Motion.Locked(amendment.AbsoluteIndex));
        _directTarget = amendment.Target; _directPath = amendment.Path; _directMotion = amendment.Motion;
        ++_directGeneration; _directDispatchedGeneration = _directGeneration; _directRejectedGeneration = -1;
        _directPhysicalTail = previousTail is { } tail ? tail with { Generation = _directGeneration } : null;
        _directProgress = amendment.AbsoluteIndex + (amendment.ConsumedCurrent ? 1 : 0);
        _directOffset = amendment.ConsumedCurrent ? _directProgress : _directOffset;
        _directAcceptedOffset = _directOffset;
        _directAcceptedProgress = _directProgress; _directAcceptedPath = Array.AsReadOnly(amendment.Path.ToArray());
        _directAcceptedMotion = amendment.Motion;
        // Updating the suffix does not begin a new physical slide.
        if (amendment.Motion.InputLockActive && _directLockedIndex == amendment.AbsoluteIndex)
            _directLockedGeneration = _directGeneration;
        _directNextValidation = now.AddSeconds(.05); _directValidationVersion = snapshot.Version;
        _latestSolution = _dispatchedSolution = solution;
    }

    private bool IsWithinDirectCorridor(Vector3 position, int index, DateTime now)
    {
        var origin = index > 0 ? _directMotion?.Landing(index - 1, _directPath[index - 1]) ?? _directPath[index - 1]
            : _directOrigin ?? position;
        var landing = _directMotion?.Landing(index, _directPath[index]) ?? _directPath[index];
        if (GeometryHelper.DistanceSquaredPointToSegmentXZ(position, origin, landing) <= .8f * .8f) return true;
        // A released slide can reach its native waypoint before the predicted
        // physical endpoint. The following walking ray starts at that consumed
        // waypoint; validate its actual remaining motion normally below.
        if (index > 0 && _directMotion is { InputLockActive: false } motion
            && motion.Locked(index - 1) && !motion.Locked(index)
            && GeometryHelper.DistanceSquaredPointToSegmentXZ(position, _directPath[index - 1], landing) <= .8f * .8f)
            return true;
        return CommittedPreviousDirectRay(index, now) is { } tail
            && GeometryHelper.DistanceSquaredPointToSegmentXZ(position, tail.From, tail.To) <= .8f * .8f;
    }

    private DirectPhysicalTail? CommittedPreviousDirectRay(int index, DateTime now)
    {
        if (!_directInputAcquired || _directMotion is not { InputLockActive: true } motion
            || index <= 0 || !motion.Locked(index - 1) || !motion.Locked(index)) return null;
        if (_directPhysicalTail is { } tail && tail.Generation == _directGeneration
            && tail.Index == index - 1 && now < tail.Until) return tail;
        // Native arrival tolerance can consume one point before its physical
        // slide ends. Its acquired lock and bounded lease prove that old ray.
        if (_directProgress != index - 1 || _directLockedGeneration != _directGeneration
            || _directLockedIndex != index - 1 || now >= _directInputLockedUntil) return null;
        var previous = index - 1;
        var from = previous > 0 ? motion.Landing(previous - 1, _directPath[previous - 1])
            : _directOrigin ?? _directPath[previous];
        return new(_directGeneration, previous, from, motion.Landing(previous, _directPath[previous]), _directInputLockedUntil);
    }

    private static double DirectTravelDuration(DirectTraversalMotion? motion, int index, Vector3 from, Vector3 to, float speed)
        => motion?.Locked(index) == true && motion.Timing is { } timing
            ? timing.Duration(from, to, speed) : GeometryHelper.DistanceXZ(from, to) / MathF.Max(.1f, speed);

    private bool IsDirectSegmentSafe(SafeZoneSnapshot snapshot, DirectTraversalMotion? motion, int index,
        Vector3 from, Vector3 to, float speed, DateTime start, DateTime end, double endpointSafety = 0)
    {
        if (snapshot.ArenaBoundary is { } boundary && !IsArenaSegmentSafe(boundary, from, to)) return false;
        foreach (var obstacle in snapshot.HardObstacles)
            if (obstacle.Geometry.Contains(from) || obstacle.Geometry.Contains(to) || obstacle.Geometry.IntersectsSegment(from, to))
                return false;
        if (_directDangerIndex == null || !ReferenceEquals(_directDangerIndex.Snapshot, snapshot))
            _directDangerIndex = new(snapshot);
        if (motion?.Locked(index) != true || motion.Timing is not { } timing)
            return _directDangerIndex.IsSafe(new DirectLinearClock(from, to, (end - start).TotalSeconds), from, to, start);
        var key = (timing, from, to, speed);
        if (!_directClocks.TryGetValue(key, out var clock))
        {
            if (_directClocks.Count >= 256) _directClocks.Clear();
            _directClocks[key] = clock = timing.Prepare(from, to, speed);
        }
        if (!_directDangerIndex.IsSafe(clock, from, to, start)) return false;
        return endpointSafety <= 0 || _directDangerIndex.IsSafe(to, to,
            start.AddSeconds(clock.EarliestDuration), start.AddSeconds(clock.LatestDuration + endpointSafety));
    }

    private sealed class DirectLinearClock(Vector3 from, Vector3 to, double duration) : IDirectTraversalClock
    {
        public double EarliestDuration => duration;
        public double LatestDuration => duration;
        public (Vector3 From, Vector3 To)? Occupancy(double first, double last)
        {
            if (last < 0 || first > duration || first > last) return null;
            if (duration <= 0) return (from, to);
            return (Vector3.Lerp(from, to, (float)Math.Clamp(first / duration, 0, 1)),
                Vector3.Lerp(from, to, (float)Math.Clamp(last / duration, 0, 1)));
        }
    }

    private sealed class DirectDangerIndex
    {
        private const float RowSize = 2;
        private sealed record Danger(IGeometryShape Geometry, GeometryBounds Bounds, bool FiniteBounds, DangerWindow[] Windows);
        private readonly Dictionary<int, Danger[]> _rows;
        private readonly Danger[] _broad, _all;
        public SafeZoneSnapshot Snapshot { get; }

        public DirectDangerIndex(SafeZoneSnapshot snapshot)
        {
            Snapshot = snapshot;
            var rows = new Dictionary<int, List<Danger>>(); var broad = new List<Danger>();
            _all = new Danger[snapshot.DangerAreas.Count];
            for (var i = 0; i < _all.Length; ++i)
            {
                var source = snapshot.DangerAreas[i]; var bounds = source.Geometry.GetBounds().Expand(GeometryHelper.DefaultEpsilon);
                var finite = float.IsFinite(bounds.MinX) && float.IsFinite(bounds.MaxX)
                    && float.IsFinite(bounds.MinZ) && float.IsFinite(bounds.MaxZ);
                var danger = new Danger(source.Geometry, bounds, finite, source.DangerWindows.Select(w => w.Buffered(.2f, .1f)).ToArray());
                _all[i] = danger;
                var first = finite ? MathF.Floor(bounds.MinZ / RowSize) : float.NegativeInfinity;
                var last = finite ? MathF.Floor(bounds.MaxZ / RowSize) : float.PositiveInfinity;
                if (first < int.MinValue || last >= int.MaxValue || last - first > 256)
                { broad.Add(danger); continue; }
                for (var row = (int)first; row <= (int)last; ++row)
                {
                    if (!rows.TryGetValue(row, out var entries)) rows[row] = entries = [];
                    entries.Add(danger);
                }
            }
            _rows = rows.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()); _broad = broad.ToArray();
        }

        public bool IsSafe(Vector3 from, Vector3 to, DateTime start, DateTime end)
        {
            var bounds = new GeometryBounds(MathF.Min(from.X, to.X), MathF.Min(from.Z, to.Z),
                MathF.Max(from.X, to.X), MathF.Max(from.Z, to.Z));
            var first = MathF.Floor(bounds.MinZ / RowSize); var last = MathF.Floor(bounds.MaxZ / RowSize);
            if (!float.IsFinite(first) || !float.IsFinite(last) || first < int.MinValue || last >= int.MaxValue || last - first > 256)
                return Safe(_all, from, to, bounds, start, end);
            if (!Safe(_broad, from, to, bounds, start, end)) return false;
            for (var row = (int)first; row <= (int)last; ++row)
                if (_rows.TryGetValue(row, out var danger) && !Safe(danger, from, to, bounds, start, end)) return false;
            return true;
        }

        public bool IsSafe(IDirectTraversalClock clock, Vector3 from, Vector3 to, DateTime start)
        {
            var bounds = new GeometryBounds(MathF.Min(from.X, to.X), MathF.Min(from.Z, to.Z),
                MathF.Max(from.X, to.X), MathF.Max(from.Z, to.Z));
            var first = MathF.Floor(bounds.MinZ / RowSize); var last = MathF.Floor(bounds.MaxZ / RowSize);
            if (!float.IsFinite(first) || !float.IsFinite(last) || first < int.MinValue || last >= int.MaxValue || last - first > 256)
                return Safe(_all, clock, bounds, start);
            if (!Safe(_broad, clock, bounds, start)) return false;
            for (var row = (int)first; row <= (int)last; ++row)
                if (_rows.TryGetValue(row, out var danger) && !Safe(danger, clock, bounds, start)) return false;
            return true;
        }

        private static bool Safe(Danger[] dangers, IDirectTraversalClock clock, GeometryBounds bounds, DateTime start)
        {
            foreach (var danger in dangers)
            {
                if (danger.FiniteBounds && (danger.Bounds.MinX > bounds.MaxX || danger.Bounds.MaxX < bounds.MinX
                    || danger.Bounds.MinZ > bounds.MaxZ || danger.Bounds.MaxZ < bounds.MinZ)) continue;
                foreach (var window in danger.Windows)
                    if (clock.Occupancy((window.DangerFrom - start).TotalSeconds,
                        window.DangerUntil is { } until ? (until - start).TotalSeconds : double.PositiveInfinity) is { } occupied
                        && (danger.Geometry.Contains(occupied.From) || danger.Geometry.Contains(occupied.To)
                            || danger.Geometry.IntersectsSegment(occupied.From, occupied.To))) return false;
            }
            return true;
        }

        private static bool Safe(Danger[] dangers, Vector3 from, Vector3 to, GeometryBounds bounds, DateTime start, DateTime end)
        {
            foreach (var danger in dangers)
            {
                if (danger.FiniteBounds && (danger.Bounds.MinX > bounds.MaxX || danger.Bounds.MaxX < bounds.MinX
                    || danger.Bounds.MinZ > bounds.MaxZ || danger.Bounds.MaxZ < bounds.MinZ)) continue;
                var active = false;
                foreach (var window in danger.Windows)
                    if (window.OverlapsInterval(start, end)) { active = true; break; }
                if (active && (danger.Geometry.Contains(from) || danger.Geometry.Contains(to) || danger.Geometry.IntersectsSegment(from, to)))
                    return false;
            }
            return true;
        }
    }

    internal bool CommitJumpRoute(Vector3 landing, Vector3 position, float speed, DateTime now)
    {
        if (_disposed || !_hasControl || _externalMovementEnabled() || _externalHasControl()
            || _dispatchedSolution is not { IsSuccess: true } || !OnLandingRoute(landing)) return false;
        if (!float.IsFinite(speed) || speed is < 4.8f or > 8.5f) return false;
        var snapshot = _manager.BuildSnapshot(now);
        var player = new PlayerState(position, 0, speed, false, false, 0, true, snapshot.Version);
        // A straight direct traversal was admitted with its own 0.2/0.1s
        // cushions. Revalidate it with that same contract at launch; the
        // general route's 0.5s end cushion otherwise rejects an already safe
        // stair run and forces a late standing restart before each riser.
        var direct = _directTarget == landing && _directMotion == null && _directPath.Count == 1
            && _directGeneration == _directDispatchedGeneration;
        if (direct
            ? !IsDirectSegmentSafe(snapshot, null, 0, position, landing, speed, now,
                now.AddSeconds(GeometryHelper.DistanceXZ(position, landing) / speed), .1)
            : !IsTemporalPhaseSafe(_dispatchedSolution, player, snapshot, now)) return false;
        _jumpLanding = landing; _jumpCommittedAt = now;
        return true;
    }

    internal void ClearJumpRoute() { _jumpLanding = null; _jumpCommittedAt = default; }

    private bool OnLandingRoute(Vector3 landing)
        => _greenMove?.GetStatus() is { IsRunning: true, CurrentPoint: { } current, FinalPoint: { } final }
            && GeometryHelper.DistanceXZ(current.Position, landing) <= .35f
            && GeometryHelper.DistanceXZ(final.Position, landing) <= .35f;

    private bool ContinueJumpRoute(DateTime now)
    {
        if (_jumpLanding is not { } landing) return false;
        if (!_hasControl || !_setting.EffectiveEnableAutoMovement || _externalMovementEnabled() || _externalHasControl()
            || now < _jumpCommittedAt || now - _jumpCommittedAt > TimeSpan.FromSeconds(1.2) || !OnLandingRoute(landing))
        { ClearJumpRoute(); return false; }
        // A jump cannot wait in midair for a newly calculated ground route.
        // The caller releases this bounded lease on landing or loss of capability.
        _phase = DispatchPhase.Moving;
        return true;
    }

    public void Reset()
    {
        _directTarget = null; _directUntil = default;
        _directDangerIndex = null;
        ClearJumpRoute();
        CancelPendingSolve();
        ReleaseControl();
        ReleaseMovementHandoff();
        _latestSolution = null;
        _dispatchedSolution = null;
        _lastUnsafeUtc = DateTime.MinValue;
        _lastSolveStartedAt = default;
        _lastSolveVersion = -1;
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
        ReleaseMovementHandoff();
        _greenMove = null;
        _latestSolution = null;
        _dispatchedSolution = null;
    }

    public void Update(PlayerState? currentPlayer, MovementCapability capability, DateTime now, bool allowMovement = true)
    {
        ClearEscapeWaitBlock();
        if (_disposed || _greenMove is null)
        {
            return;
        }

        // 功能关闭或玩家不存在：松手（仅当曾接管才 Stop），不解算、不下发。
        if (!_setting.EffectiveEnableSafeZone || currentPlayer is null)
        {
            Reset();
            return;
        }

        // 保留危险结束余量内的窗口，否则回收会让解算器提前认为刚判定的区域已经可进入。
        _manager.PruneExpiredAreas(now - TimeSpan.FromSeconds(MathF.Max(0f, _options.DangerEndSafetyTime)));

        var player = currentPlayer.Value;

        // 每帧解析一次快照：既供重解，也供 ShouldWaitForCast 读取最新危险区。
        var snapshot = _manager.BuildSnapshot(now);
        if (_setting.EffectiveEnableAutoMovement && allowMovement && !_externalMovementEnabled() && !_externalHasControl())
        {
            if (!PrepareMovementHandoff(snapshot, player, now, false))
            {
                ReleaseControl();
                return;
            }
        }
        else
        {
            ReleaseControl();
            ReleaseMovementHandoff();
        }
        if (allowMovement && ContinueJumpRoute(now)) return;
        CompletePendingSolve(snapshot);
        if (UpdateDirectTraversal(player, snapshot, now, allowMovement))
        {
            CompleteMovementHandoff(snapshot, now);
            return;
        }
        if (IsPointUnsafeNow(snapshot, player.Position, now))
        {
            _lastUnsafeUtc = now;
        }

        if (!IsSolutionValid(_latestSolution, snapshot, player, now))
        {
            if (CanSkipSolve(snapshot, player.Position, now)
                && !(_dispatchedSolution is { IsArenaReentry: true } returning
                    && IsTemporalPhaseSafe(returning, player, snapshot, now)))
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
        if (!_setting.EffectiveEnableAutoMovement || !allowMovement)
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
        // A temporary pocket is not completion of a timed mechanic goal. Moving
        // hazards can reopen its passage without changing the snapshot version.
        if (solution.IsPartialEscape && !GoalDirectedTemporalPlanning && IsTemporalPhaseSafe(solution, player, snapshot, now))
            return true;

        var ageSeconds = (now - solution.SolvedAt).TotalSeconds;

        // 周期重解：覆盖版本号不变时的限时/延迟危险窗口推进与跟随实体的危险区移动。
        if (ageSeconds > (_setting.RuntimePreferStraightPaths ? .25 : ResolveTtlSeconds))
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

    internal const double SupersedeIntervalSeconds = 0.15;
    private const float LastResortStartSafety = 0.05f;
    private string? _pendingGoalKey;
    private bool _pendingSupersedes;
    private DateTime _lastSupersedeAt;

    /// <summary>Active mechanic goals (priority &gt; 0; follow/uptime goals excluded) as a comparable key.</summary>
    internal static string MechanicGoalKey(SafeZoneSnapshot snapshot, DateTime now)
    {
        var key = new System.Text.StringBuilder();
        foreach (var goal in snapshot.GoalAreas)
            foreach (var window in goal.GoalWindows)
                if (window.Priority > 0 && window.IsActiveAt(now))
                {
                    key.Append(goal.Geometry.Position.X.ToString("F1")).Append(',')
                        .Append(goal.Geometry.Position.Z.ToString("F1")).Append(':').Append(window.Priority).Append(';');
                    break;
                }
        return key.ToString();
    }

    private void StartPendingSolve(
        SafeZoneSnapshot snapshot,
        PlayerState player,
        MovementCapability capability,
        DateTime solveTime,
        SafeZoneSolution? previousSolution)
    {
        var goalKey = MechanicGoalKey(snapshot, solveTime);
        var superseding = false;
        if (_pendingSolveTask is { IsCompleted: false })
        {
            // Moving hazards change the registry every frame. Let this bounded solve
            // finish, then validate its route against the newest snapshot before use.
            // A changed mechanic goal set is different: a solve started just before a new
            // goal (the ring pocket after the steel) can only return the stale answer and
            // holds a slow worker for the whole short window. Supersede it, rate-limited.
            // A superseding solve always completes: a goal that keeps moving must not starve the worker.
            if (goalKey == _pendingGoalKey || _pendingSupersedes || (solveTime - _lastSupersedeAt).TotalSeconds < SupersedeIntervalSeconds)
                return;
            _pendingSolveCts?.Cancel();
            _asyncSolveCancelledCount++;
            _pendingSolveTask = null;
            _pendingSolveCts = null;
            _lastSupersedeAt = solveTime;
            _lastSolveVersion = 0;
            superseding = true;
        }

        // Failed or rejected candidates have no usable TTL. Bound retries for
        // the same snapshot as well, instead of scheduling a worker every frame.
        var retryInterval = previousSolution is { IsSuccess: true } ? UrgentResolveTtlSeconds : ResolveTtlSeconds;
        if (snapshot.Version == _lastSolveVersion && solveTime >= _lastSolveStartedAt
            && (solveTime - _lastSolveStartedAt).TotalSeconds < retryInterval)
            return;

        DisposeCompletedPendingSolve();

        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _pendingSolveCts = cts;
        _pendingSolveSnapshotVersion = snapshot.Version;
        _pendingGoalKey = goalKey;
        _pendingSupersedes = superseding;
        _asyncSolveStartedCount++;
        _lastSolveStartedAt = solveTime;
        _lastSolveVersion = snapshot.Version;
        var options = CaptureSolveOptions();

        var queuedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        _pendingSolveTask = _scheduleSolve(() =>
        {
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            AsyncSolveResult Run()
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        return AsyncSolveResult.Cancelled(snapshot.Version);
                    }

                    var solution = _solver.Solve(snapshot, player, capability, options, solveTime, previousSolution);
                    return AsyncSolveResult.Completed(snapshot.Version, solution, token.IsCancellationRequested);
                }
                catch (Exception ex)
                {
                    return AsyncSolveResult.Failed(snapshot.Version, ex);
                }
            }
            var result = Run();
            result.QueuedAt = queuedAt;
            result.StartedAt = startedAt;
            result.FinishedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            return result;
        }, token);
    }

    private bool GoalDirectedTemporalPlanning => _setting.RuntimePreferStraightPaths || _setting.RuntimeGoalDirectedTemporalPlanning;

    private SolveOptions CaptureSolveOptions() => _options with
    {
        // Local terrain cells already test the character footprint. Eroding them
        // again by a full grid cell would remove ordinary dungeon doorways.
        ArenaSafetyMargin = _manager.UsesFallbackBoundary ? 0 : _setting.RuntimeArenaSafetyMargin ?? _options.ArenaSafetyMargin,
        HardSafetyMargin = _setting.RuntimeHardSafetyMargin ?? _options.HardSafetyMargin,
        PreferStraightPaths = _setting.RuntimePreferStraightPaths,
        PreferSafeRefuge = _setting.RuntimePreferSafeRefuge,
        GoalDirectedTemporalPlanning = GoalDirectedTemporalPlanning,
    };

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

        LastSolveQueueMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(result.QueuedAt, result.StartedAt).TotalMilliseconds;
        LastSolveWorkMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(result.StartedAt, result.FinishedAt).TotalMilliseconds;
        LastSolvePublishMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(result.FinishedAt).TotalMilliseconds;

        if (result.Exception is not null)
        {
            _asyncSolveDiscardedCount++;
            Svc.Log.Warning(result.Exception, "[MasterPlayer SafeZone] 后台解算失败。");
            return;
        }

        if (_disposed ||
            !_setting.EffectiveEnableSafeZone ||
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

    private void DispatchCore(SafeZoneSolution? solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (ContinueJumpRoute(now)) return;
        if (_externalMovementEnabled() || _externalHasControl())
        {
            _phase = DispatchPhase.YieldingToHost;
            ReleaseControl();
            return;
        }

        if (_hasControl && _greenMove?.IsRunning == true
            && _dispatchedSolution is { IsArenaReentry: true } returning
            && GeometryHelper.DistanceXZ(player.Position, returning.ExecutablePath[^1].Position) > returning.MaxArrivalTolerance
            && IsTemporalPhaseSafe(returning, player, snapshot, now))
        {
            _phase = DispatchPhase.Moving;
            return;
        }
        if (TryContinueTemporalPhase(player, snapshot, now)) return;

        if (solution is null || !solution.IsSuccess)
        {
            if (TryContinueDispatchedRoute(player, snapshot, now))
                return;
            _phase = DispatchPhase.Released;
            ReleaseControl();
            return;
        }

        // A new snapshot is a reason to validate, not to restart GreenMove. Keep
        // both its current waypoint and the worker's fresh result/TTL separately.
        var keepCurrentRoute = ShouldKeepCurrentRoute(solution, player, snapshot, now);
        // The solver can authorize exiting a hard obstacle spawned on the player.
        // Ordinary traversal rejects that initial overlap; retain that verified
        // escape unchanged only while its source geometry version still matches.
        var validateCandidate = solution.SourceSnapshotVersion != snapshot.Version || solution.UsesTemporalPlanning
            || !IsPointUnsafeNow(snapshot, player.Position, now)
            || !snapshot.HardObstacles.Any(o => o.Geometry.Contains(player.Position));

        if (!keepCurrentRoute && validateCandidate && !ReferenceEquals(solution, _dispatchedSolution))
        {
            var rebased = RebaseCandidate(solution, player.Position, now);
            if (ReferenceEquals(_latestSolution, solution)) _latestSolution = rebased;
            solution = rebased;
        }

        if (!keepCurrentRoute && validateCandidate && !IsTemporalPhaseSafe(solution, player, snapshot, now))
        {
            // 后台候选从旧位置起算，其前几个路点可能已在玩家身后。
            // 候选过期不等于正在执行的路线失效；从当前路点按最新危险窗复核后继续。
            if (TryContinueDispatchedRoute(player, snapshot, now))
                return;
            // A temporal phase without an intermediate wait has the same immediate escape
            // contract; retain its end wait and also validate movement startup below.
            if (!TryUseEmergencyRoute(ref solution, player, snapshot, now,
                allowTemporal: solution.UsesTemporalPlanning && solution.ExecutablePath.SkipLast(1).All(p => !p.IsWaitPoint)))
            {
                _latestSolution = null;
                _phase = DispatchPhase.Released;
                ReleaseControl();
                return;
            }
        }

        // The worker may now prefer another refuge (including its old start cell).
        // If the executing phase still validates, its waypoint/wait schedule owns
        // movement until arrival; a candidate's hold or cast wait must not stop it.
        if (keepCurrentRoute)
        {
            _phase = DispatchPhase.Moving;
            return;
        }

        if (solution.UsesTemporalPlanning && solution.ExecutablePath.Count > 0 &&
            solution.ExecutablePath[^1] is { IsWaitPoint: true } wait &&
            (solution.ExecutablePath.Count == 1 || GeometryHelper.DistanceXZ(player.Position, wait.Position) <= MinArrivalTolerance))
        {
            RememberTemporalContinuation(solution, snapshot);
            if (_phase != DispatchPhase.HoldingForDeparture)
            {
                // 首次接管可能已有跟随路线；用当前位置替换路线后才停止，确保停的是自己拥有的移动。
                if (!_hasControl)
                {
                    if (!PrepareMovementHandoff(snapshot, player, now, true))
                    {
                        ReleaseControl();
                        return;
                    }
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
        if (shouldMove && !solution.IsArenaMarginRecovery && !solution.IsArenaReentry
            && (PreferTimedRefuge(snapshot) || !solution.UsesTemporalPlanning || player.CastTimeRemaining <= ShortCastSeconds)
            && ShouldWaitForCast(solution, player, snapshot, now))
        {
            _phase = DispatchPhase.WaitingForCast;
            shouldMove = false;
        }
        else if (shouldMove && !solution.IsArenaMarginRecovery && !solution.IsArenaReentry &&
            (CanHoldSafeRefuge(solution, player, snapshot, now) || ShouldWaitForEscape(solution, player, snapshot, now)))
        {
            _phase = DispatchPhase.WaitingForEscape;
            shouldMove = false;
        }

        // 玩家真正无所事事时松手，避免膨胀环里的视觉抖动。
        if (shouldMove && !solution.IsArenaReentry && CanSkipSolve(snapshot, player.Position, now))
        {
            _phase = DispatchPhase.Released;
            shouldMove = false;
        }

        // run1904-b4: an escape route out of a pending puddle was released half-way because the next
        // solve (with the new staging goal) found no full temporal plan. A failed re-plan is not evidence
        // that the running escape is unsafe; keep it while it still validates against the latest dangers.
        if (!shouldMove && solution.Status is SolutionStatus.NoTemporalPath or SolutionStatus.NoReachableSafeCell
            && SafeZoneSolver.HasPendingDangerAtPlayer(snapshot, player.Position, now) && TryContinueDispatchedRoute(player, snapshot, now))
            return;

        if (!shouldMove)
        {
            if (_phase is not (DispatchPhase.WaitingForCast or DispatchPhase.WaitingForEscape))
            {
                _phase = DispatchPhase.Released;
            }

            ReleaseControl();
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
            var queue = BuildGreenMovePoints(solution, snapshot, now);
            if (!ValidateTerrainPath(player.Position, queue))
            {
                _latestSolution = null;
                _phase = DispatchPhase.Released;
                ReleaseControl();
                return;
            }
            if (!PrepareMovementHandoff(snapshot, player, now, true))
            {
                ReleaseControl();
                return;
            }
            _greenMove!.MoveTo(queue);
            _dispatchedSolution = solution;
            // GreenMove now runs the solver's route; a rejected shortcut must not withdraw it.
            _directDispatchedGeneration = -1;
            RememberTemporalContinuation(solution, snapshot);
            _dispatchCount++;
            _lastDispatchUtc = now;
        }

        _phase = DispatchPhase.Moving;
        _hasControl = true;
    }

    private bool ValidateTerrainPath(Vector3 from, IReadOnlyList<GreenMovePoint> points)
    {
        if (CheckTerrainTraversal == null) return true;
        var remaining = 2f;
        foreach (var point in points)
        {
            var delta = point.Position - from;
            var distance = GeometryHelper.DistanceXZ(from, point.Position);
            if (distance < .001f) continue;
            var end = distance > remaining ? from + delta * (remaining / distance) : point.Position;
            if (!CheckTerrainTraversal(from, end)) return false;
            remaining -= distance;
            if (remaining <= 0) break;
            from = end;
        }
        return true;
    }

    private bool TryContinueDispatchedRoute(PlayerState player, SafeZoneSnapshot snapshot, DateTime now)
    {
        if (!_hasControl || _greenMove is not { IsRunning: true } ||
            _dispatchedSolution is not { IsSuccess: true } previous)
            return false;
        if (!IsTemporalPhaseSafe(previous, player, snapshot, now)
            && !TryUseEmergencyRoute(ref previous, player, snapshot, now,
                allowTemporal: GoalDirectedTemporalPlanning && !previous.ExecutablePath.Any(p => p.IsWaitPoint))
            // An escape already under way out of a pending hit is never improved by stopping in it: keep it
            // while it still clears the hit itself (spatial margins intact), even past the start cushion.
            // (Gargoyle ring regression: released 1.6y short of the pocket 0.5s before the ring.)
            && !(SafeZoneSolver.HasPendingDangerAtPlayer(snapshot, player.Position, now)
                && IsTemporalPhaseSafe(previous, player, snapshot, now, LastResortStartSafety)))
            return false;

        _phase = DispatchPhase.Moving;
        return true;
    }

    private static SafeZoneSolution RebaseCandidate(SafeZoneSolution solution, Vector3 position, DateTime now)
    {
        var path = solution.ExecutablePath;
        var next = 1;
        var closest = 0.75f * 0.75f;
        var p = new Vector2(position.X, position.Z);
        // Only discard a prefix already travelled. The current position must be
        // on that polyline; the full remaining route is checked against the latest
        // geometry and time windows below. Never skip a planned waiting phase.
        for (var i = 0; i + 1 < path.Count && !path[i].IsWaitPoint; ++i)
        {
            var a = new Vector2(path[i].Position.X, path[i].Position.Z);
            var b = new Vector2(path[i + 1].Position.X, path[i + 1].Position.Z);
            var delta = b - a;
            if (delta.LengthSquared() < 0.000001f) continue;
            var along = Math.Clamp(Vector2.Dot(p - a, delta) / delta.LengthSquared(), 0, 1);
            var distance = Vector2.DistanceSquared(p, a + along * delta);
            if (distance + 0.000001f >= closest) continue;
            closest = distance;
            next = i + 1;
        }
        if (next <= 1) return solution;
        var remaining = new TimedWaypoint[path.Count - next + 1];
        remaining[0] = TimedWaypoint.Pass(position, now);
        for (var i = next; i < path.Count; ++i) remaining[i - next + 1] = path[i];
        return solution.WithExecutablePath(remaining);
    }

    private bool TryUseEmergencyRoute(ref SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, bool allowTemporal = false)
    {
        const float emergencyStartSafety = 0.25f;
        if (solution.UsesTemporalPlanning && !allowTemporal || solution.ExecutablePath.Count < 2 || solution.ReducedDangerStartSafetyTime.HasValue
            || _options.DangerStartSafetyTime <= emergencyStartSafety
            || !SafeZoneSolver.HasPendingDangerAtPlayer(snapshot, player.Position, now)
            || !IsTemporalPhaseSafe(solution, player, snapshot, now, emergencyStartSafety)
            || allowTemporal && !(_hasControl && _greenMove is { IsRunning: true })
                && !IsTemporalPhaseSafe(solution, player, snapshot, now, emergencyStartSafety, startDelay: .1))
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

    private bool IsTemporalPhaseSafe(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, float? startSafety = null, double startDelay = 0)
    {
        startSafety ??= solution.ReducedDangerStartSafetyTime;
        var path = solution.ExecutablePath;
        if (solution.IsArenaReentry)
            return path.Count is > 0 and <= 2 && path.All(p => !p.IsWaitPoint)
                && ArenaReentryRecovery.IsSafe(snapshot, player.Position, path[^1].Position, now,
                    player.CurrentSpeed, _options, solution.MaxArrivalTolerance, startDelay);
        if (solution.IsArenaMarginRecovery)
            return path.Count is > 0 and <= 2 && path.All(p => !p.IsWaitPoint)
                && ArenaMarginRecovery.IsSafe(snapshot, player.Position, path[^1].Position, now,
                    player.CurrentSpeed, _options, solution.MaxArrivalTolerance, startDelay);
        if (path.Count == 0)
            return IsTemporalSegmentSafe(snapshot, player.Position, player.Position, now, now, startSafety);
        if (path.Count == 1 && GeometryHelper.DistanceXZ(player.Position, path[0].Position) > MinArrivalTolerance)
            return false;

        var nextIndex = 1;
        if (ReferenceEquals(solution, _dispatchedSolution) && _greenMove is not null)
            nextIndex = Math.Clamp(_greenMove.GetStatus().CurrentIndex + 1, 1, path.Count);
        var position = player.Position;
        var time = now.AddSeconds(startDelay);
        if (startDelay > 0 && !IsTemporalSegmentSafe(snapshot, position, position, now, time, startSafety)) return false;
        // An escape may begin inside persistent damage or an obstacle. Permit only
        // the initial exit from those shapes, never re-entry or an unsafe endpoint.
        HashSet<IGeometryShape>? escaping = null;
        if (!solution.UsesTemporalPlanning || solution.ExitsInitialPersistentDanger)
        {
            escaping = [];
            foreach (var obstacle in snapshot.HardObstacles)
                if (obstacle.Geometry.Contains(position)) escaping.Add(obstacle.Geometry);
            foreach (var danger in snapshot.DangerAreas)
                if (danger.Geometry.Contains(position))
                    foreach (var window in danger.DangerWindows)
                        if (window.IsActiveAt(now)) { escaping.Add(danger.Geometry); break; }
        }
        else if (_setting.RuntimePreferSafeRefuge)
        {
            foreach (var danger in snapshot.DangerAreas)
                if (danger.Geometry.Contains(position)
                    && danger.DangerWindows.Any(w => w.HasExplicitTiming && w.HasMotionEnvelope && !w.RequireOutwardExit && w.IsActiveAt(now)))
                    (escaping ??= []).Add(danger.Geometry);
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
        // A shortcut can arrive before the safe interval used by the worker.
        // Standing there must remain safe until the original arrival, otherwise
        // removing a detour turns a timed escape into an early hit.
        var holdUntil = solution.UsesTemporalPlanning && end.PlannedArrivalTime > time ? end.PlannedArrivalTime : time;
        // Moving-hazard refuges also need a full second of safe standing time.
        if (_setting.RuntimePreferSafeRefuge && !solution.IsLeastDangerFallback
            && snapshot.DangerAreas.Any(d => d.DangerWindows.Any(w => w.HasExplicitTiming && w.HasMotionEnvelope))
            && !snapshot.GoalAreas.Any(g => g.GoalWindows.Any(w => w.Priority >= 0 && w.IsActiveAt(now)))
            && holdUntil < time.AddSeconds(1))
            holdUntil = time.AddSeconds(1);
        return IsTemporalSegmentSafe(snapshot, position, position, time, holdUntil, startSafety);
    }

    private bool IsTemporalSegmentSafe(SafeZoneSnapshot snapshot, Vector3 from, Vector3 to, DateTime start, DateTime end, float? startSafety = null, HashSet<IGeometryShape>? escaping = null, float? endSafety = null)
    {
        if (snapshot.ArenaBoundary is { } boundary && !IsArenaSegmentSafe(boundary, from, to))
            return false;
        foreach (var obstacle in snapshot.HardObstacles)
        {
            if (obstacle.Geometry.Contains(from) || obstacle.Geometry.Contains(to) || obstacle.Geometry.IntersectsSegment(from, to))
            {
                if (escaping?.Contains(obstacle.Geometry) != true
                    || !ArenaBoundaryTraversal.AllowsInitialExit(obstacle.Geometry, from, to)) return false;
                if (!obstacle.Geometry.Contains(to)) escaping.Remove(obstacle.Geometry);
            }
        }
        foreach (var danger in snapshot.DangerAreas)
        {
            if (!danger.Geometry.Contains(from) && !danger.Geometry.Contains(to) && !danger.Geometry.IntersectsSegment(from, to))
                continue;
            foreach (var window in danger.DangerWindows)
            {
                if (window.RequireOutwardExit && window.IsActiveAt(start) && danger.Geometry is CircleGeometry circle
                    && circle.Contains(from) && GeometryHelper.DistanceSquaredPointToSegmentXZ(circle.Position, from, to)
                        + .0001f < GeometryHelper.DistanceSquaredXZ(circle.Position, from))
                    return false;
                var buffered = window.Buffered(startSafety ?? _options.DangerStartSafetyTime, endSafety ?? _options.DangerEndSafetyTime);
                if (buffered.OverlapsInterval(start, end))
                {
                    if (escaping == null)
                    {
                        var duration = (end - start).TotalSeconds;
                        var a = duration <= 0 ? from : Vector3.Lerp(from, to, (float)Math.Clamp((buffered.DangerFrom - start).TotalSeconds / duration, 0, 1));
                        var b = duration <= 0 ? to : Vector3.Lerp(from, to, buffered.DangerUntil is { } until ? (float)Math.Clamp((until - start).TotalSeconds / duration, 0, 1) : 1);
                        if (danger.Geometry.Contains(a) || danger.Geometry.Contains(b) || danger.Geometry.IntersectsSegment(a, b)) return false;
                        continue;
                    }
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
        // Composite contours can include internal edges covered by another shape.
        // Check every interval between crossings against the actual containment rule.
        return ArenaBoundaryTraversal.ContainsSegment(boundary, from, to);
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
            // An idle candidate only proves safety at the worker's sampled instant.
            // Do not stop an ongoing escape inside a pending hit while its full
            // remaining route still validates against the current snapshot.
            // Goal-directed mechanics can deliberately publish an idle hold;
            // their timed phases are handled by TryContinueTemporalPhase.
            return newSolution.IsSuccess && !GoalDirectedTemporalPlanning
                && _dispatchedSolution.TargetPosition is { } destination
                && ActiveGoalPriorityAt(snapshot, player.Position, now) <= ActiveGoalPriorityAt(snapshot, destination, now)
                && SafeZoneSolver.HasPendingDangerAtPlayer(snapshot, player.Position, now)
                && TryContinueDispatchedRoute(player, snapshot, now);
        }

        // A worker can decide to remain in its start cell while an earlier
        // route keeps moving. Publishing that obsolete idle cell must not turn
        // into a command to walk back after the player has already left it.
        var candidatePath = newSolution.ExecutablePath;
        var cellRadius = _options.GridResolution * .7071068f;
        if (newSolution.SourceSnapshotVersion != snapshot.Version
            && !newSolution.UsesTemporalPlanning && !_dispatchedSolution.UsesTemporalPlanning
            && candidatePath.Count == 2 && candidatePath.All(p => !p.IsWaitPoint)
            && GeometryHelper.DistanceXZ(candidatePath[0].Position, candidatePath[1].Position) <= cellRadius
            && GeometryHelper.DistanceXZ(player.Position, candidatePath[1].Position) > _options.GridResolution
            && ActiveGoalPriorityAt(snapshot, candidatePath[1].Position, now) == int.MinValue
            && snapshot.GoalAreas.Any(g => g.GoalWindows.Any(w => w.IsActiveAt(now)))
            && TryContinueDispatchedRoute(player, snapshot, now))
            return true;

        // Do not reverse a safe ongoing return for an older temporary pocket
        // with lower goal priority. Recheck the route against the live snapshot.
        if (!_dispatchedSolution.UsesTemporalPlanning
            && _greenMove.GetStatus() is { IsRunning: true, FinalPoint: { } ongoing }
            && newSolution.TargetPosition is { } candidate
            && ActiveGoalPriorityAt(snapshot, ongoing.Position, now) > ActiveGoalPriorityAt(snapshot, candidate, now)
            && !IsPointUnsafeNow(snapshot, ongoing.Position, now)
            && TryContinueDispatchedRoute(player, snapshot, now))
            return true;

        if (!(newSolution.UsesTemporalPlanning && _dispatchedSolution.UsesTemporalPlanning) &&
            newSolution.SourceSnapshotVersion != _dispatchedSolution.SourceSnapshotVersion &&
            (newSolution.TargetPosition is not { } target || _dispatchedSolution.TargetPosition is not { } previousTarget ||
             GeometryHelper.DistanceXZ(target, previousTarget) > MinArrivalTolerance))
        {
            return false;
        }

        if (newSolution.UsesTemporalPlanning || _dispatchedSolution.UsesTemporalPlanning)
        {
            // A new grid cell/refuge is only an alternative. Changing it every
            // solve resets GreenMove's progress and can send the player backward.
            // Live safety and active goal priority below still invalidate the route.
            if (newSolution.UsesTemporalPlanning != _dispatchedSolution.UsesTemporalPlanning ||
                _dispatchedSolution.ExecutablePath.Any(p => p.IsWaitPoint) && IsSolutionPlanOverdue(_dispatchedSolution, player.Position, now))
                return false;
        }

        var status = _greenMove.GetStatus();
        if (!status.IsRunning || status.FinalPoint is not { } finalPoint)
        {
            return false;
        }

        if (newSolution.TargetPosition is { } requested &&
            ActiveGoalPriorityAt(snapshot, requested, now) > ActiveGoalPriorityAt(snapshot, finalPoint.Position, now))
            return false;

        return (_dispatchedSolution.UsesTemporalPlanning || !IsPointUnsafeNow(snapshot, finalPoint.Position, now)) &&
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

    private static int ActiveGoalPriorityAt(SafeZoneSnapshot snapshot, Vector3 point, DateTime now)
    {
        var priority = int.MinValue;
        foreach (var goal in snapshot.GoalAreas)
            if (goal.Geometry.Contains(point))
                foreach (var window in goal.GoalWindows)
                    if (window.IsActiveAt(now)) priority = Math.Max(priority, window.Priority);
        return priority;
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
        // A short correction (e.g. back inside the eroded arena after a knockback) must not already count as
        // arrived: GreenMove would stop at once and each re-plan restarts it (B26 replay: 14s frozen 0.4y from it).
        tolerance = MathF.Min(tolerance, MathF.Max(MinArrivalTolerance, GeometryHelper.DistanceXZ(path[0].Position, destination) * 0.5f));
        foreach (var danger in snapshot.DangerAreas)
        {
            if (danger.Geometry is not CircleGeometry circle
                || !danger.DangerWindows.Any(w => w.DangerUntil is null
                    || w.DangerUntil.Value > now.AddSeconds(-_options.DangerEndSafetyTime)))
                continue;
            var clearance = GeometryHelper.DistanceXZ(circle.Position, destination) - circle.Radius;
            // A safe endpoint does not make its arrival disk safe. Ordinary escape
            // and least-danger routes may end closer to a circle than 0.5y (or even
            // 0.1y). Let GreenMove finish outside the hit instead of stopping in it.
            if (clearance > 0f)
                tolerance = MathF.Min(tolerance, clearance * .9f);
        }
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
        var delay = player.CastTimeRemaining - _options.CastSlideWindow + DepartureReserveSeconds;
        if (PreferTimedRefuge(snapshot) && solution.IsSuccess && !solution.ReducedDangerStartSafetyTime.HasValue
            && !solution.IsLeastDangerFallback && solution.ExecutablePath.Count >= 2
            && IsTemporalPhaseSafe(solution, player, snapshot, now, startDelay: delay + .35))
            return true;
        return CanDelayEscape(solution, player, snapshot, now, delay) || CanFinishShortCast(solution, player, snapshot, now, delay);
    }

    // run1896: a 1s whistle cast was cut 5s before the hit because the route was temporal or a
    // mechanic goal was active, which CanDelayEscape refuses. For short casts only, wait when the
    // whole route still finishes well before the first hit on the player's current spot.
    internal const float ShortCastSeconds = 1.2f, ShortCastSlackSeconds = 1.5f;
    private bool CanFinishShortCast(SafeZoneSolution solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now, double delay)
    {
        if (player.CastTimeRemaining > ShortCastSeconds || !solution.IsSuccess || solution.ExecutablePath.Count < 2
            || player.CurrentSpeed <= 0.1f || IsPointUnsafeNow(snapshot, player.Position, now))
            return false;
        DateTime? firstHit = null;
        foreach (var danger in snapshot.DangerAreas)
            foreach (var window in danger.DangerWindows)
                if (window.DangerFrom > now && window.DangerFrom != DateTime.MaxValue && danger.Geometry.Contains(player.Position)
                    && (firstHit is not { } f || window.DangerFrom < f))
                    firstHit = window.DangerFrom;
        if (firstHit is not { } hit)
            return false;
        var length = 0f;
        for (var i = 1; i < solution.ExecutablePath.Count; ++i)
            length += GeometryHelper.DistanceXZ(solution.ExecutablePath[i - 1].Position, solution.ExecutablePath[i].Position);
        var arrival = now.AddSeconds(delay + length / player.CurrentSpeed + ShortCastSlackSeconds + _options.DangerStartSafetyTime);
        return arrival < hit;
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
        _temporalContinuation = null;
        _temporalGoals = [];
        _directSuffixAmendment = null;
        _directInputAcquired = false;
        _directPhysicalTail = null;
        ClearJumpRoute();
        if (!_hasControl)
        {
            return;
        }

        // 宿主安全区可能已在本帧替换路线，此时只撤销自己的控制记录。
        // Naturally completed queues need no additional native stop.
        if (!_externalHasControl() && _greenMove?.IsRunning == true)
            _greenMove.Stop();
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
        public long QueuedAt { get; set; }
        public long StartedAt { get; set; }
        public long FinishedAt { get; set; }

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
