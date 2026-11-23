using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Source = BossMod.Heavensward.Dungeon.D11Antitower;

internal static class AntitowerRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (shape, type) in new (IGeometryShape, Type)[]
        {
            (AntitowerArenas.ZuroRoggo(0), typeof(Source.D111ZuroRoggo.D111ZuroRoggo)),
            (AntitowerArenas.Ziggy(0), typeof(Source.D112Ziggy.D112Ziggy)),
            (AntitowerArenas.Calcabrina(0), typeof(Source.D113Calcabrina.D113Calcabrina)),
        })
            run($"Antitower {type.Name} precise arena and world projection match source", () =>
            {
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(shape);
                var bounds = shape.GetBounds();
                for (var x = bounds.MinX + .13f; x < bounds.MaxX; x += .57f)
                    for (var z = bounds.MinZ + .27f; z < bounds.MaxZ; z += .57f)
                    {
                        var p = new Vector3(x, 0, z);
                        var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                        Check(shape.Contains(p) == expected && host.Contains(p) == expected, "Arena differs.");
                    }
            });
        run("Antitower ownership is remade territory 1111 only and includes doll aliases", () =>
        {
            foreach (var oid in new uint[] { 0x14FC, 0x3D82, 0x1503, 0x1504, 0x1502 })
                Check(ArrDungeonCatalog.Owns(oid, 1111) && !ArrDungeonCatalog.Owns(oid, 561), "Wrong territory or alias ownership.");
            Check(!ArrDungeonCatalog.Owns(0x14FE, 1111), "Helper must not become a primary module.");
        });
        foreach (var (boss, caster, action, range) in new[]
        {
            (0x14FCu, 0x233Cu, 5538u, 6f), (0x14FCu, 0x233Cu, 5979u, 6f), (0x14FCu, 0x233Cu, 5977u, 6f),
            (0x3D82u, 0x3D83u, 31838u, 12f), (0x3D82u, 0x3D83u, 31839u, 11f), (0x3D82u, 0x3D82u, 31832u, 40f),
            (0x1503u, 0x1504u, 5552u, 10f), (0x1503u, 0x15E3u, 5552u, 10f), (0x1503u, 0x15E1u, 5552u, 10f),
            (0x1503u, 0x1503u, 5551u, 19.9f), (0x1503u, 0x15E2u, 5551u, 19.9f), (0x1503u, 0x15E0u, 5551u, 19.9f),
        })
            run($"Antitower cast {action} caster {caster:X} preserves extent time margin and cancellation", () =>
            {
                using var f = new Fight(boss, 1111);
                var source = boss == caster ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, caster, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(Vector3.UnitX * (range - .01f)) && !geometry.Contains(Vector3.UnitX * (range + .01f)), "Wrong shape extent.");
                Check(HostDrawingGeometry.Convert(geometry).Contains(Vector3.UnitX * (range - .01f)), "World projection differs.");
                if (action == 5552) Check(!geometry.Contains(Vector3.UnitX * 4.9f), "Donut inner radius missing.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Cast clock differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (range + .4f)), "Margin absent.");
                f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel retained cast.");
            });
        run("Antitower croak preserves first origin twelve distinct effects and repeat cycle", () =>
        {
            using var f = new Fight(0x14FC, 1111);
            f.Actors[10] = new(10, 0x233C, new(.123f, 0, .456f)); f.Scan();
            for (var cycle = 0; cycle < 2; ++cycle)
            {
                f.Now = f.Start.AddSeconds(cycle * 40); f.Engine.Tick(f.Now);
                for (var hit = 1; hit <= 12; ++hit)
                {
                    f.Engine.Effect(10, 5540, (uint)hit, f.Now.AddSeconds(hit), sourceRotation: hit == 1 ? MathF.PI / 2 : 0);
                    f.Engine.Effect(10, 5540, (uint)hit, f.Now.AddSeconds(hit), target: 101);
                    f.Render();
                    Check(f.Draw.Previews.Count == (hit == 12 ? 0 : 1), "Wrong croak count or duplicate advanced sequence.");
                    if (hit == 1)
                    {
                        var geometry = f.Draw.Previews.Single().Value.Geometry;
                        Check(geometry.Contains(new(13, 0, .456f)) && !geometry.Contains(new(.123f, 0, 13)), "Croak did not lock first heading.");
                    }
                }
                Check(f.Snapshot.DangerAreas.Count == 0, "Twelfth croak did not clear navigation.");
            }
        });
        run("Antitower chirp birth uses original clock and harmony clears all without snapshot resurrection", () =>
        {
            using var f = new Fight(0x14FC, 1111);
            f.Engine.ObjectCreated(10, f.Start);
            f.Now = f.Start.AddSeconds(.5);
            f.Actors[10] = new(10, 0x14FE, Vector3.UnitX * 2);
            f.Actors[11] = new(11, 0x14FE, Vector3.UnitX * 5); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 2 && f.Snapshot.DangerAreas.Min(a => a.DangerWindows.Single().DangerFrom) == f.Start.AddSeconds(8.7), "Chirp birth drift.");
            f.Engine.Effect(10, 5543, 1, f.Now); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Harmony did not clear all or snapshot resurrected.");
            f.Actors[12] = new(12, 0x14FE, Vector3.Zero); f.Scan();
            Check(f.Draw.Previews.Count == 1, "New chirp lost after old harmony.");
            f.Engine.RemoveActor(12); f.Actors.Remove(12); f.Render();
            Check(f.Draw.Previews.Count == 0, "Removed chirp leaked.");
            f.Actors[12] = new(12, 0x14FE, Vector3.UnitX * 7); f.Scan();
            Check(f.Draw.Previews.Count == 1, "Reused actor ID did not produce a new chirp.");
        });
        run("Antitower frog and concussion statuses show cleansing and add priority", () =>
        {
            using var f = new Fight(0x14FC, 1111);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false, Statuses: [439, 3513]);
            f.Actors[10] = new(10, 0x14FD, Vector3.UnitX); f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("蛙变")) && f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("可驱散")), "Status hints missing.");
            Check(f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Choir add priority missing.");
        });
        run("Antitower Ziggy tether gives target meteor goals others charge avoidance and resolves without resurrection", () =>
        {
            using var f = new Fight(0x3D82, 1111);
            Vector3 center = new(185.8f, 0, 137.5f);
            f.Actors[1] = f.Actors[1] with { Position = center, Tethers = new Dictionary<uint, ulong> { [2] = 100 } };
            f.Actors[100] = new(100, 0, center + Vector3.UnitZ * 16, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, center + Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x3D83, center + Vector3.UnitZ * 10); f.Scan();
            var goal = f.Snapshot.GoalAreas.Single().Geometry;
            Check(goal.Contains(center + Vector3.UnitZ * 15) && !goal.Contains(center + Vector3.UnitZ * 10)
                && !goal.Contains(center + Vector3.UnitX * 15), "Meteor shadow geometry differs.");
            Check(HostDrawingGeometry.Convert(goal).Contains(center + Vector3.UnitZ * 15), "World meteor shadow differs.");
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1, "Target bait output missing.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Snapshot.DangerAreas.Single().Geometry.Contains(center + Vector3.UnitZ * 5), "Other viewer did not avoid charge.");
            f.PlayerId = 100; f.Scan();
            f.Engine.Effect(1, 31840, 1, f.Now, 100); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Resolved tether resurrected from snapshot.");
        });
        run("Antitower Ziggy untether removes charge but keeps LOS target until actual effect", () =>
        {
            using var f = new Fight(0x3D82, 1111);
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 16, Hostile: false);
            f.Actors[10] = new(10, 0x3D83, Vector3.UnitZ * 10); f.Scan();
            f.Engine.Tether(100, 2, true, f.Start, 1);
            f.Engine.Tether(100, 2, false, f.Start.AddSeconds(.1), 1); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Untether prematurely cleared LOS target.");
            f.Engine.Effect(1, 31840, 1, f.Start.AddSeconds(.2), 100); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "Effect did not clear LOS.");
        });
        run("Antitower meteor shadow produces actual reachable path and dead meteors remove goals", () =>
        {
            using var f = new Fight(0x3D82, 1111);
            Vector3 center = new(185.8f, 0, 137.5f);
            var player = center - Vector3.UnitX * 5;
            f.Actors[1] = f.Actors[1] with { Position = center };
            f.Actors[100] = new(100, 0, player, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x3D83, center + Vector3.UnitZ * 10); f.Scan();
            f.Engine.Tether(100, 2, true, f.Now, 1); f.Render();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 1
                && snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position) && f.Engine.Arena!.Contains(path[^1].Position),
                $"Meteor shelter unreachable: {solution.Status}, fallback={solution.IsLeastDangerFallback}, points={path.Count}.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0, "Dead meteor retained shelter.");
        });
        run("Antitower delayed Jab survives natural finish until actual damage and cancels cleanly", () =>
        {
            using var f = new Fight(0x3D82, 1111);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
            f.Cast(1, 31837, 5);
            f.Engine.StartCast(1, f.Actors[1].Cast! with { TargetId = 100 }, f.Now);
            f.Complete(1, 5);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Jab prediction ended before effect.");
            f.Engine.Effect(1, 31837, 1, f.Now, 100);
            Check(!f.Engine.PredictedDamage.Any(), "Jab effect leaked prediction.");
            f.Now = f.Start.AddSeconds(10); f.Cast(1, 31837, 5);
            f.Engine.StartCast(1, f.Actors[1].Cast! with { TargetId = 100 }, f.Now);
            f.Engine.Finish(1);
            Check(!f.Engine.PredictedDamage.Any(), "Cancelled Jab retained prediction.");
        });
        run("Antitower doll group survives primary death hidden large doll and ends with all NPC dolls", () =>
        {
            using var f = new Fight(0x1503, 1111);
            f.Actors[10] = new(10, 0x1502, Vector3.Zero, Targetable: false);
            f.Actors[11] = new(11, 0x15E0, Vector3.Zero);
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.Active?.BossOid == 0x1503 && f.Engine.PrimaryId == 10, "Small doll death lost large hidden phase.");
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Scan();
            Check(f.Engine.Active == null, "Player dolls incorrectly kept NPC phase alive.");
        });
        run("Antitower parry prediction moves without ban active sides forbid matching target and loss clears", () =>
        {
            using var f = new Fight(0x1503, 1111);
            f.Actors[10] = new(10, 0x1502, Vector3.Zero);
            f.Actors[100] = new(100, 0, -Vector3.UnitZ * 5, TargetId: 10, PartySlot: 0, Hostile: false); f.Scan();
            f.Cast(10, 5557, 3);
            Check(f.Draw.Previews.Count == 3 && f.Snapshot.DangerAreas.Count == 3 && f.Engine.ForbiddenTargets.Length == 0, "Imminent parry lost distinction.");
            f.Complete(10, 3);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 10, StatusId = 680, Param = 14, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([10UL]), "Side shield did not ban protected target.");
            f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitZ * 5 }; f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Snapshot.DangerAreas.Count == 0, "Front attack incorrectly forbidden.");
            f.Actors[100] = f.Actors[100] with { Position = -Vector3.UnitZ * 5 }; f.Scan();
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 10, StatusId = 680, Timestamp = f.Now.AddSeconds(.1) }, f.Now.AddSeconds(.1)); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Engine.ForbiddenTargets.Length == 0 && f.Snapshot.DangerAreas.Count == 0, "Parry status loss leaked.");
        });
        run("Antitower cancelled Brace removes imminent parry and snapshot active param overrides prediction", () =>
        {
            using var f = new Fight(0x1503, 1111);
            f.Actors[10] = new(10, 0x1502, Vector3.Zero); f.Scan();
            f.Cast(10, 5557, 3); f.Engine.Finish(10); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled Brace retained parry.");
            f.Actors[10] = f.Actors[10] with { Cast = null, Statuses = [680], StatusParams = new Dictionary<uint, ushort> { [680] = 1 } };
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, TargetId: 10, Hostile: false); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Engine.ForbiddenTargets.SequenceEqual([10UL]), "Snapshot parry parameter missing.");
            f.Now = f.Start.AddSeconds(1);
            f.Cast(10, 5557, 3);
            f.Actors[100] = f.Actors[100] with { Position = -Vector3.UnitZ * 5 }; f.Scan();
            f.Engine.StatusChanged(10, 680, false, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 3 && f.Engine.ForbiddenTargets.Length == 0,
                "Old active shield loss incorrectly removed the next imminent sides.");
        });
        run("Antitower glance icon locks large doll source time gaze cone viewer and death cleanup", () =>
        {
            using var f = new Fight(0x1503, 1111);
            f.Actors[10] = new(10, 0x1502, Vector3.Zero, InCombat: true);
            f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, -Vector3.UnitZ * 5, PartySlot: 1, Hostile: false); f.Scan();
            f.Now = f.Start.AddSeconds(.4); f.Engine.Icon(100, 73, f.Start); f.Render();
            Check(f.Engine.Gazes.Single().Source == 10 && f.Engine.Gazes.Single().FinishAt == f.Start.AddSeconds(3.1), "Gaze used wrong doll or event clock.");
            Check(f.Engine.GazeFacing(Vector3.UnitZ * 5, MathF.PI) == null, "Gaze forced too early.");
            f.Now = f.Start.AddSeconds(2.5); f.Engine.Tick(f.Now);
            var heading = f.Engine.GazeFacing(Vector3.UnitZ * 5, MathF.PI);
            Check(heading != null && MathF.Abs(MathF.IEEERemainder(heading.Value - MathF.PI, 2 * MathF.PI)) > MathF.PI / 4, "Facing did not turn away from large doll.");
            f.PlayerId = 101; f.Scan();
            Check(!f.Engine.Gazes.Any() && f.Engine.GazeFacing(-Vector3.UnitZ * 5, 0) == null, "Outside cone received gaze.");
            f.Engine.Effect(10, 5559, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Glance effect leaked.");
            f.Engine.Icon(100, 73, f.Now);
            f.Actors[100] = f.Actors[100] with { Dead = true }; f.Scan();
            Check(f.Draw.Previews.Count == 0, "Dead icon target leaked bait.");
        });
        run("Antitower fetters and player dolls preserve rescue hints and priority", () =>
        {
            using var f = new Fight(0x1503, 1111);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false, Statuses: [1055]);
            f.Actors[10] = new(10, 0x15E3, Vector3.UnitX); f.Scan();
            Check(f.Engine.Hints(Vector3.Zero).Any(h => h.Contains("解救")) && f.Engine.PriorityTarget(1, Vector3.Zero) == 10, "Doll rescue output missing.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.ArenaBoundary == null, "Stop leaked Antitower scene.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
