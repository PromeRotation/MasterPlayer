using System.Numerics;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class Pt30PassageRegression
{
    public static void PartialRefugeRefresh()
    {
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true };
        using var manager = new SafeZoneManager(setting);
        manager.SetArenaBoundary(new CircleTemplate(30).Resolve(Vector3.Zero, 0, null));
        var snapshot = manager.BuildSnapshot(now);
        using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
        var solution = new SafeZoneSolution(SolutionStatus.Success, snapshot.Version, now, now, Vector3.Zero,
            [new TimedWaypoint(Vector3.Zero, now, now.AddSeconds(6), true, 1)], null)
        { UsesTemporalPlanning = true, IsPartialEscape = true };
        var method = typeof(SafeZoneRebornMovementSystem).GetMethod("IsSolutionValid", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var player = new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version);
        Check(!(bool)method.Invoke(movement, [solution, snapshot, player, now.AddSeconds(1)])!, "goal-directed partial refuge must expire before its six-second hold ends");
        setting.RuntimeGoalDirectedTemporalPlanning = false;
        Check((bool)method.Invoke(movement, [solution, snapshot, player, now.AddSeconds(1)])!, "ordinary safe refuge retains its stability contract");
    }

    public static void Run()
    {
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse30", "merged-001823-passages.json")));
        foreach (var sample in file.RootElement.GetProperty("snapshots").EnumerateArray())
        {
            var t = sample.GetProperty("t").GetDouble();
            var player = Vector(sample.GetProperty("player"));
            var rotation = sample.GetProperty("rotation").GetSingle();
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            var boundary = Pt30Arena.Boundary(player.Y);
            var hole = Pt30Arena.Hole(player.Y);
            manager.SetArenaBoundary(boundary);
            manager.AddObstacle(hole, new StaticTransform(Vector3.Zero, 0));
            foreach (var danger in sample.GetProperty("hazards").EnumerateArray())
            {
                var shape = Shape(danger);
                var from = now.AddSeconds(danger.GetProperty("from").GetDouble());
                var until = danger.GetProperty("until");
                var window = new DangerWindow(from, until.ValueKind == JsonValueKind.Null ? null : now.AddSeconds(until.GetDouble()), now)
                {
                    HasMotionEnvelope = danger.GetProperty("HasMotionEnvelope").GetBoolean(),
                    HasExplicitTiming = danger.GetProperty("HasExplicitTiming").GetBoolean(),
                };
                manager.AddDangerArea(shape,
                    new StaticTransform(Vector(danger.GetProperty("position")), danger.GetProperty("rotation").GetSingle()), [window]);
            }
            var goal = new FanTemplate(29, 128 * MathF.PI / 180);
            manager.AddGoalArea(goal, new StaticTransform(new(-300, player.Y, -300), sample.GetProperty("goalRotation").GetSingle()),
                [GoalWindow.Persistent(now, 5, PositionPreference.Nearest)]);
            var snapshot = manager.BuildSnapshot(now);
            var options = new SolveOptions { GoalDirectedTemporalPlanning = true, HardSafetyMargin = Pt30Encounter.NAVIGATION_GRID_MARGIN, ArenaSafetyMargin = Pt30Encounter.NAVIGATION_GRID_MARGIN };
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, rotation, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), options, now);
            Check(solution.IsSuccess && !solution.IsPartialEscape && !solution.IsLeastDangerFallback, $"recorded {t}s must reach the safe side, not hold a temporary pocket: {solution.Status}");
            var path = solution.ExecutablePath.Concat(solution.ContinuationPath.Skip(1)).ToArray();
            Check(path.Length >= 2 && snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position), $"recorded {t}s must end in the Reach goal");
            var reach = sample.GetProperty("hazards").EnumerateArray().First(h => h.GetProperty("shape").GetString() == nameof(FanTemplate));
            for (var i = 0; i < path.Length; ++i)
            {
                CheckSegment(snapshot, path[i].Position, path[i].Position, path[i].PlannedArrivalTime, path[i].PlannedDepartureTime);
                if (i > 0)
                    CheckSegment(snapshot, path[i - 1].Position, path[i].Position, path[i - 1].PlannedDepartureTime, path[i].PlannedArrivalTime);
            }
            var hitEnd = now.AddSeconds(reach.GetProperty("until").GetDouble());
            if (path[^1].PlannedArrivalTime < hitEnd)
                CheckSegment(snapshot, path[^1].Position, path[^1].Position, path[^1].PlannedArrivalTime, hitEnd);
        }
    }

    private static void CheckSegment(SafeZoneSnapshot snapshot, Vector3 from, Vector3 to, DateTime start, DateTime end)
    {
        var duration = Math.Max(0, (end - start).TotalSeconds);
        var steps = Math.Max(1, (int)Math.Ceiling(duration / .01));
        for (var i = 0; i <= steps; ++i)
        {
            var position = Vector3.Lerp(from, to, (float)i / steps);
            var at = start.AddSeconds(duration * i / steps);
            Check(snapshot.ArenaBoundary!.Contains(position) && !snapshot.HardObstacles.Any(o => o.Geometry.Contains(position)), "passage route must stay on real floor");
            Check(!snapshot.DangerAreas.Any(d => d.Geometry.Contains(position) && d.DangerWindows.Any(w => w.IsActiveAt(at))), "passage route must retain the inflated danger geometry and timed ball sweeps");
        }
    }

    private static GeometryTemplate Shape(JsonElement value)
    {
        var p = value.GetProperty("parameters");
        return value.GetProperty("shape").GetString() switch
        {
            nameof(Pt30BallSweepTemplate) => new Pt30BallSweepTemplate(Vector(p.GetProperty("Start")), Vector(p.GetProperty("End")), p.GetProperty("Radius").GetSingle()),
            nameof(DonutTemplate) => new DonutTemplate(p.GetProperty("InnerRadius").GetSingle(), p.GetProperty("OuterRadius").GetSingle()),
            nameof(FanTemplate) => new FanTemplate(p.GetProperty("Radius").GetSingle(), p.GetProperty("AngleRadians").GetSingle()),
            nameof(CircleTemplate) => new CircleTemplate(p.GetProperty("Radius").GetSingle()),
            _ => throw new InvalidOperationException($"Unexpected fixture geometry: {value.GetProperty("shape")}"),
        };
    }

    private static Vector3 Vector(JsonElement value) => new(value.GetProperty("X").GetSingle(), value.GetProperty("Y").GetSingle(), value.GetProperty("Z").GetSingle());

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
