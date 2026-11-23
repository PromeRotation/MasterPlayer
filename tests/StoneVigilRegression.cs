using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;
using Chudo = BossMod.RealmReborn.Dungeon.D10StoneVigil.D101ChudoYudo.D101ChudoYudo;
using Koshchei = BossMod.RealmReborn.Dungeon.D10StoneVigil.D102Koshchei.D102Koshchei;
using Isgebind = BossMod.RealmReborn.Dungeon.D10StoneVigil.D103Isgebind.D103Isgebind;

internal static class StoneVigilRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Stone Vigil three resolved arenas preserve wall cuts and world drawing contours", () =>
        {
            foreach (var (geometry, original) in new (IGeometryShape, BossMod.ArenaBoundsCustom)[]
            {
                (StoneVigilArenas.ChudoYudo(3), Chudo.arena), (StoneVigilArenas.Koshchei(3), Koshchei.arena),
                (StoneVigilArenas.Isgebind(3), Isgebind.arena),
            })
            {
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .5f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .5f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z)) == expected && host.Contains(new(x, 3, z)) == expected,
                            $"Arena changed at {x}, {z}.");
                    }
            }
        });
        foreach (var (boss, action, oid, radius) in new[]
        {
            (0x5B5u, 902u, 0x5B5u, 10.24f), (0x5B5u, 903u, 0x5B5u, 44.24f),
            (0x38C7u, 29053u, 0x38C7u, 6f), (0x5AFu, 1022u, 0x5AFu, 27f),
            (0x5AFu, 1023u, 0x5AFu, 5f), (0x5AFu, 1024u, 0x233Cu, 5f),
        })
            run($"Stone Vigil AID {action} exact cast geometry and timestamp", () =>
            {
                using var f = new Fight(boss, 1042);
                f.Actors[10] = new(10, oid, f.Center); f.Scan(); f.Cast(10, action, 4, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Cast radius differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(4.3), "NPC timing differs.");
            });
        run("Stone Vigil Cauterize keeps forward forty-eight by twenty rectangle", () =>
        {
            using var f = new Fight(0x5AF, 1042);
            f.Cast(1, 1026, 4, MathF.PI / 2);
            var geometry = f.Draw.Previews.Single().Value.Geometry;
            Check(geometry.Contains(f.Center + new Vector3(47.99f, 0, 9.99f))
                && !geometry.Contains(f.Center - Vector3.UnitX)
                && !geometry.Contains(f.Center + new Vector3(24, 0, 10.01f)), "Cauterize dimensions differ.");
        });
        run("Stone Vigil Typhoon shows four stationary stages then ten moving stages with point-six cadence", () =>
        {
            using var f = Typhoon();
            Check(f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count == 4, "Initial Typhoon must show four explosions.");
            f.Actors[11] = new(11, 0x38D0, new(57, 0, -80)); f.Scan();
            f.Engine.Effect(11, 28731, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 4, "Stationary helper at X >= 56 advanced Typhoon.");
            f.Actors[11] = f.Actors[11] with { Position = new(54, 0, -80) }; f.Scan();
            f.Engine.Effect(11, 28731, 2, f.Now); f.Render();
            var predictions = f.Engine.OutputCasts.Select(c => c.Value).ToArray();
            Check(predictions.Length == 10 && f.Draw.Previews.Count == 10, "First moving explosion must expand preview to ten.");
            for (var i = 0; i < 10; ++i)
            {
                var expected = new BossMod.WPos(54 - 1.745f * (i + 1), -80).Quantized();
                Check(Vector2.Distance(new(predictions[i].Location.X, predictions[i].Location.Z), new(expected.X, expected.Z)) < .001f,
                    "Typhoon movement step/quantization differs.");
                Check(predictions[i].FinishAt == f.Start.AddSeconds(.6).AddSeconds(.6 * i), "Typhoon cadence differs.");
            }
            f.Engine.Effect(11, 28731, 2, f.Now);
            Check(f.Engine.OutputCasts.First().Value == predictions[0], "Duplicate hit advanced Typhoon.");
        });
        run("Stone Vigil parallel Typhoon rows advance independently and dead visuals release only their own row", () =>
        {
            using var f = Typhoon();
            f.Actors[20] = new(20, 0x38C8, new(57, 0, -75)); f.Scan();
            f.Actors[11] = new(11, 0x38D0, new(54, 0, -80)); f.Scan();
            f.Engine.Effect(11, 28731, 1, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 14, "Moving row changed another row's preview cap.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; f.Scan();
            Check(f.Draw.Previews.Count == 4 && f.Engine.OutputCasts.All(c => c.Value.Location.Z > -76),
                "Dead row cleared the wrong Z lane.");
            f.Actors.Remove(20); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Removed visual retained its row.");
        });
        run("Stone Vigil all fifteen moving explosions finish without resurrecting a stale visual", () =>
        {
            using var f = Typhoon();
            f.Actors[11] = new(11, 0x38D0, new(54, 0, -80)); f.Scan();
            for (var i = 0; i < 15; ++i)
            {
                f.Now = f.Start.AddSeconds(i * .6);
                f.Actors[11] = f.Actors[11] with { Position = new(54 - 1.745f * i, 0, -80) };
                f.Scan(); f.Engine.Effect(11, 28731, (uint)(i + 1), f.Now); f.Render();
            }
            f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Completed Typhoon recreated from stale visual.");
        });
        run("Stone Vigil Touchdown follows boss timeline plus seven-point-eight seconds and clears on hit or timeout", () =>
        {
            using var f = new Fight(0x5AF, 1042);
            f.Engine.Timeline(10, 0x8E, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "An unrelated helper created Touchdown.");
            f.Engine.Timeline(1, 0x8E, f.Now); f.Render();
            var cast = f.Engine.OutputCasts.Single().Value;
            Check(cast.FinishAt == f.Start.AddSeconds(7.8) && f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 5 },
                "Touchdown prediction changed.");
            var expected = new BossMod.WPos(0, -248).Quantized();
            Check(Vector2.Distance(new(cast.Location.X, cast.Location.Z), new(expected.X, expected.Z)) < .001f, "Touchdown missed arena center.");
            f.Engine.Effect(1, 1027, 1, f.Start.AddSeconds(7.8)); f.Render();
            Check(f.Draw.Previews.Count == 0, "Touchdown effect retained prediction.");
            f.Engine.Timeline(1, 0x8E, f.Now);
            f.Now = f.Start.AddSeconds(10); f.Engine.Tick(f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0, "Missing Touchdown effect left infinite output.");
        });
        run("Stone Vigil tank cleave distinction pauses while boss is casting", () =>
        {
            foreach (var (boss, action) in new[] { (0x5B5u, 902u), (0x5AFu, 1022u) })
            {
                using var f = new Fight(boss, 1042);
                f.Actors[1] = f.Actors[1] with { TargetId = 101 };
                f.Actors[101] = new(101, 0, f.Center + Vector3.UnitX * 3, Hostile: false, PartySlot: 1); f.Scan();
                Check(f.Draw.Previews.Single().Value.OutlineOnly, "Missing automatic cleave display.");
                f.Cast(1, action, 1);
                Check(f.Draw.Previews.Count == 1 && !f.Draw.Previews.Single().Value.OutlineOnly, "Automatic cleave duplicated cast display.");
            }
        });
        run("Stone Vigil actual temporal solver escapes moving Typhoon inside the real arena", () =>
        {
            using var f = Typhoon();
            f.Actors[11] = new(11, 0x38D0, new(54, 0, -80)); f.Scan();
            f.Engine.Effect(11, 28731, 1, f.Now); f.Render();
            var snapshot = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(snapshot, new(new(50, 0, -80), 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .5f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count >= 2, "No Typhoon escape route.");
            Check(path.All(p => f.Engine.Arena!.Contains(p.Position)), "Typhoon path crossed arena wall.");
            Check(snapshot.DangerAreas.All(a => !a.Geometry.Contains(path[^1].Position)), "Path ended in a later Typhoon stage.");
        });
    }

    private static Fight Typhoon()
    {
        var fight = new Fight(0x38C7, 1042);
        fight.Actors[10] = new(10, 0x38C8, new(57, 0, -80));
        fight.Scan();
        return fight;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
