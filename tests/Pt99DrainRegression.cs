using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using PromeRotation.LogSystem;

internal static class Pt99DrainRegression
{
    private static readonly DateTime Start = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Obstacle escape execution permits initial exit and rejects reentry, other obstacles and unsafe endpoints", ExitBoundaries);
        foreach (var latency in new[] { .08, .3 })
            run($"PT99 recorded dark then light drain movement latency={latency:F2}", () => Simulate(latency));
        run("PT99 recorded drain switches out of the current dark puddle", () =>
        {
            using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "pt99-drain-021732.json")));
            var fixture = file.RootElement;
            var encounter = new Pt99Encounter();
            var p = fixture.GetProperty("stuckPlayer");
            var player = new Vector3(p[0].GetSingle(), 0, p[2].GetSingle());
            var now = Start.AddSeconds(7.2);
            encounter.Synchronize([
                new ReplayActorState { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 2, Oid = 0x48EB, Position = new(-600, 0, -315), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 100, Kind = 1, Position = player, Hp = 1000,
                    Statuses = [new(4559, 0, 0, Start.AddSeconds(180))] }], Start);
            foreach (var ev in fixture.GetProperty("events").EnumerateArray())
            {
                var at = Start.AddSeconds(ev.GetProperty("t").GetDouble());
                if (at > now) break;
                var source = ev.GetProperty("source").GetUInt64();
                var action = ev.GetProperty("action").GetUInt32();
                if (ev.GetProperty("kind").GetString() == "cast")
                    encounter.StartCast(new(source, action, encounter.Center, 0, at,
                        at.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), source));
                else encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
                    GlobalSequence = ev.GetProperty("seq").GetUInt32() }, at);
            }
            var areas = encounter.Collect(now, 100).Where(a => !a.PreviewOnly).ToArray();
            Check(areas.Single(a => a.Key == "color/goal").Name == "获取光之复仇", "Second drain requires light");
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(encounter.Boundary);
            foreach (var area in areas)
            {
                var binding = new StaticTransform(area.Position, area.Rotation);
                if (area.Obstacle) manager.AddObstacle(area.Shape, binding);
                else if (area.Safe) manager.AddGoalArea(area.Shape, binding,
                    [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)]);
                else manager.AddDangerArea(area.Shape, binding, [area.Window]);
            }
            var snapshot = manager.BuildSnapshot(now);
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, .08f, false, 6, 6), new SolveOptions { HardSafetyMargin = .2f, ArenaSafetyMargin = 0,
                    GoalDirectedTemporalPlanning = true }, now);
            Check(solution.IsSuccess && !solution.IsNoMovement && !solution.IsLeastDangerFallback,
                "The recorded dark-water start must produce a real route to light");
        });
    }

    private static void ExitBoundaries()
    {
        var setting = new SafeZoneSetting { EnableSafeZone = true };
        using var manager = new SafeZoneManager(setting);
        manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 10));
        var obstacle = manager.AddObstacle(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0));
        using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
        var method = typeof(SafeZoneRebornMovementSystem).GetMethod("IsTemporalPhaseSafe", BindingFlags.NonPublic | BindingFlags.Instance)!;
        bool Accept(bool temporal, bool exits, params Vector3[] points)
        {
            var snapshot = manager.BuildSnapshot(Start);
            var path = points.Select((p, i) => TimedWaypoint.Pass(p, Start.AddSeconds(i))).ToArray();
            var solution = new SafeZoneSolution(SolutionStatus.Success, snapshot.Version, Start, Start, points[^1], path)
                { UsesTemporalPlanning = temporal, ExitsInitialPersistentDanger = exits };
            var player = new PlayerState(points[0], 0, 6, false, false, 0, true, snapshot.Version);
            return (bool)method.Invoke(movement, [solution, player, snapshot, Start, null, 0d])!;
        }
        Check(Accept(false, false, Vector3.Zero, Vector3.UnitX, Vector3.UnitX * 3), "Non-temporal route can exit its starting obstacle");
        Check(Accept(true, true, Vector3.Zero, Vector3.UnitX * 3), "Temporal solver escape may exit its starting obstacle");
        Check(!Accept(true, false, Vector3.Zero, Vector3.UnitX * 3), "Ordinary temporal route cannot bypass obstacles");
        Check(!Accept(false, false, Vector3.Zero, Vector3.UnitX), "Endpoint must be outside the obstacle");
        Check(!Accept(false, false, Vector3.Zero, Vector3.UnitX * 3, Vector3.Zero, Vector3.UnitX * 4), "Cannot reenter after exiting");
        Check(!Accept(false, false, Vector3.UnitX * -3, Vector3.UnitX * 3), "Cannot enter an obstacle that did not contain the start");
        Check(!Accept(false, false, Vector3.Zero, Vector3.UnitX * 12), "Arena boundary still blocks escape routes");
        var other = manager.AddObstacle(new CircleTemplate(.5f), new StaticTransform(Vector3.UnitX * 4, 0));
        Check(!Accept(false, false, Vector3.Zero, Vector3.UnitX * 5), "Another obstacle still blocks the route");
        manager.RemoveArea(other);
        manager.RemoveArea(obstacle);
        manager.AddObstacle(new PtShapeTemplate(new PtCompositeGeometry([
            new CircleGeometry(Vector3.Zero, 1), new CircleGeometry(Vector3.UnitX * 3, 1)])), new StaticTransform(Vector3.Zero, 0));
        Check(!Accept(false, false, Vector3.Zero, Vector3.UnitX * 5), "A single segment cannot exit and reenter a composite obstacle");
    }

    private static void Simulate(double latency)
    {
        using var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt99-drain-021732.json")));
        var fixture = file.RootElement;
        var events = fixture.GetProperty("events").EnumerateArray().ToArray();
        var initial = fixture.GetProperty("initialPlayer");
        var player = new Vector3(initial[0].GetSingle(), 0, initial[2].GetSingle());
        var encounter = new Pt99Encounter();
        var status = 4560u;
        uint pendingStatus = 0;
        var statusAt = 0d;
        var acquired = 0d;
        var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true };
        using var manager = new SafeZoneManager(setting);
        manager.SetArenaBoundary(encounter.Boundary);
        manager.AddGoalArea(new CircleTemplate(53), new StaticTransform(new(-600, 0, -330), 0),
            [GoalWindow.Persistent(Start, 1, PositionPreference.Nearest)]);
        var handles = new Dictionary<string, SafeZoneAreaHandle>();
        var solver = new SafeZoneSolver();
        var move = new SimulatedMove();
        using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
        movement.Attach(move);
        var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var latestField = typeof(SafeZoneRebornMovementSystem).GetField("_latestSolution", BindingFlags.NonPublic | BindingFlags.Instance)!;
        SafeZoneSolution? latest = null;
        SafeZoneSolution? pending = null;
        var pendingAt = double.PositiveInfinity;
        var nextSolve = 0d;
        var index = 0;
        var hits = 0;
        var trace = new List<string>();
        for (var tick = 0; tick <= 605; ++tick)
        {
            var t = tick * .02;
            var now = Start.AddSeconds(t);
            player = move.Advance(player, .02f);
            var water = PtArenaData.LightPuddles.Any(p => p.Contains(player)) ? 4560u
                : PtArenaData.DarkPuddles.Any(p => p.Contains(player)) ? 4559u : 0;
            if (water != 0 && water != status && pendingStatus != water)
            { pendingStatus = water; statusAt = t + .2; }
            if (water != pendingStatus) pendingStatus = 0;
            if (pendingStatus != 0 && t >= statusAt)
            { status = pendingStatus; acquired = t; pendingStatus = 0; }
            encounter.Synchronize([
                new ReplayActorState { Id = 1, Oid = 0x48EA, Position = new(-600, 0, -330), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 2, Oid = 0x48EB, Position = new(-600, 0, -315), InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 100, Kind = 1, Position = player, Hp = 1000,
                    Statuses = [new(status, 0, 0, Start.AddSeconds(180))] }], now);
            while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t + .00001)
            {
                var ev = events[index++];
                var at = Start.AddSeconds(ev.GetProperty("t").GetDouble());
                var action = ev.GetProperty("action").GetUInt32();
                var source = ev.GetProperty("source").GetUInt64();
                if (ev.GetProperty("kind").GetString() == "cast")
                    encounter.StartCast(new(source, action, encounter.Center, 0, at,
                        at.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), source));
                else
                {
                    ++hits;
                    Check(status == (action == 44091 ? 4559u : 4560u) && acquired <= t - 1,
                        $"Drain {action} failed at {t:F3}: status={status}, acquired={acquired:F2}, player={player}\n{string.Join("\n", trace.TakeLast(10))}");
                    // The first effect has zero targets, so reproduce the production
                    // fallback when the PR action-effect event is absent.
                    if (action != 44091)
                        encounter.Apply(new LogSystemActionEffectEvent { SourceId = source, ActionId = action,
                            GlobalSequence = ev.GetProperty("seq").GetUInt32() }, at);
                }
            }
            Check(encounter.Boundary.Contains(player), "Stay in the original arena");
            var wanted = encounter.Collect(now, 100).Where(a => !a.PreviewOnly).ToArray();
            var keys = wanted.Select(a => a.Key).ToHashSet();
            foreach (var key in handles.Keys.Where(k => !keys.Contains(k)).ToArray())
            { manager.RemoveArea(handles[key]); handles.Remove(key); }
            foreach (var area in wanted)
            {
                var binding = new StaticTransform(area.Position, area.Rotation);
                if (!handles.TryGetValue(area.Key, out var handle))
                {
                    if (area.Obstacle) handles[area.Key] = manager.AddObstacle(area.Shape, binding);
                    else if (area.Safe) handles[area.Key] = manager.AddGoalArea(area.Shape, binding,
                        [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)]);
                    else handles[area.Key] = manager.AddDangerArea(area.Shape, binding, [area.Window]);
                }
                else manager.UpdateArea(handle, template: area.Shape, binding: binding,
                    dangerWindows: !area.Safe && !area.Obstacle ? [area.Window] : null,
                    goalWindows: area.Safe ? [new(area.From, area.Until, area.Priority, PositionPreference.Nearest)] : null,
                    incrementVersion: false);
            }
            if (pending != null && t >= pendingAt - .00001)
            { latest = pending; latestField.SetValue(movement, latest); pending = null; }
            var snapshot = manager.BuildSnapshot(now);
            var state = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
            dispatch.Invoke(movement, [latest, state, snapshot, now]);
            latest = movement.LatestSolution;
            if (pending == null && t >= nextSolve - .00001)
            {
                pending = solver.Solve(snapshot, state, new(6, 6, .08f, false, 6, 6),
                    new SolveOptions { HardSafetyMargin = .2f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true }, now, latest);
                trace.Add($"t={t:F2} status={status} player={player} {pending.Status} noMove={pending.IsNoMovement} "
                    + string.Join(";", pending.ExecutablePath.Select(p => p.Position)));
                pendingAt = t + latency;
                nextSolve = t + .2;
            }
        }
        Check(hits == 2, "Both actual drains must be checked");
    }

    private sealed class SimulatedMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> points = [];
        private int index;
        private double elapsed;
        private double ready;
        public bool IsRunning => index < points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? points[index] : null,
            points.Count > 0 ? points[^1] : null, index, points.Count, points.Count - index);
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> route)
        { if (!IsRunning) ready = elapsed + .08; points = route; index = 0; }
        public void Stop() { points = []; index = 0; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> route) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => ++index;
        public Vector3 Advance(Vector3 current, float dt)
        {
            elapsed += dt;
            if (elapsed < ready) return current;
            while (IsRunning)
            {
                var point = points[index];
                if (Vector3.Distance(current, point.Position) <= point.Tolerance) { ++index; continue; }
                var delta = point.Position - current;
                delta.Y = 0;
                return current + Vector3.Normalize(delta) * Math.Min(6 * dt, delta.Length());
            }
            return current;
        }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
