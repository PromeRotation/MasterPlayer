using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Follow;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Pt99BlazeRegression
{
    private static readonly DateTime Start = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT99 recorded exaflares retain every real hit without zero-target effects", () => Replay(false));
        run("PT99 recorded exaflares reanchor both directions with complete effects", () => Replay(true));
        foreach (var latency in new[] { .08, .3 })
            run($"PT99 recorded exaflares actual movement without zero-target effects latency={latency:F2}", () => Simulate(latency));
        foreach (var complete in new[] { false, true })
            run($"PT99 Final Verse 215425 all 64 actual exaflares complete-effects={complete}",
                () => Replay(complete, "pt99-blaze-215425.json", 64));
        foreach (var latency in new[] { .08, .3 })
            run($"PT99 Final Verse 215425 actual movement latency={latency:F2}",
                () => Simulate(latency, "pt99-blaze-215425.json", 64));
        foreach (var complete in new[] { false, true })
        {
            run($"PT99 Final Verse 003315 second blaze model complete-effects={complete}",
                () => Replay(complete, "pt99-blaze-003315.json", 68));
            foreach (var latency in new[] { .08, .3 })
                run($"PT99 Final Verse 003315 second blaze handoff latency={latency:F2} complete-effects={complete}",
                    () => Simulate(latency, "pt99-blaze-003315.json", 68, .2f, complete));
        }
        for (var round = 0; round < 4; ++round)
        foreach (var complete in new[] { false, true })
        foreach (var latency in new[] { .08, .3 })
        {
            var selectedRound = round;
            run($"PT99 whole-round refuge 133056 round={round + 1} latency={latency:F2} complete-effects={complete}",
                () => Simulate(latency, "pt99-blaze-133056.json", 68, .2f, complete, true, true,
                    selectedRound, true, 1));
        }
        run("PT99 whole-round refuge retains colour deadlines, wrong-water obstacles and chain hold", RefugeConstraints);
        run("PT99 whole-round refuge rebuilds four rounds, respects margin and releases cancelled or expired rounds", RefugeLifecycle);
        for (var round = 0; round < 4; ++round)
        foreach (var latency in new[] { .08, .3 })
        {
            var selectedRound = round;
            run($"PT99 whole-round refuge colour acquisition round={round + 1} latency={latency:F2}",
                () => Simulate(latency, "pt99-blaze-133056.json", 68, .2f, true, true, true,
                    selectedRound, true, 1, true));
        }
        foreach (var complete in new[] { false, true })
        {
            run($"PT99 Final Verse 133056 all four blaze models complete-effects={complete}",
                () => Replay(complete, "pt99-blaze-133056.json", 272));
            foreach (var latency in new[] { .08, .3 })
            {
                run($"PT99 Final Verse 133056 all four blaze movement latency={latency:F2} complete-effects={complete}",
                    () => Simulate(latency, "pt99-blaze-133056.json", 272, .2f, complete));
                foreach (var masked in new[] { false, true })
                    run($"PT99 Final Verse 133056 fourth blaze follow continuation latency={latency:F2} masked={masked} complete-effects={complete}",
                        () => Simulate(latency, "pt99-blaze-133056.json", 68, .2f, complete, true, masked, 3));
            }
        }
    }

    private static void Simulate(double latency, string fixture = "pt99-blaze-150614.json", int expected = 136,
        float margin = .5f, bool completeEffects = false, bool followTarget = false, bool maskFollowDangers = true,
        int skipRounds = 0, bool wholeRoundRefuge = false, int roundCount = int.MaxValue, bool colourMechanic = false)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", fixture)));
        var checks = 0;
        var minimum = float.PositiveInfinity;
        foreach (var round in file.RootElement.GetProperty("rounds").EnumerateArray().Skip(skipRounds).Take(roundCount))
        {
            var handoff = round.TryGetProperty("handoff", out var recordedHandoff) ? recordedHandoff.GetDouble() : 0;
            var recordedMoves = round.TryGetProperty("recordedMoves", out var moves)
                ? moves.EnumerateArray().ToArray() : [];
            var encounter = new Pt99Encounter();
            var player = Vector(round.GetProperty("player"));
            player.Y = 0;
            var actors = new List<ReplayActorState>
            {
                new() { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new() { Id = 100, Kind = 1, Position = player, Hp = 1000, MaxHp = 1000 }
            };
            encounter.Synchronize(actors, Start);
            uint sequence = 10000;
            foreach (var crystal in round.GetProperty("crystals").EnumerateArray())
            {
                var now = Start.AddSeconds(crystal.GetProperty("created").GetDouble());
                encounter.Apply(new LogSystemActionEffectEvent { SourceId = 1,
                    ActionId = crystal.GetProperty("axis").GetUInt32(), GlobalSequence = sequence++ }, now);
                actors.Add(new ReplayActorState { Id = crystal.GetProperty("source").GetUInt64(),
                    Oid = 0x1EBE70, Position = Vector(crystal.GetProperty("position")) });
                encounter.Synchronize(actors, now);
            }
            if (colourMechanic)
            {
                encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4559, Duration = 180 }, Start);
                encounter.StartCast(new(1, 44088, encounter.Center, 0, Start, Start.AddSeconds(4), 100));
            }
            var colourAcquired = !colourMechanic;
            var currentColour = 4559u;
            var pendingColour = 0u;
            var waterSince = double.PositiveInfinity;
            var drainResolved = false;
            var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true,
                RuntimeHardSafetyMargin = margin, RuntimeArenaSafetyMargin = 0 };
            using var manager = new SafeZoneManager(setting);
            manager.SetArenaBoundary(encounter.Boundary);
            var handles = new Dictionary<string, SafeZoneAreaHandle>();
            var follow = default(SafeZoneAreaHandle);
            var followActive = false;
            var solver = new SafeZoneSolver();
            var move = new Pt10SimulatedMove();
            using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
            movement.Attach(move);
            var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var latestField = typeof(SafeZoneRebornMovementSystem).GetField("_latestSolution", BindingFlags.NonPublic | BindingFlags.Instance)!;
            SafeZoneSolution? latest = null;
            SafeZoneSolution? pending = null;
            var pendingAt = double.PositiveInfinity;
            var nextSolve = 0d;
            var nextScan = 0d;
            var index = 0;
            var t = 0d;
            var trace = new List<string>();
            var events = round.GetProperty("events").EnumerateArray().ToArray();
            var firstHit = events.First(e => e.GetProperty("kind").GetString() == "effect").GetProperty("t").GetDouble();
            var lastHit = events.Last(e => e.GetProperty("kind").GetString() == "effect").GetProperty("t").GetDouble();
            PtBossEncounter.Area? initialRefuge = null;
            var reachedRefugeAt = double.PositiveInfinity;
            while (t <= 18)
            {
                var now = Start.AddSeconds(t);
                if (colourMechanic)
                {
                    var water = PtArenaData.LightPuddles.Any(p => p.Contains(player)) ? 4560u
                        : PtArenaData.DarkPuddles.Any(p => p.Contains(player)) ? 4559u : 0;
                    if (water != pendingColour)
                    {
                        pendingColour = water;
                        waterSince = t;
                    }
                    if (water != 0 && water != currentColour && t >= waterSince + .2)
                    {
                        encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = currentColour }, now);
                        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = water, Duration = 180 }, now);
                        currentColour = water;
                        colourAcquired |= water == 4560;
                    }
                    if (t >= 3 && t < 4)
                        Check(colourAcquired && currentColour == 4560,
                            $"Colour must be acquired before the drain deadline and retained until resolution; t={t:F3}, player={player}");
                    if (t >= 4 && !drainResolved)
                    {
                        encounter.Apply(new LogSystemActionEffectEvent { SourceId = 1, ActionId = 44088, GlobalSequence = sequence++ }, now);
                        drainResolved = true;
                    }
                }
                var active = t >= handoff;
                if (!active)
                {
                    player = Vector(recordedMoves.Last(p => p.GetProperty("t").GetDouble() <= t).GetProperty("position"));
                    player.Y = 0;
                }
                Check(encounter.Boundary.Contains(player), "Exaflare movement remains in the physical 40x30m arena");
                while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t + .000001)
                {
                    var e = events[index++];
                    if (e.GetProperty("kind").GetString() == "cast")
                        encounter.StartCast(new(e.GetProperty("source").GetUInt64(), e.GetProperty("action").GetUInt32(),
                            Vector(e.GetProperty("position")), e.GetProperty("rotation").GetSingle(), now,
                            now.AddSeconds(e.GetProperty("seconds").GetDouble() + .3), 0));
                    else
                    {
                        ++checks;
                        var point = Vector(e.GetProperty("position"));
                        var clearance = Vector2.Distance(new(player.X, player.Z), new(point.X, point.Z)) - 5;
                        minimum = Math.Min(minimum, clearance);
                        Check(clearance > 0,
                            $"Exaflare hit at {t:F3}, latency={latency:F2}, player={player}, center={point}\n{string.Join("\n", trace.TakeLast(8))}");
                        if (handoff > 0 || followTarget)
                            Check(clearance > margin, $"Recorded handoff must preserve the requested {margin:F2}m danger margin at {t:F3}");
                        if (completeEffects)
                            encounter.Apply(new LogSystemActionEffectEvent {
                                SourceId = e.GetProperty("source").GetUInt64(), ActionId = e.GetProperty("action").GetUInt32(),
                                GlobalSequence = e.GetProperty("sequence").GetUInt32(), EffectPosition = point,
                                SourcePosition = Vector(e.GetProperty("sourcePosition")),
                                SourceRotation = e.GetProperty("rotation").GetSingle(), TargetIndex = -1 }, now);
                    }
                }
                if (t >= nextScan - .000001)
                {
                    var wanted = encounter.Collect(now, 100, margin).Where(a => !a.PreviewOnly
                        && (wholeRoundRefuge || a.Key.StartsWith("blaze/"))).ToArray();
                    if (wholeRoundRefuge && t < lastHit)
                    {
                        var refuge = wanted.SingleOrDefault(a => a.Key == "blaze-refuge")
                            ?? throw new InvalidOperationException("Observed crystal directions must provide a whole-round refuge before the first explosion");
                        initialRefuge ??= refuge;
                        Check(ReferenceEquals(initialRefuge.Shape, refuge.Shape),
                            "Consuming effects must not expand or replace the whole-round refuge");
                    }
                    var keys = wanted.Select(a => a.Key).ToHashSet();
                    foreach (var key in handles.Keys.Where(k => !keys.Contains(k)).ToArray())
                    {
                        manager.RemoveArea(handles[key]);
                        handles.Remove(key);
                    }
                    foreach (var area in wanted)
                    {
                        var binding = new StaticTransform(area.Position, area.Rotation);
                        if (!handles.TryGetValue(area.Key, out var handle))
                            handles[area.Key] = area.Obstacle ? manager.AddObstacle(area.Shape, binding)
                                : area.Safe ? manager.AddGoalArea(area.Shape, binding,
                                    [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)])
                                : manager.AddDangerArea(area.Shape, binding, [area.Window]);
                        else manager.UpdateArea(handle, template: area.Shape, binding: binding,
                            dangerWindows: !area.Safe && !area.Obstacle ? [area.Window] : null,
                            goalWindows: area.Safe ? [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)] : null,
                            incrementVersion: false);
                    }
                    nextScan = t + .1;
                }
                if (pending != null && t >= pendingAt - .000001)
                {
                    latest = pending;
                    latestField.SetValue(movement, latest);
                    pending = null;
                }
                if (followTarget)
                {
                    var target = file.RootElement.GetProperty("followTarget");
                    // In all four rounds stress the planner with a changing optional
                    // attack target. This is adversarial input, not recorded targeting.
                    var position = wholeRoundRefuge && t > 8
                        ? new Vector3(-600, 0, -285) : Vector(target.GetProperty("position"));
                    var radius = target.GetProperty("radius").GetSingle();
                    var beforeFollow = manager.BuildSnapshot(now);
                    GeometryTemplate template = maskFollowDangers
                        ? new DangerAwareFollowGoalTemplate(radius, beforeFollow.DangerAreas
                            .Where(a => a.DangerWindows.Any(w => OptionalGoalSafety.MasksFollowGoal(
                                w.DangerFrom, w.DangerUntil, now, a.Geometry)))
                            .Select(a => a.Geometry).Concat(beforeFollow.HardObstacles.Select(a => a.Geometry)),
                            beforeFollow.ArenaBoundary)
                        : new CircleTemplate(radius);
                    var enabled = template is not DangerAwareFollowGoalTemplate masked || masked.HasSafeArea(position);
                    // FollowController refreshes both the mask and GoalFrom every frame, even for a stationary target.
                    GoalWindow[] windows = enabled ? [new GoalWindow(now, null, -1, PositionPreference.Nearest)] : [];
                    if (!follow.IsValid)
                        follow = manager.AddGoalArea(template, new StaticTransform(position, 0), windows);
                    else
                        manager.UpdateArea(follow, template: template, goalWindows: windows,
                            incrementVersion: enabled != followActive);
                    followActive = enabled;
                }
                var snapshot = manager.BuildSnapshot(now);
                if (wholeRoundRefuge && initialRefuge != null)
                {
                    var inside = initialRefuge.Geometry.Contains(player);
                    if (inside && double.IsPositiveInfinity(reachedRefugeAt)) reachedRefugeAt = t;
                    if (t >= (colourMechanic ? lastHit - .05 : firstHit - .05) && t <= lastHit + .15)
                        Check(inside, $"Round {skipRounds + 1}: must reach and retain the whole-round refuge before first hit; t={t:F3}, player={player}\n{string.Join("\n", trace.TakeLast(5))}");
                }
                var state = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
                if (active) dispatch.Invoke(movement, [latest, state, snapshot, now]);
                latest = movement.LatestSolution;
                if (active && pending == null && t >= nextSolve - .000001)
                {
                    pending = solver.Solve(snapshot, state, new(6, 6, .08f, false, 6, 6),
                        new SolveOptions { HardSafetyMargin = margin, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, now, latest);
                    if (wholeRoundRefuge && !colourMechanic && trace.Count == 0)
                        Check(pending.IsSuccess && !pending.IsPartialEscape
                            && initialRefuge!.Geometry.Contains(pending.ExecutablePath.Count > 0 ? pending.ExecutablePath[^1].Position : player)
                            && !pending.ExecutablePath.Any(p => p.IsWaitPoint),
                            "Plan directly into the joint refuge rather than a temporary stopping point or waiting phase");
                    if (handoff > 0 && trace.Count == 0)
                        Check(pending.IsSuccess && !pending.IsPartialEscape && !pending.IsLeastDangerFallback
                            && pending.ContinuationPath.Count > 1,
                            "The recorded handoff must plan a real departure beyond its first refuge, not just a later-dangerous stopping point");
                    trace.Add($"t={t:F2} player={player} phase={movement.Phase} running={move.IsRunning} status={pending.Status} noMove={pending.IsNoMovement} "
                        + $"partial={pending.IsPartialEscape} suffix={pending.ContinuationPath.Count} "
                        + string.Join(";", pending.ExecutablePath.Select(p => p.Position)));
                    pendingAt = t + latency;
                    nextSolve = t + .2;
                }
                var nextEvent = index < events.Length ? events[index].GetProperty("t").GetDouble() : double.PositiveInfinity;
                var dt = Math.Min(.02, nextEvent - t);
                Check(dt > .0000001, "Simulation advances beyond each event");
                player = move.Advance(player, (float)dt);
                t += dt;
            }
            if (wholeRoundRefuge)
            {
                Check(reachedRefugeAt < (colourMechanic ? lastHit : firstHit),
                    "Reach the joint refuge before the first explosion, or return after the higher-priority colour deadline");
                Check(!encounter.Collect(Start.AddSeconds(30), 100).Any(a => a.Key == "blaze-refuge"),
                    "Round completion releases the refuge and allows optional approach to resume");
                Console.WriteLine($"PT99 whole-round refuge round={skipRounds + 1}: reached at {reachedRefugeAt:F3}s before first hit {firstHit:F3}s");
            }
        }
        Check(checks == expected, "Movement checks every recorded actual effect time");
        Console.WriteLine($"PT99 blaze latency={latency:F2}: {checks} real effects, minimum physical clearance={minimum:F3}m");
    }

    private static void RefugeConstraints()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-blaze-133056.json")));
        var round = file.RootElement.GetProperty("rounds")[0];
        var encounter = new Pt99Encounter();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
            new() { Id = 2, Oid = 0x48EB, Position = new(-600, 0, -315), InCombat = true, Hp = 1000, MaxHp = 1000 },
            new() { Id = 100, Kind = 1, Position = encounter.Center, Hp = 1000,
                Statuses = [new(4559, 0, 0, Start.AddSeconds(180))] }
        };
        encounter.Synchronize(actors, Start);
        uint sequence = 10000;
        foreach (var crystal in round.GetProperty("crystals").EnumerateArray())
        {
            var at = Start.AddSeconds(crystal.GetProperty("created").GetDouble());
            encounter.Apply(new LogSystemActionEffectEvent { SourceId = 1,
                ActionId = crystal.GetProperty("axis").GetUInt32(), GlobalSequence = sequence++ }, at);
            actors.Add(new ReplayActorState { Id = crystal.GetProperty("source").GetUInt64(),
                Oid = 0x1EBE70, Position = Vector(crystal.GetProperty("position")) });
            encounter.Synchronize(actors, at);
        }
        foreach (var e in round.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "cast"))
            encounter.StartCast(new(e.GetProperty("source").GetUInt64(), 44079,
                Vector(e.GetProperty("position")), 0, Start, Start.AddSeconds(7), 0));
        encounter.StartCast(new(1, 44088, encounter.Center, 0, Start, Start.AddSeconds(6), 100));
        var areas = encounter.Collect(Start, 100);
        var refuge = areas.Single(a => a.Key == "blaze-refuge");
        Check(areas.Single(a => a.Key == "color/goal").Priority > refuge.Priority,
            "Light acquisition must outrank the whole-round refuge");
        Check(areas.Any(a => a.Key == "color/deadline" && !a.Safe)
            && areas.Where(a => a.Key.StartsWith("color/avoid/")).All(a => a.Obstacle)
            && areas.Count(a => a.Key.StartsWith("color/avoid/")) == PtArenaData.DarkPuddles.Length,
            "The colour deadline and all wrong-colour water obstacles remain registered");
        var from = new Vector3(-600, 0, -300);
        using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
        manager.SetArenaBoundary(encounter.Boundary);
        foreach (var area in areas.Where(a => !a.PreviewOnly))
        {
            var binding = new StaticTransform(area.Position, area.Rotation);
            if (area.Obstacle) manager.AddObstacle(area.Shape, binding);
            else if (area.Safe) manager.AddGoalArea(area.Shape, binding,
                [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)]);
            else manager.AddDangerArea(area.Shape, binding, [area.Window]);
        }
        var snapshot = manager.BuildSnapshot(Start);
        var solution = new SafeZoneSolver().Solve(snapshot, new(from, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, .08f, false, 6, 6), new SolveOptions { HardSafetyMargin = .5f,
                ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, Start);
        Check(solution.IsSuccess && PtArenaData.LightPuddles.Any(p => p.Contains(solution.ExecutablePath[^1].Position)),
            $"The real solver must choose the colour goal before the lower-priority refuge: {solution.Status}");
        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4562, Duration = 10 }, Start);
        Check(encounter.HoldMovement(Start, 100), "Chains still hold movement during the refuge strategy");
        encounter.Reset();
        Check(!encounter.Collect(Start, 100).Any(a => a.Key == "blaze-refuge"),
            "Reset releases the whole-round strategy");
    }

    private static void RefugeLifecycle()
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-blaze-133056.json")));
        var encounter = new Pt99Encounter();
        var actors = new List<ReplayActorState>
        {
            new() { Id = 1, Oid = 0x48EA, InCombat = true, Hp = 1000, MaxHp = 1000 },
            new() { Id = 100, Kind = 1, Hp = 1000 }
        };
        Vector3[] safePoints = [new(-617.9f, 0, -312.9f), new(-590.2f, 0, -312.9f),
            new(-614, 0, -287.2f), new(-590, 0, -287.2f)];
        var index = 0;
        uint sequence = 20000;
        foreach (var round in file.RootElement.GetProperty("rounds").EnumerateArray())
        {
            var start = Start.AddSeconds(index * 60);
            var offset = (ulong)(index * 1000);
            encounter.Synchronize(actors, start.AddSeconds(-30));
            foreach (var crystal in round.GetProperty("crystals").EnumerateArray())
            {
                var at = start.AddSeconds(crystal.GetProperty("created").GetDouble());
                encounter.Apply(new LogSystemActionEffectEvent { SourceId = 1,
                    ActionId = crystal.GetProperty("axis").GetUInt32(), GlobalSequence = sequence++ }, at);
                actors.Add(new ReplayActorState { Id = offset + crystal.GetProperty("source").GetUInt64(),
                    Oid = 0x1EBE70, Position = Vector(crystal.GetProperty("position")) });
                encounter.Synchronize(actors, at);
            }
            var casts = round.GetProperty("events").EnumerateArray()
                .Where(e => e.GetProperty("kind").GetString() == "cast").ToArray();
            foreach (var e in casts)
                encounter.StartCast(new(offset + e.GetProperty("source").GetUInt64(), 44079,
                    Vector(e.GetProperty("position")), 0, start, start.AddSeconds(7), 0));
            var narrow = encounter.Collect(start, 100, 0).Single(a => a.Key == "blaze-refuge");
            var wide = encounter.Collect(start, 100, 2).Single(a => a.Key == "blaze-refuge");
            Check(!ReferenceEquals(narrow.Shape, wide.Shape) && wide.Geometry.Contains(safePoints[index]),
                $"Round {index + 1}: rebuild the new footprint and honour the configured margin");
            foreach (var e in round.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "effect"))
                Check(!wide.Geometry.Contains(Vector(e.GetProperty("position"))),
                    "Every observed explosion center is excluded from the joint refuge");
            Check(!encounter.Collect(start.AddSeconds(30), 100).Any(a => a.Key == "blaze-refuge"),
                "Missing all zero-target effects must not retain the goal beyond expiry");
            if (index == 3)
            {
                foreach (var e in casts) encounter.Cancel(offset + e.GetProperty("source").GetUInt64());
                Check(!encounter.Collect(start.AddSeconds(1), 100).Any(a => a.Key == "blaze-refuge"),
                    "Cancelling this round releases the refuge even when previous rounds had no effects");
            }
            ++index;
        }
    }

    private static void Replay(bool completeEffects, string fixture = "pt99-blaze-150614.json", int expected = 136)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", fixture)));
        var checks = 0;
        var recordedHit = false;
        foreach (var round in file.RootElement.GetProperty("rounds").EnumerateArray())
        {
            var encounter = new Pt99Encounter();
            var actors = new List<ReplayActorState>
            {
                new() { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new() { Id = 100, Kind = 1, Position = encounter.Center, Hp = 1000, MaxHp = 1000 }
            };
            encounter.Synchronize(actors, Start);
            uint sequence = 10000;
            foreach (var crystal in round.GetProperty("crystals").EnumerateArray())
            {
                var time = Start.AddSeconds(crystal.GetProperty("created").GetDouble());
                encounter.Apply(new LogSystemActionEffectEvent { SourceId = 1,
                    ActionId = crystal.GetProperty("axis").GetUInt32(), GlobalSequence = sequence++ }, time);
                actors.Add(new ReplayActorState { Id = crystal.GetProperty("source").GetUInt64(),
                    Oid = 0x1EBE70, Position = Vector(crystal.GetProperty("position")) });
                encounter.Synchronize(actors, time);
            }
            foreach (var e in round.GetProperty("events").EnumerateArray())
            {
                var now = Start.AddSeconds(e.GetProperty("t").GetDouble());
                var source = e.GetProperty("source").GetUInt64();
                var action = e.GetProperty("action").GetUInt32();
                var position = Vector(e.GetProperty("position"));
                var rotation = e.GetProperty("rotation").GetSingle();
                if (e.GetProperty("kind").GetString() == "cast")
                {
                    encounter.StartCast(new(source, action, position, rotation, now,
                        now.AddSeconds(e.GetProperty("seconds").GetDouble() + .3), 0,
                        Vector(e.GetProperty("sourcePosition"))));
                    continue;
                }
                var candidates = encounter.Collect(now, 100).Where(a => a.Key.StartsWith("blaze/")
                    && Vector3.Distance(a.Position, position) < .1f).ToArray();
                Check(candidates.Any(a => a.Window.IsActiveAt(now)),
                    $"Real {action} at {e.GetProperty("t")} must remain dangerous at {position}; complete={completeEffects}");
                if (e.GetProperty("hit").GetBoolean())
                {
                    recordedHit = true;
                    var player = Vector(e.GetProperty("recordedPlayer"));
                    Check(candidates.Any(a => a.Window.IsActiveAt(now) && a.Geometry.Contains(player)),
                        "The recorded hit position must still be recognized as R5 danger");
                }
                ++checks;
                if (completeEffects)
                {
                    var effect = new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
                        GlobalSequence = e.GetProperty("sequence").GetUInt32(), EffectPosition = position,
                        SourcePosition = Vector(e.GetProperty("sourcePosition")), SourceRotation = rotation, TargetIndex = -1 };
                    encounter.Apply(effect, now);
                    encounter.Apply(effect, now);
                }
            }
            Check(!encounter.Collect(Start.AddSeconds(30), 100).Any(a => a.Key.StartsWith("blaze/")),
                "Exaflares expire with or without effect delivery");
            encounter.Reset();
            Check(encounter.Collect(Start.AddSeconds(30), 100).Count == 10,
                "Reset removes exaflares and preserves only puddle previews");
        }
        Check(checks == expected && recordedHit, "All recorded effects and the actual hit must be checked");
    }

    private static Vector3 Vector(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
