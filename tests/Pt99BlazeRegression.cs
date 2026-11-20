using System.Numerics;
using System.Reflection;
using System.Text.Json;
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
    }

    private static void Simulate(double latency)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-blaze-150614.json")));
        var checks = 0;
        var minimum = float.PositiveInfinity;
        foreach (var round in file.RootElement.GetProperty("rounds").EnumerateArray())
        {
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
            var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true,
                RuntimeHardSafetyMargin = .5f, RuntimeArenaSafetyMargin = 0 };
            using var manager = new SafeZoneManager(setting);
            manager.SetArenaBoundary(encounter.Boundary);
            var handles = new Dictionary<string, SafeZoneAreaHandle>();
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
            while (t <= 18)
            {
                var now = Start.AddSeconds(t);
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
                    }
                }
                if (t >= nextScan - .000001)
                {
                    var wanted = encounter.Collect(now, 100).Where(a => a.Key.StartsWith("blaze/")).ToArray();
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
                            handles[area.Key] = manager.AddDangerArea(area.Shape, binding, [area.Window]);
                        else manager.UpdateArea(handle, template: area.Shape, binding: binding,
                            dangerWindows: [area.Window], incrementVersion: false);
                    }
                    nextScan = t + .1;
                }
                if (pending != null && t >= pendingAt - .000001)
                {
                    latest = pending;
                    latestField.SetValue(movement, latest);
                    pending = null;
                }
                var snapshot = manager.BuildSnapshot(now);
                var state = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
                dispatch.Invoke(movement, [latest, state, snapshot, now]);
                latest = movement.LatestSolution;
                if (pending == null && t >= nextSolve - .000001)
                {
                    pending = solver.Solve(snapshot, state, new(6, 6, .08f, false, 6, 6),
                        new SolveOptions { HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, now, latest);
                    trace.Add($"t={t:F2} player={player} phase={movement.Phase} running={move.IsRunning} status={pending.Status} noMove={pending.IsNoMovement} "
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
        }
        Check(checks == 136, "Movement checks both recorded rounds at every actual effect time");
        Console.WriteLine($"PT99 blaze latency={latency:F2}: {checks} real effects, minimum physical clearance={minimum:F3}m");
    }

    private static void Replay(bool completeEffects)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-blaze-150614.json")));
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
                        "The actual 216890-damage stop must still be recognized as R5 danger");
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
        Check(checks == 136 && recordedHit, "Both complete ARR rounds and the actual hit must be checked");
    }

    private static Vector3 Vector(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
