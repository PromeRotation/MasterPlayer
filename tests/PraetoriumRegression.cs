using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class PraetoriumRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Praetorium exact Colossus and both Nero arena boundaries match runtime and world geometry", () =>
        {
            foreach (var (geometry, module, field) in new[]
            {
                (PraetoriumArenas.Colossus(1), "D141Colossus", "arena"),
                (PraetoriumArenas.NeroStarting(1), "D142Nero", "startingBounds"),
                (PraetoriumArenas.Nero(1), "D142Nero", "DefaultBounds"),
            })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D14Praetorium.D141Colossus.D141Colossus).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D14Praetorium.{module}.{module}")!;
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                var rect = geometry.GetBounds();
                for (var x = rect.MinX + .117f; x < rect.MaxX; x += .4f)
                    for (var z = rect.MinZ + .213f; z < rect.MaxZ; z += .4f)
                    {
                        var expected = bounds.Contains(new(x - bounds.Center.X, z - bounds.Center.Z));
                        Check(geometry.Contains(new(x, 1, z)) == expected && host.Contains(new(x, 1, z)) == expected, "Arena boundary changed.");
                    }
            }
        });
        foreach (var (boss, action, radius) in new[]
        {
            (0x3872u, 28469u, 6f), (0x3872u, 28470u, 6f), (0x3872u, 28473u, 25f),
            (0x3873u, 28482u, 7f), (0x3873u, 28478u, 45f), (0x3873u, 28481u, 7f),
            (0x3875u, 29052u, 8f),
        })
            run($"Praetorium AID {action} shape radius locked rotation and original activation", () =>
            {
                using var f = new Fight(boss, 1044);
                var source = f.Engine.Active!.Casts.Single(c => c.Action == action).CasterOid == boss ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, 0x233C, f.Center); f.Scan(); }
                if (boss == 0x3873) { f.Engine.MapEffect(0, 0x00020001); f.Render(); }
                f.Cast(source, action, 5, MathF.PI / 2);
                var preview = f.Draw.Previews.Single().Value.Geometry;
                Check(preview.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !preview.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Actual shape radius changed.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.3), "Cast activation changed.");
            });
        run("Praetorium Nero pre-fight map effect survives encounter activation without recreating the old ring", () =>
        {
            var engine = new DungeonEncounter(1044);
            engine.MapEffect(0, 0x00020001);
            engine.Synchronize([new(1, 0x3873, new(-164, 0, 0), InCombat: true)], new(-164, 0, 0), DateTime.UtcNow);
            Check(engine.Arena!.Contains(new(-144.1f, 0, 0)) && !engine.Arena.Contains(new(-143.9f, 0, 0))
                && !engine.AmbientAoes().Any(a => a.Id == "nero/ring"), "Pre-pull map state was lost.");
        });
        run("Praetorium Nero ring activates after three point six seconds and map event shrinks the real boundary", () =>
        {
            using var f = new Fight(0x3873, 1044);
            var ring = f.Snapshot.DangerAreas.Single();
            Check(ring.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.6), "Ring began immediately or timer drifted.");
            Check(f.Engine.Arena!.Contains(new(-140, 0, 0)), "Initial arena already shrank.");
            f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.6), "Scan restarted ring clock.");
            f.Events.Handle(new LogSystemMapEffectEvent { Position = 0, Param1 = 1, Param2 = 2 }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0
                && !f.Engine.Arena!.Contains(new(-140, 0, 0)), "Map effect retained old ring/boundary.");
        });
        foreach (var (boss, action, caster) in new[] { (0x3872u, 28472u, 0x233Cu), (0x3875u, 28492u, 0x233Cu) })
            run($"Praetorium spread {action} follows targets and only avoids other people for the current spread target", () =>
            {
                using var f = new Fight(boss, 1044);
                f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, f.Center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
                f.Actors[10] = new(10, caster, f.Center); f.Scan();
                TargetCast(f, 10, action, 100);
                Check(f.Draw.Previews.Count == 1 && !f.Draw.Previews.Single().Value.Safe
                    && f.Snapshot.DangerAreas.Count == 1 && f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[101].Position)
                    && !f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Spread target tried to flee its own circle.");
                f.PlayerId = 101; f.Scan();
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position), "Non-target failed to avoid spread target.");
                f.Actors[100] = f.Actors[100] with { Position = f.Center + Vector3.UnitZ * 12 }; f.Scan();
                Check(f.Draw.Previews.Single().Value.Geometry.Contains(f.Actors[100].Position)
                    && !f.Draw.Previews.Single().Value.Geometry.Contains(f.Center), "Spread stayed at cast-start location.");
                f.Engine.Finish(10, action, f.Now.AddSeconds(1)); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled spread retained output.");
            });
        foreach (var (boss, action) in new[] { (0x3873u, 28477u), (0x3875u, 28493u) })
            run($"Praetorium stack {action} publishes a moving safe goal and cleans it on perspective change death and completion", () =>
            {
                using var f = new Fight(boss, 1044);
                if (boss == 0x3873) f.Engine.MapEffect(0, 0x00020001);
                f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false);
                f.Actors[101] = new(101, 0, f.Center + Vector3.UnitX * 8, PartySlot: 1, Hostile: false); f.Scan();
                TargetCast(f, 1, action, 101);
                Check(f.Snapshot.GoalAreas.Count == 1 && f.Snapshot.DangerAreas.Count == 0
                    && f.Draw.Previews.Single().Value is { Safe: true, OutlineOnly: true }, "Stack became a danger circle.");
                f.Actors[101] = f.Actors[101] with { Position = f.Center + Vector3.UnitZ * 8 }; f.Scan();
                Check(f.Snapshot.GoalAreas.Single().Geometry.Contains(f.Actors[101].Position), "Stack goal stopped following its target.");
                f.PlayerId = 101; f.Scan();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 1, "Stack target chased itself.");
                f.PlayerId = 100; f.Actors[101] = f.Actors[101] with { Dead = true }; f.Scan();
                Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Dead stack target retained output.");
            });
        run("Praetorium Nero knockback publishes center movement goal actual route and personal landing in both views", () =>
        {
            using var f = new Fight(0x3873, 1044);
            f.Engine.MapEffect(0, 0x00020001);
            var boss = new Vector3(-164, 0, 0);
            var player = new Vector3(-150, 0, 0);
            f.Actors[1] = f.Actors[1] with { Position = boss };
            f.Actors[100] = new(100, 0, player, PartySlot: 0, Hostile: false); f.Scan();
            var cast = new DungeonCastObservation(28476, boss, 0, f.Now, f.Now.AddSeconds(5.3));
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Draw.Previews.Count == 3, "Knockback goal/landing/trajectory missing.");
            var landing = f.Draw.Previews["nero/knockback-landing"];
            Check(!landing.Safe && landing.Geometry.Contains(new(-138, 0, 0))
                && HostDrawingGeometry.Convert(landing.Geometry).Contains(new(-138, 0, 0)), "Unsafe knockback landing changed.");
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count >= 2
                && snapshot.GoalAreas.Single().Geometry.Contains(path[^1].Position), "Planner failed to move into knockback-safe center.");
            f.Complete(1, 5); Check(f.Snapshot.GoalAreas.Count == 0 && f.Draw.Previews.Count == 0, "Knockback completion leaked goals.");
        });
        run("Praetorium Gaius Terminus Est begins six seconds after spawn and clears all rays on first hit without stale resurrection", () =>
        {
            using var f = new Fight(0x3875, 1044);
            f.Actors[10] = new(10, 0x3878, f.Center, Rotation: MathF.PI / 2);
            f.Actors[11] = new(11, 0x3878, f.Center + Vector3.UnitZ * 8); f.Scan();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.All(d => d.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6)),
                "Spawn prediction waited for cast-start or used NPC delay.");
            Check(f.Draw.Previews["terminus/10"].Geometry.Contains(f.Center + Vector3.UnitX * 39)
                && !f.Draw.Previews["terminus/10"].Geometry.Contains(f.Center - Vector3.UnitX), "Spawn rotation was lost.");
            f.Now = f.Start.AddSeconds(1);
            f.Actors[10] = f.Actors[10] with { Position = f.Center + Vector3.UnitZ * 2, Rotation = 0 }; f.Scan();
            Check(f.Snapshot.DangerAreas.All(d => d.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6))
                && f.Draw.Previews["terminus/10"].Geometry.Contains(f.Center + Vector3.UnitX * 39), "Snapshot movement reset locked ray/timer.");
            f.Engine.Effect(10, 28487, 1, f.Start.AddSeconds(6)); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "First hit failed to clear the whole set.");
            f.Scan(); Check(f.Draw.Previews.Count == 0, "Stale ray actor recreated a resolved set.");
            f.Actors[12] = new(12, 0x3878, f.Center); f.Scan(); Check(f.Draw.Previews.Count == 1, "Next wave did not get a new spawn prediction.");
            f.Actors.Remove(12); f.Scan(); Check(f.Draw.Previews.Count == 0, "Removed ray leaked.");
        });
        run("Praetorium Gaius enrage is ninety-two point four seconds and prioritizes targetable phantoms until boss returns", () =>
        {
            using var f = new Fight(0x3875, 1044);
            f.Actors[10] = new(10, 0x3877, f.Center + Vector3.UnitX * 5);
            f.Actors[1] = f.Actors[1] with { Targetable = false }; f.Scan();
            Check(Math.Abs(f.Engine.GaiusEnrageSeconds!.Value - 92.4) < .001
                && f.Engine.PriorityTarget(1, f.Center) == 10, "Enrage start or phantom priority differs.");
            f.Now = f.Start.AddSeconds(10); f.Scan(); Check(Math.Abs(f.Engine.GaiusEnrageSeconds!.Value - 82.4) < .001, "Scan restarted enrage.");
            f.Actors[1] = f.Actors[1] with { Targetable = true }; f.Scan(); Check(f.Engine.GaiusEnrageSeconds == null, "Boss returning did not clear enrage.");
        });
        run("Praetorium phantom Heirsbane uses its actual action and party target", () =>
        {
            using var f = new Fight(0x3875, 1044);
            f.Actors[10] = new(10, 0x3877, f.Center); f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false); f.Scan();
            TargetCast(f, 10, 28498, 100);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Phantom actual Heirsbane action omitted.");
        });
    }

    private static void TargetCast(Fight f, ulong source, uint action, ulong target)
    {
        var cast = new DungeonCastObservation(action, f.Center, 0, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast };
        f.Engine.StartCast(source, cast, f.Now); f.Render();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
