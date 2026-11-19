using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using MasterPlayer.Dodge;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Replay;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;

internal static class Pt30TemporalRegression
{
    public static void Run()
    {
        var now = new DateTime(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);
        var ordinary = new DangerWindow(now.AddSeconds(2), now.AddSeconds(2.4), now);
        Check(ordinary.Buffered(.5f, .3f).DangerFrom == now.AddSeconds(1.5), "ordinary casts keep their preparation buffer");
        var explicitHit = ordinary with { HasExplicitTiming = true };
        Check(explicitHit.Buffered(.5f, .3f) == explicitHit, "calibrated hits must not get a second timing reserve");
        var e = new Pt30Encounter();
        var center = new Vector3(-300, 0, -300);
        var boss = new Pt30Encounter.Actor(1, Pt30Encounter.BOSS_OID, center, 0, false, true);
        e.Synchronize([boss], now);
        e.Cast(10, 39642, center, 0, now, now.AddSeconds(2));
        e.Cast(11, 39643, center, 0, now, now.AddSeconds(2.6));
        var rings = e.Hazards(now, 100, center).OrderBy(h => h.From).ToArray();
        Check(rings[0].Window(now).Buffered(.5f, .3f).DangerUntil < rings[1].From, "sequential halos retain the gap between effects");
        Check(rings[0].Shape.Resolve(center, 0, null).Contains(center + new Vector3(12, 0, 0)), "timing fix must retain the actual ring geometry");
        e.Reset();
        e.Synchronize([boss], now);
        var stationary = new Pt30Encounter.Actor(50, Pt30Encounter.BALL_OID, center + new Vector3(0, 0, 22), 0, false, true);
        e.Synchronize([boss, stationary], now);
        Check(e.Hazards(now, 100, center).Single().Window(now).DangerUntil == null, "stationary ball stays dangerous until it moves or deactivates");
        foreach (var velocity in new[] { -.3f, .16f, .3f })
        {
            e.Reset();
            e.Synchronize([boss, stationary], now.AddSeconds(-.1));
            var ball = stationary with { Position = Orbit(velocity * .1f) };
            e.Synchronize([boss, ball], now);
            var hazards = e.Hazards(now, 100, center);
            Check(hazards.Count == Pt30Encounter.BALL_PREDICTION_SLICES && hazards.Count(h => h.Window(now).IsActiveAt(now)) == 1, "future ball positions must not all be active now");
            Check(hazards.Max(h => h.Until) > now.AddSeconds(new SolveOptions().PlanningHorizon), "ball predictions cover the entire planning horizon without a false disappearance");
            Check(hazards.All(h => h.Shape is Pt30BallSweepTemplate { Radius: < 4.3f }), "movement must not enlarge the ball sideways by its travel distance");
            Check(hazards[0].BallPreviewPosition == ball.Position && hazards.Skip(1).All(h => h.BallPreviewPosition == null), "preview stays at the real ball position only");
            for (var step = 0; step <= 1000; ++step)
            {
                var t = step * .01f;
                var position = Orbit(velocity * (.1f + t));
                var active = hazards.Where(h => h.Window(now).IsActiveAt(now.AddSeconds(t))).ToArray();
                for (var angle = 0; angle < 16; ++angle)
                {
                    var edge = position + new Vector3(MathF.Sin(angle * MathF.PI / 8) * 4, 0, MathF.Cos(angle * MathF.PI / 8) * 4);
                    Check(active.Any(h => h.Shape.Resolve(h.Position, h.Rotation, null).Contains(edge)), "swept envelope covers the entire moving 4m ball at its actual time");
                }
            }
        }
        var capsule = new Pt30BallSweepGeometry(Vector3.Zero, new(10, 0, 0), 4);
        Check(capsule.Contains(new(-4, 100, 0)) && capsule.Contains(new(5, 0, 4)) && !capsule.Contains(new(5, 0, 4.1f)), "capsule has 4m side width and round end caps, independent of height");
        Check(capsule.IntersectsSegment(new(5, 0, -10), new(5, 0, 10)) && !capsule.IntersectsSegment(new(-10, 0, 5), new(20, 0, 5)), "capsule segment crossing and parallel clearance");
        Vector3 Orbit(float angle) => center + new Vector3(MathF.Sin(angle) * 22, 0, MathF.Cos(angle) * 22);
    }

