using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using PromeRotation.LogSystem;
using Fight = HalataliRegression.Fight;

internal static class KeeperRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (native, typeName) in new[] { (KeeperArenas.Einhander(0), "D291Einhander"),
            (KeeperArenas.Gunship(0), "D292MagitekGunship"), (KeeperArenas.Midgardsormr(0), "D293Midgardsormr") })
            run($"Keeper {typeName} arena subtraction and world geometry match actual runtime", () =>
            {
                var type = typeof(BossMod.RealmReborn.Dungeon.D29TheKeeperoftheLake.D291Einhander.D291Einhander).Assembly
                    .GetType($"BossMod.RealmReborn.Dungeon.D29TheKeeperoftheLake.{typeName}.{typeName}")!;
                var bounds = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var host = HostDrawingGeometry.Convert(native);
                for (var x = -23.13f; x < 23; x += .37f)
                    for (var z = -23.27f; z < 23; z += .37f)
                    {
                        var p = native.Position + new Vector3(x, 0, z);
                        var expected = bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z));
                        Check(native.Contains(p) == expected && host.Contains(p) == expected, "Arena cutout changed.");
                    }
            });
        run("Keeper Gunship Flamethrower survives cast finish and counts six distinct packets not targets", () =>
        {
            using var f = new Fight(0xE68, 1063); f.Cast(1, 3389, 3.5, MathF.PI / 2);
            f.Complete(1, 3.5);
            for (uint sequence = 1; sequence <= 6; ++sequence)
            {
                f.Engine.Effect(1, sequence == 1 ? 3389u : 3390u, sequence, f.Now, 100);
                f.Engine.Effect(1, sequence == 1 ? 3389u : 3390u, sequence, f.Now, 101); f.Render();
                Check(f.Draw.Previews.Count == (sequence == 6 ? 0 : 1)
                    && f.Snapshot.DangerAreas.Count == (sequence == 6 ? 0 : 1), "Repeated target advanced or early-ended Flamethrower.");
            }
            f.Now = f.Now.AddSeconds(1); f.Cast(1, 3389, 3.5); f.Engine.Finish(1, 3389); f.Render();
            Check(f.Draw.Previews.Count == 0, "Cancelled Flamethrower remained.");
        });
        run("Keeper Gunship fire prediction uses original event time and hands off to event-state-filtered object", () =>
        {
            using var f = new Fight(0xE68, 1063); var location = Vector3.UnitX * 9;
            f.Actors[10] = new(10, 0xE7B, f.Center); f.Scan();
            f.Events.Handle(new LogSystemActionEffectEvent { SourceId = 10, ActionId = 3411, GlobalSequence = 1,
                EffectPosition = location, Timestamp = f.Start.AddSeconds(-.1) }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(.1),
                "Garlean Fire lost effect position or delayed-dispatch correction.");
            f.Actors[11] = new(11, 0x1E98CA, location); f.Scan();
            Check(f.Draw.Previews.Count == 1 && f.Draw.Previews.ContainsKey("garlean/zone/11"), "Fire object duplicated prediction.");
            f.Actors[11] = f.Actors[11] with { EventState = 7 }; f.Scan();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Ended fire event state remained dangerous.");
        });
        run("Keeper Midgardsormr Antipathy three stages use independent helper finishes and two-second steps", () =>
        {
            using var f = new Fight(0x392A, 1063);
            foreach (var i in new ulong[] { 10, 11, 12 }) f.Actors[i] = new(i, 0x233C, f.Center);
            f.Scan(); f.Cast(10, 29286, 4); f.Cast(11, 29287, 6); f.Cast(12, 29288, 8);
            Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: 6 }, "First stage displayed future rings.");
            f.Complete(10, 4);
            Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry { InnerRadius: 6, OuterRadius: 12 }
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(6), "Second stage changed.");
            f.Complete(11, 6);
            Check(f.Draw.Previews.Single().Value.Geometry is DonutGeometry { InnerRadius: 12, OuterRadius: 20 }
                && f.Snapshot.DangerAreas.Single().DangerWindows.Single().DangerFrom == f.Start.AddSeconds(8), "Third stage changed.");
            f.Complete(12, 8); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final helper finish leaked.");
        });
        run("Keeper Mirage timeline forecasts locked pose twelve seconds ahead and one effect clears all rays", () =>
        {
            using var f = new Fight(0x392A, 1063);
            f.Actors[10] = new(10, 0x392C, f.Center, Rotation: MathF.PI / 2);
            f.Actors[11] = new(11, 0x392B, Vector3.UnitZ * 3); f.Scan();
            f.Engine.Timeline(10, 0x11D3, f.Now); f.Engine.Timeline(11, 0x11D3, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.All(a => a.DangerWindows.Single().DangerFrom == f.Start.AddSeconds(12)),
                "Mirage timeline delay changed.");
            f.Actors[10] = f.Actors[10] with { Position = Vector3.UnitZ * 20, Rotation = 0 };
            f.Now = f.Now.AddSeconds(1); f.Scan();
            Check(f.Draw.Previews["mirage/10"].Geometry.Contains(Vector3.UnitX * 30)
                && !f.Draw.Previews["mirage/10"].Geometry.Contains(new(0, 0, 30)), "Timeline pose followed moving actor.");
            f.Engine.Effect(10, 29290, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Mirage effect failed to clear both rays.");
        });
        run("Keeper Akh Morn stack follows target through all four effects and ignores duplicate AOE target rows", () =>
        {
            using var f = new Fight(0x392A, 1063);
            f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false);
            f.Actors[101] = new(101, 0, Vector3.UnitX * 5, PartySlot: 1, Hostile: false); f.Scan();
            var cast = new DungeonCastObservation(29283, f.Center, 0, f.Now, f.Now.AddSeconds(5.3), 101);
            f.Actors[1] = f.Actors[1] with { Cast = cast }; f.Engine.StartCast(1, cast, f.Now); f.Render();
            f.Complete(1, 5);
            for (uint sequence = 1; sequence <= 4; ++sequence)
            {
                f.Engine.Effect(1, sequence == 1 ? 29283u : 29284u, sequence, f.Now, 100);
                f.Engine.Effect(1, sequence == 1 ? 29283u : 29284u, sequence, f.Now, 101);
                f.Actors[101] = f.Actors[101] with { Position = Vector3.UnitX * (5 + sequence) }; f.Scan();
                Check(f.Snapshot.GoalAreas.Count == (sequence == 4 ? 0 : 1) && f.Draw.Previews.Count == (sequence == 4 ? 0 : 1),
                    "Stack ended before fourth unique hit or leaked after it.");
                if (sequence < 4) Check(f.Draw.Previews.Values.Single().Geometry.Position == f.Actors[101].Position, "Stack failed to follow target.");
            }
        });
        run("Keeper delayed raidwide and delayable Heavy Swing remain until actual effect", () =>
        {
            using var f = new Fight(0x392A, 1063);
            f.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false); f.Scan(); f.Cast(1, 29891, 4); f.Complete(1, 4);
            Check(f.Engine.PredictedDamage.Single().Activation == f.Start.AddSeconds(4.8), "Disgust visual-end delay changed.");
            f.Engine.Effect(20, 29892, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Disgust effect retained prediction.");
            using var first = new Fight(0x3927, 1063);
            first.Actors[100] = new(100, 0, f.Center, PartySlot: 0, Hostile: false);
            first.Actors[1] = first.Actors[1] with { TargetId = 100 }; first.Scan();
            var cast = new DungeonCastObservation(29620, first.Center, 0, first.Now, first.Now.AddSeconds(5.3), 100);
            first.Actors[1] = first.Actors[1] with { Cast = cast }; first.Engine.StartCast(1, cast, first.Now); first.Complete(1, 5);
            Check(first.Engine.PredictedDamage.Count() == 1, "Heavy Swing dropped at natural cast end.");
            first.Engine.Effect(1, 29620, 1, first.Now); Check(!first.Engine.PredictedDamage.Any(), "Heavy Swing effect did not clear.");
        });
        foreach (var (boss, caster, action, radius) in new[]
        {
            (0x3927u, 0x3929u, 29275u, 12f), (0x3927u, 0x3927u, 29272u, 15f),
            (0xE68u, 0xE7Bu, 3392u, 5f), (0x392Au, 0x392Au, 29888u, 22f),
        })
            run($"Keeper AID {action} actual display radius and navigation margin", () =>
            {
                using var f = new Fight(boss, 1063);
                var source = caster == boss ? 1UL : 10UL; if (source == 10) { f.Actors[10] = new(10, caster, f.Center); f.Scan(); }
                f.Cast(source, action, 4);
                Check(f.Draw.Previews.Single().Value.Geometry is CircleGeometry { Radius: var r } && r == radius
                    && f.Snapshot.DangerAreas.Single().Geometry is CircleGeometry { Radius: var inflated } && inflated == radius + .5f,
                    "Comment range or hitbox replaced actual shape radius.");
            });
        foreach (var (boss, caster, action, length, width) in new[]
        {
            (0x3927u, 0x3927u, 29271u, 40f, 4f), (0xE68u, 0xE69u, 1433u, 32.8f, 5f),
            (0x392Au, 0x392Au, 29889u, 40f, 12f), (0x392Au, 0x392Au, 29280u, 40f, 12f),
        })
            run($"Keeper AID {action} rectangle dimensions and locked rotation", () =>
            {
                using var f = new Fight(boss, 1063);
                var source = caster == boss ? 1UL : 10UL; if (source == 10) { f.Actors[10] = new(10, caster, f.Center); f.Scan(); }
                f.Cast(source, action, 4, MathF.PI / 2);
                var shape = f.Draw.Previews.Single().Value.Geometry;
                Check(shape.Contains(new(length - .01f, 0, width / 2 - .01f)) && !shape.Contains(new(length + .01f, 0, 0))
                    && !shape.Contains(new(1, 0, width / 2 + .01f)), "Ray extent or rotation differs.");
            });
        run("Keeper Phantom Outer Turmoil preserves twenty-point-five-to-thirty-nine half-donut facing", () =>
        {
            using var f = new Fight(0x392A, 1063); f.Cast(1, 29279, 7, MathF.PI / 2);
            var shape = f.Draw.Previews.Single().Value.Geometry;
            Check(shape.Contains(Vector3.UnitX * 25) && !shape.Contains(Vector3.UnitX * 20.49f)
                && !shape.Contains(Vector3.UnitX * 39.01f) && !shape.Contains(-Vector3.UnitX * 25), "Half-donut became full ring or reversed.");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
