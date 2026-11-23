using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class PharosSiriusRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Pharos Sirius Zu and Tyrant cutout arenas match runtime and world geometry", () =>
        {
            foreach (var (native, name) in new[] { (PharosSiriusArenas.Zu(1), "D172Zu"), (PharosSiriusArenas.Tyrant(1), "D173Tyrant") })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D17PharosSirius.D172Zu.D172Zu).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D17PharosSirius.{name}.{name}")!;
                var tuple = type.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var bounds = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                var host = HostDrawingGeometry.Convert(native);
                for (var x = -22.87f; x < 23; x += .31f)
                    for (var z = -22.73f; z < 23; z += .31f)
                    {
                        var expected = bounds.Contains(new(x - bounds.Center.X, z - bounds.Center.Z));
                        Check(native.Contains(new(x, 1, z)) == expected && host.Contains(new(x, 1, z)) == expected, "Arena cutouts changed.");
                    }
            }
        });
        foreach (var (boss, caster, action, radius) in new[]
        {
            (0x8FBu, 0x8FCu, 1499u, 6f), (0x8FBu, 0x1B2u, 1542u, 9f),
            (0x8FBu, 0x8FBu, 1502u, 20f), (0x8F4u, 0x8F4u, 1494u, 6f), (0x918u, 0x918u, 1674u, 6f),
        })
            run($"Pharos Sirius action {action} preserves actual radius activation and navigation margin", () =>
            {
                using var f = new Fight(boss, 160);
                var source = caster == boss ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, caster, f.Center); f.Scan(); }
                f.Cast(source, action, 4);
                Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: var r } && r == radius
                    && f.Snapshot.DangerAreas.Single().Geometry is CircleGeometry { Radius: var inflated } && inflated == radius + .5f,
                    "Display radius or navigation margin changed.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "NPC delay changed.");
            });
        foreach (var (boss, caster, action, length, width) in new[]
        {
            (0x8FBu, 0x8FBu, 1498u, 50f, 3f), (0x8EFu, 0x8EFu, 1484u, 50f, 12f), (0x8EFu, 0x8F0u, 1658u, 3f, 3f),
        })
            run($"Pharos Sirius action {action} preserves true rectangle dimensions and locked packet direction", () =>
            {
                using var f = new Fight(boss, 160);
                var source = caster == boss ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, caster, f.Center); f.Scan(); }
                f.Cast(source, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                Check(shape.Contains(new(length - .01f, 0, width / 2 - .01f))
                    && !shape.Contains(new(length + .01f, 0, 0))
                    && !shape.Contains(new(1, 0, width / 2 + .01f)) && !shape.Contains(-Vector3.UnitX), "Forward rectangle differs.");
            });
        run("Pharos Sirius crystalline shower voidzones grow nine twelve fifteen and clear removed stages", () =>
        {
            using var f = new Fight(0x8FB, 160);
            foreach (var (oid, radius) in new[] { (0x1E9058u, 9f), (0x1E9059u, 12f), (0x1E905Au, 15f) })
            {
                f.Actors[10] = new(10, oid, Vector3.UnitX * 5, Hostile: false); f.Scan();
                Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: var r } && r == radius
                    && f.Snapshot.DangerAreas.Count == 1, "Growing voidzone retained an earlier radius or duplicated navigation.");
            }
            f.Actors.Remove(10); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed crystal voidzone leaked.");
        });
        run("Pharos Sirius three-stack burst preserves packet timestamp follows players and avoids party for the marked perspective", () =>
        {
            using var f = new Fight(0x8FB, 160);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false, PartySlot: 0, StatusParams: new Dictionary<uint, ushort> { [374] = 2 });
            f.Actors[101] = new(101, 0, Vector3.UnitX * 10, Hostile: false, PartySlot: 1); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 0, "Two stacks triggered burst.");
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 374, Param = 3, Timestamp = f.Now.AddSeconds(-.3) }, f.Now); f.Render();
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 6 }
                && f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[101].Position)
                && Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 2.2) < .001,
                "Burst lost original timestamp or targeted player fled itself.");
            f.Actors[100] = f.Actors[100] with { StatusParams = new Dictionary<uint, ushort> { [374] = 3 } };
            f.Now = f.Start.AddSeconds(1); f.Scan();
            Check(Math.Abs((f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom - f.Start).TotalSeconds - 2.2) < .001,
                "Scan restarted burst timer.");
            f.PlayerId = 101; f.Actors[100] = f.Actors[100] with { Position = Vector3.UnitZ * 10 }; f.Scan();
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Actors[100].Position)
                && f.Draw.Previews.Single().Value.Geometry.Contains(f.Actors[100].Position), "Non-target output failed to follow marked player.");
            f.Engine.Effect(20, 1500, 1, f.Now); f.Render(); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Old three-stack world resurrected resolved burst.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 374, Timestamp = f.Now }, f.Now);
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 374, Param = 3, Timestamp = f.Now.AddSeconds(.1) }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1, "Next independent three-stack burst was blocked by earlier resolution.");
        });
        run("Pharos Sirius Zu eggs are forbidden attack targets while hatchlings retain priority and eight-yalm exclusion", () =>
        {
            using var f = new Fight(0x8F4, 160);
            f.Actors[10] = new(10, 0x8F7, Vector3.UnitX * 15); f.Actors[11] = new(11, 0x8F8, -Vector3.UnitX * 15);
            f.Actors[12] = new(12, 0x8F5, f.Center); f.Scan();
            Check(f.Engine.ForbiddenTargets.Order().SequenceEqual([10UL, 11UL]) && f.Engine.PriorityTarget(10, f.Center) == 12
                && f.Draw.Previews.Count == 2 && f.Draw.Previews.Values.All(p => p.Geometry is CircleGeometry { Radius: 8 }),
                "Egg ban also banned hatchlings or lost egg exclusion.");
            f.Actors.Remove(10); f.Actors[11] = f.Actors[11] with { Dead = true }; f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Draw.Previews.Count == 0, "Destroyed eggs retained ACR target ban.");
        });
        run("Pharos Sirius Siren cadenza preserves four-to-thirty donut and safe center", () =>
        {
            using var f = new Fight(0x8EF, 160); f.Cast(1, 1487, 3);
            var shape = f.Draw.Previews.Single().Value.Geometry;
            Check(!shape.Contains(Vector3.UnitZ * 3.99f) && shape.Contains(Vector3.UnitZ * 4.01f)
                && shape.Contains(Vector3.UnitZ * 29.99f) && !shape.Contains(Vector3.UnitZ * 30.01f), "Cadenza became a filled circle.");
        });
        foreach (var (boss, radius, angle) in new[] { (0x8F4u, 9f, 120f), (0x8EFu, 7f, 130f) })
            run($"Pharos Sirius boss {boss:X} auto cleave retains upstream estimated radius and angle", () =>
            {
                using var f = new Fight(boss, 160);
                f.Actors[100] = new(100, 0, Vector3.UnitZ * 5, PartySlot: 0, Hostile: false);
                f.Actors[1] = f.Actors[1] with { TargetId = 100 }; f.Scan();
                Check(f.Draw.Previews.Single().Value.Geometry is FanGeometry { Radius: var r, AngleRadians: var a }
                    && r == radius && Math.Abs(a - angle * MathF.PI / 180) < .001, "Cleave estimates were silently changed.");
            });
        run("Pharos Sirius Giga Slash landing uses ten-yalm radial knockback and immunity valid at activation", () =>
        {
            using var f = new Fight(0x8FB, 160);
            var source = new Vector3(42, 0, -56);
            var player = source + Vector3.UnitX * 15;
            f.Actors[1] = f.Actors[1] with { Position = source };
            f.Actors[100] = new(100, 0, player, Hostile: false); f.Scan();
            var cast = new DungeonCastObservation(1502, source, 0, f.Now, f.Now.AddSeconds(4.3));
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            Check(f.Draw.Previews["gigaslash/landing"].Geometry.Position == player + Vector3.UnitX * 10
                && !f.Draw.Previews["gigaslash/landing"].Safe, "Giga Slash knockback distance/landing changed.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(6) } }; f.Scan();
            Check(!f.Draw.Previews.ContainsKey("gigaslash/landing") && f.Snapshot.DangerAreas.Count == 1, "Knockback immunity hid the actual damaging circle.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(2) } }; f.Scan();
            Check(f.Draw.Previews.ContainsKey("gigaslash/landing"), "Immunity expiring before impact incorrectly cancelled knockback.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
