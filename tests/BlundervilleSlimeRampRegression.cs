using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// 2026-10-13 19:04 ARR: 66s on the FirstB final ramp, eliminated. On the
// slime (3703) the input runs at 6m/s and the ramp adds 4m/s straight down it;
// a stop slides back to the bottom. Replays the 34774 pushers of every
// recorded FirstB round through the script's decision chain with that motion,
// arriving at the foot of the ramp across the pushers' 12.85s cycle, and
// applies each recorded push by the measured rule: the player's centre within
// 1.5m of the row and inside the step's x range 0.15s before the packet.
internal static class BlundervilleSlimeRampRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc);
    private static readonly bool Debug = Environment.GetEnvironmentVariable("SLIME_DEBUG") == "1";
    private static readonly int Phases = int.TryParse(Environment.GetEnvironmentVariable("SLIME_PHASES"), out var phases) ? phases : 8;
    // The game's slime (19:04 fits); SLIME_DRIFT stresses the planner's model.
    private static readonly float Drift = float.TryParse(Environment.GetEnvironmentVariable("SLIME_DRIFT"), out var drift) ? drift : 4;
    private static readonly float SlimeEdge = float.TryParse(Environment.GetEnvironmentVariable("SLIME_EDGE"), out var edge) ? edge : BlundervilleSlimeRamp.Bottom;

    internal sealed record Effect(double T, int Source, float X, float Z, float? To);
    internal sealed record RecordedBall(double Spawn, float X, double[][] Samples, double End)
    {
        public float? Z(double time, bool observed)
        {
            if (time < Spawn || time > End) return null;
            var i = 0;
            while (i + 1 < Samples.Length && Samples[i + 1][0] <= time) ++i;
            if (observed || i + 1 == Samples.Length) return (float)Samples[i][1];
            var a = Samples[i]; var b = Samples[i + 1];
            if (i == 0 && Samples.Length > 2)
            {
                // The spawn-to-first-move gap includes a stationary animation.
                // Recover its end from the next two measured moving positions;
                // linear interpolation from spawn would invent immediate rolling.
                var speed = (Samples[2][1] - b[1]) / (Samples[2][0] - b[0]);
                if (speed > 0)
                {
                    var moved = Math.Max(a[0], b[0] - (b[1] - a[1]) / speed);
                    if (time <= moved) return (float)a[1];
                    return (float)(a[1] + (b[1] - a[1]) * (time - moved) / (b[0] - moved));
                }
            }
            return (float)(a[1] + (b[1] - a[1]) * (time - a[0]) / (b[0] - a[0]));
        }
    }
    internal sealed record Outcome(string Name, double Arrive, double Climb, double Stand, List<string> Pushes, string? Fall, double MaxMs, double Steered = 1, int Slides = 0, double DispatchRate = 0, double Finish = double.NaN);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville slime ramp owns ball navigation without hiding its preview", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            encounter.Synchronize([new(1, BlundervilleCatalog.Ball, new(1.8f, 20, -351)),
                new(2, BlundervilleCatalog.Ball, new(-200, 6, -100))], Epoch);
            Check(encounter.Previews(Epoch).Count(p => p.Id.StartsWith("ball/")) == 2, "Ramp ownership hid a ball preview.");
            Check(encounter.Hazards(Epoch).Any(h => h.Id == "ball/1"), "Normal navigation lost its ramp ball.");
            Check(!encounter.Hazards(Epoch, ramp: false).Any(h => h.Id.StartsWith("ball/1")), "A wider ball warning can interrupt the slime input.");
            Check(encounter.Hazards(Epoch, ramp: false).Any(h => h.Id == "ball/2"), "Ramp ownership disabled another course's ball.");
        });
        var recordings = Load();
        run("Blunderville slime ramp corrects late ball observations without shifting the wave clock", () =>
        {
            var balls = new BlundervilleRampBalls();
            balls.Observe(new(1, BlundervilleCatalog.Ball, new(-5.5f, 20, BlundervilleRampBalls.SpawnZ)), Epoch);
            balls.Observe(new(1, BlundervilleCatalog.Ball, new(-5.5f, 20, -358)), Epoch.AddSeconds(4.5));
            var forecast = balls.Forecast(Epoch.AddSeconds(4.5), 3);
            Check(forecast.Any(b => b.X > 5 && Math.Abs((b.Spawn - Epoch).TotalSeconds - 5.2) < .001), "Motion correction shifted the next wave.");
            var observed = forecast.Single(b => b.X < -5);
            Check(Math.Abs(BlundervilleRampBalls.Z((Epoch.AddSeconds(4.5) - observed.Spawn).TotalSeconds)!.Value + 358) < .02, "Observed moving ball was not corrected.");
            Check(!balls.Ready(Epoch.AddSeconds(30)), "A stale ball clock still authorizes an ascent.");
            var late = new BlundervilleRampBalls();
            late.Observe(new(2, BlundervilleCatalog.Ball, new(1.8333f, 16, -350)), Epoch);
            Check(late.Forecast(Epoch, 1).Count > 0, "First observation halfway down the ramp was discarded.");
        });
        run("Blunderville slime ramp recorded R2 snowballs reproduce all four 164042 hits", () =>
        {
            using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ramp-snowballs-164042.json")));
            var players = data.RootElement.GetProperty("player").Deserialize<double[][]>()!;
            var hits = data.RootElement.GetProperty("hits").Deserialize<double[][]>()!;
            Check(hits.Length == 4, "Expected the four recorded 34776 hits.");
            foreach (var hit in hits)
            {
                var before = players.Where(p => p[0] >= hit[0] - .6 && p[0] <= hit[0]).ToArray();
                Check(before.All(p => Math.Abs(p[1] - hit[1]) > 1.3), "The recording no longer demonstrates the old radius error.");
                Check(before.Any(p => ActualBalls.Any(b => Math.Abs(b.X - hit[1]) < .1 && b.Z(p[0], false) is { } z
                    && Vector2.Distance(new((float)p[1], (float)p[3]), new(b.X, z)) < 2)), "Recorded hit was not covered by the independent R2 trajectory.");
            }
        });
        run("Blunderville slime ramp schedule forecasts every recorded push step", () =>
        {
            int checks = 0, misses = 0; var worst = 0.0;
            foreach (var (name, effects) in recordings)
            {
                var pushers = new BlundervilleRampPushers(); var next = 0;
                for (var t = 10.0; t < effects[^1].T - 12; t += 2)
                {
                    while (next < effects.Count && effects[next].T <= t) Feed(pushers, effects[next++]);
                    var now = Epoch.AddSeconds(t);
                    if (!pushers.Ready(now)) continue;
                    var steps = pushers.Steps(now.AddSeconds(10));
                    foreach (var e in effects.Where(e => e.T > t + .3 && e.T < t + 9.5))
                    {
                        var at = Epoch.AddSeconds(e.T);
                        // Packet starts can be stale actor positions; destinations are not.
                        var match = steps.Where(s => MathF.Abs(s.Z - e.Z) < .5f && (e.To is { } to ? MathF.Abs(s.ToX - to) < .4f : MathF.Abs(s.FromX - e.X) < .4f))
                            .Select(s => Math.Abs((s.At - at).TotalSeconds)).DefaultIfEmpty(9).Min();
                        ++checks; worst = Math.Max(worst, match);
                        if (match > .3) { ++misses; if (Debug) Console.WriteLine($"SLIMEMISS {name} t={t:F1} step {e.T:F2} ({e.X},{e.Z}) error {match:F2}"); }
                    }
                }
            }
            Console.WriteLine($"SLIME schedule: {checks} steps, {misses} beyond 0.3s, worst {worst:F2}s");
            // 2026-10-13: 1.7% beyond 0.3s, nearly all in the three sessions whose
            // packets jitter ±0.1–0.3s (01:20 also has no destinations at all).
            Check(checks > 14000 && misses < checks / 40, "The pusher schedule no longer forecasts the recorded steps.");
        });
        if (Environment.GetEnvironmentVariable("SLIME_TRACK") is { Length: > 0 } trackPath)
            run("Blunderville slime ramp recorded track diagnostic", () =>
            {
                foreach (var (name, effects) in recordings)
                    if (name.Contains(Environment.GetEnvironmentVariable("SLIME_ONLY") ?? "")) ReplayTrack(name, effects, trackPath);
            });
        run("Blunderville slime ramp climbs every recorded round without a push", () =>
        {
            var outcomes = new List<Outcome>();
            foreach (var (name, effects) in recordings)
            {
                if (Environment.GetEnvironmentVariable("SLIME_ONLY") is { Length: > 0 } only && !name.Contains(only)) continue;
                for (var k = 0; k < Phases; ++k)
                {
                    // SLIME_ARRIVE/SLIME_LANE replay one recorded arrival (e.g. 02:08: 59.7s at x=-4.2).
                    var arrive = double.TryParse(Environment.GetEnvironmentVariable("SLIME_ARRIVE"), out var fixedArrive) ? fixedArrive + k * .1 : 20 + k * 12.85 / Phases;
                    if (arrive + (Environment.GetEnvironmentVariable("SLIME_ARRIVE") != null ? 25 : 45) > effects[^1].T) continue;
                    var outcome = Simulate(name, effects, arrive, float.TryParse(Environment.GetEnvironmentVariable("SLIME_LANE"), out var fixedLane) ? fixedLane : k % 2 == 0 ? 1.5f : -1.5f);
                    outcomes.Add(outcome);
                    Console.WriteLine($"SLIME {name} arrive {arrive:F2}: {(outcome.Fall ?? "climbed")} {outcome.Climb:F2}s (stood {outcome.Stand:F2}s) slides {outcome.Slides} dispatch {outcome.DispatchRate:F1}/s pushes {outcome.Pushes.Count}"
                        + $"{(outcome.Pushes.Count > 0 ? " [" + string.Join("; ", outcome.Pushes) + "]" : "")} search {outcome.MaxMs:F0}ms");
                }
            }
            var climbed = outcomes.Where(o => o.Fall == null).ToArray();
            Console.WriteLine($"SLIME {outcomes.Count} climbs: pushes {outcomes.Sum(o => o.Pushes.Count)} in {outcomes.Count(o => o.Pushes.Count > 0)}, falls {outcomes.Count(o => o.Fall != null)},"
                + $" mean {climbed.Average(o => o.Climb):F2}s max {climbed.Max(o => o.Climb):F2}s (straight 14.5s), stood mean {climbed.Average(o => o.Stand):F2}s, slowest scan {outcomes.Max(o => o.MaxMs):F0}ms");
            Console.WriteLine($"SLIME input held on the slime: mean {outcomes.Average(o => o.Steered):P1}, min {outcomes.Min(o => o.Steered):P1}");
            Check(outcomes.All(o => o.Fall == null), "A slime ramp climb fell or stalled.");
            // 1.14.28: the foot wait is capped at 3s (01:45 ARR: 12.5s), then the
            // deepest partial plan climbs. A few of the 177 arrivals take contact.
            Check(outcomes.All(o => o.Pushes.Count == 0), "A slime ramp climb was pushed by a recorded pusher.");
            Console.WriteLine($"SLIME slides {outcomes.Sum(o => o.Slides)}, GreenMove dispatch mean {outcomes.Average(o => o.DispatchRate):F1}/s max {outcomes.Max(o => o.DispatchRate):F1}/s");
            Check(outcomes.All(o => o.Slides == 0), "Input dropped on the slime (native slide).");
        });
        run("Blunderville slime ramp 164042 actual arrival with recorded ball positions", () =>
        {
            var effects = recordings.Single(r => r.Name.Contains("16.40.42")).Effects;
            // The fifth attempt is cut off by elimination at course t=130.865;
            // its missing post-elimination animation cannot validate an ascent.
            foreach (var (arrive, lane) in new[] { (71.84, 1.48f), (85.2, -3.92f), (98.2, -4.013f), (110.3, -4.013f) })
            {
                var o = Simulate("2026.10.14 16.40.42", effects, arrive, lane);
                Console.WriteLine($"SLIME recorded arrival {arrive:F2}: {o.Climb:F2}s, pushes {string.Join(';', o.Pushes)}, slides {o.Slides}, search {o.MaxMs:F1}ms");
                Check(o.Fall == null && o.Pushes.Count == 0 && o.Slides == 0 && o.Steered == 1, "Actual recorded arrival failed continuous safe ascent.");
            }
        });
    }

    // SLIME_TRACK=<json [[t,x,y,z],...]> SLIME_ONLY=<recording>: drive the live
    // pipeline along MP's recorded positions and log what it publishes and
    // whether the runtime keeps GreenMove running (diagnostic, no checks).
    internal static void ReplayTrack(string name, IReadOnlyList<Effect> effects, string path)
    {
        var track = JsonSerializer.Deserialize<double[][]>(File.ReadAllText(path))!;
        var encounter = new BlundervilleEncounter(_ => { });
        var now = Epoch; var move = new Slime();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var slime = new BlundervilleSlimeRamp();
        var next = 0; uint sequence = 0; var previous = Vector3.Zero; var dispatches = 0; var wasRunning = false;
        var nextScan = 0.0; BlundervilleRaceDecision? decision = null;
        for (var i = 0; i < track.Length; ++i)
        {
            var t = track[i][0]; now = Epoch.AddSeconds(t); move.Now = now;
            var p = new Vector3((float)track[i][1], (float)track[i][2], (float)track[i][3]);
            if (p.Z > -334 || p.Z < -368) { previous = p; continue; }
            while (next < effects.Count && effects[next].T <= t)
            {
                var e = effects[next++];
                encounter.Effect(0x40002000UL + (ulong)e.Source, 34774, ++sequence, Row(e.X, e.Z), 0, Epoch.AddSeconds(e.T),
                    e.To is { } to ? Row(to, e.Z) : new Vector3(-1000, -1000, -1000));
            }
            var slick = p.Z <= BlundervilleSlimeRamp.Bottom;
            if (t >= nextScan)
            {
                nextScan = t + .1;
                api.UtcNowForTest = now;
                state.SynchronizeStatuses(slick ? [new(3703, 0, now.AddSeconds(30))] : [], now);
                decision = planner.Update(state, now, p, true, false, false, null);
                decision = slime.Apply(decision, state, encounter.RampPushers, p, now, planner.Recovering);
                nav.Apply(state, decision, encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var speed = slick ? 2f : 6f;
            var player = new PlayerState(p, 0, speed, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new MovementCapability(speed, speed, 0, false, speed, speed), now);
            var running = move.IsRunning;
            if (move.Dispatches != dispatches || running != wasRunning)
                Console.WriteLine($"SLIMETRACK t={t:F2} p=({p.X:F2},{p.Z:F2}) running={running} dispatch={move.Dispatches} phase={runtime.MovementStatus.PhaseText} {decision?.Hint}"
                    + $" target={decision?.ContinuousPath?.LastOrDefault()}");
            dispatches = move.Dispatches; wasRunning = running; previous = p;
        }
    }

    internal static Outcome Simulate(string name, IReadOnlyList<Effect> effects, double arrive, float lane,
        IReadOnlyList<RecordedBall>? recordedBalls = null, double[][]? bodySamples = null, float? slimeOnset = null, float ballHit = 2)
    {
        if (float.TryParse(Environment.GetEnvironmentVariable("SLIME_BALLHIT"), out var hitOverride)) ballHit = hitOverride;
        var encounter = new BlundervilleEncounter(_ => { });
        var now = Epoch.AddSeconds(arrive); var move = new Slime();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var spin = new BlundervilleSpinBridge(); var slime = new BlundervilleSlimeRamp();
        var p = new Vector3(lane, 10.03f, -336); move.Now = now;
        var next = 0; uint sequence = 0;
        var pushIndex = 0; while (pushIndex < effects.Count && effects[pushIndex].T - .15 <= arrive) ++pushIndex;
        var pushes = new List<string>(); string? fall = null; var stand = 0.0; var maxMs = 0.0;
        var hint = ""; var t = arrive; var previous = p; double? onSlime = null; int slickTicks = 0, steeredTicks = 0;
        var balls = Balls.TryGetValue(name, out var recorded) ? recorded : []; var lastBall = double.NegativeInfinity;
        var actual = recordedBalls != null || name.Contains("16.40.42");
        var actualBalls = recordedBalls ?? ActualBalls;
        if (actual) balls = actualBalls.Select(b => (b.Spawn, b.X)).ToList();
        float? BallZ(int index, double time, bool observed) => actual ? actualBalls[index].Z(time, observed) : BlundervilleRampBalls.Z(time - balls[index].T);
        if (actual)
            foreach (var sample in actualBalls.SelectMany((b, i) => b.Samples.Where(s => s[0] < arrive).Select(s => (i, b.X, T: s[0], Z: (float)s[1]))).OrderBy(s => s.T))
                encounter.RampBalls.Observe(new(0x40003000UL + (ulong)sample.i, BlundervilleCatalog.Ball, new(sample.X, 18.5f, sample.Z)), Epoch.AddSeconds(sample.T));
        var bodyIndex = 0; var bodyPositions = new Dictionary<int, double[]>(); var lastBody = double.NegativeInfinity;
        for (var tick = 0; tick < 9000; ++tick)
        {
            t = arrive + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            while (next < effects.Count && effects[next].T <= t)
            {
                var e = effects[next++];
                encounter.Effect(0x40002000UL + (ulong)e.Source, 34774, ++sequence, Row(e.X, e.Z), 0, Epoch.AddSeconds(e.T),
                    e.To is { } to ? Row(to, e.Z) : new Vector3(-1000, -1000, -1000));
            }
            var slick = p.Z <= (slimeOnset ?? SlimeEdge) && p.Z > BlundervilleSlimeRamp.Top;
            move.Slick = slick;
            while (bodySamples != null && bodyIndex < bodySamples.Length && bodySamples[bodyIndex][0] <= t)
            {
                var body = bodySamples[bodyIndex++]; bodyPositions[(int)body[1]] = body;
            }
            // Independently observed resting endpoints. A fresh ActorMove
            // confirms arrival; only the following 0.7s is certainly stationary.
            if (slick && t > lastBody + 1 && bodyPositions.Values.Any(b => t - b[0] < .7
                && Vector2.Distance(new(p.X, p.Z), new((float)b[2], (float)b[4])) <= 1))
            { pushes.Add($"t={t:F2} ({p.X:F2},{p.Z:F2}) recorded resting pusher body"); lastBody = t; }
            // Latest ARR uses actual actor positions; collision truth is the
            // independent R2 action footprint, never the planner's clearance.
            for (var b = 0; b < balls.Count; ++b)
            {
                var (spawned, bx) = balls[b];
                if (spawned > t) break;
                if (BallZ(b, t, true) is { } seen)
                    encounter.RampBalls.Observe(new(0x40003000UL + (ulong)b, BlundervilleCatalog.Ball, new(bx, 18.5f, seen)), now);
                if (slick && BallZ(b, t, false) is { } bz && Vector2.Distance(new(p.X, p.Z), new(bx, bz)) < ballHit && t > lastBall + 1)
                { pushes.Add($"t={t:F2} ({p.X:F1},{p.Z:F1}) snowball x={bx}"); lastBall = t; }
            }
            if (slick) onSlime ??= t;
            if (tick % 10 == 0)
            {
                api.UtcNowForTest = now;
                // Exercise the live navigation input too: observing spawn times alone
                // never registers the rolling ball envelopes that can stop GreenMove.
                encounter.Synchronize(balls.Select((b, i) => (b, i))
                    .Where(b => BallZ(b.i, t, true) != null)
                    .Select(b => new BlundervilleActor(0x40003000UL + (ulong)b.i, BlundervilleCatalog.Ball,
                        new(b.b.X, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstB,
                            BallZ(b.i, t, true)!.Value), BallZ(b.i, t, true)!.Value))), now);
                state.SynchronizeStatuses(slick ? [new(3703, 0, now.AddSeconds(30))] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null);
                decision = spin.Apply(decision, state, encounter.SpinBlades, p, move.Rotation, now, planner.Recovering);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                decision = slime.Apply(decision, state, encounter.RampPushers, p, now, planner.Recovering,
                    Environment.GetEnvironmentVariable("SLIME_NOBALLS") == "1" ? null : encounter.RampBalls);
                // Baseline for comparison: straight up the arrival lane, no timing.
                if (Environment.GetEnvironmentVariable("SLIME_NAIVE") == "1" && p.Z <= BlundervilleSlimeRamp.Begin)
                    decision = decision with { DirectTraversal = true, ContinuousPath = [p with { Z = p.Z - 2 }], Target = p with { Z = p.Z - 2 }, Hint = "naive" };
                maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                if (Debug && decision.Hint != hint) Console.WriteLine($"SLIMEDBG {name} t={t:F2} p=({p.X:F2},{p.Z:F2}) {decision.Hint}");
                hint = decision.Hint;
                if (Debug && slick) Console.WriteLine($"BALLDBG t={t:F2} p=({p.X:F2},{p.Z:F2}) complete={slime.Complete} aim={decision.Target} live={string.Join(';', encounter.RampBalls.Forecast(now, 0).Select(b => $"{b.X:F1}@{(b.Spawn-Epoch).TotalSeconds:F2}"))}");
                nav.Apply(state, decision, encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var speed = slick ? 2f : 6f;
            var player = new PlayerState(p, move.Rotation, speed, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new MovementCapability(speed, speed, 0, false, speed, speed), now);
            if (slick)
            {
                ++slickTicks;
                if (move.IsRunning) ++steeredTicks;
                else if (Debug) Console.WriteLine($"SLIMEIDLE {name} t={t:F2} p=({p.X:F2},{p.Z:F2}) hint={hint}");
            }
            previous = p; p = move.Advance(p, .01f);
            p.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstB, p.Z);
            if (Vector3.DistanceSquared(previous, p) < 1e-8f) stand += .01;
            // Recorded pushes by the measured rule; a push throws the player to the step's end.
            while (pushIndex < effects.Count && effects[pushIndex].T - .15 <= t + .01)
            {
                var e = effects[pushIndex++];
                var to = e.To ?? Following(effects, pushIndex - 1);
                if (to is not { } end) continue;
                // A packet's start can be a stale actor position (01:02: -6.88
                // for a -3.52 -> 0 step); the previous destination is not.
                var begin = Previous(effects, pushIndex - 1) ?? e.X;
                if (MathF.Abs(p.Z - e.Z) <= BlundervilleRampPushers.Half && p.X >= MathF.Min(begin, end) && p.X <= MathF.Max(begin, end))
                {
                    pushes.Add($"t={e.T:F2} ({p.X:F1},{p.Z:F1}) step {begin:F1}->{end:F1}");
                    p.X = end;
                }
            }
            if (p.Z is < BlundervilleSlimeRamp.Bottom and > BlundervilleSlimeRamp.Top && MathF.Abs(p.X) > 6.4f) { fall = $"fell at t={t:F2} ({p.X:F2},{p.Z:F2})"; break; }
            if (p.Z <= BlundervilleSlimeRamp.Top) return new(name, arrive, t - (onSlime ?? arrive), stand, pushes, null, maxMs, steeredTicks / (double)Math.Max(1, slickTicks),
                move.Slides, move.Dispatches / Math.Max(1, t - arrive), t);
        }
        return new(name, arrive, t - (onSlime ?? arrive), stand, pushes, fall ?? $"stalled at ({p.X:F2},{p.Z:F2})", maxMs);
    }

    private static Vector3 Row(float x, float z) => new(x, BlundervilleRoutes.HeightHint(BlundervilleCourse.FirstB, z) + 3.6f, z);

    // 01:20 packets lack the destination: the same pusher's next start.
    private static float? Following(IReadOnlyList<Effect> effects, int index)
    {
        for (var i = index + 1; i < effects.Count && effects[i].T < effects[index].T + 2.5; ++i)
            if (effects[i].Source == effects[index].Source) return effects[i].X;
        return null;
    }

    private static float? Previous(IReadOnlyList<Effect> effects, int index)
    {
        for (var i = index - 1; i >= 0 && effects[i].T > effects[index].T - 2.5; --i)
            if (effects[i].Source == effects[index].Source) return effects[i].To ?? effects[index].X;
        return null;
    }

    private static void Feed(BlundervilleRampPushers pushers, Effect e)
        => pushers.Effect(0x40002000UL + (ulong)e.Source, Row(e.X, e.Z), e.To is { } to ? Row(to, e.Z) : null, Epoch.AddSeconds(e.T));

    // Snowball spawns (course time, lane x) per recording.
    private static readonly Dictionary<string, List<(double T, float X)>> Balls = [];
    private static readonly List<RecordedBall> ActualBalls = [];

    private static List<(string Name, List<Effect> Effects)> Load()
    {
        using var actual = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ramp-snowballs-164042.json")));
        ActualBalls.Clear();
        foreach (var b in actual.RootElement.GetProperty("balls").EnumerateArray())
            ActualBalls.Add(new(b.GetProperty("spawn").GetDouble(), b.GetProperty("x").GetSingle(), b.GetProperty("samples").Deserialize<double[][]>()!,
                b.GetProperty("end").ValueKind == JsonValueKind.Null ? double.PositiveInfinity : b.GetProperty("end").GetDouble()));
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ramp-recorded.json")));
        var result = new List<(string, List<Effect>)>();
        foreach (var recording in data.RootElement.GetProperty("recordings").EnumerateObject())
        {
            if (recording.Value.TryGetProperty("balls", out var balls))
                Balls[recording.Name] = balls.EnumerateArray().Select(b => (b[0].GetDouble(), b[1].GetSingle())).ToList();
            result.Add((recording.Name, recording.Value.GetProperty("effects").EnumerateArray()
                .Select(e => new Effect(e[0].GetDouble(), e[1].GetInt32(), e[2].GetSingle(), e[3].GetSingle(),
                    e[4].ValueKind == JsonValueKind.Null ? null : e[4].GetSingle())).ToList()));
        }
        return result;
    }

    // Off the slime: walks at 6m/s and stops. On it: the input runs at 6m/s
    // toward the current point and the ramp adds Drift m/s down it (+z);
    // without input it slides down at Drift m/s.
    private sealed class Slime : IGreenMoveSystem
    {
        private static readonly float SlideAngle = float.TryParse(Environment.GetEnvironmentVariable("SLIME_ANGLE"), out var degrees) ? degrees * MathF.PI / 180 : 50 * MathF.PI / 180;
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready, _slideUntil;
        public DateTime Now; public bool Slick; public float Rotation; public int Dispatches, Slides;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; ++Dispatches; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            while (IsRunning && Flat(_points[_index].Position - p).Length() <= Math.Max(_points[_index].Tolerance, .01f)) ++_index;
            var input = Vector3.Zero;
            if (IsRunning && Now >= _ready)
            {
                var d = Flat(_points[_index].Position - p);
                if (d.LengthSquared() > 1e-6f) { input = Vector3.Normalize(d); Rotation = MathF.Atan2(input.X, input.Z); }
            }
            // 02:08/04:10 ARRs (server move state 3): a frame without input, or
            // steering well off straight up, starts a slide that ignores input
            // for about a second (MP slid 10x as often as humans while doing so).
            var sideways = input != Vector3.Zero && -input.Z < MathF.Cos(SlideAngle);
            if (Slick && (input == Vector3.Zero || sideways) && Now >= _slideUntil) { _slideUntil = Now.AddSeconds(1); ++Slides; }
            if (Slick && Now < _slideUntil) input = Vector3.Zero;
            if (Slick) return p + (input * 6 + new Vector3(0, 0, Drift)) * dt;
            if (input == Vector3.Zero) return p;
            var distance = Flat(_points[_index].Position - p).Length();
            return p + input * MathF.Min(distance, 6 * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
