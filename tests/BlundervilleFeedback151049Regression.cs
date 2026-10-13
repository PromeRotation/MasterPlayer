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

// ARR 2026-10-12 15:10:49 (MP 1.13.0): the Final stopped for 9.5s beside the
// z=267.5 circles, later walked into an unpredicted 34801 sweep and a wave.
// Five manual qualifying runs supply the same devices with other phases.
internal static class BlundervilleFeedback151049Regression
{
    private static readonly DateTime Epoch = new(2026, 10, 12, 15, 0, 0, DateTimeKind.Utc);
    private static readonly string[] Recordings =
        ["2026.10.12 15.10.49", "2026.10.12 17.44.29", "2026.10.07 22.18.33", "2026.10.08 23.23.58", "2026.10.07 20.42.37", "2026.10.07 19.51.58", "2026.10.08 23.46.23"];
    private static readonly uint[] Damaging = [34801, 34802, 34804, 34812, 34796, 29795];

    public static void Run(Action<string, Action> run)
    {
        // ARR 2026-10-12 16:55:40 (1.14.0/1.14.1): the Final planner cleared
        // DirectTraversal on every course, so FirstA's cylinder/ice and the
        // arrow-bridge traversals never ran and the player stood at the bridges.
        // ARR 2026-10-12 17:44:29: the plan ended in the V floor wedge between two
        // wave-zone pits and stood there through four wave hits.
        run("Blunderville 174429 Final lanes avoid floor dead ends", () =>
        {
            Check(!BlundervilleFinalPlanner.Viable(-1, 206) && !BlundervilleFinalPlanner.Viable(0, 206), "The wave-zone pit wedge was a viable lane cell.");
            Check(BlundervilleFinalPlanner.Viable(-4, 156) && BlundervilleFinalPlanner.Viable(4, 156) && BlundervilleFinalPlanner.Viable(6, 270),
                "Viability rejected the stair run-up or the walkway lane.");
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, Epoch);
            var player = new Vector3(-1.24f, 19.73f, 206.23f);
            var normal = new BlundervilleRaceDecision(new(-13, 25.6f, 190), false, false, null, "route");
            var result = new BlundervilleFinalPlanner().Apply(normal, state, [], player, Epoch.AddSeconds(30));
            Check(!result.Hold && result.Target != player, "A player in the dead end was held in place instead of walking out.");
        });
        run("Blunderville 165540 Final planner passes other courses' direct traversals through", () =>
        {
            foreach (var (stage, player) in new[] { (20013u, new Vector3(-200, 10, -250)), (20014u, new Vector3(0, 10, -300)), (20017u, new Vector3(4, 31.5f, 150)) })
            {
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, stage, 0, 0, 0, Epoch);
                var decision = new BlundervilleRaceDecision(player + Vector3.UnitZ * -5, false, false, null, "direct", true) { ContinuousPath = [player + Vector3.UnitZ * -5] };
                var result = new BlundervilleFinalPlanner().Apply(decision, state, [], player, Epoch.AddSeconds(1));
                Check(ReferenceEquals(result, decision), $"The Final planner rewrote a stage {stage} decision outside its slope.");
            }
        });
        foreach (var recording in Recordings)
            run($"Blunderville 151049 Final sweeps and circles are forecast a second ahead {recording}", () =>
            {
                using var replay = new Recording(recording);
                int covered = 0, checkedHits = 0;
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() is 34801 or 34802 or 34796 or 29795 && e[0].GetDouble() > 13))
                {
                    var t = e[0].GetDouble();
                    var position = Vec(e[4]);
                    // A spot's first visit has no earlier beat to recur from
                    // (17:44: an 11.07s-cycle sweep spot first fired at 16.75s).
                    if (!replay.Events.Any(p => p[1].GetString() == "hit" && p[3].GetUInt32() == e[3].GetUInt32() && p[0].GetDouble() < t - .5
                        && Vector3.Distance(Vec(p[4]), position) < .5f)) continue;
                    replay.Advance(t - 1);
                    var at = Epoch.AddSeconds(t);
                    ++checkedHits;
                    // As consumed: the Final planner widens every window by its own lead
                    // (17:44: one beat came 0.26s before its projection from a late packet).
                    if (replay.Encounter.Hazards(Epoch.AddSeconds(t - 1)).Any(h => h.From.AddSeconds(-BlundervilleFinalPlanner.Lead) <= at && h.Until >= at && h.Geometry.Contains(position)))
                        ++covered;
                    else if (checkedHits - covered <= 3)
                        Console.WriteLine($"FINAL miss {e[3].GetUInt32()} at {t:F2} {position} nearest={string.Join(";", replay.Encounter.Hazards(Epoch.AddSeconds(t - 1))
                            .Where(h => h.Geometry.Contains(position)).Select(h => $"{(h.From - at).TotalSeconds:F2}..{(h.Until - at).TotalSeconds:F2}").Take(4))}");
                }
                Console.WriteLine($"FINAL forecast {recording}: {covered}/{checkedHits}");
                Check(checkedHits > 100 && covered == checkedHits, "A Final sweep or circle impact was not forecast one second ahead.");
            });
        // FirstB: the final-ramp push at 85.26s cost ~18s; every pusher step
        // must be forecast one second ahead from the earlier steps alone.
        using (var ramp = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "firstb-ramp-pushers.json"))))
            foreach (var recording in ramp.RootElement.GetProperty("recordings").EnumerateObject().Select(p => (p.Name, Rows: p.Value.EnumerateArray().ToArray())).ToArray())
                run($"Blunderville 151049 FirstB ramp pushers are forecast a second ahead {recording.Name}", () =>
                {
                    var engine = new BlundervilleEncounter(_ => { });
                    int index = 0, covered = 0, checkedSteps = 0, playerHits = 0;
                    foreach (var row in recording.Rows)
                    {
                        var t = row[0].GetDouble(); var at = Epoch.AddSeconds(t);
                        while (index < recording.Rows.Length && recording.Rows[index][0].GetDouble() <= t - 1)
                        {
                            var e = recording.Rows[index++];
                            engine.Effect((ulong)e[1].GetInt32() + 1, 34774, 0, Vec(e[2]), 0, Epoch.AddSeconds(e[0].GetDouble()), Vec(e[3]));
                        }
                        // A pusher needs one observed step before it can be forecast.
                        if (index < 3 || t < 5) continue;
                        var from = Vec(row[2]); var to = Vec(row[3]);
                        // Without a destination, the next packet's start ends this step.
                        if (!BlundervilleRampPushers.OnRamp(from, to) && recording.Rows.FirstOrDefault(r => r[1].GetInt32() == row[1].GetInt32()
                            && r[0].GetDouble() > t + .5) is { ValueKind: JsonValueKind.Array } next) to = Vec(next[2]);
                        if (!BlundervilleRampPushers.OnRamp(from, to)) continue;
                        ++checkedSteps;
                        var hazards = engine.Hazards(Epoch.AddSeconds(t - 1)).Where(h => h.Id.StartsWith("ramp/") && h.From <= at && h.Until >= at).ToArray();
                        if (new[] { 0f, .5f, 1f }.All(s => hazards.Any(h => h.Geometry.Contains(Vector3.Lerp(from, to, s))))) ++covered;
                        else if (checkedSteps - covered <= 3) Console.WriteLine($"RAMP miss {recording.Name} t={t:F2} {from}->{to}");
                        if (row[4].GetInt32() == 1) ++playerHits;
                    }
                    Console.WriteLine($"RAMP forecast {recording.Name}: {covered}/{checkedSteps}, player pushes {playerHits}");
                    Check(checkedSteps > 200 && covered >= checkedSteps - 2, "A FirstB ramp pusher step was not forecast one second ahead.");
                });
        // The FirstB ramp climb itself: BlundervilleSlimeRampRegression (slime physics).
        foreach (var recording in Recordings)
            foreach (var side in new[] { -1f, 1f })
                run($"Blunderville 151049 Final run to the stairs without contact {recording} side {side}", () => Simulate(recording, side, .3));
    }

    private static void Simulate(string recording, float side, double latency)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        using var replay = new Recording(recording); var now = Epoch;
        var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20017, 0, 0, 0, Epoch);
        var planner = new BlundervilleRacePlanner();
        var p = new Vector3(side * 1.5f, 4.24f, 323);
        var previous = p;
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = 0.0; var nextScan = 0.0;
        var impacts = replay.Events.Where(e => e[1].GetString() == "hit" && Damaging.Contains(e[3].GetUInt32())).ToArray(); var hitIndex = 0;
        const double frame = 1.0 / 30;
        var waits = 0.0; var direct = false;
        var finalPlanner = new BlundervilleFinalPlanner();
        for (var tick = 0; tick * frame < 65; ++tick)
        {
            var t = tick * frame; now = Epoch.AddSeconds(t); move.Now = now;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1; replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, null, false);
                var hazards = replay.Encounter.TraversalHazards(now).ToArray();
                // Mirrors BlundervilleScript: Final planner, then refuge only after the timed solver failed.
                decision = finalPlanner.Apply(decision, state, hazards, p, now, nav.GetDirectTraversalExecution(now));
                if (!decision.DirectTraversal)
                    decision = BlundervilleRefuge.Apply(decision, state, hazards, p, now, runtime.MovementStatus.LatestSolution is not { IsSuccess: true });
                direct = decision.DirectTraversal;
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, 6, Vector3.DistanceSquared(previous, p) > 1e-6f, false, 0, true, snapshot.Version);
            if (direct) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(6, 6, 0, false, 6, 6), now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", flags)!.Invoke(runtime.MovementStatus, null)!;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now);
                    ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
                }
                runtime.MovementStatus.GetType().GetMethod("Dispatch", flags)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            var next = move.Advance(p, (float)frame);
            next.Y = BlundervilleRoutes.HeightHint(BlundervilleCourse.Final, next.Z);
            if (Vector3.DistanceSquared(next, p) < 1e-8f) waits += frame;
            previous = p; p = next;
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
            {
                var e = impacts[hitIndex++];
                if (!BlundervilleCatalog.TryGet(e[3].GetUInt32(), out var rule) || rule.Shape == null) continue;
                var geometry = rule.Shape.Resolve(Vec(e[4]), e[5].GetSingle(), null);
                var at = Vector3.Lerp(previous, p, (float)Math.Clamp((e[0].GetDouble() - t) / frame, 0, 1));
                Check(!geometry.Contains(at, .1f), $"Final hit by {e[3].GetUInt32()} at t={e[0].GetDouble():F2} p={at} source={e[4]} ({recording} side {side}).");
            }
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.Final).Contains(p), $"Left the Final floor at t={t:F2} p={p}.");
            if (p.Z < BlundervilleFinalPlanner.Bottom)
            {
                // Hand-over: the stair executor climbs blind; approximate its
                // timeline and keep checking every recorded impact.
                var handover = t;
                var stairs = new Vector3(MathF.Sign(p.X) * BlundervilleRoutes.FinalStairLane, p.Y, 137);
                var trigger = new Vector3(MathF.Sign(p.X) * 8.5f, p.Y, 128); var finish = new Vector3(MathF.Sign(p.X) * 8.5f, p.Y, 123.8f);
                var start = p;
                while (t < handover + BlundervilleFinalPlanner.StairSeconds + BlundervilleFinalPlanner.FinishWalkSeconds)
                {
                    t += frame; previous = p;
                    // Stair executor timeline: 153→137 in StairSeconds with a
                    // ±1.15m back-and-forth per step, then the finish walk.
                    var elapsed = t - handover;
                    if (elapsed < BlundervilleFinalPlanner.StairSeconds)
                    {
                        var s = (float)(elapsed / BlundervilleFinalPlanner.StairSeconds);
                        p = Vector3.Lerp(start, stairs, s) + Vector3.UnitZ * 1.15f * MathF.Sin(s * 4 * MathF.PI);
                    }
                    else
                    {
                        var s = (float)Math.Clamp((elapsed - BlundervilleFinalPlanner.StairSeconds) / BlundervilleFinalPlanner.FinishWalkSeconds, 0, 1);
                        p = s < .7f ? Vector3.Lerp(stairs, trigger, s / .7f) : Vector3.Lerp(trigger, finish, (s - .7f) / .3f);
                    }
                    while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
                    {
                        var e = impacts[hitIndex++];
                        if (!BlundervilleCatalog.TryGet(e[3].GetUInt32(), out var rule) || rule.Shape == null) continue;
                        var at = Vector3.Lerp(previous, p, (float)Math.Clamp((e[0].GetDouble() - t) / frame, 0, 1));
                        if (!rule.Shape.Resolve(Vec(e[4]), e[5].GetSingle(), null).Contains(at, .1f)) continue;
                        // The planner owns the run-up and the first flight; the
                        // later stair steps run blind in the jump executor and
                        // are only preferred clear (see StairsClear).
                        var message = $"Stair hand-over hit by {e[3].GetUInt32()} at t={e[0].GetDouble():F2} p={at} ({recording} side {side}).";
                        Check(t - handover > BlundervilleFinalPlanner.FirstFlightSeconds, message);
                        Console.WriteLine("  late " + message);
                    }
                }
                Console.WriteLine($"FINAL {recording} side {side}: reached the stairs in {handover:F2}s, idle {waits:F1}s, dispatch {move.Count}");
                Check(handover < 42, "The Final run was much slower than the manual qualifying runs (29–37s to z=160).");
                return;
            }
        }
        throw new InvalidOperationException($"The Final run never reached the stairs; stopped at {p} ({recording} side {side}).");
    }

    private sealed class Recording : IDisposable
    {
        private readonly JsonDocument _data;
        public readonly JsonElement[] Events;
        private readonly Dictionary<ulong, BlundervilleActor> _actors;
        public readonly BlundervilleEncounter Encounter = new(_ => { });
        private int _index; private double _sample = -1;
        public Recording(string name)
        {
            _data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "final-recorded.json")));
            var recording = _data.RootElement.GetProperty("recordings").GetProperty(name);
            Events = recording.GetProperty("events").EnumerateArray().ToArray();
            _actors = recording.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(),
                d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
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
                    case "despawn": _actors.Remove(id); break;
                    case "move": if (_actors.TryGetValue(id, out var moved)) _actors[id] = moved with { Position = Vec(e[3]), Rotation = e[4].GetSingle() }; break;
                    case "cast":
                        // Same as BlundervilleScript: the impact follows the cast bar by ~0.3s.
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
