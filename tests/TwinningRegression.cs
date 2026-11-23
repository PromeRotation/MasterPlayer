using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class TwinningRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (typeName, native) in new (string, Func<float, IGeometryShape>)[]
            { ("D072Mithridates", TwinningArenas.Mithridates), ("D073TheTycoon", TwinningArenas.Tycoon) })
            run($"Twinning {typeName} exact resolved source arena both views and ownership", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D07Twinning.{typeName}.{typeName}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var shape = native(0); var world = HostDrawingGeometry.Convert(shape);
                for (var x = -22f; x < 22; x += .17f)
                    for (var z = -22f; z < 22; z += .17f)
                    {
                        var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z); var expected = source.Contains(new(x, z));
                        Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source arena or world conversion differs.");
                    }
                Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.twinning") && TwinningCatalog.Create().Length == 3, "Discovery/default enable missing.");
                foreach (var d in TwinningCatalog.Create()) Check(ArrDungeonCatalog.Owns(d.BossOid, 840) && !ArrDungeonCatalog.Owns(d.BossOid, 838), "Ownership escaped.");
            });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x27D1, 0x27D1, 15717, new BossMod.AOEShapeCone(12, new(MathF.PI / 3))),
            (0x2803, 0x233C, 15852, new BossMod.AOEShapeRect(50, 4)),
            (0x2805, 0x233C, 15866, new BossMod.AOEShapeCircle(8)),
        })
            run($"Twinning cast {action} source shape locked heading both views and cancel", () =>
            {
                using var f = Prepare(boss); var id = boss == oid ? 1UL : 10UL;
                if (id == 10) { f.Actors[10] = new(10, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Missing or duplicate cast output.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        run("Twinning Alpha four line icons preserve arrival FIFO source clocks heading cages and repeated effect", () =>
        {
            using var f = Prepare(0x27D1); var center = f.Actors[1].Position;
            for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0,
                center + new Vector3(i < 2 ? -8 : 8, 0, i % 2 == 0 ? -8 : 8), PartySlot: i, Hostile: false);
            f.Actors[20] = new(20, 0x27D3, center + Vector3.UnitX * 12, HitboxRadius: .5f); Scan(f);
            ulong[] targets = [102, 100, 103, 101];
            for (var i = 0; i < 4; ++i) f.Engine.Icon(targets[i], (uint)(53 - i), f.Start.AddSeconds(.1 * i));
            f.Render();
            uint[] effects = [15713, 15718, 15719, 15720];
            for (var i = 0; i < 4; ++i)
            {
                var aoes = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("twinning/impact/") && a.Id.Count(c => c == '/') == 2).ToArray();
                Check(aoes.Length == 4 - i && aoes[0].Activation == f.Start.AddSeconds(.1 * i).AddSeconds(7.2), "Icon sorted numerically or clock lost.");
                var delta = f.Actors[targets[i]].Position - center;
                Check(Math.Abs(aoes[0].Rotation - MathF.Atan2(delta.X, delta.Z)) < .00001f, "Line did not track FIFO target.");
                Compare(aoes[0].Shape, center, aoes[0].Rotation, new BossMod.AOEShapeRect(50, 3));
                f.Engine.Effect(1, effects[i], (uint)i, f.Now); f.Engine.Effect(1, effects[i], (uint)i, f.Now); f.Render();
            }
            Check(!f.Engine.AmbientAoes().Any(), "Four effects retained baits/cage drawings.");
        });
        run("Twinning own line steers away from cage and party with true solver and viewer switch", () =>
        {
            using var f = Prepare(0x27D1); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitX * 9 };
            f.Actors[101] = f.Actors[101] with { Position = center + Vector3.UnitZ * 10 };
            f.Actors[20] = new(20, 0x27D3, center + Vector3.UnitX * 14); Scan(f);
            f.Engine.Icon(100, 50, f.Start); f.Render();
            var cage = f.Engine.AmbientAoes().Single(a => a.Id == "twinning/impact/0/avoid/20");
            Solve(f, p => !cage.Shape.Resolve(cage.Position, cage.Rotation, null).Contains(p));
            f.PlayerId = 101; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.Contains("/avoid/20")) && f.Engine.AmbientAoes().Single(a => a.Id == "twinning/impact/0").Navigate,
                "Unmarked viewer retained baiter cage constraint.");
        });
        run("Twinning Pounce four real effects original clock cage radius eleven party spread and removal", () =>
        {
            using var f = Prepare(0x27D1); var center = f.Actors[1].Position;
            f.Actors[20] = new(20, 0x27D3, center + Vector3.UnitX * 12); Scan(f);
            for (var i = 0; i < 4; ++i)
            {
                var target = (ulong)(100 + i);
                if (i >= 2) f.Actors[target] = new(target, 0, center + Vector3.UnitX * (i * 3), PartySlot: i, Hostile: false);
                Scan(f); f.Engine.Icon(target, 90, f.Start.AddSeconds(.2 * i));
            }
            f.Render();
            var constraint = f.Engine.AmbientAoes().Single(a => a.Id == "twinning/pounce/0/cage/20");
            Check(constraint.Shape is CircleTemplate { Radius: 11 } && constraint.Activation == f.Start.AddSeconds(4.6), "Pounce cage/clock differs.");
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "twinning/pounce/0/separate/101"), "Pounce party protection missing.");
            uint[] effects = [15711, 15721, 15722, 16842];
            for (var i = 0; i < 4; ++i)
            {
                f.Engine.Effect(1, effects[i], (uint)i, f.Now); f.Engine.Effect(1, effects[i], (uint)i, f.Now); f.Render();
                Check(f.Engine.AmbientAoes().Count(a => a.Id.StartsWith("twinning/pounce/") && a.Id.Count(c => c == '/') == 2) == 3 - i, "Pounce FIFO/dedup differs.");
            }
            f.Engine.Icon(101, 90, f.Start.AddSeconds(10)); f.Engine.RemoveActor(101); f.Render();
            Check(!f.Engine.AmbientAoes().Any(), "Removed Pounce target leaked.");
        });
        run("Twinning ChargeEradicated forces four stack real solver persistent original R8 pool state seven", () =>
        {
            using var f = Prepare(0x27D1); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center - Vector3.UnitX * 10 };
            f.Actors[101] = f.Actors[101] with { Position = center + Vector3.UnitX * 9 }; Scan(f);
            Cast(f, 1, 15715, 5, target: 101); Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) < 8);
            f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Actors[20] = new(20, 0x1E88F5, center); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.AmbientAoes().Single().Shape is CircleTemplate { Radius: 8 }, "Persistent pool incorrect.");
            f.Actors[20] = f.Actors[20] with { EventState = 7 }; Scan(f); Check(f.Draw.Previews.Count == 0, "Expired pool leaked.");
        });
        run("Twinning Mithridates eight shock birth clocks all clear and no stale VFX revival", () =>
        {
            using var f = Prepare(0x2803); var center = f.Actors[1].Position; f.Now = f.Start.AddSeconds(2);
            for (var i = 0; i < 8; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x2804, center + Vector3.UnitX * i);
            Scan(f); f.Engine.ObjectCreated(10, f.Start); f.Render();
            Check(f.Draw.Previews.Count == 8 && f.Engine.AmbientAoes().Single(a => a.Id == "twinning/shock/10").Activation == f.Start.AddSeconds(7.7), "Shock wave or birth correction differs.");
            f.Engine.Effect(50, 15857, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Shock all-clear revived.");
            f.Now = f.Start.AddSeconds(20); f.Actors[30] = new(30, 0x2804, center); Scan(f);
            Check(f.Draw.Previews.Count == 1, "New shock wave did not start.");
        });
        foreach (var effect in new uint[] { 15863, 15859 })
            run($"Twinning Tycoon laser {effect} short long birth clocks original locked direction proximity all pair cleanup", () =>
            {
                using var f = Prepare(0x2805); var center = f.Actors[1].Position; f.Now = f.Start.AddSeconds(1);
                for (var i = 0; i < 3; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x1EAC39,
                    center + Vector3.UnitZ * (i * 5), Rotation: .7f);
                Scan(f); f.Engine.ObjectCreated(10, f.Start); f.Render();
                Check(f.Draw.Previews.Count == 6 && f.Snapshot.DangerAreas.Count == 6, "Laser pair forecasts missing.");
                var shortAoe = f.Engine.AmbientAoes().Single(a => a.Id == "twinning/laser/10/short");
                var longAoe = f.Engine.AmbientAoes().Single(a => a.Id == "twinning/laser/10/long");
                Check(shortAoe.Activation == f.Start.AddSeconds(6.5) && longAoe.Activation == f.Start.AddSeconds(14.3), "Laser birth clock differs.");
                Compare(shortAoe.Shape, shortAoe.Position, shortAoe.Rotation, new BossMod.AOEShapeRect(10, 4));
                Compare(longAoe.Shape, longAoe.Position, longAoe.Rotation, new BossMod.AOEShapeRect(40, 4));
                f.Actors[10] = f.Actors[10] with { Position = center + Vector3.UnitX * 3, Rotation = 0 }; Scan(f);
                Check(f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/10/short")) == shortAoe, "Birth snapshot moved with VFX.");
                f.Engine.RemoveActor(10); f.Actors.Remove(10); Scan(f); Check(f.Draw.Previews.Count == 6, "Destroyed VFX erased pending helper effect.");
                f.Engine.Effect(50, effect, 1, f.Now, location: center + Vector3.UnitX * .8f); Scan(f);
                Check(f.Draw.Previews.Count == 4 && !f.Engine.AmbientAoes().Any(a => a.Id.Contains("/10/")), "Proximity effect did not clear both or erased neighboring origin.");
                f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked lasers.");
            });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x2803, 15855, 5), (0x2805, 17306, 5) })
            run($"Twinning spread {action} viewer switch radius and cancel", () =>
            {
                using var f = Prepare(boss); f.Actors[10] = new(10, 0x233C, f.Actors[1].Position); Scan(f);
                Cast(f, 10, action, 6, target: 100);
                Check(f.Engine.AmbientAoes().Single(a => a.Id == "party/separate/10/101").Shape is CircleTemplate { Radius: 5 }, "Marked player spread missing.");
                f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Single(a => a.Id == "party/avoid/10").Shape is CircleTemplate c && c.Radius == radius, "Viewer spread differs.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Spread cancel leaked.");
            });
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 840); f.Engine.ResetFight(); var p = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, p, InCombat: true); f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -55f; x <= 55; x += .73f)
            for (var z = -55f; z <= 55; z += .73f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source or world shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position), $"Real solver failed: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
