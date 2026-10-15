using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.GreenMoveSystem;

internal static class BlundervilleJumpRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    internal sealed record RecordedJump(string Name, BlundervilleCourse Course, Vector3 Takeoff, Vector3 Landing, float Approach);

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville jump requires a running landing route owned by this movement system", () =>
        {
            var takeoff = new Vector3(-191.5f, 13.565f, -344.57f);
            var landing = new Vector3(-194.85f, 13.565f, -345.97f);
            GreenMoveStatus Movement(Vector3 point) => new(true, new(point), new(point), 0, 1, 0);
            Check(!BlundervilleJumpExecution.Ready(landing, true, Movement(takeoff)), "Old takeoff route authorized launch.");
            Check(!BlundervilleJumpExecution.Ready(landing, false, Movement(landing)), "Another owner's route authorized launch.");
            Check(BlundervilleJumpExecution.Ready(landing, true, Movement(landing)), "Direct landing route was rejected.");
        });
        foreach (var link in RecordedJumps())
        {
            run($"Blunderville recorded short jump approaches the precise takeoff: {link.Name}", () =>
            {
                var planner = new BlundervilleJumpPlanner();
                var prep = planner.Plan(link.Course, Epoch, Approach(link), link.Landing, true, false);
                Check(prep is { Jump: false, Hold: false } && Near(prep.Target, link.Takeoff),
                    "The planner jumped early or approached an invented takeoff point.");
                Check(prep!.Corridor.Contains(link.Takeoff) && prep.Corridor.Contains(link.Landing)
                    && prep.Corridor.Contains(Vector3.Lerp(link.Takeoff, link.Landing, 0.5f)),
                    "The jump corridor excludes a recorded endpoint or the actual flight.");
                var takeoff = planner.Plan(link.Course, Epoch.AddSeconds(0.1), link.Takeoff, link.Landing, true, false);
                Check(takeoff is { Jump: true, Hold: false } && Near(takeoff.Target, link.Landing),
                    "Reaching the recorded takeoff did not trigger its measured short jump.");
                Check(ReferenceEquals(prep.Corridor, takeoff!.Corridor), "Takeoff replaced the prepared corridor.");
            });

            run($"Blunderville recorded short jump cannot launch far past or beside takeoff: {link.Name}", () =>
            {
                var direction = Direction(link);
                var perpendicular = new Vector3(-direction.Z, 0, direction.X);
                var ahead = new BlundervilleJumpPlanner().Plan(link.Course, Epoch,
                    link.Takeoff - direction * 0.3f, link.Landing, true, false);
                Check(ahead is { Jump: false }, "The planner launched before it reached the takeoff window.");
                var beside = new BlundervilleJumpPlanner().Plan(link.Course, Epoch,
                    link.Takeoff + perpendicular * 0.5f, link.Landing, true, false);
                Check(beside is { Jump: false }, "The planner launched from outside the recorded takeoff lane.");
                var passed = new BlundervilleJumpPlanner().Plan(link.Course, Epoch,
                    // Stair treads extend ~2m past their nominal takeoff; without a
                    // run-up a landing can stop 1.4m+ along (12:05 ARR) and must still
                    // jump. Reject a point beyond that band, at the riser itself.
                    link.Takeoff + direction * (link.Course == BlundervilleCourse.Final ? BlundervilleJumpExecution.StairBand + .2f : .8f), link.Landing, true, false);
                Check(passed == null || !passed.Jump && Vector3.Dot(passed.Target - link.Takeoff, direction) >= 0.55f,
                    "Passing the takeoff launched a late jump or steered backward to an already passed point.");
            });

            run($"Blunderville recorded short jump holds one flight and does not repeat its request: {link.Name}", () =>
            {
                var planner = new BlundervilleJumpPlanner();
                var takeoff = planner.Plan(link.Course, Epoch, link.Takeoff, link.Landing, true, false)!;
                Check(takeoff.Jump, "The fixture did not start a jump.");
                var pending = planner.Plan(link.Course, Epoch.AddSeconds(0.05), link.Takeoff, -link.Landing, true, false)!;
                Check(!pending.Jump && pending.Target == takeoff.Target && ReferenceEquals(pending.Corridor, takeoff.Corridor),
                    "Waiting for the game's jumping flag repeated the action or changed the landing.");
                var middle = Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY * 1.4f;
                var flight = planner.Plan(link.Course, Epoch.AddSeconds(0.2), middle, -link.Landing, false, true)!;
                Check(!flight.Jump && !flight.Hold && flight.Target == takeoff.Target
                    && ReferenceEquals(flight.Corridor, takeoff.Corridor), "Airborne steering changed the landing or repeated a jump.");
                var falling = planner.Plan(link.Course, Epoch.AddSeconds(0.4), link.Landing + Vector3.UnitY,
                    -link.Landing, false, false)!;
                Check(!falling.Jump && falling.Target == takeoff.Target && ReferenceEquals(falling.Corridor, takeoff.Corridor),
                    "A cleared jumping flag before ground contact prematurely released the flight corridor.");
            });

            foreach (var flight in new[] { false, true })
                run($"Blunderville actual solver and movement validation permit short jump {(flight ? "flight" : "preparation")}: {link.Name}", () =>
                {
                    var planner = new BlundervilleJumpPlanner();
                    var player = flight ? link.Takeoff : Approach(link);
                    var decision = planner.Plan(link.Course, Epoch, player, link.Landing, true, false)!;
                    if (flight)
                    {
                        player = Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY * 1.4f;
                        decision = planner.Plan(link.Course, Epoch.AddSeconds(0.2), player, link.Landing, false, true)!;
                    }
                    var boundary = NavigationBoundary(link.Course, player, decision.Target, decision.Corridor);
                    var snapshot = new SafeZoneSnapshot(1, Epoch, boundary, [], [],
                        [new(new(1, AreaKind.Goal), new CircleGeometry(decision.Target, 0.2f), [GoalWindow.Persistent(Epoch, 100)])]);
                    var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, 1),
                        new(6, 6, 0, false, 6, 6), new() { GridResolution = 0.5f, HardSafetyMargin = 0.5f, ArenaSafetyMargin = 0.1f, GenerateDebugData = true }, Epoch);
                    Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2
                        && Vector2.Distance(Xz(solution.ExecutablePath[^1].Position), Xz(decision.Target)) <= 0.2f,
                        "The actual local grid cannot reach the precise jump goal through the union corridor.");
                    var segmentCheck = typeof(SafeZoneRebornMovementSystem).GetMethod("IsArenaSegmentSafe", BindingFlags.NonPublic | BindingFlags.Static)!;
                    for (var i = 1; i < solution.ExecutablePath.Count; i++)
                        Check(segmentCheck.Invoke(null, [boundary, solution.ExecutablePath[i - 1].Position, solution.ExecutablePath[i].Position]) is true,
                            "Dispatch rejected a corridor-covered segment because the original floor gap contour remained inside its union.");
                });
        }

        run("Blunderville jump retry waits for the native action throttle and stops after takeoff", () =>
        {
            var link = RecordedJumps().First();
            var planner = new BlundervilleJumpPlanner();
            var first = planner.Plan(link.Course, Epoch, link.Takeoff, link.Landing, true, false)!;
            Check(first.Jump, "The retry fixture did not request a first jump.");
            Check(planner.Plan(link.Course, Epoch.AddSeconds(0.69), link.Takeoff, link.Landing, true, false)?.Jump == false,
                "A failed native action was retried before its 0.7 second throttle.");
            var retry = planner.Plan(link.Course, Epoch.AddSeconds(0.71), link.Takeoff, link.Landing, true, false)!;
            Check(retry.Jump && retry.Target == first.Target && ReferenceEquals(retry.Corridor, first.Corridor),
                "A rejected native jump could not retry toward its original landing.");
            planner.Plan(link.Course, Epoch.AddSeconds(0.8), Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY, link.Landing, false, true);
            Check(planner.Plan(link.Course, Epoch.AddSeconds(1.5), link.Landing + Vector3.UnitY, link.Landing, false, false)?.Jump == false,
                "An expired retry timer caused another request in midair.");
        });

        run("Blunderville corridor union permits covered gaps but rejects an uncovered thin hole exactly", () =>
        {
            var floor = new ThinHoleFloor();
            var from = Vector3.Zero; var to = Vector3.UnitX * 2;
            Check(floor.Contains(from) && floor.Contains(to), "The hole fixture endpoints were outside the floor.");
            Check(!ArenaBoundaryTraversal.ContainsSegment(floor, from, to),
                "A 0.004 metre interior hole was missed by endpoint or sparse-sample testing.");
            var corridor = new BlundervilleJumpCorridor(from, to);
            var union = LocalUnionBoundary(floor, from, to, corridor);
            Check(ArenaBoundaryTraversal.ContainsSegment(union, from, to),
                "A real jump corridor did not replace the old hole boundary along its flight.");
            Check(!ArenaBoundaryTraversal.ContainsSegment(union, from + Vector3.UnitZ * 2, to + Vector3.UnitZ * 2),
                "Covering one part of a floor hole erased its remaining unsupported portion.");
        });

        foreach (var side in new[] { -1f, 1f })
        {
            run($"Blunderville FirstB landing advances the base route on side {side}", () =>
            {
                var race = Started(BlundervilleCourse.FirstB);
                var basePlanner = new BlundervilleRacePlanner();
                var jumpPlanner = new BlundervilleJumpPlanner();
                var links = RecordedJumps().Where(j => j.Course == BlundervilleCourse.FirstB && MathF.Sign(j.Takeoff.X) == side).ToArray();
                for (var i = 0; i < links.Length; i++)
                {
                    var link = links[i]; var now = Epoch.AddSeconds(i * 2);
                    var baseDecision = basePlanner.Update(race, now, link.Takeoff + Vector3.UnitZ * 0.8f, true, false, false, null);
                    jumpPlanner.Plan(link.Course, now, link.Takeoff + Vector3.UnitZ * 0.8f, baseDecision.Target!.Value, true, false);
                    baseDecision = basePlanner.Update(race, now.AddSeconds(0.1), link.Takeoff, true, false, false, null);
                    Console.WriteLine($"FirstB side={side} iteration={i} takeoff={link.Takeoff} cursor={basePlanner.Cursor} target={baseDecision.Target}");
                    Check(jumpPlanner.Plan(link.Course, now.AddSeconds(0.1), link.Takeoff, baseDecision.Target!.Value, true, false)?.Jump == true,
                        $"The base route did not feed the measured FirstB takeoff: side={side}, iteration={i}, cursor={basePlanner.Cursor}, target={baseDecision.Target}.");
                    var middle = Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY;
                    baseDecision = basePlanner.Update(race, now.AddSeconds(0.3), middle, false, true, false, null);
                    jumpPlanner.Plan(link.Course, now.AddSeconds(0.3), middle, baseDecision.Target!.Value, false, true);
                    baseDecision = basePlanner.Update(race, now.AddSeconds(0.6), link.Landing, true, false, false, null);
                    Check(baseDecision.Target is { } forward && forward.Z < link.Landing.Z,
                        $"The base route tried to return to a takeoff node after its actual landing: side={side}, iteration={i}, cursor={basePlanner.Cursor}, target={baseDecision.Target}.");
                    Check(jumpPlanner.Plan(link.Course, now.AddSeconds(0.6), link.Landing, baseDecision.Target!.Value, true, false) == null,
                        "A completed FirstB jump retained its flight corridor.");
                    if (i + 1 < links.Length)
                    {
                        var betweenJumps = new[]
                        {
                            new Vector3(side * 12, 10.03f, -142.5f),
                            new Vector3(side * 12, 10.03f, -146.5f),
                            new Vector3(side * 8, 10.03f, -146.5f),
                        };
                        for (var step = 0; step < betweenJumps.Length; step++)
                        {
                            var stepTime = now.AddSeconds(0.8 + step * 0.1);
                            var position = betweenJumps[step];
                            baseDecision = basePlanner.Update(race, stepTime, position, true, false, false, null);
                            Console.WriteLine($"FirstB side={side} iteration={i} step={step} position={position} cursor={basePlanner.Cursor} target={baseDecision.Target}");
                            Check(baseDecision.Target != null,
                                $"The route ended between FirstB jumps: side={side}, iteration={i}, step={step}, cursor={basePlanner.Cursor}.");
                            jumpPlanner.Plan(link.Course, stepTime, position, baseDecision.Target!.Value, true, false);
                        }
                        Check(baseDecision.Target is { } next && Near(next, links[i + 1].Takeoff),
                            $"The side board and cross board did not advance to the next FirstB takeoff: side={side}, iteration={i}, cursor={basePlanner.Cursor}, target={baseDecision.Target}.");
                    }
                }
            });

            run($"Blunderville Final links 153 to 149 to 145 to 141 to 137 and then releases on side {side}", () =>
            {
                var race = Started(BlundervilleCourse.Final);
                var basePlanner = new BlundervilleRacePlanner();
                var jumpPlanner = new BlundervilleJumpPlanner();
                var links = RecordedJumps().Where(j => j.Course == BlundervilleCourse.Final && MathF.Sign(j.Takeoff.X) == side).ToArray();
                var baseDecision = basePlanner.Update(race, Epoch, links[0].Takeoff, true, false, false, null);
                var current = jumpPlanner.Plan(BlundervilleCourse.Final, Epoch, links[0].Takeoff, baseDecision.Target!.Value, true, false);
                for (var i = 0; i < links.Length; i++)
                {
                    var link = links[i]; var now = Epoch.AddSeconds(i);
                    Check(current is { Jump: true, Hold: false } && Near(current.Target, link.Landing),
                        "The Final stair sequence skipped a measured landing or reversed a jump.");
                    var corridor = current!.Corridor;
                    var pending = jumpPlanner.Plan(BlundervilleCourse.Final, now.AddSeconds(0.05), link.Takeoff, baseDecision.Target!.Value, true, false)!;
                    Check(!pending.Jump && ReferenceEquals(pending.Corridor, corridor), "The Final jump repeated before the game's airborne state arrived.");
                    var middle = Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY * 1.4f;
                    var airborne = jumpPlanner.Plan(BlundervilleCourse.Final, now.AddSeconds(0.3), middle, baseDecision.Target!.Value, false, true)!;
                    Check(!airborne.Jump && airborne.Target == link.Landing && ReferenceEquals(airborne.Corridor, corridor),
                        "The Final stair flight changed its measured landing.");
                    baseDecision = basePlanner.Update(race, now.AddSeconds(0.7), link.Landing, true, false, false, null);
                    Check(baseDecision.Target is { } forward && forward.Z < link.Landing.Z, "The base Final route aimed behind the completed stair jump.");
                    current = jumpPlanner.Plan(BlundervilleCourse.Final, now.AddSeconds(0.7), link.Landing, baseDecision.Target!.Value, true, false);
                }
                Check(current == null, "Landing at 137 repeated the last jump or steered back to takeoff 141.");
            });
        }

        run("Blunderville FirstB low floor never starts or retains an upper-floor jump", () =>
        {
            var link = RecordedJumps().First(j => j.Course == BlundervilleCourse.FirstB);
            var planner = new BlundervilleJumpPlanner();
            Check(planner.Plan(link.Course, Epoch, link.Takeoff with { Y = 7 }, link.Landing, true, false) == null,
                "A player on the lower recovery floor started an upper-floor jump.");
            planner.Plan(link.Course, Epoch.AddSeconds(0.1), link.Takeoff, link.Landing, true, false);
            planner.Plan(link.Course, Epoch.AddSeconds(0.2), Vector3.Lerp(link.Takeoff, link.Landing, 0.5f) + Vector3.UnitY, link.Landing, false, true);
            Check(planner.Plan(link.Course, Epoch.AddSeconds(0.5), (Vector3.Lerp(link.Takeoff, link.Landing, 0.5f)) with { Y = 7 }, link.Landing, true, false) == null,
                "Landing on the lower floor retained the unreachable upper landing instead of releasing recovery navigation.");
        });

        foreach (var reset in new[] { "rewind", "teleport", "course change", "explicit reset", "nonfinite input" })
            run($"Blunderville short jump resets its pending landing on {reset}", () =>
            {
                var link = RecordedJumps().First(j => j.Course == BlundervilleCourse.FirstB && j.Takeoff.X > 0);
                var planner = new BlundervilleJumpPlanner();
                var takeoff = planner.Plan(link.Course, Epoch, link.Takeoff, link.Landing, true, false)!;
                var player = link.Takeoff + Vector3.UnitZ * 0.8f;
                var target = link.Landing; var now = Epoch.AddSeconds(0.1);
                switch (reset)
                {
                    case "rewind": now = Epoch.AddSeconds(-1); break;
                    case "teleport": player.X = -player.X; target.X = -target.X; break;
                    case "course change":
                        Check(planner.Plan(BlundervilleCourse.Delivery, now, player, target, true, false) == null, "An unrelated course retained a short jump.");
                        now = now.AddSeconds(0.1); break;
                    case "explicit reset": planner.Reset(); break;
                    case "nonfinite input":
                        Check(planner.Plan(link.Course, now, new(float.NaN, 10, -134), target, true, false) == null, "Nonfinite replay input produced a jump.");
                        now = now.AddSeconds(0.1); break;
                }
                var preparation = planner.Plan(link.Course, now, player, target, true, false);
                Check(preparation is { Jump: false } && !ReferenceEquals(preparation.Corridor, takeoff.Corridor)
                    && MathF.Abs(preparation.Target.X - (player.X < 0 ? -10.25f : 10.25f)) < 0.001f && preparation.Target.Z == -134.2f,
                    "Reset preserved its old flight landing or corridor in a new simulation state.");
            });
    }

    internal static IEnumerable<RecordedJump> RecordedJumps()
    {
        foreach (var side in new[] { -1f, 1f })
        {
            yield return new($"FirstB {side} entry", BlundervilleCourse.FirstB, new(side * 10.25f, 10.03f, -134.2f), new(side * 11.9f, 10.03f, -138.4f), 0.8f);
            yield return new($"FirstB {side} second", BlundervilleCourse.FirstB, new(side * 8, 10.03f, -151.5f), new(side * 8, 10.03f, -155.9f), 0.8f);
            yield return new($"Final {side} 153-149", BlundervilleCourse.Final, new(side * BlundervilleRoutes.FinalStairLane, 31.5f, 153), new(side * BlundervilleRoutes.FinalStairLane, 32.6f, 149), 0.4f);
            yield return new($"Final {side} 149-145", BlundervilleCourse.Final, new(side * BlundervilleRoutes.FinalStairLane, 32.6f, 149), new(side * BlundervilleRoutes.FinalStairLane, 33.5f, 145), 0.4f);
            yield return new($"Final {side} 145-141", BlundervilleCourse.Final, new(side * BlundervilleRoutes.FinalStairLane, 33.5f, 145), new(side * BlundervilleRoutes.FinalStairLane, 34.4f, 141), 0.4f);
            yield return new($"Final {side} 141-137", BlundervilleCourse.Final, new(side * BlundervilleRoutes.FinalStairLane, 34.4f, 141), new(side * BlundervilleRoutes.FinalStairLane, 35.8f, 137), 0.4f);
        }
        yield return new("Ring west southwest", BlundervilleCourse.Ring, new(183.8f, 10, -138.6f), new(185.6f, 10, -142.8f), 0.8f);
        yield return new("Ring east southeast", BlundervilleCourse.Ring, new(209.5f, 10, -147.7f), new(210.3f, 10, -143.3f), 0.8f);
        yield return new("Ring east northward", BlundervilleCourse.Ring, new(210.8f, 10, -142.6f), new(213.2f, 10, -138.7f), 0.8f);
    }

    private static BlundervilleRaceState Started(BlundervilleCourse course)
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001,
            course == BlundervilleCourse.FirstB ? 0x4E2Eu : 0x4E31u, 0, 0, 0, Epoch);
        return state;
    }

    private static IGeometryShape NavigationBoundary(BlundervilleCourse course, Vector3 player, Vector3 target, IGeometryShape corridor)
        => LocalUnionBoundary(BlundervilleRoutes.Boundary(course), player, target, corridor);

    private static IGeometryShape LocalUnionBoundary(IGeometryShape source, Vector3 player, Vector3 target, IGeometryShape corridor)
    {
        var combinedType = typeof(BlundervilleNavigation).GetNestedType("CrossingCorridor", BindingFlags.NonPublic)!;
        var localType = typeof(BlundervilleNavigation).GetNestedType("LocalCorridor", BindingFlags.NonPublic)!;
        var combined = (IGeometryShape)Activator.CreateInstance(combinedType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [source, corridor], null)!;
        var middle = (player + target) * 0.5f;
        var center = new Vector3(MathF.Round(middle.X / 10) * 10, player.Y, MathF.Round(middle.Z / 10) * 10);
        return (IGeometryShape)Activator.CreateInstance(localType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, [combined, center, 8f], null)!;
    }

    private static Vector2 Xz(Vector3 value) => new(value.X, value.Z);
    private static Vector3 Direction(RecordedJump link) => Vector3.Normalize(new(link.Landing.X - link.Takeoff.X, 0, link.Landing.Z - link.Takeoff.Z));
    private static Vector3 Approach(RecordedJump link) => link.Takeoff - Direction(link) * link.Approach;
    private static bool Near(Vector3 actual, Vector3 expected) => Vector3.Distance(actual, expected) < 0.001f;
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class ThinHoleFloor : IGeometryShape
    {
        private readonly RectangleGeometry _outside = new(Vector3.Zero, 0, 20, 10);
        private readonly RectangleGeometry _hole = new(new Vector3(1.003f, 0, 0), 0, 0.004f, 6);
        public GeometryKind Kind => _outside.Kind;
        public Vector3 Position => _outside.Position;
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => _outside.Contains(point, epsilon) && !_hole.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => _outside.IntersectsSegment(start, end, epsilon);
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => _outside.GetContours(segments).Concat(_hole.GetContours(segments)).ToArray();
        public GeometryBounds GetBounds() => _outside.GetBounds();
    }
}
