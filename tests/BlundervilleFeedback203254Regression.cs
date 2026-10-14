using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BlundervilleFeedback203254Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const string Fixture = "animation-firstb-203254.json";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville 203254 reconnects four actual upper-floor knockbacks", () =>
        {
            using var replay = new BlundervilleAnimationRegression.Recording(Fixture);
            var state = Start(); var planner = Seed(state);
            foreach (var landing in replay.Data.RootElement.GetProperty("landings").EnumerateArray())
            {
                var hit = Epoch.AddSeconds(landing.GetProperty("hitAt").GetDouble());
                var resume = Epoch.AddSeconds(landing.GetProperty("resumeAt").GetDouble());
                var p = Vec(landing.GetProperty("position"));
                state.SynchronizeStatuses([new(149, 0, resume)], hit);
                Check(planner.Update(state, hit, Vec(landing.GetProperty("before")), false, false, false, null).Hold,
                    "Actual stun must stop input.");
                state.SynchronizeStatuses([], resume);
                var oldCursor = planner.Cursor;
                planner.Update(state, resume, p, false, true, false, null);
                Check(planner.Cursor == oldCursor, "An airborne position advanced the landing route.");
                var decision = planner.Update(state, resume, p, true, false, false, null);
                Check(!decision.Hold && !planner.Recovering && decision.Target is { } target && target.X < 0 && target.Z < p.Z,
                    $"Upper landing {p} retained stale target {decision.Target}, cursor {planner.Cursor}.");
            }
        });
        run("Blunderville 203254 protection only delays circles and restores on loss", () =>
        {
            using var replay = new BlundervilleAnimationRegression.Recording(Fixture);
            var now = Epoch.AddSeconds(151.963); replay.Advance(151.963);
            var p = new Vector3(-2.762f, 9.995f, -205.127f);
            replay.Encounter.Synchronize(replay.Actors.Values.Append(new(999, 16351, p)), now);
            var state = Start(); var planner = Seed(state);
            using var runtime = new SafeZoneRuntime(new BlundervilleFirstBFeedbackRegression.Moving(6), () => false, () => false) { ReplayClock = () => now };
            using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager); api.UtcNowForTest = now;
            var nav = new BlundervilleNavigation(runtime, api, new());
            void Apply() => nav.Apply(state, planner.Update(state, now, p, true, false, false, null), replay.Encounter, now, p, true, false, false);
            bool ActiveCircle() => runtime.Manager.BuildSnapshot(now).DangerAreas.Any(a => a.Geometry is CircleGeometry { Radius: 5 }
                && a.Geometry.Contains(p) && a.DangerWindows.Any(w => w.IsActiveAt(now)));
            Apply(); Check(ActiveCircle(), "The recorded landing must begin inside an active circle.");
            var until = Epoch.AddSeconds(153.928);
            state.SynchronizeStatuses([new(529, 0, until)], now); Apply();
            Check(!ActiveCircle(), "Confirmed protection did not allow an immediate exit.");
            Check(runtime.Manager.BuildSnapshot(now).DangerAreas.Any(a => a.Geometry is CircleGeometry { Radius: 5 }
                && a.Geometry.Contains(p) && a.DangerWindows.Any(w => w.IsActiveAt(until.AddSeconds(.01)))),
                "Protection removed the post-expiry hazard.");
            Check(runtime.Manager.BuildSnapshot(now).DangerAreas.Any(a => a.Geometry is CircleGeometry { Radius: 2 }
                && a.Geometry.Contains(p) && a.DangerWindows.Any(w => w.IsActiveAt(now))), "Protection suppressed an unrelated obstacle.");
            Check(replay.Encounter.Hazards(now).Any(h => h.Geometry is CircleGeometry { Radius: 5 }
                && h.Geometry.Contains(p) && h.From <= now && h.Until >= now), "Navigation changed the visible animation.");
            var version = runtime.Manager.BuildSnapshot(now).Version;
            state.SynchronizeStatuses([new(529, 0, until.AddSeconds(.03))], now); Apply();
            Check(runtime.Manager.BuildSnapshot(now).Version == version, "Status sampling jitter invalidated the worker route.");
            state.SynchronizeStatuses([], now); Apply();
            Check(ActiveCircle(), "Early loss of protection retained a stale safe window.");
            state.SynchronizeStatuses([new(529, 0, DateTime.MaxValue)], now); Apply();
            Check(ActiveCircle(), "Unknown protection duration became permanent immunity.");
            state.SynchronizeStatuses([new(529, 0, until)], now);
            Check(state.ObstacleImmunityExpiresAt(until.AddSeconds(.01)) == null, "Expired protection remained active.");
        });
        for (var i = 0; i < 4; ++i)
        {
            var index = i;
            foreach (var latency in new[] { .08, .3 })
                run($"Blunderville 203254 landing {index + 1} resumes actual navigation latency {latency}", () => Simulate(index, latency));
        }
    }

    private static void Simulate(int index, double latency)
    {
        using var replay = new BlundervilleAnimationRegression.Recording(Fixture);
        var landing = replay.Data.RootElement.GetProperty("landings")[index];
        var phase = landing.GetProperty("resumeAt").GetDouble();
        var now = Epoch.AddSeconds(phase); var state = Start(); var planner = Seed(state);
        var hit = Epoch.AddSeconds(landing.GetProperty("hitAt").GetDouble());
        state.SynchronizeStatuses([new(149, 0, now)], hit);
        planner.Update(state, hit, Vec(landing.GetProperty("before")), false, false, false, null);
        var immuneUntil = Epoch.AddSeconds(landing.GetProperty("immuneUntil").GetDouble());
        state.SynchronizeStatuses([new(529, 0, immuneUntil)], now);
        var p = Vec(landing.GetProperty("position")); var start = p;
        var move = new BlundervilleFirstBFeedbackRegression.Moving(6);
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        SafeZoneSolution? pending = null, accepted = null;
        var ready = phase; var nextSolve = phase; var firstMove = double.NaN; var reverse = 0f; var maxMs = 0.0;
        var t = phase; var nextLog = phase;
        var contacts = replay.Events.Where(e => e[0].GetDouble() >= phase && e[1].GetString() == "hit").ToArray(); var contactIndex = 0;
        // Each burst is independently bounded by its first/last observed
        // contacts (0.4s apart), so validation also covers time between packets.
        var bursts = new List<(double From, double Until, Vector3 Center)>();
        foreach (var group in replay.Events.Where(e => e[1].GetString() == "hit").GroupBy(e => e[2].GetUInt64()))
        {
            double from = -1, last = -1;
            foreach (var e in group)
            {
                var at = e[0].GetDouble();
                if (at - last > .8 && from >= 0) { bursts.Add((from, last, replay.Actors[group.Key].Position)); from = -1; }
                if (from < 0) from = at;
                last = at;
            }
            if (from >= 0) bursts.Add((from, last, replay.Actors[group.Key].Position));
        }
        for (var tick = 0; tick < 2600; ++tick)
        {
            t = phase + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            if (tick % 10 == 0)
            {
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, null);
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, Player(p, snapshot), new(6, 6, 0, false, 6, 6), options, now, accepted);
                watch.Stop(); maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                ready = t + Math.Max(latency, SimClock.Solve(watch)); nextSolve = ready + .1;
            }
            runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, Player(p, snapshot), snapshot, now]);
            var next = move.Advance(p, .01f); reverse += MathF.Max(0, next.Z - p.Z); p = next;
            if (double.IsNaN(firstMove) && Vector3.Distance(start, p) > .3f) firstMove = t - phase;
            Check(BlundervilleRoutes.Boundary(state.Course).Contains(p), $"Recovery left the floor at {p}.");
            foreach (var z in new[] { -212f, -232f })
                Check(MathF.Abs(p.X) + MathF.Abs(p.Z - z) >= 5.1f, $"Recovery cut through a solid wall at {p}.");
            if (now > immuneUntil)
                foreach (var burst in bursts.Where(b => b.From <= t && b.Until >= t))
                    Check(Vector2.Distance(new(p.X, p.Z), new(burst.Center.X, burst.Center.Z)) > 5.15f,
                        $"Recovery entered the observed active animation, t={t:F2}, p={p}, helper={burst.Center}.");
            while (contactIndex < contacts.Length && contacts[contactIndex][0].GetDouble() <= t)
            {
                var contact = contacts[contactIndex++];
                if (Epoch.AddSeconds(contact[0].GetDouble()) <= immuneUntil) continue;
                var center = replay.Actors[contact[2].GetUInt64()].Position;
                Check(Vector2.Distance(new(p.X, p.Z), new(center.X, center.Z)) > 5.15f,
                    $"Recovery hit a real 34773 contact after immunity ended, t={t:F2}, p={p}, helper={center}.");
            }
            if (t >= nextLog)
            {
                Console.WriteLine($"203254 trace landing={index + 1} t={t:F2} p={p} cursor={planner.Cursor} solve={accepted?.DescribeOutcome()} running={move.IsRunning}");
                nextLog = t + 3;
            }
            if (p.Z < -248) break;
        }
        Console.WriteLine($"203254 landing={index + 1} latency={latency} end={p} elapsed={t - phase:F2} firstMove={firstMove:F2} dispatch={move.Count} stops={move.Stops} reverse={reverse:F2} maxSolve={maxMs:F1}ms");
        Check(!double.IsNaN(firstMove) && firstMove < 2, "The recovered player did not promptly resume walking.");
        Check(p.Z < -248 && reverse < 5, "Recovery stalled or returned to the old route.");
        Check(t <= replay.Data.RootElement.GetProperty("duration").GetDouble(), "The simulation ran past the recorded evidence.");
    }

    private static BlundervilleRacePlanner Seed(BlundervilleRaceState state)
    {
        var planner = new BlundervilleRacePlanner();
        planner.Update(state, Epoch.AddSeconds(148.8), new(6.943f, 9.995f, -202.747f), true, false, false, null);
        return planner;
    }
    private static BlundervilleRaceState Start()
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch.AddSeconds(127.374));
        return state;
    }
    private static PlayerState Player(Vector3 p, SafeZoneSnapshot s) => new(p, 0, 6, false, false, 0, true, s.Version);
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
