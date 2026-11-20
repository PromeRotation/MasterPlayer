using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Pt60SpinRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT60 recorded first pulse covers the real hit time", FirstHit);
        run("PT60 recorded rotation previews only the next pair and consumes all pulses", Sequence);
        run("PT60 recorded rotation escapes every real pulse with solver latency", Simulate);
        run("PT60 rotating effects reject other origins and missing poses", OriginIdentity);
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse", "pt60-spin-214526.json")));
    private static Vector3 Vector(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static Pt60Encounter Encounter(JsonElement cycle, bool clockwise = false)
    {
        var encounter = new Pt60Encounter();
        encounter.Synchronize([new ReplayActorState { Id = 1, Oid = 0x49DE, InCombat = true }], Start);
        encounter.StartCast(new(1, clockwise ? 44868u : cycle.GetProperty("action").GetUInt32(), Vector(cycle.GetProperty("position")),
            cycle.GetProperty("rotation").GetSingle(), Start, Start.AddSeconds(cycle.GetProperty("seconds").GetDouble() + .3), 1));
        return encounter;
    }

    private static void Apply(Pt60Encounter encounter, JsonElement effect, bool clockwise = false, float initialRotation = 0)
    {
        var rotation = effect.GetProperty("rotation").GetSingle();
        if (clockwise) rotation = 2 * initialRotation - rotation;
        encounter.Apply(new LogSystemActionEffectEvent
        {
            SourceId = effect.GetProperty("source").GetUInt64(), ActionId = 44909,
            GlobalSequence = effect.GetProperty("sequence").GetUInt32(), SourceRotation = rotation,
            SourcePosition = Vector(effect.GetProperty("position")), TargetIndex = 0,
        }, Start.AddSeconds(effect.GetProperty("t").GetDouble()));
    }

    private static void FirstHit()
    {
        using var fixture = Fixture();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var encounter = Encounter(cycle);
            var hit = cycle.GetProperty("effects").EnumerateArray().First(e => e.GetProperty("hit").GetBoolean());
            var at = Start.AddSeconds(hit.GetProperty("t").GetDouble());
            var point = Vector(hit.GetProperty("player"));
            Check(encounter.Collect(Start, 100).Any(a => !a.Safe && a.From <= at && a.Until >= at && a.Geometry.Contains(point)),
                "The recorded first-hit position is incorrectly safe at the real effect time.");
        }
    }

    private static void Sequence()
    {
        using var fixture = Fixture();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        foreach (var clockwise in new[] { false, true })
        {
            var encounter = Encounter(cycle, clockwise);
            var initial = encounter.Collect(Start, 100);
            Check(initial.Count(a => !a.Safe) == 20, "Navigation lost future rotating pulses.");
            Check(initial.Count(a => !a.Safe && !a.NavigationOnly) == 2, "All ten rotations are drawn over each other.");
            var remaining = 20;
            foreach (var effect in cycle.GetProperty("effects").EnumerateArray())
            {
                Apply(encounter, effect, clockwise, cycle.GetProperty("rotation").GetSingle());
                var now = Start.AddSeconds(effect.GetProperty("t").GetDouble());
                var areas = encounter.Collect(now, 100);
                Check(areas.Count(a => !a.Safe) == --remaining, "A real effect failed to consume exactly one pulse.");
                Check(areas.Count(a => !a.Safe && !a.NavigationOnly) <= 2, "Future sweeps leaked into previews.");
                Check(areas.Where(a => !a.Safe).All(a => a.Shape is FanTemplate { Radius: 60 } f && Math.Abs(f.AngleRadians - MathF.PI / 3) < .001f), "Physical cone dimensions changed.");
                Apply(encounter, effect, clockwise, cycle.GetProperty("rotation").GetSingle());
                Check(encounter.Collect(now, 100).Count == areas.Count, "Duplicate target advanced a second pulse.");
            }
            Check(!encounter.Collect(Start.AddSeconds(16), 100).Any(), "Output survives the tenth pair.");
            encounter = Encounter(cycle, clockwise);
            encounter.Cancel(1);
            Check(!encounter.Collect(Start, 100).Any(), "Cancel retained rotation output.");
        }
    }

    private static void OriginIdentity()
    {
        using var fixture = Fixture();
        var cycle = fixture.RootElement.GetProperty("cycles")[0];
        var encounter = Encounter(cycle);
        var position = Vector(cycle.GetProperty("position"));
        var rotation = cycle.GetProperty("rotation").GetSingle();
        uint sequence = 900000;
        foreach (var origin in new Vector3?[] { null, Vector3.Zero, position + new Vector3(50, 0, 0) })
        {
            encounter.Apply(new LogSystemActionEffectEvent { SourceId = 999, ActionId = 44909,
                GlobalSequence = sequence++, SourcePosition = origin, SourceRotation = rotation }, Start);
            Check(encounter.Collect(Start, 100).Count(a => !a.Safe) == 20, "Unrelated or absent source origin consumed a rotating pulse.");
        }
        Apply(encounter, cycle.GetProperty("effects")[0]);
        Check(encounter.Collect(Start, 100).Count(a => !a.Safe) == 19, "Valid recorded pulse failed to advance after rejected effects.");
    }

    private static void Simulate()
    {
        using var fixture = Fixture();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        foreach (var latency in new[] { .08, .3 })
        {
            var encounter = Encounter(cycle);
            var player = Vector(cycle.GetProperty("initialPlayer"));
            var events = cycle.GetProperty("effects").EnumerateArray().ToArray();
            var next = 0;
            IReadOnlyList<TimedWaypoint> path = [];
            IReadOnlyList<TimedWaypoint>? pending = null;
            var ready = Start;
            var nextSolve = Start;
            var checks = 0;
            for (var t = 0.0; t < events[^1].GetProperty("t").GetDouble() + .1; t += .02)
            {
                var now = Start.AddSeconds(t);
                if (pending != null && now >= ready) { path = pending; pending = null; }
                if (path.Count > 0) player = PositionAt(path, now);
                Check(encounter.Boundary.Contains(player), "Rotation route left the physical arena.");
                while (next < events.Length && events[next].GetProperty("t").GetDouble() <= t)
                {
                    var effect = events[next++];
                    var hitAt = Start.AddSeconds(effect.GetProperty("t").GetDouble());
                    var actualPlayer = path.Count == 0 ? player : PositionAt(path, hitAt);
                    var actual = new FanTemplate(60, MathF.PI / 3).Resolve(Vector(effect.GetProperty("position")), effect.GetProperty("rotation").GetSingle(), null);
                    Check(!actual.Contains(actualPlayer), $"Recorded rotation hit {next}, latency {latency}, at {actualPlayer}.");
                    ++checks;
                    Apply(encounter, effect);
                }
                if (pending != null || now < nextSolve || next == events.Length) continue;
                using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
                manager.SetArenaBoundary(encounter.Boundary);
                foreach (var area in encounter.Collect(now, 100))
                {
                    var binding = new StaticTransform(area.Position, area.Rotation);
                    if (area.Safe) manager.AddGoalArea(area.Shape, binding, [new GoalWindow(area.From, area.Until, area.Priority, PositionPreference.Nearest)]);
                    else manager.AddDangerArea(area.Shape, binding, [area.Window]);
                }
                ready = now.AddSeconds(latency + .08);
                var startPoint = path.Count == 0 ? player : PositionAt(path, ready);
                var snapshot = manager.BuildSnapshot(ready);
                var solution = new SafeZoneSolver().Solve(snapshot, new(startPoint, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, ready);
                Check(solution.IsSuccess && !solution.IsLeastDangerFallback, $"Rotation solve failed at {t:F2}: {solution.DescribeOutcome()}");
                if (solution.ExecutablePath.Count > 0) pending = solution.ExecutablePath;
                nextSolve = now.AddSeconds(.1);
            }
            Check(checks == 20, "Simulation must validate every pulse.");
        }
    }

    private static Vector3 PositionAt(IReadOnlyList<TimedWaypoint> path, DateTime at)
    {
        for (var i = 0; i < path.Count; ++i)
        {
            if (at <= path[i].PlannedDepartureTime || i == path.Count - 1) return path[i].Position;
            var next = path[i + 1];
            if (at < next.PlannedArrivalTime) return Vector3.Lerp(path[i].Position, next.Position,
                (float)((at - path[i].PlannedDepartureTime) / (next.PlannedArrivalTime - path[i].PlannedDepartureTime)));
        }
        throw new InvalidOperationException("Empty movement path.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
