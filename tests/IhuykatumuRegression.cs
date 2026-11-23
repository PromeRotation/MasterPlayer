using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class IhuykatumuRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Ihuykatumu ownership client territory exact clipped arenas both views", () =>
        {
            Check(ArrDungeonCatalog.Create(1167).Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.ihuykatumu"), "Discovery.");
            foreach (var (typeName, arena) in new (string, MasterPlayer.Spatial.Geometry.IGeometryShape)[]
            {
                ("D012Drowsie", IhuykatumuArenas.Drowsie(0)), ("D013Apollyon", IhuykatumuArenas.Apollyon(0)),
            })
            {
                var type = typeof(BossMod.Dawntrail.Dungeon.D01Ihuykatumu.D012Drowsie.D012Drowsie).Assembly
                    .GetType($"BossMod.Dawntrail.Dungeon.D01Ihuykatumu.{typeName}.{typeName}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("DefaultBounds", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
                var c = arena.Position; var world = HostDrawingGeometry.Convert(arena);
                Check(c.X == source.Center.X && c.Z == source.Center.Z, "Resolved center.");
                for (var x = -21.137f; x < 21; x += .37f)
                    for (var z = -21.231f; z < 21; z += .37f)
                    {
                        var p = c + new Vector3(x, 0, z); var expected = source.Contains(new(p.X - c.X, p.Z - c.Z));
                        Check(arena.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source clipped bounds.");
                    }
            }
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4190, 36493, new BossMod.AOEShapeCone(60, new(MathF.PI / 12))),
            (0x4195, 36478, new BossMod.AOEShapeCircle(8)),
            (0x4195, 36479, new BossMod.AOEShapeRect(40, 5)),
            (0x4195, 36482, new BossMod.AOEShapeRect(40, 8)),
            (0x4195, 36475, new BossMod.AOEShapeCone(60, new(75 * MathF.PI / 180))),
            (0x4195, 36485, new BossMod.AOEShapeCircle(6)),
            (0x4165, 36340, new BossMod.AOEShapeRect(50, 6)),
            (0x4165, 36346, new BossMod.AOEShapeRect(50, 6)),
            (0x4165, 36350, new BossMod.AOEShapeCircle(4)),
            (0x4165, 36351, new BossMod.AOEShapeCone(40, new(MathF.PI / 8))),
            (0x4165, 36358, new BossMod.AOEShapeDonut(5, 60)),
            (0x4165, 36355, new BossMod.AOEShapeRect(40, 20)),
        })
            run($"Ihuykatumu {action} source geometry radar world observed finish no timeout", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var c = f.Engine.OutputCasts.Single().Value; var native = c.Rule.Shape!.Resolve(c.Location, c.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -61.137f; x < 61; x += 1.31f)
                    for (var z = -61.231f; z < 61; z += 1.31f)
                    {
                        var p = c.Location + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(c.Location.X, c.Location.Z), new(c.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Shape mismatch.");
                    }
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Clock cleared cast.");
                Finish(f, 10); Check(f.Draw.Previews.Count == 0, "Finish leaked.");
            });
        foreach (var (action, shape) in new (uint, BossMod.AOEShape)[]
        {
            (36497, new BossMod.AOEShapeCircle(12)), (36500, new BossMod.AOEShapeRect(35, 5)),
            (36498, new BossMod.AOEShapeCircle(8)), (36501, new BossMod.AOEShapeCircle(4)),
            (36499, new BossMod.AOEShapeRect(25, 3)), (36502, new BossMod.AOEShapeCircle(6)),
            (36503, new BossMod.AOEShapeRect(25, 3)), (36504, new BossMod.AOEShapeRect(35, 5)),
            (36505, new BossMod.AOEShapeDonut(6, 40)),
        })
            run($"Ihuykatumu Bury {action} exact ambient geometry", () =>
            {
                using var f = Prepare(0x4190); Cast(f, 10, action);
                var a = Aoes(f, "bury").Single(); var geometry = HostDrawingGeometry.Convert(a.Shape.Resolve(a.Position, a.Rotation, null));
                for (var x = -41.137f; x < 41; x += 1.19f)
                    for (var z = -41.231f; z < 41; z += 1.19f)
                    {
                        var p = a.Position + new Vector3(x, 0, z);
                        Check(geometry.Contains(p, 0) == shape.Check(new(p.X, p.Z), new(a.Position.X, a.Position.Z), new(a.Rotation)), "Bury world shape.");
                    }
            });
        run("Ihuykatumu Bury all risky FIFO by different caster persistent first two color destroy advances", () =>
        {
            using var f = Prepare(0x4190); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36497); Cast(f, 11, 36505, location: c + Vector3.UnitX * 4); Cast(f, 12, 36500, location: c + Vector3.UnitX * 8);
            var a = Aoes(f, "bury");
            Check(a.Length == 3 && a.All(x => x.Navigate) && a.Take(2).All(x => x.Color == new Vector4(1, 1, 0, 1))
                && f.Draw.Previews.Count == 3 && f.Snapshot.DangerAreas.Count == 3, "Bury risk/display.");
            Finish(f, 12); a = Aoes(f, "bury");
            Check(a.Length == 2 && a[0].Position == c + Vector3.UnitX * 4 && a[0].Color != null && a[1].Color == null, "Finished by caster rather than FIFO or color reset.");
            f.Engine.RemoveActor(11, f.Now); f.Actors.Remove(11); f.Render();
            Check(Aoes(f, "bury").Single().Position == c + Vector3.UnitX * 8, "Destroyed caster did not advance.");
        });
        run("Ihuykatumu inhale fixed quantized anchor independent real resurface finish exactly six hits", () =>
        {
            using var f = Prepare(0x4190); Cast(f, 10, 36494);
            Check(f.Draw.Previews.Count == 2 && Aoes(f, "inhale").Single().Position == Q(new(15, 0, -95)), "Separate inhale origin.");
            Finish(f, 10); Check(f.Draw.Previews.Count == 1, "Resurface end cleared inhale.");
            for (uint i = 1; i <= 5; ++i) f.Engine.Effect(10, 36496, i, f.Now);
            f.Render(); Check(Aoes(f, "inhale").Length == 1, "Early inhale clear.");
            f.Engine.Effect(10, 36496, 6, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Sixth inhale did not clear.");
        });
        run("Ihuykatumu concentric current stage true end clock axis tolerance and full sequence", () =>
        {
            using var f = Prepare(0x4190); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36515); Cast(f, 11, 36516);
            Check(Aoes(f, "shore").Single().Shape is CircleTemplate { Radius: 10 }, "Second start created extra ring.");
            f.Now = f.Now.AddSeconds(10); f.Engine.Tick(f.Now); f.Render();
            Check(Aoes(f, "shore").Single().Shape is CircleTemplate, "Clock advanced concentric.");
            Finish(f, 10);
            Check(Aoes(f, "shore").Single() is { Shape: DonutTemplate { InnerRadius: 10, OuterRadius: 20 }, Activation: var at }
                && at == f.Now.AddSeconds(2), "First finish order/time.");
            Finish(f, 11); Check(Aoes(f, "shore").Single().Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 30 }, "Second real finish.");
            Cast(f, 12, 36517, location: c + new Vector3(.9f, 0, .9f)); Finish(f, 12);
            Check(!Aoes(f, "shore").Any(), "Axis tolerance/final clear.");
        });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x4190, 36508, 14), (0x4190, 36513, 6), (0x4195, 36522, 6), (0x4165, 36353, 6) })
            run($"Ihuykatumu {action} dynamic spread outline radius plus one perspective", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action, 101);
                Check(Aoes(f, "spread").Single(a => a.Display) is { OutlineOnly: true, Shape: CircleTemplate { Radius: var r } } && r == radius
                    && Aoes(f, "spread").Single(a => a.Navigate).Shape is CircleTemplate { Radius: var n } && n == radius + 1, "Spread radii.");
                f.PlayerId = 101; Scan(f); Check(Aoes(f, "spread").Single(a => a.Navigate).Position == Q(f.Actors[100].Position), "Spread perspective.");
                Finish(f, 10); Check(!Aoes(f, "spread").Any(), "Spread end.");
            });
        run("Ihuykatumu tether enemy filter both directions untether retain dynamic heading own outline edge and second clock", () =>
        {
            using var f = Prepare(0x4190); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x4192, c - Vector3.UnitZ * 10); f.Actors[21] = new(21, 0x4191, c); Scan(f);
            f.Engine.Tether(101, 17, true, f.Now, 21); f.Render(); Check(!Aoes(f, "bait").Any(), "Large add accepted by medium-only bait.");
            f.Engine.Tether(20, 17, true, f.Now, 101); f.Render();
            var first = Aoes(f, "bait").Single(a => a.Id.EndsWith("/display"));
            Check(first.Navigate && !first.OutlineOnly && first.Activation == f.Now.AddSeconds(8.6), "Reverse tether/activation.");
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 10 }; Scan(f);
            Check(MathF.Abs(Aoes(f, "bait").Single(a => a.Id.EndsWith("/display")).Rotation - MathF.PI / 4) < .00001f, "Tether heading frozen.");
            f.Engine.Tether(101, 17, false, f.Now, 20); f.PlayerId = 101; Scan(f);
            Check(Aoes(f, "bait").Single(a => a.Id.EndsWith("/display")).OutlineOnly && Aoes(f, "bait").Any(a => a.Id.EndsWith("/edge")), "Untether cleared bait or personal constraints.");
            f.Engine.Effect(1, 36512, 1, f.Now); f.Render(); Check(!Aoes(f, "bait").Any(), "Real hit did not clear all.");
            f.Now = f.Now.AddSeconds(20); f.Engine.Tether(101, 17, true, f.Now, 20); f.Render();
            Check(Aoes(f, "bait").Single(a => a.Id.EndsWith("/display")).Activation == first.Activation, "Source base activation retention changed.");
        });
        foreach (var sw in new[] { false, true })
            run($"Ihuykatumu whirlwind sw={sw} twelve exact event FIFO angles delayed capsules obstacle dead lifecycle", () =>
            {
                using var f = Prepare(0x4165);
                f.Actors[20] = new(20, 0x416C, sw ? new(-120.1f, 0, 279.9f) : new(-121, 0, 250), Dead: true, Rotation: .7f); Scan(f);
                var first = Aoes(f, "cutting"); var at = f.Now;
                Check(first.Length == 4 && first.All(a => a.Navigate && a.Shape is RectangleTemplate { Width: 8, Length: 72 })
                    && first.All(a => a.Activation == at.AddSeconds(8.6)), "First cutting wave.");
                float[] angles = [89.999f, 44.998f, 134.999f, -.003f];
                Check(first.Select(a => a.Rotation).Zip(angles).All(p => MathF.Abs(p.First - p.Second * MathF.PI / 180) < .000001f), "Exact source directions.");
                Check(first[0].Position == Q(sw ? new(-102.935f, 0, 274.357f) : new(-111.688f, 0, 253.942f)), "SW coordinatewise comparison.");
                var moving = Aoes(f, "whirlwind");
                Check(moving.Single(a => a.Display).Shape is DungeonCapsuleTemplate { Radius: 4.5f, Length: 5 }
                    && moving.Count(a => !a.Display && a.Navigate && !a.Obstacle) == 4 && moving.Any(a => a.Obstacle), "Moving voidzone components.");
                for (uint i = 1; i <= 4; ++i) f.Engine.Effect(1, 36360, i, f.Now);
                f.Render(); Check(Aoes(f, "cutting").All(a => a.Activation == at.AddSeconds(16.7)), "Four effects did not promote second wave.");
                f.Engine.RemoveActor(20, f.Now); f.Actors.Remove(20); Scan(f);
                Check(Aoes(f, "cutting").Length == 4 && !Aoes(f, "whirlwind").Any(), "Whirlwind destroy cleared future cutting.");
                for (uint i = 5; i <= 12; ++i) f.Engine.Effect(1, 36360, i, f.Now);
                f.Render(); Check(!Aoes(f, "cutting").Any(), "All twelve not consumed.");
            });
        run("Ihuykatumu lightning cast event prediction delayed actual object match within two no timeout", () =>
        {
            using var f = Prepare(0x4165); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36349, 101);
            Check(Aoes(f, "spark/cast").Single().Position == f.Actors[101].Position, "Spark cast target tracking.");
            f.Engine.Effect(10, 36349, 1, f.Now, 101); Finish(f, 10);
            Check(Aoes(f, "spark/event").Single().Activation == f.Now.AddSeconds(.7), "Spark delay.");
            f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now); f.Render(); Check(Aoes(f, "spark/event").Length == 1, "Unspawned prediction timed out.");
            f.Actors[20] = new(20, 0x1EBA21, f.Actors[101].Position + Vector3.UnitX * 1.9f, Dead: true); Scan(f);
            Check(!Aoes(f, "spark/event").Any() && Aoes(f, "spark/object").Length == 1, "Object prediction handoff/dead filter.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(!Aoes(f, "spark").Any(), "Inactive lightning object retained.");
        });
        run("Ihuykatumu wing first eight cap source endings promote ninth", () =>
        {
            using var f = Prepare(0x4165); for (ulong i = 20; i < 29; ++i) Cast(f, i, 36351);
            Check(f.Draw.Previews.Count == 8 && f.Engine.OutputCasts.Count() == 8, "Wing cap.");
            Finish(f, 20); Check(f.Draw.Previews.Count == 8 && f.Engine.OutputCasts.Any(c => c.Value.Source == 28), "Ninth did not promote.");
            Cast(f, 29, 36351);
            Check(!f.Engine.OutputCasts.Any(c => c.Value.Source == 29) && f.Engine.OutputCasts.Any(c => c.Value.Source == 28),
                "Dictionary reused slot displaced older ninth wing.");
        });
        run("Ihuykatumu concentric packet correction and dead cast retain until actual ending", () =>
        {
            using var f = Prepare(0x4190); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36515);
            var corrected = f.Actors[10].Cast! with { Location = c + Vector3.UnitX * 3 };
            f.Engine.StartCast(10, corrected, f.Now);
            Check(Aoes(f, "shore").Single().Position == corrected.Location, "Packet correction not applied to sequence.");
            f.Actors[10] = f.Actors[10] with { Cast = corrected, Dead = true }; Scan(f);
            Check(Aoes(f, "shore").Single().Shape is CircleTemplate, "Death flag advanced an ongoing cast.");
            Finish(f, 10); Check(Aoes(f, "shore").Single().Shape is DonutTemplate { InnerRadius: 10 }, "Corrected end failed to advance.");
            Cast(f, 11, 36497); f.Actors[11] = f.Actors[11] with { Dead = true }; Scan(f);
            Check(Aoes(f, "bury").Length == 1, "Death flag consumed Bury FIFO.");
            f.Engine.RemoveActor(11, f.Now); f.Actors.Remove(11); f.Render();
            Check(!Aoes(f, "bury").Any(), "Destruction failed to consume ongoing cast.");
        });
        run("Ihuykatumu area buster dynamic target no spread margin own outline and damage", () =>
        {
            using var f = Prepare(0x4165); Cast(f, 10, 36357, 101);
            Check(Aoes(f, "buster").Single().Shape is CircleTemplate { Radius: 6 }
                && f.Engine.PredictedDamage.Single().DamageType == "Tankbuster", "Area buster was scalar or wrong margin.");
            f.PlayerId = 101; Scan(f);
            Check(Aoes(f, "buster").Single(a => a.Display).OutlineOnly && Aoes(f, "buster").Single(a => a.Navigate).Position == f.Actors[100].Position,
                "Buster personal avoid.");
            Finish(f, 10); Check(!Aoes(f, "buster").Any() && !f.Engine.PredictedDamage.Any(), "Buster real finish.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1167); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, Vector3? location = null)
    {
        if (!f.Actors.ContainsKey(source)) f.Actors[source] = new(source, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, location ?? f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(5), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; Scan(f); f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong source)
    {
        f.Actors[source] = f.Actors[source] with { Cast = null }; f.Engine.Finish(source, endedAt: f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Aoes(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("ihuy/" + key)).ToArray();
    private static Vector3 Q(Vector3 p)
    {
        const float grid = 2000f / 65535f;
        return new((MathF.Round(p.X / grid) - .5f) * grid, p.Y, (MathF.Round(p.Z / grid) - .5f) * grid);
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
