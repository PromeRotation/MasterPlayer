using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class AeryRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (geometry, name, field) in new[] { (AeryArenas.Rangda(0), "D031Rangda", "arena"),
            (AeryArenas.GyascutusStarting(0), "D032Gyascutus", "StartingBounds"), (AeryArenas.Nidhogg(0), "D033Nidhogg", "arena") })
            run($"Aery {name} exact starting arena and world contours match runtime", () =>
            {
                var type = typeof(BossMod.Heavensward.Dungeon.D03Aery.D031Rangda.D031Rangda).Assembly
                    .GetType($"BossMod.Heavensward.Dungeon.D03Aery.{name}.{name}")!;
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                for (var x = -34.13f; x < 34; x += .43f)
                    for (var z = -34.27f; z < 34; z += .43f)
                    {
                        var p = geometry.Position + new Vector3(x, 0, z);
                        var expected = bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z));
                        Check(geometry.Contains(p) == expected && host.Contains(p) == expected, "Arena topology changed.");
                    }
            });
        foreach (var (boss, oid, action, inside, outside) in new[]
        {
            (0xEA6u, 0xEA6u, 3893u, 2.99f, 3.01f),
            (0x3970u, 0x233Cu, 30184u, 9.99f, 10.01f),
            (0x3970u, 0x3970u, 30191u, 11.99f, 12.01f),
            (0x39CAu, 0x233Cu, 30197u, 67.99f, 68.01f),
            (0x39CAu, 0x233Cu, 30195u, 29.99f, 30.01f),
            (0x39CAu, 0x233Cu, 30200u, 5.99f, 6.01f),
            (0x39CAu, 0x39CAu, 30198u, 79.99f, 80.01f),
            (0x39CAu, 0x39CCu, 30209u, 29.99f, 30.01f),
        })
            run($"Aery AID {action} exact display navigation margin and packet timing", () =>
            {
                using var f = new Fight(boss, 1065);
                var source = boss == oid ? 1UL : 10UL;
                if (source != 1) { f.Actors[source] = new(source, oid, Vector3.Zero); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Values.Single().Geometry;
                Check(geometry.Contains(Vector3.UnitX * inside) && HostDrawingGeometry.Convert(geometry).Contains(Vector3.UnitX * inside)
                    && !geometry.Contains(Vector3.UnitX * outside), "Geometry length or axis differs.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(Vector3.UnitX * (outside + .2f))
                    && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "Margin or timing lost.");
                f.Engine.Finish(source, action); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Cancel leaked.");
            });
        foreach (var (boss, action, inner, outer) in new[] { (0xEA6u, 3889u, 8f, 60f), (0x3970u, 30190u, 11f, 20f) })
            run($"Aery AID {action} retains both donut edges in native and world geometry", () =>
            {
                using var f = new Fight(boss, 1065); f.Cast(1, action, 7);
                var ring = (DonutGeometry)f.Draw.Previews.Values.Single().Geometry;
                Check(ring.InnerRadius == inner && ring.OuterRadius == outer && !ring.Contains(Vector3.Zero)
                    && HostDrawingGeometry.Convert(ring).Contains(Vector3.UnitX * (inner + .1f)), "Donut safe center lost.");
            });
        run("Aery Rangda Electrocution keeps all living candidates until three distinct packets and ignores AOE rows", () =>
        {
            using var f = new Fight(0xEA6, 1065);
            for (ulong id = 100; id < 104; ++id)
                f.Actors[id] = new(id, 0, new((id - 99) * 5, 0, (id - 100) * 3), PartySlot: (int)id - 100, Hostile: false);
            f.Scan(); f.Cast(1, 3890, 3); f.Complete(1, 3);
            Check(f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count > 0
                && f.Snapshot.DangerAreas.All(a => Math.Abs((a.DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 4.2) < .001),
                "Visual finish ended baits or changed delay.");
            f.Engine.Effect(1, 3891, 1, f.Now, 100);
            f.Engine.Effect(1, 3891, 1, f.Now, 101); f.Render();
            Check(f.Draw.Previews.Count == 4, "Duplicate AOE row advanced hit count.");
            f.Engine.Effect(1, 3891, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 4, "Baits cleared at second hit.");
            f.Engine.Effect(1, 3891, 3, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Third hit retained baits.");
        });
        run("Aery Rangda tether reaches a statue, switches perspective and resists stale snapshots after transfer", () =>
        {
            using var f = new Fight(0xEA6, 1065);
            var center = AeryArenas.Rangda(0).Position;
            f.Actors[1] = f.Actors[1] with { Position = center, Tethers = new Dictionary<uint, ulong> { [6] = 100 } };
            f.Actors[100] = new(100, 0, center, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, center, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0xEA8, center + Vector3.UnitX * 12); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 1 && f.Engine.Hints(center).Any(h => h.Contains("传给石像")), "Tether target lacks goal/hint.");
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(center, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions(), f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && path.Count > 1
                && Vector3.Distance(path[^1].Position, f.Actors[10].Position) <= 4, "No real route to statue.");
            f.PlayerId = 101; f.Scan(); Check(f.Snapshot.GoalAreas.Count == 0, "Wrong player was moved to statue.");
            f.PlayerId = 100;
            f.Events.Handle(new LogSystemTetherCancelEvent { SourceId = 1, TargetId = 100, TetherId = 6, Timestamp = f.Now }, f.Now); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0, "Stale snapshot resurrected transferred tether.");
            f.Actors[1] = f.Actors[1] with { Tethers = new Dictionary<uint, ulong> { [6] = 10 } }; f.Now = f.Now.AddSeconds(1); f.Scan();
            Check(f.Snapshot.GoalAreas.Count == 0, "Statue-bound tether still moved player.");
        });
        run("Aery Gyascutus shrink forecast survives natural cast and packed map event replaces boundary permanently", () =>
        {
            using var f = new Fight(0x3970, 1065); f.Cast(1, 30181, 4);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.1),
                "Fumes shrink prediction delay differs.");
            f.Complete(1, 4); Check(f.Draw.Previews.Count == 1, "Natural cast finish cleared shrink.");
            f.Events.Handle(new LogSystemMapEffectEvent { Position = 0, Param1 = 1, Param2 = 2, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.Arena is CircleGeometry { Radius: 19.9f } && f.Draw.Previews.Count == 0, "Map shrink boundary or cleanup differs.");
            f.Engine.ResetFight(); f.Scan();
            Check(f.Engine.Arena is CircleGeometry { Radius: 19.9f }, "Reengagement restored already removed floor.");
        });
        run("Aery Nidhogg fetters forbid boss through PR target restrictions and restore on status removal and perspective", () =>
        {
            using var f = new Fight(0x39CA, 1065);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false);
            f.Actors[10] = new(10, 0x39CB, Vector3.UnitX * 10, HitboxRadius: 1); f.Scan();
            var cast = new DungeonCastObservation(30204, f.Actors[10].Position, 0, f.Now, f.Now.AddSeconds(15.3), 101);
            f.Actors[10] = f.Actors[10] with { Cast = cast }; f.Engine.StartCast(10, cast, f.Now);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 101, StatusId = 3324, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, Vector3.Zero) == 10
                && f.Snapshot.GoalAreas.Single().Geometry is CircleGeometry { Radius: 4 }, "Fetter restrictions or attack-range goal missing.");
            f.PlayerId = 101; f.Scan(); Check(f.Engine.ForbiddenTargets.Length == 0, "Chained player inherited other-player boss ban.");
            f.PlayerId = 100;
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 101, StatusId = 3324, Timestamp = f.Now }, f.Now); f.Render();
            Check(f.Engine.ForbiddenTargets.Length == 0, "Fetter status removal retained boss ban.");
            f.Engine.RemoveActor(10); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 0, "Destroyed fetter retained movement goal.");
        });
        run("Aery delayed Scarlet Price survives cast completion and clears only on matching effect", () =>
        {
            using var f = new Fight(0x39CA, 1065);
            f.Actors[100] = new(100, 0, Vector3.Zero, PartySlot: 0, Hostile: false); f.Scan();
            var cast = new DungeonCastObservation(30205, Vector3.Zero, 0, f.Now, f.Now.AddSeconds(5.3), 100);
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Complete(1, 5);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Delayable tankbuster vanished with cast.");
            f.Engine.Effect(1, 30205, 1, f.Now);
            Check(!f.Engine.PredictedDamage.Any(), "Tankbuster effect retained prediction.");
        });
        run("Aery Body Slam center goal landing drawing and immunity use actual hit time", () =>
        {
            using var f = new Fight(0x3970, 1065);
            var center = new Vector3(11.978f, 0, 67.979f);
            f.Actors[100] = new(100, 0, center + Vector3.UnitX * 5, PartySlot: 0, Hostile: false); f.Scan();
            var cast = new DungeonCastObservation(31234, center, 0, f.Now, f.Now.AddSeconds(4.3));
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(f.Snapshot.GoalAreas.Single().Geometry is CircleGeometry { Radius: 9 }
                && f.Draw.Previews["aery/body-slam/landing"].Geometry.Position == center + Vector3.UnitX * 15,
                "Knockback goal or landing differs.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(6) } }; f.Scan();
            Check(f.Draw.Previews.Count == 1, "Immune player retained landing/path.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(3) } }; f.Scan();
            Check(f.Draw.Previews.Count == 3, "Expiring immunity hid hit-time knockback.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
