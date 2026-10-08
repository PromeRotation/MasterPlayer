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

internal static class BlundervilleAnimationRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run(Action<string, Action> run)
    {
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville"), "rhythm-*.json"))
            run($"Blunderville animation cross recording {Path.GetFileNameWithoutExtension(file)}", () =>
            {
                using var replay = new Recording(Path.GetFileName(file));
                var counts = new Dictionary<(ulong, uint), int>(); var covered = 0; var total = 0;
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit"))
                {
                    var source = e[2].GetUInt64(); var action = e[3].GetUInt32(); var key = (source, action);
                    var seen = counts.GetValueOrDefault(key) + 1; counts[key] = seen;
                    var t = e[0].GetDouble(); replay.Advance(Math.Max(0, t - .35));
                    var actor = replay.Actors[source];
                    // North hammers use the earlier helper-turn tracker. Side lanes learn independently.
                    if (action == 34716 && MathF.Abs(actor.Position.X + 200) <= 6) continue;
                    var warmup = action == 34773 ? 18 : action is 34775 or 34716 ? 6
                        : action == 34801 ? actor.Position.Z > 215 ? 18 : 10 : 4;
                    if (seen <= warmup) continue;
                    ++total;
                    var pose = replay.Events.LastOrDefault(p => p[1].GetString() == "turn" && p[2].GetUInt64() == source && p[0].GetDouble() <= t);
                    var hitPosition = pose.ValueKind == JsonValueKind.Undefined ? actor.Position : Vec(pose[4]);
                    if (replay.Encounter.Hazards(Epoch.AddSeconds(t - .35)).Any(h => h.Id.StartsWith($"rhythm/{source}/{action}/")
                        && h.Geometry.Contains(hitPosition, .01f)
                        && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t))) ++covered;
                    else Console.WriteLine($"RHYTHM missing source={source} action={action} time={t:F3} position={hitPosition}");
                }
                Console.WriteLine($"RHYTHM {Path.GetFileName(file)}: {covered}/{total} predicted at 350ms lead");
                Check(total > 10 && covered == total, "A learned no-cast animation was missing before its actual hit.");
                replay.Encounter.Synchronize([], Epoch.AddSeconds(60));
                Check(!replay.Encounter.Hazards(Epoch.AddSeconds(60)).Any(), "Despawn retained rhythm predictions.");
            });
        run("Blunderville animation rhythm requires two cycles and drops invalid timing", () =>
        {
            var encounter = new BlundervilleEncounter(_ => { });
            var actor = new BlundervilleActor(1, 16338, new(10, 6, 267.5f));
            encounter.Synchronize([actor], Epoch);
            encounter.Effect(1, 34802, 1, actor.Position, 0, Epoch);
            encounter.Effect(1, 34802, 2, actor.Position, 0, Epoch.AddSeconds(1.38));
            Check(!encounter.Hazards(Epoch.AddSeconds(2)).Any(), "One interval invented a cycle.");
            encounter.Effect(1, 34802, 3, actor.Position, 0, Epoch.AddSeconds(2.76));
            Check(encounter.Hazards(Epoch.AddSeconds(3)).Any(h => h.From > Epoch.AddSeconds(3)), "Two matching cycles failed to forecast.");
            encounter.Effect(1, 34802, 4, actor.Position, 0, Epoch.AddSeconds(5.8));
            Check(!encounter.Hazards(Epoch.AddSeconds(6.2)).Any(), "An interrupted cycle retained stale predictions.");
        });
        run("Blunderville animation recorded sweeps are forecast before the effect", () =>
        {
            using var replay = new Recording();
            var total = 0; var covered = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit"))
            {
                var t = e[0].GetDouble();
                if (t < .4 || t > 65) continue;
                replay.Advance(t - .3);
                var hazards = replay.Encounter.Hazards(Epoch.AddSeconds(t - .3));
                if (hazards.Any(h => h.Id.StartsWith($"animation/{e[2].GetUInt64()}/")
                    && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t))) ++covered;
                ++total;
            }
            Console.WriteLine($"ANIMATION forecasts {covered}/{total} at 300ms lead");
            Check(total > 1000 && covered == total, "Recorded effects were not predicted before their packets arrived.");
        });
        run("Blunderville animation all seven recorded knockbacks fall inside the centered helper rectangle", () =>
        {
            using var replay = new Recording();
            foreach (var hit in replay.Data.RootElement.GetProperty("hits").EnumerateArray())
            {
                var t = hit.GetProperty("time").GetDouble(); replay.Advance(t - .3);
                var id = hit.GetProperty("source").GetUInt64(); var p = Vec(hit.GetProperty("position"));
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - .3)).Any(h => h.Id.StartsWith($"animation/{id}/")
                    && h.Geometry.Contains(p) && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t)),
                    $"Recorded hit {t:F3} at {p} was outside its predicted geometry/window.");
            }
        });
        run("Blunderville animation Delivery north sweeps are forecast before impact", () =>
        {
            using var replay = new Recording("animation-delivery-041020.json");
            var count = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit"))
            {
                var t = e[0].GetDouble(); if (t < .4) continue;
                replay.Advance(t - .3);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - .3)).Any(h => h.Id.StartsWith($"animation/{e[2].GetUInt64()}/")
                    && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t)),
                    $"Delivery sweep at {t:F3} had no advance prediction."); ++count;
            }
            Check(count > 90, "Delivery fixture did not exercise repeated alternate sweeps.");
        });
        run("Blunderville animation Final alternate endpoints predict the evening recording", () =>
        {
            using var replay = new Recording("animation-final-194843.json");
            var count = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 29795 or 34796))
            {
                var t = e[0].GetDouble(); if (t < 8) continue;
                replay.Advance(t - 1);
                var source = e[2].GetUInt64();
                var pose = replay.Events.LastOrDefault(p => p[1].GetString() == "turn" && p[2].GetUInt64() == source && p[0].GetDouble() <= t);
                var position = pose.ValueKind == JsonValueKind.Undefined ? replay.Actors[source].Position : Vec(pose[4]);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - 1)).Any(h => h.Id.StartsWith($"pulse/{source}/")
                    && h.Geometry.Contains(position) && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t)
                    && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t)), $"Final pulse {source} at {t:F3} was not predicted one second ahead.");
                ++count;
            }
            Check(count > 30, "The Final fixture did not cover repeated endpoint changes.");
            replay.Encounter.Synchronize([], Epoch.AddSeconds(60));
            Check(!replay.Encounter.Hazards(Epoch.AddSeconds(60)).Any(), "Despawn retained Final forecasts.");
        });
        run("Blunderville animation Final learning cache and invalidation", () =>
        {
            var engine = new BlundervilleEncounter(_ => { });
            var a = new BlundervilleActor(1, 16338, new(-6.782f, 36.106f, 136.908f));
            var b = a with { Position = new(-3.218f, 36.302f, 135.092f) };
            engine.Synchronize([a], Epoch); engine.Effect(1, 29795, 1, a.Position, 0, Epoch);
            Check(!engine.Hazards(Epoch).Any(), "A single effect invented a period.");
            engine.Synchronize([b], Epoch.AddSeconds(2.15)); engine.Effect(1, 29795, 2, b.Position, 0, Epoch.AddSeconds(2.5));
            Check(!engine.Hazards(Epoch.AddSeconds(2.5)).Any(), "One interval invented a reliable alternating cycle.");
            engine.Synchronize([a], Epoch.AddSeconds(4.65)); engine.Effect(1, 29795, 3, a.Position, 0, Epoch.AddSeconds(5));
            var first = engine.Hazards(Epoch.AddSeconds(5)).First();
            engine.Synchronize([a], Epoch.AddSeconds(5.1));
            Check(ReferenceEquals(first, engine.Hazards(Epoch.AddSeconds(5.1)).First()), "Stable Final scans rebuilt forecasts.");
            Check(!engine.Hazards(Epoch.AddSeconds(20)).Any(), "A lost helper signal kept danger forever.");
            engine.Synchronize([a with { Position = new(-8, 36, 136) }], Epoch.AddSeconds(20.1));
            Check(!engine.Hazards(Epoch.AddSeconds(20.1)).Any(), "A third endpoint reused an invalid period.");
            engine.Reset();
            Check(!engine.Hazards(Epoch.AddSeconds(21)).Any(), "Reset retained learned Final endpoints.");
        });
        foreach (var action in new[] { 34716u, 34718u })
            run($"Blunderville animation {action} lifecycle and stable scan cache", () =>
            {
                var engine = new BlundervilleEncounter(_ => { });
                var actor = new BlundervilleActor(1, 16338, action == 34716 ? new(-209, 12.2058f, -130.3f) : new(-198, 10.07f, -261), Rotation: MathF.PI / 2);
                engine.Synchronize([actor], Epoch);
                Check(!engine.Hazards(Epoch).Any(), "An idle spawn invented an animation clock.");
                engine.Map((byte)(action == 34716 ? 0 : 12), 0x20001, Epoch);
                engine.Synchronize([actor], Epoch.AddSeconds(.1));
                var first = engine.Hazards(Epoch.AddSeconds(.1)).First();
                engine.Synchronize([actor], Epoch.AddSeconds(.2));
                Check(ReferenceEquals(first, engine.Hazards(Epoch.AddSeconds(.2)).First()), "Stable scans rebuilt forecasts and invalidated navigation.");
                Check(!engine.Hazards(Epoch.AddSeconds(15)).Any(), "Lost animation signals left permanent danger.");
                engine.Synchronize([actor with { Rotation = -MathF.PI / 2 }], Epoch.AddSeconds(16));
                Check(engine.Hazards(Epoch.AddSeconds(16)).Any(), "A new turn failed to recover the animation clock.");
                engine.Remove(1); Check(!engine.Hazards(Epoch.AddSeconds(16)).Any(), "Despawn retained predictions.");
                engine.Synchronize([actor], Epoch.AddSeconds(17));
                engine.Effect(1, action, 1, actor.Position, actor.Rotation, Epoch.AddSeconds(17.5));
                Check(engine.Hazards(Epoch.AddSeconds(17.5)).Any(), "Mid-sweep load could not synchronize on an actual effect.");
                engine.Tick(Epoch); Check(!engine.Hazards(Epoch).Any(), "Rewind retained future danger.");
            });
        foreach (var latency in new[] { .08, .3 })
        {
            run($"Blunderville animation latest race start latency {latency:F2}", () => Simulate(.1, latency, false,
                "animation-start-004353.json", new(-201.5f, 11.28f, -105)));
            run($"Blunderville animation recorded slope stall latency {latency:F2}", () => Simulate(20.702, latency, false,
                "animation-firsta-041020.json", new(-205.04f, 16.92f, -145.8f)));
            run($"Blunderville animation Delivery north passage latency {latency:F2}", () => Simulate(30, latency, false,
                "animation-delivery-041020.json", new(-199.91f, 6, 230), true));
            foreach (var phase in new[] { 2.7, 3.6 })
                run($"Blunderville animation actual slope navigation phase {phase:F1} latency {latency:F2}", () => Simulate(phase, latency));
            run($"Blunderville animation arrow bridge navigation latency {latency:F2}", () => Simulate(2.7, latency, true));
        }
    }

    private static void Simulate(double phase, double latency, bool bridge = false,
        string fixture = "animation-firsta-030838.json", Vector3? start = null, bool delivery = false)
    {
        using var replay = new Recording(fixture);
        var now = Epoch; var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, delivery ? 20016u : 20013u, 0, 0, 0, Epoch);
        if (delivery) state.SynchronizeStatuses([new(3704, 37, Epoch.AddMinutes(2))], Epoch);
        var p = start ?? (bridge ? new Vector3(-200, 10.03f, -248) : new Vector3(-208.759f, 11.276f, -126.299f));
        var target = p; var planner = new BlundervilleRacePlanner();
        var finish = delivery ? 222 : bridge ? -288 : -160;
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = phase;
        var checks = 0; var maxMs = 0.0; var reverse = 0f;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0;
        for (var tick = 0; tick < 4000; ++tick)
        {
            var t = phase + tick * .01; now = Epoch.AddSeconds(t); move.Now = now;
            if (tick % 10 == 0)
            {
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, null); target = decision.Target!.Value;
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, Player(p, snapshot), new(6, 6, 0, false, 6, 6), options, now);
                watch.Stop(); maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
                ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
            }
            runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, Player(p, snapshot), snapshot, now]);
            var next = move.Advance(p, .01f); reverse += MathF.Max(0, next.Z - p.Z);
            p = next with { Y = delivery ? 6 : bridge ? 10.03f : Math.Clamp(12.66712f + (-next.Z - 130.3f) * .267949f, 11.28f, 17.47f) };
            Check(BlundervilleRoutes.Boundary(state.Course).Contains(p), $"Movement left the floor at {p}.");
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
            {
                var effect = impacts[hitIndex++]; if (effect[0].GetDouble() < phase) continue;
                var actor = replay.Actors[effect[2].GetUInt64()]; var action = effect[3].GetUInt32();
                var shape = new RectangleGeometry(actor.Position, effect[4].GetSingle(), action == 34716 ? 2 : 10, action == 34716 ? 3 : 4);
                Check(!shape.Contains(p, .15f), $"Animated sweep hit simulated player at t={t:F2}, p={p}, helper={actor.Position}."); ++checks;
            }
            if (p.Z < finish) break;
        }
        Console.WriteLine($"ANIMATION bridge={bridge} phase={phase} latency={latency} end={p} dispatch={move.Count} stops={move.Stops} reverse={reverse:F2} maxSolve={maxMs:F1}ms impactChecks={checks}");
        Check(p.Z < finish, "Animation avoidance never crossed the recorded obstacle rows.");
        Check(move.Count < 45 && reverse < 4, "Animation handling repeatedly restarted or reversed the route.");
    }

    private sealed class Recording : IDisposable
    {
        public JsonDocument Data { get; }
        public JsonElement[] Events { get; }
        public Dictionary<ulong, BlundervilleActor> Actors { get; }
        public BlundervilleEncounter Encounter { get; } = new(_ => { });
        private int _index;
        private double _sampleTime = -1;
        public Recording(string fixture = "animation-firsta-030838.json")
        {
            Data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", fixture)));
            Events = Data.RootElement.GetProperty("events").EnumerateArray().ToArray();
            Actors = Data.RootElement.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(), 16338,
                Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
            Encounter.Synchronize(Actors.Values, Epoch.AddSeconds(-1));
        }
        public void Advance(double time)
        {
            while (_sampleTime + .1 < time)
            {
                _sampleTime += .1;
                Sample(_sampleTime);
            }
            Sample(time); _sampleTime = time;
        }
        private void Sample(double time)
        {
            while (_index < Events.Length && Events[_index][0].GetDouble() <= time)
            {
                var e = Events[_index++]; var at = Epoch.AddSeconds(e[0].GetDouble()); var kind = e[1].GetString();
                if (kind == "map") Encounter.Map(e[2].GetByte(), e[3].GetUInt32(), at);
                else
                {
                    var id = e[2].GetUInt64(); var actor = Actors[id];
                    if (kind == "turn") Actors[id] = actor with { Rotation = e[3].GetSingle(), Position = e.GetArrayLength() > 4 ? Vec(e[4]) : actor.Position };
                    else Encounter.Effect(id, e[3].GetUInt32(), (uint)_index, actor.Position, e[4].GetSingle(), at);
                }
                // Object packets update the next main-thread sample. Grouped rows
                // are scanned together below; no prediction reads future events.

            }
            Encounter.Synchronize(Actors.Values, Epoch.AddSeconds(time));
        }
        public void Dispose() => Data.Dispose();
    }

    private sealed class Moving : IGreenMoveSystem
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
            return p + d / distance * MathF.Min(distance, 6 * dt);
        }
    }
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static PlayerState Player(Vector3 p, SafeZoneSnapshot s) => new(p, 0, 6, false, false, 0, true, s.Version);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
