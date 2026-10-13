using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// 2026-10-13 13:20 ARR: MP was knocked off the FirstB spin bridge twice and
// eliminated; 12 of 18 FirstB ARRs show 1–6 knockbacks there. Under 3702 the
// character moves at 3.6m/s and its facing turns toward the input at ~40°/s;
// it cannot stop. Replays every recorded blade schedule through the script's
// decision chain with that motion, arriving at the bridge at many phases, and
// scores each blade effect by the measured rule: the player's centre inside the
// 3x3m box 0.05s before the packet.
internal static class BlundervilleSpinBridgeRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 6, 0, 0, DateTimeKind.Utc);
    private static readonly bool Debug = Environment.GetEnvironmentVariable("SPIN_DEBUG") == "1";
    private static readonly int Phases = int.TryParse(Environment.GetEnvironmentVariable("SPIN_PHASES"), out var phases) ? phases : 6;
    // The game's 3702 motion (13:20 ARR); SPIN_SPEED/SPIN_RATE stress the planner's model.
    private static readonly float Rate = (float.TryParse(Environment.GetEnvironmentVariable("SPIN_RATE"), out var rate) ? rate : 40) * MathF.PI / 180;
    private static readonly float SpinSpeed = float.TryParse(Environment.GetEnvironmentVariable("SPIN_SPEED"), out var speed) ? speed : 3.6f;

    internal sealed record Effect(double T, float X, float Z, float Rotation);
    internal sealed record Outcome(string Name, double Arrive, double Elapsed, double Stand, List<string> Hits, string? Fall, int Tiers, int Blind, double MaxMs, double Steered = 1);

    public static void Run(Action<string, Action> run)
    {
        var recordings = Load();
        run("Blunderville spin schedule predicts every recorded blade", () =>
        {
            int checks = 0, misses = 0, wide = 0, notReady = 0; var worst = 0.0;
            foreach (var (name, effects) in recordings)
            {
                var schedule = new BlundervilleSpinSchedule(); var next = 0;
                for (var t = 15.0; t < effects[^1].T - 21; t += 3)
                {
                    while (next < effects.Length && effects[next].T <= t)
                    { var e = effects[next++]; schedule.Effect(new(e.X, 10, e.Z), e.Rotation, Epoch.AddSeconds(e.T)); }
                    var now = Epoch.AddSeconds(t);
                    if (!schedule.Ready(now)) { ++notReady; continue; }
                    var predicted = schedule.Impacts(now, now.AddSeconds(20.5));
                    foreach (var e in effects.Where(e => e.T - BlundervilleSpinSchedule.Lead > t && e.T < t + 20))
                    {
                        var at = Epoch.AddSeconds(e.T - BlundervilleSpinSchedule.Lead);
                        var error = predicted.Where(p => MathF.Abs(p.X - e.X) < .1f && MathF.Abs(p.Z - e.Z) < .1f)
                            .Select(p => Math.Abs((p.At - at).TotalSeconds)).DefaultIfEmpty(9).Min();
                        ++checks; worst = Math.Max(worst, error);
                        if (error > .3) ++wide;
                        if (error > .1) { ++misses; if (Debug) Console.WriteLine($"SPINMISS {name} t={t:F1} effect {e.T:F2} ({e.X},{e.Z}) error {error:F2}"); }
                    }
                }
            }
            Console.WriteLine($"SPIN schedule: {checks} effects, {misses} beyond 0.1s, {wide} beyond 0.3s, worst {worst:F2}s, {notReady} checkpoints not ready");
            // 2026-10-13: 3.8% beyond 0.1s, nearly all in four sessions whose packets
            // jitter ±0.2s (01:20, 00:14, 23:19, 01:42); the few beyond 0.3s are
            // stalls that deliver several rows at one instant (2.1s in 19:51).
            Check(checks > 60000 && misses < checks / 20 && wide < checks / 200, "The blade clock no longer predicts the recorded effects.");
        });
        run("Blunderville spin bridge crosses every recorded round without a knockback", () =>
        {
            var outcomes = new List<Outcome>();
            foreach (var (name, effects) in recordings)
            {
                if (Environment.GetEnvironmentVariable("SPIN_ONLY") is { Length: > 0 } only && !name.Contains(only)) continue;
                for (var k = 0; k < Phases; ++k)
                {
                    var arrive = 26 + k * 4.28 / Phases;
                    // 3702 arrives with the network: 18 ARRs gained it at z -270.6..-272.5.
                    var outcome = Simulate(name, effects, arrive, k % 2 == 0 ? 5 : -5, -270.6f - (k * 7 % 20) * .1f);
                    outcomes.Add(outcome);
                    Console.WriteLine($"SPIN {name} arrive {arrive:F2}: {(outcome.Fall ?? "crossed")} {outcome.Elapsed:F2}s (stood {outcome.Stand:F2}s) hits {outcome.Hits.Count}"
                        + $"{(outcome.Hits.Count > 0 ? " [" + string.Join("; ", outcome.Hits) + "]" : "")} narrower tiers {outcome.Tiers} blind {outcome.Blind} search {outcome.MaxMs:F0}ms");
                }
            }
            var crossed = outcomes.Where(o => o.Fall == null).ToArray();
            Console.WriteLine($"SPIN steering accepted on the bridge: mean {outcomes.Average(o => o.Steered):P1}, min {outcomes.Min(o => o.Steered):P1}");
            Console.WriteLine($"SPIN {outcomes.Count} crossings: hits {outcomes.Sum(o => o.Hits.Count)} in {outcomes.Count(o => o.Hits.Count > 0)}, falls {outcomes.Count(o => o.Fall != null)},"
                + $" mean {crossed.Average(o => o.Elapsed):F2}s max {crossed.Max(o => o.Elapsed):F2}s, stood mean {crossed.Average(o => o.Stand):F2}s, slowest search {outcomes.Max(o => o.MaxMs):F0}ms");
            // 2026-10-13, 18 rounds x 6 arrivals (24 and model stress via SPIN_PHASES,
            // SPIN_SPEED 3.55–3.65, SPIN_RATE 36–44): no knockback, no fall, GreenMove
            // steering the whole bridge, 21.3s mean from z -255 (17.6s of it on the
            // bridge; a straight walk takes 20.1s), the gate wait 0.96s on average.
            Check(outcomes.All(o => o.Fall == null), "A spin bridge crossing fell or stalled.");
            Check(outcomes.All(o => o.Hits.Count == 0), "A spin bridge crossing was knocked by a recorded blade.");
            Check(outcomes.All(o => o.Steered > .95), "The runtime refused the spin bridge steering.");
            Check(crossed.Average(o => o.Elapsed) < 22.5, "The spin bridge crossing became slow.");
        });
    }

    internal static Outcome Simulate(string name, IReadOnlyList<Effect> effects, double arrive, float lane, float status = -271)
    {
        var encounter = new BlundervilleEncounter(_ => { });
        var now = Epoch.AddSeconds(arrive); var move = new Spinning();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new());
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var spin = new BlundervilleSpinBridge();
        var p = new Vector3(lane, 10.03f, -255); move.Now = now;
        var next = 0; uint sequence = 0;
        var hitIndex = 0; while (hitIndex < effects.Count && effects[hitIndex].T - BlundervilleSpinSchedule.Lead <= arrive) ++hitIndex;
        var hits = new List<string>(); string? fall = null; var stand = 0.0; var tiers = 0; var blind = 0; var maxMs = 0.0;
        var hint = ""; var t = arrive; var previous = p; int spinTicks = 0, steeredTicks = 0;
        for (var tick = 0; tick < 6000; ++tick)
        {
            t = arrive + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            while (next < effects.Count && effects[next].T <= t)
            {
                var e = effects[next++];
                encounter.Effect(Source(e), 34775, ++sequence, new(e.X, 10, e.Z), e.Rotation, Epoch.AddSeconds(e.T));
            }
            var spinning = p.Z <= status && p.Z > -332.5f;
            // The 13:20 ARR: the character halts ~0.15s as the status lands.
            if (spinning && !move.Spin) move.Stall = now.AddSeconds(.15);
            move.Spin = spinning;
            if (tick % 10 == 0)
            {
                api.UtcNowForTest = now;
                state.SynchronizeStatuses(spinning ? [new(3702, 7, now.AddSeconds(30))] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                decision = spin.Apply(decision, state, encounter.SpinBlades, p, move.Rotation, now, planner.Recovering);
                maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                if (spinning && spin.Current is { Tier: > 1 }) ++tiers;
                if (spinning && spin.Current == null) ++blind;
                if (Debug && decision.Hint != hint) Console.WriteLine($"SPINDBG {name} t={t:F2} p=({p.X:F2},{p.Z:F2}) heading={move.Heading * 180 / MathF.PI:F0} {decision.Hint}");
                hint = decision.Hint;
                nav.Apply(state, decision, encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var speed = spinning ? 3.6f : 6f;
            var player = new PlayerState(p, move.Rotation, speed, spinning || Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new MovementCapability(speed, speed, 0, false, speed, speed), now);
            if (spinning) { ++spinTicks; if (move.IsRunning) ++steeredTicks; }
            previous = p; p = move.Advance(p, .01f) with { Y = 10.03f };
            if (Vector3.DistanceSquared(previous, p) < 1e-8f) stand += .01;
            while (hitIndex < effects.Count && effects[hitIndex].T - BlundervilleSpinSchedule.Lead <= t + .01)
            {
                var e = effects[hitIndex++];
                if (p.Z <= -270.5f && MathF.Abs(p.X - e.X) < BlundervilleSpinSchedule.Half && MathF.Abs(p.Z - e.Z) < BlundervilleSpinSchedule.Half)
                    hits.Add($"t={e.T:F2} ({p.X:F1},{p.Z:F1}) box ({e.X},{e.Z})");
            }
            if (p.Z is <= -270.5f and >= -334.75f && p.X is < -7.78f or > 7.53f) { fall = $"fell at t={t:F2} ({p.X:F2},{p.Z:F2})"; break; }
            if (p.Z < -334.5f) return new(name, arrive, t - arrive, stand, hits, null, tiers, blind, maxMs, steeredTicks / (double)Math.Max(1, spinTicks));
        }
        return new(name, arrive, t - arrive, stand, hits, fall ?? $"stalled at ({p.X:F2},{p.Z:F2})", tiers, blind, maxMs);
    }

    private static ulong Source(Effect e) => 0x40001000UL + (ulong)((MathF.Round(-e.Z) - 270) * 20 + MathF.Round(e.X * 2) + 16);

    private static List<(string Name, Effect[] Effects)> Load()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "spin-recorded.json")));
        var result = new List<(string, Effect[])>();
        foreach (var recording in data.RootElement.GetProperty("recordings").EnumerateObject())
            result.Add((recording.Name, recording.Value.GetProperty("effects").EnumerateArray()
                .Select(e => new Effect(e[0].GetDouble(), e[1].GetSingle(), e[2].GetSingle(), e[3].GetSingle())).ToArray()));
        return result;
    }

    // Before 3702: walks at 6m/s and stops. Under 3702: always 3.6m/s along
    // the facing, which turns toward the current point at no more than 40°/s.
    private sealed class Spinning : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now, Stall; public bool Spin;
        // 0 down the bridge (-z), positive toward +x; Rotation is the game's (0 = +z).
        public float Heading;
        public float Rotation => MathF.PI - Heading;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
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
            if (Spin)
            {
                if (IsRunning && Now >= _ready)
                {
                    var d = Flat(_points[_index].Position - p);
                    var delta = MathF.Atan2(d.X, -d.Z) - Heading;
                    while (delta > MathF.PI) delta -= 2 * MathF.PI;
                    while (delta < -MathF.PI) delta += 2 * MathF.PI;
                    Heading += Math.Clamp(delta, -Rate * dt, Rate * dt);
                }
                return Now < Stall ? p : p + new Vector3(MathF.Sin(Heading), 0, -MathF.Cos(Heading)) * SpinSpeed * dt;
            }
            if (!IsRunning || Now < _ready) return p;
            var v = Flat(_points[_index].Position - p); var distance = v.Length();
            if (distance < 1e-4f) return p;
            Heading = MathF.Atan2(v.X, -v.Z);
            return p + v / distance * MathF.Min(distance, 6 * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
