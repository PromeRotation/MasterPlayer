using System.Numerics;
using System.Reflection;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class NavigationTemporalContinuationRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    public static void Run(Action<string, Action> run)
    {
        foreach (var change in new[] { "none", "danger", "obstacle", "floor", "goal", "goal expiry", "superseded result", "owner", "second owner", "hold", "clear", "host", "rewind", "timeout" })
            run($"Navigation continuation revalidates a saved wait suffix: {change}", () =>
            {
                var move = new Move(); var host = false; var owner = new object();
                using var runtime = new SafeZoneRuntime(move, () => host, () => host) { ReplayClock = () => Epoch };
                runtime.RequestAutomaticMovement(owner, true, preferStraightPaths: true);
                var goal = runtime.Manager.AddGoalArea(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 10, 0),
                    [change == "goal expiry" ? new(Epoch, Epoch.AddSeconds(.8), 100, PositionPreference.Center) : GoalWindow.Persistent(Epoch, 100)]);
                runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
                var wait = new TimedWaypoint(Vector3.Zero, Epoch, Epoch.AddSeconds(1), true, 1);
                var shortWait = new TimedWaypoint(Vector3.UnitZ * .6f, Epoch.AddSeconds(1.1), Epoch.AddSeconds(1.12), true, 1);
                var solution = new SafeZoneSolution(SolutionStatus.Success, runtime.Manager.Version, Epoch, Epoch, null, [wait])
                {
                    UsesTemporalPlanning = true,
                    ContinuationPath = [wait, shortWait, TimedWaypoint.Pass(Vector3.UnitZ * 10, Epoch.AddSeconds(2.8))],
                };
                if (change == "superseded result")
                    runtime.Manager.UpdateArea(goal, new CircleTemplate(1), new StaticTransform(-Vector3.UnitZ * 10, 0));
                Dispatch(runtime, solution, Epoch);
                Check(!move.IsRunning && runtime.MovementStatus.HasControl, "The initial phase did not hold at its wait point.");
                var at = Epoch.AddSeconds(1.02);
                switch (change)
                {
                    case "danger": runtime.Registry.AddPersistentDanger(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 4, 0)); break;
                    case "obstacle": runtime.Registry.AddObstacle(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 4, 0)); break;
                    case "floor": runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 2)); break;
                    case "goal": runtime.Manager.UpdateArea(goal, new CircleTemplate(1), new StaticTransform(-Vector3.UnitZ * 10, 0)); break;
                    case "owner": runtime.RequestAutomaticMovement(owner, false); break;
                    case "second owner": runtime.RequestAutomaticMovement(new object(), true); break;
                    case "hold": runtime.RequestAutomaticMovement(owner, true, hold: true, preferStraightPaths: true); break;
                    case "clear": runtime.Clear(); break;
                    case "host": host = true; break;
                    case "rewind": at = Epoch.AddSeconds(-1); break;
                    case "timeout": at = Epoch.AddSeconds(11); break;
                }
                Dispatch(runtime, null, at);
                if (change != "none")
                {
                    Check(!move.IsRunning && move.Count == 1, "A stale suffix survived a safety or ownership change.");
                    return;
                }
                Check(move.IsRunning && move.Points[^1].Position == Vector3.UnitZ * 10 && move.Count == 2,
                    "A consumed short wait blocked departure while the worker had no replacement result.");
                Dispatch(runtime, null, at.AddSeconds(.02));
                Check(move.Count == 2 && move.IsRunning, "A safe resumed route was resent or stopped on the next frame.");
            });

        foreach (var optionalGoal in new[] { false, true })
        foreach (var change in new[] { "none", "partial re-plan", "danger", "obstacle", "floor", "new goal", "owner", "host" }
            .Concat(optionalGoal ? new[] { "follow removed", "follow inactive", "follow promoted" } : []))
            run($"Navigation continuation optional-follow={optionalGoal} revalidates a complete refuge exit: {change}", () =>
            {
                var move = new Move();
                var host = false;
                var owner = new object();
                using var runtime = new SafeZoneRuntime(move, () => host, () => host) { ReplayClock = () => Epoch };
                runtime.RequestAutomaticMovement(owner, true, goalDirectedTemporalPlanning: true);
                runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
                var follow = optionalGoal
                    ? runtime.Manager.AddGoalArea(new CircleTemplate(10), new StaticTransform(Vector3.Zero, 0),
                        [GoalWindow.Persistent(Epoch, -1, PositionPreference.Nearest)])
                    : default;
                var wait = new TimedWaypoint(Vector3.Zero, Epoch, Epoch.AddSeconds(1), true, 1);
                var solution = new SafeZoneSolution(SolutionStatus.Success, runtime.Manager.Version, Epoch, Epoch, null, [wait])
                {
                    UsesTemporalPlanning = true,
                    ContinuationPath = [wait, TimedWaypoint.Pass(Vector3.UnitZ * 5, Epoch.AddSeconds(2))],
                    RefinedGridResolution = .25f,
                };
                Dispatch(runtime, solution, Epoch);
                Check(!move.IsRunning && runtime.MovementStatus.HasControl, "A complete refuge route must hold until departure.");
                var replacement = change == "partial re-plan"
                    ? new SafeZoneSolution(SolutionStatus.Success, runtime.Manager.Version, Epoch, Epoch, -Vector3.UnitZ,
                        [TimedWaypoint.Pass(Vector3.Zero, Epoch), TimedWaypoint.Pass(-Vector3.UnitZ, Epoch.AddSeconds(.2))])
                        { UsesTemporalPlanning = true, IsPartialEscape = true }
                    : null;
                if (optionalGoal)
                    runtime.Manager.UpdateArea(follow, new CircleTemplate(12), new StaticTransform(Vector3.UnitX * 3, 0),
                        goalWindows: [GoalWindow.Persistent(Epoch.AddSeconds(.5), -1, PositionPreference.Nearest)],
                        incrementVersion: false);
                Dispatch(runtime, replacement, Epoch.AddSeconds(.5));
                Check(!move.IsRunning && runtime.MovementStatus.HasControl, "Refreshing follow or a partial re-plan cancelled the verified wait.");
                switch (change)
                {
                    case "danger": runtime.Registry.AddPersistentDanger(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 3, 0)); break;
                    case "obstacle": runtime.Registry.AddObstacle(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 3, 0)); break;
                    case "floor": runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 2)); break;
                    case "new goal": runtime.Registry.AddPersistentSafeArea(new CircleTemplate(1), new StaticTransform(-Vector3.UnitZ * 5, 0)); break;
                    case "owner": runtime.RequestAutomaticMovement(owner, false); break;
                    case "host": host = true; break;
                    case "follow removed": runtime.Manager.RemoveArea(follow); break;
                    case "follow inactive": runtime.Manager.UpdateArea(follow, goalWindows: []); break;
                    case "follow promoted": runtime.Manager.UpdateArea(follow,
                        goalWindows: [GoalWindow.Persistent(Epoch.AddSeconds(.5), 0)]); break;
                }
                Dispatch(runtime, null, Epoch.AddSeconds(1.02));
                if (change is not ("none" or "partial re-plan" or "follow removed" or "follow inactive"))
                {
                    Check(!move.IsRunning && move.Count == 1, "A refuge suffix survived changed safety or ownership.");
                    return;
                }
                Check(move.IsRunning && move.Points[^1].Position == Vector3.UnitZ * 5 && move.Count == 2,
                    "A complete escape lost its verified refuge exit after an optional follow refresh.");
                Check(runtime.MovementStatus.LatestSolution?.RefinedGridResolution == .25f,
                    "Resuming the suffix discarded the grid used to verify its narrow passage.");
                Dispatch(runtime, replacement, Epoch.AddSeconds(1.04));
                Check(move.IsRunning && move.Count == 2 && move.Points[^1].Position == Vector3.UnitZ * 5,
                    "A delayed partial re-plan stopped or reversed the still-safe complete phase.");
            });

        foreach (var priority in new[] { -1, 0 })
            run($"Navigation continuation distinguishes optional attack range from a mechanic destination: priority={priority}", () =>
            {
                using var runtime = new SafeZoneRuntime(new Move(), () => false, () => false);
                runtime.Setting.EnableSafeZone = true;
                runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
                runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0),
                    [new DangerWindow(Epoch.AddSeconds(2), Epoch.AddSeconds(5))]);
                var goal = new CircleGeometry(Vector3.Zero, 1);
                runtime.Manager.AddGoalArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0),
                    [GoalWindow.Persistent(Epoch, priority)]);
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                var solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, .08f, false, 6, 6), new SolveOptions { GoalDirectedTemporalPlanning = true }, Epoch);
                Check(solution.IsSuccess && !solution.IsPartialEscape && !solution.IsLeastDangerFallback,
                    "The destination policy lost the complete safe route.");
                var endpoint = solution.ContinuationPath.Count > 0
                    ? solution.ContinuationPath[^1].Position : solution.ExecutablePath[^1].Position;
                Check(priority < 0
                        ? !goal.Contains(endpoint) && !solution.UsesTemporalPlanning
                        : goal.Contains(endpoint) && solution.UsesTemporalPlanning,
                    "Optional uptime forced a timed return, or a real mechanic destination was abandoned.");
            });

        run("Navigation continuation rejects an early shortcut into a future hit", () =>
        {
            var move = new Move();
            using var runtime = new SafeZoneRuntime(move, () => false, () => false) { ReplayClock = () => Epoch };
            runtime.RequestAutomaticMovement(new object(), true, goalDirectedTemporalPlanning: true);
            runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 20));
            var endpoint = Vector3.UnitZ * 5;
            runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(endpoint, 0),
                [new DangerWindow(Epoch.AddSeconds(2.4), Epoch.AddSeconds(3))]);
            var wait = new TimedWaypoint(Vector3.Zero, Epoch, Epoch.AddSeconds(1), true, 1);
            var solution = new SafeZoneSolution(SolutionStatus.Success, runtime.Manager.Version, Epoch, Epoch, null, [wait])
            {
                UsesTemporalPlanning = true,
                ContinuationPath =
                [
                    wait,
                    TimedWaypoint.Pass(new Vector3(5, 0, -5), Epoch.AddSeconds(2.2)),
                    TimedWaypoint.Pass(new Vector3(5, 0, 5), Epoch.AddSeconds(3.9)),
                    TimedWaypoint.Pass(endpoint, Epoch.AddSeconds(4.8)),
                ],
            };
            Dispatch(runtime, solution, Epoch);
            Dispatch(runtime, null, Epoch.AddSeconds(1.02));
            Check(move.IsRunning && move.Points[^1].Position == endpoint, "The safe detour was discarded.");
            var position = Vector3.Zero;
            var arrival = 1.02;
            foreach (var point in move.Points)
            {
                arrival += Vector3.Distance(position, point.Position) / 6;
                position = point.Position;
            }
            Check(arrival > 3.5,
                "A shortcut arrived before the future hit and left the player standing in it instead of preserving the safe arrival interval.");
        });

        run("Navigation continuation is opt-in for race goals", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
            var wait = new TimedWaypoint(Vector3.Zero, Epoch, Epoch.AddSeconds(1), true, 1);
            var solution = new SafeZoneSolution(SolutionStatus.Success, 0, Epoch, Epoch, null, [wait])
            { UsesTemporalPlanning = true, ContinuationPath = [wait, TimedWaypoint.Pass(Vector3.UnitZ * 5, Epoch.AddSeconds(2))] };
            Dispatch(runtime, solution, Epoch); Dispatch(runtime, null, Epoch.AddSeconds(1.1));
            Check(!move.IsRunning && move.Count == 1, "Ordinary combat started using a race continuation.");
        });

        run("Navigation owner hard margins preserve other owners and clear on release", () =>
        {
            using var runtime = new SafeZoneRuntime(new Move(), () => false, () => false);
            var race = new object(); var other = new object();
            runtime.RequestAutomaticMovement(race, true, hardSafetyMargin: .1f);
            Check(Margin(runtime) == .1f, "The narrow lane margin did not reach the solver.");
            runtime.RequestAutomaticMovement(other, true);
            Check(Margin(runtime) == .5f, "A race margin weakened another owner's ordinary clearance.");
            runtime.RequestAutomaticMovement(other, false);
            Check(Margin(runtime) == .1f, "Removing the other owner did not restore race clearance.");
            runtime.RequestAutomaticMovement(race, true, hardSafetyMargin: float.NaN);
            Check(Margin(runtime) == .5f && runtime.Setting.RuntimeHardSafetyMargin == null, "Invalid clearance leaked into a solve.");
            runtime.RequestAutomaticMovement(race, true, hardSafetyMargin: .1f);
            runtime.Clear();
            Check(Margin(runtime) == .5f && runtime.Setting.RuntimeHardSafetyMargin == null, "Clear left a stale margin override.");
        });
    }

    private static float Margin(SafeZoneRuntime runtime) => ((SolveOptions)runtime.MovementStatus.GetType()
        .GetMethod("CaptureSolveOptions", Private)!.Invoke(runtime.MovementStatus, null)!).HardSafetyMargin;
    private static void Dispatch(SafeZoneRuntime runtime, SafeZoneSolution? solution, DateTime now)
    {
        var snapshot = runtime.Manager.BuildSnapshot(now);
        runtime.MovementStatus.GetType().GetMethod("Dispatch", Private)!.Invoke(runtime.MovementStatus,
            [solution, new PlayerState(Vector3.Zero, 0, 6, false, false, 0, true, snapshot.Version), snapshot, now]);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class Move : IGreenMoveSystem
    {
        public IReadOnlyList<GreenMovePoint> Points = []; public int Count;
        public bool IsRunning => Points.Count > 0;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? Points[0] : null, IsRunning ? Points[^1] : null, 0, Points.Count, Math.Max(0, Points.Count - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { Points = points; Count++; }
        public void Stop() => Points = [];
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => throw new NotSupportedException();
    }
}
