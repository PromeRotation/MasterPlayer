using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Dalamud.Game.ClientState.Objects.Enums;
using MasterPlayer.Settings;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;
using PromeRotation.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static partial class FollowRegression
{
    private static void RunCombatRange(Action<string, Action> run)
    {
        run("Combat range migrates old settings and persists independent player distance", () =>
        {
            using var scope = new FollowScope();
            var options = new JsonSerializerOptions { IncludeFields = true };
            var old = JsonSerializer.Deserialize<MasterPlayerSettings>("{\"FollowDistance\":8}", options)!;
            Check(old.CombatDistance == CombatDistanceMode.Melee && old.RangedAttackRange == 25 && old.FollowDistance == 8,
                "old settings did not retain party distance with safe melee defaults");
            old.CombatDistance = CombatDistanceMode.Ranged;
            old.RangedAttackRange = 20;
            var restored = JsonSerializer.Deserialize<MasterPlayerSettings>(JsonSerializer.Serialize(old, options), options)!;
            Check(restored.CombatDistance == CombatDistanceMode.Ranged && restored.RangedAttackRange == 20 && restored.FollowDistance == 8,
                "mode or distances did not persist");
        });

        foreach (var automatic in new[] { false, true })
        {
            run($"Combat range holds ranged attacks without chasing the boss; planner={automatic}", () =>
            {
                using var scope = new FollowScope { TargetPosition = new(20,0,0) };
                scope.Settings.EnableAutoDodge = automatic;
                scope.Settings.CombatDistance = CombatDistanceMode.Ranged;
                scope.Update();
                if (automatic)
                {
                    var now = DateTime.UtcNow;
                    var result = SolveRange(scope, now);
                    Check(result.IsSuccess && result.IsNoMovement, $"ranged player chased center: {result.TargetPosition}");
                    Check(result.DebugData?.SearchExpansions <= 2, "standing safely in range searched the entire arena");
                }
                Check(scope.MoveCount == 0, "ranged player sent direct movement while already in range");
            });
            run($"Combat range returns into ranged skill reach; planner={automatic}", () =>
            {
                using var scope = new FollowScope { TargetPosition = new(35,0,0) };
                scope.Settings.EnableAutoDodge = automatic;
                scope.Settings.CombatDistance = CombatDistanceMode.Ranged;
                scope.Update();
                if (automatic)
                {
                    var result = SolveRange(scope, DateTime.UtcNow);
                    Check(result.IsSuccess && result.TargetPosition is { } target && target.X > 5 && target.X < 8,
                        $"ranged return overshot or did not approach: {result.TargetPosition}");
                    Check(Vector3.Distance(result.TargetPosition!.Value, scope.TargetPosition) <= 29, "ranged destination outside skill reach");
                    Check(result.DebugData?.SearchExpansions < 100, "nearest ranged destination exhausted the grid");
                }
                else
                    Check(scope.MoveCount == 1 && scope.LastPoint?.Tolerance == 29, "direct ranged return has wrong tolerance");
            });
        }

        run("Combat range does not apply ranged skill reach to player following", () =>
        {
            using var scope = new FollowScope { TargetKind = ObjectKind.Pc, TargetPosition = new(10,0,0) };
            scope.Settings.CombatDistance = CombatDistanceMode.Ranged;
            scope.Settings.FollowDistance = 2;
            scope.Update();
            Check(scope.LastPoint?.Tolerance == 2, "ranged mode changed party following");
        });

        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, -10f, 500f })
            run($"Combat range clamps invalid ranged configuration {invalid}", () =>
            {
                using var scope = new FollowScope { TargetPosition = new(50,0,0) };
                scope.Settings.CombatDistance = CombatDistanceMode.Ranged;
                scope.Settings.RangedAttackRange = invalid;
                scope.Update();
                Check(scope.LastPoint is { } point && float.IsFinite(point.Tolerance) && point.Tolerance is >= 9 and <= 29,
                    "invalid range escaped validation");
            });

        run("Combat range keeps a narrow melee safe sector under 25 percent", () =>
        {
            using var scope = new FollowScope { TargetPosition = Vector3.Zero, PlayerPosition = new(0,0,-12) };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            var fan = new FanTemplate(30, MathF.PI * 1.8f);
            scope.Manager.AddDangerArea(fan, new StaticTransform(Vector3.Zero,0), [new DangerWindow(now.AddSeconds(3),now.AddSeconds(5))]);
            scope.Update();
            var goal = scope.Manager.BuildSnapshot(now).GoalAreas.Single();
            Check(goal.GoalWindows.Any(w => w.IsActiveAt(now)), "usable melee sector was discarded by its area ratio");
            var result = SolveRange(scope, now);
            Check(result.IsSuccess && result.TargetPosition is { } target && goal.Geometry.Contains(target) && target.Length() <= 7,
                $"failed to approach through narrow sector: {result.Status}, {result.TargetPosition}");
            CheckPathOutside(result, fan.Resolve(Vector3.Zero,0,null));
        });

        foreach (var casting in new[] { false, true })
            run($"Combat range escapes to the melee side of urgent damage; casting={casting}", () =>
            {
                using var scope = new FollowScope { TargetPosition = new(7,0,0), PlayerCasting = casting };
                scope.Settings.EnableAutoDodge = true;
                var now = DateTime.UtcNow; scope.Now = now;
                scope.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero,0),
                    [new DangerWindow(now.AddSeconds(1.2),now.AddSeconds(3))]);
                scope.Update();
                var result = SolveRange(scope, now, delay: .1f);
                Check(result.IsSuccess && result.TargetPosition is { } target && target.X > 2 && Vector3.Distance(target,scope.TargetPosition) <= 7,
                    $"escape abandoned reachable melee: {result.Status}, {result.TargetPosition}");
                var last = result.ExecutablePath.Last();
                Check(last.PlannedArrivalTime < now.AddSeconds(1.2), "uptime delayed escape past hit");
            });

        run("Combat range leaves a full melee circle and returns only after resolution", () =>
        {
            using var scope = new FollowScope { TargetPosition = Vector3.Zero, PlayerPosition = new(5,0,0) };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            var hazard = new CircleGeometry(Vector3.Zero,9);
            scope.Manager.AddDangerArea(new CircleTemplate(9),new StaticTransform(Vector3.Zero,0),
                [new DangerWindow(now.AddSeconds(2),now.AddSeconds(3))]);
            scope.Update();
            var escape = SolveRange(scope,now);
            Check(escape.IsSuccess && escape.TargetPosition is { } target && !hazard.Contains(target), "uptime blocked mandatory disengagement");
            scope.PlayerPosition = escape.TargetPosition!.Value;
            scope.Now = now.AddSeconds(3.1); scope.Update();
            var held = SolveRange(scope,scope.Now.Value);
            Check(held.IsSuccess && held.ExecutablePath.All(p => !hazard.Contains(p.Position)), "returned before safety tail elapsed");
            scope.Now = now.AddSeconds(3.4); scope.Update();
            var returned = SolveRange(scope,scope.Now.Value);
            Check(returned.IsSuccess && returned.TargetPosition is { } melee && melee.Length() <= 7, "did not return into melee after damage resolved");
        });

        run("Combat range closes into the safe inner ring of a donut", () =>
        {
            using var scope = new FollowScope { TargetPosition = Vector3.Zero, PlayerPosition = new(0,0,-10) };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            scope.Manager.AddDangerArea(new DonutTemplate(6,30),new StaticTransform(Vector3.Zero,0),
                [new DangerWindow(now.AddSeconds(3),now.AddSeconds(4))]);
            scope.Update();
            var result = SolveRange(scope,now);
            Check(result.IsSuccess && result.TargetPosition is { } target && target.Length() < 5.5f,
                $"donut did not choose safe melee inside: {result.Status}, {result.TargetPosition}");
        });

        run("Combat range respects arena edges while approaching a moving boss", () =>
        {
            using var scope = new FollowScope { TargetPosition = new(18,0,0) };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            var arena = new CircleGeometry(Vector3.Zero,20);
            scope.Manager.SetArenaBoundary(arena);
            scope.Update();
            var first = scope.Manager.BuildSnapshot(now);
            var result = SolveRange(scope,now);
            Check(result.IsSuccess && result.TargetPosition is { } target && arena.Contains(target) && Vector3.Distance(target,scope.TargetPosition) <= 7,
                "edge boss had no reachable attack position");
            scope.PlayerPosition = result.TargetPosition!.Value;
            scope.TargetPosition = new(-12,0,0); scope.Update();
            var moved = SolveRange(scope,now);
            Check(moved.IsSuccess && moved.TargetPosition is { } destination && Vector3.Distance(destination,scope.TargetPosition) <= 7,
                "boss movement retained a stale attack location");
            Check(first.GoalAreas.Single().Geometry.Position == new Vector3(18,0,0), "moving goal mutated earlier worker snapshot");
        });

        run("Combat range changes mode without leaking goals or restarting every frame", () =>
        {
            using var scope = new FollowScope { TargetPosition = new(20,0,0) };
            scope.Settings.EnableAutoDodge = true;
            var now = DateTime.UtcNow; scope.Now = now;
            scope.Update();
            Check(SolveRange(scope,now).TargetPosition is not null, "melee did not approach");
            scope.Settings.CombatDistance = CombatDistanceMode.Ranged; scope.Update();
            var snapshot = scope.Manager.BuildSnapshot(now);
            Check(snapshot.GoalAreas.Count == 1 && SolveRange(scope,now).IsNoMovement, "ranged switch kept stale melee goal");
            for (var i=0; i<10; ++i) scope.Update();
            Check(scope.Manager.BuildSnapshot(now).Version == snapshot.Version, "unchanged goal reset solver each frame");
            scope.TargetDead = true; scope.Update();
            Check(scope.Manager.BuildSnapshot(now).GoalAreas.Count == 0, "target death leaked attack goal");
        });

        foreach (var delay in new[] { .08, .2 })
            foreach (var scenario in new[] { "narrow sector", "disengage and return", "urgent splash" })
                run($"Combat range live executor: {scenario}, {delay:F2}s worker delay", () => RunRangeMovement(scenario,delay));
    }

    private static void RunRangeMovement(string scenario, double delay)
    {
        using var scope = new FollowScope { TargetPosition = Vector3.Zero };
        scope.Settings.EnableAutoDodge = true;
        scope.PlayerPosition = scenario switch
        {
            "narrow sector" => new(0,0,-12),
            "disengage and return" => new(5,0,0),
            _ => new(6,0,0),
        };
        var epoch = DateTime.UtcNow;
        GeometryTemplate template = scenario switch
        {
            "narrow sector" => new FanTemplate(30,MathF.PI*1.8f),
            "disengage and return" => new CircleTemplate(9),
            _ => new CircleTemplate(2),
        };
        var origin = scenario == "urgent splash" ? scope.PlayerPosition : Vector3.Zero;
        var danger = template.Resolve(origin,0,null);
        var hit = scenario == "urgent splash" ? 1.2 : 2.5;
        scope.Manager.AddDangerArea(template,new StaticTransform(origin,0),
            [new DangerWindow(epoch.AddSeconds(hit),epoch.AddSeconds(3.5))]);
        scope.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero,22));
        var (movement,move) = ((object,IGreenMoveSystem))typeof(LateDodgeRegression)
            .GetMethod("CreateMovement",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null)!;
        const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
        var latest = movement.GetType().GetField("_latestSolution",flags)!;
        var dispatch = movement.GetType().GetMethod("Dispatch",flags)!;
        var advance = move.GetType().GetMethod("Advance")!;
        SafeZoneSolution? pending = null;
        double ready = 0;
        var moved = false;
        for (var tick=0; tick<=600; ++tick)
        {
            var t = tick * .01;
            var now = epoch.AddSeconds(t); scope.Now = now; scope.Update();
            var snapshot = scope.Manager.BuildSnapshot(now);
            if (pending != null && t + 1e-6 >= ready) { latest.SetValue(movement,pending); pending=null; }
            var solution = (SafeZoneSolution?)latest.GetValue(movement);
            var player = new PlayerState(scope.PlayerPosition,0,6,false,false,0,true,snapshot.Version);
            if (pending == null)
            {
                pending = new SafeZoneSolver().Solve(snapshot,player,new(6,6,0,false,6,6),new(),now,solution);
                ready=t+delay;
            }
            dispatch.Invoke(movement,[solution,player,snapshot,now]);
            moved |= move.IsRunning;
            scope.PlayerPosition=(Vector3)advance.Invoke(move,[scope.PlayerPosition,.01f])!;
            Check(snapshot.ArenaBoundary!.Contains(scope.PlayerPosition), "execution left arena");
            if (t>=hit && t<=3.8) Check(!danger.Contains(scope.PlayerPosition), $"{scenario} hit at {t:F2}: {scope.PlayerPosition}");
            if (scenario=="urgent splash") Check(scope.PlayerPosition.Length()<=7.5, "urgent escape unnecessarily left reachable melee range");
        }
        Check(moved && scope.PlayerPosition.Length()<=7.5 && scope.MoveCount==0,
            $"{scenario} failed to finish in melee through the shared executor: {scope.PlayerPosition}");
    }

    private static SafeZoneSolution SolveRange(FollowScope scope, DateTime now, float delay = 0)
    {
        var snapshot = scope.Manager.BuildSnapshot(now);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = new SafeZoneSolver().Solve(snapshot,
            new PlayerState(scope.PlayerPosition,0,6,false,false,0,true,snapshot.Version),
            new MovementCapability(6,6,delay,false,6,6), new SolveOptions { GenerateDebugData = true }, now);
        Console.WriteLine($"  range solve: {watch.Elapsed.TotalMilliseconds:F1} ms, expansions={result.DebugData?.SearchExpansions}, {result.Status}, target={result.TargetPosition}");
        return result;
    }

    private static void CheckPathOutside(SafeZoneSolution solution, IGeometryShape danger)
    {
        for (var i=1; i<solution.ExecutablePath.Count; ++i)
            Check(!danger.IntersectsSegment(solution.ExecutablePath[i-1].Position,solution.ExecutablePath[i].Position),
                "safe approach crossed pending hazard");
    }
}
