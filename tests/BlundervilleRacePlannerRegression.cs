using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using MasterPlayer.Mechanics.Blunderville;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class BlundervilleRacePlannerRegression
{
    private static readonly DateTime Epoch = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly MovementCapability Movement = new(6, 6, 0, false, 6, 6);
    private static readonly SolveOptions Options = new()
    {
        GridResolution = 0.5f, HardSafetyMargin = 0.5f, GenerateDebugData = true,
        DangerStartSafetyTime = 0.5f, DangerEndSafetyTime = 0.3f,
        PlanningHorizon = 10, MaximumWaitTime = 8,
    };
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Blunderville");

    public static void Run(Action<string, Action> run)
    {
        run("Blunderville recorded slope cliff never triggers an unplanned jump", () =>
        {
            var state = new BlundervilleRaceState();
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, 20013, 0, 0, 0, Epoch);
            var planner = new BlundervilleRacePlanner();
            foreach (var p in new[] { new Vector3(-208.82f, 11.276f, -126.238f), new Vector3(-207.141f, 11.368f, -127.245f) })
            {
                var decision = planner.Update(state, Epoch, p, true, false, true, null);
                Check(!decision.Jump && !decision.Hold && decision.Target != null, "A missing forward floor probe launched toward the central pit.");
            }
        });
        foreach (var file in Directory.GetFiles(Fixtures, "race-arr-*.json").Order())
            run($"Blunderville race {Path.GetFileNameWithoutExtension(file)}: recorded start, status and qualification gates", () => RecordedRounds(file));

        foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
        {
            run($"Blunderville race {course}: enabled routes remain in the measured corridor", () =>
            {
                var start = BlundervilleRoutes.VerifiedStart(course);
                Check(BlundervilleRoutes.FromPosition(start) == course, "The verified start selects another course.");
                Check(BlundervilleRoutes.Boundary(course).Contains(start, 0.15f), "The verified start is outside the walkable course.");
                foreach (var side in new[] { -1f, 1f })
                {
                    var nodes = BlundervilleRoutes.Nodes(course, start + Vector3.UnitX * side);
                    Check(nodes.Count != 0, "A verified course has no navigation nodes.");
                    foreach (var node in nodes)
                        Check(BlundervilleRoutes.IsInsideCorridor(course, node.Position, 0.15f), $"{course} route node {node.Position} is outside the measured course.");
                }
            });
            run($"Blunderville race {course}: actual solver advances inside the course", () => InitialSolverAdvance(course));
        }
        run("Blunderville race ordinary route legs reach every verified node on both branches", ValidateOrdinaryLegs);

        foreach (var color in new ushort[] { 35, 36, 37 })
            run($"Blunderville race crystal {color}: scoring spoke and return to central pickup", () => DeliveryTrip(color));

        run("Blunderville race ring completes repeated circuits until recorded director qualification", () =>
        {
            var state = Start(0x4E2F);
            var planner = new BlundervilleRacePlanner();
            var nodes = BlundervilleRoutes.Nodes(state.Course, BlundervilleRoutes.VerifiedStart(state.Course));
            var now = Epoch;
            for (var lap = 0; lap < 3; ++lap)
                foreach (var node in nodes)
                {
                    now = now.AddSeconds(0.5);
                    var decision = planner.Update(state, now, node.Position, true, false, false, null);
                    Check(!decision.Hold && decision.Target != null, "Completing the ring stopped movement before a director result.");
                    Check(BlundervilleRoutes.Boundary(state.Course).Contains(decision.Target.Value, 0.15f), "Ring wrap selected a goal through the central void.");
                }
            state.Director(BlundervilleRaceState.DirectorInstance, 7, 0, 0, 0, 0, now);
            var stopped = planner.Update(state, now, nodes[^1].Position, true, false, false, null);
            Check(stopped.Hold && stopped.Target == null && !stopped.Jump, "Director qualification retained ring movement.");
        });

        foreach (var (status, offset) in new (uint, float)[] { (3698, 0), (3699, MathF.PI), (3700, MathF.PI / 2), (3701, -MathF.PI / 2) })
            run($"Blunderville race pending command {status}: facing makes the forced displacement follow the route", () =>
            {
                var state = Start(0x4E2E);
                var planner = new BlundervilleRacePlanner();
                var pos = new Vector3(5, 10.03f, -260);
                state.SynchronizeStatuses([new(status, 0, Epoch.AddSeconds(0.6))], Epoch);
                var decision = planner.Update(state, Epoch, pos, true, false, true, null);
                Check(decision.Hold && !decision.Jump && decision.Target != null && decision.Facing != null, "A pending command failed to stop and pre-align.");
                var desired = Bearing(pos, decision.Target.Value);
                Check(MathF.Abs(MathF.IEEERemainder(decision.Facing.Value + offset - desired, MathF.Tau)) < 0.001f,
                    "The prepared facing sends the forced displacement away from its route.");
            });

        run("Blunderville race native arrow steering keeps a solver goal while the route heading differs", () =>
        {
            var state = Start(0x4E2E);
            var planner = new BlundervilleRacePlanner();
            var pos = new Vector3(5, 10.03f, -260);
            state.SynchronizeStatuses([new(3694, 360, DateTime.MaxValue)], Epoch);
            var strict = planner.Update(state, Epoch, pos, true, false, true, 0);
            Check(strict.Hold && !strict.Jump, "Without native local steering, the opposite arrow accepted input.");
            var native = planner.Update(state, Epoch, pos, true, false, true, 0, nativeMisdirection: true);
            Check(!native.Hold && native.Target != null && !native.Jump, "Native local steering lost its solver goal or accepted an off-arrow jump.");
            state.SynchronizeStatuses([new(3694, 360, DateTime.MaxValue), new(149, 0, Epoch.AddSeconds(3))], Epoch);
            Check(planner.Update(state, Epoch, pos, true, false, true, 0, nativeMisdirection: true).Hold,
                "Native arrow support bypassed an independent stun lock.");
        });

        run("Blunderville race FirstA below a vanished bridge holds until the player returns to its surface", () =>
        {
            var state = Start(0x4E2D);
            var planner = new BlundervilleRacePlanner();
            var fallen = planner.Update(state, Epoch, new(-203.5f, 6, -338), true, false, true, null);
            Check(fallen.Hold && fallen.Target == null && !fallen.Jump, "The lower floor kept chasing a high bridge goal.");
            var restored = planner.Update(state, Epoch.AddSeconds(1), new(-203.5f, 13.565f, -338), true, false, false, null);
            Check(!restored.Hold && restored.Target != null, "Returning to the bridge surface did not resume the race route.");
        });

        foreach (var side in new[] { -1f, 1f })
            run($"Blunderville race FirstB fallen bridge side {side}: recover through its approach ramp", () =>
            {
                var state = Start(0x4E2E);
                var planner = new BlundervilleRacePlanner();
                var fallen = new Vector3(side * 11.5f, 6.03f, -301);
                var decision = planner.Update(state, Epoch, fallen, true, false, false, null);
                Check(planner.Recovering && !decision.Hold && decision.Target is { } target && target.Z >= fallen.Z
                    && MathF.Abs(target.X) > MathF.Abs(fallen.X) && target.Y < 8,
                    "Recovery steered into the high bridge side or continued away from the approach ramp.");
                var recovery = BlundervilleRoutes.FirstBRecovery(fallen);
                var boundary = planner.RecoveryCorridor!;
                var solution = Solve(boundary, fallen, decision.Target!.Value, Epoch, [], .2f);
                Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, "The lower recovery floor has no executable route to its approach ramp.");
                ValidatePath(solution, boundary, []);
                for (var i = 0; i < recovery.Count; ++i)
                    planner.Update(state, Epoch.AddSeconds(i + 1), recovery[i].Position, true, false, false, null);
                decision = planner.Update(state, Epoch.AddSeconds(5), new(side * 5, 10.03f, -271), true, false, false, null);
                Check(!planner.Recovering && decision.Target is { } forward && forward.Z < -271, "The recovered player retained its lower-floor recovery route.");
            });

        run("Blunderville race final stairs jump only from the ground and stop during execution", () =>
        {
            var state = Start(0x4E31);
            var planner = new BlundervilleRacePlanner();
            var pos = new Vector3(BlundervilleRoutes.FinalStairLane, 31.5f, 153.8f);
            var approach = planner.Update(state, Epoch, pos, true, false, false, null);
            Check(!approach.Jump, "The base route jumped before reaching its measured takeoff.");
            var jump = new BlundervilleJumpPlanner().Plan(state.Course, Epoch, new(BlundervilleRoutes.FinalStairLane, 31.5f, 153), approach.Target!.Value, true, false);
            Check(jump is { Jump: true } && jump.Target.Z == 149, "The measured final stair takeoff lost its landing.");
            Check(!planner.Update(state, Epoch.AddSeconds(0.1), pos + Vector3.UnitY, false, true, true, null).Jump, "The airborne player requested another jump.");
            state.SynchronizeStatuses([new(3737, 0, Epoch.AddSeconds(2))], Epoch.AddSeconds(0.2));
            var locked = planner.Update(state, Epoch.AddSeconds(0.2), pos, true, false, true, null);
            Check(locked.Hold && !locked.Jump, "Forced displacement allowed ordinary stair jumping.");
        });

        // 01:39 ARR: the race started at x=0.47 (right route); the Final lane
        // planner handed over at the left stair run-up. The base route must
        // continue on the left stairs instead of pulling back to node 1.
        run("Blunderville race final hand-over on the other stair side continues to its takeoff", () =>
        {
            var state = Start(0x4E31);
            var planner = new BlundervilleRacePlanner();
            // MP's 0.5–1s samples from the ARR (the lane planner drove the slope).
            Vector3[] track = [new(0.47f, 4.23f, 322.99f), new(.23f, 3.22f, 305.35f), new(.05f, 3.22f, 293.29f), new(1.18f, 3.68f, 284.47f),
                new(-3.68f, 5.63f, 274.71f), new(-6, 6, 266.93f), new(-5.02f, 9.05f, 252.03f), new(-5.02f, 13.54f, 236.9f),
                new(-9.54f, 14.97f, 220.97f), new(-13.63f, 17.32f, 212.76f), new(-8.77f, 19.91f, 205.74f), new(-1.94f, 23.55f, 195.91f),
                new(4.01f, 26.66f, 187.43f), new(3.07f, 29.25f, 176.72f), new(1.42f, 30.47f, 164.87f), new(-1.27f, 31.02f, 159.47f)];
            for (var i = 0; i < track.Length; ++i) planner.Update(state, Epoch.AddSeconds(i), track[i], true, false, false, null);
            var handOver = new Vector3(-3.86f, 31.45f, 155.32f);
            var decision = planner.Update(state, Epoch.AddSeconds(35), handOver, true, false, false, null);
            Check(decision.Target is { } target && target.X < 0 && target.Z < 155.32f && target.Z > 150,
                $"The hand-over target {decision.Target} is not the left stair ahead.");
            var approach = planner.Update(state, Epoch.AddSeconds(35.3), new(-BlundervilleRoutes.FinalStairLane, 31.5f, 153.8f), true, false, false, null);
            var jump = new BlundervilleJumpPlanner().Plan(state.Course, Epoch.AddSeconds(35.4),
                new(-BlundervilleRoutes.FinalStairLane, 31.5f, 153), approach.Target!.Value, true, false);
            Check(jump is { Jump: true } && jump.Target.X < 0 && jump.Target.Z == 149, "The left stair takeoff did not jump to its landing.");
            // Lane changes across the centre on the slope keep the route usable.
            var slope = planner.Update(state, Epoch.AddSeconds(20), new(5, 25.6f, 200), true, false, false, null);
            Check(slope.Target is { } ahead && ahead.Z < 200 && ahead.X > 0, $"Switching back to the right side lost the forward route ({slope.Target}).");
        });

        run("Blunderville race course geometry preserves measured voids", () =>
        {
            Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(new(-200, 14, -140)), "FirstA's split approach was filled over its central void.");
            Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.Ring).Contains(new(200, 10, -125)), "The ring's central void became walkable.");
            Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.Final).Contains(new(9, 20, 210)), "The final lattice gap became walkable.");
            foreach (var point in new[] { new Vector3(10.8f, 10, -136.2f), new Vector3(8, 10, -153.8f) })
                Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.FirstB).Contains(point), "Restoring the narrow board filled a FirstB jump void.");
            foreach (var point in new[] { new Vector3(184.6f, 10, -140.5f), new Vector3(209.9f, 10, -145.5f), new Vector3(212, 10, -140.6f) })
                Check(!BlundervilleRoutes.Boundary(BlundervilleCourse.Ring).Contains(point), "Restoring the southern board filled a Ring jump void.");
            var a = new Vector3(-211, 15.2f, -140);
            var b = new Vector3(-189, 15.2f, -140);
            var solution = Solve(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), a, b, Epoch, []);
            if (solution.IsSuccess) ValidatePath(solution, BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), []);
        });

        run("Blunderville race indexed corridor containment preserves signed-distance epsilon semantics", () =>
        {
            var random = new Random(958);
            foreach (var course in Enum.GetValues<BlundervilleCourse>().Where(c => c != BlundervilleCourse.None))
            {
                var boundary = BlundervilleRoutes.Boundary(course);
                var bounds = boundary.GetBounds().Expand(2);
                var points = boundary.GetContours().SelectMany(c => c).Concat(Enumerable.Range(0, 250).Select(_ =>
                    new Vector3(bounds.MinX + random.NextSingle() * bounds.Width, 0, bounds.MinZ + random.NextSingle() * bounds.Height)));
                foreach (var point in points)
                    foreach (var epsilon in new[] { 0, 0.0001f, 0.05f, 0.5f, -0.25f })
                        Check(boundary.Contains(point, epsilon) == (BlundervilleRoutes.SignedDistanceToCorridor(course, point) <= epsilon),
                            $"Indexed corridor changed containment at {point} with epsilon {epsilon}.");
            }
        });

        run("Blunderville race recorded bridge timing selects an edge jump to the adjacent surviving lane", BridgeEdgeJump);
        run("Blunderville race final ARR wave: solver simulation advances without entering timed damage or void", SimulateFinalWave);
    }

    private static void RecordedRounds(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        Check(doc.RootElement.GetProperty("territory").GetInt32() == 1165, "The fixture is from another territory.");
        foreach (var round in doc.RootElement.GetProperty("rounds").EnumerateArray())
        {
            var course = Enum.Parse<BlundervilleCourse>(round.GetProperty("course").GetString()!);
            var stage = round.GetProperty("stage").GetUInt32();
            var pos = Vec(round.GetProperty("start"));
            var state = new BlundervilleRaceState();
            var planner = new BlundervilleRacePlanner();
            state.Director(BlundervilleRaceState.DirectorInstance, 5, 0, 0, 0, 0, Epoch.AddSeconds(-2));
            Check(planner.Update(state, Epoch.AddSeconds(-1), pos, true, false, false, null).Hold, "The player moved before the recorded course start.");
            state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, stage, 0, 0, 0, Epoch);
            var started = planner.Update(state, Epoch, pos, true, false, false, null);
            Check(state.Course == course && started.Target != null && !started.Hold, "The recorded start failed to release its course route.");
            if (course is BlundervilleCourse.FirstA or BlundervilleCourse.FirstB or BlundervilleCourse.Final)
                Check(started.Target.Value.Z < pos.Z, "The route starts in the opposite direction to the recorded race.");
            foreach (var status in round.GetProperty("statuses").EnumerateArray().Where(s => s.GetProperty("id").GetUInt32() is 149 or 3694 or 3708 or 3737 or 3802))
            {
                var now = Epoch.AddSeconds(status.GetProperty("t").GetDouble());
                var duration = status.GetProperty("duration").GetDouble();
                var id = status.GetProperty("id").GetUInt32();
                var statusPos = Vec(status.GetProperty("pos"));
                var expires = duration == 0 ? DateTime.MaxValue : now.AddSeconds(duration);
                if (id == 3802 && duration > 0) now = expires.AddSeconds(-0.4);
                state.SynchronizeStatuses([new(id, status.GetProperty("param").GetUInt16(), expires)], now);
                var held = planner.Update(state, now, statusPos, true, false, true, null);
                Check(held.Hold && !held.Jump, $"Recorded movement restriction {id} accepted race input.");
                state.SynchronizeStatuses([], now);
            }
            var end = Epoch.AddSeconds(round.GetProperty("duration").GetDouble());
            state.SynchronizeStatuses([], end);
            state.Director(BlundervilleRaceState.DirectorInstance, round.GetProperty("qualified").GetBoolean() ? 7u : 6u, 0, 0, 0, 0, end);
            var stopped = planner.Update(state, end, pos, true, false, true, null);
            Check(stopped.Hold && stopped.Target == null && !stopped.Jump, "The recorded result failed to stop race input.");
        }
    }

    private static void InitialSolverAdvance(BlundervilleCourse course)
    {
        var pos = BlundervilleRoutes.VerifiedStart(course);
        var state = Start(Stage(course));
        var decision = new BlundervilleRacePlanner().Update(state, Epoch, pos, true, false, false, null);
        Check(decision.Target != null && !decision.Hold, "The initial planner did not publish a moving goal.");
        var boundary = BlundervilleRoutes.Boundary(course);
        var solution = Solve(boundary, pos, decision.Target.Value, Epoch, []);
        Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, $"{course}: actual solver did not find a starting route ({solution.Status}).");
        ValidatePath(solution, boundary, []);
        var next = Follow(solution.ExecutablePath, Epoch.AddSeconds(0.5), pos);
        Check(DistanceXZ(next, pos) > 0.4f, "The accepted starting route did not advance the player.");
        var remaining = DistanceXZ(solution.ExecutablePath[^1].Position, decision.Target.Value);
        Console.WriteLine($"SIM {course} start: {solution.DescribeOutcome()}, 0.5 s advance {DistanceXZ(next, pos):F2} m, goal distance {remaining:F2} m");
        Check(remaining <= 1.3f, $"{course}: solver fallback stops {remaining:F2} m before its planner node, leaving the race cursor stuck.");
    }

    private static void DeliveryTrip(ushort color)
    {
        var state = Start(0x4E30);
        var planner = new BlundervilleRacePlanner();
        var pickup = BlundervilleRaceState.PickupCenter;
        var wait = planner.Update(state, Epoch, pickup, true, false, false, null);
        Check(wait.Hold && wait.Target == pickup, "Touching the pickup fabricated a crystal before its status arrived.");
        state.SynchronizeStatuses([new(3704, color, DateTime.MaxValue)], Epoch.AddSeconds(1));
        var destination = state.DeliveryTarget!.Value;
        var outbound = BlundervilleRoutes.DeliveryNodes(pickup, destination);
        var now = Epoch.AddSeconds(1);
        var previous = pickup;
        foreach (var node in outbound)
        {
            ValidateDeliveryLeg(previous, node, now);
            var decision = planner.Update(state, now, node.Position, true, false, false, null);
            Check(decision.Target != null && BlundervilleRoutes.Boundary(state.Course).Contains(decision.Target.Value, 0.15f), "Crystal delivery selected a goal outside its scoring spoke.");
            previous = node.Position;
            now = now.AddSeconds(1);
        }
        var arrived = planner.Update(state, now, destination, true, false, false, null);
        Check(arrived.Hold && state.Score == 0 && state.CarryCrystalParam == color, "Reaching the floor fabricated a delivery confirmation.");
        state.Director(BlundervilleRaceState.DirectorInstance, 3, 1, 0, 0, 0, now);
        state.SynchronizeStatuses([], now);
        foreach (var node in outbound.Reverse())
        {
            ValidateDeliveryLeg(previous, node, now);
            var decision = planner.Update(state, now, node.Position, true, false, false, null);
            Check(decision.Target != null && BlundervilleRoutes.Boundary(state.Course).Contains(decision.Target.Value, 0.15f), "Returning for another crystal skipped its measured spoke.");
            previous = node.Position;
            now = now.AddSeconds(1);
        }
        var returned = planner.Update(state, now, pickup, true, false, false, null);
        Check(returned.Hold && returned.Target == pickup && state.Score == 1, "The completed return did not wait at the central pickup.");
        state.Director(BlundervilleRaceState.DirectorInstance, 3, 3, 0, 0, 0, now);
        var done = planner.Update(state, now, pickup, true, false, false, null);
        Check(done.Hold && done.Target == null, "Three confirmed deliveries requested another trip.");
    }

    private static void ValidateDeliveryLeg(Vector3 from, BlundervilleRouteNode destination, DateTime now)
    {
        if (DistanceXZ(from, destination.Position) < 0.1f) return;
        var boundary = BlundervilleRoutes.Boundary(BlundervilleCourse.Delivery);
        var solution = Solve(boundary, from, destination.Position, now, []);
        Check(solution.IsSuccess && solution.ExecutablePath.Count > 0, $"The crystal spoke leg {from} to {destination.Position} has no solver route.");
        ValidatePath(solution, boundary, []);
        var remaining = DistanceXZ(solution.ExecutablePath[^1].Position, destination.Position);
        Check(remaining <= destination.ArrivalRadius, $"Crystal spoke solver stops {remaining:F2} m from {destination.Position}, outside its node arrival radius.");
    }

    private static void ValidateOrdinaryLegs()
    {
        var failures = new List<string>();
        foreach (var course in new[] { BlundervilleCourse.FirstA, BlundervilleCourse.FirstB, BlundervilleCourse.Ring, BlundervilleCourse.Final })
            foreach (var side in new[] { -1f, 1f })
            {
                var previous = BlundervilleRoutes.VerifiedStart(course) + Vector3.UnitX * side * 0.5f;
                var nodes = BlundervilleRoutes.Nodes(course, previous);
                var boundary = BlundervilleRoutes.Boundary(course);
                var jumps = new BlundervilleJumpPlanner();
                for (var i = 0; i < nodes.Count; ++i)
                {
                    var node = nodes[i];
                    // FirstA's timed bridges use the independent edge/landing planner
                    // and a temporary corridor; their physical gap is not ground.
                    var bridge = course == BlundervilleCourse.FirstA && (node.Position.Z is <= -322 and >= -354 || previous.Z is <= -322 and >= -354);
                    if (!bridge && DistanceXZ(previous, node.Position) > 0.1f)
                    {
                        var scoped = boundary;
                        var jump = i > 0 && nodes[i - 1].Jump ? jumps.Plan(course, Epoch.AddSeconds(i), previous, node.Position, true, false) : null;
                        if (jump is { Jump: false } && DistanceXZ(jump.Target, node.Position) > .1f)
                        {
                            var preparationBoundary = new UnionBoundary(boundary, jump.Corridor);
                            var preparation = Solve(preparationBoundary, previous, jump.Target, Epoch.AddSeconds(i), [], .2f);
                            if (!preparation.IsSuccess || preparation.ExecutablePath.Count == 0 || DistanceXZ(preparation.ExecutablePath[^1].Position, jump.Target) > .25f)
                                failures.Add($"{course} side {side} node {i}: could not reach actual helper takeoff {jump.Target}.");
                            else
                            {
                                ValidatePath(preparation, boundary, []);
                                previous = preparation.ExecutablePath[^1].Position;
                                previous.Y = jump.Target.Y;
                                jump = jumps.Plan(course, Epoch.AddSeconds(i + .05), previous, node.Position, true, false);
                            }
                        }
                        if (jump != null && DistanceXZ(jump.Target, node.Position) < 0.1f)
                        {
                            Check(jump.Jump && !jump.Hold, $"{course}: reaching the recorded takeoff did not authorize its short jump.");
                            scoped = new UnionBoundary(boundary, jump.Corridor);
                        }
                        var solution = Solve(scoped, previous, node.Position, Epoch, []);
                        var remaining = solution.ExecutablePath.Count == 0 ? DistanceXZ(previous, node.Position)
                            : DistanceXZ(solution.ExecutablePath[^1].Position, node.Position);
                        if (solution.IsSuccess) ValidatePath(solution, scoped, []);
                        if (!solution.IsSuccess || remaining > node.ArrivalRadius)
                            failures.Add($"{course} side {side} node {i} {previous} -> {node.Position}: {solution.Status}, remaining {remaining:F2} m / radius {node.ArrivalRadius:F2}");
                        else
                        {
                            // Carry the accepted solver landing into the next leg;
                            // snapping to a catalog point on an eroded edge can put
                            // the next start outside the hard-margin raster grid.
                            previous = solution.ExecutablePath.Count > 0 ? solution.ExecutablePath[^1].Position : previous;
                            previous.Y = node.Position.Y;
                            if (jump is { Jump: true })
                            {
                                var flight = jumps.Plan(course, Epoch.AddSeconds(i + .2), (nodes[i - 1].Position + previous) / 2 + Vector3.UnitY,
                                    node.Position, false, true);
                                Check(flight != null && !flight.Jump && flight.Target == jump.Target, "A recorded short jump lost its airborne landing.");
                                // Intermediate FirstB/Ring ground nodes would call
                                // the live helper every frame and clear this latch.
                                // Final consecutive stair jumps are cleared by the
                                // next iteration, which retains its returned jump.
                                if (course != BlundervilleCourse.Final)
                                    jumps.Plan(course, Epoch.AddSeconds(i + .8), previous, i + 1 < nodes.Count ? nodes[i + 1].Position : node.Position, true, false);
                            }
                            continue;
                        }
                    }
                    previous = node.Position;
                }
            }
        Check(failures.Count == 0, "Ordinary race routes cannot reach these nodes:\n" + string.Join("\n", failures));
    }

    private static void BridgeEdgeJump()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "race-arr-08.json")));
        var round = doc.RootElement.GetProperty("rounds").EnumerateArray().First(r => r.GetProperty("course").GetString() == "FirstA");
        var tracker = new BlundervilleBridgeTracker();
        var now = Epoch;
        var maps = round.GetProperty("maps").EnumerateArray().ToArray();
        // Capture the overlap after the latest appear packet. Later disappear
        // packets must not be applied before replaying this preparation frame.
        var snapshotTime = maps.Where(m => m.GetProperty("state").GetUInt32() == 0x20001)
            .Max(m => m.GetProperty("t").GetDouble());
        foreach (var map in maps.Where(m => m.GetProperty("t").GetDouble() <= snapshotTime))
        {
            now = Epoch.AddSeconds(map.GetProperty("t").GetDouble());
            tracker.Map(map.GetProperty("index").GetByte(), map.GetProperty("state").GetUInt32(), now);
        }
        var enabled = Enumerable.Range(14, 4).Select(i => (Index: (byte)i, Close: tracker.BridgeNextDisappearAt((byte)i, now)))
            .Where(p => p.Close != null && tracker.BridgeEnabled(p.Index, now)).OrderBy(p => p.Close).First();
        now = enabled.Close!.Value.AddSeconds(-0.9);
        var centers = new[] { -210.523f, -203.5f, -196.5f, -189.5f };
        var pos = new Vector3(centers[enabled.Index - 14], 13.565f, -340);
        var planner = new BlundervilleBridgePlanner();
        var edge = planner.Plan(tracker, now, pos, new(-200, 13.565f, -367), true, false)!;
        Check(!edge.Hold && !edge.Jump && edge.CrossingCorridor != null && MathF.Abs(edge.Target.X - pos.X) > 1.5f,
            "An expiring bridge requested a seven metre center-to-center jump or stayed on the disappearing lane.");
        Check(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA).Contains(edge.Target), "Bridge takeoff was placed over the gap.");
        var preparedBoundary = new UnionBoundary(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), edge.CrossingCorridor);
        var prepare = Solve(preparedBoundary, pos, edge.Target, now, [], 0.2f);
        Check(prepare.IsSuccess && prepare.ExecutablePath.Count >= 2 && DistanceXZ(prepare.ExecutablePath[^1].Position, edge.Target) <= 0.3f,
            "The actual small bridge-preparation goal stopped before its takeoff edge.");
        ValidatePath(prepare, BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), []);
        var jump = planner.Plan(tracker, now.AddSeconds(0.1), edge.Target, new(-200, 13.565f, -367), true, false)!;
        Check(jump.Jump && !jump.Hold && jump.CrossingCorridor != null && DistanceXZ(edge.Target, jump.Target) < 4.5f,
            "The edge failed to publish a reachable adjacent-lane landing and temporary crossing corridor.");
        var boundary = new UnionBoundary(BlundervilleRoutes.Boundary(BlundervilleCourse.FirstA), jump.CrossingCorridor!);
        var solution = Solve(boundary, edge.Target, jump.Target, now.AddSeconds(0.1), [], 0.2f);
        Check(solution.IsSuccess && solution.ExecutablePath.Count >= 2, $"The safe adjacent bridge landing has no solver route ({solution.Status}).");
        ValidatePath(solution, boundary, []);
        var flight = planner.Plan(tracker, now.AddSeconds(0.4), (edge.Target + jump.Target) / 2 + Vector3.UnitY, new(-200, 13.565f, -367), false, true)!;
        Check(!flight.Jump && flight.Target == jump.Target && ReferenceEquals(flight.CrossingCorridor, jump.CrossingCorridor), "Airborne movement lost its reserved landing.");
        Console.WriteLine($"SIM FirstA bridge: edge-to-edge jump {DistanceXZ(edge.Target, jump.Target):F2} m, solver {solution.DescribeOutcome()}");
    }

    private static void SimulateFinalWave()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures, "race-final-wave.json")));
        var encounter = new BlundervilleEncounter(message => throw new InvalidOperationException(message));
        var source = 1ul;
        foreach (var cast in doc.RootElement.GetProperty("casts").EnumerateArray())
        {
            var started = Epoch.AddSeconds(cast.GetProperty("t").GetDouble());
            encounter.StartCast(source++, new(cast.GetProperty("action").GetUInt32(), Vec(cast.GetProperty("location")),
                cast.GetProperty("rotation").GetSingle(), started, started.AddSeconds(cast.GetProperty("seconds").GetDouble()),
                Vec(cast.GetProperty("origin"))), started);
        }
        var state = Start(0x4E31);
        var planner = new BlundervilleRacePlanner();
        var pos = new Vector3(6, 18.94f, 208.4f);
        var initial = pos;
        var now = Epoch;
        var boundary = BlundervilleRoutes.Boundary(state.Course);
        var allHazards = encounter.Hazards(now).ToArray();
        Check(allHazards.Length == 10 && allHazards.Count(h => h.Id.StartsWith("wave/")) == 8, "The real two-column cast did not publish its four predicted rows per column.");
        var advances = 0;
        // Include two seconds after the last row clears: the solver may retreat
        // along the narrow stairs first, then resume its race goal safely.
        for (var step = 0; step < 12; ++step)
        {
            state.SynchronizeStatuses([], now);
            var decision = planner.Update(state, now, pos, true, false, false, null);
            Check(decision.Target != null && !decision.Hold, "The wave simulation lost the final ascent goal.");
            var hazards = encounter.Hazards(now).ToArray();
            var solution = Solve(boundary, pos, decision.Target.Value, now, hazards);
            Check(solution.IsSuccess, $"Final wave step {step} has no safe route ({solution.Status}) at {pos}.");
            ValidatePath(solution, boundary, allHazards);
            var nextTime = now.AddSeconds(0.5);
            var next = Follow(solution.ExecutablePath, nextTime, pos);
            if (DistanceXZ(next, pos) > 0.1f) advances++;
            Check(boundary.Contains(next, 0.01f), "Movement integration stepped into a final-course gap.");
            Check(!allHazards.Any(h => nextTime >= h.From && nextTime <= h.Until && h.Geometry.Contains(next)), "Movement integration entered a live wave row.");
            if (BlundervilleRoutes.TrySurfaceHeight(state.Course, next, out var height)) next.Y = height;
            Console.WriteLine($"SIM Final wave t={(nextTime - Epoch).TotalSeconds:F1}: ({next.X:F2},{next.Z:F2}), hazards={hazards.Length}, {solution.DescribeOutcome()}");
            pos = next; now = nextTime;
        }
        Check(advances >= 2 && pos.Z < initial.Z - 3, "The simulation only held safely and failed to make race progress.");
    }

    private static SafeZoneSolution Solve(IGeometryShape boundary, Vector3 player, Vector3 target, DateTime now, IReadOnlyList<BlundervilleHazard> hazards, float? goalRadius = null)
    {
        var course = BlundervilleRoutes.FromPosition(player);
        var narrow = course == BlundervilleCourse.FirstB && player.Y >= 8.5f
                && (player.Z is < -118 and > -158 || target.Z is < -118 and > -158)
            || course == BlundervilleCourse.Ring && (player.Z < -137 || target.Z < -137)
            || course == BlundervilleCourse.Final && player.Y >= 35 && (player.Z < 129 || target.Z < 129);
        var tight = boundary is UnionBoundary union && (course == BlundervilleCourse.FirstA || union.Second is BlundervilleJumpCorridor);
        var radius = tight ? 8f : narrow ? 16f : 28f;
        var circle = goalRadius ?? (tight || narrow ? .2f : .8f);
        var middle = (player + target) / 2;
        var center = new Vector3(MathF.Round(middle.X / 10) * 10, player.Y, MathF.Round(middle.Z / 10) * 10);
        var localType = typeof(BlundervilleNavigation).GetNestedType("LocalCorridor", BindingFlags.NonPublic)!;
        var local = (IGeometryShape)Activator.CreateInstance(localType, [boundary, center, radius])!;
        var snapshot = new SafeZoneSnapshot(1, now, local, [],
            hazards.Select(h => new DangerAreaSnapshot(default, h.Geometry, [new(h.From, h.Until, h.Telegraph)])).ToArray(),
            [new(default, new CircleGeometry(target, circle), [GoalWindow.Persistent(now, 100, PositionPreference.Center)])]);
        return new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, 1), Movement,
            narrow ? Options with { ArenaSafetyMargin = .1f } : Options, now);
    }

    private static void ValidatePath(SafeZoneSolution solution, IGeometryShape boundary, IReadOnlyList<BlundervilleHazard> hazards)
    {
        var path = solution.ExecutablePath;
        if (path.Count == 0) return;
        var end = path[^1].PlannedDepartureTime;
        for (var time = path[0].PlannedArrivalTime; time <= end; time = time.AddSeconds(0.025))
        {
            var point = Follow(path, time, path[0].Position);
            Check(boundary.Contains(point, 0.01f), $"The executable path crosses void at {point}.");
            Check(!hazards.Any(h => time >= h.From && time <= h.Until && h.Geometry.Contains(point)), $"The executable path enters timed damage at {point}, {time:O}.");
        }
    }

    private static Vector3 Follow(IReadOnlyList<TimedWaypoint> path, DateTime time, Vector3 fallback)
    {
        if (path.Count == 0) return fallback;
        if (time <= path[0].PlannedDepartureTime) return path[0].Position;
        for (var i = 1; i < path.Count; ++i)
        {
            var previous = path[i - 1]; var next = path[i];
            if (time <= next.PlannedArrivalTime)
            {
                var duration = (next.PlannedArrivalTime - previous.PlannedDepartureTime).TotalSeconds;
                var t = duration > 0 ? (time - previous.PlannedDepartureTime).TotalSeconds / duration : 1;
                return Vector3.Lerp(previous.Position, next.Position, (float)Math.Clamp(t, 0, 1));
            }
            if (time <= next.PlannedDepartureTime) return next.Position;
        }
        return path[^1].Position;
    }

    private static BlundervilleRaceState Start(uint stage)
    {
        var state = new BlundervilleRaceState();
        state.Director(BlundervilleRaceState.DirectorInstance, 0x80000001, stage, 0, 0, 0, Epoch);
        return state;
    }
    private static uint Stage(BlundervilleCourse course) => course switch
    {
        BlundervilleCourse.FirstA => 0x4E2D, BlundervilleCourse.FirstB => 0x4E2E,
        BlundervilleCourse.Ring => 0x4E2F, BlundervilleCourse.Delivery => 0x4E30,
        BlundervilleCourse.Final => 0x4E31, _ => 0,
    };
    private static float Bearing(Vector3 a, Vector3 b) => MathF.Atan2(b.X - a.X, b.Z - a.Z);
    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static Vector3 Vec(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());
    private static void Check([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class UnionBoundary(IGeometryShape a, IGeometryShape b) : IGeometryShape
    {
        public IGeometryShape Second => b;
        public GeometryKind Kind => GeometryKind.WorldPolygon;
        public Vector3 Position => a.Position;
        public float Rotation => 0;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => a.Contains(point, epsilon) || b.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => a.IntersectsSegment(start, end, epsilon) || b.IntersectsSegment(start, end, epsilon);
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [.. a.GetContours(segments), .. b.GetContours(segments)];
        public GeometryBounds GetBounds()
        {
            var x = a.GetBounds(); var y = b.GetBounds();
            return new(MathF.Min(x.MinX, y.MinX), MathF.Min(x.MinZ, y.MinZ), MathF.Max(x.MaxX, y.MaxX), MathF.Max(x.MaxZ, y.MaxZ));
        }
    }

}
