using System.Numerics;
using System.Reflection;
using System.Diagnostics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Spatial;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleBridgeRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Finish = new(-200, 13.565f, -367);

    public static void Run(Action<string, Action> run)
    {
        foreach (var delay in new[] { .08, .3 })
            run($"Blunderville bridge jump dispatch simulation with {delay:F2}s worker latency", () => SimulateJump(delay));
        run("Blunderville bridges reject unknown state and unrelated map indices", () =>
        {
            var t = new BlundervilleBridgeTracker();
            t.Map(13, 0x20001, Epoch);
            Check(!t.BridgeEnabled(14, Epoch) && t.BridgeNextDisappearAt(14, Epoch) == null, "An unknown bridge received fabricated availability.");
            t.Map(14, 0x10001, Epoch);
            Check(t.BridgeEnabled(14, Epoch) && t.BridgeNextDisappearAt(14, Epoch) == null, "Initial availability acquired a made-up collapse time.");
            t.Map(14, 0x12345678, Epoch);
            Check(!t.BridgeEnabled(14, Epoch), "An unrecognized map state kept the bridge enabled.");
        });
        run("Blunderville bridge prediction requires two complete measured cycles", () =>
        {
            var t = new BlundervilleBridgeTracker();
            Map(t, 14, 0x20001, 0); Map(t, 14, 0x80004, 4.5); Map(t, 14, 0x20001, 6);
            Check(t.BridgeNextDisappearAt(14, Epoch.AddSeconds(6)) == null, "One cycle was accepted as a reliable repeating timer.");
            Map(t, 14, 0x80004, 10.6); Map(t, 14, 0x20001, 12);
            var deadline = t.BridgeNextDisappearAt(14, Epoch.AddSeconds(12));
            Check(deadline is { } d && Math.Abs((d - Epoch).TotalSeconds - (16.55 - BlundervilleBridgeTracker.CollapseMargin)) < 0.001,
                "The measured median timer was replaced with an assumed period.");
        });
        run("Blunderville bridge collapse is immediate and reappearance preserves its cycle", () =>
        {
            var t = Learned();
            Map(t, 14, 0x80004, 16.5);
            Check(!t.BridgeEnabled(14, Epoch.AddSeconds(16.5)), "A vanished bridge was still navigable.");
            Check(t.BridgeNextAppearAt(14, Epoch.AddSeconds(16.5)) == Epoch.AddSeconds(18), "Reappearance did not use the measured absent interval.");
            Map(t, 14, 0x20001, 18);
            Check(t.BridgeEnabled(14, Epoch.AddSeconds(18)) && t.BridgeNextDisappearAt(14, Epoch.AddSeconds(18)) == Epoch.AddSeconds(22.5 - BlundervilleBridgeTracker.CollapseMargin),
                "The next measured appearance lost timing calibration.");
        });
        run("Blunderville missed collapse cannot revive through an expired prediction", () =>
        {
            var t = Learned();
            Check(!t.BridgeEnabled(14, Epoch.AddSeconds(16.6)), "A missed collapse packet kept the predicted support active.");
            Check(t.BridgeNextDisappearAt(14, Epoch.AddSeconds(17)) == null, "An expired timer was returned as a future deadline.");
            Check(!t.BridgeEnabled(14, Epoch.AddSeconds(29)), "A stale packet retained support indefinitely.");
        });
        run("Blunderville bridge duplicate state does not move its collapse time", () =>
        {
            var t = Learned();
            Map(t, 14, 0x20001, 15.2);
            Check(t.BridgeNextDisappearAt(14, Epoch.AddSeconds(15.2)) == Epoch.AddSeconds(16.5 - BlundervilleBridgeTracker.CollapseMargin), "A duplicate appearance extended the safe window.");
        });
        run("Blunderville a single late bridge packet keeps the earlier cycle deadline", () =>
        {
            var t = Learned();
            Map(t, 14, 0x80004, 16.5); Map(t, 14, 0x20001, 19);
            // The 2.5s absence is one late appearance packet. The fitted clock
            // survives, and the collapse follows the previous collapse by one
            // period instead of being pushed out by the delayed appearance.
            Check(t.BridgeNextDisappearAt(14, Epoch.AddSeconds(19)) == Epoch.AddSeconds(22.5 - BlundervilleBridgeTracker.CollapseMargin),
                "A delayed appearance discarded or extended the measured bridge cycle.");
        });
        run("Blunderville discontinuous bridge timing drops a false periodic fit", () =>
        {
            var t = Learned();
            Map(t, 14, 0x80004, 16.5); Map(t, 14, 0x20001, 19);
            Map(t, 14, 0x80004, 21.2); Map(t, 14, 0x20001, 24.4);
            Map(t, 14, 0x80004, 26.1); Map(t, 14, 0x20001, 29.9);
            Check(t.BridgeNextDisappearAt(14, Epoch.AddSeconds(29.9)) == null, "A mostly discontinuous bridge clock remained predictable.");
        });
        run("Blunderville bridge replay seek and reset discard all support and timers", () =>
        {
            var t = Learned();
            Map(t, 14, 0x20001, 1);
            Check(!t.BridgeEnabled(15, Epoch.AddSeconds(1)) && t.BridgeNextDisappearAt(14, Epoch.AddSeconds(1)) == null, "A replay seek retained another bridge's future state.");
            t.Reset();
            Check(!t.BridgeEnabled(14, Epoch.AddSeconds(1)), "Reset retained bridge support.");
        });
        run("Blunderville bridge deadline overflow fails safely", () =>
        {
            var t = new BlundervilleBridgeTracker();
            var start = DateTime.MaxValue.AddSeconds(-13);
            foreach (var (seconds, state) in new (double, uint)[] { (0, 0x20001), (4.5, 0x80004), (6, 0x20001), (10.5, 0x80004), (12, 0x20001) })
                t.Map(14, state, start.AddSeconds(seconds));
            Check(t.BridgeNextDisappearAt(14, start.AddSeconds(12)) == null && !t.BridgeEnabled(14, start.AddSeconds(12)),
                "An overflowed deadline produced valid bridge support.");
        });
        run("Blunderville bridge planner stays scoped to the verified bridge section", () =>
        {
            var p = new BlundervilleBridgePlanner();
            Check(p.Plan(Learned(), Epoch.AddSeconds(15), new(-200, 10, -290), Finish, true, false) == null, "The bridge planner took over an unrelated zone.");
            Check(p.Plan(Learned(), Epoch.AddSeconds(15), new(float.NaN, 13.565f, -338), Finish, true, false) == null, "A nonfinite player coordinate produced steering.");
        });
        run("Blunderville bridge entry waits for observed timing and chooses the longer safe lane", () =>
        {
            var p = new BlundervilleBridgePlanner();
            var waiting = p.Plan(new(), Epoch.AddSeconds(15), new(-200, 13.565f, -320), Finish, true, false);
            Check(waiting is { Hold: true, Jump: false }, "Unknown bridge timing permitted entry.");
            var entry = p.Plan(Learned(), Epoch.AddSeconds(15.2), new(-200, 13.565f, -320), Finish, true, false);
            Check(entry is { Hold: false } && entry.Target.X == -203.5f && entry.Target.Z > -322.928f,
                "Entry did not select the reachable bridge with more remaining time.");
            var aligned = p.Plan(Learned(), Epoch.AddSeconds(15.5), new(-203.5f, 13.565f, -321.7f), Finish, true, false);
            Check(aligned is { Hold: false } && aligned.Target.X == -203.5f && aligned.Target.Z < -322.928f,
                "An aligned entry did not advance along the selected bridge.");
        });
        run("Blunderville bridge switch approaches the edge before jumping the measured gap", () =>
        {
            var p = new BlundervilleBridgePlanner(); var t = Learned();
            var preparation = p.Plan(t, Epoch.AddSeconds(15.3), new(-196.5f, 13.565f, -338), Finish, true, false);
            Check(preparation is { Hold: false, Jump: false, CrossingCorridor: not null } && preparation.Target.X == -198.5f,
                "A seven-metre center-to-center jump replaced edge preparation.");
            var takeoff = p.Plan(t, Epoch.AddSeconds(15.6), new(-198.3f, 13.565f, -338.8f), Finish, true, false);
            Check(takeoff is { Jump: true, Hold: false, CrossingCorridor: not null } && Math.Abs(takeoff.Target.X + 201.85f) < 0.01f,
                "Takeoff did not target the neighbouring bridge's near edge.");
            Check(ReferenceEquals(preparation!.CrossingCorridor, takeoff!.CrossingCorridor), "Takeoff replaced the prepared crossing corridor.");
            Check(takeoff!.CrossingCorridor!.Contains(new(-200, 13.565f, -339.5f)), "The actual airborne crossing was excluded from its temporary corridor.");
        });
        run("Blunderville bridge airborne landing and corridor remain stable until grounded", () =>
        {
            var p = new BlundervilleBridgePlanner(); var t = Learned();
            p.Plan(t, Epoch.AddSeconds(15.3), new(-196.5f, 13.565f, -338), Finish, true, false);
            var takeoff = p.Plan(t, Epoch.AddSeconds(15.6), new(-198.3f, 13.565f, -338.8f), Finish, true, false)!;
            var airborne = p.Plan(t, Epoch.AddSeconds(15.9), new(-200.1f, 15.3f, -339.5f), new(-196.5f, 13.565f, -345), false, true)!;
            Check(airborne.Target == takeoff.Target && ReferenceEquals(airborne.CrossingCorridor, takeoff.CrossingCorridor) && !airborne.Jump,
                "Airborne steering changed landing or repeated a jump.");
            var landed = p.Plan(t, Epoch.AddSeconds(16.2), takeoff.Target, Finish, true, false)!;
            Check(landed.CrossingCorridor == null && landed.Target.X == -203.5f && !landed.Jump, "A completed landing stayed latched or steered back to the vanished lane.");
        });
        run("Blunderville actual safe-zone grid can reach the prepared bridge takeoff edge", () =>
        {
            var p = new BlundervilleBridgePlanner(); var now = Epoch.AddSeconds(15.3);
            var player = new Vector3(-196.5f, 13.565f, -338);
            var prep = p.Plan(Learned(), now, player, Finish, true, false)!;
            var combinedType = typeof(BlundervilleNavigation).GetNestedType("CrossingCorridor", BindingFlags.NonPublic)!;
            var localType = typeof(BlundervilleNavigation).GetNestedType("LocalCorridor", BindingFlags.NonPublic)!;
            var combined = (IGeometryShape)Activator.CreateInstance(combinedType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, [BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), prep.CrossingCorridor!], null)!;
            foreach (var radius in new[] { 28f, 8f })
            {
                var local = (IGeometryShape)Activator.CreateInstance(localType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null, [combined, player, radius], null)!;
                var snapshot = new SafeZoneSnapshot(1, now, local, [], [],
                    [new(new(1, AreaKind.Goal), new CircleGeometry(prep.Target, 0.2f), [GoalWindow.Persistent(now, 100)])]);
                var watch = Stopwatch.StartNew();
                var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, 1), new(6, 6, 0, false, 6, 6),
                    new() { GridResolution = 0.5f, HardSafetyMargin = 0.5f, GenerateDebugData = true }, now);
                watch.Stop();
                var debug = solution.DebugData;
                Console.WriteLine($"SIM Bridge prep radius={radius}: {watch.Elapsed.TotalMilliseconds:F1} ms, grid={debug?.GridResolution:F3}, cells={debug?.GridColumns}x{debug?.GridRows}, target={solution.TargetPosition}");
                Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2 && solution.ExecutablePath[^1].Position.X + 0.2f <= -198.2f,
                    "Hard-margin rasterization left the takeoff edge unreachable.");
            }
        });
        run("Blunderville bridge endpoint jumps onto the verified finish platform", () =>
        {
            var p = new BlundervilleBridgePlanner(); var t = Learned();
            var jump = p.Plan(t, Epoch.AddSeconds(15.4), new(-203.5f, 13.565f, -351.2f), Finish, true, false)!;
            Check(jump.Jump && jump.Target.Z < -353.072f && jump.Target.X == -203.5f && jump.CrossingCorridor != null,
                "The final jump turned inward before reaching the platform.");
            p.Plan(t, Epoch.AddSeconds(15.7), new(-203.5f, 15.3f, -353), Finish, false, true);
            Check(p.Plan(t, Epoch.AddSeconds(16), jump.Target, Finish, true, false) == null, "Finishing the bridge retained its takeover.");
        });
    }

    private static BlundervilleBridgeTracker Learned()
    {
        var t = new BlundervilleBridgeTracker();
        var events = new List<(double Seconds, byte Index, uint State)>();
        for (byte index = 14; index <= 17; index++)
        {
            var offset = index % 2 == 0 ? 0 : 3;
            foreach (var (seconds, state) in new (double, uint)[] { (0, 0x20001), (4.5, 0x80004), (6, 0x20001), (10.5, 0x80004), (12, 0x20001) })
                events.Add((seconds + offset, index, state));
        }
        foreach (var e in events.OrderBy(e => e.Seconds)) Map(t, e.Index, e.State, e.Seconds);
        return t;
    }

    private static void SimulateJump(double latency)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var now = Epoch.AddSeconds(15.3);
        var move = new JumpMove();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        var tracker = Learned(); var planner = new BlundervilleBridgePlanner(); var gate = new BlundervilleJumpExecution();
        var encounter = new BlundervilleEncounter(_ => { });
        var p = new Vector3(-196.5f, 13.565f, -338);
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0;
        double? launch = null; var finished = false;
        for (var tick = 0; tick < 300; ++tick)
        {
            var t = tick * .01; now = Epoch.AddSeconds(15.3 + t); move.Now = now;
            var flying = launch is { } at && t - at < .7;
            if (tick == 120) { Map(tracker, 14, 0x80004, 16.5); Map(tracker, 16, 0x80004, 16.5); }
            if (tick % 10 == 0)
            {
                var plan = planner.Plan(tracker, now, p, Finish, !flying, flying)!;
                if (gate.Prepare(plan.CrossingCorridor as BlundervilleJumpCorridor, p) is { } execution)
                    plan = plan with { Target = execution.Target, Hold = execution.Hold, Jump = false, CrossingCorridor = execution.Corridor };
                api.UtcNowForTest = now;
                nav.Apply(state, new(plan.Target, plan.Hold, plan.Jump, null, plan.Hint), encounter, now, p, true, false, false, plan.CrossingCorridor);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), options, now);
                ready = t + latency; nextSolve = ready + .1;
            }
            if (gate.Sample(now, p, !flying, flying, runtime.MovementStatus.HasControl, move.GetStatus()))
            {
                Check(launch == null, "A single gap launched more than once.");
                Check(nav.CommitJump(gate.Landing!.Value, p, gate.MeasuredSpeed, now), "Landing route could not be committed.");
                gate.ConfirmLaunch(now); launch = t; flying = true;
            }
            if (!gate.InFlight) nav.ReleaseJump();
            if (gate.NeedsRunupRetry) nav.StopForRunup();
            runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus,
                [accepted, new PlayerState(p, 0, 6, false, false, 0, true, snapshot.Version), snapshot, now]);
            p = move.Advance(p);
            if (!flying)
                Check(Math.Abs(p.X + 196.5f) < 2.425f && 15.3 + t < 16.5 || Math.Abs(p.X + 203.5f) < 2.425f,
                    $"Walking or landing entered an unsupported bridge gap at {p}, t={t:F2}.");
            if (launch is { } started && t >= started + .7)
            {
                Check(Math.Abs(p.X + 203.5f) < 2.425f, "Flight ended short of the neighbouring bridge.");
                finished = true; break;
            }
        }
        Check(finished, "Bridge switch never completed a jump.");
    }

    private sealed class JumpMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _points = points; _index = 0; _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void ClearQueue() => Stop(); public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p)
        {
            if (!IsRunning || Now < _ready) return p;
            var delta = _points[_index].Position - p; delta.Y = 0; var length = delta.Length();
            if (length <= _points[_index].Tolerance) { ++_index; return p; }
            return p + delta / length * Math.Min(length, .06f);
        }
    }

    private static void Map(BlundervilleBridgeTracker t, byte index, uint state, double seconds) => t.Map(index, state, Epoch.AddSeconds(seconds));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
