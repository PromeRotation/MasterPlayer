using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;
using Pelican = BossMod.RealmReborn.Dungeon.D07Brayflox.D071GreatYellowPelican.D071GreatYellowPelican;
using Drake = BossMod.RealmReborn.Dungeon.D07Brayflox.D072InfernoDrake.D072InfernoDrake;
using Hellbender = BossMod.RealmReborn.Dungeon.D07Brayflox.D073Hellbender.D120Hellbender;
using Aiatar = BossMod.RealmReborn.Dungeon.D07Brayflox.D074Aiatar.D074Aiatar;

internal static class BrayfloxRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Brayflox four exact arenas and world conversion preserve Aiatar entrance cut", () =>
        {
            foreach (var (geometry, original) in new (IGeometryShape, BossMod.ArenaBoundsCustom)[]
            {
                (BrayfloxArenas.Pelican(3), Pelican.arena), (BrayfloxArenas.Drake(3), Drake.arena),
                (BrayfloxArenas.Hellbender(3), Hellbender.arena), (BrayfloxArenas.Aiatar(3), Aiatar.arena),
            })
            {
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .7f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z), 0) == expected, $"Arena mismatch {x}, {z}.");
                        Check(host.Contains(new(x, 3, z), 0) == expected, "World conversion changed a boundary.");
                    }
            }
            var aiatar = BrayfloxArenas.Aiatar(3);
            Check(!aiatar.Contains(new(-45, 3, -236)) && aiatar.Contains(new(-44.5f, 3, -236)), "Aiatar's subtracted entrance cap was filled.");
        });
        foreach (var (boss, action, sourceOid, radius) in new[]
        {
            (0x1A7u, 506u, 0x1A7u, 9.2f), (0x1ABu, 982u, 0x1ABu, 8f),
            (0x1ABu, 448u, 0x1ABu, 8f), (0x38C5u, 28657u, 0x233Cu, 2f), (0x38C5u, 28658u, 0x233Cu, 10f),
        })
            run($"Brayflox AID {action} original dimensions, helper ownership and navigation margin", () =>
            {
                using var f = new Fight(boss, 1041);
                var source = sourceOid == boss ? 1UL : 10;
                if (source != 1) { f.Actors[source] = new(source, sourceOid, f.Center); f.Scan(); }
                f.Cast(source, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Original display radius changed.");
                Check(f.Snapshot.DangerAreas.Single().Geometry.Contains(f.Center + Vector3.UnitX * (radius + .4f)), "Navigation margin missing.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "NPC cast delay shifted.");
            });
        run("Brayflox Inferno Drake uses actual 8.6 cleave and preserves tank-party distinction", () =>
        {
            using var f = new Fight(0x3DE, 1041);
            f.Actors[1] = f.Actors[1] with { TargetId = 101 };
            f.Actors[101] = new(101, 0, f.Center + Vector3.UnitX * 3, PartySlot: 1, Hostile: false);
            f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry is FanGeometry { Radius: 8.6f }, "Copied 9.6 comment instead of 8.6 component.");
            Check(f.Snapshot.DangerAreas.Count == 1, "Missing automatic cleave navigation.");
        });
        run("Brayflox Aiatar breath is forward 30 by 8 and Burst promotes only four helpers", () =>
        {
            using var f = new Fight(0x38C5, 1041);
            f.Cast(1, 28660, 3, MathF.PI / 2);
            var breath = f.Draw.Previews.Single().Value.Geometry;
            Check(breath.Contains(f.Center + new Vector3(29.9f, 0, 3.9f))
                && !breath.Contains(f.Center + new Vector3(-.1f, 0, 0))
                && !breath.Contains(f.Center + new Vector3(15, 0, 4.1f)), "DragonBreath dimensions changed.");
            f.Engine.Finish(1);
            f.Actors[1] = f.Actors[1] with { Cast = null };
            for (var i = 10; i < 18; ++i)
            {
                f.Actors[(ulong)i] = new((ulong)i, 0x233C, f.Center);
                f.Scan();
                f.Cast((ulong)i, 28658, 9);
            }
            Check(f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count == 4 && f.Engine.Casts.Count == 8, "Burst cap differs from BMR.");
            f.Complete(10, 9);
            Check(f.Draw.Previews.Count == 4 && f.Engine.OutputCasts.Any(c => c.Value.Source == 14), "Next Burst failed to promote.");
        });
        run("Brayflox bound player marker and bubble priority follow gain, loss and stale snapshots", () =>
        {
            using var f = new Fight(0x1AB, 1041);
            f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false);
            f.Actors[10] = new(10, 0x428, f.Center + Vector3.UnitX);
            f.Scan();
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 100, StatusId = 280, Timestamp = f.Now }, f.Now);
            f.Render();
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 1.5f }
                && f.Draw.Previews.Single().Value.OutlineOnly && f.Snapshot.DangerAreas.Count == 0, "Bound player marker became damage.");
            Check(f.Engine.Hints(f.Center).Contains("击杀水泡，解救被束缚队友") && f.Engine.PriorityTarget(1, f.Center) == 10, "Bubble rescue hint/priority missing.");
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 100, StatusId = 280, Timestamp = f.Now }, f.Now);
            f.Render();
            Check(f.Draw.Previews.Count == 0, "Bind loss leaked marker.");
        });
        run("Brayflox Pelican and Drake add priorities remain separate from incidental enemies", () =>
        {
            using var f = new Fight(0x1A7, 1041);
            f.Actors[10] = new(10, 0x1AA, f.Center);
            f.Actors[11] = new(11, 0x1A9, f.Center + Vector3.UnitX);
            f.Scan();
            Check(f.Engine.PriorityTarget(10, f.Center) == 11, "VioletBack did not outrank SableBack.");
            f.Actors[11] = f.Actors[11] with { Dead = true };
            f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Center) == 10, "SableBack did not outrank boss.");
            using var drake = new Fight(0x3DE, 1041);
            drake.Actors[10] = new(10, 0x3DF, drake.Center);
            drake.Scan();
            Check(drake.Engine.PriorityTarget(1, drake.Center) == 10, "Templest Biast priority lost.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
