using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class SnowcloakRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (native, typeName, field) in new[]
        {
            (SnowcloakArenas.NorthernStarting(0), "D260NorthernBateleur", "arena1"),
            (SnowcloakArenas.Northern(0), "D260NorthernBateleur", "Arena2"),
            (SnowcloakArenas.WandilStarting(0), "D261Wandil", "DefaultArena"),
            (SnowcloakArenas.Wandil(0), "D261Wandil", "SmallArena"),
            (SnowcloakArenas.Yeti(0), "D262Yeti", "arena"), (SnowcloakArenas.Fenrir(0), "D263Fenrir", "arena"),
        })
            run($"Snowcloak {typeName} {field} exact union arena and world conversion", () =>
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D26Snowcloak.D261Wandil.D261Wandil).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D26Snowcloak.{typeName}.{typeName}")!;
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(native);
                for (var x = -80.13f; x < 80; x += .63f)
                    for (var z = -100.27f; z < 100; z += .63f)
                    {
                        var p = native.Position + new Vector3(x, 0, z);
                        var expected = bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, "Arena topology changed.");
                    }
            });
        run("Snowcloak trash activates on any in-bounds combatant and survives primary death", () =>
        {
            using var f = new Fight(0xD1E, 1062);
            Check(f.Engine.Active == null, "Out-of-bounds trash activated.");
            var p = new Vector3(-30, 0, -170);
            f.Actors[1] = f.Actors[1] with { Position = p, InCombat = false };
            f.Actors[10] = new(10, 0xD31, p, InCombat: true); f.Scan();
            Check(f.Engine.Active?.BossOid == 0xD1E && f.Engine.PrimaryId == 10, "Ice sprite did not activate trash encounter.");
            f.Actors[11] = new(11, 0xD1F, p, InCombat: true); f.Actors.Remove(10); f.Scan();
            Check(f.Engine.PrimaryId == 11 && f.Engine.Active != null, "Primary removal ended active trash encounter.");
        });
        run("Snowcloak wall and wind state keep absolute five-second activation without scan reset", () =>
        {
            using var f = new Fight(0xD1E, 1062);
            f.Actors[1] = f.Actors[1] with { Position = new(-30, 0, -170) };
            f.Actors[10] = new(10, 0x1E94F1, Vector3.Zero);
            f.Actors[11] = new(11, 0x1E96D4, Vector3.Zero); f.Scan();
            f.Engine.Animation(10, 0x00040008); f.Engine.Animation(11, 0x00040008); f.Render();
            Check(f.Engine.Arena!.Contains(new(-50, 0, -110)) && f.Draw.Previews.Count == 1, "Wall removal failed or wind missing.");
            var wind = f.Draw.Previews.Values.Single().Geometry;
            var origin = new Vector3(1.5f, 0, -140.75f);
            var dir = new Vector3(MathF.Sin(-135 * MathF.PI / 180), 0, MathF.Cos(-135 * MathF.PI / 180));
            Check(wind.Contains(origin + dir * 16.99f) && !wind.Contains(origin + dir * 17.01f), "Wind direction or length differs.");
            f.Now = f.Start.AddSeconds(1); f.Engine.Tick(f.Now); f.Engine.Animation(11, 0x00010002); f.Render();
            f.Now = f.Start.AddSeconds(2); f.Scan(); f.Engine.Animation(11, 0x00010002); f.Render();
            Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6), "Repeated animation extended wind timer.");
        });
        run("Snowcloak Tundra preserves polygon hole until environmental shrink and clears forecast", () =>
        {
            using var f = new Fight(0xD07, 1062);
            f.Cast(1, 3082, 3);
            var center = new Vector3(56.41631f, 0, -88.51856f);
            Check(f.Draw.Previews.Count == 20 && !f.Draw.Previews.Values.Any(p => p.Geometry.Contains(center))
                && f.Draw.Previews.Values.Any(p => p.Geometry.Contains(center + Vector3.UnitX * 15)), "Tundra ring filled its safe center.");
            Check(f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.3)), "Tundra lost +2s delay.");
            f.Actors[10] = new(10, 0x1E9661, center); f.Scan();
            f.Engine.Animation(10, 0x00040008); f.Render();
            Check(f.Draw.Previews.Count == 0 && !f.Engine.Arena!.Contains(center + Vector3.UnitX * 15), "Shrink retained old boundary or ring.");
        });
        run("Snowcloak Snow Drift moves repeatedly through real solver and clears only on effect or cancellation", () =>
        {
            using var f = new Fight(0xD07, 1062);
            var position = new Vector3(56, 0, -88);
            f.Actors[100] = new(100, 0, position, Hostile: false);
            f.Cast(1, 3080, 5);
            f.Now = f.Start.AddSeconds(6.5); f.Scan();
            Check(!f.Engine.RequireSnowMovement && f.Snapshot.DangerAreas.Count == 0, "Movement restriction began too early.");
            f.Now = f.Start.AddSeconds(6.6); f.Scan();
            Check(f.Engine.RequireSnowMovement, "Delayed movement requirement lost after natural cast finish.");
            for (var i = 0; i < 3; ++i)
            {
                var snapshot = f.Snapshot;
                var solution = new SafeZoneSolver().Solve(snapshot, new(position, 0, 6, false, false, 0, true, snapshot.Version),
                    new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
                var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
                Check(solution.Status == SolutionStatus.Success && path.Count >= 2
                    && Vector3.Distance(path[^1].Position, position) > 1.1f, "Move requirement produced no safe movement.");
                position = path[^1].Position;
                f.Actors[100] = f.Actors[100] with { Position = position }; f.Now = f.Now.AddSeconds(.1); f.Scan();
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(position), "Arriving at first point stopped continuous movement.");
            }
            f.Engine.Effect(20, 3079, 1, f.Now); f.Render();
            Check(!f.Engine.RequireSnowMovement && f.Snapshot.DangerAreas.Count == 0, "Snow Drift effect retained movement/cast block.");
            f.Now = f.Now.AddSeconds(1); f.Cast(1, 3080, 5); f.Engine.Finish(1, 3080); f.Render();
            Check(!f.Engine.RequireSnowMovement, "Cancelled Snow Drift retained cast block.");
        });
        run("Snowcloak Yeti shows circle then donut with independent helper and exact three-second prediction", () =>
        {
            using var f = new Fight(0x3977, 1062);
            f.Actors[10] = new(10, 0x233C, f.Center); f.Scan();
            f.Cast(1, 29586, 5); f.Cast(10, 29591, 8);
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 11 }, "Future donut duplicated first stage.");
            f.Complete(1, 5);
            Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry { InnerRadius: 10, OuterRadius: 40 }
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(8), "Second-stage activation changed.");
            f.Complete(10, 8); Check(f.Draw.Previews.Count == 0, "Helper finish did not complete sequence.");
        });
        run("Snowcloak Fenrir intact animation-state ice shelters then shatters after four-point-five seconds", () =>
        {
            using var f = new Fight(0x3979, 1062);
            f.Actors[10] = new(10, 0x397A, new(10, 0, 0), HitboxRadius: 2.5f);
            f.Actors[11] = new(11, 0x397A, new(-10, 0, 0), HitboxRadius: 2.5f, AnimationState1: 1); f.Scan();
            f.Cast(1, 29599, 8);
            var shelter = f.Draw.Previews["lunar/safe/1"].Geometry;
            Check(shelter.Contains(new(15, 0, 0)) && !shelter.Contains(new(-15, 0, 0))
                && shelter.Contains(new(11, 0, 0)) && !shelter.Contains(new(81, 0, 0)), "Fenrir blocker filter/range/hitbox differs.");
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(new(-15, 0, 0))
                && !f.Snapshot.DangerAreas.Single().Geometry.Contains(new(15, 0, 0)), "Lunar Cry lost shelter complement navigation.");
            f.Complete(1, 8);
            Check(!f.Draw.Previews.ContainsKey("lunar/safe/1") && f.Draw.Previews["frozenpillar/10"].Geometry is CircleGeometry { Radius: 8 }
                && !f.Draw.Previews.ContainsKey("frozenpillar/11")
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(12.5),
                "Natural Lunar Cry end dropped shatter prediction or included broken ice.");
            f.Engine.Effect(10, 29601, 1, f.Start.AddSeconds(12.5)); f.Render();
            Check(!f.Draw.Previews.ContainsKey("frozenpillar/10"), "Shatter effect leaked prediction.");
        });
        foreach (var (boss, caster, action, radius) in new[]
        {
            (0xD07u, 0xD2Eu, 3111u, 8f), (0x3977u, 0x233Cu, 29589u, 15f),
            (0x3977u, 0x233Cu, 29590u, 2f), (0x3979u, 0x397Au, 29600u, 4f), (0x3979u, 0x397Au, 29648u, 8f),
        })
            run($"Snowcloak AID {action} radius timing margin and preview isolation", () =>
            {
                using var f = new Fight(boss, 1062); f.Actors[10] = new(10, caster, f.Center); f.Scan(); f.Cast(10, action, 4);
                Check(f.Draw.Previews["cast/10"].Geometry is CircleGeometry { Radius: var r } && r == radius
                    && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3)
                    && f.Safe.NavigationOnly.Count == 1, "Circle timing or display isolation differs.");
            });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
