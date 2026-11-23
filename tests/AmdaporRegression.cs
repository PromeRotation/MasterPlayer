using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class AmdaporRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Amdapor Psycheflayer keeps the encounter across phase-one death and direct phase-two restoration", () =>
        {
            using var f = new Fight(0x5D1, 167);
            f.Actors[2] = new(2, 0x5D0, f.Center, InCombat: true);
            f.Actors[10] = new(10, 0x5D4, f.Center); f.Scan(); f.Cast(10, 1087, 7);
            var arena = f.Engine.Arena;
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.PrimaryId == 2 && ReferenceEquals(f.Engine.Arena, arena)
                && f.Draw.Previews.Count == 1, "Phase-one death reset live phase-two mechanics.");
            f.Engine.ResetFight(); f.Scan();
            Check(f.Engine.Active?.BossOid == 0x5D1 && f.Engine.PrimaryId == 2, "Mid-fight phase-two world could not restore encounter.");
        });
        foreach (var (boss, sourceOid, action, radius) in new[]
        {
            (0x5D1u, 0x5D1u, 1084u, 5f), (0x5D1u, 0x5D4u, 1087u, 30.5f),
            (0x5CCu, 0x5CCu, 584u, 11.5f), (0x5CCu, 0x5CCu, 1073u, 35.5f), (0x5CCu, 0x5CDu, 1077u, 6f),
        })
            run($"Amdapor action {action} retains actual radius locked direction and planning margin", () =>
            {
                using var f = new Fight(boss, 167);
                var source = sourceOid == boss ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, sourceOid, f.Center); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Actual action radius differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + Vector3.UnitX * (radius + .4f)), "Navigation margin missing.");
            });
        foreach (var (oid, action, length, halfWidth) in new[]
        {
            (0x5D2u, 1086u, 12.76f, 4f), (0x19Au, 1045u, 50f, 4f), (0x19Au, 1046u, 50f, 3.5f),
        })
            run($"Amdapor action {action} preserves forward rectangle dimensions", () =>
            {
                using var f = new Fight(action == 1086 ? 0x5D1u : 0x283u, 167);
                f.Actors[10] = new(10, oid, f.Center); f.Scan(); f.Cast(10, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews["cast/10"].Geometry;
                Check(geometry.Contains(new(length - .01f, 0, halfWidth - .01f))
                    && !geometry.Contains(new(length + .01f, 0, 0))
                    && !geometry.Contains(new(1, 0, halfWidth + .01f)) && !geometry.Contains(-Vector3.UnitX), "Rectangle dimensions differ.");
            });
        run("Amdapor Demon Wall forbidden zones follow the advancing wall and current pollen event state", () =>
        {
            using var f = new Fight(0x283, 167);
            var wall = new Vector3(200, 0, -145);
            f.Actors[1] = f.Actors[1] with { Position = wall };
            f.Actors[10] = new(10, 0x1E86B1, new(200, 0, -122), Hostile: false, EventState: 0); f.Scan();
            Check(f.Draw.Previews.Count == 2 && f.Draw.Previews["wall/behind"].Geometry.Contains(new(200, 0, -150))
                && f.Draw.Previews["wall/pollen"].Geometry.Contains(new(200, 0, -115)), "Wall/pollen forbidden geometry changed.");
            f.Events.Handle(new LogSystemEObjSetStateEvent { SourceId = 10, State = 1 }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1, "Inactive pollen retained zone.");
            f.Actors[10] = f.Actors[10] with { EventState = 1 };
            f.Actors[1] = f.Actors[1] with { Position = new(200, 0, -138) }; f.Scan();
            Check(f.Draw.Previews["wall/behind"].Geometry.Contains(new(200, 0, -140)), "Forbidden zone did not advance.");
        });
        run("Amdapor Demon Wall Repel publishes the two-yalm center strip and twenty-yalm personal landing", () =>
        {
            using var f = new Fight(0x283, 167);
            var wall = new Vector3(200, 0, -145);
            var player = new Vector3(205, 0, -140);
            f.Actors[1] = f.Actors[1] with { Position = wall };
            f.Actors[100] = new(100, 0, player, Hostile: false, PartySlot: 0); f.Scan();
            var cast = new DungeonCastObservation(1047, wall, 0, f.Now, f.Now.AddSeconds(3.3));
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            var goal = f.Snapshot.GoalAreas.Single().Geometry;
            Check(goal.Contains(new(200.99f, 0, -140)) && !goal.Contains(new(201.01f, 0, -140))
                && !goal.Contains(wall + Vector3.UnitZ), "Repel center strip lost its two-yalm offset or width.");
            var landing = f.Draw.Previews["wall/repel-landing"].Geometry.Position;
            Check(Math.Abs(Vector3.Distance(player, landing) - 20) < .001 && !f.Engine.Arena!.Contains(landing), "Repel distance differs.");
            f.Complete(1, 3); Check(f.Snapshot.GoalAreas.Count == 0 && !f.Draw.Previews.ContainsKey("wall/repel-landing"), "Repel completion leaked.");
        });
        run("Amdapor line of sight analytical union and pillar exclusions match upstream resolved custom polygon", () =>
        {
            var pillars = new[]
            {
                new DungeonActor(10, 0x1E86A7, new(10, 0, 0), HitboxRadius: 2),
                new DungeonActor(11, 0x1E86A8, new(0, 0, -10), HitboxRadius: 2),
            };
            var native = new DungeonLosGeometry(Vector3.Zero, pillars, true);
            var danger = new DungeonLosGeometry(Vector3.Zero, pillars, false);
            var upstream = new BossMod.AOEShapeCustom(
                [new BossMod.DonutSegmentHA(new(0, 0), 10.2f, 100, new(MathF.PI / 2), new(MathF.Asin(.2f))),
                    new BossMod.DonutSegmentHA(new(0, 0), 10.2f, 100, new(MathF.PI), new(MathF.Asin(.2f)))],
                [new BossMod.Circle(new(10, 0), 2.5f), new BossMod.Circle(new(0, -10), 2.5f)]);
            var reference = upstream.GetCombinedPolygon(default);
            var host = HostDrawingGeometry.Convert(native);
            for (var x = -24.73f; x < 25; x += .43f)
                for (var z = -24.87f; z < 25; z += .43f)
                {
                    var p = new Vector3(x, 0, z);
                    var expected = reference.Contains(new(x, z));
                    var actual = native.Contains(p);
                    Check(actual == host.Contains(p) && danger.Contains(p) != actual, "Display and planner disagree on line of sight.");
                    // 上游圆弧是离散多边形；离边界5厘米以内比较解析点会受弦差影响。
                    if (new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitZ }.All(d => native.Contains(p + d * .05f) == actual))
                        Check(actual == expected, "Shelter union or blocker exclusions changed away from the polygon edge.");
                }
            Check(danger.IntersectsSegment(new(9, 0, 0), new(15, 0, 0)), "Pillar-crossing segment was treated as safe.");
            Check(!danger.IntersectsSegment(new(15, 0, 0), new(24, 0, 0)), "Segment inside shelter became forbidden.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var contours = native.GetContours();
            Check(contours.Count > 0 && contours.Count < 2000 && watch.Elapsed.TotalSeconds < 1, "Native shelter contour generation is excessive.");
            foreach (var piece in contours)
            {
                var center = piece.Aggregate(Vector3.Zero, (sum, point) => sum + point) / piece.Count;
                Check(native.Contains(center, .02f), "Rendered shelter filled a blocker or an unsafe angular gap.");
            }
            Check(ReferenceEquals(contours, native.GetContours()), "Shelter tessellation was rebuilt each draw.");
        });
        run("Amdapor line of sight captures only intact pillars and clears on cast cancellation", () =>
        {
            using var f = new Fight(0x5CC, 167);
            f.Actors[10] = new(10, 0x1E86A7, new(10, 0, 0), Hostile: false, HitboxRadius: 2);
            f.Actors[11] = new(11, 0x1E86A8, new(-10, 0, 0), Hostile: false, HitboxRadius: 2, EventState: 1); f.Scan();
            f.Cast(1, 1074, 7);
            var safe = f.Draw.Previews["los/display/1"].Geometry;
            Check(safe.Contains(new(15, 0, 0)) && !safe.Contains(new(-15, 0, 0))
                && !safe.Contains(new(11, 0, 0)), "Destroyed pillar created shelter or hitbox was allowed.");
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(7.3), "LoS activation differs.");
            f.Engine.Finish(1, 1074, f.Now.AddSeconds(2)); f.Render();
            Check(!f.Draw.Previews.ContainsKey("los/display/1") && f.Snapshot.DangerAreas.Count == 0, "Cancelled LoS retained output.");
        });
        run("Amdapor actual solver routes behind an intact pillar before line of sight damage", () =>
        {
            using var f = new Fight(0x5CC, 167);
            var player = new Vector3(5, 0, 4);
            f.Actors[100] = new(100, 0, player, Hostile: false);
            f.Actors[10] = new(10, 0x1E86A7, new(10, 0, 0), Hostile: false, HitboxRadius: 2); f.Scan(); f.Cast(1, 1074, 4);
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count >= 2
                && f.Draw.Previews["los/display/1"].Geometry.Contains(path[^1].Position), "Solver did not reach actual shelter.");
        });
        run("Amdapor Plague Dance preserves six point one second bait clock and pillar-safe role-specific navigation", () =>
        {
            using var f = new Fight(0x5CC, 167);
            f.Actors[100] = new(100, 0, new(0, 0, 10), Hostile: false);
            f.Actors[101] = new(101, 0, new(0, 0, -10), Hostile: false);
            f.Actors[10] = new(10, 0x1E86A7, new(10, 0, 0), Hostile: false, HitboxRadius: 2); f.Scan();
            f.Events.Handle(new LogSystemTetherEvent { SourceId = 1, TargetId = 100, TetherId = 1, Timestamp = f.Now.ToLocalTime() }, f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[10].Position)
                && Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 6.1) < .001,
                "Bait target was directed into a future shelter pillar.");
            f.Actors[1] = f.Actors[1] with { Tethers = new Dictionary<uint, ulong> { [1] = 100 } };
            f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 6.1) < .001,
                "Tether snapshot restarted bait clock.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Non-target did not avoid bait.");
            f.Events.Handle(new LogSystemTetherCancelEvent { SourceId = 1, TargetId = 100, TetherId = 1, Timestamp = f.Now.ToLocalTime() }, f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 0 && !f.Draw.Previews.ContainsKey("plague/display"), "Cancelled tether retained bait.");
        });
        run("Amdapor Dark Nova voidzone lasts until removal and Dark Helot gets target priority", () =>
        {
            using var f = new Fight(0x5CC, 167);
            f.Actors[10] = new(10, 0x5CE, f.Center); f.Actors[11] = new(11, 0x5CD, Vector3.UnitX * 5); f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 11.5f }
                && f.Engine.PriorityTarget(1, f.Center) == 11, "Nova/helot output missing.");
            f.Actors.Remove(10); f.Scan(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed nova retained voidzone.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
