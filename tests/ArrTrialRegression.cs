using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class ArrTrialRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("ArrTrial exact Ifrit polygon Titan circle Garuda circle and isolated ownership", () =>
        {
            var source = BossMod.RealmReborn.Trial.T01IfritN.T01IfritN.IfritArena;
            var shape = ArrTrialCatalog.IfritArena(0);
            var world = HostDrawingGeometry.Convert(shape);
            for (var x = -20f; x < 20; x += .137f)
                for (var z = -20f; z < 20; z += .237f)
                {
                    var p = new Vector3(x, 0, z);
                    Check(shape.Contains(p) == world.Contains(p), "Ifrit world shape differs.");
                    var expected = source.Contains(new(x, z));
                    if (shape.Contains(p) == expected) continue;
                    Check(shape.GetContours().Min(c => Enumerable.Range(0, c.Count).Min(i =>
                        GeometryHelper.DistanceSquaredPointToSegment2D(new(x, z), new(c[i].X, c[i].Z),
                            new(c[(i + 1) % c.Count].X, c[(i + 1) % c.Count].Z)))) < .000001f, "Ifrit 36-gon differs from source.");
                }
            foreach (var (territory, oid, radius, id) in new[]
            {
                (1045u, 0xCFu, 19.5f, "ifrit"), (1046u, 0xF6u, 20f, "titan"), (1047u, 0xEFu, 21f, "garuda"),
            })
            {
                Check(ArrDungeonCatalog.Owns(oid, territory) && !ArrDungeonCatalog.Owns(oid, 0)
                    && ArrDungeonCatalog.IsScript($"masterplayer.trial.{id}-normal"), "Ownership or script routing differs.");
                var arena = ArrDungeonCatalog.Create(territory).Single().Arena(Vector3.Zero);
                Check(arena.Contains(Vector3.UnitZ * (radius - .01f)) && !arena.Contains(Vector3.UnitZ * (radius + .01f)), "Arena range differs.");
            }
        });
        foreach (var (territory, oid, action, radius) in new[]
        {
            (1045u, 0xCFu, 733u, 8f), (1045u, 0xCFu, 734u, 8f),
            (1046u, 0xF6u, 651u, 18f), (1046u, 0xF6u, 650u, 40f), (1046u, 0xF6u, 973u, 6f),
            (1047u, 0xEFu, 659u, 11.7f), (1047u, 0xEFu, 660u, 31.7f), (1047u, 0xEFu, 661u, 24.7f),
        })
            run($"ArrTrial {action} source independent locked geometry real finish cancellation margin and world output", () =>
            {
                using var f = new Fight(oid, territory);
                f.Actors[10] = new(10, 0xFFFF, Vector3.Zero); f.Scan();
                f.Cast(10, action, 4, MathF.PI / 2);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Wrong source OID filtering or duplicate output.");
                var shape = f.Draw.Previews.Single().Value.Geometry;
                var p = Vector3.UnitX * (radius - .01f);
                Check(shape.Contains(p) && !shape.Contains(Vector3.UnitX * (radius + .01f))
                    && HostDrawingGeometry.Convert(shape).Contains(p), "Shape extent or world conversion differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3)
                    && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerUntil == null, "NPC delay or observed lifetime differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (radius + .4f)), "Navigation margin missing.");
                f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitZ * 100, Rotation = 2 };
                f.Now = f.Start.AddSeconds(20); f.Scan();
                Check(f.Draw.Previews.Count == 1 && f.Engine.Casts[10].Location == Vector3.Zero, "Elapsed time or later motion replaced active cast.");
                f.Actors[10] = f.Actors[10] with { Dead = true };
                f.Scan(); Check(f.Draw.Previews.Count == 1, "Death flag prematurely ended cast.");
                f.Actors[10] = f.Actors[10] with { Cast = null };
                f.Scan(); Check(f.Draw.Previews.Count == 0, "Observed cast end on dead caster leaked.");
                f.Actors[10] = f.Actors[10] with { Dead = false };
                f.Cast(10, action, 4);
                f.Engine.Finish(10, action); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel retained output.");
            });
        foreach (var (territory, oid, radius) in new[] { (1045u, 0xCFu, 16f), (1046u, 0xF6u, 11.25f), (1047u, 0xEFu, 11.7f) })
            run($"ArrTrial {territory} continuous cleave follows target and tank protects party", () =>
            {
                using var f = new Fight(oid, territory);
                f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
                f.Actors[1] = f.Actors[1] with { TargetId = 100, Rotation = MathF.PI };
                f.Scan();
                Check(f.Draw.Previews.Single().Value.Geometry.Contains(Vector3.UnitZ * (radius - .01f))
                    && f.Draw.Previews.Single().Value.OutlineOnly, "Cleave does not follow actual target or lost outline.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * 50)
                    && !f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitZ * 50), "Tank did not protect party.");
                f.PlayerId = 101; f.Scan();
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitZ * 5)
                    && !f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * 5), "Non-target did not avoid cleave.");
                Check(f.Engine.Hints(Vector3.UnitZ * 5).Contains("避开顺劈")
                    && !f.Engine.Hints(Vector3.UnitX * 5).Contains("避开顺劈"), "Cleave warning missing or outside shape.");
                f.Actors[1] = f.Actors[1] with { InCombat = false };
                f.Scan(); Check(f.Engine.Active != null, "Transient combat flag ended trivial phase.");
            });
        run("ArrTrial Ifrit nail clock uses first targetable observation and original 35 seconds", () =>
        {
            using var f = new Fight(0xCF, 1045);
            f.Actors[10] = new(10, 0xD0, Vector3.Zero, Targetable: false); f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Any(), "Untargetable nail started countdown.");
            f.Now = f.Start.AddSeconds(3);
            f.Actors[10] = f.Actors[10] with { Targetable = true }; f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Single().Contains("35.0"), "Nail clock did not start at first observation.");
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 10 && f.Engine.ForbiddenTargets.Length == 0, "Nail priority or action restrictions differ.");
            f.Now = f.Now.AddSeconds(40); f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Single().Contains("0.0"), "Nail countdown did not clamp.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Any(), "Dead nail retained global countdown.");
        });
        run("ArrTrial Titan heart 62 seconds and gaol heart boss priorities", () =>
        {
            using var f = new Fight(0xF6, 1046);
            f.Actors[10] = new(10, 0x5E3, Vector3.Zero); f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Single().Contains("62.0") && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Heart clock or priority differs.");
            f.Actors[11] = new(11, 0x5A4, Vector3.Zero); f.Scan();
            Check(f.Engine.PriorityTarget(10, Vector3.Zero) == 11, "Gaol did not outrank heart.");
            f.Actors[11] = f.Actors[11] with { Dead = true };
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(!f.Engine.Hints(Vector3.Zero).Any() && f.Engine.PriorityTarget(10, Vector3.Zero) == 1, "Dead targets retained countdown or priority.");
        });
        run("ArrTrial Garuda friction is navigation only any boss cast releases it and AerialBlast effect activates eye", () =>
        {
            using var f = new Fight(0xEF, 1047);
            f.Actors[10] = new(10, 0xED, Vector3.UnitZ * 10, HitboxRadius: 2);
            f.Actors[11] = new(11, 0x622, Vector3.Zero);
            f.Actors[12] = new(12, 0xEE, Vector3.Zero); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.OutlineOnly,
                "Friction drew filled AOE or missing navigation.");
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 12, "Feather priority missing.");
            f.Cast(1, 662, 4);
            f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 1, "AerialBlast start activated eye or failed to release Friction.");
            f.Engine.Effect(1, 662, 1, f.Now);
            f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 2, "AerialBlast effect did not activate eye.");
            Check(f.Engine.Hints(Vector3.UnitX * 15).Contains("离开风眼危险环")
                && !f.Engine.Hints(Vector3.Zero).Contains("离开风眼危险环"), "Eye warning missing or inside safe circle.");
            Check(f.Draw.Previews.Values.Single(p => !p.OutlineOnly).Geometry is DonutGeometry d && d.InnerRadius == 12 && d.OuterRadius == 25, "Eye dimensions differ.");
            f.Actors[1] = f.Actors[1] with { Cast = null }; f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 2, "Friction did not resume between casts.");
            f.Actors.Remove(11); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 1 && f.Draw.Previews.Count == 1, "Removed eye helper retained donut.");
        });
        run("ArrTrial Garuda LOS snapshots actual hitboxes includes dead pillars excludes body and clears on source removal", () =>
        {
            using var f = new Fight(0xEF, 1047);
            var origin = new Vector3(0, 0, -15);
            f.Actors[10] = new(10, 0xED, Vector3.Zero, Dead: true, Targetable: false, HitboxRadius: 2);
            f.Actors[1] = f.Actors[1] with { Position = origin, Cast = new(667, origin, 0, f.Now, f.Now.AddSeconds(4.3)) };
            f.Scan();
            var safe = f.Draw.Previews.Values.Single(a => a.Safe).Geometry;
            Check(safe.Contains(new(0, 0, 5)) && !safe.Contains(Vector3.Zero) && !safe.Contains(new(5, 0, 5)),
                "LOS shadow, blocker hitbox or alive filtering differs.");
            Check(HostDrawingGeometry.Convert(safe).Contains(new(0, 0, 5)), "World LOS lost safe shadow.");
            Check(!f.Snapshot.DangerAreas.Single().Geometry.Contains(new(0, 0, 5))
                && f.Snapshot.DangerAreas.Single().Geometry.Contains(new(5, 0, 5)), "LOS navigation did not use shadow complement.");
            f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitX * 15, HitboxRadius = 10 };
            f.Scan(); Check(f.Draw.Previews.Values.Single(a => a.Safe).Geometry.Contains(new(0, 0, 5)), "LOS shadow followed later blocker motion.");
            f.Engine.RemoveActor(1); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed primary retained LOS.");
        });
        run("ArrTrial Garuda actual temporal solver reaches a safe pillar shadow", () =>
        {
            using var f = new Fight(0xEF, 1047);
            var origin = new Vector3(0, 0, -15);
            f.Actors[10] = new(10, 0xED, Vector3.Zero, HitboxRadius: 2);
            f.Actors[1] = f.Actors[1] with { Position = origin, Cast = new(667, origin, 0, f.Now, f.Now.AddSeconds(4.3)) };
            f.Scan();
            var snapshot = f.Snapshot;
            var player = new Vector3(5, 0, 5);
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback, "No safe LOS route.");
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(path.Count > 1 && path.All(p => f.Engine.Arena!.Contains(p.Position))
                && f.Draw.Previews.Values.Single(a => a.Safe).Geometry.Contains(path[^1].Position), "LOS route did not reach a pillar shadow.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
