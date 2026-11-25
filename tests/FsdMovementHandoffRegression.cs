using System.Numerics;
using System.Reflection;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class FsdMovementHandoffRegression
{
    private static readonly DateTime Epoch = new(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(Action<string, Action> run)
    {
        run("FSD handoff ordinary chest goals cannot reserve idle or terrain-rejected movement for 35 minutes", () =>
        {
            using var runtime = Create(out var move);
            var calls = 0;
            runtime.SetMovementHandoff(active => { if (active) calls++; return true; });
            foreach (var priority in new[] { 20, 2000 })
                runtime.Manager.AddGoalArea(new CircleTemplate(3), new StaticTransform(Vector3.UnitZ * 38, 0),
                    [GoalWindow.Persistent(Epoch, priority) with { RequiresMovementHandoff = false }]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            foreach (var now in new[] { Epoch, Epoch.AddMinutes(35) })
            {
                Dispatch(runtime, null, Vector3.Zero, snapshot, now);
                Dispatch(runtime, Idle(snapshot, now), Vector3.Zero, snapshot, now);
                Check(!runtime.MovementStatus.MechanicMovementActive && !runtime.MovementStatus.HasControl,
                    "Persistent ordinary chest goals reserved an idle handoff.");
            }
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).CheckTerrainTraversal = (_, _) => false;
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(calls == 0 && move.Moves == 0 && !runtime.MovementStatus.MechanicMovementActive,
                "A terrain-rejected ordinary goal interrupted FSD.");
            runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch, Epoch.AddSeconds(1))]);
            snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, null, Vector3.Zero, snapshot, Epoch);
            Check(runtime.MovementStatus.MechanicMovementActive, "Ordinary goal classification suppressed real damage preemption.");
            Dispatch(runtime, null, Vector3.UnitZ * 6, snapshot, Epoch.AddSeconds(2));
            Check(!runtime.MovementStatus.MechanicMovementActive, "Ordinary goals prolonged the completed danger's lease.");
        });

        run("FSD handoff mechanic goals retain stationary ownership until removal", () =>
        {
            using var runtime = Create(out var move);
            var goal = runtime.Manager.AddGoalArea(new CircleTemplate(1), new StaticTransform(Vector3.Zero, 0),
                [GoalWindow.Persistent(Epoch, 100)]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(runtime.MovementStatus.MechanicMovementActive && !runtime.MovementStatus.HasControl && move.Moves == 0,
                "A required stationary mechanic goal yielded to ordinary floor movement.");
            runtime.Manager.RemoveArea(goal);
            snapshot = runtime.Manager.BuildSnapshot(Epoch.AddSeconds(1));
            Dispatch(runtime, null, Vector3.Zero, snapshot, Epoch.AddSeconds(1));
            Check(!runtime.MovementStatus.MechanicMovementActive, "Removing a mechanism goal left an ownership lease.");
        });

        run("FSD handoff session transitions stop old routes and repeated requests preserve current mechanisms", () =>
        {
            using var runtime = Create(out var move);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            runtime.SetFsdSessionActive(true);
            Check(runtime.FsdSessionActive && !move.IsRunning && !runtime.MovementStatus.MechanicMovementActive,
                "Starting FSD retained an ordinary route from the previous responsibility.");
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            runtime.SetFsdSessionActive(true);
            Check(move.IsRunning && runtime.MovementStatus.MechanicMovementActive,
                "An idempotent session refresh reset active mechanism movement.");
            runtime.SetFsdSessionActive(false);
            Check(!runtime.FsdSessionActive && !move.IsRunning && !runtime.MovementStatus.MechanicMovementActive,
                "Ending FSD left the previous session's route or ownership.");
        });

        run("Terrain publication retains only a running escape fully covered by the old boundary", () =>
        {
            using var runtime = Create(out var move);
            var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
            var boundary = new CircleGeometry(Vector3.Zero, 25);
            Check(!movement.ShouldRetainTerrain(boundary, Vector3.Zero), "An idle runtime retained terrain.");
            runtime.Manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(2.7), Epoch.AddSeconds(3.3))]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(movement.ShouldRetainTerrain(boundary, Vector3.UnitZ), "A running covered escape failed to retain terrain.");
            runtime.Setting.EnableAutoMovement = false;
            Check(!movement.ShouldRetainTerrain(boundary, Vector3.UnitZ), "Disabled movement retained terrain publication.");
            runtime.Setting.EnableAutoMovement = true;
            Check(!movement.ShouldRetainTerrain(new CircleGeometry(Vector3.Zero, 4), Vector3.UnitZ),
                "A route outside the old map prevented terrain replacement.");
            Check(!movement.ShouldRetainTerrain(boundary, Vector3.UnitX * 26), "A displaced player retained an uncovered map.");
            move.Stop();
            Check(movement.MechanicMovementActive && !movement.ShouldRetainTerrain(boundary, Vector3.UnitZ * 6),
                "Reaching safety waited for the entire AOE ownership lease to end.");
        });
        run("Terrain publication does not wait for ordinary optional movement", () =>
        {
            using var runtime = Create(out _);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(!((SafeZoneRebornMovementSystem)runtime.MovementStatus).ShouldRetainTerrain(
                new CircleGeometry(Vector3.Zero, 25), Vector3.Zero), "Ordinary following froze terrain publication.");
        });
        run("FSD handoff direct acknowledgement denial retries without writing movement", () =>
        {
            using var runtime = Create(out var move);
            var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
            var accepted = false;
            runtime.SetMovementHandoff(_ => accepted);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            movement.SetDirectTraversal(Vector3.UnitZ * 6, Epoch.AddSeconds(1));
            movement.Update(Player(Vector3.Zero, snapshot), new(6, 6, 0, false, 6, 6), Epoch);
            Check(move.Moves == 0 && movement.MechanicMovementActive, "A denied direct route wrote input or lost its pending handoff.");
            accepted = true;
            movement.Update(Player(Vector3.Zero, snapshot), new(6, 6, 0, false, 6, 6), Epoch.AddSeconds(.02));
            Check(move.Moves == 1, "An acknowledged direct route failed to retry.");
        });

        run("FSD handoff validates direct shortcuts before requesting ownership", () =>
        {
            foreach (var terrainSafe in new[] { false, true })
            {
                using var runtime = Create(out var move);
                var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
                var calls = 0;
                runtime.SetMovementHandoff(active => { if (active) { Check(move.Moves == 0, "Handoff followed the first direct output."); calls++; } return true; });
                movement.CheckTerrainTraversal = (_, _) => terrainSafe;
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                movement.SetDirectTraversal(Vector3.UnitZ * 6, Epoch.AddSeconds(1));
                movement.Update(Player(Vector3.Zero, snapshot), new(6, 6, 0, false, 6, 6), Epoch);
                Check(terrainSafe ? calls == 1 && move.Moves == 1 : calls == 0 && move.Moves == 0,
                    "Direct traversal acquired ownership before validation or dispatched without an acknowledgement.");
            }
        });

        run("FSD handoff idle at a new mandatory goal stops an optional escape destination", () =>
        {
            using var runtime = Create(out var move);
            runtime.Manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(2.7), Epoch.AddSeconds(3.3))]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            runtime.Manager.AddGoalArea(new CircleTemplate(.5f), new StaticTransform(Vector3.UnitZ, 0),
                [GoalWindow.Persistent(Epoch, 100)]);
            snapshot = runtime.Manager.BuildSnapshot(Epoch.AddSeconds(.1));
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.UnitZ, snapshot, Epoch.AddSeconds(.1));
            Check(!move.IsRunning, "A completed higher-priority mechanic goal failed to stop optional movement.");
        });

        run("FSD handoff respects an idle hold from a goal-directed mechanic", () =>
        {
            using var runtime = Create(out var move);
            runtime.Setting.RuntimeGoalDirectedTemporalPlanning = true;
            runtime.Manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(2.7), Epoch.AddSeconds(3.3))]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Dispatch(runtime, Idle(snapshot, Epoch.AddSeconds(.1)), Vector3.UnitZ, snapshot, Epoch.AddSeconds(.1));
            Check(!move.IsRunning, "Generic escape continuity overrode an explicit mechanic hold.");
        });

        run("FSD handoff keeps a verified escape moving when a worker publishes a short idle candidate", () =>
        {
            using var runtime = Create(out var move);
            runtime.Manager.AddDangerArea(new CircleTemplate(3), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(2.7), Epoch.AddSeconds(3.3))]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(move.Moves == 1 && move.IsRunning, "The pending-hit escape did not start.");
            for (var frame = 1; frame <= 10; ++frame)
                Dispatch(runtime, Idle(snapshot, Epoch.AddSeconds(frame * .02)), Vector3.UnitZ, snapshot, Epoch.AddSeconds(frame * .02));
            Check(move.Moves == 1 && move.IsRunning && !move.Events.Contains("stop-mp"),
                "An idle worker candidate stopped a verified escape inside the pending AOE.");
            runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.UnitZ * 5, 0), [new(Epoch, null)]);
            snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.UnitZ, snapshot, Epoch.AddSeconds(.25));
            Check(!move.IsRunning, "Continuity kept a route blocked by newly observed damage.");
        });

        run("FSD handoff ignores obsolete routes after the player is safe", () =>
        {
            using var runtime = Create(out var move);
            runtime.Manager.ClearAllAreas();
            runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 25));
            var calls = 0;
            runtime.SetMovementHandoff(_ => { calls++; return true; });
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            var stale = Route(snapshot, Epoch);
            for (var frame = 0; frame < 20; ++frame)
                Dispatch(runtime, stale, Vector3.UnitZ * 6, snapshot, Epoch.AddSeconds(frame * .02));
            Check(calls == 0 && move.Moves == 0 && !runtime.MovementStatus.MechanicMovementActive,
                $"An obsolete route caused {calls} needless FSD ownership changes.");
        });

        run("FSD handoff does not cancel FSD for a terrain-rejected optional route", () =>
        {
            using var runtime = Create(out var move);
            var calls = 0;
            runtime.SetMovementHandoff(_ => { calls++; return true; });
            ((SafeZoneRebornMovementSystem)runtime.MovementStatus).CheckTerrainTraversal = (_, _) => false;
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(calls == 0 && move.Moves == 0, "A rejected route interrupted FSD before MP had usable output.");
        });

        run("FSD handoff revokes FSD before the first MP route and stops MP before release", () =>
        {
            using var runtime = Create(out var move);
            var events = move.Events;
            runtime.SetMovementHandoff(active =>
            {
                Check(runtime.MovementStatus.MechanicMovementActive == active, "The live scalar lagged behind the handoff.");
                if (!active) Check(!move.IsRunning, "FSD resumed before MP stopped its executor.");
                events.Add(active ? "yield-fsd" : "resume-fsd");
                return true;
            });
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(events.SequenceEqual(new[] { "yield-fsd", "move-mp" }), "MP dispatched before the synchronous acknowledgement.");
            runtime.SuspendMovement();
            Check(events.TakeLast(2).SequenceEqual(new[] { "stop-mp", "resume-fsd" }), "Release ordering allowed concurrent movement.");
        });

        run("FSD handoff denied acknowledgement prevents MP output and retries", () =>
        {
            using var runtime = Create(out var move);
            bool accepted = false;
            runtime.SetMovementHandoff(_ => accepted);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(move.Moves == 0 && runtime.MovementStatus.MechanicMovementActive, "A refused handoff still wrote movement.");
            accepted = true;
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(move.Moves == 1, "A later acknowledgement did not resume MP dispatch.");
        });

        run("FSD handoff holds during cast and escape waits without actual movement control", () =>
        {
            foreach (bool casting in new[] { false, true })
            {
                using var runtime = Create(out var move);
                var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
                runtime.Setting.AttackUptimeExpiresAt = Epoch.AddSeconds(10);
                runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0),
                    [new(Epoch.AddSeconds(4), Epoch.AddSeconds(4.6))]);
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                var player = Player(Vector3.Zero, snapshot) with { IsCasting = casting, CastTimeRemaining = casting ? 1 : 0 };
                InvokeDispatch(movement, Route(snapshot, Epoch), player, snapshot, Epoch);
                Check(!move.IsRunning && !movement.HasControl && movement.MechanicMovementActive,
                    "A safe wait released FSD's mechanism pause.");
                Check(movement.PhaseText == (casting ? "等读条" : "等待撤离时机"), "The regression did not exercise the intended wait.");
            }
        });

        run("FSD handoff keeps the safe refuge until related waves end, ignoring remote hazards", () =>
        {
            using var runtime = Create(out var move);
            var releases = 0;
            runtime.SetMovementHandoff(active => { if (!active) releases++; return true; });
            runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(1), Epoch.AddSeconds(1.6))]);
            runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.UnitX * 20, 0), [new(Epoch, null)]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, null, Vector3.Zero, snapshot, Epoch);
            Check(runtime.MovementStatus.MechanicMovementActive && move.Moves == 0, "Pending asynchronous planning did not reserve movement.");
            var refuge = Vector3.UnitZ * 6;
            Dispatch(runtime, Idle(snapshot, Epoch), refuge, snapshot, Epoch.AddSeconds(.5));
            Check(runtime.MovementStatus.MechanicMovementActive && releases == 0, "Reaching a safe point prematurely resumed the chest route.");
            runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(refuge, 0),
                [new(Epoch.AddSeconds(2), Epoch.AddSeconds(2.6))]);
            snapshot = runtime.Manager.BuildSnapshot(Epoch.AddSeconds(.6));
            Dispatch(runtime, null, refuge, snapshot, Epoch.AddSeconds(.6));
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.UnitZ * 10, snapshot, Epoch.AddSeconds(2));
            Check(runtime.MovementStatus.MechanicMovementActive && releases == 0, "The second wave lost the mechanism lease.");
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.UnitZ * 10, snapshot, Epoch.AddSeconds(3));
            Check(!runtime.MovementStatus.MechanicMovementActive && releases == 1, "Finished waves or an unrelated persistent hazard blocked chest resumption.");
        });

        run("FSD handoff does not reserve movement for distant hazards or optional attack goals", () =>
        {
            using var runtime = Create(out _);
            runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(Vector3.UnitX * 20, 0), [new(Epoch, null)]);
            runtime.Manager.AddGoalArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0), [GoalWindow.Persistent(Epoch, -1)]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            Check(!runtime.MovementStatus.MechanicMovementActive, "Unrelated registrations monopolized FSD movement.");
        });

        run("FSD handoff releases a completed direct route despite repeated target refresh", () =>
        {
            using var runtime = Create(out var move);
            var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
            var target = Vector3.UnitZ * 6;
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
            for (int i = 0; i < 4; ++i)
            {
                var now = Epoch.AddSeconds(1 + i * .05);
                movement.SetDirectTraversal(target, now.AddSeconds(.25), [target], sampledAt: now);
                movement.Update(Player(target, snapshot), new(6, 6, 0, false, 6, 6), now);
                Check(!movement.HasControl && !movement.MechanicMovementActive && !move.IsRunning,
                    "An already reached direct request retained FSD's movement lease.");
            }
        });

        run("FSD handoff releases after escaping a persistent or relocated danger", () =>
        {
            using var runtime = Create(out _);
            runtime.Manager.AddDangerArea(new CircleTemplate(2), new StaticTransform(Vector3.Zero, 0), [new(Epoch, null)]);
            var snapshot = runtime.Manager.BuildSnapshot(Epoch);
            Dispatch(runtime, null, Vector3.Zero, snapshot, Epoch);
            Check(runtime.MovementStatus.MechanicMovementActive, "Persistent damage at the player did not preempt FSD.");
            Dispatch(runtime, Idle(snapshot, Epoch), Vector3.UnitZ * 6, snapshot, Epoch.AddSeconds(1));
            Check(!runtime.MovementStatus.MechanicMovementActive, "A permanent hazard left behind indefinitely blocked chest work.");
        });

        foreach (var lifecycle in new[] { "reset", "missing-player", "disabled", "replay", "dispose", "detach" })
            run($"FSD handoff releases on {lifecycle}", () =>
            {
                using var runtime = Create(out var move);
                var releases = 0;
                runtime.SetMovementHandoff(active => { if (!active) { releases++; Check(!move.IsRunning, "Release preceded MP stop."); } return true; });
                var snapshot = runtime.Manager.BuildSnapshot(Epoch);
                Dispatch(runtime, Route(snapshot, Epoch), Vector3.Zero, snapshot, Epoch);
                var movement = (SafeZoneRebornMovementSystem)runtime.MovementStatus;
                switch (lifecycle)
                {
                    case "reset": movement.Reset(); break;
                    case "missing-player": movement.Update(null, default, Epoch); break;
                    case "disabled": runtime.SuspendMovement(); break;
                    case "replay": movement.Update(Player(Vector3.Zero, snapshot), new(6, 6, 0, false, 6, 6), Epoch, false); break;
                    case "dispose": runtime.Dispose(); break;
                    case "detach": runtime.SetMovementHandoff(null); break;
                }
                Check(!movement.MechanicMovementActive && !move.IsRunning && releases == 1, "Lifecycle failed to revoke the lease exactly once.");
            });
    }

    private static SafeZoneRuntime Create(out TrackingMove move)
    {
        move = new();
        var runtime = new SafeZoneRuntime(move, () => false, () => false);
        runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
        runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 25));
        runtime.Manager.AddGoalArea(new CircleTemplate(10), new StaticTransform(Vector3.Zero, 0), [GoalWindow.Persistent(Epoch, -1)]);
        return runtime;
    }

    private static SafeZoneSolution Route(SafeZoneSnapshot snapshot, DateTime now) =>
        SafeZoneSolution.Succeeded(snapshot.Version, now, now, Vector3.UnitZ * 6,
            [TimedWaypoint.Pass(Vector3.Zero, now), TimedWaypoint.Pass(Vector3.UnitZ * 6, now.AddSeconds(1))]);
    private static SafeZoneSolution Idle(SafeZoneSnapshot snapshot, DateTime now) =>
        SafeZoneSolution.Succeeded(snapshot.Version, now, now, null, null);
    private static PlayerState Player(Vector3 position, SafeZoneSnapshot snapshot) => new(position, 0, 6, false, false, 0, true, snapshot.Version);
    private static void Dispatch(SafeZoneRuntime runtime, SafeZoneSolution? solution, Vector3 position, SafeZoneSnapshot snapshot, DateTime now) =>
        InvokeDispatch(runtime.MovementStatus, solution, Player(position, snapshot), snapshot, now);
    private static void InvokeDispatch(object movement, SafeZoneSolution? solution, PlayerState player, SafeZoneSnapshot snapshot, DateTime now) =>
        movement.GetType().GetMethod("Dispatch", Private)!.Invoke(movement, [solution, player, snapshot, now]);
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }

    private sealed class TrackingMove : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> points = [];
        internal List<string> Events { get; } = [];
        internal int Moves { get; private set; }
        public bool IsRunning => points.Count > 0;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? points[0] : null,
            IsRunning ? points[^1] : null, 0, points.Count, Math.Max(0, points.Count - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> path) { Events.Add("move-mp"); Moves++; points = path; }
        public void Stop() { Events.Add("stop-mp"); points = []; }
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> path) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public void SkipCurrent() => points = points.Skip(1).ToArray();
    }
}
