using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Crucible;
using MasterPlayer.Scripting;
using PromeRotation.GreenMoveSystem;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

// ARR: 斗兽奇弈高段 第一盘 2026.10.26 23.15.58.dat. B22 quake chapter pad 19y away (arrived 0.7s
// late); B31 six-pulse breath pocket reached only at its deadline, after a drifting cloud touched
// the stationary player. Replays the real components, local solver and GreenMove at run speed.
internal static class RecordedHigh1FeedbackRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 26, 14, 15, 58, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void Run(Action<string, Action> run)
    {
        run("Recorded high-one 231558 dragon breath pocket through drifting clouds", Dragon);
        foreach (var delay in new[] { .08, .3 })
        {
            run($"Recorded high-one 231558 owl chapters reach the pads, {delay:F2}s worker", () => Replay("owl-chapters", delay));
        }
    }

    private static Vector3 Vector(JsonElement e) => new(e[0].GetSingle(), 0, e[2].GetSingle());

    private static void Replay(string key, double delay)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Crucible", "high1-231558.json")));
        var fight = fixture.RootElement.GetProperty("fights").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key);
        var world = new WorldState { CurrentTime = Epoch };
        var boss = new Actor { InstanceID = 1, OID = fight.GetProperty("oid").GetUInt32(), NameID = fight.GetProperty("nameId").GetUInt32(),
            Position = WPos.FromVec3(Vector(fight.GetProperty("boss"))), HPMP = (100, 100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true, HitboxRadius = 2 };
        var player = new Actor { InstanceID = 2, Position = WPos.FromVec3(Vector(fight.GetProperty("player"))), HPMP = (100, 100), Type = ActorType.Player, IsTargetable = true };
        var helper = new Actor { InstanceID = 3, OID = 9020, NameID = boss.NameID, Position = boss.Position, HPMP = (100, 100), Type = ActorType.Enemy };
        world.Actors.Add(boss); world.Actors.Add(player); world.Actors.Add(helper); world.Player = player;
        if (fight.TryGetProperty("objects", out var objects))
        {
            var id = 50ul;
            foreach (var o in objects.EnumerateArray())
                world.Actors.Add(new Actor { InstanceID = id++, OID = o.GetProperty("oid").GetUInt32(), Position = WPos.FromVec3(Vector(o.GetProperty("pos"))), Type = ActorType.EventObj, HPMP = (1, 1) });
        }
        var clouds = new List<(Actor Actor, JsonElement[] Track)>();
        if (fight.TryGetProperty("clouds", out var cloudList))
            foreach (var c in cloudList.EnumerateArray())
                clouds.Add((new Actor { InstanceID = c.GetProperty("actor").GetUInt64(), OID = 19674, NameID = 14630, HPMP = (100, 100), Type = ActorType.Enemy, IsDestroyed = true },
                    c.GetProperty("track").EnumerateArray().ToArray()));
        foreach (var (cloud, _) in clouds) world.Actors.Add(cloud);

        var engine = new CrucibleEngine(world, 22, 31); engine.SelectGroup();
        using var safeZone = new ScriptSafeZoneApi { DangerMarginForTest = .5f };
        var output = new CrucibleOutput(safeZone, new ScriptDrawApi(), safeZone.Manager, _ => { }, () => .5f);
        var (movement, move) = ((object, IGreenMoveSystem))typeof(LateDodgeRegression).GetMethod("CreateMovement", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!;
        var type = movement.GetType();
        type.GetField("_manager", Private)!.SetValue(movement, safeZone.Manager);
        type.GetField("_setting", Private)!.SetValue(movement, new SafeZoneSetting { EnableSafeZone = true, EnableAutoMovement = true, AttackUptimeExpiresAt = Epoch.AddMinutes(10) });
        var latest = type.GetField("_latestSolution", Private)!;
        var dispatch = type.GetMethod("Dispatch", Private)!;
        var valid = type.GetMethod("IsSolutionValid", Private)!;
        var advance = move.GetType().GetMethod("Advance")!;
        var setDirect = type.GetMethod("SetDirectTraversal", Private)!;
        var updateDirect = type.GetMethod("UpdateDirectTraversal", Private)!;

        var casts = fight.GetProperty("casts").EnumerateArray().ToArray();
        var started = new HashSet<int>(); var resolved = new HashSet<int>();
        SafeZoneSolution? pending = null;
        double ready = 0, nextSolve = 0, movementReady = 0, closestCloud = double.MaxValue;
        var wasMoving = false;
        var position = player.Position.ToVec3();
        var end = casts.Max(c => c.TryGetProperty("hit", out var h) ? h.GetDouble() : c.GetProperty("event").GetDouble()) + .5;
        var failures = new List<string>();
        double worstFrame = 0, worstApply = 0, totalMs = 0, worstHost = 0, totalHost = 0, solveMs = 0, worstSolve = 0; var frames = 0;
        for (var tick = 0; tick * .02 <= end; ++tick)
        {
            var t = tick * .02; var now = Epoch.AddSeconds(t);
            world.CurrentTime = now; player.Position = WPos.FromVec3(position);
            foreach (var (cloud, track) in clouds)
            {
                var sample = track.LastOrDefault(s => s[0].GetDouble() <= t);
                var alive = sample.ValueKind == JsonValueKind.Array && sample[1].ValueKind == JsonValueKind.Number;
                if (alive) cloud.Position = new(sample[1].GetSingle(), sample[2].GetSingle());
                cloud.IsDestroyed = !alive;
                if (alive) closestCloud = Math.Min(closestCloud, (cloud.Position - player.Position).Length());
            }
            for (var i = 0; i < casts.Length; ++i)
            {
                var c = casts[i]; var action = c.GetProperty("action").GetUInt32();
                if (c.TryGetProperty("start", out var start) && t >= start.GetDouble() && started.Add(i))
                    engine.Cast(boss, new() { Action = new(action), FinishAt = Epoch.AddSeconds(c.GetProperty("finish").GetDouble()),
                        LocXZ = WPos.FromVec3(Vector(c.GetProperty("location"))), Rotation = c.GetProperty("rotation").GetSingle().Degrees() });
                var hitAt = c.TryGetProperty("hit", out var hit) ? hit.GetDouble() : c.GetProperty("event").GetDouble();
                if (t >= hitAt && resolved.Add(i))
                {
                    if (action is 48657 or 48658)
                    {
                        var pad = world.Actors.Single(a => a.OID == (action == 48657 ? 2004354u : 2015457u));
                        if (!player.Position.InCircle(pad.Position, 6))
                            failures.Add($"{action} at {t:F2}s: {(player.Position - pad.Position).Length():F1}y from its pad");
                        engine.Effect(boss, new() { Action = new(action), GlobalSequence = (uint)i + 1 });
                        engine.FinishCast(boss, action);
                    }
                    else if (action == 48807)
                        engine.FinishCast(boss, action);
                    else if (action == 48808)
                    {
                        helper.Position = WPos.FromVec3(Vector(c.GetProperty("origin")));
                        if (!player.Position.InCircle(helper.Position, 7))
                            failures.Add($"breath pulse at {t:F2}s: {(player.Position - helper.Position).Length():F1}y from the pocket");
                        engine.Effect(helper, new() { Action = new(action), GlobalSequence = (uint)i + 1 });
                    }
                }
            }
            player.Position = WPos.FromVec3(position);
            var frameWatch = System.Diagnostics.Stopwatch.StartNew();
            engine.Update(); var frame = engine.Frame(); var frameMs = frameWatch.Elapsed.TotalMilliseconds;
            output.Apply(engine, frame, 0, true, true, true);
            var applyMs = frameWatch.Elapsed.TotalMilliseconds - frameMs;
            if (tick > 5) { worstFrame = Math.Max(worstFrame, frameMs); worstApply = Math.Max(worstApply, applyMs); totalMs += frameMs + applyMs; ++frames; }
            var hostWatch = System.Diagnostics.Stopwatch.StartNew();
            var snapshot = safeZone.Manager.BuildSnapshot(now);
            // CrucibleScript hands a component's timed route to the runtime as a direct traversal.
            if (frame.Hints.DirectRoute is { Count: > 0 } route)
                try { setDirect.Invoke(movement, [route[^1].ToVec3(), now.AddSeconds(.25), route.Select(r => r.ToVec3()).ToArray(), 0f, null, now]); } catch (TargetInvocationException e) { Console.WriteLine(e.InnerException); throw; }
            bool direct; try { direct = (bool)updateDirect.Invoke(movement, [new PlayerState(position, 0, 6, move.IsRunning, false, 0, true, snapshot.Version), snapshot, now, true])!; } catch (TargetInvocationException e) { Console.WriteLine(e.InnerException); throw; }
            if (direct)
            {
                pending = null;
                if (move.IsRunning && !wasMoving) movementReady = t + .08;
                if (t >= movementReady) position = (Vector3)advance.Invoke(move, [position, .02f])!;
                wasMoving = move.IsRunning;
                continue;
            }
            if (pending != null && t >= ready) { latest.SetValue(movement, pending); pending = null; }
            var accepted = (SafeZoneSolution?)latest.GetValue(movement);
            if (pending == null && t >= nextSolve && !(bool)valid.Invoke(movement, [accepted, snapshot, new PlayerState(position, 0, 6, false, false, 0, true, snapshot.Version), now])!)
            {
                var solveWatch = System.Diagnostics.Stopwatch.StartNew();
                pending = new SafeZoneSolver().Solve(snapshot, new(position, 0, 6, false, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), new(), now, accepted);
                ready = t + delay; nextSolve = t + .1;
                solveMs = solveWatch.Elapsed.TotalMilliseconds; worstSolve = Math.Max(worstSolve, solveMs);
            }
            dispatch.Invoke(movement, [accepted, new PlayerState(position, 0, 6, false, false, 0, true, snapshot.Version), snapshot, now]);
            var hostMs = hostWatch.Elapsed.TotalMilliseconds - solveMs; solveMs = 0;
            if (tick > 5) { worstHost = Math.Max(worstHost, hostMs); totalHost += hostMs; }
            if (move.IsRunning && !wasMoving) movementReady = t + .08;
            if (t >= movementReady) position = (Vector3)advance.Invoke(move, [position, .02f])!;
            wasMoving = move.IsRunning;
            if (Environment.GetEnvironmentVariable("MP_H1_TRACE") == "1" && tick % 10 == 0)
                Console.WriteLine($"    t={t:F2} p=({position.X:F1},{position.Z:F1}) goals={string.Join(";", snapshot.GoalAreas.Select(g => $"{g.Geometry.Position.X:F0},{g.Geometry.Position.Z:F0}"))} dangers={snapshot.DangerAreas.Count} sol={accepted?.Status} path={accepted?.ExecutablePath.Count} tgt={accepted?.TargetPosition} goalwin={string.Join(",",snapshot.GoalAreas.Select(g=>g.GoalWindows.Count))} run={move.IsRunning}");
        }
        output.Clear();
        Console.WriteLine($"  {key}: main thread worst engine {worstFrame:F2}ms, worst output {worstApply:F2}ms, mean {totalMs / Math.Max(1, frames):F2}ms; snapshot+route check worst {worstHost:F2}ms mean {totalHost / Math.Max(1, frames):F2}ms; background solve worst {worstSolve:F1}ms");
        if (clouds.Count > 0)
        {
            Console.WriteLine($"  {key}: closest cloud {closestCloud:F2}y");
            // 23:15 ARR: cloud contact fired at ~2.3y.
            if (closestCloud < 2.4) failures.Add($"cloud came within {closestCloud:F2}y");
        }
        if (failures.Count > 0) throw new InvalidOperationException($"{key}: " + string.Join("; ", failures));
    }

    // Full runtime: CrucibleOutput + SafeZoneRuntime (solver, dispatch, direct traversal) + mover,
    // the way CrucibleScript drives it, with all 24 recorded cloud tracks.
    private static void Dragon()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Crucible", "high1-231558.json")));
        var fight = fixture.RootElement.GetProperty("fights").EnumerateArray().Single(f => f.GetProperty("key").GetString() == "dragon-waves");
        var now = Epoch;
        var world = new WorldState { CurrentTime = now };
        var boss = new Actor { InstanceID = 1, OID = 19672, NameID = 14628, Position = new(920, -420), HPMP = (100, 100), Type = ActorType.Enemy, IsTargetable = true, InCombat = true, HitboxRadius = 2 };
        var player = new Actor { InstanceID = 2, Position = WPos.FromVec3(Vector(fight.GetProperty("player"))), HPMP = (100, 100), Type = ActorType.Player, IsTargetable = true };
        var helper = new Actor { InstanceID = 3, OID = 9020, NameID = 14628, Position = boss.Position, HPMP = (100, 100), Type = ActorType.Enemy };
        world.Actors.Add(boss); world.Actors.Add(player); world.Actors.Add(helper); world.Player = player;
        var clouds = fight.GetProperty("clouds").EnumerateArray().Select(c => (Actor: new Actor { InstanceID = c.GetProperty("actor").GetUInt64(), OID = 19674, NameID = 14630,
            HPMP = (100, 100), Type = ActorType.Enemy, IsDestroyed = true }, Track: c.GetProperty("track").EnumerateArray().ToArray())).ToArray();
        foreach (var (cloud, _) in clouds) world.Actors.Add(cloud);
        var engine = new CrucibleEngine(world, 22, 31); engine.SelectGroup();
        var move = new BlundervilleRingRegression.Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => now };
        runtime.Setting.EnableSafeZone = true; runtime.Setting.EnableAutoMovement = true;
        using var api = new ScriptSafeZoneApi { DangerMarginForTest = .5f }; api.ShareManagerForTest(runtime.Manager);
        var output = new CrucibleOutput(api, new ScriptDrawApi(), runtime.Manager, _ => { }, () => .5f);
        var owner = new object(); var routing = false;
        var casts = fight.GetProperty("casts").EnumerateArray().ToArray();
        var done = new HashSet<int>(); var started = false;
        var p = player.Position.ToVec3(); var closest = double.MaxValue; var failures = new List<string>();
        double nextFrame = 0, worstHints = 0, closestAt = 0;
        for (var tick = 0; tick * .02 <= 19; ++tick)
        {
            var t = tick * .02; now = Epoch.AddSeconds(t);
            move.Now = now; api.UtcNowForTest = now; world.CurrentTime = now; player.Position = WPos.FromVec3(p);
            foreach (var (cloud, track) in clouds)
            {
                var sample = track.LastOrDefault(s => s[0].GetDouble() <= t);
                var alive = sample.ValueKind == JsonValueKind.Array && sample[1].ValueKind == JsonValueKind.Number;
                if (alive) cloud.Position = new(sample[1].GetSingle(), sample[2].GetSingle());
                cloud.IsDestroyed = !alive;
                if (alive && (cloud.Position - player.Position).Length() < closest) { closest = (cloud.Position - player.Position).Length(); closestAt = t; }
            }
            var breath = casts[0];
            if (!started && t >= breath.GetProperty("start").GetDouble())
            {
                started = true;
                engine.Cast(boss, new() { Action = new(48807), FinishAt = Epoch.AddSeconds(breath.GetProperty("finish").GetDouble()), LocXZ = boss.Position, Rotation = breath.GetProperty("rotation").GetSingle().Degrees() });
            }
            for (var i = 1; i < casts.Length; ++i)
                if (t >= casts[i].GetProperty("event").GetDouble() && done.Add(i))
                {
                    helper.Position = WPos.FromVec3(Vector(casts[i].GetProperty("origin")));
                    if (!player.Position.InCircle(helper.Position, 7))
                        failures.Add($"pulse {i} at {t:F2}s {(player.Position - helper.Position).Length():F1}y from the pocket");
                    engine.Effect(helper, new() { Action = new(48808), GlobalSequence = (uint)i });
                }
            if (t >= nextFrame)
            {
                nextFrame = t + .1;
                var watch = System.Diagnostics.Stopwatch.StartNew();
                engine.Update(); var frame = engine.Frame();
                output.Apply(engine, frame, 0, true, true, true);
                // The background route search delivers on a later frame, as in game.
                engine.Module!.FindComponent<CrucibleDragonWaves>()?.PendingRoute?.Wait();
                if (tick > 5) worstHints = Math.Max(worstHints, watch.Elapsed.TotalMilliseconds);
                if (frame.Hints.DirectRoute is { Count: > 0 } route)
                {
                    runtime.RequestAutomaticMovement(owner, true);
                    runtime.RequestDirectTraversal(owner, route[^1].ToVec3(), now, route.Select(r => r.ToVec3()).ToArray());
                    routing = true;
                }
                else if (routing)
                {
                    runtime.RequestDirectTraversal(owner, null, now); runtime.RequestAutomaticMovement(owner, false);
                    routing = false;
                }
                if (Environment.GetEnvironmentVariable("MP_H1_TRACE") == "1")
                    Console.WriteLine($"    t={t:F2} p=({p.X:F1},{p.Z:F1}) route={frame.Hints.DirectRoute?.Count} exec={runtime.MovementStatus.PhaseText}");
            }
            var snapshot = runtime.Manager.BuildSnapshot(now);
            ((MasterPlayer.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(p, 0, 6, move.IsRunning, false, 0, true, snapshot.Version), new(6, 6, 0, false, 6, 6), now);
            p = move.Advance(p, .02f, 6);
        }
        output.Clear();
        Console.WriteLine($"  dragon-waves: closest cloud {closest:F2}y at {closestAt:F2}s, worst hints+output {worstHints:F2}ms");
        if (closest < 2.4) failures.Add($"cloud came within {closest:F2}y");
        if (failures.Count > 0) throw new InvalidOperationException("dragon-waves: " + string.Join("; ", failures));
    }
}
