using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Diagnostics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleJumpExecutionRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville jump execution reproduces recorded standing versus running launch speeds", () =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/Blunderville/jumps-firstb-014207.json")));
            foreach (var episode in doc.RootElement.GetProperty("episodes").EnumerateArray())
            {
                var samples = episode.GetProperty("samples").EnumerateArray().ToArray();
                var air = Array.FindIndex(samples, s => s.GetProperty("flags").GetInt32() == 1072);
                Check(air >= 0, "Fixture lost the recorded launch marker.");
                var from = samples[air]; var endTime = from.GetProperty("t").GetDouble() + .25;
                var to = samples.Skip(air + 1).First(s => s.GetProperty("t").GetDouble() >= endTime);
                Vector3 Point(JsonElement s) { var p = s.GetProperty("p").EnumerateArray().Select(v => v.GetSingle()).ToArray(); return new(p[0], 0, p[2]); }
                var speed = Vector3.Distance(Point(from), Point(to)) / (to.GetProperty("t").GetDouble() - from.GetProperty("t").GetDouble());
                Console.WriteLine($"RECORDED JUMP {episode.GetProperty("name").GetString()}: {speed:F2}m/s");
                Check(episode.GetProperty("landed").GetBoolean() ? speed > 3.8 : speed is > 2 and < 3,
                    "The failed/landed flight-speed evidence changed.");
            }
        });
        foreach (var speed in new[] { 0f, 2.5f, 6f })
            run($"Blunderville jump execution measures run-up at {speed:F1}m/s", () =>
            {
                var gate = new BlundervilleJumpExecution(); var link = Link(1);
                var direction = Direction(link); var p = link.Takeoff - direction * 1.15f;
                gate.Prepare(link, p); var launched = false;
                for (var i = 0; i < 150; ++i)
                {
                    var now = Epoch.AddSeconds(i * .01);
                    if (gate.Sample(now, p, true, false, true, Moving(link.Landing))) { launched = true; break; }
                    if (gate.NeedsRunupRetry) break;
                    p += direction * speed * .01f;
                }
                Check(launched == (speed >= 4.8), "A stopped/slow character launched, or a measured run failed to launch.");
            });
        foreach (var interruption in new[] { "changed route", "lost owner", "airborne", "rewind", "clear" })
            run($"Blunderville jump execution rejects {interruption} before launch", () =>
            {
                var gate = new BlundervilleJumpExecution(); var link = Link(1); var direction = Direction(link);
                gate.Prepare(link, link.Takeoff - direction * 1.15f);
                for (var i = 0; i < 12; ++i)
                    Check(!gate.Sample(Epoch.AddSeconds(i * .01), link.Takeoff - direction * (1.15f - i * .06f), true, false, true, Moving(link.Landing)), "Premature launch.");
                if (interruption == "clear") gate.Clear();
                var allowed = gate.Sample(interruption == "rewind" ? Epoch.AddSeconds(-1) : Epoch.AddSeconds(.2), link.Takeoff,
                    interruption != "airborne", interruption == "airborne", interruption != "lost owner",
                    Moving(interruption == "changed route" ? link.Takeoff : link.Landing));
                Check(!allowed, "An invalid run-up authorized jump.");
            });
        run("Blunderville jump execution releases only after observed landing and bounds absent confirmation", () =>
        {
            var gate = new BlundervilleJumpExecution(); var link = Link(1);
            gate.Prepare(link, link.Takeoff - Direction(link)); gate.ConfirmLaunch(Epoch);
            gate.Sample(Epoch.AddSeconds(.1), link.Takeoff + Vector3.UnitY, false, true, true, Moving(link.Landing));
            Check(gate.Prepare(null, link.Takeoff + Vector3.UnitY)?.Target == link.Landing, "A planner change replaced the airborne landing.");
            gate.Sample(Epoch.AddSeconds(.3), link.Takeoff + Vector3.UnitY, false, false, true, Moving(link.Landing));
            Check(gate.InFlight, "Clearing the jump condition before floor contact ended the flight.");
            gate.Sample(Epoch.AddSeconds(.5), link.Landing, true, false, true, Moving(link.Landing));
            Check(!gate.InFlight, "Real floor contact retained the flight lease.");
            gate.Clear(); gate.Prepare(link, link.Takeoff - Direction(link)); gate.ConfirmLaunch(Epoch);
            gate.Sample(Epoch.AddSeconds(.26), link.Takeoff, true, false, true, Moving(link.Landing));
            Check(!gate.InFlight && gate.NeedsRunupRetry, "A native action without takeoff walked through the gap.");
        });
        foreach (var side in new[] { -1, 1 })
            foreach (var latency in new[] { .08, .3 })
                foreach (var fps in new[] { 15, 30, 60 })
                    run($"Blunderville jump execution FirstB side {side} {latency:F2}s worker {fps}fps", () => Simulate(side, latency, fps));
        foreach (var jump in BlundervilleJumpRegression.RecordedJumps())
        {
            run($"Blunderville jump execution grounded run-up floor: {jump.Name}", () =>
            {
                var link = new BlundervilleJumpCorridor(jump.Takeoff, jump.Landing);
                var staging = link.Takeoff - Direction(link) * 1.15f;
                var floor = BlundervilleRoutes.Boundary(jump.Course);
                Check(ArenaBoundaryTraversal.ContainsSegment(floor, staging, link.Takeoff), $"Run-up is not supported: {staging} -> {link.Takeoff}.");
            });
            if (!jump.Name.Contains("entry"))
                run($"Blunderville jump execution delayed run-up and flight: {jump.Name}", () => Simulate(1, .3, 30, jump));
        }
        foreach (var side in new[] { -1, 1 })
            foreach (var latency in new[] { .08, .3 })
                run($"Blunderville jump execution clears second gap before forced movement {side} {latency:F2}s", () => ForcedApproach(side, latency));
        run("Blunderville jump execution forced movement chooses a complete supported displacement", () =>
        {
            var floor = BlundervilleRoutes.Boundary(BlundervilleCourse.FirstB);
            var p = new Vector3(8, 10.03f, -161);
            var watch = Stopwatch.StartNew();
            var plan = BlundervilleForcedMovement.Heading(floor, p, p - Vector3.UnitZ * 10);
            Check(plan.Safe, "A known long straight floor did not accept forced movement.");
            var end = p + new Vector3(MathF.Sin(plan.Heading), 0, MathF.Cos(plan.Heading)) * BlundervilleForcedMovement.Travel;
            Check(ArenaBoundaryTraversal.ContainsSegment(floor, p, end), "Forced movement crosses unsupported floor.");
            p = new(8.103f, 9.995f, -146.563f);
            plan = BlundervilleForcedMovement.Heading(floor, p, new(8, 10, -151.5f));
            Check(!plan.Safe, "The recorded last-second trapped position was falsely declared safe for a complete forced move.");
            Console.WriteLine($"FORCED full-path assessment: {watch.Elapsed.TotalMilliseconds:F2}ms");
        });
    }

    private static void Simulate(int side, double latency, int fps, BlundervilleJumpRegression.RecordedJump? recorded = null)
    {
        var now = Epoch; var move = new Movement();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        var course = recorded?.Course ?? BlundervilleCourse.FirstB;
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001,
            course == BlundervilleCourse.Ring ? 20015u : course == BlundervilleCourse.Final ? 20017u : 20014u, 0, 0, 0, now);
        var planner = new BlundervilleJumpPlanner(); var execution = new BlundervilleJumpExecution();
        var encounter = new BlundervilleEncounter(_ => { });
        var link = recorded == null ? Link(side) : new BlundervilleJumpCorridor(recorded.Takeoff, recorded.Landing);
        var direction = Direction(link);
        var p = recorded == null ? new Vector3(side * 10.239f, 10.03f, -131.701f) : link.Takeoff - direction * 1.15f;
        var frame = 1f / fps;
        Check(BlundervilleRoutes.Boundary(course).Contains(p), "Recorded grounded approach is outside the floor.");
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        double? launch = null; var launchSpeed = 0f; var lastSpeed = 0f; var stopsAtLaunch = 0;
        for (var tick = 0; tick < fps * 8; ++tick)
        {
            var t = tick / (double)fps; now = Epoch.AddSeconds(t); move.Now = now;
            var air = launch is { } at && t - at < .7;
            if (execution.Sample(now, p, !air, air, runtime.MovementStatus.HasControl, move.GetStatus()))
            {
                Check(launch == null && nav.CommitJump(execution.Landing!.Value, p, execution.MeasuredSpeed, now), "Jump did not own its dispatched landing.");
                launch = t; air = true; launchSpeed = lastSpeed >= 4.8f ? 6 : 2.5f;
                Check(launchSpeed == 6, "Stationary launch reproduced the recorded shortfall.");
                execution.ConfirmLaunch(now); stopsAtLaunch = move.Stops;
            }
            if (!execution.InFlight) nav.ReleaseJump();
            if (execution.NeedsRunupRetry) nav.StopForRunup();
            if (t >= nextScan)
            {
                var plan = planner.Plan(course, now, p, link.Landing, !air, air);
                var step = execution.Prepare(plan?.Corridor, p);
                var target = step?.Target ?? plan?.Target ?? link.Landing;
                api.UtcNowForTest = now;
                nav.Apply(state, new(target, false, false, null, ""), encounter, now, p, true, false, false, step?.Corridor ?? plan?.Corridor);
                nextScan = t + .1;
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, Player(p, snapshot), new(6, 6, 0, false, 6, 6), options, now);
                ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
            }
            // In flight, deliberately invalidate the ground solution to prove
            // that ordinary solver churn cannot remove the committed input.
            runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus,
                [air ? null : accepted, Player(p, snapshot), snapshot, now]);
            var next = move.Advance(p, frame, air ? launchSpeed : 6);
            lastSpeed = Vector3.Distance(next, p) / frame; p = next;
            if (air) Check(move.Stops == stopsAtLaunch, "Ground replanning stopped midair movement.");
            if (launch is { } started && t >= started + .7)
            {
                Check(Vector3.Dot((p - link.Takeoff) with { Y = 0 }, direction) >= 3.7f, $"Flight ended before landing floor: {p}.");
                Check(BlundervilleRoutes.Boundary(course).Contains(p), $"No floor at landing {p}.");
                return;
            }
        }
        throw new InvalidOperationException($"Did not finish running jump: side={side}, fps={fps}, p={p}, speed={execution.MeasuredSpeed}.");
    }

    private static void ForcedApproach(int side, double latency)
    {
        var now = Epoch; var move = new Movement();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, now);
        var planner = new BlundervilleRacePlanner(); var jumper = new BlundervilleJumpPlanner(); var gate = new BlundervilleJumpExecution();
        var encounter = new BlundervilleEncounter(_ => { }); var floor = BlundervilleRoutes.Boundary(BlundervilleCourse.FirstB);
        var p = new Vector3(side * 8.103f, 10.03f, -146.563f);
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        double? launch = null; float? facing = null; var landed = false;
        for (var tick = 0; tick < 241; ++tick)
        {
            var t = tick / 30.0; now = Epoch.AddSeconds(t); move.Now = now;
            var air = launch is { } at && t - at < .7;
            state.SynchronizeStatuses(t < 5 ? [new(3701, 0, Epoch.AddSeconds(5))] : [new(3737, 8, Epoch.AddSeconds(8))], now);
            if (state.CanJump(now) && gate.Sample(now, p, !air, air, runtime.MovementStatus.HasControl, move.GetStatus()))
            {
                Check(launch == null && nav.CommitJump(gate.Landing!.Value, p, gate.MeasuredSpeed, now), "Second gap failed to commit.");
                gate.ConfirmLaunch(now); launch = t; air = true;
            }
            if (!gate.InFlight || !state.CanJump(now)) nav.ReleaseJump();
            if (gate.NeedsRunupRetry) nav.StopForRunup();
            if (t >= nextScan)
            {
                var decision = planner.Update(state, now, p, !air, air, false, null);
                BlundervilleJumpCorridor? corridor = null;
                if (state.CanJump(now) && (!decision.Hold || gate.InFlight))
                {
                    var step = jumper.Plan(state.Course, now, p, decision.Target ?? p, !air, air);
                    step = gate.Prepare(step?.Corridor, p) ?? step;
                    if (step != null) { decision = decision with { Target = step.Target, Hold = false }; corridor = step.Corridor; }
                }
                else gate.Clear();
                if (decision.Facing != null) facing = decision.Facing;
                api.UtcNowForTest = now;
                nav.Apply(state, decision, encounter, now, p, true, false, false, corridor);
                nextScan = t + .1;
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve && t < 5)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, Player(p, snapshot), new(6, 6, 0, false, 6, 6), options, now);
                ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
            }
            if (!runtime.Setting.RuntimeMovementHeld)
                runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, Player(p, snapshot), snapshot, now]);
            if (t < 5) p = move.Advance(p, 1f / 30, 6);
            else
            {
                Check(facing != null && !air && landed, $"Forced movement began before a confirmed safe jump landing: {p}.");
                var heading = facing!.Value - MathF.PI / 2;
                var next = p + new Vector3(MathF.Sin(heading), 0, MathF.Cos(heading)) * .2f;
                Check(ArenaBoundaryTraversal.ContainsSegment(floor, p, next), $"Forced movement walked off floor at {p}, t={t:F2}.");
                p = next;
            }
            if (launch is { } began && !air && t > began + .7)
            { landed = true; Check(floor.Contains(p), $"Second jump missed landing: {p}."); }
        }
        Console.WriteLine($"FORCED APPROACH side={side} latency={latency:F2} launch={launch:F2}s final={p}");
    }
    private static PlayerState Player(Vector3 p, SafeZoneSnapshot s) => new(p, 0, 6, false, false, 0, true, s.Version);
    private static BlundervilleJumpCorridor Link(int side) => new(new(side * 10.25f, 10.03f, -134.2f), new(side * 11.9f, 10.03f, -138.4f));
    private static Vector3 Direction(BlundervilleJumpCorridor l) => Vector3.Normalize((l.Landing - l.Takeoff) with { Y = 0 });
    private static GreenMoveStatus Moving(Vector3 p) => new(true, new(p), new(p), 0, 1, 0);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private sealed class Movement : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now; public int Stops;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _points = points; _index = 0; _ready = Now.AddSeconds(.08); }
        public void Stop() { ++Stops; _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void ClearQueue() => Stop(); public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float seconds, float speed)
        {
            if (!IsRunning || Now < _ready) return p;
            while (IsRunning && Vector3.Distance(p, _points[_index].Position with { Y = p.Y }) <= Math.Max(_points[_index].Tolerance, .01f)) ++_index;
            if (!IsRunning) return p;
            var delta = (_points[_index].Position - p) with { Y = 0 }; var length = delta.Length();
            return p + delta / length * Math.Min(length, seconds * speed);
        }
    }
}
