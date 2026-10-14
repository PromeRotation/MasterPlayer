using System.Numerics;
using System.Text.Json;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleFeedback001440Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville latest Delivery raised floor excludes the recorded lower-floor shortcut", () =>
        {
            var upper = BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, new(-200, 6, 252));
            var lowerPoint = new Vector3(-226.27f, 3.25f, 276.5f);
            Check(!upper.Contains(lowerPoint) && BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, lowerPoint).Contains(lowerPoint),
                "The bridge and its overlapping lower floor still share the same traversal mask.");
            foreach (var p in new[] { new Vector3(-200, 6, 252), new(-231, 6, 271), new(-240.2991f, 6, 275.4989f) })
                Check(upper.Contains(p), $"A required upper-floor anchor was lost: {p}.");
        });
        foreach (var color in new ushort[] { 35, 36, 37 })
            run($"Blunderville latest Delivery upper-floor outbound and return color {color}", () =>
            {
                using var runtime = new SafeZoneRuntime(new Moving(), () => false, () => false) { ReplayClock = () => Epoch };
                using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager); api.UtcNowForTest = Epoch;
                var navigation = new BlundervilleNavigation(runtime, api, new());
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
                state.SynchronizeStatuses([new(3704, color, Epoch.AddMinutes(2))], Epoch);
                var p = BlundervilleRaceState.PickupCenter;
                var nodes = BlundervilleRoutes.DeliveryNodes(p, state.DeliveryTarget!.Value);
                foreach (var node in nodes.Concat(nodes.Reverse()))
                {
                    if (Distance(p, node.Position) < .1f) continue;
                    navigation.Apply(state, new(node.Position, false, false, null, ""), new(_ => { }), Epoch, p, true, false, false);
                    var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(runtime.MovementStatus, null)!;
                    var solution = new SafeZoneSolver().Solve(snapshot, new(p, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), options, Epoch);
                    Check(solution.IsSuccess && solution.ExecutablePath.Count > 1 && Distance(solution.ExecutablePath[^1].Position, node.Position) <= node.ArrivalRadius,
                        $"Upper-floor color {color} could not reach {node.Position} from {p}: {solution.Status}/{solution.TargetPosition}.");
                    for (var i = 1; i < solution.ExecutablePath.Count; ++i)
                        Check(ArenaBoundaryTraversal.ContainsSegment(snapshot.ArenaBoundary!, solution.ExecutablePath[i - 1].Position, solution.ExecutablePath[i].Position),
                            "An upper-floor path crosses a real hole.");
                    p = node.Position;
                }
            });
        run("Blunderville latest FirstB rejoins after the recorded forced displacement", () =>
        {
            using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "Blunderville", "progress-001440.json")));
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20014, 0, 0, 0, Epoch);
            var race = new BlundervilleRacePlanner(); var jumps = new BlundervilleJumpPlanner();
            var passedSecondTakeoff = false; var joinedAfterForced = false;
            foreach (var sample in file.RootElement.GetProperty("samples").EnumerateArray())
            {
                var t = sample[0].GetDouble(); var p = Vec(sample[1]); var now = Epoch.AddSeconds(t);
                var statuses = t is >= 8.143 and < 13.341 ? new[] { new BlundervilleRaceStatus(3700, 0, Epoch.AddSeconds(13.143)) }
                    : t is >= 13.341 and < 16.248 ? [new BlundervilleRaceStatus(3737, 4, Epoch.AddSeconds(16.248))]
                    : [];
                state.SynchronizeStatuses(statuses, now);
                var grounded = p.Y <= 10.1f;
                var decision = race.Update(state, now, p, grounded, !grounded, false, null);
                if (decision.Target is not { } target) continue;
                var jump = jumps.Plan(state.Course, now, p, target, grounded, !grounded);
                if (t > 9 && t < 13.341 && p.Z < -155.9f)
                {
                    Check(jump == null || jump.Target.Z < p.Z + .5f,
                        $"A passed takeoff or old corner pulled FirstB backward at t={t:F3}: {jump?.Target}.");
                    passedSecondTakeoff = true;
                }
                if (t >= 16.248 && p.Z < -170)
                {
                    Check(target.Z < -180 && race.Cursor >= 8,
                        $"The forced displacement ended ahead of the route but its cursor stayed behind: {target}, {race.Cursor}.");
                    joinedAfterForced = true;
                }
            }
            Check(passedSecondTakeoff && joinedAfterForced, "The new ARR fixture omitted the failing FirstB segments.");
        });

        run("Blunderville latest Delivery charge and sweep forecasts cover recorded hits", () =>
        {
            using var replay = new Recording(); var charge = 0; var sweeps = 0;
            var seen = new Dictionary<(ulong, uint), int>();
            var rays = new Dictionary<ulong, (Vector3 A, Vector3 B)>();
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit"))
            {
                var action = e[3].GetUInt32(); var t = e[0].GetDouble();
                var key = (e[2].GetUInt64(), action); var count = seen.GetValueOrDefault(key) + 1; seen[key] = count;
                if (action == 34774 && e[6].ValueKind == JsonValueKind.Array) rays[key.Item1] = (Vec(e[4]), Vec(e[6]));
                if (count <= 3 || action is not (34774 or 34799)) continue;
                replay.Advance(t - .3);
                var source = e[2].GetUInt64(); var at = Epoch.AddSeconds(t);
                var position = Vec(e[4]);
                var endpoint = action == 34774 ? Vector3.Distance(position, rays[source].A) < .3f ? rays[source].B : rays[source].A : position;
                var mid = (position + endpoint) / 2;
                var hazards = replay.Encounter.Hazards(Epoch.AddSeconds(t - .3)).ToArray();
                Check(hazards.Any(h =>
                        h.Id.StartsWith(action == 34774 ? $"charge/{source}/" : $"rhythm/{source}/34799/")
                        && h.From <= at && h.Until >= at && h.Geometry.Contains(mid)),
                    $"No pre-hit animation forecast for action {action} source {source} at t={t:F3}; from={position}, to={endpoint}; windows={string.Join(';', hazards.Where(h=>h.Id.StartsWith($"charge/{source}/")).Select(h=>$"{(h.From-Epoch).TotalSeconds:F2}..{(h.Until-Epoch).TotalSeconds:F2} {h.Geometry.Position}"))}.");
                if (action == 34774) charge++; else sweeps++;
            }
            Check(charge >= 50 && sweeps >= 50, "The recorded Delivery forecast sample is incomplete.");
        });

        run("Blunderville latest Delivery waits at pickup for its first unknown charge cycle", () =>
        {
            using var replay = new Recording(); var destination = new Vector3(-240.2991f, 6, 275.4989f);
            replay.Advance(4);
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
            state.SynchronizeStatuses([new(3704, 35, Epoch.AddMinutes(2))], Epoch.AddSeconds(4));
            var pickup = BlundervilleRaceState.PickupCenter;
            var normal = new BlundervilleRacePlanner().Update(state, Epoch.AddSeconds(4), pickup, true, false, false, null);
            Check(BlundervilleDeliveryPlanner.Apply(normal, state, replay.Encounter, pickup, Epoch.AddSeconds(4)).Hold,
                "An unseen charge cycle allowed the player to leave pickup into its body lane.");
            replay.Advance(12);
            Check(replay.Encounter.DeliveryReady(destination, Epoch.AddSeconds(12))
                && !BlundervilleDeliveryPlanner.Apply(normal, state, replay.Encounter, pickup, Epoch.AddSeconds(12)).Hold,
                "A confirmed cycle kept blocking the Delivery route indefinitely.");
        });
        foreach (var latency in new[] { .08, .3 })
            run($"Blunderville latest Delivery three west crystals worker {latency:F2}", () => SimulateDelivery(latency));
    }

    private static void SimulateDelivery(double latency)
    {
        using var replay = new Recording(); var now = Epoch;
        var move = new Moving(); using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var navigation = new BlundervilleNavigation(runtime, api, new()); var planner = new BlundervilleRacePlanner();
        var outbound = new BlundervilleFinalPlanner(BlundervilleFinalPlanner.DeliveryWest);
        var returning = new BlundervilleFinalPlanner(BlundervilleFinalPlanner.ReturnWest);
        var direct = false; var nextScan = 4.0;
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
        var p = BlundervilleRaceState.PickupCenter with { Y = 6 }; var carrying = true; var score = 0;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray(); var hitIndex = 0;
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 4.0;
        var maximumSolve = 0.0; var elapsed = 0.0; var previous = p;
        var positions = new List<(double T, Vector3 P)>();
        var frameTimes = new List<double>();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var capture = runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!;
        var dispatch = runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!;
        for (var tick = 0; tick < 4500; ++tick)
        {
            var frameTimer = System.Diagnostics.Stopwatch.StartNew();
            var t = 4 + tick * .02; elapsed = t - 4; now = Epoch.AddSeconds(t); move.Now = now;
            if (t >= nextScan)
            {
                nextScan = t + .1;
                replay.Advance(t); api.UtcNowForTest = now;
                replay.Encounter.DeliverySchedule.SetStart(Epoch);
                state.SynchronizeStatuses(carrying ? [new(3704, 35, now.AddSeconds(10))] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null);
                decision = BlundervilleDeliveryPlanner.Apply(decision, state, replay.Encounter, p, now);
                var corridor = carrying ? outbound : returning;
                (carrying ? returning : outbound).Reset();
                decision = corridor.Apply(decision, state, replay.Encounter.TraversalHazards(now).ToArray(), p, now, navigation.GetDirectTraversalExecution(now));
                direct = decision.DirectTraversal;
                if (corridor.PendingSearch is { IsCompleted: false } search)
                {
                    frameTimer.Stop();
                    var timer = System.Diagnostics.Stopwatch.StartNew(); search.GetAwaiter().GetResult();
                    nextScan = t + Math.Max(.1, Math.Max(latency, timer.Elapsed.TotalSeconds));
                    frameTimer.Start();
                }
                navigation.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
            if (!runtime.Setting.EffectiveEnableAutoMovement)
            { move.Stop(); pending = accepted = null; nextSolve = t; }
            else if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(6, 6, 0, false, 6, 6), now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    frameTimer.Stop();
                    var options = (SolveOptions)capture.Invoke(runtime.MovementStatus, null)!;
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now);
                    maximumSolve = Math.Max(maximumSolve, timer.Elapsed.TotalMilliseconds);
                    ready = t + Math.Max(latency, timer.Elapsed.TotalSeconds); nextSolve = ready + .1;
                    frameTimer.Start();
                }
                dispatch.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            if (tick >= 50) frameTimes.Add(frameTimer.Elapsed.TotalMilliseconds);
            positions.Add((t, p));
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() - BlundervilleDeliverySchedule.HitLead <= t)
            {
                var hit = impacts[hitIndex++]; if (hit[0].GetDouble() < 4) continue;
                var action = hit[3].GetUInt32(); var source = Vec(hit[4]);
                var instant = hit[0].GetDouble() - BlundervilleDeliverySchedule.HitLead;
                if (instant < 4 || !carrying) continue;
                var atHit = positions.LastOrDefault(s => s.T <= instant + 1e-6).P;
                Check(!BlundervilleDeliverySchedule.Hits(action, source, atHit),
                    $"Delivery hit action {action} source {hit[2]} at t={t:F2}, p={atHit}, score={score}.");
            }
            previous = p; p = move.Advance(p, .02f);
            Check(ArenaBoundaryTraversal.ContainsSegment(BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p), previous, p),
                $"Delivery left its raised floor between {previous} and {p}.");
            if (carrying && Distance(p, new(-240.2991f, 6, 275.4989f)) <= 2f)
            {
                carrying = false; state.Director(BlundervilleRaceState.DirectorInstance, 3, (uint)++score, 0, 0, 0, now);
                Console.WriteLine($"DELIVERY score={score} t={t:F2} p={p}");
                if (score == 3) break;
            }
            // The hub pickup EventRange fires 3.15–3.90m from its centre (21 rounds).
            else if (!carrying && Distance(p, BlundervilleRaceState.PickupCenter) <= 3.15f) carrying = true;
        }
        Console.WriteLine($"DELIVERY score={score} elapsed={elapsed:F2} p={p} dispatch={move.Count} solverMax={maximumSolve:F1}ms");
        frameTimes.Sort();
        Console.WriteLine($"DELIVERY CPU frame p95={frameTimes[(int)(frameTimes.Count * .95)]:F2}ms max={frameTimes[^1]:F2}ms (sampling/navigation/dispatch; excludes worker/input/render)");
        Check(score == 3, "The simulated Delivery round did not deliver all three crystals within the recording.");
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private DateTime _ready; private Vector3? _previous;
        public DateTime Now; public int Count;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _points = points; _index = 0; _previous = null; _ready = Now.AddSeconds(.08); Count++; }
        public void Stop() { _points = []; _index = 0; _previous = null; }
        public void Append(GreenMovePoint point) => Append([point]);
        public void Append(IReadOnlyList<GreenMovePoint> points) => _points = _points.Concat(points).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => _index++;
        public Vector3 Advance(Vector3 p, float dt)
        {
            var old = _previous; _previous = p;
            while (IsRunning && (Distance(p, _points[_index].Position) <= _points[_index].Tolerance
                || old is { } previous && GeometryHelper.DistanceSquaredPointToSegmentXZ(_points[_index].Position, previous, p)
                    <= _points[_index].Tolerance * _points[_index].Tolerance)) _index++;
            if (!IsRunning || Now < _ready) return p;
            var direction = _points[_index].Position - p; direction.Y = 0;
            return p + Vector3.Normalize(direction) * (6 * dt);
        }
    }

    private sealed class Recording : IDisposable
    {
        private readonly JsonDocument _file;
        public JsonElement[] Events { get; }
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        public BlundervilleEncounter Encounter { get; } = new(_ => { });
        private int _index;
        private double _sample = -1;
        public Recording()
        {
            _file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "Blunderville", "delivery-001440.json")));
            Events = _file.RootElement.GetProperty("events").EnumerateArray().ToArray();
            _actors = _file.RootElement.GetProperty("devices").EnumerateArray().Select(d =>
                new BlundervilleActor(d.GetProperty("id").GetUInt64(), d.GetProperty("oid").GetUInt32(),
                    Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
            Encounter.Synchronize(_actors.Values, Epoch.AddSeconds(-1));
        }
        public void Advance(double time)
        {
            while (_sample + .1 < time) Sample(_sample += .1);
            Sample(time); _sample = time;
        }
        private void Sample(double time)
        {
            while (_index < Events.Length && Events[_index][0].GetDouble() <= time)
            {
                var e = Events[_index++]; var id = e[2].GetUInt64(); var kind = e[1].GetString();
                var at = Epoch.AddSeconds(e[0].GetDouble());
                if (kind == "move")
                    _actors[id] = _actors[id] with { Position = Vec(e[3]), Rotation = e[4].GetSingle() };
                else if (kind == "hit")
                    Encounter.Effect(id, e[3].GetUInt32(), (uint)_index, Vec(e[4]), e[5].GetSingle(), at,
                        e.GetArrayLength() > 6 && e[6].ValueKind == JsonValueKind.Array ? Vec(e[6]) : null);
                else if (kind == "cast")
                    Encounter.StartCast(id, new(e[3].GetUInt32(), Vec(e[4]), e[5].GetSingle(), at,
                        at.AddSeconds(e[6].GetDouble() + .3), _actors[id].Position), at);
            }
            Encounter.Synchronize(_actors.Values, Epoch.AddSeconds(time));
        }
        public void Dispose() => _file.Dispose();
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
