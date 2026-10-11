using System.Numerics;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using PromeRotation.GreenMoveSystem;

internal static class NavigationDirectTraversalPhysicalRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);

    public static void Run(Action<string, Action> run)
    {
        run("Direct traversal appends after native completion during a physical slide", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var first = new Vector3(4.5f, 0, 0); var next = new Vector3(5, 0, -4.5f);
            var initial = new DirectTraversalMotion([3], [true], PhysicalLandings: [new(5, 0, 0)]);
            runtime.RequestDirectTraversal(owner, first, Epoch, [first], motion: initial);
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(.1f, 0, 0), Epoch.AddSeconds(.03), true);
            move.SkipCurrent();
            Vector3[] route = [first, next];
            var motion = new DirectTraversalMotion([3, 3], [true, true], PhysicalLandings: [new(5, 0, 0), new(5, 0, -5)]);
            runtime.RequestDirectTraversal(owner, next, Epoch.AddSeconds(.06), route, motion: motion);
            Tick(runtime, new(4.1f, 0, 0), Epoch.AddSeconds(.06), true);
            var execution = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(move.Count == 2 && move.Clears == 0 && move.Points.SequenceEqual([next])
                && execution.HasControl && execution.Path.SequenceEqual(route) && execution.CurrentIndex == 1,
                "The consumed native point prevented appending a safe successor during its physical slide.");
            Tick(runtime, new(4.2f, 0, 0), Epoch.AddSeconds(.09), true);
            Check(move.IsRunning && move.Count == 2 && runtime.MovementStatus.HasControl,
                "The resumed suffix discarded the remaining physical tail of the consumed native point.");
        });
        run("Direct traversal retains ownership inside physical tolerance until native arrival", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            var target = new Vector3(5, 0, 0);
            runtime.RequestDirectTraversal(owner, target, Epoch, motion: new([6], [false], InputLockActive: false));
            Tick(runtime, Vector3.Zero, Epoch);
            Tick(runtime, new(4.8f, 0, 0), Epoch.AddSeconds(.1), true);
            Check(move.IsRunning && runtime.MovementStatus.HasControl && move.Count == 1,
                "Physical tolerance released ownership while the native point was still running.");
            runtime.RequestAutomaticMovement(owner, false);
            Check(!move.IsRunning, "The owner could no longer stop its unfinished native route.");
        });
        run("Direct traversal releases ice into a validated walking ray after early native arrival", () =>
        {
            foreach (var scenario in new[] { "safe", "hazard", "outside" })
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
                Vector3[] route = [new(4.5f, 0, 0), new(8, 0, 0)];
                var motion = new DirectTraversalMotion([3, 6], [true, false], PhysicalLandings: [new(5, 0, 0), route[1]]);
                runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
                Tick(runtime, Vector3.Zero, Epoch);
                Tick(runtime, new(.1f, 0, 0), Epoch.AddSeconds(.03), true);
                move.SkipCurrent();
                if (scenario == "hazard") runtime.Manager.AddDangerArea(new CircleTemplate(.2f),
                    new StaticTransform(new(6, 0, 0), 0), [new(Epoch, null)]);
                runtime.RequestDirectTraversal(owner, route[^1], Epoch.AddSeconds(.06), route,
                    motion: motion with { InputLockActive = false });
                Tick(runtime, new(4, 0, scenario == "outside" ? .81f : 0), Epoch.AddSeconds(.06), true);
                Check(scenario == "safe" ? move.IsRunning && move.Count == 1 : !move.IsRunning,
                    $"The dry transition either lost its queued ray or bypassed validation: {scenario}.");
            }
        });
        run("Direct traversal checks physical slide beyond the queued target", () =>
        {
            foreach (var physical in new[] { false, true })
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
                var queued = new Vector3(4.9f, 0, 0); var landing = new Vector3(5, 0, 0);
                runtime.Manager.AddDangerArea(new CircleTemplate(.04f), new StaticTransform(landing, 0), [new(Epoch, null)]);
                runtime.RequestDirectTraversal(owner, queued, Epoch,
                    motion: new([3], [true], InputLockActive: false, PhysicalLandings: physical ? [landing] : null));
                Tick(runtime, Vector3.Zero, Epoch);
                Check(physical ? !move.IsRunning && runtime.GetDirectTraversalExecution(owner, Epoch).Rejected : move.IsRunning,
                    "The physical overshoot was omitted or the queued-target negative control became unsafe.");
            }
        });

        run("Direct traversal keeps native targets while validating suffix from physical seams", () =>
        {
            var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
            Vector3[] route = [new(4.9f, 0, 0), new(5, 0, -4.9f)];
            Vector3[] landings = [new(5, 0, 0), new(5, 0, -5)];
            var motion = new DirectTraversalMotion([3, 3], [true, true], PhysicalLandings: landings);
            runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
            Tick(runtime, Vector3.Zero, Epoch);
            Check(move.Points.SequenceEqual(route), "The physical endpoints replaced the native queued targets.");
            Tick(runtime, new(.1f, 0, 0), Epoch.AddSeconds(.03), true);
            // A line from the queued seam (4.9,0) clips this circle. The real
            // five-metre landing is (5,0), so the following locked ray is safe.
            runtime.Manager.AddDangerArea(new CircleTemplate(.025f), new StaticTransform(new(4.95f, 0, 2.5f), 0), [new(Epoch, null)]);
            Vector3[] amended = [route[0], new(5, 0, 4.9f)];
            Vector3[] amendedLandings = [landings[0], new(5, 0, 5)];
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.06), amended,
                motion: motion with { PhysicalLandings = amendedLandings });
            Tick(runtime, new(.2f, 0, 0), Epoch.AddSeconds(.06), true);
            Check(move.Count == 1 && move.Clears == 1 && move.Points.SequenceEqual(amended)
                && move.GetStatus().CurrentPoint?.Position == route[0],
                "The physical seam validation rejected a safe suffix or restarted its native current target.");
            var accepted = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.06));
            Check(accepted.Motion != null && accepted.Motion.Matches(motion with { PhysicalLandings = amendedLandings }, amended),
                "The accepted suffix feedback omitted its physical motion contract.");

            var rejectedMotion = motion with { PhysicalLandings = [new(4.7f, 0, 0), amendedLandings[1]] };
            runtime.RequestDirectTraversal(owner, amended[^1], Epoch.AddSeconds(.09), amended,
                motion: rejectedMotion);
            Tick(runtime, new(.3f, 0, 0), Epoch.AddSeconds(.09), true);
            Check(move.Count == 1 && move.Clears == 1,
                "An identical queued prefix silently replaced its acquired physical landing contract.");
            var feedback = runtime.GetDirectTraversalExecution(owner, Epoch.AddSeconds(.09));
            Check(feedback.Path.SequenceEqual(amended) && feedback.HasControl && feedback.Motion != null
                && feedback.Motion.Matches(accepted.Motion, amended) && !feedback.Motion.Matches(rejectedMotion, amended),
                "A rejected motion sharing the queued targets was reported as accepted.");
        });

        run("Direct traversal retains only a proven physical tail after early native advancement", () =>
        {
            foreach (var scenario in new[] { "acquired tail", "not acquired", "walking prefix", "lock ended", "lease expired", "outside both rays" })
            {
                var move = new Move(); using var runtime = new SafeZoneRuntime(move, () => false, () => false);
                runtime.Setting.EnableSafeZone = true; var owner = new object(); runtime.RequestAutomaticMovement(owner, true);
                Vector3[] route = [new(4.9f, 0, 0), new(5.25f, 0, 4.9f), new(5.25f, 0, 9.9f)];
                Vector3[] landings = [new(5.25f, 0, 0), new(5.25f, 0, 5), new(5.25f, 0, 10)];
                var motion = new DirectTraversalMotion([3, 3, 3], [scenario != "walking prefix", true, true], PhysicalLandings: landings);
                runtime.RequestDirectTraversal(owner, route[^1], Epoch, route, motion: motion);
                Tick(runtime, Vector3.Zero, Epoch);
                if (scenario != "not acquired") Tick(runtime, new(.1f, 0, 0), Epoch.AddSeconds(.03), true);
                move.SkipCurrent();
                var now = Epoch.AddSeconds(scenario == "lease expired" ? 2.2 : .06);
                if (scenario == "lock ended") motion = motion with { InputLockActive = false };
                runtime.RequestDirectTraversal(owner, route[^1], now, route, motion: motion);
                // The native point is within its .55m tolerance, while the
                // current physical ray starts .85m away. Steering remains on
                // the previous acquired ray until the actual slide ends.
                var position = new Vector3(4.4f, 0, scenario == "outside both rays" ? -.81f : 0);
                Tick(runtime, position, now, true);
                var acceptedTail = scenario == "acquired tail";
                Check(acceptedTail ? move.IsRunning && runtime.MovementStatus.HasControl
                    : !move.IsRunning && runtime.GetDirectTraversalExecution(owner, now).Rejected,
                    $"Previous-ray acceptance lacked physical commitment evidence for {scenario}.");
                if (!acceptedTail) continue;
                Check(move.Count == 1 && move.GetStatus().CurrentIndex == 1,
                    "A valid physical tail restarted its native current target.");
                Vector3[] amended = [route[0], route[1], new(10.15f, 0, 5)];
                var amendedMotion = motion with { PhysicalLandings = [landings[0], landings[1], new(10.25f, 0, 5)] };
                runtime.RequestDirectTraversal(owner, amended[^1], now.AddSeconds(.03), amended, motion: amendedMotion);
                Tick(runtime, new(4.5f, 0, 0), now.AddSeconds(.03), true);
                Check(move.Count == 1 && move.Clears == 1 && move.Points.SequenceEqual(amended)
                    && move.GetStatus().CurrentPoint?.Position == route[1],
                    "A suffix amendment rejected the proven previous physical tail or changed its native current point.");
                Tick(runtime, new(4.6f, 0, 0), now.AddSeconds(.06), true);
                Check(move.IsRunning && runtime.MovementStatus.HasControl,
                    "Amending the suffix discarded the still-acquired previous physical ray.");
            }
        });
    }

    private static void Tick(SafeZoneRuntime runtime, Vector3 position, DateTime now, bool moving = false)
    {
        var snapshot = runtime.Manager.BuildSnapshot(now);
        ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(position, 0, 6, moving, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), now);
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private sealed class Move : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _points = []; private int _index;
        public int Count, Clears;
        public IEnumerable<Vector3> Points => _points.Select(point => point.Position);
        public bool IsRunning => _index < _points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _points[_index] : null,
            IsRunning ? _points[^1] : null, _index, _points.Count, Math.Max(0, _points.Count - _index - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _points = points; _index = 0; ++Count; }
        public void Stop() { _points = []; _index = 0; }
        public void Append(GreenMovePoint point) => Append([point]);
        public void Append(IReadOnlyList<GreenMovePoint> points) => _points = _points.Concat(points).ToArray();
        public void ClearQueue() { _points = _points.Take(_index + 1).ToArray(); ++Clears; }
        public void SkipCurrent() => ++_index;
    }
}
