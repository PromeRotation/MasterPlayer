using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// 2026-10-13 route research: MP stopped before each of the three FirstA ramp
// swinger rows (17:44, 16:55, 02:33: 10.3–12.4s for z -127..-150 against 3.6s
// for the best humans). Replays the recorded swinger animations from every
// arrival phase of their 4.2s pattern, through the script's chain, and
// compares the timed corridor with the solver alone.
internal static class BlundervilleSwingerRampRegression
{
    // The animation replay's clock.
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly (string Fixture, double First)[] Fixtures =
        [("animation-firsta-030838.json", 2), ("animation-firsta-041020.json", 2), ("animation-start-004353.json", 2)];

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville swinger ramp corridor crosses the recorded rows without stopping", () =>
        {
            // The script compiles its planners on a worker when it starts.
            BlundervilleWarmup.Run();
            var results = new List<(string Fixture, double Phase, double Corridor, int CorridorHits, double Solver, int SolverHits)>();
            foreach (var (fixture, first) in Fixtures)
                for (var phase = first; phase < first + 4.2; phase += .35)
                {
                    var corridor = Simulate(fixture, phase, true); var solver = Simulate(fixture, phase, false);
                    results.Add((fixture, phase, corridor.Elapsed, corridor.Hits, solver.Elapsed, solver.Hits));
                    Console.WriteLine($"SWINGER {fixture[10..]} phase {phase:F2}: corridor {corridor.Elapsed:F2}s hits {corridor.Hits}, solver {solver.Elapsed:F2}s hits {solver.Hits}");
                }
            Console.WriteLine($"SWINGER slowest lane planner scan {LaneMs:F1}ms; after warm-up {Spread()}; first {string.Join(" ", LaneScans.Take(8).Select(x => x.ToString("F1")))}; top {string.Join(" ", LaneScans.Select((x, i) => (x, i)).OrderByDescending(v => v.x).Take(5).Select(v => $"{v.x:F1}@{v.i}"))}");
            Console.WriteLine($"SWINGER corridor mean {results.Average(r => r.Corridor):F2}s max {results.Max(r => r.Corridor):F2}s hits {results.Sum(r => r.CorridorHits)};"
                + $" solver mean {results.Average(r => r.Solver):F2}s max {results.Max(r => r.Solver):F2}s hits {results.Sum(r => r.SolverHits)}");
            // 2026-10-13: corridor mean 6.1s (max 7.2s), the solver alone 11.8s
            // (max 13.9s; live MP 10.3–12.4s), no sweep hits either way.
            Check(results.Sum(r => r.CorridorHits) == 0, "The swinger corridor walked into a recorded sweep.");
            Check(results.Average(r => r.Corridor) < 7 && results.Max(r => r.Corridor) < 8.5, "The swinger corridor no longer crosses the ramp without stopping.");
        });
        // The slope rows (z -219.9..-239.05) and the 3802 bomb gained at -212.6:
        // it explodes on movement within ~0.6s of its 5.008s expiry.
        run("Blunderville swinger slope corridor holds the bomb clear of the sweeps", () =>
        {
            var results = new List<(double Phase, (double Elapsed, int Hits, bool Exploded) Corridor, (double Elapsed, int Hits, bool Exploded) Solver)>();
            for (var phase = 10.0; phase < 14.2; phase += .35)
            {
                var corridor = Simulate("animation-firsta-030838.json", phase, true, true); var solver = Simulate("animation-firsta-030838.json", phase, false, true);
                results.Add((phase, corridor, solver));
                Console.WriteLine($"SLOPE phase {phase:F2}: corridor {corridor.Elapsed:F2}s hits {corridor.Hits} bomb {(corridor.Exploded ? "EXPLODED" : "ok")},"
                    + $" solver {solver.Elapsed:F2}s hits {solver.Hits} bomb {(solver.Exploded ? "EXPLODED" : "ok")}");
            }
            Console.WriteLine($"SLOPE slowest lane planner scan {LaneMs:F1}ms; after warm-up {Spread()}");
            Console.WriteLine($"SLOPE corridor mean {results.Average(r => r.Corridor.Elapsed):F2}s max {results.Max(r => r.Corridor.Elapsed):F2}s hits {results.Sum(r => r.Corridor.Hits)}"
                + $" explosions {results.Count(r => r.Corridor.Exploded)}; solver mean {results.Average(r => r.Solver.Elapsed):F2}s max {results.Max(r => r.Solver.Elapsed):F2}s"
                + $" hits {results.Sum(r => r.Solver.Hits)} explosions {results.Count(r => r.Solver.Exploded)}");
            // 2026-10-13: corridor mean 8.8s (max 9.2s), the solver with the early
            // bomb stop 13.3s (max 15.1s; live MP 12.7–14.0s).
            Check(results.All(r => r.Corridor.Hits == 0 && !r.Corridor.Exploded), "The slope corridor was hit or set off the bomb.");
            Check(results.Average(r => r.Corridor.Elapsed) < 10, "The slope corridor no longer crosses the rows without long holds.");
        });
    }

    // Slowest main-thread lane planner scan in the runs so far.
    internal static double LaneMs;
    internal static readonly List<double> LaneScans = [];
    private static string Spread() { var v = LaneScans.Skip(5).OrderBy(x => x).ToArray(); return v.Length == 0 ? "" : $"p50 {v[v.Length / 2]:F1}ms p95 {v[v.Length * 95 / 100]:F1}ms p99 {v[v.Length * 99 / 100]:F1}ms max {v[^1]:F1}ms"; }

    private static (double Elapsed, int Hits) Simulate(string fixture, double phase, bool corridor)
    {
        var (elapsed, hits, _) = Simulate(fixture, phase, corridor, false);
        return (elapsed, hits);
    }

    private static (double Elapsed, int Hits, bool Exploded) Simulate(string fixture, double phase, bool corridor, bool slope)
    {
        using var replay = new BlundervilleAnimationRegression.Recording(fixture);
        var now = Epoch; var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner();
        var lanes = new BlundervilleFinalPlanner(slope ? BlundervilleFinalPlanner.FirstASlope : BlundervilleFinalPlanner.FirstARamp);
        var p = slope ? new Vector3(-200, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, -209.5f), -209.5f) : new Vector3(-208.76f, 11.28f, -126.3f);
        var previous = p; var direct = false; DateTime? bomb = null; var lastMove = DateTime.MinValue; var exploded = false;
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = phase; var nextScan = phase;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0; var hits = 0;
        const double frame = 1.0 / 30;
        for (var tick = 0; tick < 30 * 30; ++tick)
        {
            var t = phase + tick * frame; now = Epoch.AddSeconds(t); move.Now = now;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                state.SynchronizeStatuses(bomb is { } b && now < b ? [new(3802, 0, b)] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null);
                // Mirrors BlundervilleScript: the FirstA lane planner, then the bomb stop.
                var laneWatch = System.Diagnostics.Stopwatch.StartNew();
                if (corridor) decision = lanes.Apply(decision, state, replay.Encounter.TraversalHazards(now).ToArray(), p, now, nav.GetDirectTraversalExecution(now));
                LaneMs = Math.Max(LaneMs, laneWatch.Elapsed.TotalMilliseconds); if (corridor) LaneScans.Add(laneWatch.Elapsed.TotalMilliseconds);
                if (!decision.Hold && !(corridor && lanes.HoldsBomb && !state.MustStopForAccelerationBomb(now))
                    && BlundervilleBombStop.StopEarly(state, replay.Encounter.TraversalHazards(now), p, now))
                    decision = decision with { Hold = true, Jump = false };
                direct = decision.DirectTraversal;
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            var capability = new MovementCapability(6, 6, 0, false, 6, 6);
            if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, capability, now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                    pending = new SafeZoneSolver().Solve(snapshot, player, capability, options, now);
                    ready = t + .12; nextSolve = ready + .1;
                }
                // A held runtime has reset its movement; nothing is dispatched.
                if (!runtime.Setting.RuntimeMovementHeld)
                    runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            // 19 recorded expiries: still >= 0.62s never exploded, <= 0.47s always did.
            if (bomb is { } expiry && !exploded && now.AddSeconds(frame) >= expiry && now < expiry && (expiry - lastMove).TotalSeconds < .6) exploded = true;
            previous = p; p = move.Advance(p, (float)frame);
            p = p with { Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstA, p.Z) };
            if (Vector3.DistanceSquared(previous, p) > 1e-6f) lastMove = now.AddSeconds(frame);
            if (slope && bomb == null && p.Z <= -212.6f) bomb = now.AddSeconds(frame + 5.008);
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(p), $"Movement left the floor at {p} ({fixture} {phase:F2}).");
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
            {
                var effect = impacts[hitIndex++]; if (effect[0].GetDouble() < phase || effect[3].GetUInt32() != 34716) continue;
                var actor = replay.Actors[effect[2].GetUInt64()];
                if (new RectangleGeometry(actor.Position, effect[4].GetSingle(), 2, 3).Contains(p, .15f)) ++hits;
            }
            if (p.Z < (slope ? -247.5f : -151.5f) && (bomb is not { } done || now > done)) return (t - phase, hits, exploded);
        }
        return (30, hits, exploded);
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, IsRunning ? Math.Max(0, _points.Count - _index - 1) : 0);
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            while (IsRunning && Flat(_points[_index].Position - p).Length() <= Math.Max(_points[_index].Tolerance, .01f)) ++_index;
            if (!IsRunning || Now < _ready) return p;
            var d = Flat(_points[_index].Position - p); var distance = d.Length();
            return p + d / distance * MathF.Min(distance, 6 * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
