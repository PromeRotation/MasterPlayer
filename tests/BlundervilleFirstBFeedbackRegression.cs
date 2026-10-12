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

internal static class BlundervilleFirstBFeedbackRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Fixture = "animation-firstb-231958.json";

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville FirstB latest measured solid wall interiors are not walkable", () =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "Blunderville", "firstb-walls-231958.json")));
            foreach (var wall in data.RootElement.GetProperty("walls").EnumerateArray())
            {
                var z = wall.EnumerateArray().Average(p => p[1].GetSingle());
                foreach (var x in new[] { -2.16f, 0, 2.16f })
                    Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.FirstB).Contains(new(x, 10, z + 2.8f)),
                        "The cached navmesh still permits walking through the solid diamond.");
            }
        });
        run("Blunderville FirstB latest burst forecast stays stable between contact packets and expires", () =>
        {
            var tracker = new BlundervilleRhythmTracker(); var actor = new BlundervilleActor(1, 16338, new(10, 10, -202));
            tracker.Effect(actor, 34773, actor.Position, 0, Epoch);
            tracker.Effect(actor, 34773, actor.Position, 0, Epoch.AddSeconds(6.1));
            var forecast = tracker.Hazards(Epoch.AddSeconds(6.1)).ToArray();
            Check(forecast.Length == 3, "A complete repeating burst failed to forecast the next animation.");
            tracker.Effect(actor, 34773, actor.Position, 0, Epoch.AddSeconds(6.5));
            Check(forecast.Zip(tracker.Hazards(Epoch.AddSeconds(6.5)), ReferenceEquals).All(same => same),
                "Individual contact packets rebuilt the continuous animation window.");
            Check(!tracker.Hazards(Epoch.AddSeconds(30)).Any(), "Lost effect signals left permanent spinning danger.");
            tracker.Effect(actor, 34773, actor.Position + Vector3.UnitX, 0, Epoch.AddSeconds(6.9));
            Check(tracker.Hazards(Epoch.AddSeconds(6.9)).Count() == 1, "A moved helper reused its old animation clock.");
            tracker.Reset(); Check(!tracker.Hazards(Epoch.AddSeconds(7)).Any(), "Reset retained the burst clock.");
        });
        run("Blunderville FirstB latest forced command retains heading through the recorded status gap", () =>
        {
            var state = Start(); var planner = new BlundervilleRacePlanner();
            var p = new Vector3(8.164f, 9.995f, -169.146f);
            var expiry = Epoch.AddSeconds(140.774);
            state.SynchronizeStatuses([new(3701, 0, expiry)], Epoch.AddSeconds(140.043));
            var aligned = planner.Update(state, Epoch.AddSeconds(140.043), p, true, false, false, null);
            Check(aligned.Hold && aligned.Facing != null, "The pending command did not align.");
            state.SynchronizeStatuses([], Epoch.AddSeconds(140.883));
            var gap = planner.Update(state, Epoch.AddSeconds(140.883), p, true, false, false, null);
            Check(gap.Hold && gap.Facing == aligned.Facing, "The countdown released facing before 3737 arrived.");
            var heading = gap.Facing!.Value - MathF.PI / 2;
            for (var d = 0f; d <= 18.5f; d += .1f)
                Check(BlundervilleRoutes.Boundary(state.Course).Contains(p + new Vector3(MathF.Sin(heading), 0, MathF.Cos(heading)) * d, -.15f),
                    "The recorded command runs off the opening platform.");
            state.SynchronizeStatuses([new(3737, 8, Epoch.AddSeconds(144))], Epoch.AddSeconds(141.079));
            var active = planner.Update(state, Epoch.AddSeconds(141.079), p, true, false, false, null);
            Check(active.Hold && active.Facing == null, "The active server movement retained the preparation lock.");
            state.SynchronizeStatuses([], Epoch.AddSeconds(144));
            Check(!planner.Update(state, Epoch.AddSeconds(144), new(8.164f, 10, -187.646f), true, false, false, null).Hold,
                "The finished command never resumed walking.");
        });
        run("Blunderville FirstB latest cancelled forced command and reset release the heading latch", () =>
        {
            var state = Start(); var planner = new BlundervilleRacePlanner(); var p = new Vector3(8, 10, -168);
            state.SynchronizeStatuses([new(3699, 0, Epoch.AddSeconds(.6))], Epoch);
            planner.Update(state, Epoch, p, true, false, false, null);
            state.SynchronizeStatuses([], Epoch.AddSeconds(1.4));
            Check(!planner.Update(state, Epoch.AddSeconds(1.4), p, true, false, false, null).Hold, "Lost status left a permanent hold.");
            state.SynchronizeStatuses([new(3700, 0, Epoch.AddSeconds(2))], Epoch.AddSeconds(1.5));
            planner.Update(state, Epoch.AddSeconds(1.5), p, true, false, false, null);
            planner.Reset(); state.SynchronizeStatuses([], Epoch.AddSeconds(1.6));
            Check(planner.Update(state, Epoch.AddSeconds(1.6), p, true, false, false, null).Facing == null, "Reset retained a previous facing.");
        });
        run("Blunderville FirstB latest knockback waits for landing before capturing the recovery route", () =>
        {
            var state = Start(); var planner = new BlundervilleRacePlanner();
            var air = planner.Update(state, Epoch, new(-9, 8, -289), false, false, false, null);
            Check(air.Hold && !planner.Recovering, "The falling trajectory was captured as a walkable recovery route.");
            var landed = planner.Update(state, Epoch.AddSeconds(.5), new(-11, 6, -294), true, false, false, null);
            Check(planner.Recovering && landed.Target is { Y: 6, Z: -294 }, "Recovery used the old airborne corner.");
        });
        run("Blunderville FirstB latest four recorded knockbacks are covered before impact", () =>
        {
            using var replay = new BlundervilleAnimationRegression.Recording(Fixture);
            var count = 0;
            foreach (var hit in replay.Data.RootElement.GetProperty("localHits").EnumerateArray())
            {
                var t = hit.GetProperty("t").GetDouble(); var source = hit.GetProperty("source").GetUInt64();
                var position = Vec(hit.GetProperty("position")); replay.Advance(t - .4);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - .4)).Any(h => h.Id.StartsWith($"animation/{source}/")
                    && h.From <= Epoch.AddSeconds(t) && h.Until >= Epoch.AddSeconds(t) && h.Geometry.Contains(position, .15f)),
                    $"The sweep at {t:F3}s still misses the recorded hit at {position}.");
                ++count;
            }
            Check(count == 4, "Fixture lost a recorded knockback.");
            replay.Encounter.Reset();
            Check(!replay.Encounter.Hazards(Epoch.AddSeconds(250)).Any(), "Reset retained the FirstB animation.");
        });
        foreach (var speed in new[] { 6f, 3.6f })
            foreach (var latency in new[] { .08, .3 })
            {
                run($"Blunderville FirstB latest rotating section speed {speed} latency {latency}", () => Simulate(false, speed, latency));
                run($"Blunderville FirstB latest third section speed {speed} latency {latency}", () => Simulate(true, speed, latency));
            }
        run("Blunderville FirstB spin weave stays clear of every row box", () =>
        {
            var route = BlundervilleSpinWeave.Route;
            for (var i = 0; i + 1 < route.Count; ++i)
                Check(BlundervilleSpinWeave.Clear(route[i], route[i + 1]), $"The spin weave segment {route[i]} -> {route[i + 1]} touches a row box.");
            Check(!BlundervilleSpinWeave.Clear(new(4, 10.03f, -279), new(4, 10.03f, -283)), "A path through a right row was accepted.");
        });
        // The timing-free weave as the script publishes it: 3.6m/s under 3702.
        foreach (var latency in new[] { .08, .3 })
            run($"Blunderville FirstB spin weave third section latency {latency}", () =>
            {
                var weave = Simulate(true, 3.6f, latency, weave: true); var solver = Simulate(true, 3.6f, latency);
                Console.WriteLine($"FIRSTB spin weave {weave:F2}s, solver {solver:F2}s");
                // 2026-10-13: 25.0/25.7s against 27.8/30.1s for the solver (live ~28s);
                // the ~84m line itself takes 23.3s at 3.6m/s.
                Check(weave < solver - 2 && weave < 26.5, "The timing-free weave was not clearly faster than the solver's detours.");
            });
    }

    private static double Simulate(bool bridge, float speed, double latency, bool weave = false)
    {
        using var replay = new BlundervilleAnimationRegression.Recording(Fixture);
        var phase = bridge ? 188.0 : 166.0;
        var now = Epoch.AddSeconds(phase); var move = new Moving(speed);
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = Start(); var planner = new BlundervilleRacePlanner();
        var p = bridge ? new Vector3(4.99f, 10.025f, -266.468f) : new Vector3(6.394f, 10, -192.981f);
        var finish = bridge ? -336 : -248;
        SafeZoneSolution? pending = null, accepted = null; var ready = phase; var nextSolve = phase;
        var effects = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0;
        var maxMs = 0.0; var reverse = 0f; var checks = 0; var direct = false; var t = phase;
        for (var tick = 0; tick < 10000; ++tick)
        {
            t = phase + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            if (tick % 10 == 0)
            {
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, null);
                // Mirrors BlundervilleScript: the timing-free spin weave.
                if (weave) decision = BlundervilleSpinWeave.Apply(decision, state, p, planner.Recovering);
                direct = decision.DirectTraversal;
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (direct)
                ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(Player(p, snapshot, speed), new(speed, speed, 0, false, speed, speed), now);
            else
            {
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, Player(p, snapshot, speed), new(speed, speed, 0, false, speed, speed), options, now);
                watch.Stop(); maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
            }
            runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, Player(p, snapshot, speed), snapshot, now]);
            }
            var next = move.Advance(p, .01f); reverse += MathF.Max(0, next.Z - p.Z); p = next with { Y = 10.025f };
            Check(BlundervilleRoutes.Boundary(state.Course).Contains(p), $"Movement left the upper floor at {p}.");
            if (!bridge)
                foreach (var z in new[] { -212f, -232f })
                    Check(MathF.Abs(p.X) + MathF.Abs(p.Z - z) >= 5.1f, $"Movement cut the measured diamond wall at {p}.");
            while (hitIndex < effects.Length && effects[hitIndex][0].GetDouble() <= t)
            {
                var e = effects[hitIndex++]; if (e[0].GetDouble() < phase) continue;
                var actor = replay.Actors[e[2].GetUInt64()];
                // Independent measured contact footprints, not the production catalog.
                IGeometryShape shape = e[3].GetUInt32() == 34775 ? new RectangleGeometry(actor.Position, e[4].GetSingle(), 3, 3) : new CircleGeometry(actor.Position, 5);
                if (shape.Contains(p, .15f)) throw new InvalidOperationException($"Contact at {t:F2}s, player {p}, helper {actor.Position}, action {e[3]}. Forecast: "
                    + string.Join("; ", replay.Encounter.Hazards(now).Where(h => h.Id.Contains($"/{actor.Id}/")).Select(h => $"{h.Id} {(h.From - Epoch).TotalSeconds:F2}..{(h.Until - Epoch).TotalSeconds:F2}"))
                    + $" solution={accepted?.DescribeOutcome()} points=" + string.Join(";", accepted?.ExecutablePath.Select(p => p.Position) ?? []));
                ++checks;
            }
            if (p.Z < finish) break;
        }
        Console.WriteLine($"FIRSTB bridge={bridge} weave={weave} speed={speed} latency={latency} end={p} elapsed={t - phase:F2} dispatch={move.Count} stops={move.Stops} reverse={reverse:F2} maxSolve={maxMs:F1}ms contacts={checks}");
        Check(p.Z < finish, "The actual navigation never crossed this FirstB section.");
        Check(move.Count < 100 && reverse < 5, "The route repeatedly restarted or reversed.");
        return t - phase;
    }

    private sealed class Moving(float speed) : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now; public int Count, Stops;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { _points = p; _index = 0; ++Count; _ready = Now.AddSeconds(.08); }
        public void Stop() { ++Stops; _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void ClearQueue() => Stop(); public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            if (!IsRunning || Now < _ready) return p;
            var d = _points[_index].Position - p; d.Y = 0; var distance = d.Length();
            if (distance <= _points[_index].Tolerance) { ++_index; return p; }
            return p + d / distance * MathF.Min(distance, speed * dt);
        }
    }
    private static BlundervilleRaceState Start()
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch);
        return state;
    }
    private static PlayerState Player(Vector3 p, SafeZoneSnapshot s, float speed) => new(p, 0, speed, false, false, 0, true, s.Version);
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
