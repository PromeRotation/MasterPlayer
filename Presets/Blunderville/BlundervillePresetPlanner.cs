using System.Diagnostics;
using System.Numerics;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using static MasterPlayer.Mechanics.Blunderville.BlundervillePresetGeometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervillePresetPlanner
{
    internal sealed record Route(string Name, Vector3[] Points);
    internal sealed record Result(string Name, List<BlundervilleFinalPlanner.Waypoint> Plan, double Milliseconds);
    private BlundervilleRouteLibrary? _library;
    private BlundervilleFinalRouteSettings? _courseRoutes;
    private Route[] _routes = [];
    private Vector3[] _tail = [];
    private Task<Result?>? _pending;
    private CancellationTokenSource? _cancellation;
    private Result? _result;
    private Vector3[]? _stretch;
    private int _next, _stretchStart;
    private DateTime _last, _retry, _requestedAt;
    private Vector3 _requestedFrom;
    private float _speed;
    internal bool Enabled => _library?.UsePresets == true;
    internal Task? Pending => _pending;
    internal string Status { get; private set; } = "等待第三关路线";
    internal double LastSearchMilliseconds { get; private set; }

    internal void ConfigureCourse(BlundervilleFinalRouteSettings settings)
    {
        if (ReferenceEquals(_courseRoutes, settings)) return;
        _courseRoutes = settings;
        var assigned = settings.AssignedRoute();
        Configure(new BlundervilleRouteLibrary { UsePresets = true, Routes = assigned == null ? [] : [assigned] });
        _tail = settings.Nodes(default).Where(n => n.Position.Z <= EndZ).Select(n => n.Position).ToArray();
    }

    internal void Configure(BlundervilleRouteLibrary library)
    {
        if (ReferenceEquals(_library, library)) return;
        Reset(); _library = library;
        _routes = library.Version != 1 ? [] : (library.Routes ?? []).Take(MaxRoutes)
            .Where(r => r != null && r.Enabled && Validate(r) == null)
            .Select(r => new Route(r.Name, Compile(r))).ToArray();
    }

    internal void Reset()
    {
        _cancellation?.Cancel(); _cancellation?.Dispose(); _cancellation = null;
        _pending = null; _result = null; _stretch = null; _next = 0;
        _last = _retry = default;
    }

    internal BlundervilleRaceDecision Apply(BlundervilleRaceDecision normal, BlundervilleRaceState state,
        IReadOnlyList<BlundervilleHazard> hazards, Vector3 player, DateTime now, DirectTraversalExecution? execution = null)
    {
        if (now < _last) Reset();
        _last = now;
        if (!Enabled || state.Course != BlundervilleCourse.Final || !state.IsRacing || state.Qualified || !state.CanMove(now))
        { Reset(); return normal; }
        if (_routes.Length == 0) return Hold(player, "请先在地图中指定各机制走法并保存；不会自动选路");
        if (player.Z <= BlundervilleFinalPlanner.Bottom)
        {
            Reset();
            if (_courseRoutes == null) return normal;
            if (!_tail.Zip(_tail.Skip(1)).Any(s => GeometryHelper.DistanceSquaredPointToSegmentXZ(player, s.First, s.Second) <= .4f * .4f))
                return Hold(player, "已偏离指定的台阶／终点路线，请手动回线");
            return normal with { DirectTraversal = !normal.Hold };
        }
        if (_courseRoutes != null && Suffix(_routes[0].Points, player, .4f) == null)
            return Stop(player, "已偏离你指定的路线，请手动回到路线后继续");
        if (normal.Hold) { Reset(); return normal; }
        if (player.Y <= 2 || !Floor.Contains(player, 0)) return Stop(player, "已离开坡道，请手动回到第三关路线");
        var speed = state.NavigationBaseSpeed(now);
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        if (_result != null)
        {
            var plan = _result.Plan;
            if (_stretch != null && execution is { HasControl: true } && execution.Path.SequenceEqual(_stretch))
                _next = Math.Max(_next, Math.Min(plan.Count, _stretchStart + execution.CurrentIndex));
            while (_next < plan.Count && Distance(player, plan[_next].Position) < .2f) ++_next;
            if (_next >= plan.Count || execution is { Rejected: true } || MathF.Abs(speed - _speed) > .01f
                || GeometryHelper.DistanceSquaredPointToSegmentXZ(player, plan[_next - 1].Position, plan[_next].Position) > 1
                || !SafeRemaining(plan, _next, player, now, speed, index))
            { _result = null; _stretch = null; }
        }
        if (_result == null)
        {
            if (_pending != null && _pending.IsCompleted)
            {
                var done = _pending; _pending = null;
                var result = done.IsCompletedSuccessfully ? done.Result : null;
                _ = done.Exception;
                _cancellation?.Dispose(); _cancellation = null;
                if (result != null && now - _requestedAt < TimeSpan.FromSeconds(3) && Distance(player, _requestedFrom) < .4f && MathF.Abs(speed - _speed) < .01f)
                {
                    LastSearchMilliseconds = result.Milliseconds;
                    var plan = Rebase(result.Plan, player, now, speed, index);
                    if (plan != null) { _result = result with { Plan = plan }; _next = 1; }
                }
                if (_result == null) _retry = now.AddSeconds(.4);
            }
            if (_result == null)
            {
                if (_pending == null && now >= _retry)
                {
                    var captured = hazards.ToArray(); var routes = _routes;
                    _requestedAt = now; _requestedFrom = player; _speed = speed;
                    _cancellation = new(); var token = _cancellation.Token;
                    _pending = Task.Run(() => Find(routes, captured, player, now, speed, token));
                }
                return Hold(player, index.Stand(player, 0, 1.5)
                    ? "等待第三关路线的安全通行时机"
                    : "第三关路线暂不可通行，当前位置有危险，请手动避让");
            }
        }
        var current = _result.Plan[_next - 1];
        if (current.Departure > now && current.Departure - current.Arrival > TimeSpan.FromMilliseconds(50))
        {
            _stretch = null;
            if (Distance(player, current.Position) > .35f) return Stop(player, "已偏离等待点，请重新接入路线");
            return Hold(player, $"{_result.Name}：等待 {(current.Departure - now).TotalSeconds:F1} 秒");
        }
        if (_stretch == null || _next >= _stretchStart + _stretch.Length)
        {
            var end = _next;
            while (end < _result.Plan.Count - 1 && _result.Plan[end].Departure == _result.Plan[end].Arrival) ++end;
            _stretchStart = _next;
            _stretch = _result.Plan.Skip(_next).Take(end - _next + 1).Select(w => w.Position).ToArray();
        }
        Status = $"第三关路线：{_result.Name}";
        return new(_stretch[^1], false, false, null, Status, true) { ContinuousPath = _stretch };
    }

    private BlundervilleRaceDecision Hold(Vector3 p, string reason)
    {
        Status = "第三关路线：" + reason;
        return new(p, true, false, null, Status, false);
    }

    private BlundervilleRaceDecision Stop(Vector3 p, string reason) { Reset(); return Hold(p, reason); }

    private static bool SafeRemaining(List<BlundervilleFinalPlanner.Waypoint> plan, int next, Vector3 player,
        DateTime now, float speed, BlundervilleFinalPlanner.HazardIndex index)
    {
        var prior = plan[next - 1];
        var progress = Math.Clamp(Distance(prior.Position, plan[next].Position) - Distance(player, plan[next].Position), 0, Distance(prior.Position, plan[next].Position));
        var scheduled = (prior.Departure - now).TotalSeconds + progress / speed;
        if (scheduled < -BlundervilleFinalPlanner.Lag) return false;
        if (scheduled > 0 && !index.Stand(player, 0, scheduled)) return false;
        var from = prior.Position; var start = (prior.Departure - now).TotalSeconds;
        for (var i = next; i < plan.Count; ++i)
        {
            var arrival = (plan[i].Arrival - now).TotalSeconds;
            if (arrival < start || !index.Segment(from, plan[i].Position, start, arrival)) return false;
            var departure = (plan[i].Departure - now).TotalSeconds;
            if (departure > arrival && !index.Stand(plan[i].Position, arrival, departure)) return false;
            from = plan[i].Position; start = departure;
        }
        return EndSafe(index, from, start);
    }

    private static List<BlundervilleFinalPlanner.Waypoint>? Rebase(List<BlundervilleFinalPlanner.Waypoint> plan,
        Vector3 player, DateTime now, float speed, BlundervilleFinalPlanner.HazardIndex index)
    {
        var result = new List<BlundervilleFinalPlanner.Waypoint>();
        var position = player; var arrival = now;
        for (var i = 0; i < plan.Count; ++i)
        {
            var p = i == 0 ? player : plan[i].Position;
            if (i > 0)
            {
                var start = result[^1].Departure; arrival = start.AddSeconds(Distance(position, p) / speed);
                if (!Link(position, p) || !index.Segment(position, p, (start - now).TotalSeconds, (arrival - now).TotalSeconds)) return null;
            }
            var departure = plan[i].Departure > plan[i].Arrival && plan[i].Departure > arrival ? plan[i].Departure : arrival;
            if (departure > arrival && !index.Stand(p, (arrival - now).TotalSeconds, (departure - now).TotalSeconds)) return null;
            result.Add(new(p, arrival, departure)); position = p;
        }
        return EndSafe(index, position, (arrival - now).TotalSeconds) ? result : null;
    }

    private static bool EndSafe(BlundervilleFinalPlanner.HazardIndex index, Vector3 p, double time)
        => MathF.Abs(p.Z - EndZ) < .01f
            ? BlundervilleFinalPlanner.FirstFlightClear(index, p, time)
            : index.Stand(p, time, time + BlundervilleFinalPlanner.StandAfterGoal);

    internal static Result? Find(IReadOnlyList<Route> routes, IReadOnlyList<BlundervilleHazard> hazards,
        Vector3 player, DateTime now, float speed, CancellationToken cancellation = default)
    {
        if (!float.IsFinite(speed) || speed <= 0) return null;
        var timer = Stopwatch.StartNew();
        var index = new BlundervilleFinalPlanner.HazardIndex(hazards, now, BlundervilleFinalPlanner.Final);
        Result? best = null;
        foreach (var route in routes.Take(MaxRoutes))
        {
            if (cancellation.IsCancellationRequested) return null;
            var path = Suffix(route.Points, player);
            if (path == null) continue;
            List<BlundervilleFinalPlanner.Waypoint> plan = [new(player, now, now)];
            var block = new List<Vector3>(); var time = 0d;
            for (var i = 1; i < path.Length; ++i)
            {
                block.Add(path[i]);
                var end = path[i];
                if (i != path.Length - 1 && !Stops.Any(z => MathF.Abs(z - end.Z) < .01f)) continue;
                var from = plan[^1].Position;
                var accepted = false;
                for (var step = 0; step <= 120; ++step)
                {
                    if (cancellation.IsCancellationRequested) return null;
                    var departure = time + step * .1;
                    if (step > 0 && (!BlundervilleFinalPlanner.Final.Rest(from.Z) || !index.Stand(from, time, departure))) break;
                    var at = departure; var p = from; var clear = true;
                    foreach (var to in block)
                    {
                        var arrival = at + Distance(p, to) / speed;
                        if (!index.Segment(p, to, at, arrival)) { clear = false; break; }
                        p = to; at = arrival;
                    }
                    if (!clear || !EndSafe(index, end, at)) continue;
                    plan[^1] = plan[^1] with { Departure = now.AddSeconds(departure) };
                    p = from; time = departure;
                    foreach (var to in block)
                    {
                        time += Distance(p, to) / speed;
                        plan.Add(new(to, now.AddSeconds(time), now.AddSeconds(time))); p = to;
                    }
                    accepted = true; break;
                }
                if (!accepted) break;
                block.Clear();
                if (player.Z - end.Z >= 45) break;
            }
            if (plan.Count < 2 || player.Z - plan[^1].Position.Z < .5f && MathF.Abs(plan[^1].Position.Z - EndZ) > .01f) continue;
            if (best == null || plan[^1].Position.Z < best.Plan[^1].Position.Z - .01f
                || MathF.Abs(plan[^1].Position.Z - best.Plan[^1].Position.Z) < .01f && plan[^1].Arrival < best.Plan[^1].Arrival)
                best = new(route.Name, plan, 0);
        }
        return best == null ? null : best with { Milliseconds = timer.Elapsed.TotalMilliseconds };
    }

    internal static Vector3[]? Suffix(Vector3[] points, Vector3 player, float joinDistance = 3.5f)
    {
        var nearest = float.PositiveInfinity; var segment = -1; var join = player;
        for (var i = 0; i < points.Length - 1; ++i)
        {
            var a = points[i]; var b = points[i + 1]; var d = b - a; d.Y = 0;
            var relative = player - a; relative.Y = 0;
            var t = Math.Clamp(Vector3.Dot(relative, d) / MathF.Max(.0001f, d.LengthSquared()), 0, 1);
            var p = Vector3.Lerp(a, b, t); var distance = Distance(player, p);
            if (distance >= nearest || p.Z > player.Z + .5f) continue;
            nearest = distance; segment = i; join = p;
        }
        if (segment < 0 || nearest > joinDistance || !Link(player, join)) return null;
        var result = new List<Vector3> { player };
        if (nearest > .05f) result.Add(join);
        foreach (var p in points.Skip(segment + 1))
            if (Distance(result[^1], p) > .05f) result.Add(p);
        for (var i = 1; i < result.Count; ++i) if (!Link(result[i - 1], result[i])) return null;
        return result.Count > 1 ? result.ToArray() : null;
    }
}
