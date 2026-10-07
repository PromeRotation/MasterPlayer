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
            foreach (var phase in new[] { 2.7, 3.6 })
                run($"Blunderville animation actual slope navigation phase {phase:F1} latency {latency:F2}", () => Simulate(phase, latency));
            run($"Blunderville animation arrow bridge navigation latency {latency:F2}", () => Simulate(2.7, latency, true));
        }
    }

    private static void Simulate(double phase, double latency, bool bridge = false)
    {
        using var replay = new Recording();
        var now = Epoch; var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        var p = bridge ? new Vector3(-200, 10.03f, -248) : new Vector3(-208.759f, 11.276f, -126.299f);
        var target = p; var planner = new BlundervilleRacePlanner();
        var finish = bridge ? -288 : -160;
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = phase;
        var checks = 0; var maxMs = 0.0; var reverse = 0f;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0;
        for (var tick = 0; tick < 2400; ++tick)
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
            p = next with { Y = bridge ? 10.03f : Math.Clamp(11.28f + (-next.Z - 126) * .27f, 11.28f, 17.47f) };
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(p), $"Movement left the slope at {p}.");
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
        public JsonDocument Data { get; } = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "animation-firsta-030838.json")));
        public JsonElement[] Events { get; }
        public Dictionary<ulong, BlundervilleActor> Actors { get; }
        public BlundervilleEncounter Encounter { get; } = new(_ => { });
        private int _index;
        private double _sampleTime = -1;
        public Recording()
        {
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
                    if (kind == "turn") Actors[id] = actor with { Rotation = e[3].GetSingle() };
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
