using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Pt50ChaseRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT50 recorded first miss and hit victims preserve actual chase origins", RecordedOrigins);
        run("PT50 marked player escapes the next chase rather than its current preview", ChaseEscapes);
        run("PT50 chase cancellation duplicate targets and source ownership", Lifecycle);
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse", "pt50-chase-155404.json")));
    private static Vector3 Vector(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
    private static ReplayActorState Actor(ulong id, Vector3 position) => new()
    {
        Id = id, EntityId = (uint)id, Oid = id == 1 ? 0x4872u : 0, Kind = (byte)(id == 1 ? 2 : 1),
        Position = position, InCombat = true, Hp = 1000, MaxHp = 1000,
    };
    private static void Sync(Pt50Encounter encounter, Vector3 boss, Vector3 player, DateTime now)
        => encounter.Synchronize([Actor(1, boss), Actor(100, player)], now);
    private static LogSystemActionEffectEvent Effect(JsonElement ev) => new()
    {
        SourceId = 1, ActionId = ev.GetProperty("action").GetUInt32(), GlobalSequence = ev.GetProperty("sequence").GetUInt32(),
        EffectPosition = Vector(ev.GetProperty("position")), SourcePosition = Vector(ev.GetProperty("sourcePosition")),
        TargetId = ev.GetProperty("target").GetUInt64(),
        TargetPosition = ev.GetProperty("targetPosition").ValueKind == JsonValueKind.Null ? null : Vector(ev.GetProperty("targetPosition")),
    };

    private static void RecordedOrigins()
    {
        using var fixture = Fixture();
        var hits = 0;
        var misses = 0;
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var encounter = new Pt50Encounter();
            var boss = Vector(cycle.GetProperty("initial").GetProperty("boss"));
            var player = Vector(cycle.GetProperty("initial").GetProperty("player"));
            Sync(encounter, boss, player, Start);
            encounter.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 640 }, Start);
            var remaining = 4;
            foreach (var ev in cycle.GetProperty("events").EnumerateArray())
            {
                var now = Start.AddSeconds(ev.GetProperty("t").GetDouble());
                switch (ev.GetProperty("kind").GetString())
                {
                    case "frame":
                        if (ev.GetProperty("actor").GetInt32() == 1) boss = Vector(ev.GetProperty("position"));
                        else player = Vector(ev.GetProperty("position"));
                        Sync(encounter, boss, player, now);
                        break;
                    case "cast":
                        encounter.StartCast(new(1, 43534, Vector(ev.GetProperty("position")), 0, now,
                            now.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), 1));
                        break;
                    case "effect":
                        var effect = Effect(ev);
                        encounter.Apply(effect, now);
                        encounter.Apply(effect with { TargetIndex = 1, TargetPosition = player + Vector3.One }, now);
                        --remaining;
                        if (ev.GetProperty("hitPlayer").GetBoolean()) ++hits; else ++misses;
                        if (remaining == 0)
                        {
                            Check(!encounter.Collect(now, 100).Any(a => a.Key.StartsWith("pit/")), "Fourth real hit must clear all chase output.");
                            break;
                        }
                        var origin = effect.ActionId == 43534 ? effect.SourcePosition!.Value : effect.EffectPosition!.Value;
                        var offset = player - origin;
                        offset.Y = 0;
                        var predicted = origin + (offset.LengthSquared() > 25 ? Vector3.Normalize(offset) * 5 : offset);
                        var preview = encounter.Collect(now, 100).Single(a => a.Key == "pit/chase");
                        Check(Vector2.Distance(new(preview.Position.X, preview.Position.Z), new(predicted.X, predicted.Z)) < .001f,
                            $"Cycle {cycle.GetProperty("castTime")} Action{effect.ActionId}: next circle must advance from the actual ground origin, never a hit victim or zero; got {preview.Position}, expected {predicted}.");
                        Check(preview.Shape is CircleTemplate { Radius: 6 }, "Physical chase footprint must remain R6.");
                        Check(preview.Name.Contains($"{remaining}/4"), "Duplicate target advanced chase twice.");
                    break;
                }
            }
        }
        Check(hits == 6 && misses == 6, $"Fixture must cover all twelve effects, including six real hits and six misses; got {hits}/{misses}.");
    }

    private static void ChaseEscapes()
    {
        using var fixture = Fixture();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        foreach (var latency in new[] { .08, .3 })
        {
            var encounter = new Pt50Encounter();
            var first = cycle.GetProperty("events").EnumerateArray().First(e => e.GetProperty("kind").GetString() == "cast");
            var origin = Vector(first.GetProperty("position"));
            var player = origin;
            Sync(encounter, origin, player, Start);
            encounter.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 640 }, Start);
            var arenaState = cycle.GetProperty("arenaState").GetUInt32();
            if (arenaState != 0)
                encounter.Apply(new LogSystemMapEffectEvent { Position = 1, Param1 = (ushort)arenaState, Param2 = (ushort)(arenaState >> 16) }, Start);
            encounter.StartCast(new(1, 43534, origin, 0, Start, Start.AddSeconds(first.GetProperty("seconds").GetDouble() + .3), 1));
            var now = Start;
            var count = 0;
            foreach (var ev in cycle.GetProperty("events").EnumerateArray().Where(e => e.GetProperty("kind").GetString() == "effect"))
            {
                using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
                manager.SetArenaBoundary(encounter.Boundary);
                var own = encounter.Collect(now, 100);
                if (count > 0)
                {
                    Check(own.Single(a => a.Key == "pit/chase").PreviewOnly, "Moving R6 preview must not compete with the self-chase navigation constraint.");
                    var escape = own.Single(a => a.Key == "pit/escape");
                    Check(escape.NavigationOnly && escape.Shape is CircleTemplate { Radius: 11 }, "Self chase needs 5m pursuit + real 6m blast, before the configured margin.");
                    var other = encounter.Collect(now, 101);
                    Check(!other.Any(a => a.Key == "pit/escape") && !other.Single(a => a.Key == "pit/chase").PreviewOnly,
                        "Changing player perspective must leave other players with the actual moving R6 hazard.");
                }
                foreach (var area in own.Where(a => !a.PreviewOnly))
                {
                    if (area.Obstacle) manager.AddObstacle(area.Shape, new StaticTransform(area.Position, area.Rotation));
                    else manager.AddDangerArea(area.Shape, new StaticTransform(area.Position, area.Rotation), [area.Window]);
                }
                var solveAt = now.AddSeconds(latency + .08);
                var snapshot = manager.BuildSnapshot(solveAt);
                var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, solveAt);
                Check(solution.IsSuccess && !solution.IsLeastDangerFallback, $"Cycle {cycle.GetProperty("castTime")} hit {count + 1}, latency {latency}: {solution.DescribeOutcome()}");
                var path = solution.ExecutablePath;
                var hitAt = Start.AddSeconds(ev.GetProperty("t").GetDouble());
                for (var at = solveAt; at <= hitAt; at = at.AddSeconds(.05))
                {
                    var point = path.Count == 0 ? player : PositionAt(path, at);
                    Check(encounter.Boundary.Contains(point), "Chase route left the physical arena.");
                    Check(!own.Any(a => a.Obstacle && a.Geometry.Contains(point)), "Chase route crossed the center hole.");
                }
                if (path.Count > 0) player = PositionAt(path, hitAt);
                if (count > 0)
                {
                    var delta = player - origin;
                    delta.Y = 0;
                    origin += delta.LengthSquared() > 25 ? Vector3.Normalize(delta) * 5 : delta;
                }
                Check(Vector2.Distance(new(player.X, player.Z), new(origin.X, origin.Z)) > 6,
                    $"Simulated player was hit by chase {count + 1}; latency {latency}, player={player}, blast={origin}.");
                Sync(encounter, origin, player, hitAt);
                encounter.Apply(new LogSystemActionEffectEvent
                {
                    SourceId = 1, ActionId = ev.GetProperty("action").GetUInt32(), GlobalSequence = ev.GetProperty("sequence").GetUInt32(),
                    EffectPosition = count == 0 ? Vector3.Zero : origin, SourcePosition = origin, TargetId = 0,
                }, hitAt);
                now = hitAt;
                ++count;
            }
            Check(count == 4 && !encounter.Collect(now, 100).Any(a => a.Key.StartsWith("pit/")), "Simulation must complete four chases and release output.");
        }
    }

    private static Vector3 PositionAt(IReadOnlyList<TimedWaypoint> path, DateTime at)
    {
        for (var i = 0; i < path.Count; ++i)
        {
            if (at <= path[i].PlannedDepartureTime || i == path.Count - 1) return path[i].Position;
            var next = path[i + 1];
            if (at < next.PlannedArrivalTime)
                return Vector3.Lerp(path[i].Position, next.Position,
                    (float)((at - path[i].PlannedDepartureTime) / (next.PlannedArrivalTime - path[i].PlannedDepartureTime)));
        }
        throw new InvalidOperationException("Empty movement path.");
    }

    private static void Lifecycle()
    {
        var e = new Pt50Encounter();
        var center = new Vector3(-300, 0, -300);
        Sync(e, center, center, Start);
        e.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 640 }, Start);
        Check(!e.Collect(Start, 100).Any(), "Icon alone must not start a chase.");
        e.StartCast(new(1, 43534, center, 0, Start, Start.AddSeconds(3), 1));
        e.Apply(new LogSystemActionEffectEvent
        {
            SourceId = 1, ActionId = 43534, GlobalSequence = 10, TargetId = 100,
            SourcePosition = center, TargetPosition = center + Vector3.UnitX, EffectPosition = Vector3.Zero,
        }, Start.AddSeconds(3));
        Check(e.Collect(Start.AddSeconds(3), 100).Single(a => a.Key == "pit/escape").Position == center,
            "A first hit with a damage recipient is still centered on the caster.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 999, ActionId = 43536, GlobalSequence = 1 }, Start.AddSeconds(3));
        Check(e.Collect(Start.AddSeconds(3), 100).Any(a => a.Key == "pit/chase"), "Unrelated source must not finish this chase.");
        e.Cancel(1);
        Check(!e.Collect(Start.AddSeconds(3), 100).Any(a => a.Key.StartsWith("pit/")), "Cancel must remove chase output.");
        e.Apply(new LogSystemActionEffectEvent { SourceId = 1, ActionId = 43535, GlobalSequence = 2 }, Start.AddSeconds(5.1));
        Check(!e.Collect(Start.AddSeconds(5.1), 100).Any(), "Late effect must not resurrect a canceled chase.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
