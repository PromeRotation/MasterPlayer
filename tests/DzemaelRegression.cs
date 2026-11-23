using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class DzemaelRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Dzemael remade arenas preserve exact polygons, entrance subtraction and world conversion", () =>
        {
            foreach (var (geometry, name) in new[]
            {
                (DzemaelArenas.AllSeeingEye(3), "D111AllSeeingEye"),
                (DzemaelArenas.Taulurd(3), "D112Taulurd"),
                (DzemaelArenas.Batraal(3), "D113Batraal"),
            })
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D11DzemaelDarkhold.D111AllSeeingEye.D111AllSeeingEye).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D11DzemaelDarkhold.{name}.{name}")!;
                var tuple = type.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var original = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                var host = HostDrawingGeometry.Convert(geometry);
                var bounds = geometry.GetBounds();
                for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += .5f)
                    for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += .5f)
                    {
                        var expected = original.Contains(new(x - original.Center.X, z - original.Center.Z));
                        Check(geometry.Contains(new(x, 3, z)) == expected && host.Contains(new(x, 3, z)) == expected,
                            $"Arena {name} differs at {x}, {z}.");
                    }
            }
        });
        foreach (var (boss, oid, action, radius) in new[]
        {
            (0x4A8Au, 0x233Cu, 45568u, 10f), (0x4A8Bu, 0x4A8Bu, 45571u, 8f),
            (0x4A8Bu, 0x4A8Cu, 45575u, 5f), (0x4A8Du, 0x4A8Du, 45579u, 12f),
            (0x4A8Du, 0x4A8Eu, 45583u, 6f),
        })
            run($"Dzemael AID {action} exact dimensions and native absolute danger timing", () =>
            {
                using var f = new Fight(boss, 1330);
                f.Actors[10] = new(10, oid, f.Center); f.Scan(); f.Cast(10, action, 5, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + Vector3.UnitX * (radius - .01f))
                    && !geometry.Contains(f.Center + Vector3.UnitX * (radius + .01f)), "Original radius differs.");
                Check(f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(5.3), "NPC delay differs.");
            });
        run("Dzemael Blink and Desolation retain forward length, width and cast facing", () =>
        {
            foreach (var (boss, action, length, halfWidth) in new[]
            {
                (0x4A8Au, 45570u, 55f, 5f), (0x4A8Du, 45582u, 60f, 3f),
            })
            {
                using var f = new Fight(boss, 1330);
                f.Cast(1, action, 3, MathF.PI / 2);
                var geometry = f.Draw.Previews.Single().Value.Geometry;
                Check(geometry.Contains(f.Center + new Vector3(length - .01f, 0, halfWidth - .01f))
                    && !geometry.Contains(f.Center - Vector3.UnitX)
                    && !geometry.Contains(f.Center + new Vector3(10, 0, halfWidth + .01f)), "Forward ray dimensions differ.");
            }
        });
        run("Dzemael VoidMatter six-helper cap promotes a later helper after natural finish", () =>
        {
            using var f = new Fight(0x4A8A, 1330);
            for (var i = 10; i < 18; ++i)
            {
                f.Actors[(ulong)i] = new((ulong)i, 0x233C, f.Center); f.Scan();
                f.Cast((ulong)i, 45568, 8);
            }
            Check(f.Engine.Casts.Count == 8 && f.Draw.Previews.Count == 6 && f.Snapshot.DangerAreas.Count == 6, "VoidMatter cap differs.");
            f.Complete(10, 8);
            Check(f.Draw.Previews.Count == 6 && f.Engine.OutputCasts.Any(c => c.Value.Source == 16), "Later VoidMatter failed to promote.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Wipe left VoidMatter output.");
        });
        run("Dzemael Batraal invincibility bans only boss and retargets crystals through gain and loss", () =>
        {
            using var f = new Fight(0x4A8D, 1330);
            f.Actors[10] = new(10, 0x4A8E, f.Center);
            f.Actors[100] = new(100, 0, f.Center, Hostile: false, Statuses: [4410]); f.Scan();
            f.Events.Handle(new LogSystemStatusAddEvent { TargetId = 1, StatusId = 4410, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ForbiddenTargets.SequenceEqual([1UL]) && f.Engine.PriorityTarget(1, f.Center) == 10,
                "Invincibility banned a crystal/party member or retained boss targeting.");
            f.Actors[10] = f.Actors[10] with { Targetable = false }; f.Scan();
            Check(f.Engine.PriorityTarget(1, f.Center) == 0, "Invincible boss was selected when no crystal available.");
            f.Actors[1] = f.Actors[1] with { Statuses = [4410] };
            f.Events.Handle(new LogSystemStatusRemoveEvent { TargetId = 1, StatusId = 4410, Timestamp = f.Now }, f.Now);
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Engine.PriorityTarget(0, f.Center) == 1,
                "Status loss did not immediately restore boss despite stale object status.");
        });
        run("Dzemael invincible actor removal and death release action restriction", () =>
        {
            using var f = new Fight(0x4A8D, 1330);
            f.Actors[1] = f.Actors[1] with { Statuses = [4410] }; f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 1, "Scanned invincibility missing.");
            f.Actors[1] = f.Actors[1] with { Dead = true }; f.Scan();
            Check(f.Engine.ForbiddenTargets.Length == 0 && f.Snapshot.ArenaBoundary == null, "Death retained invincibility restriction.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
