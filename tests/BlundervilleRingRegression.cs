using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

// 2026-10-13 route research, 21 Ring rounds: the beam (34808) rotates on the
// course-start clock and laps the ring every 32.9s, then 25.75s after 30s and
// 22.5s after 75s; MP stood ~6s at the NE corner with the beam 1.6rad behind it
// (17:44). Replays every recorded round through the script's decision chain,
// jumps included, and scores the snapshots that would have hit the player.
internal static class BlundervilleRingRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 4, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(200, 10, -125);
    private const float Body = .5f;
    private static readonly bool Debug = Environment.GetEnvironmentVariable("RING_DEBUG") == "1";
    private static readonly double DebugFrom = double.TryParse(Environment.GetEnvironmentVariable("RING_FROM"), out var from) ? from : 0;
    private static readonly double DebugTo = double.TryParse(Environment.GetEnvironmentVariable("RING_TO"), out var to) ? to : 1e9;
    private static readonly bool Forecast = Environment.GetEnvironmentVariable("RING_FORECAST") != "0";
    private static readonly double Latency = double.TryParse(Environment.GetEnvironmentVariable("RING_LATENCY"), out var latency) ? latency : .12;

    internal sealed record Outcome(string Name, double End, double Survived, string? Fall, List<string> BeamHits, List<string> CastHits,
        double LongestStand, float BeamMargin, double Laps);

    public static void Run(Action<string, Action> run)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ring-recorded.json")));
        var names = data.RootElement.GetProperty("recordings").EnumerateObject().Select(p => p.Name)
            .Where(n => Environment.GetEnvironmentVariable("RING_ONLY") is not { Length: > 0 } only || n.Contains(only)).ToArray();
        run("Blunderville ring beam forecast covers every recorded tick", () =>
        {
            int ticks = 0, misses = 0;
            foreach (var name in names)
            {
                using var replay = new Recording(name);
                replay.Encounter.RingBeam.SetStart(Epoch);
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == BlundervilleCatalog.BeamAction && e[0].GetDouble() > 2.5))
                {
                    // Forecast 2s ahead from the ticks seen so far; the strip's
                    // ring section (15–25m) must lie inside the forecast slot.
                    var t = e[0].GetDouble(); replay.Advance(t - 2); var at = Epoch.AddSeconds(t - BlundervilleRingBeam.Lead);
                    var forecast = replay.Encounter.RingBeam.Hazards(Epoch.AddSeconds(t - 2)).Where(h => h.From <= at && h.Until >= at).ToArray();
                    var rotation = e[5].GetSingle(); var axis = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
                    var side = new Vector3(axis.Z, 0, -axis.X);
                    ++ticks;
                    var corners = new[] { 15f, 25f }.SelectMany(r => new[] { -4f, 4f }.Select(x => BlundervilleRingBeam.Center + axis * r + side * x));
                    if (!corners.All(c => forecast.Any(h => h.Geometry.Contains(c, .05f))) && misses++ < 5)
                        Console.WriteLine($"BEAM miss {name} t={t:F2} rotation={rotation:F3}");
                }
            }
            Console.WriteLine($"BEAM forecast covers {ticks - misses}/{ticks} ticks");
            Check(ticks > 1500 && misses == 0, "A recorded beam tick fell outside its forecast slot.");
        });
        run("Blunderville ring rounds survive the recorded hazards", () =>
        {
            var outcomes = names.Select(Simulate).ToArray();
            foreach (var o in outcomes)
                Console.WriteLine($"RING {o.Name}: {(o.Fall ?? "survived")} {o.Survived:F1}/{o.End:F1}s, beam hits {o.BeamHits.Count}"
                    + $"{(o.BeamHits.Count > 0 ? " [" + string.Join("; ", o.BeamHits.Take(3)) + "]" : "")}, cast hits {o.CastHits.Count}"
                    + $"{(o.CastHits.Count > 0 ? " [" + string.Join("; ", o.CastHits.Take(3)) + "]" : "")}, longest stand {o.LongestStand:F1}s,"
                    + $" beam margin {o.BeamMargin:F1}m, laps {o.Laps:F2}");
            var clean = outcomes.Count(o => o.Fall == null && o.BeamHits.Count == 0 && o.CastHits.Count == 0);
            Console.WriteLine($"RING clean rounds {clean}/{outcomes.Length}, falls {outcomes.Count(o => o.Fall != null)},"
                + $" beam hits {outcomes.Sum(o => o.BeamHits.Count)}, cast hits {outcomes.Sum(o => o.CastHits.Count)}");
            // 2026-10-13 (fixed 0.12s solve latency): 1.14.8 16/21 clean, no falls,
            // 11 beam and 12 cast snapshots, all in the four rounds past 69s
            // (1.14.6: 9/21, 20 and 21; 1.14.7: 11/21, 9 and 15). Raise as it improves.
            Check(outcomes.All(o => o.Fall == null), "A Ring replay fell off the course.");
            Check(outcomes.Count(o => o.Fall == null && o.BeamHits.Count == 0 && o.CastHits.Count == 0) >= 15
                && outcomes.Sum(o => o.BeamHits.Count) <= 14 && outcomes.Sum(o => o.CastHits.Count) <= 15,
                "The Ring replays fell below their recorded baseline.");
        });
    }

    private static Outcome Simulate(string recording)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var replay = new Recording(recording); var now = Epoch;
        var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20015, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var jumper = new BlundervilleJumpPlanner(); var gate = new BlundervilleJumpExecution();
        var floor = BlundervilleRoutes.Boundary(BlundervilleCourse.Ring);
        var p = new Vector3(215.4f, 10, -136.7f); var previous = p;
        var history = new List<(double T, Vector3 P, bool Air)>();
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        double? launch = null; var launchSpeed = 6f; var lastSpeed = 0f;
        var beamHits = new List<string>(); var castHits = new List<string>(); string? fall = null;
        var stand = 0.0; var longest = 0.0; var standing = new Dictionary<string, double>(); var margin = float.MaxValue; var angle = 0.0; var lastAngle = Angle(p);
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0;
        const double frame = 1.0 / 30;
        var t = 0.0;
        for (var tick = 0; (t = tick * frame) < replay.End; ++tick)
        {
            now = Epoch.AddSeconds(t); move.Now = now;
            var air = launch is { } at && t - at < .75;
            if (state.CanJump(now) && gate.Sample(now, p, !air, air, runtime.MovementStatus.HasControl, move.GetStatus()))
            {
                if (Debug) Console.WriteLine($"DBG launch t={t:F2} p=({p.X:F2},{p.Z:F2}) speed={gate.MeasuredSpeed:F2} last={lastSpeed:F2}");
                if (nav.CommitJump(gate.Landing!.Value, p, gate.MeasuredSpeed, now))
                {
                    gate.ConfirmLaunch(now); launch = t; air = true; launchSpeed = lastSpeed >= 4.8f ? 6 : 2.5f;
                }
                else { nav.ReleaseJump(); gate.RejectLaunch(); }
            }
            if (!gate.InFlight) nav.ReleaseJump();
            // StopOwnedMovement resets the movement system: the worker's
            // stale route to the landing must not be dispatched again.
            if (gate.NeedsRunupRetry) { nav.StopForRunup(); nextScan = t; accepted = pending = null; nextSolve = t; }
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                // Mirrors BlundervilleScript: the Director start drives the beam forecast.
                if (Forecast) replay.Encounter.RingBeam.SetStart(state.StartedByDirector ? state.StartedAt : null);
                state.SynchronizeStatuses([], now);
                // Mirrors BlundervilleScript.Render for the Ring.
                var decision = planner.Update(state, now, p, !air, air, false, null, false);
                IGeometryShape? crossing = null;
                if (state.CanJump(now) && !decision.Hold && !planner.Recovering && decision.Target is { } forward)
                {
                    if (jumper.Plan(state.Course, now, p, forward, !air, air) is { } step)
                    {
                        decision = decision with { Target = step.Target, Hold = step.Hold, Jump = step.Jump, Hint = step.Hint };
                        crossing = step.Corridor;
                    }
                }
                else jumper.Reset();
                if (planner.Recovering) crossing = planner.RecoveryCorridor;
                if (state.CanJump(now) && (!decision.Hold || gate.InFlight))
                {
                    if (gate.Prepare(crossing as BlundervilleJumpCorridor, p) is { } jump)
                    {
                        decision = decision with { Target = jump.Target, Hold = false, Jump = false, Hint = jump.Hint };
                        crossing = jump.Corridor;
                    }
                }
                else { gate.Clear(); nav.ReleaseJump(); }
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false, crossing);
                if (Debug && t >= DebugFrom && t <= DebugTo)
                    Console.WriteLine($"DBG t={t:F2} p=({p.X:F2},{p.Z:F2}) air={air} hint={decision.Hint} target={decision.Target} hold={decision.Hold} flight={gate.InFlight} speed={gate.MeasuredSpeed:F2} moving={move.GetStatus().IsRunning} point={move.GetStatus().CurrentPoint?.Position} final={move.GetStatus().FinalPoint?.Position} solve={accepted?.Status} path={accepted?.ExecutablePath.Count} beam={BlundervilleRingBeam.Model(t):F2}");
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            var capability = new MovementCapability(6, 6, 0, false, 6, 6);
            if (pending != null && t >= ready) { accepted = pending; pending = null; }
            if (pending == null && t >= nextSolve)
            {
                var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                pending = new SafeZoneSolver().Solve(snapshot, player, capability, options, now);
                // Live logs: ~24 async solves per 5s on the Ring (one per ~0.21s).
                // A fixed latency keeps the replay deterministic across machines.
                ready = t + Latency; nextSolve = ready + .1;
            }
            if (!runtime.Setting.RuntimeMovementHeld)
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [air ? null : accepted, player, snapshot, now]);
            previous = p; p = move.Advance(p, (float)frame, air ? launchSpeed : 6);
            lastSpeed = Vector3.Distance(previous, p) / (float)frame;
            var landed = launch is { } began && !(t + frame - began < .75);
            if (landed) launch = null;
            history.Add((t + frame, p, air && !landed));
            if (!(air && !landed) && !floor.Contains(p, .05f)) { fall = $"fell at t={t + frame:F2} p=({p.X:F2},{p.Z:F2})"; break; }
            stand = lastSpeed < .1f ? stand + frame : 0; longest = Math.Max(longest, stand);
            if (lastSpeed < .1f && !(air && !landed))
            {
                var key = $"{Math.Round(p.X)},{Math.Round(p.Z)}";
                standing[key] = standing.GetValueOrDefault(key) + frame;
            }
            var a = Angle(p); var delta = a - lastAngle;
            if (delta > MathF.PI) delta -= 2 * MathF.PI; else if (delta < -MathF.PI) delta += 2 * MathF.PI;
            angle += delta; lastAngle = a;
            // Snapshots: casts resolve ~0.3s before their effect packet, beam ticks
            // ~0.1s before (the rotation update that precedes each tick).
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t + frame)
            {
                var e = impacts[hitIndex++]; var effect = e[0].GetDouble(); var action = e[3].GetUInt32();
                var shape = replay.Shape(e);
                if (shape == null) continue;
                foreach (var lead in action == BlundervilleCatalog.BeamAction ? new[] { .1, 0 } : [.3, 0])
                {
                    var instant = effect - lead;
                    if (instant < 0) continue;
                    var sample = history.LastOrDefault(h => h.T <= instant + 1e-6);
                    if (sample.T == 0 || sample.Air) continue;
                    if (action == BlundervilleCatalog.BeamAction)
                    {
                        var local = Distance(shape, sample.P);
                        margin = MathF.Min(margin, local);
                        if (shape.Contains(sample.P, Body)) { beamHits.Add($"t={effect:F2} ({sample.P.X:F1},{sample.P.Z:F1})"); break; }
                    }
                    else if (Hits(shape, sample.P)) { castHits.Add($"{action} t={effect:F2} ({sample.P.X:F1},{sample.P.Z:F1})"); break; }
                }
            }
        }
        if (Debug) Console.WriteLine($"STAND {recording}: total {standing.Values.Sum():F1}s; " + string.Join(", ",
            standing.OrderByDescending(s => s.Value).Take(8).Select(s => $"({s.Key}) {s.Value:F1}")));
        return new(recording, replay.End, Math.Min(t, replay.End), fall, beamHits, castHits, longest, margin, Math.Abs(angle) / (2 * Math.PI));
    }

    private static double Angle(Vector3 p) => Math.Atan2(p.X - Center.X, p.Z - Center.Z);

    // FanGeometry.Contains adds its epsilon to the half angle in radians; grow
    // the 180-degree fans by the body radius in metres instead.
    private static bool Hits(IGeometryShape shape, Vector3 p)
    {
        if (shape is not FanGeometry fan) return shape.Contains(p, Body);
        var offset = new Vector2(p.X - fan.Position.X, p.Z - fan.Position.Z);
        return offset.Length() <= fan.Radius + Body
            && Vector2.Dot(offset, new(MathF.Sin(fan.Rotation), MathF.Cos(fan.Rotation))) >= -Body;
    }

    // Lateral distance outside the beam's 8m strip (negative inside).
    private static float Distance(IGeometryShape beam, Vector3 p)
    {
        var heading = new Vector2(MathF.Sin(beam.Rotation), MathF.Cos(beam.Rotation));
        var offset = new Vector2(p.X - beam.Position.X, p.Z - beam.Position.Z);
        if (Vector2.Dot(offset, heading) < 0) return offset.Length() - 4;
        return MathF.Abs(offset.X * heading.Y - offset.Y * heading.X) - 4;
    }

    private sealed class Recording : IDisposable
    {
        private readonly JsonDocument _data;
        public readonly JsonElement[] Events;
        public readonly double End;
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        private readonly Dictionary<ulong, (uint Action, float Rotation, Vector3 Location)> _lastCast = [];
        public readonly BlundervilleEncounter Encounter = new(_ => { });
        private int _index; private double _sample = -1;
        public Recording(string name)
        {
            _data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ring-recorded.json")));
            var recording = _data.RootElement.GetProperty("recordings").GetProperty(name);
            Events = recording.GetProperty("events").EnumerateArray().ToArray();
            End = recording.GetProperty("end").GetDouble();
            _actors = recording.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(),
                d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
        }
        public void Advance(double time)
        {
            while (_sample + .1 < time) { _sample += .1; Sample(_sample); }
            Sample(time); _sample = time;
        }
        // The damage shape of an effect, with the cast's facing for the fans.
        public IGeometryShape? Shape(JsonElement e)
        {
            var action = e[3].GetUInt32(); var pos = Vec(e[4]);
            if (action == BlundervilleCatalog.BeamAction) return new RayRectangleGeometry(pos, e[5].GetSingle(), 8, 100);
            if (action == 34805) return new CircleGeometry(pos, 6);
            if (!_lastCast.TryGetValue(e[2].GetUInt64(), out var cast) || cast.Action != action) return null;
            if (action == 35409)
            {
                var delta = (cast.Location - pos) with { Y = 0 };
                return delta.Length() > .5f ? new RayRectangleGeometry(pos, MathF.Atan2(delta.X, delta.Z), 3, delta.Length()) : null;
            }
            return new FanGeometry(pos, cast.Rotation, 8, MathF.PI);
        }
        private void Sample(double time)
        {
            while (_index < Events.Length && Events[_index][0].GetDouble() <= time)
            {
                var e = Events[_index++]; var id = e[2].GetUInt64(); var at = Epoch.AddSeconds(e[0].GetDouble());
                switch (e[1].GetString())
                {
                    case "spawn": _actors[id] = new(id, e[3].GetUInt32(), Vec(e[4]), Rotation: e[5].GetSingle()); break;
                    case "move": if (_actors.TryGetValue(id, out var moved)) _actors[id] = moved with { Position = Vec(e[3]), Rotation = e[4].GetSingle() }; break;
                    case "remove": _actors.Remove(id); Encounter.Remove(id); break;
                    case "cast":
                        var cast = new BlundervilleCast(e[3].GetUInt32(), Vec(e[7]), e[6].GetSingle(), at, at.AddSeconds(e[4].GetDouble() + .3), Vec(e[5]));
                        _lastCast[id] = (cast.Action, e[6].GetSingle(), Vec(e[7]));
                        if (_actors.TryGetValue(id, out var caster)) _actors[id] = caster with { Cast = cast };
                        Encounter.StartCast(id, cast, at);
                        break;
                    case "hit":
                        Encounter.Effect(id, e[3].GetUInt32(), e[7].GetUInt32(), Vec(e[4]), e[5].GetSingle(), at,
                            e[6].ValueKind == JsonValueKind.Array ? Vec(e[6]) : null);
                        if (_actors.TryGetValue(id, out var source)) _actors[id] = source with { Cast = null };
                        break;
                }
            }
            Encounter.Synchronize(_actors.Values, Epoch.AddSeconds(time));
        }
        public void Dispose() => _data.Dispose();
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready;
        public DateTime Now;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, IsRunning ? Math.Max(0, _points.Count - _index - 1) : 0);
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt, float speed)
        {
            while (IsRunning && Flat(_points[_index].Position - p).Length() <= Math.Max(_points[_index].Tolerance, .01f)) ++_index;
            if (!IsRunning || Now < _ready) return p;
            var d = Flat(_points[_index].Position - p); var distance = d.Length();
            return p + d / distance * MathF.Min(distance, speed * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
