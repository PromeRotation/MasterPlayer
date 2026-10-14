using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BlundervilleFeedback205734Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Action<string, Action> run)
    {
        foreach (var latency in new[] { .08, .3 })
            run($"Blunderville 205734 charger landing resumes latency {latency}", () => Landing(latency));
        foreach (var onset in new[] { -338f, -339.9f, -340.5f })
            foreach (var offset in new[] { -.2, 0, .2 })
                run($"Blunderville 205734 ramp recorded bodies onset {onset} arrival offset {offset}", () => Ramp(onset, offset));
    }
    private static void Ramp(float onset, double offset)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ramp-feedback-205734.json")));
        var root = data.RootElement;
        var effects = root.GetProperty("effects").EnumerateArray().Select(e => new BlundervilleSlimeRampRegression.Effect(
            e[0].GetDouble(), e[1].GetInt32(), e[2].GetSingle(), e[3].GetSingle(), e[4].ValueKind == JsonValueKind.Null ? null : e[4].GetSingle())).ToArray();
        var balls = root.GetProperty("balls").EnumerateArray().Select(b => new BlundervilleSlimeRampRegression.RecordedBall(
            b.GetProperty("spawn").GetDouble(), b.GetProperty("x").GetSingle(), b.GetProperty("samples").Deserialize<double[][]>()!,
            b.GetProperty("end").ValueKind == JsonValueKind.Null ? double.PositiveInfinity : b.GetProperty("end").GetDouble())).ToArray();
        var bodies = root.GetProperty("bodySamples").Deserialize<double[][]>()!;
        var stun = root.GetProperty("stun").Deserialize<double[]>()!;
        Check(bodies.Where(b => b[0] <= stun[0] && stun[0] - b[0] < .7).Any(b => Vector2.Distance(
            new((float)b[2], (float)b[4]), new((float)stun[1], (float)stun[3])) <= 1), "The independent body samples do not reproduce the recorded stun.");
        var o = BlundervilleSlimeRampRegression.Simulate("205734", effects, 71.9 + offset, 1.48f, balls, bodies, onset);
        Console.WriteLine($"205734 ramp onset={onset} offset={offset} climb={o.Climb:F2} stand={o.Stand:F2} elapsed={o.Finish-o.Arrive:F2} pushed={string.Join(';', o.Pushes)} fall={o.Fall} slides={o.Slides} scan={o.MaxMs:F1}ms");
        Check(o.Fall == null && o.Pushes.Count == 0 && o.Slides == 0 && o.Steered == 1, "Actual ramp arrival hit an obstacle or lost uphill input.");
        Check(o.Stand < 4, "Ramp approach still keeps a stale departure window.");
        Check(o.Finish < root.GetProperty("end").GetDouble(), "The ascent exceeded the recording.");
    }
    private static void Landing(double latency)
    {
        using var replay = new BlundervilleAnimationRegression.Recording("animation-firstb-205734.json");
        var phase = 205.273; var now = Epoch.AddSeconds(phase);
        var p = new Vector3(15.183f, 9.995f, -254.81f); var start = p;
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch.AddSeconds(161.86));
        var planner = new BlundervilleRacePlanner();
        planner.Update(state, Epoch.AddSeconds(201.7), new(5.295f, 9.995f, -252.461f), true, false, false, null);
        state.SynchronizeStatuses([new(149, 0, now), new(529, 0, Epoch.AddSeconds(206.741))], Epoch.AddSeconds(202.367));
        planner.Update(state, Epoch.AddSeconds(202.367), p, true, false, false, null);
        state.SynchronizeStatuses([new(529, 0, Epoch.AddSeconds(206.741))], now);
        var move = new BlundervilleFirstBFeedbackRegression.Moving(6);
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        SafeZoneSolution? accepted = null, pending = null;
        var ready = phase; var nextSolve = phase; var firstMove = double.NaN; var t = phase; var maxMs = 0.0;
        for (var tick = 0; tick < 1000; ++tick)
        {
            t = phase + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            if (tick % 10 == 0)
            {
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, null);
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, false, false, 0, true, snapshot.Version);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now, accepted);
                maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                ready = t + Math.Max(latency, SimClock.Solve(watch)); nextSolve = ready + .1;
                if (tick < 100) Console.WriteLine($"205734 solve t={t:F2} {pending.DescribeOutcome()} path={string.Join(';', pending.ExecutablePath.Select(w => w.Position))}");
            }
            runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            p = move.Advance(p, .01f);
            if (double.IsNaN(firstMove) && Vector3.Distance(p, start) > .3f) firstMove = t - phase;
            Check(BlundervilleRoutes.Boundary(state.Course).Contains(p), $"Charger recovery left the floor at {p}.");
            if (p.Z < -262) break;
        }
        Console.WriteLine($"205734 charger latency={latency} firstMove={firstMove:F2} end={p} elapsed={t-phase:F2} maxSolve={maxMs:F1}ms dispatch={move.Count}");
        Check(firstMove < 1 && p.Z < -262, "Charger recovery stalled.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
