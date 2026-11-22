using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Follow;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using PromeRotation.LogSystem;

internal static class Pt99BoundsRegression
{
    private static readonly DateTime Start = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    public static void RunLive(Action<string, Action> run)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-bounds-143046.json")));
        run("PT99 Bounds arrival disk stays outside a pending circle", PreciseArrival);
        foreach (var latency in new[] { .08, .3 })
            foreach (var follow in new[] { false, true })
                // A 300ms worker is covered from bind release. The late recorded
                // prefix uses the measured live-scale latency: forcing a route for
                // another 300ms after dispatch has stopped cannot verify recovery.
                foreach (var handoff in latency < .1 ? new[] { 0d, fixture.RootElement.GetProperty("handoff").GetDouble() } : new[] { 0d })
                run($"PT99 Bounds 143046 margin=.2 follow={follow} handoff={handoff:F3} latency={latency:F2}", () =>
                    Simulate(fixture.RootElement, fixture.RootElement.GetProperty("bindRelease").GetDouble()
                        - fixture.RootElement.GetProperty("events").EnumerateArray().Single(e =>
                            e.GetProperty("kind").GetString() == "effect" && e.GetProperty("action").GetUInt32() == 44082)
                        .GetProperty("t").GetDouble(), latency, .2f, follow, true, handoff));
    }

    private static void PreciseArrival()
    {
        var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true, RuntimeHardSafetyMargin = .2f };
        using var manager = new SafeZoneManager(setting);
        var center = new Vector3(-593.95743f, 0, -303.5172f);
        manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(center, 0),
            [new(Start.AddSeconds(8.801), Start.AddSeconds(9.151))]);
        var from = new Vector3(-593.19446f, 0, -305.1652f);
        using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
        var build = typeof(SafeZoneRebornMovementSystem).GetMethod("BuildGreenMovePoints", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var to in new[] { new Vector3(-592.19446f, 0, -306.1652f), new Vector3(-592.8893f, 0, -306.40933f) })
        {
            var route = SafeZoneSolution.Succeeded(manager.Version, Start, Start, to,
                [TimedWaypoint.Pass(from, Start), TimedWaypoint.Pass(to, Start.AddSeconds(1))]);
            Check(!route.UsesTemporalPlanning, "Exercise the ordinary escape branch of a goal-directed solve");
            var points = (GreenMovePoint[])build.Invoke(movement, [route, manager.BuildSnapshot(Start), Start])!;
            Check(Distance(to, center) - points[^1].Tolerance > 3,
                $"Arrival tolerance {points[^1].Tolerance:F3} permits stopping inside the recorded R3 circle");
            var expired = Start.AddSeconds(10);
            var restored = (GreenMovePoint[])build.Invoke(movement, [route, manager.BuildSnapshot(expired), expired])!;
            Check(restored[^1].Tolerance == .5f, "Expired circles must not permanently force precise arrival");
        }
    }

    public static void Run(Action<string, Action> run)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-bounds-015418.json")));
        foreach (var sequence in fixture.RootElement.GetProperty("sequences").EnumerateArray())
        {
            var territory = sequence.GetProperty("territory").GetUInt32();
            var recordingStart = sequence.GetProperty("recordingStart").GetDouble();
            run($"PT99 Bounds recorded timing territory={territory} t={recordingStart:F3}", () => Timing(sequence));
        }
        var normal = fixture.RootElement.GetProperty("sequences")[0];
        foreach (var latency in new[] { .08, .3 })
            run($"PT99 Bounds actual solver escapes a center goal after recorded pull and bind latency={latency:F2}",
                () => Simulate(normal, fixture.RootElement.GetProperty("bindSeconds").GetDouble(), latency));
    }

    private static void Timing(JsonElement sequence)
    {
        var encounter = new Pt99Encounter();
        foreach (var ev in sequence.GetProperty("events").EnumerateArray())
        {
            var now = Start.AddSeconds(ev.GetProperty("t").GetDouble());
            if (ev.GetProperty("action").GetUInt32() == 44084)
            {
                var central = encounter.Collect(now, 100).Single(a => a.Key == "bounds/end");
                Check(central.Window.IsActiveAt(now), "Central R8 must be dangerous at the recorded actual hit, not four seconds later");
                Check(central.Geometry.Contains(encounter.Center + new Vector3(7.9f, 0, 0))
                    && !central.Geometry.Contains(encounter.Center + new Vector3(8.1f, 0, 0)), "Keep actual R8 footprint");
            }
            Apply(encounter, ev);
            if (ev.GetProperty("action").GetUInt32() == 44082 && ev.GetProperty("kind").GetString() == "effect")
            {
                var goal = encounter.Collect(now, 100).Single(a => a.Key == "bounds/escape");
                Check(goal.Safe && goal.NavigationOnly && !goal.Geometry.Contains(encounter.Center)
                    && !goal.Geometry.Contains(encounter.Center + new Vector3(9, 0, 0))
                    && goal.Geometry.Contains(encounter.Center + new Vector3(11, 0, 0)), "Pull escape goal clears both the central hit and the R7 ring of R3 circles");
            }
            if (ev.GetProperty("action").GetUInt32() == 44082 && ev.GetProperty("kind").GetString() == "cast")
            {
                var central = encounter.Collect(now, 100).SingleOrDefault(a => a.Key == "bounds/end");
                if (central != null)
                    Check(Math.Abs((central.From - now).TotalSeconds - 11.5) < .000001
                        && Math.Abs((central.Until - now).TotalSeconds - 11.85) < .000001,
                        "11.7s is measured from pull cast start; retain explicit -.2/+.15 timing margins");
            }
            if (ev.GetProperty("action").GetUInt32() == 44084)
                Check(!encounter.Collect(now, 100).Any(a => a.Key.StartsWith("bounds/")), "Actual central effect clears danger and escape goal");
        }
        encounter.Reset();
        var first = sequence.GetProperty("events")[0];
        Apply(encounter, first);
        Check(encounter.Collect(Start, 100).Any(a => a.Key == "bounds/end"), "Predict the central hit before the pull resolves");
        var pull = sequence.GetProperty("events").EnumerateArray().Single(e => e.GetProperty("action").GetUInt32() == 44082
            && e.GetProperty("kind").GetString() == "effect");
        Apply(encounter, pull);
        encounter.Cancel(first.GetProperty("source").GetUInt64());
        Check(!encounter.Collect(Start, 100).Any(a => a.Key.StartsWith("bounds/")), "Cancel removes danger and escape goal owned by this caster");
    }

    private static void Simulate(JsonElement sequence, double bindSeconds, double latency, float margin = .5f, bool followTarget = false, bool fullPipeline = false, double handoff = 0)
    {
        var events = sequence.GetProperty("events").EnumerateArray().ToArray();
        var releaseAt = events.Single(e => e.GetProperty("action").GetUInt32() == 44082
            && e.GetProperty("kind").GetString() == "effect").GetProperty("t").GetDouble() + bindSeconds;
        var encounter = new Pt99Encounter();
        var player = encounter.Center;
        var setting = new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true, RuntimeGoalDirectedTemporalPlanning = true,
            RuntimeHardSafetyMargin = margin, RuntimeArenaSafetyMargin = 0 };
        using var manager = new SafeZoneManager(setting);
        manager.SetArenaBoundary(encounter.Boundary);
        // A competing stationary preference must yield to the central hit and may
        // resume afterwards. This is a solver boundary case, not a recorded goal.
        if (!followTarget && !sequence.TryGetProperty("moves", out _))
            manager.AddGoalArea(new CircleTemplate(12), new StaticTransform(encounter.Center, 0),
                [GoalWindow.Persistent(Start, 1, PositionPreference.Nearest)]);
        var followHandle = default(SafeZoneAreaHandle);
        var followActive = false;
        var handles = new Dictionary<string, SafeZoneAreaHandle>();
        var solver = new SafeZoneSolver();
        var options = new SolveOptions { HardSafetyMargin = margin, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true };
        var move = new SimulatedMove();
        using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
        movement.Attach(move);
        var clock = new SolveClock(latency);
        if (fullPipeline)
        {
            var scheduler = typeof(SafeZoneRebornMovementSystem).GetField("_scheduleSolve", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var resultType = scheduler.FieldType.GenericTypeArguments[0].GenericTypeArguments[0];
            var method = typeof(SolveClock).GetMethod(nameof(SolveClock.Schedule))!.MakeGenericMethod(resultType);
            scheduler.SetValue(movement, Delegate.CreateDelegate(scheduler.FieldType, clock, method));
        }
        var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var latestField = typeof(SafeZoneRebornMovementSystem).GetField("_latestSolution", BindingFlags.NonPublic | BindingFlags.Instance)!;
        SafeZoneSolution? latest = null;
        SafeZoneSolution? pending = null;
        var pendingAt = double.PositiveInfinity;
        var nextSolve = releaseAt;
        var index = 0;
        var smallHits = 0;
        var centralHits = 0;
        var plans = new List<string>();
        var times = new SortedSet<double>(Enumerable.Range(0, 611).Select(t => t * .02));
        times.UnionWith(events.Select(e => e.GetProperty("t").GetDouble()));
        times.Add(releaseAt);
        if (handoff > 0)
        {
            times.Add(handoff);
            times.UnionWith(sequence.GetProperty("moves").EnumerateArray().Select(p => p.GetProperty("t").GetDouble()));
        }
        var previousTime = 0d;
        foreach (var t in times)
        {
            var now = Start.AddSeconds(t);
            var previousPosition = player;
            player = t < releaseAt ? encounter.Center : move.Advance(player, (float)(t - previousTime));
            previousTime = t;
            if (handoff > 0 && t >= releaseAt && t <= handoff)
            {
                var recorded = sequence.GetProperty("moves").EnumerateArray().LastOrDefault(p => p.GetProperty("t").GetDouble() <= t);
                if (recorded.ValueKind != JsonValueKind.Undefined)
                    player = Point(recorded.GetProperty("position"));
                if (Distance(previousPosition, player) > .001f)
                    move.ObserveMotion();
            }
            encounter.Synchronize([
                new ReplayActorState { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 2, Oid = 0x48EB, Position = new(-600, 0, -315), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 100, Kind = 1, Position = player, Hp = 1000,
                    Statuses = [new(4560, 0, 0, DateTime.MaxValue)] }], now);
            while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t + .00001)
            {
                var ev = events[index++];
                var action = ev.GetProperty("action").GetUInt32();
                if (ev.GetProperty("kind").GetString() == "effect" && action is 44083 or 44084)
                {
                    var center = action == 44084 ? encounter.Center : Point(ev.GetProperty("position"));
                    var radius = action == 44084 ? 8 : 3;
                    Check(Distance(player, center) > radius, $"Hit by {action} at {t:F3}s: player={player}, radius={radius}\n{string.Join("\n", plans.TakeLast(12))}");
                    if (action == 44084) ++centralHits;
                    else ++smallHits;
                }
                Apply(encounter, ev);
            }
            Check(encounter.Boundary.Contains(player), $"Left the arena at {t:F3}s");
            if (t < releaseAt && !fullPipeline) continue;
            var wanted = encounter.Collect(now, 100).Where(a => !a.PreviewOnly).ToArray();
            var keys = wanted.Select(a => a.Key).ToHashSet();
            foreach (var key in handles.Keys.Where(k => !keys.Contains(k)).ToArray())
            { manager.RemoveArea(handles[key]); handles.Remove(key); }
            foreach (var area in wanted)
            {
                Check(!area.Obstacle && (!area.Safe || area.Key == "bounds/escape"), "Recorded player already has light; only the escape goal is needed");
                var binding = new StaticTransform(area.Position, area.Rotation);
                if (!handles.TryGetValue(area.Key, out var handle))
                    handles[area.Key] = area.Safe
                        ? manager.AddGoalArea(area.Shape, binding, [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)])
                        : manager.AddDangerArea(area.Shape, binding, [area.Window]);
                else manager.UpdateArea(handle, dangerWindows: area.Safe ? null : [area.Window], incrementVersion: false);
            }
            if (pending != null && t >= pendingAt - .00001)
            {
                latest = pending;
                latestField.SetValue(movement, latest);
                pending = null;
            }
            var snapshot = manager.BuildSnapshot(now);
            // FollowController yields its optional goal while a mechanic goal is active.
            if (followTarget && wanted.Any(a => a.Safe && a.Priority >= 0))
            {
                if (followHandle.IsValid) manager.RemoveArea(followHandle);
                followHandle = default;
                followActive = false;
                snapshot = manager.BuildSnapshot(now);
            }
            else if (followTarget)
            {
                var target = sequence.GetProperty("followTarget");
                var position = Point(target.GetProperty("position"));
                var template = new DangerAwareFollowGoalTemplate(target.GetProperty("radius").GetSingle(),
                    snapshot.DangerAreas.Where(a => a.DangerWindows.Any(w =>
                        OptionalGoalSafety.MasksFollowGoal(w.DangerFrom, w.DangerUntil, now, a.Geometry)))
                        .Select(a => a.Geometry).Concat(snapshot.HardObstacles.Select(a => a.Geometry)), snapshot.ArenaBoundary);
                var enabled = template.HasSafeArea(position);
                GoalWindow[] windows = enabled ? [new(now, null, -1, PositionPreference.Nearest)] : [];
                if (!followHandle.IsValid)
                    followHandle = manager.AddGoalArea(template, new StaticTransform(position, 0), windows);
                else
                    manager.UpdateArea(followHandle, template: template, goalWindows: windows, incrementVersion: enabled != followActive);
                followActive = enabled;
                snapshot = manager.BuildSnapshot(now);
            }
            var state = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
            if (fullPipeline)
            {
                clock.Advance(t);
                movement.Update(state, new(6, 6, .08f, false, 6, 6), now);
                plans.Add($"t={t:F3} pos={player} phase={movement.Phase} target={movement.LatestSolution?.TargetPosition} running={move.IsRunning}");
            }
            else dispatch.Invoke(movement, [latest, state, snapshot, now]);
            latest = movement.LatestSolution;
            if (!fullPipeline && pending == null && t >= nextSolve - .00001)
            {
                pending = solver.Solve(snapshot, state, new(6, 6, .08f, false, 6, 6), options, now, latest);
                plans.Add($"t={t:F2} {pending.Status} noMove={pending.IsNoMovement} temporal={pending.UsesTemporalPlanning} "
                    + string.Join(";", pending.ExecutablePath.Select(p => $"{p.Position}@{(p.PlannedArrivalTime - Start).TotalSeconds:F2}")));
                pendingAt = t + latency;
                nextSolve = t + .2;
            }
        }
        Check(smallHits == 12 && centralHits == 1, "Exercise every recorded circle and the central explosion");
    }

    private sealed class SolveClock(double latency)
    {
        private readonly List<(double At, Action Publish)> pending = [];
        private double now;

        public Task<T> Schedule<T>(Func<T> solve, CancellationToken token)
        {
            var result = solve();
            var promise = new TaskCompletionSource<T>();
            pending.Add((now + latency, () => promise.SetResult(result)));
            return promise.Task;
        }

        public void Advance(double time)
        {
            now = time;
            foreach (var task in pending.Where(p => p.At <= time).ToArray())
            {
                task.Publish();
                pending.Remove(task);
            }
        }
    }

    private static void Apply(Pt99Encounter encounter, JsonElement ev)
    {
        var now = Start.AddSeconds(ev.GetProperty("t").GetDouble());
        var source = ev.GetProperty("source").GetUInt64();
        var action = ev.GetProperty("action").GetUInt32();
        var position = Point(ev.GetProperty("position"));
        if (ev.GetProperty("kind").GetString() == "cast")
            encounter.StartCast(new(source, action, position, ev.GetProperty("rotation").GetSingle(), now,
                now.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), source));
        else encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
            GlobalSequence = ev.GetProperty("seq").GetUInt32(), SourcePosition = position }, now);
    }

    private sealed class SimulatedMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> points = [];
        private int index;
        private double elapsed;
        private double ready;
        public bool IsRunning => index < points.Count;
        // The recorded prefix supplies actual motion even when it differs from
        // this simulated route. It has already consumed movement startup.
        public void ObserveMotion() => ready = Math.Min(ready, elapsed);
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? points[index] : null,
            points.Count > 0 ? points[^1] : null, index, points.Count, points.Count - index);
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> route)
        {
            if (!IsRunning) ready = elapsed + .08;
            points = route;
            index = 0;
        }
        public void Stop() { points = []; index = 0; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> route) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => ++index;
        public Vector3 Advance(Vector3 current, float dt)
        {
            elapsed += dt;
            if (elapsed < ready) return current;
            while (IsRunning)
            {
                var point = points[index];
                if (Distance(current, point.Position) <= point.Tolerance) { ++index; continue; }
                var delta = point.Position - current;
                delta.Y = 0;
                return current + Vector3.Normalize(delta) * Math.Min(6 * dt, delta.Length());
            }
            return current;
        }
    }

    private static Vector3 Point(JsonElement p) => new(p[0].GetSingle(), 0, p[2].GetSingle());
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
