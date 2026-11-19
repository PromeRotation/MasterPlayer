using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using PromeRotation.GreenMoveSystem;

internal static class Pt50OverlapRegression
{
    private static readonly DateTime Start = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("PT50 sand immunity retains the wind requirement and sinking deadline", SandImmunity);
        foreach (var latency in new[] { .08, .3 })
        {
            if (Environment.GetEnvironmentVariable("PT50_SIM_LATENCY") is { } selected && latency.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) != selected) continue;
            run($"PT50 full recorded sand rocks chase and wind overlap latency={latency:F2}", () => Simulate(latency));
        }
    }

    private static void Simulate(double latency)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt50-overlap-155404.json")));
        var data = fixture.RootElement;
        var events = data.GetProperty("events").EnumerateArray().ToArray();
        var duration = data.GetProperty("duration").GetDouble();
        var player = Vector(data.GetProperty("initialPlayer"));
        var boss = new Vector3(-299.76f, 0, -292.5f);
        var chaseOrigin = Vector3.Zero;
        var chaseCount = 0;
        var sandActive = false;
        var hole = false;
        var shrunk = false;
        double? sandEntered = null;
        var observedSand = false;
        var sinkUntil = DateTime.MaxValue;
        var encounter = new Pt50Encounter();
        var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimeGoalDirectedTemporalPlanning = true };
        var manager = new SafeZoneManager(setting);
        using var managerScope = manager;
        var registered = new Dictionary<string, SafeZoneAreaHandle>();
        var solver = new SafeZoneSolver();
        var options = new SolveOptions { HardSafetyMargin = .5f, ArenaSafetyMargin = 0, GoalDirectedTemporalPlanning = true };
        var move = new SimulatedMove();
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
        var rocks = 0;
        var chases = 0;
        var winds = 0;
        var sinkEntries = 0;
        var noRoutes = 0;
        var maxSand = 0d;
        var minRockClearance = float.PositiveInfinity;
        var minChaseClearance = float.PositiveInfinity;
        var trace = new List<object>();
        var hits = new List<object>();
        var plans = new List<object>();
        var failures = new List<string>();
        Sync(Start);

        const double STEP = .02;
        for (var tick = 0; tick * STEP <= duration + STEP / 2; ++tick)
        {
            var t = tick * STEP;
            var now = Start.AddSeconds(t);
            player = move.Advance(player, (float)STEP);
            var inSand = sandActive && !PtArenaData.SandIslands.Any(a => a.Contains(player));
            if (inSand)
            {
                if (sandEntered == null) { sandEntered = t; ++sinkEntries; }
                maxSand = Math.Max(maxSand, t - sandEntered.Value);
                if (t - sandEntered >= data.GetProperty("sinkingSeconds").GetDouble())
                { failures.Add($"Sank at {t:F3}s, position {player}, continuous sand {t - sandEntered:F2}s."); break; }
            }
            else sandEntered = null;

            if (t >= nextScan - .00001)
            {
                var visibleSand = sandEntered != null && t - sandEntered >= .1;
                if (visibleSand != observedSand)
                {
                    observedSand = visibleSand;
                    if (visibleSand)
                    {
                        sinkUntil = Start.AddSeconds(sandEntered!.Value + 4);
                        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 567, Param = 0x400,
                            Duration = (float)(sinkUntil - now).TotalSeconds }, now);
                        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4625 }, now);
                    }
                    else
                    {
                        encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 567 }, now);
                        encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 4625 }, now);
                    }
                }
                Sync(now);
                nextScan = t + .1;
            }

            while (index < events.Length && events[index].GetProperty("t").GetDouble() <= t + .00001)
            {
                var ev = events[index++];
                var at = Start.AddSeconds(ev.GetProperty("t").GetDouble());
                var kind = ev.GetProperty("kind").GetString();
                switch (kind)
                {
                    case "cast":
                        var action = ev.GetProperty("action").GetUInt32();
                        var position = Vector(ev.GetProperty("position"));
                        if (action == 43534)
                        {
                            chaseOrigin = player;
                            boss = player;
                            position = player;
                            chaseCount = 0;
                        }
                        encounter.StartCast(new(ev.GetProperty("source").GetUInt64(), action, position,
                            ev.GetProperty("rotation").GetSingle(), at, at.AddSeconds(ev.GetProperty("seconds").GetDouble() + .3), 1));
                        break;
                    case "effect":
                        action = ev.GetProperty("action").GetUInt32();
                        position = Vector(ev.GetProperty("position"));
                        if (action == 43532)
                        {
                            ++rocks;
                            var clearance = Distance(player, position) - 5;
                            minRockClearance = Math.Min(minRockClearance, clearance);
                            if (clearance <= 0) failures.Add($"Rock hit at {t:F3}s: player={player}, center={position}, clearance={clearance:F3}.");
                        }
                        if (action is 43534 or 43535 or 43536)
                        {
                            if (chaseCount > 0)
                            {
                                var delta = player - chaseOrigin;
                                delta.Y = 0;
                                chaseOrigin += delta.LengthSquared() > 25 ? Vector3.Normalize(delta) * 5 : delta;
                            }
                            position = chaseOrigin;
                            boss = chaseOrigin;
                            ++chaseCount;
                            ++chases;
                            var clearance = Distance(player, position) - 6;
                            minChaseClearance = Math.Min(minChaseClearance, clearance);
                            if (clearance <= 0) failures.Add($"Chase hit {chaseCount} at {t:F3}s: player={player}, center={position}, clearance={clearance:F3}.");
                        }
                        if (action == 43538)
                        {
                            ++winds;
                            if (!inSand || !observedSand) failures.Add($"Wind hit without sand immunity at {t:F3}s: {player}.");
                        }
                        hits.Add(new { t, action, position = new[] { position.X, position.Y, position.Z }, player = new[] { player.X, player.Y, player.Z } });
                        encounter.Apply(new LogSystemActionEffectEvent { SourceId = ev.GetProperty("source").GetUInt64(), ActionId = action,
                            GlobalSequence = ev.GetProperty("sequence").GetUInt32(), SourcePosition = position,
                            EffectPosition = action == 43534 ? Vector3.Zero : position }, at);
                        break;
                    case "icon":
                        encounter.Apply(new LogSystemTargetIconEvent { TargetId = 100, MarkerId = 640 }, at);
                        break;
                    case "map":
                        var state = ev.GetProperty("state").GetUInt32();
                        var mapIndex = ev.GetProperty("index").GetUInt32();
                        if (mapIndex == 0 && state == 0x00020001) sandActive = true;
                        if (mapIndex == 1 && state is 0x00020001 or 0x00100004) { shrunk = true; hole = state == 0x00020001; }
                        if (state == 0x00080004) break;
                        encounter.Apply(new LogSystemMapEffectEvent { Position = mapIndex, Param1 = (ushort)state, Param2 = (ushort)(state >> 16) }, at);
                        break;
                    case "cancel": encounter.Cancel(ev.GetProperty("source").GetUInt64()); break;
                }
            }
            if (failures.Count > 0) break;
            if (!encounter.Boundary.Contains(player)) { failures.Add($"Left arena at {t:F3}s: {player}, shrunk={shrunk}."); break; }
            if (hole && Distance(player, new(-300, 0, -300)) < 3) { failures.Add($"Entered center hole at {t:F3}s: {player}."); break; }

            if (pending != null && t >= pendingAt - .00001)
            {
                if (!pending.IsSuccess) ++noRoutes;
                latest = pending;
                latestField.SetValue(movement, latest);
                pending = null;
            }
            Publish(now);
            var currentSnapshot = manager.BuildSnapshot(now);
            var currentState = new PlayerState(player, 0, 6, move.IsRunning, false, 0, true, currentSnapshot.Version);
            dispatch.Invoke(movement, [latest, currentState, currentSnapshot, now]);
            latest = movement.LatestSolution;
            if (pending == null && t >= nextSolve - .00001)
            {
                pending = solver.Solve(currentSnapshot, currentState,
                    new(6, 6, .08f, false, 6, 6), options, now, latest);
                plans.Add(new { t, player = new[] { player.X, player.Z }, pending.Status, pending.IsSuccess, pending.IsNoMovement, pending.UsesTemporalPlanning,
                    path = pending.ExecutablePath.Select(p => new { position = new[] { p.Position.X, p.Position.Z }, arrival = (p.PlannedArrivalTime - Start).TotalSeconds,
                        departure = (p.PlannedDepartureTime - Start).TotalSeconds, p.IsWaitPoint }) });
                pendingAt = t + latency;
                nextSolve = t + .2;
            }
            if (tick % 5 == 0)
                trace.Add(new { t, position = new[] { player.X, player.Y, player.Z }, sand = inSand, remaining = sinkUntil == DateTime.MaxValue ? 0 : (sinkUntil - now).TotalSeconds,
                    dangers = encounter.Collect(now, 100).Where(a => !a.PreviewOnly && !a.Safe && !a.Obstacle).Select(a => new { a.Key, from = (a.From - Start).TotalSeconds, until = a.Until == DateTime.MaxValue ? -1 : (a.Until - Start).TotalSeconds }) });
            if (tick % 1500 == 0 && tick > 0)
                Console.WriteLine($"PT50 overlap latency={latency:F2}: simulated {t:F0}/{duration:F0}s, rocks={rocks}, chase={chases}, wind={winds}");
        }
        var output = Environment.GetEnvironmentVariable("PT50_SIM_OUTPUT");
        if (output != null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, $"overlap-{latency:F2}.json"), JsonSerializer.Serialize(new
            { latency, rocks, chases, winds, sinkEntries, maxSand, minRockClearance, minChaseClearance, noRoutes, failures, trace, hits, plans,
                arena = PtArenaData.SandFloor.GetContours().Select(c => c.Select(p => new[] { p.X, p.Z })),
                islands = PtArenaData.SandIslands.SelectMany(a => a.GetContours()).Select(c => c.Select(p => new[] { p.X, p.Z })) }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"PT50 overlap latency={latency:F2}: rocks={rocks}/30 chase={chases}/12 wind={winds}/2 sandEntries={sinkEntries} maxSand={maxSand:F2}s minClearance rock={minRockClearance:F3} chase={minChaseClearance:F3} noRoute={noRoutes}");
        Check(failures.Count == 0, string.Join("\n", failures));
        Check(rocks == 30 && chases == 12 && winds == 2 && sinkEntries > 0, "Full combined timeline was not exercised.");

        void Sync(DateTime now)
        {
            ReplayStatus[] statuses = observedSand ? [new(567, 0x400, 0, sinkUntil), new(4625, 0, 0, DateTime.MaxValue)] : [];
            encounter.Synchronize([
                new ReplayActorState { Id = 1, Oid = 0x4872, Position = boss, InCombat = true, Hp = 1000, MaxHp = 1000 },
                new ReplayActorState { Id = 100, Kind = 1, Position = player, Hp = 1000, MaxHp = 1000, Statuses = statuses }], now);
        }
        void Publish(DateTime now)
        {
            manager.SetArenaBoundary(encounter.Boundary);
            var wanted = encounter.Collect(now, 100).Where(a => !a.PreviewOnly).ToArray();
            var keys = wanted.Select(a => a.Key).ToHashSet();
            foreach (var key in registered.Keys.Where(k => !keys.Contains(k)).ToArray())
            { manager.RemoveArea(registered[key]); registered.Remove(key); }
            foreach (var area in wanted)
            {
                var binding = new StaticTransform(area.Position, area.Rotation);
                if (!registered.TryGetValue(area.Key, out var handle) || !manager.ContainsArea(handle))
                    registered[area.Key] = area.Obstacle ? manager.AddObstacle(area.Shape, binding)
                        : manager.AddDangerArea(area.Shape, binding, [area.Window]);
                else manager.UpdateArea(handle, template: area.Shape, binding: binding, dangerWindows: area.Obstacle ? null : [area.Window], incrementVersion: false);
            }
        }
    }
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
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
        {
            if (!IsRunning) ready = elapsed + .08;
            points = route; index = 0;
        }
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
                if (Distance(current, point.Position) <= point.Tolerance) { ++index; continue; }
                var delta = point.Position - current; delta.Y = 0;
                return current + Vector3.Normalize(delta) * Math.Min(6 * dt, delta.Length());
            }
            return current;
        }
    }
    private static Vector3 Vector(JsonElement p) => new(p[0].GetSingle(), 0, p[2].GetSingle());
    private static void SandImmunity()
    {
        var encounter = new Pt50Encounter();
        var center = new Vector3(-300, 0, -300);
        encounter.Synchronize([new ReplayActorState { Id = 1, Oid = 0x4872, Position = center, InCombat = true }], Start);
        encounter.StartCast(new(1, 43531, center, 0, Start, Start.AddSeconds(3), 1));
        encounter.StartCast(new(2, 43538, center, 0, Start, Start.AddSeconds(8), 2));
        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 567, Duration = 4 }, Start.AddSeconds(6));
        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 4625 }, Start.AddSeconds(6));
        var output = encounter.Collect(Start.AddSeconds(7), 100);
        Check(output.Any(a => a.Key == "sand/enter") && output.Any(a => a.Key == "sand/leave"),
            "Duty immunity must retain the required sand position while preserving the four-second sinking deadline.");
        encounter.Apply(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 160, Duration = 10 }, Start.AddSeconds(7));
        Check(!encounter.Collect(Start.AddSeconds(7), 100).Any(a => a.Key == "sand/enter"), "Independent knockback immunity may release the entry condition.");
        encounter.Apply(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 160 }, Start.AddSeconds(7));
        encounter.Apply(new LogSystemActionEffectEvent { SourceId = 2, ActionId = 43538, GlobalSequence = 1 }, Start.AddSeconds(8));
        Check(!encounter.Collect(Start.AddSeconds(8), 100).Any(a => a.Key == "sand/enter") && encounter.Collect(Start.AddSeconds(8), 100).Any(a => a.Key == "sand/leave"),
            "The real wind effect must release the sand requirement and retain the sinking escape.");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
