using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class CuttersCryRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Cutters Cry all three polygon arenas match original runtime and world conversion", () =>
        {
            foreach (var (geometry, name) in new[]
            {
                (CuttersCryArenas.Princess(3), "D091MyrmidonPrincess"),
                (CuttersCryArenas.Worm(3), "D092GiantTunnelWorm"),
                (CuttersCryArenas.Chimera(3), "D093Chimera"),
            })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D09CuttersCry.D093Chimera.D093Chimera).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D09CuttersCry.{name}.{name}")!;
                var original = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .127f; x < bounds.MaxX; x += .7f)
                    for (var z = bounds.MinZ + .213f; z < bounds.MaxZ; z += .7f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z)) == expected && host.Contains(new(x, 3, z)) == expected,
                            $"Arena {name} changed at {x}, {z}.");
                    }
            }
        });
        foreach (var (x, z, angle) in new[]
        {
            (-135.623f, 147.871f, -45f), (-142.695f, 144.942f, 0f),
            (-149.766f, 162.013f, 135f), (-142.695f, 164.942f, 180f),
            (-152.695f, 154.942f, 90f), (-135.623f, 162.013f, -135f),
            (-149.766f, 147.871f, 45f), (-132.694f, 154.942f, -90f),
        })
            run($"Cutters Cry Worm seven-stage SandPillar sequence from {angle} degrees preserves cadence and final Earthbreak", () =>
            {
                using var f = new Fight(0x48FF, 1303);
                f.Actors[10] = new(10, 0x233C, new(x, 4, z)); f.Scan();
                f.Engine.Effect(10, 44232, 1, f.Now); f.Render();
                var casts = f.Engine.OutputCasts.Select(c => c.Value).ToArray();
                Check(casts.Length == 7 && f.Draw.Previews.Count == 7 && f.Snapshot.DangerAreas.Count == 7, "Incomplete seven-stage prediction.");
                for (var i = 0; i < 7; ++i)
                {
                    var direction = new BossMod.WDir(MathF.Sin(angle * MathF.PI / 180), MathF.Cos(angle * MathF.PI / 180));
                    var expected = (new BossMod.WPos(x, z) + direction * (4 * (i + 1))).Quantized();
                    Check(Vector2.Distance(new(casts[i].Location.X, casts[i].Location.Z), new(expected.X, expected.Z)) < .001f, "Sand sequence step/quantization changed.");
                    Check(casts[i].FinishAt == f.Start.AddSeconds(.9 * (i + 1)), "Sand sequence incorrectly added NPC padding.");
                    Check(casts[i].Rule.Shape is MasterPlayer.Spatial.SafeZone.CircleTemplate { Radius: var radius }
                        && radius == (i == 6 ? 10 : 4), "Final blast radius differs.");
                }
                f.Engine.Effect(10, 44232, 1, f.Now);
                Check(f.Engine.OutputCasts.Count() == 7, "Duplicate effect advanced a stage.");
                for (var i = 1; i <= 6; ++i)
                {
                    f.Now = f.Start.AddSeconds(.9 * i);
                    f.Engine.Effect(10, 44232, (uint)(i + 1), f.Now); f.Render();
                    Check(f.Draw.Previews.Count == 7 - i, "A resolved sand pillar was not removed.");
                }
                Check(f.Engine.OutputCasts.Single().Value.Rule.Action == 44233, "Final Earthbreak prediction disappeared early.");
                f.Engine.Effect(1, 44233, 20, f.Start.AddSeconds(6.3)); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final Earthbreak leaked.");
            });
        run("Cutters Cry BottomlessDesert predicts Earthbreak at 3.1 seconds and unknown starts invent no line", () =>
        {
            using var f = new Fight(0x48FF, 1303);
            f.Actors[10] = new(10, 0x233C, new(-142, 0, 155)); f.Scan();
            f.Engine.Effect(10, 44232, 1, f.Now); f.Render();
            Check(!f.Engine.OutputCasts.Any(), "Unknown sand source invented a line.");
            f.Engine.Effect(10, 44234, 2, f.Now); f.Render();
            Check(f.Engine.OutputCasts.Single().Value.FinishAt == f.Start.AddSeconds(3.1), "Pull blast timing changed.");
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 10 }, "Pull blast radius changed.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Wipe retained no-cast sequence.");
        });
        run("Cutters Cry Chimera orb retains 10.5 second activation while moving and only its target kites at ten yards", () =>
        {
            using var f = new Fight(0x4900, 1303);
            f.Actors[10] = new(10, 0x4901, new(-180, 0, -210)); f.Scan();
            f.Events.Handle(new LogSystemTargetIconEvent { SourceId = 100, TargetId = 100, MarkerId = 23 }, f.Now); f.Render();
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 6 }
                && f.Snapshot.DangerAreas.Count == 2, "Target kite radius polluted the shared display.");
            Check(f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(10.5)), "Orb spawn timing changed.");
            f.Now = f.Start.AddSeconds(2);
            f.Actors[10] = f.Actors[10] with { Position = new(-175, 0, -210) }; f.Scan();
            Check(f.Draw.Previews.Single().Value.Geometry.Position.X > -175.1f
                && f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(10.5)), "Moving orb restarted its activation clock.");
            f.PlayerId = 101; f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 1 && !f.Engine.Hints(f.Center).Contains("被雷球点名：风筝雷球"), "Perspective switch retained target-only kite restriction.");
            f.Engine.Effect(10, 44240, 3, f.Now); f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Detonated orb resurrected from stale frame.");
        });
        run("Cutters Cry orb disappearance, death and encounter reset clear timed navigation", () =>
        {
            using var f = new Fight(0x4900, 1303);
            f.Actors[10] = new(10, 0x4901, f.Center); f.Scan();
            f.Actors.Remove(10); f.Scan();
            Check(f.Snapshot.DangerAreas.Count == 0 && f.Draw.Previews.Count == 0, "Removed orb retained navigation.");
            f.Actors[11] = new(11, 0x4901, f.Center); f.Scan();
            f.Actors[11] = f.Actors[11] with { Dead = true }; f.Scan();
            Check(f.Draw.Previews.Count == 0, "Dead orb retained display.");
        });
        foreach (var (boss, oid, action, radius) in new[]
        {
            (0x48FBu, 0x233Cu, 44227u, 5f), (0x48FBu, 0x48FBu, 44229u, 9f),
            (0x4900u, 0x4900u, 44243u, 9.7f), (0x4900u, 0x4900u, 44242u, 9.7f),
            (0x4900u, 0x4900u, 44237u, 9.7f), (0x4900u, 0x233Cu, 44245u, 5f),
        })
            run($"Cutters Cry AID {action} actual radius and cast snapshot timing", () =>
            {
                using var f = new Fight(boss, 1303);
                f.Actors[10] = new(10, oid, f.Center); f.Scan(); f.Cast(10, action, 3, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                Check(shape.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !shape.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Display radius differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(3.3), "Cast padding differs.");
            });
        run("Cutters Cry Dragon voice keeps eight-yard hole and original thirty-yard outer edge", () =>
        {
            using var f = new Fight(0x4900, 1303);
            f.Cast(1, 44238, 4.5);
            var shape = f.Draw.Previews.Single().Value.Geometry;
            Check(!shape.Contains(f.Center + Vector3.UnitX * 7.99f)
                && shape.Contains(f.Center + Vector3.UnitX * 8.01f)
                && !shape.Contains(f.Center + Vector3.UnitX * 30.01f), "Dragon voice dimensions changed.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
