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

    internal sealed record Effect(double T, int Source, float X, float Z, float? To);
    internal sealed record Outcome(string Name, double Arrive, double Climb, double Stand, List<string> Pushes, string? Fall, double MaxMs, double Steered = 1, int Slides = 0, double DispatchRate = 0);

    public static void Run(Action<string, Action> run)
    {
        var recordings = Load();
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
            Check(outcomes.All(o => o.Pushes.Count == 0), "A slime ramp climb was pushed by a recorded pusher.");
            Console.WriteLine($"SLIME slides {outcomes.Sum(o => o.Slides)}, GreenMove dispatch mean {outcomes.Average(o => o.DispatchRate):F1}/s max {outcomes.Max(o => o.DispatchRate):F1}/s");
            Check(outcomes.All(o => o.Slides == 0), "Input dropped on the slime (native slide).");
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

    internal static Outcome Simulate(string name, IReadOnlyList<Effect> effects, double arrive, float lane)
    {
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
        for (var tick = 0; tick < 9000; ++tick)
        {
            t = arrive + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            while (next < effects.Count && effects[next].T <= t)
            {
                var e = effects[next++];
                encounter.Effect(0x40002000UL + (ulong)e.Source, 34774, ++sequence, Row(e.X, e.Z), 0, Epoch.AddSeconds(e.T),
                    e.To is { } to ? Row(to, e.Z) : new Vector3(-1000, -1000, -1000));
            }
            var slick = p.Z <= BlundervilleSlimeRamp.Bottom && p.Z > BlundervilleSlimeRamp.Top;
            move.Slick = slick;
            if (slick) onSlime ??= t;
            if (tick % 10 == 0)
            {
                api.UtcNowForTest = now;
                state.SynchronizeStatuses(slick ? [new(3703, 0, now.AddSeconds(30))] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null);
                decision = spin.Apply(decision, state, encounter.SpinBlades, p, move.Rotation, now, planner.Recovering);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                decision = slime.Apply(decision, state, encounter.RampPushers, p, now, planner.Recovering);
                // Baseline for comparison: straight up the arrival lane, no timing.
                if (Environment.GetEnvironmentVariable("SLIME_NAIVE") == "1" && p.Z <= BlundervilleSlimeRamp.Begin)
                    decision = decision with { DirectTraversal = true, ContinuousPath = [p with { Z = p.Z - 2 }], Target = p with { Z = p.Z - 2 }, Hint = "naive" };
                maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                if (Debug && decision.Hint != hint) Console.WriteLine($"SLIMEDBG {name} t={t:F2} p=({p.X:F2},{p.Z:F2}) {decision.Hint}");
                hint = decision.Hint;
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
                move.Slides, move.Dispatches / Math.Max(1, t - arrive));
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

    private static List<(string Name, List<Effect> Effects)> Load()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ramp-recorded.json")));
        var result = new List<(string, List<Effect>)>();
        foreach (var recording in data.RootElement.GetProperty("recordings").EnumerateObject())
            result.Add((recording.Name, recording.Value.GetProperty("effects").EnumerateArray()
                .Select(e => new Effect(e[0].GetDouble(), e[1].GetInt32(), e[2].GetSingle(), e[3].GetSingle(),
                    e[4].ValueKind == JsonValueKind.Null ? null : e[4].GetSingle())).ToList()));
        return result;
    }

    // Off the slime: walks at 6m/s and stops. On it: the input runs at 6m/s
    // toward the current point and the ramp adds Drift m/s down it (+z);
    // without input it slides down at Drift m/s.
    private sealed class Slime : IGreenMoveSystem
    {
        private static readonly float SlideAngle = float.TryParse(Environment.GetEnvironmentVariable("SLIME_ANGLE"), out var degrees) ? degrees * MathF.PI / 180 : 20 * MathF.PI / 180;
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
