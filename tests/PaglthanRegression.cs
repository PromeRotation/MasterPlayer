using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class PaglthanRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Paglthan source exact arenas radar world ownership", () =>
        {
            foreach (var (oid, name) in new[] { (0x3169u, "D131Amhuluk"), (0x316Au, "D133LunarBahamut") })
            {
                using var f = Prepare(oid);
                var type = typeof(BossMod.BossModule).Assembly.GetType($"BossMod.Shadowbringers.Dungeon.D13Paglthan.{name}.{name}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                var native = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(native);
                for (var x = -22.137f; x < 22; x += .23f)
                    for (var z = -22.231f; z < 22; z += .23f)
                    {
                        var p = native.Position + new Vector3(x, 0, z); var expected = source.Contains(new(x, z));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.paglthan")
                && new uint[] { 0x3169, 0x32FE, 0x316A }.All(o => ArrDungeonCatalog.Owns(o, 938) && !ArrDungeonCatalog.Owns(o, 933)), "Ownership missing or leaked.");
        });
        foreach (var (boss, oid, action, source) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x32FE, 0x233C, 23701, new BossMod.AOEShapeCircle(12)),
            (0x32FE, 0x31B0, 23708, new BossMod.AOEShapeCircle(6)),
            (0x32FE, 0x31AE, 23705, new BossMod.AOEShapeRect(40, 3.5f)),
            (0x316A, 0x316B, 24667, new BossMod.AOEShapeCircle(2)),
            (0x316A, 0x316B, 23368, new BossMod.AOEShapeCircle(9)),
            (0x316A, 0x316A, 23385, new BossMod.AOEShapeCone(30, new(MathF.PI / 4))),
            (0x316A, 0x233C, 23374, new BossMod.AOEShapeCircle(6)),
            (0x316A, 0x316A, 23378, new BossMod.AOEShapeRect(41, 6)),
            (0x316A, 0x233C, 23370, new BossMod.AOEShapeCircle(11)),
            (0x316A, 0x233C, 23371, new BossMod.AOEShapeCircle(6)),
        })
            run($"Paglthan cast {action} exact source geometry both views and cancel", () =>
            {
                using var f = Prepare(boss); var id = boss == oid ? 1UL : 10UL;
                if (id != 1) { f.Actors[id] = new(id, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 4, .7f); var cast = f.Engine.Casts[id]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, source);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Ordinary cast missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled ordinary cast leaked.");
            });
        run("Paglthan lightning common activation transfer status free rods real solver perspectives and true effect", () =>
        {
            using var f = Prepare(0x3169); var c = f.Actors[1].Position;
            f.Actors[101] = f.Actors[101] with { Position = c - Vector3.UnitX * 13 };
            f.Actors[20] = new(20, 0x31B6, c + Vector3.UnitX * 12); f.Actors[21] = new(21, 0x31B6, c + Vector3.UnitZ * 12); Scan(f);
            f.Engine.StatusChanged(100, 2574, true, f.Start); f.Engine.StatusChanged(101, 2574, true, f.Start.AddSeconds(.2)); f.Render();
            Check(f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("paglthan/lightning/")).All(a => a.Activation == f.Start.AddSeconds(10.8)), "Lightning did not share first activation.");
            Solve(f, p => Math.Min(Vector3.Distance(p, f.Actors[20].Position), Vector3.Distance(p, f.Actors[21].Position)) <= 4);
            f.Engine.StatusChanged(100, 2574, false, f.Start.AddSeconds(.3)); f.Engine.StatusChanged(20, 2574, true, f.Start.AddSeconds(.3)); f.Render();
            Check(f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 2
                && f.Engine.AmbientAoes().All(a => !a.OutlineOnly), "Non-bait perspective lost filled lightning circles.");
            f.PlayerId = 101; Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id == "paglthan/rod/20")
                && f.Engine.AmbientAoes().Any(a => a.Id == "paglthan/rod/21"), "Occupied rod offered for transfer.");
            f.Engine.Effect(10, 23628, 1, f.Now); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Lightning real effect leaked.");
            f.Engine.StatusChanged(101, 2574, true, f.Start.AddSeconds(12)); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "paglthan/rod/20"), "Hit cleared rod occupancy before status loss.");
            f.Engine.StatusChanged(20, 2574, false, f.Start.AddSeconds(12)); f.Render();
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "paglthan/rod/20"), "Status loss did not restore free rod.");
        });
        run("Paglthan shock birth first seventy two then thirty effects duplicates no resurrect", () =>
        {
            using var f = Prepare(0x3169); var c = f.Actors[1].Position;
            for (var wave = 0; wave < 2; ++wave)
            {
                var id = (ulong)(20 + wave); f.Engine.ObjectCreated(id, f.Now.AddSeconds(-1));
                f.Actors[id] = new(id, wave == 0 ? 0x31A2u : 0x31A3u, c + Vector3.UnitZ * 8); Scan(f);
                var aoe = f.Engine.AmbientAoes().Single();
                Check(Math.Abs((aoe.Activation!.Value - f.Now.AddSeconds(2.7)).TotalMilliseconds) < .001, "Shock birth clock lost.");
                Compare(aoe.Shape, aoe.Position, 0, new BossMod.AOEShapeCircle(wave == 0 ? 5 : 10));
                var count = wave == 0 ? 72 : 30;
                for (var i = 0; i < count; ++i)
                {
                    f.Engine.Effect(id, 23634, (uint)(wave * 100 + i), f.Now); f.Engine.Effect(id, 23634, (uint)(wave * 100 + i), f.Now);
                    if (i < count - 1) Check(f.Engine.AmbientAoes().Any(), "Shock wave cleared early.");
                }
                f.Render(); Scan(f); Check(!f.Engine.AmbientAoes().Any(), "Shock final count leaked or resurrected.");
                f.Now = f.Now.AddSeconds(10);
            }
        });
        run("Paglthan blaster tail future drawing only until first real effect and original clock", () =>
        {
            using var f = Prepare(0x3169); Cast(f, 1, 24773, 4, .7f); var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 2 && aoes[0].Navigate && !aoes[1].Navigate && f.Draw.Previews.Count == 2 && f.Snapshot.DangerAreas.Count == 1, "Tail should preview without navigation.");
            Compare(aoes[0].Shape, aoes[0].Position, aoes[0].Rotation, new BossMod.AOEShapeCone(26, new(MathF.PI / 3)));
            Compare(aoes[1].Shape, aoes[1].Position, aoes[1].Rotation, new BossMod.AOEShapeCone(25, new(MathF.PI / 6)));
            Check(Math.Abs((aoes[1].Activation!.Value - f.Start.AddSeconds(6.9)).TotalMilliseconds) < .001, "Tail delay differs.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 2, "Cast finish must retain predicted pair.");
            f.Engine.Effect(1, 24773, 1, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Single().Navigate && f.Snapshot.DangerAreas.Count == 1, "Tail did not become current.");
            f.Engine.Effect(1, 23631, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Tail hit leaked.");
        });
        run("Paglthan fortress hidden primary pulls on predator and persists through core stage", () =>
        {
            using var f = Prepare(0x32FE); Check(f.Engine.Active?.BossOid == 0x32FE && f.Engine.PrimaryId == 1, "Predator did not activate hidden marker.");
            f.Actors[20] = f.Actors[20] with { InCombat = false }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x32FE, "Fortress reset when first predator combat ended.");
            f.Engine.RemoveActor(1); f.Render(); Check(f.Engine.Active == null, "Removed primary did not end fortress.");
        });
        run("Paglthan cannon original map clock all three lanes and platform map clears wave", () =>
        {
            using var f = Prepare(0x32FE);
            for (uint i = 8; i <= 10; ++i) f.Engine.MapEffect(i, 0x00200010, f.Start.AddSeconds(-1));
            f.Render(); Check(f.Draw.Previews.Count == 3, "Cannon lanes missing.");
            foreach (var a in f.Engine.AmbientAoes())
            {
                Compare(a.Shape, a.Position, a.Rotation, new BossMod.AOEShapeRect(60, 5));
                Check(a.Activation == f.Start.AddSeconds(11.1), "Cannon clock lost.");
            }
            f.Engine.MapEffect(0x0D, 0x00020001); f.Render();
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("paglthan/cannon/")), "Platform map did not clear cannons.");
        });
        run("Paglthan moving missile current circle future capsule real clock hit and object lifetime", () =>
        {
            using var f = Prepare(0x32FE); var c = f.Actors[1].Position;
            f.Actors[30] = new(30, 0x31B2, c, Rotation: .7f); Scan(f); var future = f.Engine.AmbientAoes().Single(a => a.Id.EndsWith("/future"));
            Compare(future.Shape, future.Position, future.Rotation, new BossMod.AOEShapeCapsule(1, 10));
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 2 && future.Activation == f.Start.AddSeconds(1.1), "Missile windows differ.");
            f.Now = f.Start.AddSeconds(.5); f.Actors[30] = f.Actors[30] with { Position = c + Vector3.UnitX * 3, Rotation = .9f }; Scan(f);
            Check(f.Engine.AmbientAoes().All(a => a.Position == f.Actors[30].Position), "Missile did not track position.");
            f.Engine.Effect(30, 23704, 1, f.Now); f.Render(); Scan(f); Check(f.Draw.Previews.Count == 0, "Missile effect resurrected from snapshot.");
            f.Actors[31] = new(31, 0x31B2, c); Scan(f); f.Engine.RemoveActor(31); f.Render(); Check(f.Draw.Previews.Count == 0, "Destroyed missile leaked.");
        });
        run("Paglthan platform actual goal solver core perspective switch map release", () =>
        {
            using var f = Prepare(0x32FE); f.Engine.MapEffect(0x0D, 0x00020001); f.Render();
            Check(f.Snapshot.GoalAreas.Count == 1, "Source infinite activation did not become actionable MP goal.");
            Solve(f, p => Vector3.Distance(p, new(-175, 0, 30)) <= 2);
            f.Actors[100] = f.Actors[100] with { Position = new(-175, 0, 8.5f) }; Scan(f);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0 && f.Engine.Arena!.Contains(new(-169, 0, 14), 0)
                && !f.Engine.Arena.Contains(new(-167, 0, 8.5f), 0), "Core arena not switched.");
            f.PlayerId = 101; Scan(f); Check(f.Snapshot.GoalAreas.Count == 1 && f.Engine.Arena!.Position.Z == 43, "Perspective did not return to ground.");
            f.Engine.MapEffect(0x0D, 0x00080004); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.GoalAreas.Count == 0, "Platform map release leaked.");
        });
        run("Paglthan ballistic north south cone union real solver immunity still preserves custom navigation", () =>
        {
            using var f = Prepare(0x32FE); var c = f.Actors[1].Position; f.Actors[10] = new(10, 0x233C, c); Scan(f);
            Cast(f, 10, 23703, 5); var a = f.Engine.AmbientAoes().Single(a => a.Id == "paglthan/ballistic/10");
            var danger = a.Shape.Resolve(c, 0, null);
            Check(!danger.Contains(c + Vector3.UnitZ * 10, 0) && !danger.Contains(c - Vector3.UnitZ * 10, 0)
                && danger.Contains(c + Vector3.UnitX * 10, 0), "Ballistic union directions wrong.");
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 5, Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Start.AddSeconds(10) } }; Scan(f);
            Check(f.Engine.AmbientAoes().Count() == 1, "Immunity should suppress landing only.");
            Solve(f, p => !danger.Contains(p, 0));
            f.Engine.Finish(10); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Cancelled ballistic leaked.");
        });
        foreach (var own in new[] { false, true })
            run($"Paglthan four hit akh morn mandatory stack own {own} persists finish duplicates and final", () =>
            {
                using var f = Prepare(0x316A); var c = f.Actors[1].Position;
                f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitX * 12 }; Scan(f); Cast(f, 1, 23381, 5, target: own ? 100UL : 101UL);
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 2 : 4));
                f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "First cast finish cleared multihit stack.");
                for (var i = 0; i < 4; ++i)
                {
                    var action = i == 0 ? 23381u : 23382u; f.Engine.Effect(1, action, (uint)i, f.Now); f.Engine.Effect(1, action, (uint)i, f.Now);
                    f.Render(); Check(f.Draw.Previews.Count == (i == 3 ? 0 : 1), "AkhMorn effect count wrong.");
                }
            });
        foreach (var count in new[] { 1, 2 })
            run($"Paglthan kan rhai {count} centers twenty hits each icon bait handoff exact cross both views", () =>
            {
                using var f = Prepare(0x316A); var c = f.Actors[1].Position;
                f.Engine.Icon(100, 260, f.Start.AddSeconds(-1)); f.Render(); var bait = f.Engine.AmbientAoes().First();
                Compare(bait.Shape, bait.Position, bait.Rotation, new BossMod.AOEShapeCross(15, 3));
                Check(bait.Activation == f.Start.AddSeconds(4.6), "Cross icon clock lost.");
                Solve(f, p => !new DungeonCrossTemplate(15, 3).Resolve(f.Actors[101].Position, bait.Rotation, null).Contains(p, 0));
                for (var i = 0; i < count; ++i)
                {
                    var id = (ulong)(20 + i); f.Actors[id] = new(id, 0x233C, c + Vector3.UnitX * (i * 10)); Scan(f);
                    f.Engine.Effect(id, 23377, (uint)i, f.Now);
                }
                f.Render(); Check(f.Draw.Previews.Count == count && !f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("paglthan/crossbait/")), "Cross bait handoff failed.");
                for (var i = 0; i < count * 20; ++i)
                {
                    f.Engine.Effect(20, 23376, (uint)i, f.Now); f.Engine.Effect(20, 23376, (uint)i, f.Now);
                    if (i < count * 20 - 1) Check(f.Engine.AmbientAoes().Count() == count, "Crosses cleared early.");
                }
                f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Final cross hit leaked.");
            });
        run("Paglthan flatten keeps natural visual finish until matching target effect", () =>
        {
            using var f = Prepare(0x316A); Cast(f, 1, 23384, 5, target: 100); f.Engine.Finish(1, completed: true);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Flatten finish lost pending target.");
            f.Engine.Effect(1, 23384, 1, f.Now, target: 101); Check(f.Engine.PredictedDamage.Any(), "Wrong target consumed Flatten.");
            f.Engine.Effect(1, 23384, 2, f.Now, target: 100); Check(!f.Engine.PredictedDamage.Any(), "Flatten real hit leaked.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 938); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: boss != 0x32FE, Targetable: boss != 0x32FE);
        if (boss == 0x32FE) f.Actors[20] = new(20, 0x31AF, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double seconds, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -62.137f; x < 62; x += .71f)
            for (var z = -62.231f; z < 62; z += .71f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot; var player = f.Actors[f.PlayerId].Position;
        var solution = new SafeZoneSolver().Solve(snapshot, new(player, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && accept(path.Count > 0 ? path[^1].Position : player),
            $"Real solver failed: {solution.Status}, from {player} to {(path.Count > 0 ? path[^1].Position : player)}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
