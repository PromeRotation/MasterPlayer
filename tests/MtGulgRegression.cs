using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class MtGulgRegression
{
    internal static void Run(Action<string, Action> run)
    {
        foreach (var (name, typeName, field, native) in new (string, string, string, Func<float, IGeometryShape>)[]
        {
            ("Cruelty", "D051ForgivenCruelty.D051ForgivenCruelty", "arena", MtGulgArenas.Cruelty),
            ("Apathy first", "D052ForgivenApathy.D052ForgivenApathy", "arena1", MtGulgArenas.ApathyFirst),
            ("Apathy second", "D052ForgivenApathy.D052ForgivenApathy", "arena2", MtGulgArenas.ApathySecond),
        })
            run($"MtGulg {name} exact source arena both views", () =>
            {
                var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon.D05MtGulg." + typeName)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                CompareArena(native(0), source);
            });
        run("MtGulg small arena source subtraction and six-module ownership", () =>
        {
            BossMod.WPos center = new(-240, 237);
            var source = new BossMod.ArenaBoundsCustom([new BossMod.Polygon(center, 15, 64)],
                [new BossMod.Rectangle(new(-225, 237), .5f, 20), new BossMod.Rectangle(new(-255, 237), .5f, 20)]);
            CompareArena(MtGulgArenas.ObscenitySmall(0), source);
            Check(MtGulgCatalog.Create().Length == 6 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.mt-gulg"), "Six modules or default enable missing.");
            foreach (var d in MtGulgCatalog.Create()) Check(ArrDungeonCatalog.Owns(d.BossOid, 822) && !ArrDungeonCatalog.Owns(d.BossOid, 836), "Ownership leaked.");
            Check(!ArrDungeonCatalog.Owns(0x28EE, 822), "Unmigrated primary module claimed.");
        });
        foreach (var (boss, action, oid, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x28F2, 16813, 0x28F1, new BossMod.AOEShapeRect(40, 4)),
            (0x28F2, 16814, 0x28F2, new BossMod.AOEShapeCone(12, new(MathF.PI / 4))),
            (0x27CA, 16818, 0x27CA, new BossMod.AOEShapeRect(40, 2.5f)),
            (0x27CA, 15616, 0x233C, new BossMod.AOEShapeCone(25, new(MathF.PI / 6))),
            (0x27CA, 17153, 0x233C, new BossMod.AOEShapeCone(25, new(MathF.PI / 6))),
            (0x27CA, 15619, 0x233C, new BossMod.AOEShapeCircle(10)),
            (0x28F0, 16245, 0x233C, new BossMod.AOEShapeCone(60, new(MathF.PI / 6))),
            (0x28F3, 16250, 0x233C, new BossMod.AOEShapeRect(30, 7.5f)),
            (0x28F3, 16248, 0x233C, new BossMod.AOEShapeRect(30, 7.5f)),
            (0x28F3, 16251, 0x2947, new BossMod.AOEShapeRect(40, 2)),
            (0x27CE, 15638, 0x27CE, new BossMod.AOEShapeCircle(8)),
            (0x27CE, 15640, 0x27CE, new BossMod.AOEShapeCircle(8)),
            (0x27CE, 15649, 0x27CF, new BossMod.AOEShapeCircle(8)),
            (0x27CE, 15641, 0x233C, new BossMod.AOEShapeDonut(10, 16)),
            (0x27CE, 18025, 0x233C, new BossMod.AOEShapeDonut(18, 32)),
            (0x27CE, 15642, 0x27CE, new BossMod.AOEShapeRect(40, 2.5f)),
            (0x27CE, 15643, 0x233C, new BossMod.AOEShapeRect(40, 2.5f)),
            (0x27CE, 15648, 0x27CF, new BossMod.AOEShapeRect(40, 2.5f)),
        })
            run($"MtGulg action {action} original shape packet direction double views and cancellation", () =>
            {
                using var f = Prepare(boss); var id = oid == boss ? 1UL : 10UL;
                if (id == 10) { f.Actors[10] = new(10, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id];
                Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1 && f.Safe.NavigationOnly.Count == 1, "Missing or duplicate outputs.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancelled cast leaked.");
            });
        foreach (var (action, count) in new (uint, int)[] { (16989, 5), (16987, 2), (15622, 2), (16988, 1) })
            run($"MtGulg Exegesis {action} fixed cells cast plus point four effect latch and stale cast", () =>
            {
                using var f = Prepare(0x27CC); Cast(f, 1, action, 5, .8f);
                Check(f.Draw.Previews.Count == count && f.Snapshot.DangerAreas.Count == count, "Nine-cell pattern count differs.");
                foreach (var aoe in f.Engine.AmbientAoes())
                {
                    Check(Math.Abs((aoe.Activation!.Value - f.Start.AddSeconds(5.7)).TotalMilliseconds) < .001, "Exegesis activation differs.");
                    Compare(aoe.Shape, aoe.Position, aoe.Rotation, action == 16988
                        ? new BossMod.AOEShapeCross(15, 5) : new BossMod.AOEShapeRect(5, 5, 5));
                }
                f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == count, "Natural finish erased pending cells.");
                f.Engine.Effect(20, 15623, 1, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Consumed cells revived.");
                f.Now = f.Start.AddSeconds(10); Cast(f, 1, action, 5); f.Engine.Finish(1); f.Render();
                Check(f.Draw.Previews.Count == 0, "Cancel retained cells.");
            });
        foreach (var pattern in new[] { 0, 1, 2 })
            run($"MtGulg GoldChaser pattern {pattern} six effects original clock four previews and exact risky promotion", () =>
            {
                using var f = Prepare(0x27CE);
                f.Now = f.Start.AddSeconds(2);
                f.Engine.Effect(1, 15652, 1, f.Now, occurredAt: f.Start.AddSeconds(1));
                Vector3[] rings = pattern switch
                {
                    1 => [new(-227.5f, 0, 253), new(-232.5f, 0, 251.5f)],
                    2 => [new(-247.5f, 0, 251.5f), new(-252.5f, 0, 253)],
                    _ => [new(-250, 0, 253), new(-230, 0, 253)],
                };
                for (var i = 0; i < 6; ++i)
                {
                    var p = i < 2 ? rings[i] : new Vector3(-250 + i * 3, 0, 252 - i);
                    f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x1EAB62, p); Scan(f);
                    Check(f.Draw.Previews.Count == Math.Min(4, (i + 1) / 2 * 2), "Incomplete pair guessed or four-preview cap lost.");
                }
                double[] delays = pattern == 0 ? [7.1, 7.1, 8.1, 8.1, 11.1, 11.1] : [7.1, 7.6, 8.1, 8.6, 9.1, 9.1];
                for (var i = 0; i < 6; ++i)
                {
                    var aoes = f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("gulg/gold/")).ToArray();
                    Check(aoes.Length == Math.Min(4, 6 - i), "FIFO did not promote later line.");
                    Check(aoes[0].Activation == f.Start.AddSeconds(1 + delays[i]), "Ringsmith clock lost.");
                    Check(aoes.Count(a => a.Navigate) == (i < 5 && delays[i] == delays[i + 1] ? 2 : 1), "Future preview became current danger.");
                    Compare(aoes[0].Shape, aoes[0].Position, aoes[0].Rotation, new BossMod.AOEShapeRect(40, 2.5f));
                    f.Engine.Effect(50, 15655, (uint)i, f.Now); f.Engine.Effect(50, 15655, (uint)i, f.Now); Scan(f);
                }
                Check(f.Draw.Previews.Count == 0, "Six effects did not clear or stale rings revived.");
                f.Now = f.Start.AddSeconds(20); f.Engine.Effect(1, 15652, 2, f.Now);
                f.Actors[30] = new(30, 0x1EAB62, rings[0]); f.Actors[31] = new(31, 0x1EAB62, rings[1]); Scan(f);
                Check(f.Draw.Previews.Count == 2, "Next six-line wave could not start.");
                f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0, "Reset retained gold lines.");
            });
        run("MtGulg Whimsy birth clocks donut all-clear tower FIFO real solver and viewer occupancy", () =>
        {
            using var f = Prepare(0x27CC); var center = f.Actors[1].Position;
            f.Now = f.Start.AddSeconds(2);
            f.Actors[10] = new(10, 0x27CD, center); f.Actors[11] = new(11, 0x27CD, center + Vector3.UnitZ * 10);
            f.Actors[20] = new(20, 0x1EAACF, center - Vector3.UnitX * 9);
            f.Actors[21] = new(21, 0x1EAACF, center + Vector3.UnitX * 9); Scan(f);
            f.Engine.ObjectCreated(10, f.Start); f.Engine.ObjectCreated(20, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "gulg/donut/10").Activation == f.Start.AddSeconds(10.6), "Donut birth not corrected.");
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "gulg/tower/20").Activation == f.Start.AddSeconds(7.6), "Tower birth not corrected.");
            Cast(f, 10, 15630, 6); f.Engine.Finish(10, completed: true); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id.StartsWith("gulg/donut")), "Natural Contrition finish did not clear whole group or rearmed.");
            f.Actors[101] = f.Actors[101] with { Position = f.Actors[20].Position }; Scan(f);
            Solve(f, p => Vector3.Distance(p, f.Actors[21].Position) < 5);
            f.PlayerId = 101; Scan(f); Check(!f.Engine.AmbientAoes().Any(a => a.Id == "gulg/tower/avoid/20"), "Viewer switched tower occupancy wrong.");
            f.Engine.Effect(50, 15633, 1, f.Now); f.Engine.Effect(50, 15633, 1, f.Now); Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "gulg/tower/20") && f.Engine.AmbientAoes().Any(a => a.Id == "gulg/tower/21"), "Tower FIFO or duplicate handling differs.");
            f.Engine.Effect(50, 15632, 2, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Failed tower not removed.");
        });
        run("MtGulg Obscenity moving orb capsule proximity animation stale snapshot and ring distance", () =>
        {
            using var f = Prepare(0x27CE); var origin = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x27D0, origin);
            f.Actors[20] = new(20, 0x1EAB62, origin + Vector3.UnitZ * 12); Scan(f);
            var aoe = f.Engine.AmbientAoes().Single();
            Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeCapsule(3, 12));
            f.Actors[10] = f.Actors[10] with { Position = origin + Vector3.UnitX * 8 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single().Shape is CircleTemplate, "Orb did not follow or matched sideways ring.");
            f.Engine.Animation(10, 0x00040008, f.Now); Scan(f); Check(f.Draw.Previews.Count == 0, "Consumed orb revived.");
            f.Actors[11] = new(11, 0x27D0, origin, Rotation: MathF.PI); Scan(f);
            Check(f.Engine.AmbientAoes().Single().Shape is CircleTemplate, "Ring behind orb produced capsule.");
            f.Engine.RemoveActor(11); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed orb leaked.");
        });
        run("MtGulg Penance cast delay persistent donut shrink sprint and restore both views true solver", () =>
        {
            using var f = Prepare(0x27CE); var center = f.Actors[1].Position;
            f.Actors[100] = f.Actors[100] with { Position = center + Vector3.UnitZ * 18 }; Scan(f);
            Cast(f, 1, 15644, 3);
            Check(Math.Abs((f.Engine.AmbientAoes().Single().Activation!.Value - f.Start.AddSeconds(4)).TotalMilliseconds) < .001, "Penance cast plus .7 differs.");
            Solve(f, p => Vector3.Distance(p, center) < 14.5f);
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 1, "Natural Penance finish erased donut.");
            f.Actors[50] = new(50, 0, center); Scan(f);
            f.Engine.Animation(50, 0x00010002, f.Now); f.Render();
            Check(f.Engine.WantsGulgSprint && f.Draw.Previews.Count == 0 && !f.Engine.Arena!.Contains(center + Vector3.UnitZ * 17), "Shrink/sprint/donut handoff missing.");
            var world = HostDrawingGeometry.Convert(f.Engine.Arena!); Check(!world.Contains(center + Vector3.UnitX * 14.8f), "Small world arena lost side subtraction.");
            f.Actors[50] = f.Actors[50] with { EventAnimation = 0x00010002 }; Scan(f);
            f.Actors[51] = new(51, 0, center); Scan(f);
            f.Engine.Animation(51, 0x00040008, f.Now.AddSeconds(1)); Scan(f);
            Check(!f.Engine.WantsGulgSprint && f.Engine.Arena!.Contains(center + Vector3.UnitZ * 18), "Restore lost or stale animation reshrank.");
        });
        foreach (var (boss, visual, effect, delay, count) in new (uint, uint, uint, double, int)[]
        {
            (0x27CA, 15611, 15611, 0, 1), (0x27CC, 15625, 15626, .5, 1),
            (0x27CC, 15627, 15628, .5, 2), (0x27CE, 15634, 15635, .8, 1), (0x27CE, 15636, 15637, .8, 2),
        })
            run($"MtGulg delayed damage {visual} target clock natural finish actual effect and cancel", () =>
            {
                using var f = Prepare(boss); Cast(f, 1, visual, 4, target: 100);
                Check(f.Engine.PredictedDamage.Count() == count && f.Engine.PredictedDamage.All(d => d.Activation == f.Start.AddSeconds(4.3 + delay)), "Prediction duplicate or delay differs.");
                f.Engine.Finish(1, completed: true); f.Render(); Check(f.Engine.PredictedDamage.Count() == count, "Delayed damage erased by natural finish.");
                f.Engine.Effect(visual == effect ? 1UL : 20UL, effect, 1, f.Now); Scan(f); Check(!f.Engine.PredictedDamage.Any(), "Real effect did not remove delayed damage.");
                f.Now = f.Start.AddSeconds(10); Cast(f, 1, visual, 4, target: 100); f.Engine.Finish(1);
                Check(!f.Engine.PredictedDamage.Any(), "Cancelled damage retained.");
            });
        run("MtGulg Prejudice companions persist no primary and hand off to Apathy original room selector", () =>
        {
            using var f = Prepare(0x28F2); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x28F1, center, InCombat: true); Scan(f); Cast(f, 10, 16813, 5);
            f.Engine.RemoveActor(1); f.Actors.Remove(1); Scan(f);
            Check(f.Engine.Active?.BossOid == 0x28F2 && f.Engine.PrimaryId == 10 && f.Draw.Previews.Count == 1, "Companion encounter reset.");
            f.Actors[10] = f.Actors[10] with { InCombat = false }; Scan(f);
            Check(f.Engine.Active != null, "Source CheckReset=false lost.");
            f.Actors[20] = new(20, 0x28F0, new(0, 0, -200), InCombat: true); Scan(f);
            Check(f.Engine.Active?.BossOid == 0x28F0 && f.Engine.Arena!.Position == MtGulgArenas.ApathyFirst(0).Position && f.Draw.Previews.Count == 0, "Apathy handoff retained trash cast.");
            f.Engine.ResetFight(); f.Actors[20] = f.Actors[20] with { Position = new(-190, 0, -100) }; Scan(f);
            Check(f.Engine.Arena!.Position == MtGulgArenas.ApathySecond(0).Position, "Second room selector differs.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 822); f.Engine.ResetFight();
        var p = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, p, InCombat: true);
        f.Actors[100] = new(100, 0, p, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, p + Vector3.UnitZ * 8, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, double duration, float rotation = 0, ulong target = 0)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void CompareArena(IGeometryShape shape, BossMod.ArenaBoundsCustom source)
    {
        var world = HostDrawingGeometry.Convert(shape);
        for (var x = -35f; x <= 35; x += .21f)
            for (var z = -35f; z <= 35; z += .21f)
            {
                var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z); var expected = source.Contains(new(x, z));
                Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source arena or world conversion differs.");
            }
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -62f; x <= 62; x += .73f)
            for (var z = -62f; z <= 62; z += .73f)
            {
                var p = origin + new Vector3(x, 0, z);
                var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p) == expected, "Source analytic shape differs.");
                if (template is DungeonCapsuleTemplate && shape.Contains(p, .025f) != shape.Contains(p, -.025f)) continue;
                Check(world.Contains(p) == expected, "World shape differs.");
            }
    }
    private static void Solve(Fight f, Func<Vector3, bool> accept)
    {
        var snapshot = f.Snapshot;
        var solution = new SafeZoneSolver().Solve(snapshot, new(f.Actors[f.PlayerId].Position, 0, 6, false, false, 0, true, snapshot.Version),
            new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0 && accept(path[^1].Position),
            $"Real solver failed: {solution.Status}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
