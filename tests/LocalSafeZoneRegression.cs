using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using MasterPlayer.Radar;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class LocalSafeZoneRegression
{
    private static readonly DateTime Now = new(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc);
    private static readonly MovementCapability Capability = new(6, 6, 0, false, 6, 6);
    private static readonly PlayerState Player = new(Vector3.Zero, 0, 6, false, false, 0, true, 1);

    public static void Run(Action<string, Action> run)
    {
        foreach (var transition in new[] { "clear", "disabled", "missing player" })
            run($"Local runtime rejects a completed worker result after {transition}", () =>
            {
                var move = new FakeMove();
                using var runtime = CreateRuntime(move);
                var system = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
                var snapshot = runtime.Manager.BuildSnapshot(Now);
                var solution = new SafeZoneSolver().Solve(snapshot, Player, Capability, new(), Now);
                Check(solution.ExecutablePath.Count >= 2, "The stale result must contain a real movable route.");
                SeedCompletedWorker(system, solution);
                switch (transition)
                {
                    case "clear": runtime.Clear(); break;
                    case "disabled": runtime.Setting.EnableSafeZone = false; system.Update(Player, Capability, Now); break;
                    case "missing player": system.Update(null, Capability, Now); break;
                }
                runtime.Setting.EnableSafeZone = true;
                runtime.Manager.AddGoalArea(new CircleTemplate(0.5f), new StaticTransform(-Vector3.UnitX * 8, 0), [GoalWindow.Persistent(Now)]);
                typeof(SafeZoneRebornMovementSystem).GetMethod("CompletePendingSolve", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(system, [runtime.Manager.BuildSnapshot(Now)]);
                Check(system.LatestSolution == null && system.AsyncSolveAcceptedCount == 0 && system.AsyncSolveDiscardedCount == 1,
                    "Cancellation after worker completion must still prevent acceptance across lifecycle boundaries.");
                Check(move.MoveCount == 0, "A discarded result must never reach GreenMove.");
            });

        foreach (var hostOwns in new[] { false, true })
            run($"Local clear and dispose preserve movement ownership, host={hostOwns}", () =>
            {
                var move = new FakeMove();
                var runtime = new SafeZoneRuntime(move, () => false, () => hostOwns);
                runtime.Setting.EnableSafeZone = true;
                var handle = runtime.Registry.AddPersistentDanger(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0));
                move.MoveTo(new GreenMovePoint(Vector3.UnitX * 10));
                SetField(runtime.MovementStatus, "_hasControl", true);
                runtime.Clear();
                runtime.Clear();
                Check(!runtime.Manager.ContainsArea(handle) && !runtime.MovementStatus.HasControl,
                    "Clear/dispose must remove local areas and ownership.");
                runtime.Dispose();
                runtime.Dispose();
                Check(move.StopCount == (hostOwns ? 0 : 1) && move.IsRunning == hostOwns,
                    "Cleanup must stop its own route once and preserve a host-owned route.");
            });

        run("Local runtime suppresses movement throughout replay while retaining display data", () =>
        {
            var move = new FakeMove();
            using var runtime = CreateRuntime(move);
            var system = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
            var snapshot = runtime.Manager.BuildSnapshot(Now);
            SeedCompletedWorker(system, new SafeZoneSolver().Solve(snapshot, Player, Capability, new(), Now));
            move.MoveTo(new GreenMovePoint(Vector3.UnitX * 8));
            SetField(system, "_hasControl", true);
            for (var tick = 0; tick < 5; tick++)
                system.Update(Player, Capability, Now.AddMilliseconds(tick * 10), allowMovement: false);
            Check(move.MoveCount == 1 && move.StopCount == 1 && !system.HasControl,
                "Replay must release an old route and never dispatch a new one.");
            Check(system.LatestSolution != null && runtime.Manager.BuildSnapshot(Now).GoalAreas.Count == 1,
                "Disabling movement must leave solver/display data available.");
        });

        run("Dead players cannot restart local movement when new enemy casts arrive", () =>
        {
            var objects = typeof(Svc).GetProperty(nameof(Svc.Objects))!;
            var previous = objects.GetValue(null);
            var deadPlayer = ScriptServiceProxy.Create<IPlayerCharacter>((name, _) => name == "get_IsDead"
                ? true : throw new InvalidOperationException("Dead player state must not be sampled: " + name));
            objects.SetValue(null, ScriptServiceProxy.Create<IObjectTable>((name, _) => name == "get_LocalPlayer"
                ? deadPlayer : throw new NotSupportedException(name)));
            try
            {
                var move = new FakeMove();
                using var runtime = CreateRuntime(move);
                move.MoveTo(new GreenMovePoint(Vector3.UnitX * 8));
                SetField(runtime.MovementStatus, "_hasControl", true);
                for (var i = 0; i < 3; i++)
                {
                    runtime.Registry.AddPersistentDanger(new CircleTemplate(3 + i), new StaticTransform(Vector3.Zero, 0));
                    runtime.Update(1, true);
                }
                Check(move.MoveCount == 1 && move.StopCount == 1 && runtime.MovementStatus.AsyncSolveStartedCount == 0,
                    "Death must continuously gate both solve and dispatch, even after new danger registrations.");
            }
            finally { objects.SetValue(null, previous); }
        });

        var center = new Vector3(13, 2, -17);
        IGeometryShape[] shapes =
        [
            new CircleGeometry(center, 5), new DonutGeometry(center, 3, 8),
            new FanGeometry(center, 0.73f, 8, 1.3f), new RectangleGeometry(center, 0.73f, 3, 9),
            new RayRectangleGeometry(center, 0.73f, -2, 5, 9),
            new LocalPolygonGeometry(center, 0.73f, [new(-3, -4), new(5, -2), new(2, 6)]),
            new WorldPolygonGeometry([center + new Vector3(-3, 0, -4), center + new Vector3(5, 0, -2), center + new Vector3(2, 0, 6)])
        ];
        foreach (var shape in shapes)
            run($"Host drawing conversion preserves {shape.GetType().Name}", () =>
            {
                var host = HostDrawingGeometry.Convert(shape);
                Check(host.GetType().Assembly == typeof(PromeRotation.Plugin).Assembly,
                    "Built-in shapes must use host concrete geometry types for DrawManager dispatch.");
                var localContours = shape.GetContours();
                var hostContours = host.GetContours();
                Check(localContours.Count == hostContours.Count && localContours.Zip(hostContours)
                    .All(pair => pair.First.SequenceEqual(pair.Second)), "Drawing must retain all contours, including donut holes and polygon pose.");
                for (var x = -10f; x <= 10; x += 0.5f)
                    for (var z = -10f; z <= 10; z += 0.5f)
                        Check(shape.Contains(center + new Vector3(x, 0, z)) == host.Contains(center + new Vector3(x, 0, z)),
                            "The rendered geometry must describe the same occupied points as the local solver.");
            });
    }

    private static SafeZoneRuntime CreateRuntime(FakeMove move)
    {
        var runtime = new SafeZoneRuntime(move, () => false, () => false);
        runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
        runtime.Registry.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
        runtime.Manager.AddGoalArea(new CircleTemplate(0.5f), new StaticTransform(Vector3.UnitX * 8, 0), [GoalWindow.Persistent(Now)]);
        return runtime;
    }

    private static void SeedCompletedWorker(SafeZoneRebornMovementSystem system, SafeZoneSolution solution)
    {
        var type = typeof(SafeZoneRebornMovementSystem).GetNestedType("AsyncSolveResult", BindingFlags.NonPublic)!;
        var result = type.GetMethod("Completed")!.Invoke(null, [solution.SourceSnapshotVersion, solution, false]);
        var task = typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, [result])!;
        SetField(system, "_pendingSolveTask", task);
        SetField(system, "_pendingSolveCts", new CancellationTokenSource());
    }

    private static void SetField(object target, string name, object value)
        => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _route = [];
        public int MoveCount { get; private set; }
        public int StopCount { get; private set; }
        public bool IsRunning => _route.Count > 0;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _route[0] : null,
            IsRunning ? _route[^1] : null, 0, _route.Count, _route.Count);
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _route = points; MoveCount++; }
        public void Stop() { _route = []; StopCount++; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => throw new NotSupportedException();
    }
}
