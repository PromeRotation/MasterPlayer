using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

// 2026-10-14 23:45 ARR. FirstB final ramp: two snowball stuns with the
// player 2.49m from the ball centre and one pusher step on the middle row.
// Final: a 34801 sweep knocked the player to (1.66,190.0); the slope planner
// then reported a dead end and the player stood until eliminated.
internal static class BlundervilleFeedback234521Regression
{
    // Widest recorded snowball stun distance (16:40 / 23:45 ARRs): 2.63m.
    internal const float RecordedBallHit = 2.65f;
    public static void Run(Action<string, Action> run)
    {
        foreach (var onset in new[] { -338f, -339.9f, -340.5f })
            foreach (var offset in new[] { -.2, 0, .2 })
                run($"Blunderville 234521 ramp onset {onset} arrival offset {offset}", () => Ramp(onset, offset));
        // 2026-10-15 01:45 ARR: the planner held at the ramp foot from 60.4s
        // to 72.9s before the player took over by hand.
        foreach (var onset in new[] { -338f, -339.9f, -340.5f })
            foreach (var offset in new[] { -.2, 0, .2 })
                run($"Blunderville 014539 ramp onset {onset} arrival offset {offset}", () => Ramp(onset, offset, "014539", 60.6, -1.02f, 5));
        // 02:10 ARR: the foot plan walked the player between lanes along z=-337.2.
        foreach (var onset in new[] { -338f, -339.9f, -340.5f })
            foreach (var offset in new[] { -.2, 0, .2 })
                run($"Blunderville 021045 ramp onset {onset} arrival offset {offset}", () => Ramp(onset, offset, "021045", 67.3, -1.9f, 1));
        foreach (var latency in new[] { .08, .3 })
            run($"Blunderville 234521 Final full slope from the start line {latency:F2}s", () =>
            {
                var o = Final(.5, new(-1.419f, 4.227f, 320.729f), latency, []);
                Console.WriteLine($"234521 FINAL full {latency:F2}s end={o.End} elapsed={o.Elapsed:F2} stand={o.Longest:F2} scan={o.MaxScanMs:F1}ms hits={string.Join(';', o.Hits)} stops={string.Join(" | ", o.Stops)}");
                Check(o.Hits.Count == 0, "The slope run intersected a recorded mechanism.");
                Check(o.End.Z <= BlundervilleFinalPlanner.Exit + .5f, "The slope was not completed.");
            });
        foreach (var latency in new[] { .08, .3 })
            run($"Blunderville 234521 Final resumes after the z=190 knockback {latency:F2}s", () => FinalRecovery(latency));
        foreach (var latency in new[] { .08, .3 })
            foreach (var (at, x, z) in new[] { (16.7, -4.01f, 243.88f), (22.8, -2.91f, 219.75f), (24.1, -2.0f, 212.27f), (24.88, -4.68f, 208.64f), (25.93, -6.39f, 202.75f) })
                run($"Blunderville 234521 Final slope from the recorded position at {at:F2} {latency:F2}s", () => FinalApproach(latency, at, new(x, BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, z), z)));
    }

    private static void Ramp(float onset, double offset, string name = "234521", double arrive = 87.3, float lane = 2f, double maxStand = double.PositiveInfinity)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", $"ramp-feedback-{name}.json")));
        var root = data.RootElement;
        var effects = root.GetProperty("effects").EnumerateArray().Select(e => new BlundervilleSlimeRampRegression.Effect(
            e[0].GetDouble(), e[1].GetInt32(), e[2].GetSingle(), e[3].GetSingle(), e[4].ValueKind == JsonValueKind.Null ? null : e[4].GetSingle())).ToArray();
        var balls = root.GetProperty("balls").EnumerateArray().Select(b => new BlundervilleSlimeRampRegression.RecordedBall(
            b.GetProperty("spawn").GetDouble(), b.GetProperty("x").GetSingle(), b.GetProperty("samples").Deserialize<double[][]>()!,
            b.GetProperty("end").GetDouble())).ToArray();
        var bodies = root.GetProperty("bodySamples").Deserialize<double[][]>()!;
        // Recorded arrival at z=-336 on the x=2 lane.
        var o = BlundervilleSlimeRampRegression.Simulate(name, effects, arrive + offset, lane, balls, bodies, onset, RecordedBallHit);
        Console.WriteLine($"{name} ramp onset={onset} offset={offset} climb={o.Climb:F2} stand={o.Stand:F2} elapsed={o.Finish - o.Arrive:F2} pushed={string.Join(';', o.Pushes)} fall={o.Fall} slides={o.Slides} scan={o.MaxMs:F1}ms");
        Check(o.Fall == null && o.Pushes.Count == 0 && o.Slides == 0, "The recorded ramp still hits a snowball or pusher.");
        // 02:10 ended (round lost) during the recorded foot shuffle.
        if (name != "021045") Check(o.Finish < root.GetProperty("end").GetDouble(), "The ascent exceeded the recording.");
        Check(o.Stand < maxStand, "The ramp foot wait is too long.");
    }

    // Course time (start 445.305): sweep 34801 hit at 27.125, landing
    // (1.663,25.711,190.02) by 28.0, 149 until 30.765, 529 until 32.28,
    // 34812 hit at 31.145 while stunned, eliminated at 42.188.
    private static void FinalRecovery(double latency)
    {
        var o = Final(28.0, new(1.663f, 25.711f, 190.02f), latency, [(149, 30.765), (529, 32.28)]);
        Console.WriteLine($"234521 FINAL recovery {latency:F2}s end={o.End} elapsed={o.Elapsed:F2} firstMove={o.FirstMove:F2} stand={o.Longest:F2} hits={string.Join(';', o.Hits)} hints={o.Hints}");
        Check(o.FirstMove < 32.28 + .6, "The planner did not resume after the knockback protection.");
        Check(o.End.Z <= BlundervilleFinalPlanner.Exit + .5f, "The slope was not completed after the knockback.");
        Check(o.Longest < 3, "The slope stalled after the knockback.");
        Check(o.Hits.Count == 0, "The recovered run intersected a recorded mechanism.");
    }

    private static void FinalApproach(double latency, double start, Vector3 position)
    {
        // Recorded positions before the 27.125 sweep hit.
        var o = Final(start, position, latency, []);
        Console.WriteLine($"234521 FINAL approach t={start:F2} {latency:F2}s end={o.End} elapsed={o.Elapsed:F2} stand={o.Longest:F2} hits={string.Join(';', o.Hits)} hints={o.Hints}");
        Check(o.Hits.Count == 0, "The slope run intersected a recorded mechanism.");
        Check(o.End.Z <= BlundervilleFinalPlanner.Exit + .5f, "The slope was not completed.");
    }

    internal sealed record FinalOutcome(Vector3 End, double Elapsed, double FirstMove, double Longest, List<string> Hits, string Hints, List<string> Stops, double MaxScanMs);

    internal static FinalOutcome Final(double start, Vector3 p, double latency, (uint Id, double Until)[] statuses,
        string fixture = "feedback-234521.json", double? limit = null)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var epoch = BlundervilleRingRegression.Epoch; var now = epoch.AddSeconds(start);
        using var replay = new BlundervilleRingRegression.Recording("final", fixture);
        var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, epoch);
        var race = new BlundervilleRacePlanner(); var slope = new BlundervilleFinalPlanner();
        SafeZoneSolution? accepted = null, pending = null;
        var ready = 0d; var nextSolve = 0d; var scan = 0d; var stand = 0d; var longest = 0d; var firstMove = double.NaN;
        var history = new List<(double T, Vector3 P)>(); var hints = new List<string>(); var stops = new List<string>(); var hint = ""; var maxScan = 0d;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray();
        var end = limit ?? replay.End; var t = start; var direct = false; var free = statuses.Length == 0 ? start : statuses.Max(s => s.Until);
        const double frame = 1 / 30d;
        for (var tick = 0; t < end; ++tick)
        {
            t = start + tick * frame; now = epoch.AddSeconds(t); move.Now = now;
            if (t >= scan)
            {
                scan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                state.SynchronizeStatuses(statuses.Where(s => s.Until > t).Select(s => new BlundervilleRaceStatus(s.Id, 0, epoch.AddSeconds(s.Until))), now);
                var decision = race.Update(state, now, p, true, false, false, null);
                var danger = replay.Encounter.TraversalHazards(now).ToArray();
                if (Environment.GetEnvironmentVariable("FINAL_DEBUG") == "1" && t > 21 && t < 27.3)
                    Console.WriteLine($"FINALDBG {t:F2} p=({p.X:F2},{p.Z:F2}) " + string.Join(' ', danger.Where(h => h.Id.Contains("/34801/") && h.Geometry.Position.Z is > 195 and < 202)
                        .Select(h => $"{h.Geometry.Position.X:F0}@{(h.From - epoch).TotalSeconds:F2}-{(h.Until - epoch).TotalSeconds:F2}")));
                var scanWatch = Stopwatch.StartNew();
                decision = slope.Apply(decision, state, danger, p, now, nav.GetDirectTraversalExecution(now));
                maxScan = Math.Max(maxScan, scanWatch.Elapsed.TotalMilliseconds);
                if (!decision.DirectTraversal) decision = BlundervilleRefuge.Apply(decision, state, danger, p, now,
                    runtime.MovementStatus.LatestSolution is not { IsSuccess: true });
                if (hints.Count == 0 || !hints[^1].EndsWith(decision.Hint)) hints.Add($"{t:F1}@({p.X:F1},{p.Z:F1}):{decision.Hint}");
                direct = decision.DirectTraversal; hint = decision.Hint;
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, true, false, 0, true, snapshot.Version);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                var watch = Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now, accepted);
                ready = t + Math.Max(latency, SimClock.Solve(watch)); nextSolve = ready + .1;
            }
            if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(6, 6, 0, false, 6, 6), now);
            else if (!runtime.Setting.RuntimeMovementHeld)
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            var next = t < free ? p : move.Advance(p, (float)frame, 6);
            next.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, next.Z);
            var moved = Vector3.DistanceSquared(next, p) >= .0001f;
            if (moved && double.IsNaN(firstMove) && t >= free) firstMove = t;
            if (moved && stand >= .25) stops.Add($"{t - stand:F1}+{stand:F2}s@({p.X:F1},{p.Z:F1}) {hint}");
            stand = !moved && t >= free ? stand + frame : 0; longest = Math.Max(longest, stand);
            p = next; history.Add((t + frame, p));
            if (p.Z <= BlundervilleFinalPlanner.Exit + .5f) break;
        }
        var hits = new List<string>();
        foreach (var e in impacts)
        {
            var at = e[0].GetDouble(); if (at < Math.Max(start, free) || at > t) continue;
            if (!BlundervilleCatalog.TryGet(e[3].GetUInt32(), out var rule) || rule.Shape == null) continue;
            var v = e[4]; var shape = rule.Shape.Resolve(new(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle()), e[5].GetSingle(), null);
            foreach (var lead in new[] { .3, 0 })
            {
                var sample = history.LastOrDefault(s => s.T <= at - lead);
                if (sample.T != 0 && shape?.Contains(sample.P, .3f) == true) { hits.Add($"{e[3]}@{at:F2}({sample.P.X:F1},{sample.P.Z:F1})"); break; }
            }
        }
        return new(p, t - start, firstMove, longest, hits, string.Join(" | ", hints), stops, maxScan);
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
