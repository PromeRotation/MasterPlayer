using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.SafeZoneReborn;
using PromeRotation.GreenMoveSystem;

internal static class DohnMhegMovementRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("DohnMheg bridge real runtime dispatch precise corners hazard rejection and owner release", () =>
        {
            var now = new DateTime(2026, 11, 22, 0, 0, 0, DateTimeKind.Utc);
            var start = new Vector3(-128.5f, 0, -229);
            var target = new Vector3(-128.5f, 0, -260);
            var arena = DohnMhegArenas.Chasm(0);
            var route = DohnMhegBridgePath.Build(start, target, arena);
            var move = new Move();
            using var runtime = new SafeZoneRuntime(move, () => false, () => false);
            runtime.Setting.EnableSafeZone = true;
            runtime.Manager.SetArenaBoundary(arena);
            var owner = new object();
            runtime.RequestAutomaticMovement(owner, true, arenaSafetyMargin: .05f);
            runtime.RequestDirectTraversal(owner, target, now, route);
            Tick(runtime, start, now);
            Check(move.IsRunning && move.Path.Count == route.Length && move.Path.All(p => p.Tolerance <= .031f),
                "Real executor rejected native bridge or widened corner tolerance.");
            Check(move.Path.Select(p => p.Position).SequenceEqual(route), "Executor smoothed away bridge corners.");
            runtime.RequestAutomaticMovement(owner, false);
            Check(!move.IsRunning && !runtime.MovementStatus.HasControl, "Owner release retained bridge input.");
            runtime.RequestAutomaticMovement(owner, true, arenaSafetyMargin: .05f);
            runtime.RequestDirectTraversal(owner, target, now.AddSeconds(.1), [target]);
            Tick(runtime, start, now.AddSeconds(.1));
            Check(!move.IsRunning, "Executor accepted a direct shortcut across the chasm.");
            runtime.RequestAutomaticMovement(owner, false);
            runtime.Setting.MovementAllowed = false;
            runtime.RequestAutomaticMovement(owner, true, arenaSafetyMargin: .05f);
            runtime.RequestDirectTraversal(owner, target, now.AddSeconds(.2), route);
            Tick(runtime, start, now.AddSeconds(.2));
            Check(!move.IsRunning && !runtime.Setting.RuntimeMovementRequested, "Movement gate retained bridge request.");
        });
    }

    private static void Tick(SafeZoneRuntime runtime, Vector3 position, DateTime now)
    {
        var snapshot = runtime.Manager.BuildSnapshot(now);
        ((SafeZoneRebornMovementSystem)runtime.MovementStatus).Update(new(position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), now);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private sealed class Move : IGreenMoveSystem
    {
        public IReadOnlyList<GreenMovePoint> Path { get; private set; } = [];
        private int index;
        public bool IsRunning => index < Path.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? Path[index] : null, Path.LastOrDefault(), index, Path.Count, Math.Max(0, Path.Count - index - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> points) { Path = points; index = 0; }
        public void Stop() { Path = []; index = 0; }
        public void SkipCurrent() { if (IsRunning) ++index; }
        public void ClearQueue() { Path = Path.Take(index + 1).ToArray(); }
        public void Append(GreenMovePoint point) => Append([point]);
        public void Append(IReadOnlyList<GreenMovePoint> points) { Path = Path.Concat(points).ToArray(); }
    }
}
