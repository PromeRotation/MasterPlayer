using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;
using DispatchPhase = MasterPlayer.Spatial.SafeZoneReborn.SafeZoneRebornMovementSystem.DispatchPhase;

internal static class Pt30MovingBallRecordedRegression
{
    public static void Run()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse30", "arr-124542-moving-balls.json");
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        var center = new Vector3(-300, 0, -300);
        var boundary = Pt30Arena.Boundary(0);
        var hole = Pt30Arena.Hole(0);
        var options = new SolveOptions { HardSafetyMargin = 0, ArenaSafetyMargin = 0 };
        var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var move = ScriptServiceProxy.Create<IGreenMoveSystem>((method, _) => method switch
        {
            "MoveTo" or "Stop" => null,
            "GetStatus" => default(GreenMoveStatus),
            "get_IsRunning" => false,
            _ => throw new NotSupportedException(method),
        });
        var unnecessaryBaselineMoves = 0;
        foreach (var sample in file.RootElement.GetProperty("samples").EnumerateArray())
        {
            var t = sample.GetProperty("t").GetDouble();
            var player = Xz(sample.GetProperty("player"));
            var setting = new SafeZoneSetting
            {
                EnableSafeZone = true, RuntimePreferSafeRefuge = true,
                RuntimeHardSafetyMargin = options.HardSafetyMargin, RuntimeArenaSafetyMargin = options.ArenaSafetyMargin,
            };
            using var manager = new SafeZoneManager(setting);
            var snapshot = RegisterScene(sample, manager, center, boundary, hole, player, now);
            var state = new PlayerState(player, 0, 9, false, false, 0, true, snapshot.Version);
            var solution = new SafeZoneSolver().Solve(snapshot, state, new(9, 9, .08f, false, 9, 9), options, now);
            Check(solution.IsSuccess && !solution.IsLeastDangerFallback, $"ARR {t}s did not find a safe plan: {solution.DescribeOutcome()}");
            var shouldHold = sample.GetProperty("hold").GetBoolean();
            if (!shouldHold)
            {
                var map = RasterizedMap.Build(snapshot, player, options, now, 9);
                Check(GridRouteSafe(map, solution.ExecutablePath, now, 0), $"ARR {t}s immediate route must satisfy the search grid");
                Check(!GridRouteSafe(map, solution.ExecutablePath, now, SafeZoneRebornMovementSystem.DepartureReserveSeconds),
                    $"ARR {t}s delayed route must reproduce the lost grid departure reserve");
                Check(solution.LatestSafeDepartureTime > now
                    && solution.LatestSafeDepartureTime <= now.AddSeconds(SafeZoneRebornMovementSystem.DepartureReserveSeconds),
                    $"ARR {t}s must publish its finite grid departure deadline");
                Check(solution.WithExecutablePath(solution.ExecutablePath).LatestSafeDepartureTime == solution.LatestSafeDepartureTime,
                    "rebasing a cached route must preserve its departure deadline");
            }
            var baselineMoves = Dispatch(preferSafeRefuge: false);
            var moves = Dispatch(preferSafeRefuge: true);
            Check(moves == (shouldHold ? 0 : 1), $"ARR {t}s expected {(shouldHold ? "hold" : "departure")}, dispatched {moves}");
            if (shouldHold) unnecessaryBaselineMoves += baselineMoves;

            int Dispatch(bool preferSafeRefuge)
            {
                setting.RuntimePreferSafeRefuge = preferSafeRefuge;
                using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
                movement.Attach(move);
                dispatch.Invoke(movement, [solution, state, snapshot, now]);
                Console.WriteLine($"ARR {t}s refuge={preferSafeRefuge}: {movement.Phase}, dispatches={movement.DispatchCount}, grid departure slack={(solution.LatestSafeDepartureTime - now)?.TotalSeconds:F3}s.");
                return movement.DispatchCount;
            }
        }
        Check(unnecessaryBaselineMoves > 0, "The fixture must reproduce unnecessary movement without the safe refuge policy.");
        Console.WriteLine($"PT30 ARR moving balls: suppressed {unnecessaryBaselineMoves} unnecessary dispatches and retained the required departure.");
    }

    public static void RefugeDepartureBoundary()
    {
        var now = DateTime.UnixEpoch;
        var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimePreferSafeRefuge = true };
        using var manager = new SafeZoneManager(setting);
        manager.SetArenaBoundary(new CircleTemplate(10).Resolve(Vector3.Zero, 0, null));
        manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0),
            [new DangerWindow(now.AddSeconds(2), now.AddSeconds(2.2), now)]);
        var snapshot = manager.BuildSnapshot(now);
        var state = new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version);
        var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var move = ScriptServiceProxy.Create<IGreenMoveSystem>((method, _) => method switch
        {
            "MoveTo" or "Stop" => null, "GetStatus" => default(GreenMoveStatus), "get_IsRunning" => false,
            _ => throw new NotSupportedException(method),
        });
        var reserve = SafeZoneRebornMovementSystem.DepartureReserveSeconds;
        foreach (var (deadline, age, expected) in new (double? Deadline, double Age, int Expected)[]
        {
            (reserve - .001, 0, 1), (reserve, 0, 1), (reserve + .001, 0, 0),
            (reserve + .1, .1, 1), (null, 0, 0),
        })
        {
            var solution = new SafeZoneSolution(SolutionStatus.Success, snapshot.Version, now, now, new(3, 0, 0),
                [TimedWaypoint.Pass(Vector3.Zero, now), TimedWaypoint.Pass(new(3, 0, 0), now.AddSeconds(.5))])
            {
                UsesTemporalPlanning = true,
                LatestSafeDepartureTime = deadline.HasValue ? now.AddSeconds(deadline.Value) : null,
            };
            using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
            movement.Attach(move);
            dispatch.Invoke(movement, [solution, state, snapshot, now.AddSeconds(age)]);
            Check(movement.DispatchCount == expected,
                $"refuge deadline={deadline}, age={age}: expected {expected} dispatches, got {movement.DispatchCount}");
        }
    }

    private static bool GridRouteSafe(RasterizedMap map, IReadOnlyList<TimedWaypoint> path, DateTime now, double delay)
    {
        for (var i = 1; i < path.Count; ++i)
        {
            var from = path[i - 1];
            var to = path[i];
            var duration = (to.PlannedArrivalTime - from.PlannedDepartureTime).TotalSeconds;
            var steps = Math.Max(1, (int)Math.Ceiling(duration / .001));
            for (var step = 0; step <= steps; ++step)
            {
                var point = Vector3.Lerp(from.Position, to.Position, (float)step / steps);
                var at = (from.PlannedDepartureTime - now).TotalSeconds + delay + duration * step / steps;
                var (col, row) = map.WorldToCell(point);
                if (!map.InBounds(col, row) || map.PixelMaxGAt(col, row) < 0
                    || map.DangerIntervalsAt(row * map.Columns + col).Any(w => at >= w.From && at <= w.Until)) return false;
            }
        }
        return true;
    }

    public static void HaloGripOverlap()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse30", "arr-162029-halo-grip.json");
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var origin = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        DateTime At(JsonElement sample) => origin.AddSeconds(sample.GetProperty("t").GetDouble());
        var measure = new ObservedMoveSpeed();
        const float modelSpeed = 6;
        foreach (var frame in file.RootElement.GetProperty("run").EnumerateArray())
            measure.Record(origin.AddSeconds(frame[0].GetDouble()), new(frame[1].GetSingle(), 0, frame[2].GetSingle()), modelSpeed, true);
        var speed = measure.Effective(modelSpeed, true);
        Check(speed is > 8 and < 9, $"ARR run speed should calibrate above the model, got {speed:F2}");
        Check(measure.Effective(modelSpeed) == modelSpeed, "unrequested faster observations must not override the model");

        var setting = new SafeZoneSetting
        {
            EnableSafeZone = true, RuntimePreferSafeRefuge = true, RuntimeGoalDirectedTemporalPlanning = true,
        };
        var center = new Vector3(-300, 0, -300);
        var boundary = Pt30Arena.Boundary(0);
        var hole = Pt30Arena.Hole(0);
        const float margin = Pt30Encounter.NAVIGATION_GRID_MARGIN;
        var options = new SolveOptions { HardSafetyMargin = margin, ArenaSafetyMargin = margin, GoalDirectedTemporalPlanning = true };
        var samples = file.RootElement.GetProperty("samples");
        var player = Xz(samples[0].GetProperty("player"));
        var now = At(samples[0]);
        using var manager = new SafeZoneManager(setting);
        var closed = RegisterScene(samples[0], manager, center, boundary, hole, player, now);
        PlayerState State(SafeZoneSnapshot snapshot, float runSpeed) => new(player, 0, runSpeed, false, false, 0, true, snapshot.Version);
        SafeZoneSolution Solve(SafeZoneSnapshot snapshot, float runSpeed, DateTime at) => new SafeZoneSolver().Solve(snapshot,
            State(snapshot, runSpeed), new(runSpeed, runSpeed, 0, false, runSpeed, runSpeed), options, at);
        using var openManager = new SafeZoneManager(setting);
        var openedAt = At(samples[1]);
        var opened = RegisterScene(samples[1], openManager, center, boundary, hole, player, openedAt);
        var hit = origin.AddSeconds(file.RootElement.GetProperty("gripHit").GetDouble());
        var arrival = Cross(speed);
        Cross(modelSpeed);

        DateTime Cross(float runSpeed)
        {
            var waiting = Solve(closed, runSpeed, now);
            Check(waiting.IsSuccess && waiting.ExecutablePath.Count == 1 && waiting.ExecutablePath[0].IsWaitPoint
                && waiting.ContinuationPath.Count > 1, "the closed halo should produce a wait followed by a real continuation");

            var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
            IReadOnlyList<GreenMovePoint> issued = [];
            var move = ScriptServiceProxy.Create<IGreenMoveSystem>((method, args) =>
            {
                if (method == "MoveTo" && args[0] is IReadOnlyList<GreenMovePoint> points) issued = points;
                return method switch
                {
                    "MoveTo" or "Stop" => null,
                    "GetStatus" => default(GreenMoveStatus),
                    "get_IsRunning" => false,
                    _ => throw new NotSupportedException(method),
                };
            });

            using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
            movement.Attach(move);
            dispatch.Invoke(movement, [waiting, State(closed, runSpeed), closed, now]);
            Check(issued.Count == 0 && movement.Phase == DispatchPhase.HoldingForDeparture, "must wait on the safe side of the closed halo");
            dispatch.Invoke(movement, [null, State(closed, runSpeed), closed, now.AddSeconds(.5)]);
            Check(movement.Phase == DispatchPhase.HoldingForDeparture, "a pending solve must not discard the verified wait");

            dispatch.Invoke(movement, [null, State(opened, runSpeed), opened, openedAt]);
            Check(issued.Count > 0 && movement.Phase == DispatchPhase.Moving, "opening must dispatch the retained route without a new worker result");
            var pointsOnRoute = issued.Select(p => p.Position).ToArray();
            var length = pointsOnRoute.Prepend(player).Zip(pointsOnRoute, Vector3.Distance).Sum();
            var arrivalAt = openedAt.AddSeconds(length / runSpeed + .1);

            Check(arrivalAt < hit && !opened.DangerAreas.Any(a => a.Geometry.Contains(issued[^1].Position)
                && a.DangerWindows.Any(w => w.IsActiveAt(hit))), "the continued route must reach safety before the recorded Grip hit");

            return arrivalAt;
        }

        using var lateManager = new SafeZoneManager(setting);
        var lateAt = At(samples[2]);
        var late = RegisterScene(samples[2], lateManager, center, boundary, hole, player, lateAt);
        var baseline = Solve(late, modelSpeed, lateAt);
        var measured = Solve(late, speed, lateAt);
        Check((!baseline.IsSuccess || baseline.IsLeastDangerFallback) && measured.IsSuccess && !measured.IsPartialEscape && !measured.IsLeastDangerFallback,
            $"late ARR crossing needs the measured speed; model={baseline.Status}, measured={measured.Status}");
        Console.WriteLine($"PT30 ARR halo + Grip: calibrated {speed:F2}y/s, continuation arrives {(hit - arrival).TotalSeconds:F2}s before hit; late 6y/s search {baseline.Status}.");
    }

    public static void ObservedSpeed()
    {
        const float model = 6;
        var origin = DateTime.UnixEpoch;
        var measure = new ObservedMoveSpeed();
        for (var i = 0; i <= 24; i++)
        {
            var t = i * .1f;
            measure.Record(origin.AddSeconds(t), new(model * t + t * t, 0, 0), model, true);
        }
        var first = measure.Effective(model, true);
        Check(first > 9 && first < 10.8f, "linear speed increase must use the observed run without extrapolating");
        measure.Record(origin.AddSeconds(9), new(20.16f, 0, 0), model, true);
        Check(measure.Effective(model, true) == first, "a short stop between phases should retain a confirmed run");
        measure.Record(origin.AddSeconds(11), new(20.16f, 0, 0), model, true);
        Check(measure.Effective(model, true) == model, "old haste must expire without fresh movement");
        measure.Reset();
        for (var i = 0; i <= 12; i++)
            measure.Record(origin.AddSeconds(i * .1), new(i * .5f, 0, 0), model, true);
        Check(MathF.Abs(measure.Effective(model, true) - 5) < .02f, "stable slower runs must still inform departure");
        measure.Record(origin.AddSeconds(1.3), new(6, 0, 0), 7, true);
        Check(measure.Effective(7, true) == 7, "model changes must invalidate older observations");
        measure.Reset();
        for (var i = 0; i <= 12; i++)
            measure.Record(origin.AddSeconds(i * .1), new(i < 6 ? i * .6f : i * .6f + 10, 0, 0), model, true);
        Check(measure.Effective(model, true) == model, "a single position jump must not authorize haste");
    }

    private static SafeZoneSnapshot RegisterScene(JsonElement sample, SafeZoneManager manager, Vector3 center,
        IGeometryShape boundary, GeometryTemplate hole, Vector3 player, DateTime now)
    {
        var encounter = new Pt30Encounter();
        var boss = new Pt30Encounter.Actor(1, Pt30Encounter.BOSS_OID, center, 0, false, true);
        var balls = sample.GetProperty("balls").EnumerateArray().ToArray();
        var lastFrame = balls[0].GetArrayLength() - 1;
        for (var frame = 0; frame <= lastFrame; frame++)
        {
            var actors = balls.Select((b, i) => new Pt30Encounter.Actor((ulong)i + 2, Pt30Encounter.BALL_OID, Xz(b[frame]), 0, false, true));
            encounter.Synchronize(actors.Prepend(boss), now.AddSeconds((frame - lastFrame) * .1));
        }
        ulong source = 100;
        foreach (var cast in sample.GetProperty("casts").EnumerateArray())
        {
            var start = now.AddSeconds(cast.GetProperty("start").GetDouble());
            var location = cast.GetProperty("offset").EnumerateArray().Select(v => v.GetSingle()).ToArray();
            encounter.Cast(source++, cast.GetProperty("action").GetUInt32(), new(location[0], location[1], location[2]),
                cast.GetProperty("rotation").GetSingle(), start, start.AddSeconds(cast.GetProperty("duration").GetSingle() + .3));
        }
        manager.SetArenaBoundary(boundary);
        manager.AddObstacle(hole, new StaticTransform(Vector3.Zero, 0));
        foreach (var hazard in encounter.Hazards(now, 0, player))
            manager.AddDangerArea(hazard.Shape, new StaticTransform(hazard.Position, hazard.Rotation), [hazard.Window(now)]);
        return manager.BuildSnapshot(now);
    }

    private static Vector3 Xz(JsonElement p) => new(p[0].GetSingle(), 0, p[1].GetSingle());

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
