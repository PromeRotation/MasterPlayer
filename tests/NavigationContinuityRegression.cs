using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class NavigationContinuityRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Action<string, Action> run)
    {
        foreach (var latency in new[] { .08, .3 })
            run($"Navigation continuity FirstA arrow bridge with {latency * 1000:F0}ms solves and unrelated hazards", () => Bridge(latency));

        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
            run($"Navigation continuity {course} actual navigation dispatch under changing snapshots", () =>
            {
                var start = BlundervilleRoutes.VerifiedStart(course);
                var target = BlundervilleRoutes.Nodes(course, start).First(n => Vector3.Distance(n.Position, start) > 3).Position;
                Bridge(.15, course, start, target);
            });

        run("Navigation continuity late candidate starts ahead of current progress", () =>
        {
            using var runtime = Runtime(out var move);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            var path = new[] { Vector3.Zero, Vector3.UnitZ, Vector3.UnitZ * 4, Vector3.UnitZ * 10 };
            var solution = SafeZoneSolution.Succeeded(snapshot.Version, Epoch, Epoch, path[^1],
                path.Select(p => TimedWaypoint.Pass(p, Epoch.AddSeconds(p.Z / 6))).ToArray());
            Dispatch(runtime, solution, Vector3.UnitZ * 3, snapshot, Epoch.AddSeconds(.5));
            Check(move.GetStatus().CurrentPoint is { Position.Z: >= 3 }, "Delayed candidate sent the player backward to an already passed waypoint.");
        });

        run("Navigation continuity keeps fresh solve age while executing a stable route", () =>
        {
            using var runtime = Runtime(out _);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            var first = Solve(snapshot, Vector3.Zero, Epoch);
            Dispatch(runtime, first, Vector3.Zero, snapshot, Epoch);
            var next = Solve(snapshot, Vector3.UnitZ, Epoch.AddSeconds(.15));
            Set(runtime.MovementStatus, "_latestSolution", next);
            Dispatch(runtime, next, Vector3.UnitZ, snapshot, Epoch.AddSeconds(.15));
            Check(ReferenceEquals(Get(runtime.MovementStatus, "_latestSolution"), next), "Keeping the route restored an expired solve and caused immediate repeated background work.");
        });

        run("Navigation continuity repeated hold requests do not clear the cached result", () =>
        {
            using var runtime = Runtime(out var move);
            var owner = new object();
            runtime.RequestAutomaticMovement(owner, true, true);
            var solution = Solve(runtime.Manager.BuildSnapshot(Epoch), Vector3.Zero, Epoch);
            Set(runtime.MovementStatus, "_latestSolution", solution);
            for (var i = 0; i < 20; ++i) runtime.RequestAutomaticMovement(owner, true, true);
            Check(ReferenceEquals(Get(runtime.MovementStatus, "_latestSolution"), solution), "Refreshing an existing hold repeatedly discarded cached navigation.");
            Check(!move.IsRunning && !runtime.Setting.EffectiveEnableAutoMovement, "Hold allowed movement.");
        });

        run("Navigation continuity changed goal redirects even if the old route is safe", () =>
        {
            using var runtime = Runtime(out var move);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Solve(snapshot, Vector3.Zero, Epoch), Vector3.Zero, snapshot, Epoch);
            runtime.Manager.ClearGoalAreas();
            runtime.Manager.AddGoalArea(new CircleTemplate(.8f), new StaticTransform(new(10, 0, 0), 0), [GoalWindow.Persistent(Epoch, 100)]);
            snapshot = runtime.Manager.BuildSnapshot(Epoch.AddSeconds(.2));
            Dispatch(runtime, Solve(snapshot, Vector3.Zero, Epoch.AddSeconds(.2)), Vector3.Zero, snapshot, Epoch.AddSeconds(.2));
            Check(move.GetStatus().FinalPoint is { Position.X: > 9 }, "A new mechanism goal left the player following the old goal.");
        });

        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
            run($"Navigation continuity {course} radar background reuses the same clipped floor", () =>
            {
                var boundary = BlundervilleRoutes.Boundary(course);
                var layout = MasterPlayer.Radar.RadarArenaLayout.Create(boundary, boundary.Position, Vector2.Zero, 150, 0);
                var clip = new MasterPlayer.Radar.RadarClipRegion(layout.Outline, Vector2.Zero, 150);
                var reused = clip.ClipPolygon(clip.Outline).ToArray();
                var independent = clip.ClipPolygon(clip.Outline.ToArray()).ToArray();
                static double Area(IReadOnlyList<Vector2> p)
                {
                    double area = 0;
                    for (var i = 0; i < p.Count; ++i) area += (double)p[i].X * p[(i + 1) % p.Count].Y - (double)p[i].Y * p[(i + 1) % p.Count].X;
                    return Math.Abs(area) / 2;
                }
                Check(Math.Abs(reused.Sum(Area) - independent.Sum(Area)) < .1, "Reused background changed the clipped floor area.");
                Check(reused.SelectMany(p => p).All(p => clip.Contains(p)), "Background fill crossed the concave boundary.");
            });

        foreach (var change in new[] { "obstacle", "boundary", "danger" })
            run($"Navigation continuity {change} still invalidates an executing route", () =>
            {
                using var runtime = Runtime(out var move);
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                var old = Solve(snapshot, Vector3.Zero, Epoch);
                Dispatch(runtime, old, Vector3.Zero, snapshot, Epoch);
                if (change == "boundary") runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 2));
                else if (change == "obstacle") runtime.Manager.AddObstacle(new CircleTemplate(2), new StaticTransform(Vector3.UnitZ * 5, 0));
                else runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.UnitZ * 5, 0), [new DangerWindow(Epoch, null)]);
                snapshot = runtime.Manager.BuildSnapshot(Epoch.AddSeconds(.1));
                Dispatch(runtime, old, Vector3.Zero, snapshot, Epoch.AddSeconds(.1));
                Check(!move.IsRunning, "A safety change was ignored to preserve continuity.");
            });

        run("Navigation continuity arrow route preserves real holes without generating a curved stream of waypoints", () =>
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "arrow-bridge-021626.json")));
            foreach (var sample in fixture.RootElement.GetProperty("positions").EnumerateArray())
            {
                var coords = sample.GetProperty("position").EnumerateArray().Select(p => p.GetSingle()).ToArray();
                var position = new Vector3(coords[0], coords[1], coords[2]);
                using var runtime = Runtime(out _);
                runtime.Manager.ClearGoalAreas();
                using var api = new ScriptSafeZoneApi();
                api.ShareManagerForTest(runtime.Manager);
                api.UtcNowForTest = Epoch;
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
                new BlundervilleNavigation(runtime, api, new()).Apply(state, new(new(-200, 10, -290), false, false, null, ""),
                    new(_ => { }), Epoch, position, true, false, false);
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                var solution = Solve(snapshot, position, Epoch, true);
                Check(solution.IsSuccess && solution.ExecutablePath.Count is >= 2 and <= 8, $"Arrow route retained short smoothed steering targets: {solution.DescribeOutcome()} at {position}.");
                for (var i = 1; i < solution.ExecutablePath.Count; ++i)
                    Check(ArenaBoundaryTraversal.ContainsSegment(snapshot.ArenaBoundary!, solution.ExecutablePath[i - 1].Position, solution.ExecutablePath[i].Position), "Straightening cut through a hole in the bridge.");
            }
        });
    }

    private static void Bridge(double latency, BlundervilleCourse course = BlundervilleCourse.FirstA, Vector3? from = null, Vector3? destination = null)
    {
        var now = Epoch;
        var move = new TrackingMove();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi();
        api.ShareManagerForTest(runtime.Manager);
        var navigation = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        var stage = course switch { BlundervilleCourse.FirstA => 20013u, BlundervilleCourse.FirstB => 20014u,
            BlundervilleCourse.Ring => 20015u, BlundervilleCourse.Delivery => 20016u, _ => 20017u };
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, stage, 0, 0, 0, Epoch);
        var encounter = new BlundervilleEncounter(_ => { });
        var position = from ?? new Vector3(-200.061f, 9.995f, -247.456f);
        var target = destination ?? new Vector3(-200, 10.03f, -290);
        var forward = Vector3.Normalize(new Vector3(target.X - position.X, 0, target.Z - position.Z));
        SafeZoneSolution? pending = null, accepted = null;
        var ready = Epoch;
        var hazard = default(SafeZoneAreaHandle);
        var backwards = 0f;
        for (var tick = 0; tick < 800; ++tick)
        {
            now = Epoch.AddSeconds(tick * .01);
            if (tick % 10 == 0)
            {
                api.UtcNowForTest = now;
                navigation.Apply(state, new(target, false, false, null, ""), encounter, now, position, true, false, false);
                runtime.Manager.RemoveArea(hazard);
                hazard = runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(new(-210, 10, -260), 0), [new DangerWindow(now, null)]);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && now >= ready) { accepted = pending; pending = null; }
            if (pending == null)
            {
                Check(runtime.Setting.RuntimePreferStraightPaths, "The race did not request straight input segments.");
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, Player(position, snapshot), new(6, 6, 0, false, 6, 6), options, now);
                ready = now.AddSeconds(latency);
            }
            Dispatch(runtime, accepted, position, snapshot, now);
            var next = move.Advance(position, .01f);
            backwards += MathF.Max(0, -Vector3.Dot(next - position, forward));
            position = next;
            if (Vector2.Distance(new(position.X, position.Z), new(target.X, target.Z)) < 1) break;
        }
        Console.WriteLine($"CONTINUITY latency={latency:F2}s dispatch={move.MoveCount}, stops={move.StopCount}, reverse={backwards:F3}m end={position}");
        Check(Vector2.Distance(new(position.X, position.Z), new(target.X, target.Z)) < 1, "Continuous dispatch did not reach the route target.");
        Check(move.MoveCount <= 3, $"Unrelated updates restarted GreenMove {move.MoveCount} times.");
        Check(backwards < .05f, "Background candidates made the player turn back on the bridge.");
    }

    private static SafeZoneRuntime Runtime(out TrackingMove move)
    {
        move = new();
        var runtime = new SafeZoneRuntime(move, () => false, () => false);
        runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
        runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 25));
        runtime.Manager.AddGoalArea(new CircleTemplate(.8f), new StaticTransform(Vector3.UnitZ * 10, 0), [GoalWindow.Persistent(Epoch, 100)]);
        return runtime;
    }
    private static SafeZoneSolution Solve(SafeZoneSnapshot snapshot, Vector3 position, DateTime now, bool straight = false)
        => new SafeZoneSolver().Solve(snapshot, Player(position, snapshot), new(6, 6, 0, false, 6, 6), new() { PreferStraightPaths = straight }, now);
    private static PlayerState Player(Vector3 p, SafeZoneSnapshot s) => new(p, 0, 6, false, false, 0, true, s.Version);
    private static void Dispatch(SafeZoneRuntime r, SafeZoneSolution? solution, Vector3 p, SafeZoneSnapshot s, DateTime now)
        => r.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(r.MovementStatus, [solution, Player(p, s), s, now]);
    private static object? Get(object o, string field) => o.GetType().GetField(field, Private)!.GetValue(o);
    private static void Set(object o, string field, object value) => o.GetType().GetField(field, Private)!.SetValue(o, value);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    private sealed class TrackingMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = [];
        private int _index;
        public int MoveCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null,
            _points.Count > 0 ? _points[^1] : null, _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { ++MoveCount; _points = points; _index = 0; }
        public void Stop() { ++StopCount; _points = []; _index = 0; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float seconds)
        {
            if (!IsRunning) return p;
            var delta = _points[_index].Position - p; delta.Y = 0;
            var length = delta.Length();
            if (length <= _points[_index].Tolerance) { ++_index; return p; }
            return p + delta / length * MathF.Min(length, seconds * 6);
        }
    }
}
