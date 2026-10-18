using System.Numerics;
using System.Diagnostics;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleGraphPlanner
{
    internal sealed record Step(int Node, Vector3 Position, DateTime Arrival, DateTime Departure)
    {
        internal DateTime LatestDeparture { get; init; }
    }
    internal sealed record Result(List<Step> Steps, int Expanded);
    internal sealed record SearchInfo(int Expanded, bool Limited);
    private sealed record SearchOutcome(Result? Plan, double Milliseconds, SearchInfo Info, int Hazards);
    private sealed record Ahead(int Node, DateTime Arrival, Vector3 Position, float Speed, Task<SearchOutcome> Work,
        CancellationTokenSource Cancel, long Requested, bool Cold)
    {
        internal bool Sampled { get; set; }
    }
    private sealed record Label(int Node, double From, double Until, double ArrivalFrom, double ArrivalUntil,
        double Travel, Label? Previous, double GoalTime = double.NaN);
    private BlundervilleFinalRouteSettings? _settings;
    private BlundervilleRouteGraph.Compiled? _graph;
    private readonly BlundervilleRacePlanner _tailRace = new();
    private long _round = -1;
    private int? _at, _to;
    private int _next, _stretchStart;
    internal float? ExitLane { get; private set; }
    private Vector3[]? _stretch;
    private Result? _plan;
    private Step? _exitStep;
    private Task<SearchOutcome>? _pending;
    private Ahead? _ahead;
    private CancellationTokenSource? _cancel;
    private DateTime _last, _retry, _requested, _reconsider;
    private Vector3 _requestedPosition;
    private float _speed;
    private readonly BlundervillePlanningBudget _planningBudget = new();
    private long _requestedClock;
    private bool _searchStarted, _pendingCold, _pendingSampled;
    private double _pollSeconds = .1;
    internal Task? Pending => _pending ?? _ahead?.Work;
    internal string Status { get; private set; } = "等待路网";
    internal string LastInvalidation { get; private set; } = "";
    internal string Diagnostics { get; private set; } = "路网尚未计算";
    internal int? CurrentNode => _at;
    internal int? NextNode => _to;
    internal Result? Plan => _plan;

    internal void ConfigureCourse(BlundervilleFinalRouteSettings settings)
    {
        if (ReferenceEquals(settings, _settings)) return;
        Reset(); _settings = settings; _graph = settings.AssignedGraph()?.Compile();
    }

    private void ClearPlan(bool keepAhead = false)
    {
        _cancel?.Cancel(); _cancel?.Dispose(); _cancel = null;
        _pending = null; _plan = null; _stretch = null; _next = 0;
        if (!keepAhead) { _ahead?.Cancel.Cancel(); _ahead?.Cancel.Dispose(); _ahead = null; }
    }

    internal void Reset()
    {
        ClearPlan(); _at = _to = null; ExitLane = null;
        _exitStep = null;
        _round = -1; _last = _retry = default; _tailRace.Reset();
    }

    private BlundervilleRaceDecision Hold(Vector3 p, string message)
    {
        Status = message; return new(p, true, false, null, message);
    }

    internal BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, DirectTraversalExecution? execution = null)
    {
        if (now < _last || _round != state.RoundVersion) { Reset(); _round = state.RoundVersion; }
        if (now > _last && _last != default) _pollSeconds = Math.Clamp((now - _last).TotalSeconds, 1d / 240, .1);
        _last = now;
        if (state.Course != BlundervilleCourse.Final || !state.IsRacing || state.Qualified) { Reset(); return normal; }
        if (_graph == null) return Hold(player, "请先保存有效路网；不会自动生成路线");
        if (!state.CanMove(now) || normal.Hold && normal.Facing != null) { ClearPlan(); return normal; }
        if (_at == null && !Attach(player))
        {
            // Spawn slots are laterally offset from the authored start (the
            // 19:43 recording starts at X=0.5). Join only that start, on the
            // start platform; this never connects different course branches.
            var start = _graph.Positions[_graph.Start];
            var entryIndex = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
            var duration = Distance(player, start) / state.NavigationBaseSpeed(now);
            if (MathF.Abs(player.Z - StartZ) <= 1 && player.Y > 2 && Link(player, start)
                && entryIndex.Segment(player, start, 0, duration) && entryIndex.Stand(start, duration, duration + 1))
            {
                Status = "从出生位置接入路网起点";
                return new(start, false, false, null, Status, true) { ContinuousPath = [start] };
            }
            return Hold(player, "请手动走到路网节点或连线上；不会自动补连接");
        }
        if (ExitLane != null) return Tail(normal, state, player, now);
        if (player.Y <= 2 || !Floor.Contains(player, 0)) { ClearPlan(); return Hold(player, "已离开坡道，请手动回到当前连线"); }
        if (_plan == null && _to is { } arriving && Distance(player, _graph.Positions[arriving]) < .03f)
        { _at = arriving; _to = null; ClearPlan(); _retry = default; }

        if (_plan != null)
        {
            var consumed = _next;
            if (_stretch != null && execution is { HasControl: true } && execution.Path.SequenceEqual(_stretch))
                consumed = Math.Max(consumed, Math.Min(_plan.Steps.Count, _stretchStart + execution.CurrentIndex));
            while (_next < _plan.Steps.Count && (_next < consumed || Distance(player, _plan.Steps[_next].Position) < .03f))
            {
                var reached = _plan.Steps[_next++]; _at = reached.Node; _to = null;
                if (_graph.Exits.Contains(reached.Node)) { _exitStep = reached; ClearPlan(); return AtExit(normal, state, hazards, player, now); }
            }
            if (_plan != null && _next == _plan.Steps.Count) { ClearPlan(keepAhead: true); _retry = default; }
            if (_plan != null) _to = _plan.Steps[_next].Node;
            if (_stretch != null && _next >= _stretchStart + _stretch.Length) _stretch = null;
        }
        if (!OnCurrentEdge(player)) { ClearPlan(); return Hold(player, "已偏离当前连线，请手动回线；不能跳到另一分支"); }
        if (_graph.Exits.Contains(_at!.Value)) return AtExit(normal, state, hazards, player, now);
        var speed = state.NavigationBaseSpeed(now);
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        if (_ahead is { Sampled: false, Work.IsCompleted: true } ready)
        {
            if (ready.Work.IsCompletedSuccessfully && ready.Work.Result.Plan is { } candidate)
                _ = Rebase(candidate, ready.Position, ready.Arrival, ready.Speed,
                    new BlundervilleFinalPlanner.HazardIndex(hazards, ready.Arrival, BlundervilleFinalPlanner.Final));
            _ = ready.Work.Exception;
            _planningBudget.Observe(Stopwatch.GetElapsedTime(ready.Requested).TotalSeconds, ready.Cold);
            ready.Sampled = true;
        }
        if (_plan == null && _pending == null && _ahead is { } ahead)
        {
            _ahead = null;
            if (_at == ahead.Node && _to == null && MathF.Abs(speed - ahead.Speed) < .01f)
            {
                _pending = ahead.Work; _cancel = ahead.Cancel; _requested = ahead.Arrival;
                _requestedPosition = ahead.Position; _speed = ahead.Speed;
                _requestedClock = ahead.Requested; _pendingCold = ahead.Cold; _pendingSampled = ahead.Sampled;
            }
            else { ahead.Cancel.Cancel(); ahead.Cancel.Dispose(); }
        }
        if (_plan != null && _plan.Steps[_next - 1].Departure > now.AddSeconds(1) && now >= _reconsider
            && _to != null && Distance(player, _graph.Positions[_at.Value]) <= .03f)
        {
            ClearPlan(); _to = null; _retry = default;
        }
        if (_plan != null)
        {
            var priorStep = _plan.Steps[_next - 1];
            var leg = Distance(priorStep.Position, _plan.Steps[_next].Position);
            var progress = Math.Clamp(leg - Distance(player, _plan.Steps[_next].Position), 0, leg);
            var restartBudget = (Latest(priorStep).AddSeconds(progress / speed) - now).TotalSeconds;
            var following = _stretch != null && execution is { HasControl: true, Rejected: false }
                && execution.Path.SequenceEqual(_stretch);
            var remainingIndex = following
                ? index.WithExecutionLag(0) : index.WithExecutionLag(Math.Clamp(restartBudget, .1, BlundervilleFinalPlanner.Lag));
            var safe = RemainingSafe(_plan, _next, player, now, speed, index, remainingIndex, out var failure);
            if (execution is { Rejected: true } || MathF.Abs(speed - _speed) > .01f || !safe
                || !following && restartBudget < .1)
            {
                LastInvalidation = $"{now:HH:mm:ss.fff} node={_at}->{_to} {failure} rejected={execution?.Rejected}";
                ClearPlan();
            }
        } // _at/_to retain the committed edge even when its timing fails.
        if (_plan == null && _pending is { IsCompleted: true })
        {
            var done = _pending; _pending = null;
            var result = done.IsCompletedSuccessfully ? done.Result.Plan : null; _ = done.Exception;
            Diagnostics = done.IsCompletedSuccessfully
                ? $"预测{done.Result.Hazards}个危险窗，路网计算{done.Result.Milliseconds:F1}ms，展开{done.Result.Info.Expanded}，"
                    + (result != null ? "已出解" : done.Result.Info.Limited ? "达到搜索上限" : "16秒视窗内暂无安全连通路线")
                : "路网计算失败";
            _cancel?.Dispose(); _cancel = null;
            if (result != null && now - _requested < TimeSpan.FromSeconds(3)
                && Distance(player, _requestedPosition) <= Math.Max(.4f, speed * .5f) && MathF.Abs(speed - _speed) < .01f)
                _plan = Rebase(result, player, now, speed, index);
            if (!_pendingSampled)
            {
                _planningBudget.Observe(Stopwatch.GetElapsedTime(_requestedClock).TotalSeconds, _pendingCold);
                _pendingSampled = true;
            }
            Diagnostics += $"，端到端预算{_planningBudget.ReserveSeconds * 1000:F1}ms，提前{_planningBudget.LeadSeconds(_planningBudget.Samples == 0, _pollSeconds):F2}s";
            if (_plan != null) { _next = 1; _to = _plan.Steps[1].Node; _reconsider = now.AddSeconds(1); }
            else _retry = now.AddSeconds(.25);
        }
        if (_plan == null)
        {
            if (_pending == null && now >= _retry)
            {
                // A rejected departure has not committed an edge while still
                // at the node; all explicitly connected successors remain eligible.
                if (Distance(player, _graph.Positions[_at.Value]) <= .03f) _to = null;
                _requestedClock = Stopwatch.GetTimestamp(); _pendingCold = !_searchStarted; _searchStarted = true; _pendingSampled = false;
                var graph = _graph; var at = _at.Value; var to = _to; var captured = hazards.ToArray();
                _requested = now; _requestedPosition = player; _speed = speed;
                _cancel = new(); var token = _cancel.Token;
                _pending = Search(graph, at, to, captured, player, now, speed, token);
            }
            // A changed forecast farther ahead must not stop the player inside
            // the current mechanism. Finish only the already committed edge,
            // with fresh segment/arrival checks, while its successors are planned.
            if (_to is { } continuing && Distance(player, _graph.Positions[_at.Value]) > .03f)
            {
                var target = _graph.Positions[continuing]; var duration = Distance(player, target) / speed;
                // Already-running input has no new acquisition delay. The
                // hazard lead/tail margins and per-frame runtime check remain.
                var movingIndex = execution is { HasControl: true, Rejected: false }
                    ? new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final with { ExecutionLag = 0 }) : index;
                if (movingIndex.Segment(player, target, 0, duration) && index.Stand(target, duration, duration + .5))
                {
                    Status = $"沿已选连线 {_at} → {_to}，更新后续分支";
                    return new(target, false, false, null, Status, true) { ContinuousPath = [target] };
                }
            }
            return Hold(player, _pending != null ? $"正在计算节点{_at}的连通路线"
                : index.Stand(player, 0, 1.5) ? $"节点{_at}：连通分支暂无安全窗口" : "当前连通分支暂不可走，站位有危险，请手动处理");
        }
        var previous = _plan.Steps[_next - 1];
        if (previous.Departure > now && previous.Departure - previous.Arrival > TimeSpan.FromMilliseconds(1))
        { _stretch = null; return Hold(player, $"节点{_at}：等待 {(previous.Departure - now).TotalSeconds:F1} 秒"); }
        if (_stretch == null)
        {
            var end = _next;
            while (end < _plan.Steps.Count - 1 && _plan.Steps[end].Arrival == _plan.Steps[end].Departure) ++end;
            _stretchStart = _next;
            _stretch = _plan.Steps.Skip(_next).Take(end - _next + 1).Select(s => s.Position).ToArray();
        }
        Status = $"沿路网：{_at} → {_to}";
        var endStep = _plan.Steps[^1];
        if (_ahead == null && !_graph.Exits.Contains(endStep.Node) && endStep.Arrival > now
            && (endStep.Arrival - now).TotalSeconds <= _planningBudget.LeadSeconds(_planningBudget.Samples == 0, _pollSeconds))
        {
            var requested = Stopwatch.GetTimestamp(); var cold = !_searchStarted; _searchStarted = true;
            var cancel = new CancellationTokenSource();
            _ahead = new(endStep.Node, endStep.Arrival, endStep.Position, speed,
                Search(_graph, endStep.Node, null, hazards.ToArray(), endStep.Position, endStep.Arrival, speed, cancel.Token), cancel, requested, cold);
        }
        return new(_stretch[^1], false, false, null, Status, true) { ContinuousPath = _stretch };
    }

    private static Task<SearchOutcome> Search(BlundervilleRouteGraph.Compiled graph, int at, int? to,
        BlundervilleHazard[] hazards, Vector3 p, DateTime now, float speed, CancellationToken token)
        => Task.Run(() =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); var info = new SearchInfo(0, false);
            var plan = Find(graph, at, to, hazards, p, now, speed, token, value => info = value);
            return new SearchOutcome(plan, watch.Elapsed.TotalMilliseconds, info, hazards.Length);
        });

    private bool Attach(Vector3 player)
    {
        var node = _graph!.Positions.OrderBy(p => Distance(p.Value, player)).First();
        if (Distance(node.Value, player) <= .4f) { _at = node.Key; return true; }
        if (BlundervilleStairGeometry.OnStairs(player) && player.Z <= EndZ)
            foreach (var exit in _graph!.Exits)
            {
                var x = _graph.Positions[exit].X;
                if (MathF.Sign(x) != MathF.Sign(player.X)) continue;
                _at = exit; CommitExit(x); return true;
            }
        var best = .4f * .4f;
        foreach (var (from, targets) in _graph.Next)
            foreach (var to in targets)
            {
                var d = GeometryHelper.DistanceSquaredPointToSegmentXZ(player, _graph.Positions[from], _graph.Positions[to]);
                if (d >= best) continue;
                best = d; _at = from; _to = to;
            }
        return _at != null;
    }

    private bool OnCurrentEdge(Vector3 player) => _to is { } to
        ? GeometryHelper.DistanceSquaredPointToSegmentXZ(player, _graph!.Positions[_at!.Value], _graph.Positions[to]) <= .4f * .4f
        : Distance(player, _graph!.Positions[_at!.Value]) <= .4f;

    private void CommitExit(float x)
    {
        ExitLane = x;
        var settings = new BlundervilleFinalRouteSettings { TailLane = x };
        _tailRace.ConfigureFinalRoutes(settings);
    }

    private BlundervilleRaceDecision AtExit(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now)
    {
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        if (_exitStep is { } exit)
        {
            if (exit.Departure > now && index.Stand(player, 0, (exit.Departure - now).TotalSeconds))
                return Hold(player, $"台阶入口：等待 {(exit.Departure - now).TotalSeconds:F1} 秒后连续登阶");
            var budget = (Latest(exit) - now).TotalSeconds;
            if (budget >= .1) index = index.WithExecutionLag(Math.Min(BlundervilleFinalPlanner.Lag, budget));
            else _exitStep = null;
        }
        if (!EndSafe(index, _graph!.Positions[_at!.Value], 0))
            return Hold(player, index.Stand(player, 0, .5) ? "已到台阶入口，等待连续登阶窗口" : "台阶入口出现危险，请手动处理");
        _exitStep = null;
        CommitExit(_graph!.Positions[_at!.Value].X);
        return Tail(normal, state, player, now);
    }

    private BlundervilleRaceDecision Tail(BlundervilleRaceDecision normal, BlundervilleRaceState state, Vector3 player, DateTime now)
    {
        if (player.Z > 137.3f && BlundervilleStairGeometry.OnStairs(player))
        {
            Status = "按当前台阶选择起跳与落点";
            return new(new(player.X, player.Y, player.Z > BlundervilleFinalPlanner.Bottom ? EndZ : Math.Max(137, player.Z - 5)), false, false, null, Status, true);
        }
        var decision = _tailRace.Update(state, now, player, true, false, false, null);
        Status = "沿已到达出口继续台阶";
        return decision with { DirectTraversal = !decision.Hold };
    }

    private static bool EndSafe(BlundervilleFinalPlanner.HazardIndex index, Vector3 p, double time)
        => MathF.Abs(p.Z - EndZ) < .01f ? index.CachedSegment(p, p with { Z = 149 }, time, BlundervilleFinalPlanner.FirstFlightSeconds)
            && index.CachedSegment(p, p with { Z = 137 }, time, BlundervilleFinalPlanner.StairSeconds)
            : index.Stand(p, time, time + (BlundervilleFinalPlanner.Final.Rest(p.Z) ? BlundervilleFinalPlanner.StandAfterGoal : 12));

    private static bool RemainingSafe(Result result, int next, Vector3 player, DateTime now, float speed,
        BlundervilleFinalPlanner.HazardIndex index, BlundervilleFinalPlanner.HazardIndex movingIndex, out string failure)
    {
        failure = "";
        var steps = result.Steps; var prior = steps[next - 1];
        // Validate from the actual position at the actual time. Reusing a past
        // departure with this later position puts the player in yesterday's
        // wave footprint and can discard a still-executable crossing.
        var departure = prior.Departure > prior.Arrival ? Math.Max(0, (prior.Departure - now).TotalSeconds) : 0;
        var currentIndex = departure > 0 ? index : movingIndex;
        if (departure > 0 && !index.Stand(player, 0, departure)) { failure = "unsafe current wait"; return false; }
        for (var i = next; i < steps.Count; ++i)
        {
            var a = steps[i - 1]; var b = steps[i];
            var from = i == next ? player : a.Position;
            var arrival = departure + Distance(from, b.Position) / speed;
            if (!currentIndex.Segment(from, b.Position, departure, arrival)) { failure = $"unsafe {a.Node}->{b.Node} t={departure:F2}..{arrival:F2}"; return false; }
            departure = b.Departure > b.Arrival ? Math.Max(arrival, (b.Departure - now).TotalSeconds) : arrival;
            if (departure > arrival && !index.Stand(b.Position, arrival, departure)) return false;
            if (b.Departure > b.Arrival)
            {
                if (!index.Stand(b.Position, arrival, departure)) { failure = $"unsafe next wait at {b.Node}"; return false; }
                // The published movement ends here. A forecast change beyond
                // this safe wait must not stop the current run in a mechanism.
                // Its next departure is checked when this node is reached.
                return true;
            }
        }
        return EndSafe(index, steps[^1].Position, departure);
    }

    private static Result? Rebase(Result result, Vector3 player, DateTime now, float speed, BlundervilleFinalPlanner.HazardIndex index)
    {
        List<Step> steps = []; var time = now;
        var source = result.Steps[0];
        var firstLeg = Distance(source.Position, result.Steps[1].Position);
        var progress = Math.Clamp(firstLeg - Distance(player, result.Steps[1].Position), 0, firstLeg);
        var sourceLatest = Latest(source).AddSeconds(progress / speed);
        var initialBudget = (sourceLatest - (source.Departure > now ? source.Departure : now)).TotalSeconds;
        if (initialBudget < .1) return null;
        var currentIndex = index.WithExecutionLag(Math.Min(BlundervilleFinalPlanner.Lag, initialBudget));
        for (var i = 0; i < result.Steps.Count; ++i)
        {
            var step = result.Steps[i]; var p = i == 0 ? player : step.Position;
            if (i > 0)
            {
                var previous = steps[^1]; time = previous.Departure.AddSeconds(Distance(previous.Position, p) / speed);
                if (!Link(previous.Position, p) || !currentIndex.Segment(previous.Position, p, (previous.Departure - now).TotalSeconds, (time - now).TotalSeconds)) return null;
            }
            var departure = step.Departure > step.Arrival && step.Departure > time ? step.Departure : time;
            if (departure > time && !index.Stand(p, (time - now).TotalSeconds, (departure - now).TotalSeconds)) return null;
            var latest = i == 0 ? sourceLatest : Latest(step);
            steps.Add(new(step.Node, p, time, departure) { LatestDeparture = latest });
            if (departure > time)
            {
                var budget = (latest - departure).TotalSeconds;
                if (budget < .1) return null;
                currentIndex = index.WithExecutionLag(Math.Min(BlundervilleFinalPlanner.Lag, budget));
            }
        }
        return EndSafe(index, steps[^1].Position, (steps[^1].Departure - now).TotalSeconds) ? new(steps, result.Expanded) : null;
    }

    private static DateTime Latest(Step step) => step.LatestDeparture == default ? step.Departure.AddSeconds(BlundervilleFinalPlanner.Lag) : step.LatestDeparture;

    internal static Result? Find(BlundervilleRouteGraph.Compiled graph, int at, int? committedTo,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, float speed, CancellationToken token = default,
        Action<SearchInfo>? report = null)
    {
        if (!float.IsFinite(speed) || speed <= 0 || !graph.Positions.ContainsKey(at)) return null;
        if (committedTo is { } target && !graph.Next[at].Contains(target)) return null;
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        // Target the next measured safe strip, including the whole wave zone.
        // A fixed 45m horizon skipped strips and expanded thousands of cyclic states.
        var strips = BlundervilleFinalPlanner.Final.Strips!;
        var strip = strips.Where(s => s.To < player.Z - .5f && !(player.Z >= s.From && player.Z <= s.To))
            .OrderByDescending(s => s.To).FirstOrDefault();
        var goals = graph.Positions.Where(p => strip == default ? graph.Exits.Contains(p.Key)
            : p.Value.Z >= strip.From && p.Value.Z <= strip.To).Select(p => p.Key).ToHashSet();
        var distances = new Dictionary<int, double>();
        var reverse = graph.Positions.Keys.ToDictionary(id => id, _ => new List<int>());
        foreach (var (from, targets) in graph.Next) foreach (var to in targets) reverse[to].Add(from);
        var open = new PriorityQueue<int, double>();
        foreach (var goal in graph.Exits) { distances[goal] = 0; open.Enqueue(goal, 0); }
        while (open.TryDequeue(out var id, out var cost))
        {
            if (cost > distances[id]) continue;
            foreach (var prev in reverse[id])
            {
                var d = cost + Distance(graph.Positions[prev], graph.Positions[id]);
                if (distances.TryGetValue(prev, out var prior) && prior <= d) continue;
                distances[prev] = d; open.Enqueue(prev, d);
            }
        }
        const double horizon = 16;
        var queue = new PriorityQueue<Label, double>();
        var covered = new Dictionary<int, List<(double From, double Until)>>();
        Label? best = null; var bestTime = 0d; var expanded = 0;
        Vector3 Position(int id) => id == int.MinValue ? player : graph.Positions[id];
        bool Rest(Vector3 p) => BlundervilleFinalPlanner.Final.Rest(p.Z) || p.Z < 184 && index.Stand(p, 0, 28);
        double MaxWait(int node) => graph.Exits.Contains(node) || Rest(Position(node)) ? 12 : 1;

        double ExitDeparture(Vector3 p, double first, double last)
        {
            var blocked = index.TraversalBlockedDepartures(p, p with { Z = 149 }, BlundervilleFinalPlanner.FirstFlightSeconds)
                .Concat(index.TraversalBlockedDepartures(p, p with { Z = 137 }, BlundervilleFinalPlanner.StairSeconds)).ToArray();
            var guarded = blocked.Concat(blocked.Select(w => (From: w.From - .3, Until: w.Until - .3))).OrderBy(w => w.From);
            return BlundervilleFinalPlanner.HazardIndex.ClearRanges(guarded, first, last)
                .Select(w => w.From).DefaultIfEmpty(double.PositiveInfinity).First();
        }

        void Offer(int node, double arrivalFrom, double arrivalUntil, double safeUntil, double travel, Label? previous)
        {
            var until = Math.Min(horizon, Math.Min(safeUntil, arrivalUntil + MaxWait(node)));
            if (arrivalFrom > until) return;
            if (!covered.TryGetValue(node, out var coverage)) covered[node] = coverage = [];
            var fresh = BlundervilleFinalPlanner.HazardIndex.ClearRanges(coverage, arrivalFrom, until, 1e-7).ToArray();
            foreach (var span in fresh)
            {
                var goalTime = graph.Exits.Contains(node) ? ExitDeparture(Position(node), span.From, span.Until) : double.NaN;
                if (double.IsPositiveInfinity(goalTime)) continue;
                var remaining = node == int.MinValue ? 0 : distances.GetValueOrDefault(node, double.PositiveInfinity);
                if (!double.IsFinite(remaining)) continue;
                var label = new Label(node, span.From, span.Until, arrivalFrom, arrivalUntil, travel, previous, goalTime);
                queue.Enqueue(label, double.IsNaN(goalTime) ? span.From + remaining / speed : goalTime);
            }
            if (fresh.Length == 0) return;
            coverage.Add((arrivalFrom, until));
            coverage.Sort((a, b) => a.From.CompareTo(b.From));
            for (var i = coverage.Count - 1; i > 0; --i)
                if (coverage[i].From <= coverage[i - 1].Until)
                {
                    coverage[i - 1] = (coverage[i - 1].From, Math.Max(coverage[i - 1].Until, coverage[i].Until));
                    coverage.RemoveAt(i);
                }
        }

        foreach (var safe in index.StandingRanges(player, 32))
            if (safe.From <= 0 && safe.Until >= 0) Offer(int.MinValue, 0, 0, safe.Until, 0, null);

        while (queue.TryDequeue(out var current, out _) && expanded < 12000)
        {
            ++expanded;
            if (token.IsCancellationRequested) return null;
            var p = Position(current.Node);
            var departure = double.IsNaN(current.GoalTime) ? current.From : current.GoalTime;
            if (current.Previous != null && (graph.Exits.Contains(current.Node) || BlundervilleFinalPlanner.Final.Rest(p.Z))
                && EndSafe(index, p, departure) && (player.Z - p.Z > .5f || graph.Exits.Contains(current.Node)))
            {
                if (best == null || p.Z < Position(best.Node).Z - .01f
                    || MathF.Abs(p.Z - Position(best.Node).Z) < .01f && departure < bestTime)
                { best = current; bestTime = departure; }
                if (goals.Contains(current.Node)) { best = current; bestTime = departure; break; }
            }
            var targets = current.Node == int.MinValue ? committedTo is { } lockedNext ? new[] { lockedNext } : graph.Next[at] : graph.Next[current.Node];
            foreach (var next in targets)
            {
                var b = graph.Positions[next]; var duration = Distance(p, b) / speed;
                if (duration <= 0) continue;
                foreach (var opening in index.DepartureRanges(p, b, duration, current.From, Math.Min(current.Until, horizon - duration)))
                    foreach (var safe in index.StandingRanges(b, 32))
                    {
                        var first = Math.Max(opening.From + duration, safe.From);
                        var last = Math.Min(opening.Until + duration, safe.Until);
                        if (first <= last) Offer(next, first, last, safe.Until, duration, current);
                    }
            }
        }
        report?.Invoke(new(expanded, expanded >= 12000));
        if (best == null) return null;
        List<Step> steps = [];
        var chosenDeparture = bestTime;
        for (var label = best; label != null; label = label.Previous)
        {
            // Keep the receiving time before extending it with a legal wait.
            // Each child was produced from one parent's continuous opening.
            var arrival = label == best
                ? Math.Max(label.ArrivalFrom, chosenDeparture - MaxWait(label.Node))
                : Math.Min(chosenDeparture, label.ArrivalUntil);
            if (arrival < label.ArrivalFrom - .00001 || arrival > label.ArrivalUntil + .00001
                || chosenDeparture < label.From - .00001 || chosenDeparture > label.Until + .00001
                || chosenDeparture - arrival > MaxWait(label.Node) + .00001) return null;
            steps.Add(new(label.Node == int.MinValue ? at : label.Node, Position(label.Node),
                now.AddSeconds(arrival), now.AddSeconds(chosenDeparture)) { LatestDeparture = now.AddSeconds(chosenDeparture + BlundervilleFinalPlanner.Lag) });
            chosenDeparture = arrival - label.Travel;
        }
        steps.Reverse();
        return steps.Count >= 2 ? new(steps, expanded) : null;
    }
}
