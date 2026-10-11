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
        run("Blunderville obstacles a warm ordinary bridge cannot authorize missing or cold ice clocks", () =>
        {
            using var replay = new Recording(); replay.Advance(40);
            var player = new Vector3(-210.25f, 17.47f, -158); var now = Epoch.AddSeconds(40);
            Check(replay.Encounter.BumpersReady(player), "The complete recorded bridge never warmed up.");
            var ordinary = replay.Actors.Values.Where(a => a.Position.Z > -181).ToArray();
            var requested = 0;
            var planner = new BlundervilleBumperPlanner(_ => { ++requested; return Task.FromResult(Array.Empty<BlundervilleBumperPlanner.Step>()); });
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, now);
            var normal = new BlundervilleRaceDecision(player, false, false, null, "test");
            replay.Encounter.Synchronize(ordinary, now);
            Check(!replay.Encounter.BumpersReady(player) && planner.Apply(normal, state, replay.Encounter, player, now).Hold && requested == 0,
                "A missing ice bridge was treated as an empty safe corridor.");
            replay.Encounter.Synchronize(replay.Actors.Values, now.AddSeconds(.1));
            Check(!replay.Encounter.BumpersReady(player) && planner.Apply(normal, state, replay.Encounter, player, now.AddSeconds(.1)).Hold && requested == 0,
                "Freshly restored ice devices inherited learned clocks from the ordinary bridge.");
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
        run("Blunderville obstacles bounds unfinished workers across expired requests and resets", () =>
        {
            using var replay=new Recording(); replay.Advance(40);
            var now=Epoch.AddSeconds(40); var player=new Vector3(-210.25f,17.47f,-158);
            var state=new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance,0x80000001,20013,0,0,0,now);
            var pending=new List<TaskCompletionSource<BlundervilleBumperPlanner.Step[]>>();
            var planner=new BlundervilleBumperPlanner(_=>
            {
                var completion=new TaskCompletionSource<BlundervilleBumperPlanner.Step[]>();
                pending.Add(completion); return completion.Task;
            });
            var normal=new BlundervilleRaceDecision(player,false,false,null,"test");
            planner.Apply(normal,state,replay.Encounter,player,now);
            planner.Apply(normal,state,replay.Encounter,player,now.AddSeconds(BlundervilleBumperPlanner.PublicationWindow + .1));
            planner.Reset(); planner.Apply(normal,state,replay.Encounter,player,now.AddSeconds(BlundervilleBumperPlanner.PublicationWindow + .2));
            Check(pending.Count==1,"Expired work kept spawning additional CPU-bound searches.");
            pending[0].SetResult([new(player-Vector3.UnitZ,0)]);
            var decision=planner.Apply(normal,state,replay.Encounter,player,now.AddSeconds(BlundervilleBumperPlanner.PublicationWindow + .3));
            Check(decision.Hold && pending.Count==2,"An expired generation supplied movement or prevented a fresh request after completion.");
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
                var decision = planner.Apply(normal, state, replay.Encounter, player,
                    now.AddSeconds(reason == "deadline" ? BlundervilleBumperPlanner.PublicationWindow + .1 : .3));
                Check(decision.Hold && completions.Count == 2, "Stale/failed worker supplied movement instead of a fresh request.");
            });
        foreach (var phase in new[] { 27.0, 32.0, 40.0 })
            foreach (var delay in new[] { .2, .3 })
                run($"Blunderville obstacles bumper route phase {phase:F1} worker {delay:F1}", () => Simulate(false, phase, delay, 2));
        foreach (var delay in new[] { .2, .3 })
            // 1.12.0 requires committed ice endpoints to stay clear for 2.5s;
            // this sparse 02:28 recording then sometimes waits one more window
            // (29–41s across runs, depending on actual search CPU time).
            run($"Blunderville obstacles slippery bumper route worker {delay:F1}", () => Simulate(false, 27, delay, 2, slippery: true, maximumElapsed: 45));
        foreach (var phase in new[] { 22.5, 27.0, 30.5, 32.0 })
            run($"Blunderville obstacles latest slippery phase {phase:F1}", () => Simulate(false, phase, .2, 2, slippery: true,
                fixture: "obstacles-firsta-044552.json"));
        // ARR 2026-10-12 01:01: endpoint packets up to ~.5s late. The strict
        // two-cycle checks never confirmed every entry-lane clock at once.
        run("Blunderville obstacles 01:01 jittered clocks open and keep the entrance gate", () =>
        {
            using var replay = new Recording("obstacles-firsta-010120.json");
            var player = new Vector3(-210.25f, 17.47f, -158);
            for (var t = 28.5; t <= 70; t += .5)
            {
                replay.Advance(t);
                Check(replay.Encounter.BumpersReady(player), $"Late endpoint packets closed the cylinder entrance at {t:F1}s.");
            }
        });
        foreach (var fx in new[] { "obstacles-firsta-010120.json", "obstacles-firsta-044552.json" })
            run($"Blunderville obstacles learned pulses strictly cover every later impact {fx}", () =>
            {
                using var replay = new Recording(fx); var covered = 0;
                foreach (var e in replay.Events.Where(e => e[1].GetString() == "hit" && e[3].GetUInt32() == 34717 && e[0].GetDouble() > 30))
                {
                    var t = e[0].GetDouble(); replay.Advance(t - 1.3);
                    Check(replay.Encounter.Hazards(Epoch.AddSeconds(t - 1.3)).Any(h => h.Id.StartsWith($"rhythm/{e[2].GetUInt64()}/34717/")
                        && h.From <= Epoch.AddSeconds(t) && h.Until >= Epoch.AddSeconds(t) && h.Geometry.Contains(Vec(e[4]))),
                        $"Pulse at {t:F2}s from {e[2]} escaped its forecast window.");
                    ++covered;
                }
                Console.WriteLine($"PULSE strict coverage {fx}: {covered}");
            });
        // The 01:01 player reached the entrance at 16.5s. Learning the ice rows
        // takes until ~27s; the noisy section then may take up to 50s in total.
        foreach (var phase in new[] { 16.5, 20.0, 24.0, 29.8 })
            run($"Blunderville obstacles 01:01 entrance phase {phase:F1}", () => Simulate(false, phase, .3, 2, slippery: true,
                frameStep: 1.0 / 30, fixture: "obstacles-firsta-010120.json", maximumElapsed: 50));
        foreach (var fps in new[] { 30, 60 })
            foreach (var length in new[] { 4.88f, 5.1f })
                run($"Blunderville obstacles ice physics at {fps}fps slide {length:F2}", () => Simulate(false, 27, .3, 2, slippery: true,
                    frameStep: 1.0 / fps, fixture: "obstacles-firsta-044552.json", slideLength: length));
        foreach (var delay in new[] { .45, .65 })
            run($"Blunderville obstacles delayed ice continuation worker {delay:F2}", () => Simulate(false, 27, delay, 2, slippery: true,
                frameStep: 1.0 / 60, fixture: "obstacles-firsta-044552.json", slideLength: 5.1f));
        foreach (var entry in new[] { -181.0f, -181.4f })
            foreach (var lengths in new[] { new[] { 4.88f, 5.1f }, new[] { 5.1f, 4.88f } })
                run($"Blunderville obstacles ice entry {entry:F1} alternating slides {lengths[0]:F2}/{lengths[1]:F2}", () =>
                    Simulate(false, 27, .3, 2, slippery: true, frameStep: 1.0 / 30,
                        fixture: "obstacles-firsta-044552.json", iceEntry: entry, slideLengths: lengths));
        foreach (var iceSpeed in new[] { 2.88f, 2.93f })
            run($"Blunderville obstacles recorded ice speed {iceSpeed:F2}", () =>
                Simulate(false, 27, .3, 2, slippery: true, frameStep: 1.0 / 30,
                    fixture: "obstacles-firsta-044552.json", slideLengths: [4.88f, 4.97f], iceSpeed: iceSpeed));
        run("Blunderville obstacles long slow ice slides at 30fps", () =>
            Simulate(false, 27, .3, 2, slippery: true, frameStep: 1.0 / 30,
                fixture: "obstacles-firsta-044552.json", slideLength: 5.1f, iceSpeed: 2.88f));
        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville obstacles recorded ice acceleration at {fps}fps", () =>
                Simulate(false, 27, .3, 2, slippery: true, frameStep: 1.0 / fps,
                    fixture: "obstacles-firsta-044552.json", slideProfiles: RecordedSlides()));
        foreach (var phase in new[] { 28.0, 29.1, 30.5, 46.0 })
            foreach (var cycle in new[] { 1.2, 2.0, 3.0 })
                foreach (var inputDelay in new[] { 0.0, .02 })
                    run($"Blunderville obstacles arrow phase {phase:F1} rotation {cycle:F1} input {inputDelay:F2}", () => Simulate(true, phase, .3, cycle, inputDelay));
        foreach (var fps in new[] { 30, 60 })
            run($"Blunderville obstacles arrow at {fps}fps", () => Simulate(true, 28, .3, 1.2, .02, frameStep: 1.0 / fps));
    }

    private static void Simulate(bool arrow, double phase, double latency, double cycle, double inputDelay = 0, bool slippery = false, double frameStep = .01,
        string fixture = "obstacles-firsta-022846.json", float slideLength = 5, float iceEntry = -181.2f, IReadOnlyList<float>? slideLengths = null, float iceSpeed = 3,
        IReadOnlyList<SlideProfile>? slideProfiles = null, double maximumElapsed = 36)
    {
        using var replay = new Recording(fixture); var now = Epoch;
        var move = new Moving { Misdirection = arrow, Cycle = cycle, InputDelay = inputDelay, SlideLength = slideLength,
            SlideLengths = slideLengths, SlideProfiles = slideProfiles };
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
        var previousPosition = p;
        var scanTimes = new List<double>(); var frameTimes = new List<double>();
        for (var tick = 0; tick * frameStep < (arrow ? 32 : 60); ++tick)
        {
            var frameWatch = System.Diagnostics.Stopwatch.StartNew(); var workerBefore = workerTotal;
            var t = phase + tick * frameStep; elapsed = t - phase; now = Epoch.AddSeconds(t); move.Time = t;
            foreach (var task in scheduled.Where(s => s.At <= now).ToArray())
            { task.Source.SetResult(task.Result); scheduled.Remove(task); }
            move.Slippery = slippery && p.Z < iceEntry && p.Z > -204.5f;
            if (slippery) state.SynchronizeStatuses(move.Slippery ? [new(3693, 50, now.AddSeconds(10))] : [], now);
            var speed = state.NavigationBaseSpeed(now); move.Speed = move.Slippery ? iceSpeed : speed;
            if (t + 1e-7 >= nextScan)
            {
                nextScan = t + .1;
                replay.Advance(t); api.UtcNowForTest = now;
                var decision = planner.Update(state, now, p, true, false, false, arrow ? move.Arrow : null, arrow);
                if (arrow) decision = arrowPlanner.Apply(decision, state, p, move.Arrow, now, move.IsRunning);
                else
                {
                    var beforeExecution = nav.GetDirectTraversalExecution(now);
                    decision = bumperPlanner.Apply(decision, state, replay.Encounter, p, now, beforeExecution);
                }
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
            if (!arrow)
                foreach (var a in replay.Actors.Values.Where(a => a.Oid == 16339))
                    Check(Distance(replay.Position(a.Id, t), p) > 1.2f, $"Bumper contact t={t:F2} p={p} cylinder={a.Id}.");
            while (hitIndex < impacts.Length && impacts[hitIndex][0].GetDouble() <= t)
            {
                var effect = impacts[hitIndex++]; if (effect[0].GetDouble() < phase) continue;
                var action = effect[3].GetUInt32(); var pos = Vec(effect[4]);
                IGeometryShape shape = action == 34717 ? new CircleGeometry(pos, 3) : new RectangleGeometry(pos, effect[5].GetSingle(), 10, 4);
                var atImpact = Vector3.Lerp(previousPosition, p,
                    (float)Math.Clamp((effect[0].GetDouble() - (t - frameStep)) / frameStep, 0, 1));
                Check(!shape.Contains(atImpact, .1f), $"Animation hit t={effect[0].GetDouble():F3} p={atImpact} source={effect[2]} action={action}.");
            }
            var next = move.Advance(p, (float)frameStep); moving = Vector3.DistanceSquared(next, p) > .0001f;
            previousPosition = p; p = next;
            Check(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(p), $"Left floor t={t + frameStep:F2} p={p}.");
            if (p.Z < (arrow ? -288.5 : slippery ? -209 : -180.9)) break;
        }
        Console.WriteLine($"OBSTACLES arrow={arrow} slippery={slippery} phase={phase} cycle={cycle} end={p} elapsed={elapsed:F2} dispatch={move.Count} amendments={move.Amendments} stops={move.Stops} slideStops={move.SlideStops} movementMax={maxMs:F1}ms workerMax={workerMax:F1}ms");
        scanTimes.Sort(); frameTimes.Sort();
        Console.WriteLine($"OBSTACLES CPU scan p95={scanTimes[(int)(scanTimes.Count * .95)]:F2}ms max={scanTimes[^1]:F2}ms frame p95={frameTimes[(int)(frameTimes.Count * .95)]:F2}ms max={frameTimes[^1]:F2}ms (replay sampling + navigation + dispatch; excludes background search/native input/render)");
        Check(phase + elapsed <= replay.Events[^1][0].GetDouble(), "Simulation exceeded the available recording.");
        Check(p.Z < (arrow ? -288.5 : slippery ? -209 : -180.9), "Course section never finished.");
        Check(elapsed < maximumElapsed, "Repeated reacquisition/stops made the section too slow.");
        Check(move.Count + move.Amendments < 45, "Repeated queue changes destabilized a safe traversal.");
        if (!arrow) Check(move.SlideStops == 0 && move.Stops == 0, "The controller interrupted continuous bridge traversal.");
    }

    private sealed class Recording : IDisposable
    {
        public JsonDocument Data;
        public JsonElement[] Events;
        public Dictionary<ulong, BlundervilleActor> Actors;
        public BlundervilleEncounter Encounter = new(_ => { });
        private readonly Dictionary<ulong, (double Time, Vector3 Position)[]> _positions;
        private int _index; private double _sample = -1;
        public Recording(string fixture = "obstacles-firsta-022846.json")
        {
            Data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", fixture)));
            Events = Data.RootElement.GetProperty("events").EnumerateArray().ToArray();
            Actors = Data.RootElement.GetProperty("devices").EnumerateArray().Select(d => new BlundervilleActor(d.GetProperty("id").GetUInt64(), d.GetProperty("oid").GetUInt32(), Vec(d.GetProperty("position")), Rotation: d.GetProperty("rotation").GetSingle())).ToDictionary(a => a.Id);
            _positions = Events.Where(e => e[1].GetString() == "move").GroupBy(e => e[2].GetUInt64()).ToDictionary(g => g.Key,
                g => g.GroupBy(e => e[0].GetDouble()).Select(batch => (batch.Key, Vec(batch.Last()[3]))).ToArray());
        }
        public Vector3 Position(ulong id, double time)
        {
            if (!_positions.TryGetValue(id, out var positions) || positions.Length == 0) return Actors[id].Position;
            if (time <= positions[0].Time) return positions[0].Position;
            var low = 0; var high = positions.Length - 1;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (positions[middle].Time < time) low = middle + 1; else high = middle;
            }
            if (positions[low].Time < time) return positions[low].Position;
            var from = positions[low - 1]; var to = positions[low];
            return Vector3.Lerp(from.Position, to.Position, (float)((time - from.Time) / (to.Time - from.Time)));
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

    private sealed record SlideProfile(float Length, double Duration, IReadOnlyList<(double Time, float Distance)> Samples)
    {
        public float Progress(double time)
        {
            if (time >= Duration) return Length;
            for (var i = 1; i < Samples.Count; ++i)
                if (time <= Samples[i].Time)
                {
                    var from = Samples[i - 1]; var to = Samples[i];
                    return float.Lerp(from.Distance, to.Distance, (float)((time - from.Time) / (to.Time - from.Time)));
                }
            return Length;
        }
    }
    private static SlideProfile[] RecordedSlides()
    {
        using var recording = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville", "ice-firsta-044552.json")));
        var events = recording.RootElement.GetProperty("events").EnumerateArray().ToArray();
        var profiles = recording.RootElement.GetProperty("iceSlides").EnumerateArray().Take(2).Select(slide =>
        {
            var start = slide.GetProperty("start").GetDouble(); var end = slide.GetProperty("end").GetDouble();
            var origin = Vec(slide.GetProperty("startPos")); var target = Vec(slide.GetProperty("endPos"));
            var length = Distance(origin, target); var direction = Vector3.Normalize(target - origin);
            var samples = new List<(double Time, float Distance)> { (0, 0) };
            foreach (var e in events.Where(e => e[1].GetString() == "move" && e[2].GetUInt64() == 1
                && e[0].GetDouble() > start && e[0].GetDouble() < end).GroupBy(e => e[0].GetDouble()).OrderBy(g => g.Key))
                samples.Add((e.Key - start, Math.Clamp(Vector3.Dot(Vec(e.Last()[3]) - origin, direction), samples[^1].Distance, length)));
            samples.Add((end - start, length));
            Check(samples.Count >= 6 && length is > 4.8f and < 5.1f, "The recorded slide profile lost its physical samples.");
            return new SlideProfile(length, end - start, samples);
        }).ToArray();
        Check(profiles.Length == 2, "Both recorded slide profiles must participate in the simulation.");
        return profiles;
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index; private double _ready; private bool _held; private float _locked;
        private Vector3? _previous;
        public bool Misdirection, Slippery; public double Cycle, Time, InputDelay; public int Count, Stops, SlideStops, Amendments;
        private float _slideRemaining; private Vector3 _slideHeading;
        public float Speed = 6, SlideLength = 5;
        public IReadOnlyList<float>? SlideLengths;
        public IReadOnlyList<SlideProfile>? SlideProfiles;
        private SlideProfile? _slideProfile;
        private double _slideAge;
        private int _slides;
        public float Arrow => _held ? _locked : (float)(Time * Math.Tau / Cycle);
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null, IsRunning ? _points[^1] : null,
            IsRunning ? _index : 0, _points.Count, IsRunning ? Math.Max(0, _points.Count - _index - 1) : 0);
        public void MoveTo(GreenMovePoint p) => MoveTo([p]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> p) { var running = IsRunning; _points = p; _index = 0; _previous = null; ++Count; if (!running) _ready = Time + (Misdirection ? InputDelay : .08); }
        public void Stop() { ++Stops; if (Slippery && _slideRemaining > .1f) ++SlideStops; _points = []; _index = 0; _previous = null; _held = false; }
        public void Append(GreenMovePoint p) => Append([p]);
        public void Append(IReadOnlyList<GreenMovePoint> p) => _points = _points.Concat(p).ToArray();
        public void ClearQueue() { _points = _points.Take(_index + 1).ToArray(); ++Amendments; }
        public void SkipCurrent() => ++_index;
        public Vector3 Advance(Vector3 p, float dt)
        {
            var previous = _previous; _previous = p;
            // GreenMoveManager consumes all arrived points before writing this
            // frame's input. GreenMoveArrival also recognizes a point crossed
            // between frames, but deliberately excludes teleports over 2m.
            while (IsRunning && Reached(p, previous, _points[_index])) { ++_index; _held = false; }
            if (!Slippery) _slideRemaining = 0;
            if (Slippery && _slideRemaining > 0)
            {
                // Release/redirect cannot steer or brake the native 5m slide.
                var step = SlideStep(dt);
                return p + _slideHeading * step;
            }
            if (!IsRunning || Time + 1e-7 < _ready) { _held = false; return p; }
            var d = _points[_index].Position - p; d.Y = 0; var distance = d.Length();
            if (Misdirection)
            {
                var a = Arrow;
                if (MathF.Abs(MathF.IEEERemainder(MathF.Atan2(d.X, d.Z) - a, MathF.Tau)) > MathF.PI / 12) { _held = false; return p; }
                _held = true; _locked = a;
                return p + new Vector3(MathF.Sin(a), 0, MathF.Cos(a)) * (Speed * dt);
            }
            if (Slippery)
            {
                _slideHeading = d / distance;
                _slideProfile = SlideProfiles is { Count: > 0 } ? SlideProfiles[_slides % SlideProfiles.Count] : null;
                _slideAge = 0;
                _slideRemaining = _slideProfile?.Length ?? (SlideLengths is { Count: > 0 } ? SlideLengths[_slides % SlideLengths.Count] : SlideLength); ++_slides;
                var step = SlideStep(dt);
                return p + _slideHeading * step;
            }
            // Native analog input advances at the current speed; it does not
            // clamp physical displacement to the target waypoint.
            return p + d / distance * (Speed * dt);
        }
        private float SlideStep(float dt)
        {
            var step = _slideProfile == null ? Speed * dt : _slideAge + dt >= _slideProfile.Duration ? _slideRemaining
                : _slideProfile.Progress(_slideAge + dt) - _slideProfile.Progress(_slideAge);
            _slideAge += dt;
            step = Math.Clamp(step, 0, _slideRemaining); _slideRemaining -= step;
            return step;
        }
        private static bool Reached(Vector3 current, Vector3? previous, GreenMovePoint point)
        {
            var target = point.Position;
            if (point.IgnoreY) { current.Y = target.Y = 0; if (previous is { } p) previous = p with { Y = 0 }; }
            if (Vector3.DistanceSquared(current, target) <= point.Tolerance * point.Tolerance) return true;
            if (previous is not { } from) return false;
            var step = current - from; var lengthSquared = step.LengthSquared();
            if (lengthSquared < .000001f || lengthSquared > 4) return false;
            var along = Vector3.Dot(target - from, step) / lengthSquared;
            return along is >= 0 and <= 1
                && Vector3.DistanceSquared(target, from + step * along) <= point.Tolerance * point.Tolerance;
        }
    }
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
