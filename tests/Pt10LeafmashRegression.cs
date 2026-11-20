using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using PromeRotation.LogSystem;

internal static class Pt10LeafmashRegression
{
    private static readonly DateTime Start = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT10 latest ARR zero-position effects retain every future jump", RecordedSequence);
        run("PT10 same-source casts missing predictions duplicates and cancellation", Identity);
        foreach (var latency in new[] { .08, .3 })
            run($"PT10 latest ARR full four-jump movement latency={latency:F2}", () => Simulate(latency));
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse", "pt10-leafmash-114707.json")));
    private static Vector3 Vector(JsonElement p) => new(p[0].GetSingle(), 0, p[2].GetSingle());
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private static Pt10Encounter Encounter(Vector3 player)
    {
        var encounter = new Pt10Encounter();
        Sync(encounter, player, Start);
        return encounter;
    }

    private static void Sync(Pt10Encounter encounter, Vector3 player, DateTime now)
        => encounter.Synchronize([
            new ReplayActorState { Id = 1, Oid = 0x460D, Position = encounter.Center, InCombat = true, Hp = 1000 },
            new ReplayActorState { Id = 100, Kind = 1, Position = player, Hp = 1000 }], now);

    private static void Apply(Pt10Encounter encounter, JsonElement e, DateTime origin)
    {
        var now = origin.AddSeconds(e.GetProperty("t").GetDouble());
        var action = e.GetProperty("action").GetUInt32();
        var source = e.GetProperty("source").GetUInt64();
        if (e.GetProperty("kind").GetString() == "cast")
            encounter.StartCast(new(source, action, Vector(e.GetProperty("position")), e.GetProperty("rotation").GetSingle(), now,
                now.AddSeconds(e.GetProperty("seconds").GetDouble() + .3), source, Vector(e.GetProperty("sourcePosition"))));
        else
            encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
                GlobalSequence = e.GetProperty("sequence").GetUInt32(), TargetIndex = 0,
                EffectPosition = Vector(e.GetProperty("position")), SourcePosition = Vector(e.GetProperty("sourcePosition")) }, now);
    }

    private static bool IsJump(PtBossEncounter.Area a) => a.Key.StartsWith("leaf/", StringComparison.Ordinal) || a.Action == 44057;

    private static void RecordedSequence()
    {
        using var fixture = Fixture();
        var encounter = Encounter(Vector3.Zero);
        var checks = 0;
        var recordedHits = 0;
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var origin = Start.AddSeconds(cycle.GetProperty("start").GetDouble());
            var events = cycle.GetProperty("events").EnumerateArray().ToArray();
            var effects = events.Where(e => e.GetProperty("action").GetUInt32() == 44057 && e.GetProperty("kind").GetString() == "effect").ToArray();
            var index = 0;
            foreach (var e in events)
            {
                var now = origin.AddSeconds(e.GetProperty("t").GetDouble());
                var isHit = e.GetProperty("action").GetUInt32() == 44057 && e.GetProperty("kind").GetString() == "effect";
                if (isHit)
                {
                    var active = encounter.Collect(now, 100).Where(IsJump).ToArray();
                    Check(active.Any(a => a.Owner == e.GetProperty("source").GetUInt64() && a.Action == 44057
                        && Distance(a.Position, Vector(e.GetProperty("sourcePosition"))) < .1f && a.From <= now && a.Until >= now),
                        $"Jump {index + 1} at ARR {cycle.GetProperty("start").GetDouble() + e.GetProperty("t").GetDouble():F3} is missing at its real hit.");
                    if (e.GetProperty("hit").GetBoolean())
                    {
                        ++recordedHits;
                        Check(active.Any(a => a.Geometry.Contains(Vector(e.GetProperty("player")))), "Recorded hit position was incorrectly safe.");
                    }
                }
                Apply(encounter, e, origin);
                if (!isHit) continue;
                ++index;
                ++checks;
                var remaining = encounter.Collect(now.AddSeconds(.4), 100).Where(IsJump).ToArray();
                Check(remaining.Length == 4 - index, $"After jump {index}, expected {4 - index} future circles, got {remaining.Length}.");
                foreach (var future in effects.Skip(index))
                    Check(remaining.Any(a => Distance(a.Position, Vector(future.GetProperty("sourcePosition"))) < .1f), "A future circle was consumed by a zero-position effect.");
                Check(remaining.All(a => a.Shape is CircleTemplate { Radius: 15 } && !a.PreviewOnly), "Physical R15 navigation was altered.");
                if (remaining.Length > 0) Check(remaining.Count(a => !a.NavigationOnly) == 1, "Preview must show exactly the next jump.");
                Apply(encounter, e, origin);
                Check(encounter.Collect(now.AddSeconds(.4), 100).Where(IsJump).SequenceEqual(remaining), "Duplicate effect consumed another jump.");
            }
        }
        Check(checks == 20 && recordedHits == 3, "Fixture must cover all five real cycles and three original hits.");
        Check(!encounter.Collect(Start.AddHours(1), 100).Any(), "Completed encounter retained navigation output.");
    }

    private static void Identity()
    {
        var encounter = Encounter(Vector3.Zero);
        var p = encounter.Center;
        void Cast(ulong source, Vector3 position, double seconds)
            => encounter.StartCast(new(source, 44057, position, 0, Start, Start.AddSeconds(seconds), source));
        void Effect(ulong source, uint sequence, double seconds)
            => encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = 44057, GlobalSequence = sequence,
                EffectPosition = Vector3.Zero, SourcePosition = Vector3.One }, Start.AddSeconds(seconds));
        Cast(10, p, 5);
        Cast(10, p + new Vector3(8, 0, 0), 7);
        Check(encounter.Collect(Start, 100).Count == 2, "Same-source overlapping observations overwrote a future jump without telegraphs.");
        Effect(99, 1, 5);
        Check(encounter.Collect(Start, 100).Count == 2, "Unrelated source consumed a jump.");
        Effect(10, 2, 5);
        var next = encounter.Collect(Start.AddSeconds(5.4), 100).Single();
        Check(next.Position == p + new Vector3(8, 0, 0), "Zero/stale positions selected the wrong same-source jump.");
        Effect(10, 2, 6);
        Check(encounter.Collect(Start.AddSeconds(5.4), 100).Count == 1, "Late duplicate consumed the second jump.");
        encounter.Cancel(99);
        Check(encounter.Collect(Start.AddSeconds(5.4), 100).Count == 1, "Other source cancellation removed a jump.");
        encounter.Cancel(10);
        Check(encounter.Collect(Start, 100).Count == 0, "Cancelled jump remained.");
        encounter.Reset();
        Check(encounter.Collect(Start, 100).Count == 0, "Reset left jump state.");
    }

    private static void Simulate(double latency)
    {
        using var fixture = Fixture();
        var total = 0;
        var minimum = float.PositiveInfinity;
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var events = cycle.GetProperty("events").EnumerateArray().ToArray();
            var player = Vector(cycle.GetProperty("initialPlayer"));
            var encounter = Encounter(player);
            var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true };
            using var manager = new SafeZoneManager(setting);
            manager.SetArenaBoundary(encounter.Boundary);
            var registered = new Dictionary<string, SafeZoneAreaHandle>();
            var options = new SolveOptions { HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true };
            var solver = new SafeZoneSolver();
            var move = new Pt10SimulatedMove();
            using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
            movement.Attach(move);
            var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var latestField = typeof(SafeZoneRebornMovementSystem).GetField("_latestSolution", BindingFlags.NonPublic | BindingFlags.Instance)!;
            SafeZoneSolution? latest = null;
            SafeZoneSolution? pending = null;
            var ready = double.PositiveInfinity;
            var nextSolve = 0d;
            var nextScan = 0d;
            var index = 0;
            var checks = 0;
            var t = 0d;
            var duration = events[^1].GetProperty("t").GetDouble() + .5;
            var trace = new List<object>();
            var hits = new List<object>();
            var failures = new List<string>();
            while (t <= duration + .000001)
            {
                var now = Start.AddSeconds(t);
                if (t >= nextScan - .000001)
                {
                    Sync(encounter, player, now);
                    nextScan = t + .1;
                }
                while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t + .000001)
                {
                    var e = events[index++];
                    if (e.GetProperty("kind").GetString() == "effect" && e.GetProperty("action").GetUInt32() == 44057)
                    {
                        var clearance = Distance(player, Vector(e.GetProperty("sourcePosition"))) - 15;
                        minimum = Math.Min(minimum, clearance);
                        ++checks;
                        ++total;
                        hits.Add(new { t, clearance, position = new[] { player.X, player.Z } });
                        if (clearance <= 0) failures.Add($"Jump {checks} hit at cycle {cycle.GetProperty("start")}+{t:F3}s latency={latency:F2}, clearance={clearance:F3}, player={player}.");
                    }
                    Apply(encounter, e, Start);
                }
                Check(encounter.Boundary.Contains(player), $"Jump route left the real R19.5 arena at {t:F3}s.");
                if (pending != null && t >= ready - .000001)
                {
                    latest = pending;
                    latestField.SetValue(movement, latest);
                    pending = null;
                }
                Publish(now);
                var snapshot = manager.BuildSnapshot(now);
                var state = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
                dispatch.Invoke(movement, [latest, state, snapshot, now]);
                latest = movement.LatestSolution;
                if (pending == null && t >= nextSolve - .000001)
                {
                    pending = solver.Solve(snapshot, state, new(6, 6, .08f, false, 6, 6), options, now, latest);
                    ready = t + latency;
                    nextSolve = t + .1;
                }
                trace.Add(new { t, position = new[] { player.X, player.Z }, move.IsRunning,
                    dangers = encounter.Collect(now, 100).Count, status = latest?.Status.ToString() });
                if (t >= duration - .000001) break;
                var next = Math.Min(t + .02, duration);
                if (index < events.Length) next = Math.Min(next, events[index].GetProperty("t").GetDouble());
                player = move.Advance(player, (float)(next - t));
                t = next;
            }
            if (Environment.GetEnvironmentVariable("PT10_SIM_OUTPUT") is { } output)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, $"jumps-{cycle.GetProperty("start")}-{latency:F2}.json"),
                    JsonSerializer.Serialize(new { latency, checks, minimum, failures, hits, trace }, new JsonSerializerOptions { WriteIndented = true }));
            }
            Check(checks == 4, "Simulation did not execute the complete four-jump sequence.");
            Check(failures.Count == 0, string.Join("\n", failures));

            void Publish(DateTime now)
            {
                var wanted = encounter.Collect(now, 100).Where(a => !a.PreviewOnly).ToArray();
                var keys = wanted.Select(a => a.Key).ToHashSet();
                foreach (var key in registered.Keys.Where(k => !keys.Contains(k)).ToArray())
                { manager.RemoveArea(registered[key]); registered.Remove(key); }
                foreach (var a in wanted)
                {
                    var binding = new StaticTransform(a.Position, a.Rotation);
                    if (!registered.TryGetValue(a.Key, out var handle) || !manager.ContainsArea(handle))
                        registered[a.Key] = manager.AddDangerArea(a.Shape, binding, [a.Window]);
                    else manager.UpdateArea(handle, template: a.Shape, binding: binding, dangerWindows: [a.Window], incrementVersion: false);
                }
            }
        }
        Console.WriteLine($"PT10 full cycles latency={latency:F2}: real hits checked={total}/20, minimum physical clearance={minimum:F3}m.");
        Check(total == 20, "Simulation must cover all five recorded cycles.");
    }
}
