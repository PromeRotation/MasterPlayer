using System.Numerics;
using System.Reflection;
using System.Diagnostics;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BlundervilleFeedback213132Regression
{
    public static void Run(Action<string, Action> run)
    {
        foreach (var latency in new[] { .12, .3 })
        run($"Blunderville 213132 Ring second lap east jumps {latency:F2}s", () =>
        {
            var o = BlundervilleRingRegression.Simulate("ring", "feedback-213132.json", 56.519, new(208.545f, 10, -153.521f), latency);
            Console.WriteLine($"213132 RING fall={o.Fall} beam={string.Join(';', o.BeamHits)} cast={string.Join(';', o.CastHits)} stand={o.LongestStand:F2}");
            if (o.Fall != null || o.BeamHits.Count > 0 || o.CastHits.Count > 0 || o.LongestStand > 1)
                throw new InvalidOperationException("The second-lap jump approach stopped or took a recorded hazard.");
        });
        foreach (var latency in new[] { .08, .3 })
        foreach (var hazards in new[] { false, true })
        foreach (var fps in new[] { 15, 30, 60 })
            run($"Blunderville 213132 Final continuous raised stairs {latency:F2}s hazards={hazards} {fps}fps", () => Stairs(latency, hazards, fps));
        foreach (var hazards in new[] { false, true })
            run($"Blunderville 213132 Final resumes from recorded first landing hazards={hazards}",
                () => Stairs(.3, hazards, 30, true));
        // 14:03/14:13 ARRs: a left-side diagonal approach reached the first
        // riser 0.44m off the stair lane and stood against it until the round ended.
        foreach (var latency in new[] { .08, .3 })
        foreach (var fps in new[] { 30, 60 })
        {
            run($"Blunderville 141311 Final left diagonal stair approach {latency:F2}s {fps}fps",
                () => Stairs(latency, false, fps, start: new(-9.5f, 31.3f, 157.5f)));
            run($"Blunderville 141311 Final stalled against first riser {latency:F2}s {fps}fps",
                () => Stairs(latency, false, fps, start: new(-4.44f, 31.85f, 151.32f), stalled: true));
        }
    }

    private static void Stairs(double latency, bool hazards, int fps, bool resume = false, Vector3? start = null, bool stalled = false)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var epoch = BlundervilleRingRegression.Epoch; var now = epoch;
        using var replay = new BlundervilleRingRegression.Recording("final", "feedback-213132.json");
        var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, epoch);
        var race = new BlundervilleRacePlanner(); var jumper = new BlundervilleJumpPlanner(); var gate = new BlundervilleJumpExecution();
        var slope = new BlundervilleFinalPlanner();
        var empty = new BlundervilleEncounter(_ => { });
        var p = start ?? (resume ? new Vector3(-3.952f, 32.609f, 149.401f) : new Vector3(-3.677f, 31.418f, 155.444f));
        p.Y = Height(p.Z);
        var begin = resume ? 34.97 : 32.543;
        SafeZoneSolution? accepted = null, pending = null;
        var ready = 0d; var nextSolve = 0d; var scan = 0d; var launches = new List<(double T, Vector3 P)>();
        double? flight = null; var launchY = 0f; var maxBack = 0f; var stand = 0d; var longest = 0d;
        var history = new List<(double T, Vector3 P)>(); var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray();
        var end = 0d; var direct = false;
        var frame = 1d / fps;
        for (var tick = 0; tick < fps * 12; ++tick)
        {
            var t = begin + tick * frame; end = t; now = epoch.AddSeconds(t); move.Now = now;
            var air = flight != null;
            if (t >= scan)
            {
                scan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                var decision = race.Update(state, now, p, !air, air, false, null);
                var encounter = hazards ? replay.Encounter : empty;
                var danger = encounter.TraversalHazards(now).ToArray();
                decision = slope.Apply(decision, state, danger, p, now, nav.GetDirectTraversalExecution(now));
                if (!decision.DirectTraversal) decision = BlundervilleRefuge.Apply(decision, state, danger, p, now,
                    runtime.MovementStatus.LatestSolution is not { IsSuccess: true });
                if (decision.Hold) { jumper.Reset(); gate.Clear(); nav.ReleaseJump(); }
                var step = decision.Hold ? null : jumper.Plan(state.Course, now, p, decision.Target ?? p, !air, air);
                var execution = decision.Hold ? null : gate.Prepare(step?.Corridor, p);
                var target = execution?.Target ?? step?.Target ?? decision.Target;
                direct = decision.DirectTraversal || execution?.Corridor is { Stairs: true } stairs && target == stairs.MovementTarget;
                nav.Apply(state, decision with { Target = target }, encounter,
                    now, p, true, false, false, execution?.Corridor ?? step?.Corridor);
                if (Environment.GetEnvironmentVariable("STAIR_DEBUG") == "1")
                    Console.WriteLine($"STAIR {t:F2} p={p} air={air} target={target} hint={execution?.Hint} speed={gate.MeasuredSpeed:F2} current={move.GetStatus().CurrentPoint?.Position}");
            }
            for (var attempt = 0; attempt < 2; ++attempt)
            {
                var wasInFlight = gate.InFlight;
                if (gate.Sample(now, p, !air, air, runtime.MovementStatus.HasControl, move.GetStatus()))
                {
                    Check(launches.Count == 0 || t - launches[^1].T >= .7 - 1e-6, "Violated native jump cooldown.");
                    if (nav.CommitJump(gate.Landing!.Value, p, gate.MeasuredSpeed, now))
                    { launches.Add((t, p)); flight = t; launchY = p.Y; air = true; gate.ConfirmLaunch(now); jumper.ConfirmLaunch(gate.Corridor!); }
                    else gate.RejectLaunch();
                }
                if (!gate.InFlight) nav.ReleaseJump();
                if (wasInFlight && gate.Landed && !air)
                {
                    scan = t + frame;
                    if (jumper.Plan(state.Course, now, p, new(-4, 35.8f, 137), true, false) is { } nextStep)
                    { gate.Prepare(nextStep.Corridor, p); continue; }
                }
                break;
            }
            if (gate.NeedsRunupRetry) { nav.StopForRunup(); scan = t; accepted = pending = null; nextSolve = t; }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, true, false, 0, true, snapshot.Version);
            if (!direct && pending != null && t >= ready) { accepted = pending; pending = null; }
            if (!direct && pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                var watch = Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now);
                ready = t + Math.Max(latency, SimClock.Solve(watch)); nextSolve = ready + .1;
            }
            if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(6, 6, 0, false, 6, 6), now);
            else if (!runtime.Setting.RuntimeMovementHeld) runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [air ? null : accepted, player, snapshot, now]);
            var next = move.Advance(p, (float)frame, 6);
            if (flight is { } launch)
            {
                var elapsed = (float)(t + frame - launch);
                next.Y = launchY + 10 * elapsed - 13.35f * elapsed * elapsed;
                if (elapsed > .35f && next.Y <= Height(next.Z)) { flight = null; next.Y = Height(next.Z); }
            }
            else if (Height(next.Z) > p.Y + .3f) next = p; // A riser cannot be walked through.
            else next.Y = Height(next.Z);
            if (p.Y > 32.2f) maxBack = Math.Max(maxBack, next.Z - p.Z);
            stand = Vector3.DistanceSquared(next, p) < .0001f ? stand + frame : 0; longest = Math.Max(longest, stand);
            p = next; history.Add((t + frame, p));
            if (p.Z <= 137.3f && flight == null) break;
        }
        var hits = new List<string>();
        if (hazards)
        foreach (var e in impacts)
        {
            var t = e[0].GetDouble(); if (t < begin || t > end) continue;
            if (!BlundervilleCatalog.TryGet(e[3].GetUInt32(), out var rule) || rule.Shape == null) continue;
            var v = e[4]; var shape = rule.Shape.Resolve(new(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle()), e[5].GetSingle(), null);
            foreach (var lead in new[] { .3, 0 })
            {
                var sample = history.LastOrDefault(s => s.T <= t - lead);
                if (sample.T != 0 && shape?.Contains(sample.P, .3f) == true) { hits.Add($"{e[3]}@{t:F2}"); break; }
            }
        }
        Console.WriteLine($"213132 STAIRS {latency:F2}s hazards={hazards} end={p} elapsed={end-begin:F2} jumps={string.Join(';', launches.Select(l => $"{l.T:F2}@{l.P.Z:F2}"))} back={maxBack:F2} stand={longest:F2} hits={string.Join(';', hits)}");
        // A stalled start may back up once for a run-up; it must still climb.
        Check(p.Z <= 137.3f && launches.Count == (resume ? 3 : 4) && (stalled || maxBack < .05f) && longest < 1, "The raised steps did not chain without reversing or stalling.");
        Check(hits.Count == 0, "The stair execution intersected a recorded mechanism.");
    }

    // Local crmt6/crmt7 top-surface profiles, checked against the ARR's
    // 32.609m first landing and 33.555/34.470m subsequent grounded samples.
    private static float Height(float z) => z > 151 ? 31.7f + (153 - z) * .105f
        : z > 147 ? 32.64f + (149 - z) * .067f
        : z > 143 ? 33.516f + (145 - z) * .067f
        : z > 139 ? 34.39f + (141 - z) * .068f
        : 35.86f + (137 - z) * .223f;
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
