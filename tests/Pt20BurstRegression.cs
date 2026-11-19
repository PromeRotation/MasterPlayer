using System.Numerics;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Pt20BurstRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT20 recorded zero-position effects retain every future burst", () => RecordedSequence(true, true));
        run("PT20 recorded bursts without cast observations resolve in order", () => RecordedSequence(false, true));
        run("PT20 recorded casts without status predictions resolve in order", () => RecordedSequence(true, false));
        run("PT20 recorded hit positions have an escape outside the actual circle", RecordedEscapes);
    }

    private static JsonDocument Fixture() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse", "pt20-bursts-200454.json")));

    private static Pt20Encounter Encounter()
    {
        var encounter = new Pt20Encounter();
        encounter.Synchronize([new ReplayActorState { Id = 1, Oid = 0x485C, InCombat = true }], Start);
        return encounter;
    }

    private static void Apply(Pt20Encounter encounter, JsonElement ev, DateTime origin, bool casts = true, bool statuses = true)
    {
        var now = origin.AddSeconds(ev.GetProperty("t").GetDouble());
        switch (ev.GetProperty("kind").GetString())
        {
            case "status" when statuses:
                encounter.Apply(new LogSystemStatusAddEvent
                {
                    TargetId = 1, StatusId = 2056, Param = ev.GetProperty("param").GetUInt16(), Duration = 3,
                }, now);
                break;
            case "statusRemove" when statuses:
                encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 2056 }, now);
                break;
            case "cast" when casts:
                encounter.StartCast(new(ev.GetProperty("source").GetUInt64(), ev.GetProperty("action").GetUInt32(),
                    Vector(ev.GetProperty("position")), 0, now, now.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), 0));
                break;
            case "effect":
                encounter.Apply(new LogSystemActionEffectEvent
                {
                    SourceId = ev.GetProperty("source").GetUInt64(), ActionId = ev.GetProperty("action").GetUInt32(),
                    GlobalSequence = ev.GetProperty("sequence").GetUInt32(), TargetIndex = 0,
                    EffectPosition = Vector3.Zero, SourcePosition = Vector(ev.GetProperty("position")),
                }, now);
                break;
        }
    }

    private static void RecordedSequence(bool casts, bool statuses)
    {
        using var fixture = Fixture();
        var encounter = Encounter();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        {
            var origin = Start.AddSeconds(cycle.GetProperty("castTime").GetDouble());
            foreach (var ev in cycle.GetProperty("events").EnumerateArray())
            {
                Apply(encounter, ev, origin, casts, statuses);
                if (ev.GetProperty("kind").GetString() != "effect") continue;
                var now = origin.AddSeconds(ev.GetProperty("t").GetDouble());
                var action = ev.GetProperty("action").GetUInt32();
                var expected = Enumerable.Range((int)action + 1, (int)(43459 - action)).Select(a => (uint)a);
                var actual = encounter.Collect(now, 100).Select(a => a.Action).Order().ToArray();
                Check(actual.SequenceEqual(expected), $"After {action}, expected only unresolved bursts; got [{string.Join(',', actual)}].");
                Apply(encounter, ev, origin, casts, statuses);
                Check(encounter.Collect(now, 100).Select(a => a.Action).Order().SequenceEqual(actual), "Duplicate effect consumed another burst.");
                Check(encounter.Collect(now, 100).All(a => a.Shape is CircleTemplate { Radius: 11 }), "Burst radius changed.");
            }
            Check(encounter.Collect(origin.AddSeconds(7), 100).Count == 0, "Burst output survived the fourth effect.");
        }
    }

    private static void RecordedEscapes()
    {
        using var fixture = Fixture();
        foreach (var cycle in fixture.RootElement.GetProperty("cycles").EnumerateArray())
        foreach (var probe in cycle.GetProperty("hitPositions").EnumerateArray())
        {
            var origin = Start.AddSeconds(cycle.GetProperty("castTime").GetDouble());
            var time = Math.Min(probe.GetProperty("t").GetDouble(), Math.Max(3.5, probe.GetProperty("stoppedSince").GetDouble() + .1));
            var now = origin.AddSeconds(time);
            var encounter = Encounter();
            foreach (var ev in cycle.GetProperty("events").EnumerateArray())
            {
                if (ev.GetProperty("t").GetDouble() > time) break;
                Apply(encounter, ev, origin);
            }
            var player = Vector(probe.GetProperty("position"));
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(encounter.Boundary);
            foreach (var area in encounter.Collect(now, 100))
                manager.AddDangerArea(area.Shape, new StaticTransform(area.Position, area.Rotation), [area.Window]);
            var snapshot = manager.BuildSnapshot(now);
            var options = new SolveOptions { GoalDirectedTemporalPlanning = true, HardSafetyMargin = .5f, ArenaSafetyMargin = 0 };
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), options, now);
            Check(solution.IsSuccess && !solution.IsNoMovement && !solution.IsLeastDangerFallback,
                $"Recorded {cycle.GetProperty("castTime")}+{time}s stop must escape: {solution.DescribeOutcome()}.");
            var path = solution.ExecutablePath.Concat(solution.ContinuationPath.Skip(1)).ToArray();
            Check(path.Length >= 2, "Escape has no movement.");
            foreach (var effect in cycle.GetProperty("events").EnumerateArray().Where(e =>
                e.GetProperty("kind").GetString() == "effect" && e.GetProperty("t").GetDouble() > time))
            {
                var hit = origin.AddSeconds(effect.GetProperty("t").GetDouble());
                var position = PositionAt(path, hit);
                var center = Vector(effect.GetProperty("position"));
                Check(Vector2.Distance(new(position.X, position.Z), new(center.X, center.Z)) > 11,
                    $"Escape still inside Action{effect.GetProperty("action")} at its actual recorded hit.");
                Check(encounter.Boundary.Contains(position), "Escape left the real arena.");
            }
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
        throw new InvalidOperationException("Empty escape path.");
    }

    private static Vector3 Vector(JsonElement value) => new(value[0].GetSingle(), value[1].GetSingle(), value[2].GetSingle());
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
