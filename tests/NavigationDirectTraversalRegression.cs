using System.Numerics;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using PromeRotation.GreenMoveSystem;

internal static class NavigationDirectTraversalRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Direct traversal jump revalidates the admitted timing and rejects changed hazards", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            var landing = new Vector3(0, 0, 4);
            runtime.RequestAutomaticMovement(owner, true);
            // This pulse has cleared with the direct traversal's .1s end
            // cushion. Ordinary path dispatch uses a longer .5s cushion.
            runtime.Manager.AddDangerArea(new CircleTemplate(.4f), new StaticTransform(Vector3.Zero, 0),
                [new(Epoch.AddSeconds(-1), Epoch.AddSeconds(-.2))]);
            runtime.RequestDirectTraversal(owner, landing, Epoch);
            Tick(runtime, Vector3.Zero, Epoch, true);
            Check(move.IsRunning && runtime.CommitJumpMovement(owner, landing, Vector3.Zero, 6, Epoch),
                "The verified direct run was rejected under a different timing contract.");
            runtime.ReleaseCommittedMovement(owner);
            var danger = runtime.Manager.AddDangerArea(new CircleTemplate(1), new StaticTransform(landing, 0), [new(Epoch, null)]);
            Check(!runtime.CommitJumpMovement(owner, landing, Vector3.Zero, 6, Epoch), "A changed landing hazard authorized flight.");
            runtime.Manager.RemoveArea(danger);
            runtime.Manager.SetArenaBoundary(new CircleGeometry(Vector3.Zero, 2));
            Check(!runtime.CommitJumpMovement(owner, landing, Vector3.Zero, 6, Epoch), "An unsupported landing authorized flight.");
        });
        foreach (var locked in new[] { false, true })
        foreach (var reason in new[] { "owner", "second owner", "hold", "expiry", "clear", "host" })
            run($"Direct traversal releases on {reason} locked={locked}", () =>
            {
                var move = new Move(); var host = false;
                using var runtime = new SafeZoneRuntime(move, () => host, () => host);
                runtime.Setting.EnableSafeZone = true;
                var owner = new object(); var other = new object();
                runtime.RequestAutomaticMovement(owner, true);
                runtime.RequestDirectTraversal(other, Vector3.UnitZ * 5, Epoch);
                Check(!move.IsRunning, "Unregistered owner acquired input.");
                runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch,
                    motion: locked ? new([3], [true]) : null);
                Tick(runtime, Vector3.Zero, Epoch);
                Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
                Check(move.IsRunning, "Owned route did not start.");
                runtime.RequestDirectTraversal(other, null, Epoch);
                Check(move.IsRunning, "Unrelated owner revoked the route.");
                var now = Epoch.AddSeconds(.1);
                switch (reason)
                {
                    case "owner": runtime.RequestAutomaticMovement(owner, false); break;
                    case "second owner": runtime.RequestAutomaticMovement(other, true); break;
                    case "hold": runtime.RequestAutomaticMovement(owner, true, hold: true); break;
                    case "expiry": now = Epoch.AddSeconds(.26); break;
                    case "clear": runtime.Clear(); break;
                    case "host": host = true; break;
                }
                Tick(runtime, Vector3.Zero, now);
                // Another controller's input is never stopped on a host takeover.
                Check(!runtime.MovementStatus.HasControl, "Direct lease retained local control.");
                if (reason != "host") Check(!move.IsRunning, "Revoked route kept moving.");
            });
        run("Direct traversal retains the complete locked queue and rejects displacement", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 2), new(0, 0, 7), new(0, 0, 12)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([6, 3, 3], [false, true, true]));
            Tick(runtime, Vector3.Zero, Epoch);
            Check(move.GetStatus().TotalCount == 3, "The approved suffix was omitted from the input queue.");
            move.SkipCurrent(); Tick(runtime, new(0, 0, 2.1f), Epoch.AddSeconds(.05), true);
            var hazard = runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(new(0, 0, 7), 0), [new(Epoch, null)]);
            Tick(runtime, new(0, 0, 3), Epoch.AddSeconds(.1), true);
            Check(move.Count == 1 && move.IsRunning, "A changed forecast restarted an acquired native slide.");
            Tick(runtime, new(2, 0, 3), Epoch.AddSeconds(.15), true);
            Check(!move.IsRunning && !runtime.MovementStatus.HasControl, "Displacement kept a stale locked direction.");
            runtime.Manager.RemoveArea(hazard);
            for (var i = 0; i < 3; ++i)
            {
                var now = Epoch.AddSeconds(.16 + i * .01);
                runtime.RequestDirectTraversal(owner, route[^1], now, route, motion: new([6, 3, 3], [false, true, true]));
                Tick(runtime, new(2, 0, 3), now);
            }
            Check(move.Count == 1 && !move.IsRunning, "A refreshed lease redispatched the displaced generation.");
            var replacement = new Vector3(2, 0, 8);
            runtime.RequestDirectTraversal(owner, replacement, Epoch.AddSeconds(.2), motion: new([3], [true]));
            Tick(runtime, new(2, 0, 3), Epoch.AddSeconds(.2));
            Check(move.Count == 1 && !move.IsRunning && runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.2)).InputLocked,
                "A replacement generation redirected the physical slide after displacement.");
            runtime.RequestDirectTraversal(owner, replacement, Epoch.AddSeconds(2.3), motion: new([3], [true]));
            Tick(runtime, new(2, 0, 3), Epoch.AddSeconds(2.3));
            Check(move.Count == 2 && move.IsRunning, "A fresh route could not recover after the physical slide ended.");
        });
        run("Direct traversal does not inherit walking input when entering the first locked segment", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 2), new(0, 0, 7)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([6, 3], [false, true]));
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(0, 0, 1), Epoch.AddSeconds(.03), true);
            move.SkipCurrent();
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[1], 0), [new(Epoch, null)]);
            Tick(runtime, route[0], Epoch.AddSeconds(.06), true);
            Check(!move.IsRunning && !runtime.MovementStatus.HasControl, "Walking input bypassed validation before ice acquired control.");
        });
        run("Direct traversal carries an acquired slide across early locked queue advancement", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3], [true, true]));
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
            move.SkipCurrent();
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[1], 0), [new(Epoch, null)]);
            Tick(runtime, new(0, 0, 4.7f), Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.IsRunning, "Early queue advancement released the previous physical slide.");
        });
        run("Direct traversal validates a planned ice approach before the input lock is active", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10)];
            var motion = new DirectTraversalMotion([3, 3], [true, true], InputLockActive: false);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            Check(!runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.03)).InputLocked,
                "A planned first slide created a physical lease while still approaching ice.");
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[0], 0), [new(Epoch, null)]);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            Check(!move.IsRunning && runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06)).Rejected,
                "Walking toward a planned locked point bypassed updated hazard validation.");
        });
        run("Direct traversal permits a safe replacement before the planned ice lock is active", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var motion = new DirectTraversalMotion([3], [true], InputLockActive: false);
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            var replacement = new Vector3(2, 0, 5);
            runtime.RequestDirectTraversal(owner, replacement, Epoch.AddSeconds(.06), motion: motion);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(move.Count == 2 && move.IsRunning && execution.HasControl && !execution.InputLocked
                && execution.Path.SequenceEqual([replacement]), "A planned slide prevented steering before the actual lock.");
        });
        run("Direct traversal requires current-point validation for an unlocked planned ice suffix", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            var motion = new DirectTraversalMotion([3, 3, 3], [true, true, true], InputLockActive: false);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[0], 0), [new(Epoch, null)]);
            Vector3[] amended = [route[0], new(2, 0, 10), new(2, 0, 15)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended, motion: motion);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(move.Clears == 0 && move.Appends == 0 && !execution.HasControl && execution.Rejected
                && !execution.InputLocked && execution.Path.SequenceEqual(route),
                "An unlocked ice approach treated its current waypoint as an irreversible acquired slide.");
        });
        run("Direct traversal does not carry a planned ice commitment across an unlocked native seam", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10)];
            var motion = new DirectTraversalMotion([3, 3], [true, true], InputLockActive: false);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            move.SkipCurrent();
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[1], 0), [new(Epoch, null)]);
            Tick(runtime, Vector3.UnitZ * 4.7f, Epoch.AddSeconds(.06), true);
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(!move.IsRunning && execution.Rejected && !execution.InputLocked,
                "The planned previous slide made the next target irreversible without an active game lock.");
        });
        foreach (var locked in new[] { false, true })
            run($"Direct traversal applies nonlinear occupancy timing only to locked segments locked={locked}", () =>
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
                runtime.Manager.AddDangerArea(new CircleTemplate(.2f), new StaticTransform(Vector3.UnitZ * 4, 0),
                    [new(Epoch.AddSeconds(.8), Epoch.AddSeconds(.9))]);
                runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch,
                    motion: new([3], [locked], InputLockActive: false, Timing: new OccupancyTiming()));
                Tick(runtime, Vector3.Zero, Epoch);
                Check(locked ? !move.IsRunning && runtime.GetDirectTraversalExecution(owner, Epoch).Rejected
                    : move.IsRunning && move.Count == 1,
                    "Direct timing clipped an occupancy envelope to a uniform fraction or changed ordinary walking.");
            });
        run("Direct traversal samples a newly active input lock without restarting its queue", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var motion = new DirectTraversalMotion([3], [true], InputLockActive: false);
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch.AddSeconds(.06),
                motion: motion with { InputLockActive = true });
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(Vector3.UnitZ * 5, 0), [new(Epoch, null)]);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Stops == 0 && move.IsRunning
                && runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06)).InputLocked,
                "An observed game lock was ignored or reacquired by replacing native input.");
        });
        run("Direct traversal removes its physical lease as soon as the sampled input lock ends", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var motion = new DirectTraversalMotion([3], [true]);
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            Check(runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.03)).InputLocked, "Active slide did not acquire its lease.");
            var unlocked = motion with { InputLockActive = false };
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch.AddSeconds(.04), motion: unlocked);
            Check(!runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.04)).InputLocked,
                "An ended game status left a stale physical lease.");
            var replacement = new Vector3(2, 0, 5);
            runtime.RequestDirectTraversal(owner, replacement, Epoch.AddSeconds(.06), motion: unlocked);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            Check(move.Count == 2 && move.IsRunning && runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06)).HasControl,
                "Normal movement waited for the synthetic lease after the game lock ended.");
        });
        run("Direct traversal requires a replacement generation to acquire its own input", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            runtime.RequestDirectTraversal(owner, new(0, 0, 5), Epoch, motion: new([3], [false]));
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
            var replacement = new Vector3(0, 0, -5);
            runtime.RequestDirectTraversal(owner, replacement, Epoch.AddSeconds(.06), motion: new([3], [true]));
            Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.06));
            Check(move.Count == 2 && move.IsRunning, "Replacement route was not dispatched.");
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(replacement, 0), [new(Epoch, null)]);
            Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.09));
            Check(!move.IsRunning && !runtime.MovementStatus.HasControl, "The previous generation's acquisition bypassed replacement validation.");
        });
        run("Direct traversal does not release when crossing the final coordinate before the native suffix", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var first = new Vector3(0, 0, 2); var final = new Vector3(0, 0, 5);
            var middle = new Vector3(1, 0, 7);
            // The destination coordinate is also an intermediate waypoint. A
            // platform route can pass through it before its remaining suffix.
            Vector3[] route = [first, final, middle, final];
            runtime.RequestDirectTraversal(owner, final, Epoch, route);
            Tick(runtime, Vector3.Zero, Epoch);
            Check(move.IsRunning && move.GetStatus().CurrentIndex == 0, "The route did not publish its first native point.");
            move.SkipCurrent();
            Tick(runtime, final, Epoch.AddSeconds(.1), true);
            Check(move.IsRunning && move.GetStatus().CurrentIndex == 1 && runtime.MovementStatus.HasControl,
                "Passing through the final coordinate released the unfinished native suffix.");
            move.SkipCurrent();
            Tick(runtime, middle, Epoch.AddSeconds(.2), true);
            Check(move.IsRunning && move.GetStatus().CurrentIndex == 2, "The route did not retain its middle suffix after the coordinate crossing.");
            move.SkipCurrent();
            Tick(runtime, final, Epoch.AddSeconds(.24), true);
            Check(!move.IsRunning && !runtime.MovementStatus.HasControl,
                "The route did not release after reaching its actual final native point.");
        });
        run("Direct traversal amends the queued suffix without restarting an early locked slide", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3, 3], [true, true, true]));
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
            move.SkipCurrent();
            var current = move.GetStatus().CurrentPoint;
            Vector3[] amended = [route[0], route[1], new(2, 0, 15), new(2, 0, 20)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended,
                motion: new([3, 3, 3, 3], [true, true, true, true]));
            Tick(runtime, new(0, 0, 4.7f), Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Stops == 0 && move.Clears == 1 && move.Appends == 1,
                "An amended suffix restarted or stopped native input.");
            Check(move.GetStatus().CurrentIndex == 1 && move.GetStatus().CurrentPoint == current
                && move.GetStatus().TotalCount == 4 && move.GetStatus().FinalPoint?.Position == amended[^1],
                "Suffix amendment lost the retained current point or route offset.");
            move.SkipCurrent(); Tick(runtime, route[1], Epoch.AddSeconds(.09), true);
            Check(move.GetStatus().CurrentPoint?.Position == amended[2] && move.Count == 1 && move.Stops == 0,
                "Amended progress returned to a consumed waypoint.");
        });
        run("Direct traversal validates every changed suffix segment before touching the queue", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3, 3], [true, true, true]));
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
            Vector3[] amended = [route[0], route[1], new(2, 0, 15), new(2, 0, 20)];
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(amended[^1], 0), [new(Epoch, null)]);
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended,
                motion: new([3, 3, 3, 3], [true, true, true, true]));
            Tick(runtime, new(0, 0, .2f), Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Stops == 0 && move.Clears == 0 && move.Appends == 0
                && move.GetStatus().FinalPoint?.Position == route[^1], "An unsafe late suffix replaced the approved queue.");
        });
        run("Direct traversal validates retained future prefix points before acknowledging an amendment", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            var motion = new DirectTraversalMotion([3, 3, 3], [true, true, true]);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[1], 0), [new(Epoch, null)]);
            Vector3[] amended = [route[0], route[1], new(2, 0, 15)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended, motion: motion);
            Tick(runtime, Vector3.UnitZ * .2f, Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Stops == 0 && move.Clears == 0 && move.Appends == 0
                && runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06)).Path.SequenceEqual(route),
                "An unsafe retained future target was treated as an acquired current slide.");
        });
        run("Direct traversal validates a walking current point before applying its suffix", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            var motion = new DirectTraversalMotion([6, 6, 6], [false, false, false]);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ, Epoch.AddSeconds(.03), true);
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(route[0], 0), [new(Epoch, null)]);
            Vector3[] amended = [route[0], new(2, 0, 10), new(2, 0, 15)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended, motion: motion);
            Tick(runtime, Vector3.UnitZ * 1.2f, Epoch.AddSeconds(.06), true);
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(move.Clears == 0 && move.Appends == 0 && !execution.HasControl && execution.Rejected
                && execution.Path.SequenceEqual(route), "An unsafe walking current point was acknowledged without validation.");
        });
        foreach (var safe in new[] { true, false })
            run($"Direct traversal ordinary suffix keeps its current input and reports acceptance safe={safe}", () =>
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
                Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
                var motion = new DirectTraversalMotion([6, 6, 6], [false, false, false]);
                runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
                Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ, Epoch.AddSeconds(.03), true);
                Vector3[] amended = [route[0], new(2, 0, 10), new(2, 0, 15)];
                if (!safe) runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(amended[^1], 0), [new(Epoch, null)]);
                runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended, motion: motion);
                Check(runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06)).Path.SequenceEqual(route),
                    "A requested candidate appeared accepted before executor validation.");
                Tick(runtime, Vector3.UnitZ * 1.2f, Epoch.AddSeconds(.06), true);
                var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
                Check(move.Count == 1 && move.Stops == 0 && execution.HasControl && execution.CurrentIndex == 0
                    && execution.Path.SequenceEqual(safe ? amended : route), "Ordinary amendment restarted input or falsely acknowledged a rejected suffix.");
                Check(move.Clears == (safe ? 1 : 0) && move.Appends == (safe ? 1 : 0), "Rejected candidate touched the native queue.");
            });
        run("Direct traversal feedback retains a bounded physical lock after the request is cleared", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch, motion: new([3], [true]));
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, Vector3.UnitZ * .1f, Epoch.AddSeconds(.03), true);
            runtime.RequestDirectTraversal(owner, null, Epoch.AddSeconds(.06));
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(!execution.HasControl && execution.InputLocked && execution.Path.SequenceEqual([Vector3.UnitZ * 5]),
                "Clearing the request forgot the accepted physical slide.");
            Check(!runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(2.3)).InputLocked, "An ended slide kept the recovery lock forever.");
            Check(!runtime.GetDirectTraversalExecution(new object(), Epoch.AddSeconds(.06)).InputLocked,
                "An unrelated owner received direct traversal feedback.");
        });
        foreach (var mismatch in new[] { "current point", "current speed", "consumed prefix" })
            run($"Direct traversal rejects suffix mismatch in {mismatch}", () =>
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object();
                runtime.RequestAutomaticMovement(owner, true);
                Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
                runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3, 3], [true, true, true]));
                Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
                move.SkipCurrent(); Tick(runtime, new(0, 0, 4.7f), Epoch.AddSeconds(.04), true);
                Vector3[] amended = [route[0], route[1], new(2, 0, 15)];
                if (mismatch == "current point") amended[1] += Vector3.UnitX;
                if (mismatch == "consumed prefix") amended[0] += Vector3.UnitX;
                runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended,
                    motion: new([3, mismatch == "current speed" ? 6 : 3, 3], [true, true, true]));
                Tick(runtime, new(0, 0, 4.8f), Epoch.AddSeconds(.06), true);
                Check(move.Count == 1 && move.Stops == 0 && move.Clears == 0 && move.Appends == 0
                    && move.GetStatus().CurrentPoint?.Position == route[1], "A mismatched prefix redirected an acquired slide.");
            });
        run("Direct traversal discards an amendment when its native index advances before dispatch", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3, 3], [true, true, true]));
            Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
            Vector3[] amended = [route[0], route[1], new(2, 0, 15)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended, motion: new([3, 3, 3], [true, true, true]));
            move.SkipCurrent(); Tick(runtime, new(0, 0, 4.7f), Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Stops == 0 && move.Clears == 0 && move.Appends == 0
                && move.GetStatus().FinalPoint?.Position == route[^1], "A stale amendment changed a later native segment.");
        });
        foreach (var ownership in new[] { "unregistered owner", "second owner", "host" })
            run($"Direct traversal suffix respects {ownership}", () =>
            {
                var move = new Move(); var host = false;
                using var runtime = new SafeZoneRuntime(move, () => host, () => host);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); var other = new object();
                runtime.RequestAutomaticMovement(owner, true);
                Vector3[] route = [new(0, 0, 5), new(0, 0, 10), new(0, 0, 15)];
                runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: new([3, 3, 3], [true, true, true]));
                Tick(runtime, Vector3.Zero, Epoch); Tick(runtime, new(0, 0, .1f), Epoch.AddSeconds(.03), true);
                Vector3[] amended = [route[0], route[1], new(2, 0, 15)];
                runtime.RequestDirectTraversal(ownership == "unregistered owner" ? other : owner, amended[^1], Epoch.AddSeconds(.06), amended,
                    motion: new([3, 3, 3], [true, true, true]));
                if (ownership == "host") host = true;
                if (ownership == "second owner") runtime.RequestAutomaticMovement(other, true);
                Tick(runtime, new(0, 0, .2f), Epoch.AddSeconds(.06), true);
                Check(move.Count == 1 && move.Clears == 0 && move.Appends == 0, "A revoked or unrelated owner amended the queue.");
                if (ownership == "host") Check(move.Stops == 0 && !runtime.MovementStatus.HasControl, "Host takeover stopped another controller's input.");
            });
        run("Direct traversal keeps a timed route while the destination is unsafe only now", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            runtime.Manager.AddDangerArea(new CircleTemplate(.5f), new StaticTransform(Vector3.UnitZ * 6, 0), [new(Epoch, Epoch.AddSeconds(.3))]);
            for (var i = 0; i < 20; ++i)
            {
                var now = Epoch.AddSeconds(i * .01);
                runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 6, now);
                Tick(runtime, Vector3.UnitZ * (i * .06f), now, true);
            }
            Check(move.Count == 1 && move.IsRunning, "An arrival-safe route was restarted each frame.");
        });
        run("Direct traversal resumes its remaining prefix after a temporary obstruction", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object();
            runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(0,0,2), new(0,0,4), new(0,0,6)];
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route);
            Tick(runtime, Vector3.Zero, Epoch);
            move.SkipCurrent();
            var hazard = runtime.Manager.AddDangerArea(new CircleTemplate(.4f), new StaticTransform(route[1], 0), [new(Epoch, null)]);
            Tick(runtime, route[0], Epoch.AddSeconds(.1), true);
            Check(!move.IsRunning, "A new obstacle was ignored.");
            runtime.Manager.RemoveArea(hazard);
            Tick(runtime, route[0], Epoch.AddSeconds(.15));
            Check(move.GetStatus().CurrentPoint?.Position == route[1], "Resume moved back to a consumed waypoint.");
            move.SkipCurrent();
            Tick(runtime, route[1], Epoch.AddSeconds(.21), true);
            Check(move.GetStatus().CurrentPoint?.Position == route[2], "Redispatch lost its original route offset.");
        });
        run("Motion envelopes retain their own uncertainty while cast windows use configured margins", () =>
        {
            var raw = new DangerWindow(Epoch, Epoch.AddSeconds(.25));
            Check(raw.Buffered(.5f, .3f).DangerFrom == Epoch.AddSeconds(-.5), "Ordinary cast lost its early margin.");
            var motion = raw with { HasMotionEnvelope = true };
            Check(motion.Buffered(.5f, .3f) == motion, "A swept body received duplicate cast margins.");
        });
        run("Rectangle analytical segment and bounds agree with rotated contours", () =>
        {
            var random = new Random(1307);
            for (var i = 0; i < 1000; ++i)
            {
                var rectangle = new RectangleGeometry(new(3,0,-4), (float)random.NextDouble() * 6.28f, 2, 7);
                var points = rectangle.GetContours()[0]; var bounds = rectangle.GetBounds();
                Check(points.All(p => p.X >= bounds.MinX - .0001 && p.X <= bounds.MaxX + .0001
                    && p.Z >= bounds.MinZ - .0001 && p.Z <= bounds.MaxZ + .0001), "Analytical bounds clipped a corner.");
                var a = new Vector3((float)random.NextDouble() * 20 - 7, 0, (float)random.NextDouble() * 20 - 14);
                var b = new Vector3((float)random.NextDouble() * 20 - 7, 0, (float)random.NextDouble() * 20 - 14);
                var polygon = new WorldPolygonGeometry(points);
                Check(rectangle.IntersectsSegment(a, b, 0) == polygon.IntersectsSegment(a, b, 0), "Analytical segment changed rotated rectangle intersections.");
            }
        });
    }
    private static void Tick(SafeZoneRuntime runtime, Vector3 p, DateTime now, bool moving = false)
    {
        var snapshot = runtime.Manager.BuildSnapshot(now);
        ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(p, 0, 6, moving, false, 0, true, snapshot.Version), new(6,6,0,false,6,6), now);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class OccupancyTiming : IDirectTraversalTiming
    {
        public double Duration(Vector3 from, Vector3 to, float speed) => 2;
        public IReadOnlyList<DirectTraversalSlice> Sweep(Vector3 from, Vector3 to, float speed)
            => [new(from, Vector3.Lerp(from, to, .5f), 0, .8), new(Vector3.Lerp(from, to, .5f), to, .8, 2)];
    }
    private sealed class Move : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _path = []; private int _index;
        public int Count, Stops, Clears, Appends;
        public bool IsRunning => _index < _path.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _path[_index] : null, _path.LastOrDefault(), _index, _path.Count, Math.Max(0,_path.Count-_index-1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _path = points; _index = 0; ++Count; }
        public void Stop() { _path = []; _index = 0; ++Stops; }
        public void SkipCurrent() { if (_index + 1 >= _path.Count) Stop(); else ++_index; }
        public void ClearQueue() { if (IsRunning) _path = _path.Take(_index + 1).ToArray(); ++Clears; }
        public void Append(GreenMovePoint point) => Append([point]);
        public void Append(IReadOnlyList<GreenMovePoint> points)
        {
            var running = IsRunning;
            _path = _path.Concat(points).ToArray();
            if (!running) _index = 0;
            ++Appends;
        }
    }
}