    public static void InspectRecording(string path, double[]? points = null, string? fixtureOutput = null)
    {
        var fixtures = new List<object>();
        var playback = new ReplayPlayback(ReplayRecordingReader.Load(path));
        var encounter = new Pt30Encounter();
        var nextScan = DateTime.MinValue;
        void Scan(float _)
        {
            var now = playback.World.CurrentTime;
            if (now < nextScan) return;
            nextScan = now.AddSeconds(.1);
            var actors = playback.World.Actors.Values;
            foreach (var actor in actors)
            {
                if (actor.EventAnimation is { } animation) encounter.Timeline(actor.Id, (ushort)animation);
                if (actor.Cast is { ActionId: 44918, Rotation: { } rotation } cast)
                    encounter.Cast(actor.Id, cast.ActionId, cast.Location, rotation, cast.StartedAt, cast.FinishAt);
            }
            encounter.Synchronize(actors.Where(a => a.Oid is Pt30Encounter.BOSS_OID or Pt30Encounter.BALL_OID)
                .Select(a => new Pt30Encounter.Actor(a.Id, a.Oid, a.Position, a.Rotation, a.Dead, a.InCombat)), now);
        }
        void Dispatch(LogSystemEvent ev)
        {
            var now = playback.World.CurrentTime;
            switch (ev)
            {
                case LogSystemCastStartEvent cast when Pt30Encounter.Actions.Contains(cast.ActionId) && CastRotation.TryGet(cast, out var rotation):
                    encounter.Cast(cast.SourceId, cast.ActionId, cast.EffectPosition, rotation, now, now.AddSeconds(cast.CastTime + .3));
                    break;
                case LogSystemTargetIconEvent icon when icon.MarkerId == 197:
                    encounter.Icon(icon.TargetId is 0 or 0xE0000000 ? icon.SourceId : icon.TargetId, icon.MarkerId, now);
                    break;
                case LogSystemPlayActionTimelineEvent timeline:
                    encounter.Timeline(timeline.SourceId, (ushort)timeline.TimelineId);
                    break;
                case LogSystemActionEffectEvent effect when Pt30Encounter.Actions.Contains(effect.ActionId):
                    encounter.Effect(effect.SourceId, effect.GlobalSequence, effect.ActionId, effect.EffectPosition, now);
                    break;
                case LogSystemCancelCastEvent cancel: encounter.Cancel(cancel.SourceId); break;
                case LogSystemInterruptCastEvent interrupt: encounter.Cancel(interrupt.SourceId); break;
            }
        }

        foreach (var t in points ?? [100.1, 102, 104, 105, 205, 206.2, 211, 212.4, 213.5, 214.5])
        {
            playback.AdvanceTo(t, Dispatch, Scan);
            var world = playback.World;
            var player = world.LocalPlayer!;
            var now = world.CurrentTime;
            var hazards = encounter.Hazards(now, world.LocalPlayerId, player.Position);
            if (fixtureOutput != null)
            {
                fixtures.Add(new
                {
                    t, player = player.Position, rotation = player.Rotation,
                    hazards = hazards.Select(h => new
                    {
                        shape = h.Shape.GetType().Name, parameters = JsonSerializer.SerializeToElement(h.Shape, h.Shape.GetType(), new JsonSerializerOptions { IncludeFields = true }),
                        position = h.Position, rotation = h.Rotation, from = (h.From - now).TotalSeconds,
                        until = h.Until == DateTime.MaxValue ? (double?)null : (h.Until - now).TotalSeconds,
                        h.HasMotionEnvelope, h.HasExplicitTiming,
                    }),
                    goalRotation = encounter.ReachGoal?.Rotation,
                });
                continue;
            }
            using var manager = new SafeZoneManager(new SafeZoneSetting { EnableSafeZone = true });
            manager.SetArenaBoundary(Pt30Arena.Boundary(player.Position.Y));
            manager.AddObstacle(Pt30Arena.Hole(player.Position.Y), new StaticTransform(Vector3.Zero, 0));
            foreach (var h in hazards)
                manager.AddDangerArea(h.Shape, new StaticTransform(h.Position, h.Rotation), [h.Window(now)]);
            if (encounter.ReachGoal is { } goal)
                manager.AddGoalArea(goal.Shape, new StaticTransform(goal.Position, goal.Rotation), [GoalWindow.Persistent(now, 5, PositionPreference.Nearest)]);
            var snapshot = manager.BuildSnapshot(now);
            foreach (var (timed, grid, margin) in (points == null
                ? new[] { (false, .5f, Pt30Encounter.NAVIGATION_GRID_MARGIN), (true, .5f, Pt30Encounter.NAVIGATION_GRID_MARGIN) }
                : new[] { (true, .5f, .5f), (true, .5f, Pt30Encounter.NAVIGATION_GRID_MARGIN) }))
            {
                var options = new SolveOptions { GoalDirectedTemporalPlanning = timed, GenerateDebugData = true, GridResolution = grid, HardSafetyMargin = margin, ArenaSafetyMargin = margin };
                var timer = Stopwatch.StartNew();
                var solution = new SafeZoneSolver().Solve(snapshot, new(player.Position, player.Rotation, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), options, now);
                if (points == null && t is 105 or 206.2 or 212.4)
                    Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && solution.ExecutablePath.Count >= 2,
                        $"recorded blocked passage must have a real escape at {t}");
                if (points == null && t == 102)
                    Check(solution.ExecutablePath.Where(p => p.IsWaitPoint).All(p => p.PlannedDepartureTime < now.AddSeconds(2)),
                        "recorded Reach preparation must not wait until its departure window is lost");
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    t, timed, grid, margin, state = encounter.Status, ms = timer.Elapsed.TotalMilliseconds,
                    budgetExceeded = solution.DebugData?.SearchBudgetExceeded, expansions = solution.DebugData?.SearchExpansions,
                    status = solution.Status.ToString(), solution.UsesTemporalPlanning, solution.IsPartialEscape, solution.IsLeastDangerFallback,
                    target = solution.TargetPosition?.ToString(),
                    continuationEnd = solution.ContinuationPath.Select(p => p.Position.ToString()).LastOrDefault(),
                    route = solution.ExecutablePath.Select(p => new { position = p.Position.ToString(), at = (p.PlannedArrivalTime - now).TotalSeconds, depart = (p.PlannedDepartureTime - now).TotalSeconds, p.IsWaitPoint }),
                }));
            }
        }
        if (fixtureOutput != null)
        {
            File.WriteAllText(fixtureOutput, JsonSerializer.Serialize(new
            {
                source = Path.GetFileName(path),
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
                snapshots = fixtures,
            }, new JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
            return;
        }
        Console.WriteLine(points == null ? "PASS recorded PT30 temporal regressions: Reach wait and three formerly blocked snapshots; the late 214.5s probe is diagnostic only." : "Recorded snapshot diagnostics complete; partial paths and failures are reported above.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
