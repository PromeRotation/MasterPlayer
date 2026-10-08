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
        foreach (var reason in new[] { "owner", "second owner", "hold", "expiry", "clear", "host" })
            run($"Direct traversal releases on {reason}", () =>
            {
                var move = new Move(); var host = false;
                using var runtime = new SafeZoneRuntime(move, () => host, () => host);
                runtime.Setting.EnableSafeZone = true;
                var owner = new object(); var other = new object();
                runtime.RequestAutomaticMovement(owner, true);
                runtime.RequestDirectTraversal(other, Vector3.UnitZ * 5, Epoch);
                Check(!move.IsRunning, "Unregistered owner acquired input.");
                runtime.RequestDirectTraversal(owner, Vector3.UnitZ * 5, Epoch);
                Tick(runtime, Vector3.Zero, Epoch);
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
    private sealed class Move : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> _path = []; private int _index;
        public int Count;
        public bool IsRunning => _index < _path.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? _path[_index] : null, _path.LastOrDefault(), _index, _path.Count, Math.Max(0,_path.Count-_index-1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { _path = points; _index = 0; ++Count; }
        public void Stop() { _path = []; _index = 0; }
        public void SkipCurrent() => ++_index;
        public void ClearQueue() => Stop();
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> points) => throw new NotSupportedException();
    }
}
