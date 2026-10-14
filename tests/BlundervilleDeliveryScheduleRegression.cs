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

// 2026-10-13 route research: MP never delivered a crystal in 5 automated
// Delivery rounds. The hazards run on the course-start clock, so the carry is
// forecast from t0 (BlundervilleDeliverySchedule) instead of waiting at the
// hub to learn cycles. Replays all recorded rounds with at least 75s of
// hazards; a crystal is lost when the calibrated footprint holds the player
// 0.40s before an effect packet.
internal static class BlundervilleDeliveryScheduleRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 13, 3, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Orders = ["WEN", "NEW", "ENW", "WNE", "NWE", "EWN"];

    public static void Run(Action<string, Action> run)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-recorded.json")));
        var names = data.RootElement.GetProperty("recordings").EnumerateObject().Select(p => p.Name).ToArray();
        run("Blunderville delivery schedule forecasts every recorded carrying hit", () =>
        {
            int covered = 0, total = 0;
            foreach (var name in names)
            {
                using var replay = new Recording(name);
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 34716 or 34774 or 34799 && e[0].GetDouble() > 2))
                {
                    var t = e[0].GetDouble(); replay.Advance(t - 1.5);
                    var instant = Epoch.AddSeconds(t - BlundervilleDeliverySchedule.HitLead);
                    ++total;
                    if (replay.Encounter.DeliverySchedule.Hazards(Epoch.AddSeconds(t - 1.5))
                        .Any(h => h.From <= instant && h.Until >= instant && h.Geometry.Contains(Vec(e[4]) with { Y = 6 }, 1)))
                        ++covered;
                }
            }
            Console.WriteLine($"DELIVERY schedule coverage {covered}/{total}");
            Check(total > 5000 && covered >= total * .995, "A recorded Delivery hit fell outside its scheduled window.");
        });
        // Baseline of the timed carry corridors: 2026-10-13 9 of 14 replays
        // delivered all three, at most 2 lost one; 2026-10-14 (wait slip and
        // retreat) 15 of 15 with none lost. Raise as it improves.
        run("Blunderville delivery schedule rounds deliver with the carry corridors", () =>
        {
            int complete = 0, lost = 0;
            for (var i = 0; i < names.Length; ++i)
            {
                var name = names[i]; var order = Environment.GetEnvironmentVariable("DELIVERY_ORDER") ?? Orders[i % Orders.Length];
                if (Environment.GetEnvironmentVariable("DELIVERY_ONLY") is { Length: > 0 } only && !name.Contains(only)) continue;
                try { Simulate(name, order); ++complete; }
                catch (InvalidOperationException e)
                {
                    if (e.Message.StartsWith("Crystal lost", StringComparison.Ordinal)) ++lost;
                    else if (e.Message.StartsWith("The Delivery round was slower", StringComparison.Ordinal)) ++complete;
                    Console.WriteLine($"DELIVERY {name} {order}: {e.Message}");
                }
            }
            Console.WriteLine($"DELIVERY rounds complete {complete}/{names.Length}, crystals lost {lost}");
            Check(complete >= 15 && lost == 0, "The Delivery carry corridors fell below their recorded baseline.");
        });
    }

    private static void Simulate(string recording, string order)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var replay = new Recording(recording); var now = Epoch;
        var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20016, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner(); var direct = false;
        BlundervilleFinalPlanner[] corridors = [new(BlundervilleFinalPlanner.DeliveryWest), new(BlundervilleFinalPlanner.DeliveryEast), new(BlundervilleFinalPlanner.DeliveryNorth)];
        // Humans pick up the first crystal 3.5–3.9s after the start.
        var p = new Vector3(-200.3f, 6, 255.5f); var previous = p;
        var history = new List<(double T, Vector3 P)>();
        var carrying = true; var score = 0; var colour = 0; var sprint = false; var stuns = 0;
        var picked = 3.8; var carries = new List<string>();
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 34716 or 34774 or 34799).ToArray(); var hitIndex = 0;
        // Charger body stuns after the recorded packets (24-round stun map).
        var charges = replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 34774 && e[6].ValueKind == JsonValueKind.Array)
            .Select(e => (T: e[0].GetDouble(), From: Vec(e[4]), To: Vec(e[6]))).ToArray();
        const double frame = 1.0 / 30, begin = 3.8;
        for (var tick = 0; begin + tick * frame < 100; ++tick)
        {
            var t = begin + tick * frame; now = Epoch.AddSeconds(t); move.Now = now;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                // Mirrors BlundervilleScript: the Director start drives the schedule.
                replay.Encounter.DeliverySchedule.SetStart(state.StartedByDirector ? state.StartedAt : null);
                state.SynchronizeStatuses(carrying ? [new(3704, Colour(order[colour]), now.AddSeconds(30))]
                    : sprint ? [new(1548, 0, now.AddSeconds(30))] : [], now);
                var decision = planner.Update(state, now, p, true, false, false, null, false);
                decision = BlundervilleDeliveryPlanner.Apply(decision, state, replay.Encounter, p, now);
                // Mirrors BlundervilleScript: the carry corridor of the crystal's colour.
                var corridor = state.CarryCrystalParam switch { 35 => corridors[0], 36 => corridors[1], 37 => corridors[2], _ => null };
                if (corridor != null)
                    decision = corridor.Apply(decision, state, replay.Encounter.TraversalHazards(now).ToArray(), p, now, nav.GetDirectTraversalExecution(now));
                foreach (var other in corridors) if (other != corridor) other.Reset();
                direct = decision.DirectTraversal;
                if (Environment.GetEnvironmentVariable("DELIVERY_DEBUG") == "1")
                    Console.WriteLine($"DLVDBG t={t:F2} p=({p.X:F2},{p.Z:F2}) {decision.Hint} path={string.Join(" ", decision.ContinuousPath?.Select(q => $"({q.X:F1},{q.Z:F1})") ?? [])}"
                        + (corridor?.Plan is { } plan ? $" plan={string.Join(" ", plan.Select(w => $"({w.Position.X:F1},{w.Position.Z:F1})@{(w.Arrival - now).TotalSeconds:F1}-{(w.Departure - now).TotalSeconds:F1}"))}" : ""));
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var speed = state.NavigationBaseSpeed(now); move.Speed = speed;
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, speed, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            var capability = new MovementCapability(speed, speed, 0, false, speed, speed);
            if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, capability, now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    pending = new SafeZoneSolver().Solve(snapshot, player, capability, options, now);
                    ready = t + Math.Max(.3, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
                }
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            previous = p; p = move.Advance(p, (float)frame);
            history.Add((t + frame, p));
            Check(ArenaBoundaryTraversal.ContainsSegment(BlundervilleRoutes.NavigationBoundary(BlundervilleCourse.Delivery, p), previous, p),
                $"Delivery left its raised floor between {previous} and {p} ({recording}).");
            // The effect packet trails the damage by ~0.40s.
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() - BlundervilleDeliverySchedule.HitLead <= t)
            {
                var e = impacts[hitIndex++]; var instant = e[0].GetDouble() - BlundervilleDeliverySchedule.HitLead;
                if (instant < begin || !carrying) continue;
                var at = history.LastOrDefault(h => h.T <= instant + 1e-6).P;
                Check(!BlundervilleDeliverySchedule.Hits(e[3].GetUInt32(), Vec(e[4]), at),
                    $"Crystal lost to {e[3].GetUInt32()} from {Vec(e[4])} at t={instant:F2} p={at} carrying {order[colour]} ({recording} {order}).");
            }
            foreach (var (packet, from, to) in charges)
            {
                if (t < packet || t > packet + 2 || t < begin + 1) continue;
                var length = Vector2.Distance(new(from.X, from.Z), new(to.X, to.Z));
                foreach (var (near, far, start, end, _) in BlundervilleDeliverySchedule.TruthZones(length))
                    if (t >= packet + start && t <= packet + end && BlundervilleDeliverySchedule.Zone(from, to, near, far, 4f).Contains(p with { Y = from.Y }))
                        ++stuns;
            }
            if (carrying && Delivered(order[colour], p))
            {
                carries.Add($"{order[colour]}{t - picked:F1}");
                carrying = false; sprint = true;
                state.Director(BlundervilleRaceState.DirectorInstance, 3, (uint)++score, 0, 0, 0, now);
                if (score == 3)
                {
                    Console.WriteLine($"DELIVERY {recording} {order}: three crystals at {t:F2}s, dispatch {move.Count}, body frames {stuns}, carries {string.Join(" ", carries)}");
                    Check(t < 75, "The Delivery round was slower than the human median (75s).");
                    return;
                }
                ++colour;
            }
            else if (!carrying && Vector2.Distance(new(p.X, p.Z), new(-200, 252)) <= 3.15f) { carrying = true; sprint = false; picked = t; }
        }
        throw new InvalidOperationException($"Delivered {score}/3 by 100s; stopped at {p} ({recording} {order}).");
    }

    private static ushort Colour(char c) => c switch { 'W' => 35, 'E' => 36, _ => 37 };

    // Drop EventRanges: W/E fire ~1.5–2.2m short of their centre, N below z 207.3.
    private static bool Delivered(char c, Vector3 p) => c switch
    {
        'W' => Vector2.Distance(new(p.X, p.Z), new(-240.30f, 275.50f)) <= 2,
        'E' => Vector2.Distance(new(p.X, p.Z), new(-159.43f, 275.25f)) <= 2,
        _ => p.Z <= 207.3f && p.X is > -203.4f and < -194.8f,
    };

    private sealed class Recording : IDisposable
    {
        private readonly JsonDocument _data;
        public readonly JsonElement[] Events;
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        public readonly BlundervilleEncounter Encounter = new(_ => { });
        private int _index; private double _sample = -1;
        public Recording(string name)
        {
            _data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "delivery-recorded.json")));
            var recording = _data.RootElement.GetProperty("recordings").GetProperty(name);
            Events = recording.GetProperty("events").EnumerateArray().ToArray();
            _actors = recording.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(),
                d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
            Encounter.DeliverySchedule.SetStart(Epoch);
        }
        public void Advance(double time)
        {
            while (_sample + .1 < time) { _sample += .1; Sample(_sample); }
            Sample(time); _sample = time;
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
                    case "cast":
                        var cast = new BlundervilleCast(e[3].GetUInt32(), Vec(e[7]), e[6].GetSingle(), at, at.AddSeconds(e[4].GetDouble() + .3), Vec(e[5]));
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
        public DateTime Now; public int Count; public float Speed = 6;
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, IsRunning ? Math.Max(0, _points.Count - _index - 1) : 0);
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; ++Count; if (!running) _ready = Now.AddSeconds(.08); }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() => _points = _points.Take(_index + 1).ToArray();
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            while (IsRunning && Flat(_points[_index].Position - p).Length() <= _points[_index].Tolerance) ++_index;
            if (!IsRunning || Now < _ready) return p;
            var d = Flat(_points[_index].Position - p); var distance = d.Length();
            return p + d / distance * MathF.Min(distance, Speed * dt);
        }
        private static Vector3 Flat(Vector3 v) => v with { Y = 0 };
    }

    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
