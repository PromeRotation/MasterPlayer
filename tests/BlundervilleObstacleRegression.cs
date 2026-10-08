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

internal static class BlundervilleObstacleRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static void Run(Action<string, Action> run)
    {
        run("Blunderville obstacles cold and expired tracking never authorize a learned route", () =>
        {
            var tracker = new BlundervilleBumperTracker();
            var actor = new BlundervilleActor(1, 16339, new(-210, 17.47f, -166), HitboxRadius: 1.8f);
            tracker.Observe(actor, Epoch);
            Check(!tracker.Ready(actor.Id) && tracker.Hazards.All(h => h.Until <= Epoch.AddSeconds(1.2)), "Cold observations invented a long animation cycle.");
            Check(tracker.Hazards.Any(h => h.From <= Epoch && h.Until >= Epoch && h.Geometry.Contains(actor.Position + Vector3.UnitX * 1.8f)), "Cold forecast lost the native body radius.");
            using var replay = new Recording(); replay.Advance(40);
            var player = new Vector3(-210.25f, 17.47f, -158);
            Check(replay.Encounter.BumpersReady(player), "Recorded clocks never became ready.");
            replay.Encounter.Synchronize(replay.Actors.Values, Epoch.AddSeconds(54));
            Check(!replay.Encounter.BumpersReady(player), "Expired clocks still authorized entering the cylinder lane.");
        });
        run("Blunderville obstacles body trajectory coverage", () =>
        {
            using var replay = new Recording(); var misses = 0; var total = 0; var extra = 0f;
            foreach(var e in replay.Events.Where(e => e[1].GetString()=="move" && replay.Actors[e[2].GetUInt64()].Oid == 16339 && e[0].GetDouble() > 38))
            {
                var t=e[0].GetDouble(); replay.Advance(t-1);
                var p=Vec(e[3]);
                var hazards=replay.Encounter.Hazards(Epoch.AddSeconds(t-1)).Where(h => h.Id.StartsWith($"bumper/{e[2].GetUInt64()}/") && h.From <= Epoch.AddSeconds(t) && h.Until >= Epoch.AddSeconds(t)).ToArray();
                ++total;
                foreach(var q in new[]{p-Vector3.UnitX*1.2f,p+Vector3.UnitX*1.2f})
                    extra=MathF.Max(extra,hazards.Min(h=>MathF.Max(h.Geometry.GetBounds().MinX-q.X,q.X-h.Geometry.GetBounds().MaxX)));
                if(!new[]{p-Vector3.UnitX*1.2f,p+Vector3.UnitX*1.2f}.All(q => hazards.Any(h=>h.Geometry.Contains(q))))
                { if(misses++<10)Console.WriteLine($"BODY miss {e}; hazards={string.Join(';',hazards.Select(h=>$"{h.Geometry.Position} {((RectangleGeometry)h.Geometry).Width}"))}"); }
            }
            Console.WriteLine($"BODY coverage {total-misses}/{total} residual={extra}");
            Check(misses==0,"Future moving body escaped the forecast envelope.");
        });
        run("Blunderville obstacles recorded contact stuns precede pulse effects", () =>
        {
            using var replay = new Recording();
            foreach (var contact in replay.Data.RootElement.GetProperty("contacts").EnumerateArray())
            {
                var t = contact[0].GetDouble(); replay.Advance(t - .3);
                var p = Vec(contact[2]);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - .3)).Any(h => h.Id.StartsWith($"bumper/{contact[1].GetUInt64()}/") && h.Geometry.Contains(p)
                    && h.From <= Epoch.AddSeconds(t) && h.Until >= Epoch.AddSeconds(t)), "Contact stun outside predicted bumper travel lane.");
            }
            replay.Encounter.Synchronize([], Epoch.AddSeconds(70));
            Check(!replay.Encounter.Hazards(Epoch.AddSeconds(70)).Any(), "Despawn retained bumper hazards.");
        });
        run("Blunderville obstacles endpoint pulses predicted before the short cast", () =>
        {
            using var replay = new Recording(); var covered = 0;
            foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 34717 && e[0].GetDouble() > 43))
            {
                var t = e[0].GetDouble(); replay.Advance(t - 1.3);
                Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - 1.3)).Any(h => h.Id.StartsWith($"rhythm/{e[2].GetUInt64()}/34717/")
                    && h.From.AddSeconds(-.5) <= Epoch.AddSeconds(t) && h.Until.AddSeconds(.3) >= Epoch.AddSeconds(t)
                    && h.Geometry.Contains(Vec(e[4]))), $"Missing pulse forecast {e}."); ++covered;
            }
            Check(covered > 100, "Insufficient bumper cycles.");
        });
        foreach (var reason in new[] { "reset", "fault", "deadline" })
            run($"Blunderville obstacles discard background results after {reason}", () =>
            {
                using var replay = new Recording(); replay.Advance(40);
                var now = Epoch.AddSeconds(40); var player = new Vector3(-210.25f, 17.47f, -158);
                var state = new BlundervilleRaceState();
                state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, now);
                var completions = new List<TaskCompletionSource<BlundervilleBumperPlanner.Step[]>>();
                var planner = new BlundervilleBumperPlanner(_ =>
                {
                    var completion = new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>();
                    completions.Add(completion); return completion.Task;
                });
                var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
                Check(planner.Apply(normal, state, replay.Encounter, player, now).Hold && completions.Count == 1, "Worker was not deferred.");
                Check(planner.Apply(normal, state, replay.Encounter, player, now.AddSeconds(.25)).Hold && completions.Count == 1, "Unfinished worker was joined or replaced early.");
                if (reason == "reset") planner.Reset();
                if (reason == "fault") completions[0].SetException(new InvalidOperationException("worker failure"));
                else completions[0].SetResult([new(player - Vector3.UnitZ, 0)]);
                var decision = planner.Apply(normal, state, replay.Encounter, player, now.AddSeconds(reason == "deadline" ? .6 : .3));
                Check(decision.Hold && completions.Count == 2, "Stale/failed worker supplied movement instead of a fresh request.");
            });
        foreach (var phase in new[] { 27.0, 32.0, 40.0 })
            foreach (var delay in new[] { .2, .3 })
                run($"Blunderville obstacles bumper route phase {phase:F1} worker {delay:F1}", () => Simulate(false, phase, delay, 2));
        foreach (var delay in new[] { .2, .3 })
            run($"Blunderville obstacles slippery bumper route worker {delay:F1}", () => Simulate(false, 27, delay, 2, slippery: true));
        foreach (var phase in new[] { 28.0, 29.1, 30.5, 46.0 })
            foreach (var cycle in new[] { 1.2, 2.0, 3.0 })
                foreach (var inputDelay in new[] { 0.0, .02 })
                    run($"Blunderville obstacles arrow phase {phase:F1} rotation {cycle:F1} input {inputDelay:F2}", () => Simulate(true, phase, .3, cycle, inputDelay));
        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville obstacles arrow at {fps}fps", () => Simulate(true, 28, .3, 1.2, .02, frameStep: 1.0 / fps));
    }

    private static void Simulate(bool arrow, double phase, double latency, double cycle, double inputDelay = 0, bool slippery = false, double frameStep = .01)
    {
        using var replay = new Recording(); var now = Epoch;
        var move = new Moving { Misdirection = arrow, Cycle = cycle, InputDelay = inputDelay };
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        using var api = new ScriptSafeZoneApi(); api.ShareManagerForTest(runtime.Manager);
        var nav = new BlundervilleNavigation(runtime, api, new()); var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
        if (arrow) state.SynchronizeStatuses([new(3694, 0, Epoch.AddMinutes(2))], Epoch);
        var planner = new BlundervilleRacePlanner(); var arrowPlanner = new BlundervilleArrowPlanner();
        var workerMax = 0.0; var workerTotal = 0.0;
        var scheduled = new List<(TaskCompletionSource<BlundervilleBumperPlanner.Step[]> Source, BlundervilleBumperPlanner.Step[] Result, DateTime At)>();
        var bumperPlanner = new BlundervilleBumperPlanner(work =>
        {
            var timer = System.Diagnostics.Stopwatch.StartNew(); var value = work();
            var milliseconds = timer.Elapsed.TotalMilliseconds;
            workerMax = Math.Max(workerMax, milliseconds); workerTotal += milliseconds;
            var completion = new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>();
            scheduled.Add((completion, value, now.AddSeconds(Math.Max(latency, milliseconds / 1000))));
            return completion.Task;
        }, latency);
        var p = arrow ? new Vector3(-200, 10.03f, -248.2f) : new Vector3(-210.25f, 17.47f, -158);
        SafeZoneSolution? pending = null, accepted = null; var ready = 0.0; var nextSolve = phase;
        var hitIndex = 0; var impacts = replay.Events.Where(e => e[1].GetString() == "hit").ToArray();
        var moving = false; var elapsed = 0.0; var maxMs = 0.0; BlundervilleRaceDecision? lastDecision = null;
        var nextScan = phase;
        var scanTimes = new List<double>(); var frameTimes = new List<double>();
        for (var tick = 0; tick * frameStep < (arrow ? 32 : 35); ++tick)
        {
            var frameWatch = System.Diagnostics.Stopwatch.StartNew(); var workerBefore = workerTotal;
            var t = phase + tick * frameStep; elapsed = t - phase; now = Epoch.AddSeconds(t); move.Time = t;
            foreach (var task in scheduled.Where(s => s.At <= now).ToArray())
            { task.Source.SetResult(task.Result); scheduled.Remove(task); }
            if (slippery) state.SynchronizeStatuses(p.Z is < -182 and > -205 ? [new(3693, 50, now.AddSeconds(10))] : [], now);
            var speed = state.NavigationBaseSpeed(now); move.Speed = speed;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1;
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, arrow ? move.Arrow : null, arrow);
                if (arrow) decision = arrowPlanner.Apply(decision, state, p, move.Arrow, now, move.IsRunning);
                else decision = bumperPlanner.Apply(decision, state, replay.Encounter, p, now);
                nav.Apply(state, decision, replay.Encounter, now, p, true, false, false);
                lastDecision = decision;
                if (elapsed > 1) scanTimes.Add(frameWatch.Elapsed.TotalMilliseconds - (workerTotal - workerBefore));
            }
            else if (arrow && lastDecision is { DirectTraversal: true })
            {
                lastDecision = arrowPlanner.Apply(lastDecision, state, p, move.Arrow, now, move.IsRunning);
                nav.ApplyDirectFrame(state, lastDecision, now);
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(p, 0, speed, moving, false, 0, true, snapshot.Version);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            if (lastDecision?.DirectTraversal == true) ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(player, new(speed, speed, 0, false, speed, speed), now);
            else
            {
                if (pending != null && t >= ready) { accepted = pending; pending = null; }
                if (pending == null && t >= nextSolve)
                {
                    var options = (SolveOptions)runtime.MovementStatus.GetType().GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!;
                    pending = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), options, now);
                    ready = t + Math.Max(latency, watch.Elapsed.TotalSeconds); nextSolve = ready + .1;
                }
                runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            }
            maxMs = Math.Max(maxMs, watch.Elapsed.TotalMilliseconds);
            if (elapsed > 1) frameTimes.Add(frameWatch.Elapsed.TotalMilliseconds - (workerTotal - workerBefore));
            var next = move.Advance(p, (float)frameStep); moving = Vector3.DistanceSquared(next, p) > .0001f; p = next;
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(p), $"Left floor t={t:F2} p={p}.");
            if (!arrow)
                foreach (var a in replay.Actors.Values.Where(a => a.Oid == 16339))
                    Check(Distance(a.Position, p) > 1.2f, $"Bumper contact t={t:F2} p={p} cylinder={a.Id}.");
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
            {
                var effect = impacts[hitIndex++]; if (effect[0].GetDouble() < phase) continue;
                var action = effect[3].GetUInt32(); var pos = Vec(effect[4]);
                IGeometryShape shape = action == 34717 ? new CircleGeometry(pos, 3) : new RectangleGeometry(pos, effect[5].GetSingle(), 10, 4);
                Check(!shape.Contains(p, .1f), $"Animation hit t={t:F2} p={p} source={effect[2]} action={action}.");
            }
            if (p.Z < (arrow ? -288.5 : -207)) break;
        }
        Console.WriteLine($"OBSTACLES arrow={arrow} slippery={slippery} phase={phase} cycle={cycle} end={p} elapsed={elapsed:F2} dispatch={move.Count} stops={move.Stops} movementMax={maxMs:F1}ms workerMax={workerMax:F1}ms");
        scanTimes.Sort(); frameTimes.Sort();
        Console.WriteLine($"OBSTACLES CPU scan p95={scanTimes[(int)(scanTimes.Count * .95)]:F2}ms max={scanTimes[^1]:F2}ms frame p95={frameTimes[(int)(frameTimes.Count * .95)]:F2}ms max={frameTimes[^1]:F2}ms (replay sampling + navigation + dispatch; excludes background search/native input/render)");
        Check(phase + elapsed <= replay.Events[^1][0].GetDouble(), "Simulation exceeded the available recording.");
        Check(p.Z < (arrow ? -288.5 : -207), "Course section never finished.");
        Check(elapsed < (arrow ? 30 : 30), "Repeated reacquisition/stops made the section too slow.");
        Check(move.Count < 45, "Repeated MoveTo restarted a safe traversal.");
    }

    private sealed class Recording : IDisposable
    {
        public JsonDocument Data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "obstacles-firsta-022846.json")));
        public JsonElement[] Events;
        public Dictionary<ulong, BlundervilleActor> Actors;
        public BlundervilleEncounter Encounter = new(_ => { });
        private int _index; private double _sample = -1;
        public Recording()
        {
            Events = Data.RootElement.GetProperty("events").EnumerateArray().ToArray();
            Actors = Data.RootElement.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(), d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
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
                if (e[1].GetString() == "move") Actors[id] = Actors[id] with { Position = Vec(e[3]), Rotation = e[4].GetSingle() };
                else if (e[1].GetString() == "cast")
                {
                    var cast = new BlundervilleCast(e[3].GetUInt32(), Vec(e[5]), e[6].GetSingle(), at, at.AddSeconds(e[4].GetDouble() + .3), Vec(e[5]));
                    Actors[id] = Actors[id] with { Cast = cast };
                    Encounter.StartCast(id, cast, at);
                }
                else
                {
                    Encounter.Effect(id, e[3].GetUInt32(), (uint)_index, Vec(e[4]), e[5].GetSingle(), at);
                    Actors[id] = Actors[id] with { Cast = null };
                }
            }
            Encounter.Synchronize(Actors.Values, Epoch.AddSeconds(time));
        }
        public void Dispose() => Data.Dispose();
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private double _ready; private bool _held; private float _locked;
        public bool Misdirection; public double Cycle, Time, InputDelay; public int Count, Stops;
        public float Speed = 6;
        public float Arrow => _held ? _locked : (float)(Time * Math.Tau / Cycle);
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, _points.LastOrDefault(), _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; ++Count; if (!running) _ready = Time + (Misdirection ? InputDelay : .08); }
        public void Stop() { ++Stops; _points = []; _index = 0; _held = false; }
        public void Append(GreenMovePoint p) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> p) => throw new NotSupportedException();
        public void ClearQueue() => Stop(); public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            if (!IsRunning || Time + 1e-7 < _ready) { _held = false; return p; }
            var d = _points[_index].Position - p; d.Y = 0; var distance = d.Length();
            if (distance <= _points[_index].Tolerance) { ++_index; _held = false; return p; }
            if (Misdirection)
            {
                var a = Arrow;
                if (MathF.Abs(MathF.IEEERemainder(MathF.Atan2(d.X, d.Z) - a, MathF.Tau)) > MathF.PI / 12) { _held = false; return p; }
                _held = true; _locked = a;
                return p + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * (Speed * dt);
            }
            return p + d / distance * MathF.Min(distance, Speed * dt);
        }
    }
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
