using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class Pt30MovingBallRecordedRegression
{
    public static void Run()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "PilgrimsTraverse30", "arr-124542-moving-balls.json");
        using var file = JsonDocument.Parse(File.ReadAllText(path));
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);
        var center = new Vector3(-300, 0, -300);
        var boundary = Pt30Arena.Boundary(0);
        var hole = Pt30Arena.Hole(0);
        var options = new SolveOptions { HardSafetyMargin = 0, ArenaSafetyMargin = 0 };
        var dispatch = typeof(SafeZoneRebornMovementSystem).GetMethod("Dispatch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var move = ScriptServiceProxy.Create<IGreenMoveSystem>((method, _) => method switch
        {
            "MoveTo" or "Stop" => null,
            "GetStatus" => default(GreenMoveStatus),
            "get_IsRunning" => false,
            _ => throw new NotSupportedException(method),
        });
        var unnecessaryBaselineMoves = 0;
        foreach (var sample in file.RootElement.GetProperty("samples").EnumerateArray())
        {
            var t = sample.GetProperty("t").GetDouble();
            var player = Xz(sample.GetProperty("player"));
            var encounter = new Pt30Encounter();
            var boss = new Pt30Encounter.Actor(1, Pt30Encounter.BOSS_OID, center, 0, false, true);
            var balls = sample.GetProperty("balls").EnumerateArray().ToArray();
            var lastFrame = balls[0].GetArrayLength() - 1;
            for (var frame = 0; frame <= lastFrame; frame++)
            {
                var actors = balls.Select((b, i) => new Pt30Encounter.Actor((ulong)i + 2, Pt30Encounter.BALL_OID, Xz(b[frame]), 0, false, true));
                encounter.Synchronize(actors.Prepend(boss), now.AddSeconds((frame - lastFrame) * .1));
            }
            ulong source = 100;
            foreach (var cast in sample.GetProperty("casts").EnumerateArray())
            {
                var start = now.AddSeconds(cast.GetProperty("start").GetDouble());
                var location = cast.GetProperty("offset").EnumerateArray().Select(v => v.GetSingle()).ToArray();
                encounter.Cast(source++, cast.GetProperty("action").GetUInt32(), new(location[0], location[1], location[2]),
                    cast.GetProperty("rotation").GetSingle(), start, start.AddSeconds(cast.GetProperty("duration").GetSingle() + .3));
            }
            var setting = new SafeZoneSetting { EnableSafeZone = true, RuntimePreferSafeRefuge = true };
            using var manager = new SafeZoneManager(setting);
            manager.SetArenaBoundary(boundary);
            manager.AddObstacle(hole, new StaticTransform(Vector3.Zero, 0));
            foreach (var hazard in encounter.Hazards(now, 0, player))
                manager.AddDangerArea(DangerMarginInflator.Inflate(hazard.Shape, .2f), new StaticTransform(hazard.Position, hazard.Rotation), [hazard.Window(now)]);
            var snapshot = manager.BuildSnapshot(now);
            var state = new PlayerState(player, 0, 9, false, false, 0, true, snapshot.Version);
            var solution = new SafeZoneSolver().Solve(snapshot, state, new(9, 9, .08f, false, 9, 9), options, now);
            Check(solution.IsSuccess && !solution.IsLeastDangerFallback, $"ARR {t}s did not find a safe plan: {solution.DescribeOutcome()}");
            var shouldHold = sample.GetProperty("hold").GetBoolean();
            var baselineMoves = Dispatch(preferSafeRefuge: false);
            var moves = Dispatch(preferSafeRefuge: true);
            Check(moves == (shouldHold ? 0 : 1), $"ARR {t}s expected {(shouldHold ? "hold" : "departure")}, dispatched {moves}");
            if (shouldHold) unnecessaryBaselineMoves += baselineMoves;

            int Dispatch(bool preferSafeRefuge)
            {
                setting.RuntimePreferSafeRefuge = preferSafeRefuge;
                using var movement = new SafeZoneRebornMovementSystem(setting, manager, () => false, () => false);
                movement.Attach(move);
                dispatch.Invoke(movement, [solution, state, snapshot, now]);
                return movement.DispatchCount;
            }
        }
        Check(unnecessaryBaselineMoves > 0, "The fixture must reproduce unnecessary movement without the safe refuge policy.");
        Console.WriteLine($"PT30 ARR moving balls: suppressed {unnecessaryBaselineMoves} unnecessary dispatches and retained the required departure.");
    }

    private static Vector3 Xz(JsonElement p) => new(p[0].GetSingle(), 0, p[1].GetSingle());

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
