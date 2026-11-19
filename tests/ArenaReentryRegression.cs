using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class ArenaReentryRegression
{
    private static readonly DateTime Now = new(2026, 11, 19, 10, 35, 38, DateTimeKind.Utc);
    public static void Run(Action<string, Action> run)
    {
        run("Arena reentry recorded PT70 stop returns to a safe interior", () =>
        {
            var boundary = new Pt70Encounter().Boundary;
            var from = new Vector3(-299.99884f, .0027513504f, -310.02722f);
            var solution = Solve(boundary, from);
            Check(solution.IsSuccess && solution.ExecutablePath.Count == 2, "Recorded 2.6cm overrun remains stranded outside the arena");
            var target = solution.ExecutablePath[^1].Position;
            Check(ArenaBoundaryTraversal.ContainsTube(boundary, target, target, .2f), "Reentry stops on the damaging edge");
            Check(Vector3.Distance(from, target) <= 2 && solution.MaxArrivalTolerance <= .1f, "Reentry exceeds its short route or stopping budget");
        });
        run("Arena reentry circle and rotated rectangle recover nearby overruns", () =>
        {
            Check(Solve(new CircleGeometry(Vector3.Zero, 6), new(6.2f, 0, 0)).ExecutablePath.Count == 2, "Circle reentry failed");
            Check(Solve(new RectangleGeometry(Vector3.Zero, MathF.PI / 4, 10, 10), new(7.2f, 0, 0)).ExecutablePath.Count == 2, "Rotated rectangle reentry failed");
        });
        run("Arena reentry rejects distant, hole, concave and blocked starts", () =>
        {
            var circle = new CircleGeometry(Vector3.Zero, 6);
            Check(!Solve(circle, new(7, 0, 0)).IsSuccess, "Distant outside position gained a route");
            Check(!Solve(new DonutGeometry(Vector3.Zero, 2, 6), Vector3.Zero).IsSuccess, "Hole gained an outside exception");
            var concave = new WorldPolygonGeometry([new(0,0,0),new(4,0,0),new(4,0,1),new(1,0,1),new(1,0,4),new(0,0,4)]);
            Check(!Solve(concave, new(1.1f,0,2)).IsSuccess, "Concave gap gained an outside exception");
            var obstacle = new ObstacleSnapshot(new(1, AreaKind.Obstacle), new CircleGeometry(new(6.2f,0,0), .2f));
            Check(!Solve(circle, new(6.2f,0,0), [obstacle]).IsSuccess, "Reentry crossed a hard obstacle");
        });
        run("Arena reentry rechecks hazards, delay, speed and stopping clearance", () =>
        {
            var boundary = new CircleGeometry(Vector3.Zero, 6);
            var from = new Vector3(6.2f, 0, 0);
            var to = new Vector3(5.4f, 0, 0);
            var options = new SolveOptions();
            SafeZoneSnapshot Snapshot(DateTime hit, DateTime? end = null) => new(1, Now, boundary, [],
                [new(new(2, AreaKind.Danger), new CircleGeometry(to, .2f), [new(hit, end)])], []);
            bool Safe(SafeZoneSnapshot snapshot, float speed = 6, double delay = 0) =>
                ArenaReentryRecovery.IsSafe(snapshot, from, to, Now, speed, options, .1f, delay);
            Check(!Safe(Snapshot(Now)), "Active danger accepted");
            Check(!Safe(Snapshot(Now.AddSeconds(.4))), "Buffered future danger accepted");
            Check(Safe(Snapshot(Now.AddSeconds(-2), Now.AddSeconds(-1))), "Fully expired danger blocks recovery");
            var clear = new SafeZoneSnapshot(1, Now, boundary, [], [], []);
            Check(!Safe(clear, 0) && !Safe(clear, float.NaN) && !Safe(clear, delay: 1), "Invalid speed/delay accepted");
            Check(!ArenaReentryRecovery.IsSafe(clear, from, new(5.95f, 0, 0), Now, 6, options, .1f), "Stopping tolerance consumes the safe interior");
            Check(!ArenaReentryRecovery.IsSafe(clear, new(7, 0, 0), to, Now, 6, options, .1f), "Delayed result accepts a now distant origin");
        });
    }
    private static SafeZoneSolution Solve(IGeometryShape boundary, Vector3 from, IReadOnlyList<ObstacleSnapshot>? obstacles = null)
    {
        var snapshot = new SafeZoneSnapshot(1, Now, boundary, obstacles ?? [], [], []);
        var player = new PlayerState(from, 0, 6, false, false, 0, true, 1);
        return new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new() { GoalDirectedTemporalPlanning = true, ArenaSafetyMargin = 0 }, Now);
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
