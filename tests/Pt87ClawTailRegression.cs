using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Replay;
using MasterPlayer.Spatial;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using PromeRotation.GreenMoveSystem;

internal static class Pt87ClawTailRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var latency in new[] { .08, .3 })
            run($"PT87 recorded turn escapes with {latency * 1000:F0}ms publication and 80ms startup", () => Simulate(latency));
        run("PT87 recorded Claw and Tail turns before the lethal first strike", () =>
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "Fixtures", "PilgrimsTraverse", "pt87-claw-tail-233909.json")));
            Check(AoeShapeDatabase.TryGetShape(43132, out var shape), "The recorded action needs an explicit shape.");
            var epoch = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
            var count = 0;
            foreach (var sample in fixture.RootElement.GetProperty("cases").EnumerateArray())
            {
                var origin = Position(sample.GetProperty("origin"));
                var target = Position(sample.GetProperty("playerAtHit"));
                var packetRotation = sample.GetProperty("packetRotation").GetSingle();
                var strikeRotation = sample.GetProperty("strikeRotation").GetSingle();
                var hit = sample.GetProperty("hit").GetBoolean();
                var cast = new LogSystemCastStartEvent { SourceId = 1, ActionId = 43132, EffectPosition = origin,
                    SourcePosition = origin, SourceRotation = packetRotation, Raw = new ReplayCastData(packetRotation) };
                Check(AoeShapeDatabase.TryCreateBinding(cast, shape!, out var binding, 1289), "Recorded cast binding was rejected.");
                using var manager = new SafeZoneManager(new() { EnableSafeZone = true });
                manager.EntityPoseOverride = _ => (origin, strikeRotation);
                var from = epoch.AddSeconds(2.7);
                manager.AddDangerArea(AoeShapeDatabase.CreateTemplate(shape!)!, binding, [new(from, from.AddSeconds(.6))]);
                var snapshot = manager.BuildSnapshot(epoch);
                Check(snapshot.DangerAreas.Single().Geometry.Contains(target) == hit,
                    $"Cast {sample.GetProperty("castTime")}: prediction disagrees with recorded targets.");
                if (hit)
                {
                    Check(!AoeShapeDatabase.CreateTemplate(shape!)!.Resolve(origin, packetRotation, null).Contains(target),
                        "The fixture must distinguish the obsolete packet direction from the actual turn.");
                    manager.SetArenaBoundary(new CircleGeometry(origin, 20));
                    snapshot = manager.BuildSnapshot(epoch.AddSeconds(.13));
                    var player = new PlayerState(target, 0, 6, false, false, 0, true, snapshot.Version);
                    var solution = new SafeZoneSolver().Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new(), epoch.AddSeconds(.13));
                    Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2
                        && !snapshot.DangerAreas.Single().Geometry.Contains(solution.ExecutablePath[^1].Position),
                        "The real solver must escape the turned first strike, rather than hold at the recorded death point.");
                }
                count++;
            }
            Check(count == 7, "All seven recorded casts must be checked.");
        });
    }

    private static void Simulate(double latency)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "PilgrimsTraverse", "pt87-claw-tail-233909.json")));
        var sample = fixture.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("hit").GetBoolean());
        var origin = Position(sample.GetProperty("origin"));
        var position = Position(sample.GetProperty("playerAtHit"));
        var packet = sample.GetProperty("packetRotation").GetSingle();
        var strike = sample.GetProperty("strikeRotation").GetSingle();
        var hitAt = sample.GetProperty("hitTime").GetDouble() - sample.GetProperty("castTime").GetDouble();
        var epoch = new DateTime(2026, 11, 19, 0, 0, 0, DateTimeKind.Utc);
        var now = epoch;
        var move = new Moving();
        using var runtime = new SafeZoneRuntime(move, () => false, () => false);
        runtime.Setting.EnableSafeZone = runtime.Setting.EnableAutoMovement = true;
        runtime.Manager.SetArenaBoundary(new CircleGeometry(origin, 20));
        Check(AoeShapeDatabase.TryGetShape(43132, out var descriptor), "Missing recorded action.");
        var cast = new LogSystemCastStartEvent { SourceId = 1, ActionId = 43132, EffectPosition = origin,
            SourcePosition = origin, SourceRotation = packet, Raw = new ReplayCastData(packet) };
        Check(AoeShapeDatabase.TryCreateBinding(cast, descriptor!, out var binding, 1289), "Missing cast binding.");
        runtime.Manager.EntityPoseOverride = _ => (origin, now < epoch.AddSeconds(.13) ? packet : strike);
        runtime.Manager.AddDangerArea(DangerMarginInflator.Inflate(AoeShapeDatabase.CreateTemplate(descriptor!)!, .2f), binding,
            [new(epoch.AddSeconds(2.7 + descriptor!.ExtraDelaySeconds), epoch.AddSeconds(3.6))]);
        var solver = new SafeZoneSolver();
        SafeZoneSolution? pending = null, accepted = null;
        var ready = epoch;
        var dispatch = runtime.MovementStatus.GetType().GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var tick = 0; tick * .01 < hitAt; ++tick)
        {
            now = epoch.AddSeconds(tick * .01);
            var snapshot = runtime.Manager.BuildSnapshot(now);
            var player = new PlayerState(position, 0, 6, move.IsRunning, false, 0, true, snapshot.Version);
            if (pending != null && now >= ready) { accepted = pending; pending = null; }
            if (pending == null)
            {
                pending = solver.Solve(snapshot, player, new(6, 6, 0, false, 6, 6), new(), now);
                ready = now.AddSeconds(latency);
            }
            dispatch.Invoke(runtime.MovementStatus, [accepted, player, snapshot, now]);
            position = move.Advance(position, (float)Math.Min(.01, hitAt - tick * .01));
        }
        Check(move.Moves > 0 && !RayRectangleTemplate.Symmetric(3, 8).Resolve(origin, strike, null).Contains(position),
            $"The simulated player remained in the lethal first strike after {latency:F2}s publication latency.");
        Console.WriteLine($"PT87 latency={latency:F2}s startup=.08s dispatches={move.Moves} final={position}");
    }

    private sealed class Moving : IGreenMoveSystem
    {
        private IReadOnlyList<GreenMovePoint> points = [];
        private int index;
        private float startup;
        public int Moves { get; private set; }
        public bool IsRunning => index < points.Count;
        public GreenMoveStatus GetStatus() => new(IsRunning, IsRunning ? points[index] : null,
            points.Count > 0 ? points[^1] : null, index, points.Count, Math.Max(0, points.Count - index - 1));
        public void MoveTo(GreenMovePoint point) => MoveTo([point]);
        public void MoveTo(IReadOnlyList<GreenMovePoint> path) { points = path; index = 0; startup = .08f; Moves++; }
        public void Stop() { points = []; index = 0; }
        public void SkipCurrent() => index++;
        public void Append(GreenMovePoint point) => throw new NotSupportedException();
        public void Append(IReadOnlyList<GreenMovePoint> path) => throw new NotSupportedException();
        public void ClearQueue() => throw new NotSupportedException();
        public Vector3 Advance(Vector3 position, float seconds)
        {
            if (startup > 0) { startup -= seconds; return position; }
            while (IsRunning)
            {
                var delta = points[index].Position - position;
                delta.Y = 0;
                var distance = delta.Length();
                if (distance <= points[index].Tolerance) { index++; continue; }
                return position + delta / distance * MathF.Min(distance, seconds * 6);
            }
            return position;
        }
    }

    private static Vector3 Position(JsonElement p) => new(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
