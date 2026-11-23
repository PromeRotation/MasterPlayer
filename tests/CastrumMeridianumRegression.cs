using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using Fight = HalataliRegression.Fight;

internal static class CastrumMeridianumRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Castrum Meridianum all three arena polygons match resolved runtime and world rendering", () =>
        {
            foreach (var (native, typeName) in new[]
            {
                (CastrumMeridianumArenas.BlackEft(2), "D131BlackEft"),
                (CastrumMeridianumArenas.Vanguard(2), "D132MagitekVanguardF1"),
                (CastrumMeridianumArenas.Livia(2), "D133Livia"),
            })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D13CastrumMeridianum.D131BlackEft.D131BlackEft).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D13CastrumMeridianum.{typeName}.{typeName}")!;
                var reference = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(native);
                var bounds = native.GetBounds();
                for (var x = bounds.MinX + .127f; x < bounds.MaxX; x += .4f)
                    for (var z = bounds.MinZ + .211f; z < bounds.MaxZ; z += .4f)
                    {
                        var expected = reference.Contains(new(x - reference.Center.X, z - reference.Center.Z));
                        Check(native.Contains(new(x, 2, z)) == expected && host.Contains(new(x, 2, z)) == expected, "Arena union/subtraction changed.");
                    }
            }
        });
        foreach (var (boss, action, radius) in new[]
        {
            (0x38CAu, 28775u, 6f), (0x38CDu, 28779u, 20f), (0x38CDu, 28781u, 10f),
            (0x38CDu, 28784u, 5f), (0x38CDu, 29146u, 11f),
            (0x38CEu, 29163u, 10f), (0x38CEu, 28787u, 8f),
            (0x38CEu, 28794u, 8f), (0x38CEu, 28790u, 8f), (0x38CEu, 29164u, 8f), (0x38CEu, 29356u, 13f),
        })
            run($"Castrum Meridianum AID {action} exact radius and independent navigation margin", () =>
            {
                using var f = new Fight(boss, 1043);
                var source = f.Engine.Active!.Casts.Single(c => c.Action == action).CasterOid == boss ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, 0x233C, f.Center); f.Scan(); }
                f.Cast(source, action, 5, MathF.PI / 2);
                var preview = f.Draw.Previews.Single().Value.Geometry;
                Check(preview.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !preview.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Display radius changed.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + Vector3.UnitX * (radius + .4f)), "Navigation margin missing.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.3), "NPC activation time changed.");
            });
        run("Castrum Meridianum Black Eft ray keeps fifty by four actual dimensions", () =>
        {
            using var f = new Fight(0x38CA, 1043);
            f.Cast(1, 28773, 5, MathF.PI / 2);
            var geometry = f.Draw.Previews.Single().Value.Geometry;
            Check(geometry.Contains(f.Center + new Vector3(49.99f, 0, 1.99f))
                && !geometry.Contains(f.Center + Vector3.UnitX * 50.01f)
                && !geometry.Contains(f.Center - Vector3.UnitX), "Actual ray was replaced by action-sheet length sixty.");
        });
        run("Castrum Meridianum Vanguard inner and outer circles coexist with independent finish times", () =>
        {
            using var f = new Fight(0x38CD, 1043);
            f.Actors[10] = new(10, 0x233C, f.Center); f.Actors[11] = new(11, 0x233C, f.Center); f.Scan();
            f.Cast(10, 28781, 5); f.Cast(11, 28782, 7);
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2, "Nested casts collapsed.");
            var ring = f.Draw.Previews.Values.Single(p => p.Geometry is DonutGeometry).Geometry;
            Check(!ring.Contains(f.Center + Vector3.UnitZ * 11.99f) && ring.Contains(f.Center + Vector3.UnitZ * 12.01f)
                && ring.Contains(f.Center + Vector3.UnitZ * 29.99f) && !ring.Contains(f.Center + Vector3.UnitZ * 30.01f), "Donut twelve-thirty changed.");
            f.Complete(10, 5);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "First circle completion cleared outer circle.");
            f.Complete(11, 7); Check(f.Draw.Previews.Count == 0, "Final donut leaked.");
        });
        foreach (var (first, second, firstRadius, secondRadius) in new[] { (29163u, 28787u, 10f, 8f), (28790u, 29356u, 8f, 13f) })
            run($"Castrum Meridianum Livia sequence {first} then {second} promotes hidden next stage after completion", () =>
            {
                using var f = new Fight(0x38CE, 1043);
                f.Actors[10] = new(10, 0x233C, f.Center); f.Actors[11] = new(11, 0x233C, f.Center); f.Scan();
                f.Cast(10, first, 4); f.Cast(11, second, 7);
                Check(f.Engine.Casts.Count == 2 && f.Draw.Previews.Count == 1
                    && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: var r } && r == firstRadius, "Later stage shown too early.");
                f.Complete(10, 4);
                Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: var r2 }
                    && r2 == secondRadius && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(7.3),
                    "Next stage lost its original clock or failed to appear.");
                f.Complete(11, 7); Check(f.Draw.Previews.Count == 0, "Completed sequence leaked.");
            });
        run("Castrum Meridianum Livia Infinite Reach displays six circles and promotes remaining helpers", () =>
        {
            using var f = new Fight(0x38CE, 1043);
            foreach (var id in Enumerable.Range(10, 8)) f.Actors[(ulong)id] = new((ulong)id, 0x233C, f.Center);
            f.Scan();
            foreach (var id in Enumerable.Range(10, 8)) f.Cast((ulong)id, 28794, id < 16 ? 5 : 8);
            Check(f.Draw.Previews.Count == 6, "Infinite Reach cap changed.");
            foreach (var id in Enumerable.Range(10, 6)) f.Actors[(ulong)id] = f.Actors[(ulong)id] with { Cast = null };
            f.Now = f.Start.AddSeconds(5); f.Scan();
            Check(f.Draw.Previews.Count == 2, "Pending circle helpers were discarded.");
        });
        run("Castrum Meridianum Angry Salamander cross preserves rotation in radar world and planner", () =>
        {
            using var f = new Fight(0x38CE, 1043);
            f.Actors[10] = new(10, 0x233C, f.Center); f.Scan(); f.Cast(10, 28797, 2.5, MathF.PI / 4);
            var shape = f.Draw.Previews.Single().Value.Geometry;
            var host = HostDrawingGeometry.Convert(shape);
            var forward = new Vector3(1, 0, 1) / MathF.Sqrt(2);
            Check(shape.Contains(f.Center + forward * 19.99f) && host.Contains(f.Center + forward * 19.99f)
                && !shape.Contains(f.Center + Vector3.UnitX * 10), "Cross rotation or gap changed.");
            Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + forward * 10 + new Vector3(1, 0, -1) / MathF.Sqrt(2) * 2.4f),
                "Cross planner margin missing.");
        });
        run("Castrum Meridianum Incendiary Support survives cast end until delayed raidwide effect and clears on cancellation", () =>
        {
            using var f = new Fight(0x38CA, 1043);
            f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false); f.Scan();
            f.Cast(1, 29268, 3); f.Complete(1, 3);
            Check(f.Engine.PredictedDamage.Single() is { DamageType: "Raidwide", TargetId: 100, Activation: var activation }
                && activation == f.Start.AddSeconds(4.3), "Delayed raidwide clock changed.");
            Check(f.Snapshot.DangerAreas.Count == 0, "Unavoidable raidwide became spatial danger.");
            f.Engine.Effect(10, 29269, 1, f.Start.AddSeconds(4.3));
            Check(!f.Engine.PredictedDamage.Any(), "Raidwide effect retained prediction.");
            f.Now = f.Start.AddSeconds(10); f.Cast(1, 29268, 3);
            f.Engine.Finish(1, 29268, f.Now.AddSeconds(1));
            Check(!f.Engine.PredictedDamage.Any(), "Cancelled raidwide retained prediction.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
