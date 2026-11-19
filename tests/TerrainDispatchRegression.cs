using System.Numerics;
using System.Reflection;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using MasterPlayer.Spatial.Terrain;
using PromeRotation.GreenMoveSystem;

internal static class TerrainDispatchRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var host in new[] { false, true })
            run($"Terrain first dispatch rechecks collision and preserves host ownership, host={host}", () =>
            {
                var moves = 0; var checks = 0;
                var movement = ScriptServiceProxy.Create<IGreenMoveSystem>((name, _) => name switch
                {
                    "get_IsRunning" => false,
                    "GetStatus" => default(GreenMoveStatus),
                    "MoveTo" => CountMove(),
                    "Stop" => null,
                    _ => throw new NotSupportedException(name),
                });
                object? CountMove() { ++moves; return null; }
                using var runtime = new SafeZoneRuntime(movement, () => false, () => host);
                runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
                var now = DateTime.UtcNow;
                var target = new Vector3(0, 0, 4);
                runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 10));
                runtime.Manager.AddGoalArea(new CircleTemplate(.8f), new StaticTransform(target, 0), [GoalWindow.Persistent(now, 100)]);
                var snapshot = runtime.Manager.BuildSnapshot(now);
                var system = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
                var allow = false;
                system.CheckTerrainTraversal = (from, to) =>
                {
                    ++checks;
                    if (GeometryHelper.DistanceXZ(from, to) > 2.001f) throw new InvalidOperationException("Native dispatch probe exceeded its lookahead.");
                    return allow;
                };
                var solution = SafeZoneSolution.Succeeded(snapshot.Version, now, now, target,
                    [TimedWaypoint.Pass(Vector3.Zero, now), TimedWaypoint.Pass(target, now.AddSeconds(1))]);
                var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var player = new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version);
                dispatch.Invoke(system, [solution, player, snapshot, now]);
                if (moves != 0 || system.HasControl || checks != (host ? 0 : 1))
                    throw new InvalidOperationException("A blocked initial route was sent, or another owner's route was queried.");
                allow = true;
                dispatch.Invoke(system, [solution, player, snapshot, now]);
                if (moves != (host ? 0 : 1) || system.HasControl == host)
                    throw new InvalidOperationException("Collision acceptance bypassed the host gate or failed to dispatch a safe route.");
            });
        run("Terrain survives combat cleanup while full scene cleanup discards it", () =>
        {
            var movement = ScriptServiceProxy.Create<IGreenMoveSystem>((name, _) => name switch
            {
                "get_IsRunning" => false,
                "GetStatus" => default(GreenMoveStatus),
                "Stop" => null,
                _ => throw new NotSupportedException(name),
            });
            using var runtime = new SafeZoneRuntime(movement, () => false, () => false);
            runtime.Setting.EnableSafeZone = true;
            var terrain = (LocalTerrainController)typeof(SafeZoneRuntime).GetField("terrain", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(runtime)!;
            var map = new LocalTerrainBoundary(Vector3.Zero, 3, Enumerable.Repeat(TerrainCellKind.Walkable, 9).ToArray(), new float[9]);
            typeof(LocalTerrainController).GetProperty(nameof(LocalTerrainController.Current), BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(terrain, map);
            runtime.Manager.SetFallbackBoundary(map);
            var now = DateTime.UtcNow;
            runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0), [new DangerWindow(now, now.AddSeconds(10))]);
            runtime.Manager.AddGoalArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0), [GoalWindow.Persistent(now, 1)]);
            runtime.RequestAutomaticMovement(new object(), true);
            runtime.ClearCombatState();
            var snapshot = runtime.Manager.BuildSnapshot(now);
            if (!ReferenceEquals(runtime.LocalTerrain, map) || !ReferenceEquals(snapshot.ArenaBoundary, map)
                || snapshot.DangerAreas.Count != 0 || snapshot.GoalAreas.Count != 0 || runtime.Setting.RuntimeMovementRequested)
                throw new InvalidOperationException("Combat cleanup retained battle state or discarded static terrain.");
            runtime.Clear();
            if (runtime.LocalTerrain != null || runtime.Manager.BuildSnapshot(now).ArenaBoundary != null)
                throw new InvalidOperationException("Full scene cleanup retained the previous scene's terrain.");
        });
        run("Terrain fallback footprint preserves danger margin and restores registered arena settings", () =>
        {
            var movement = ScriptServiceProxy.Create<IGreenMoveSystem>((name, _) => name switch
            {
                "get_IsRunning" => false,
                "GetStatus" => default(GreenMoveStatus),
                _ => throw new NotSupportedException(name),
            });
            using var runtime = new SafeZoneRuntime(movement, () => false, () => false);
            var capture = typeof(SafeZoneRebornMovementSystem).GetMethod("CaptureSolveOptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
            SolveOptions Options() => (SolveOptions)capture.Invoke(runtime.MovementStatus, null)!;
            runtime.Manager.SetFallbackBoundary(new CircleGeometry(Vector3.Zero, 10));
            if (Options().ArenaSafetyMargin != 0 || Options().HardSafetyMargin != .5f)
                throw new InvalidOperationException("Terrain doubled its footprint margin or changed AOE margin.");
            runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
            if (Options().ArenaSafetyMargin != null || Options().HardSafetyMargin != .5f)
                throw new InvalidOperationException("Terrain margin leaked into a registered mechanic arena.");
        });
    }
}
