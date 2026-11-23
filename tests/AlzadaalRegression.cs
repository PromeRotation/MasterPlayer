using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AlzadaalRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Alzadaal exact source polygon rectangular arenas and both views ownership", () =>
        {
            var source = (BossMod.ArenaBoundsCustom)typeof(BossMod.WorldState).Assembly
                .GetType("BossMod.Endwalker.Dungeon.D09AlzadaalsLegacy.D091Ambujam.D091Ambujam")!
                .GetField("DefaultBounds", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            foreach (var boss in new uint[] { 0x3879, 0x386C, 0x36C1 })
            {
                using var f = Prepare(boss); var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                if (boss == 0x3879)
                    Check(Vector2.Distance(new(arena.Position.X, arena.Position.Z), new(source.Center.X, source.Center.Z)) < .001, "Polygon center differs.");
                for (var x = -26.137f; x < 26; x += .31f)
                    for (var z = -26.231f; z < 26; z += .31f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        var expected = boss == 0x3879 ? source.Contains(new(x, z))
                            : Math.Abs(x) <= 19.5 && Math.Abs(z) <= (boss == 0x386C ? 19.5 : 24.5);
                        Check(arena.Contains(p, 0) == expected && world.Contains(p, 0) == expected, $"Arena differs at {p}.");
                    }
                Check(ArrDungeonCatalog.Owns(boss, 1050) && !ArrDungeonCatalog.Owns(boss, 986), "Ownership leaked.");
            }
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x3879, 0x233C, 29556, new BossMod.AOEShapeCircle(8)),
            (0x3879, 0x233C, 29467, new BossMod.AOEShapeCircle(8)),
            (0x386C, 0x233C, 29555, new BossMod.AOEShapeCircle(6)),
            (0x36C1, 0x36C1, 28520, new BossMod.AOEShapeRect(60, 7.5f)),
            (0x36C1, 0x233C, 28529, new BossMod.AOEShapeRect(5, 20)),
            (0x36C1, 0x233C, 28525, new BossMod.AOEShapeCircle(5)),
        })
            run($"Alzadaal {action} source shape radar world and ending", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, oid, c); Scan(f); Cast(f, 10, action, 101);
                var cast = f.Engine.Casts[10];
                Compare(new("test", "", cast.Rule.Shape!, cast.Location, cast.Rotation), shape);
                f.Engine.Finish(10); f.Render();
                Check(!f.Engine.Casts.ContainsKey(10) && !Aoes(f, "fountain").Any(), "Cast ending leaked.");
            });
        foreach (var (index, state, x, z) in new (uint, uint, float, float)[]
        {
            (0x11, 0x00080004, 117, -97), (0x11, 0x00800004, 131, -83),
            (0x11, 0x00200004, 131, -97), (0x11, 0x02000004, 117, -83),
            (0x10, 0x00200004, 109, -90), (0x10, 0x02000004, 139, -90),
            (0x10, 0x00080004, 124, -75), (0x10, 0x00800004, 124, -105),
        })
            run($"Alzadaal tentacle {index:X}/{state:X} ten point five prediction true ending FIFO both views", () =>
            {
                using var f = Prepare(0x3879);
                f.Engine.MapEffect(index, state, f.Now);
                var aoe = Aoes(f, "tentacle").Single();
                var q = new BossMod.WPos(x, z).Quantized();
                Check(Vector3.Distance(aoe.Position, new(q.X, 0, q.Z)) < .001 && aoe.Activation == f.Now.AddSeconds(10.5), "Tentacle location/time differs.");
                Compare(aoe, new BossMod.AOEShapeCircle(21)); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Tentacle outputs missing.");
                f.Actors[10] = new(10, 0x233C, aoe.Position); Scan(f); Cast(f, 10, 29158);
                f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(Aoes(f, "tentacle").Length == 1 && f.Draw.Previews.Count == 1, "Timeout consumed actual ending.");
                f.Engine.MapEffect(index, state, f.Now); f.Engine.Finish(10); f.Render();
                Check(Aoes(f, "tentacle").Single().Activation == f.Now.AddSeconds(10.5), "Tentacle ending did not consume FIFO.");
            });
        run("Alzadaal thirteen fountain arrival order then sort first ten promote and packet correction", () =>
        {
            using var f = Prepare(0x3879); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 23; ++i) f.Actors[i] = new(i, 0x233C, c + Vector3.UnitX * ((int)i - 15));
            Scan(f);
            for (ulong i = 10; i < 22; ++i) Cast(f, i, i % 2 == 0 ? 29556u : 29467u, duration: 40 - (int)i);
            Check(Aoes(f, "fountain").Select(a => a.Id).SequenceEqual(Enumerable.Range(10, 10).Select(i => $"alzadaal/fountain/{i}")), "Fountains sorted before thirteenth.");
            Cast(f, 22, 29556, duration: 18);
            var visible = Aoes(f, "fountain");
            Check(visible.Length == 10 && visible[0].Id == "alzadaal/fountain/22" && visible[^1].Id == "alzadaal/fountain/13", "Thirteenth did not sort.");
            Compare(visible[0], new BossMod.AOEShapeCircle(8));
            f.Engine.StartCast(22, f.Actors[22].Cast! with { Rotation = 1 }, f.Now); f.Render();
            Check(Aoes(f, "fountain").Length == 10, "Packet correction duplicated fountain.");
            f.Engine.Finish(22); f.Render();
            Check(Aoes(f, "fountain")[^1].Id == "alzadaal/fountain/12" && f.Draw.Previews.Count == 10 && f.Snapshot.DangerAreas.Count == 10,
                "Fountain removal failed promotion.");
        });
        foreach (var model in new byte[] { 4, 5 })
            run($"Alzadaal one wave model {model} timeline corners lengths and fourteen distinct effects", () =>
            {
                using var f = Prepare(0x386C); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x386D, new(-20.5f, 0, -202.5f), Rotation: .3f);
                f.Actors[11] = new(11, 0x386D, new(0, 0, -202.5f), Rotation: .6f); Scan(f);
                f.Engine.Timeline(10, 0x1E43, f.Now); f.Engine.Timeline(11, 0x1E43, f.Now);
                f.Engine.Effect(1, 28456, 1, f.Now); f.Actors[1] = f.Actors[1] with { ModelState = model }; Scan(f);
                var cones = Aoes(f, "reflection"); var rects = Aoes(f, "cannon");
                Check(cones.Length == 2 && rects.Length == 2 && cones.All(a => a.Activation == f.Now.AddSeconds(6.9)), "One wave prediction differs.");
                var angles = model == 4 ? new[] { BossMod.Angle.AnglesIntercardinals[2], BossMod.Angle.AnglesIntercardinals[0] }
                    : new[] { BossMod.Angle.AnglesIntercardinals[3], BossMod.Angle.AnglesIntercardinals[1] };
                for (var i = 0; i < 2; ++i)
                {
                    Check(Math.Abs(cones[i].Rotation - angles[i].Rad) < .000001, "Source angle differs.");
                    Compare(cones[i], new BossMod.AOEShapeCone(30, new(MathF.PI / 4)));
                }
                Compare(rects[0], new BossMod.AOEShapeRect(28, 4)); Compare(rects[1], new BossMod.AOEShapeRect(40, 4));
                for (uint i = 0; i < 13; ++i)
                {
                    f.Engine.Effect(20, 28455, i, f.Now);
                    f.Engine.Effect(20, 28455, i, f.Now, 101);
                }
                Check(Aoes(f, "reflection").Length == 2, "Duplicate effect counted twice.");
                f.Engine.Effect(20, 28455, 13, f.Now); f.Render();
                Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Fourteenth effect failed wave clear.");
                f.Engine.Timeline(10, 0x1E39, f.Now); f.Engine.Timeline(11, 0x1E39, f.Now);
                f.Engine.Effect(1, 28456, 2, f.Now); f.Actors[1] = f.Actors[1] with { AnimationState2 = 1 }; Scan(f);
                Check(Aoes(f, "reflection").Length == 2 && Aoes(f, "cannon").Length == 0, "Timeline deactivation or animation-only model update lost.");
            });
        run("Alzadaal two waves independently counted first two promotion status snapshot no duplicate", () =>
        {
            using var f = Prepare(0x386C); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 14; ++i)
                f.Actors[i] = new(i, 0x386D, c + Vector3.UnitX * ((int)i - 12), Statuses: [2552],
                    StatusParams: new Dictionary<uint, ushort> { [2552] = i < 12 ? (ushort)0x180 : (ushort)0x181 });
            Scan(f); f.Engine.Effect(1, 28457, 1, f.Now);
            f.Actors[1] = f.Actors[1] with { ModelState = 4 }; Scan(f); Scan(f);
            Check(Aoes(f, "reflection").Length == 2 && Aoes(f, "cannon").Length == 2 && f.Draw.Previews.Count == 4, "Two-wave cap or snapshot duplicated.");
            for (uint i = 0; i < 13; ++i) f.Engine.Effect(20, 28455, i, f.Now);
            Check(Aoes(f, "reflection").All(a => a.Activation == f.Start.AddSeconds(7.1)), "Reflection promoted early.");
            f.Engine.Effect(20, 28455, 13, f.Now); f.Render();
            Check(Aoes(f, "reflection").All(a => a.Activation == f.Start.AddSeconds(15))
                && Aoes(f, "cannon").All(a => a.Activation == f.Start.AddSeconds(6.9)), "Independent counter violated.");
            for (uint i = 0; i < 14; ++i) f.Engine.Effect(21, 28445, i, f.Now);
            Check(Aoes(f, "cannon").All(a => a.Activation == f.Start.AddSeconds(15)), "Second cannon pair not promoted.");
            // The source leaves TwoWaves as soon as its last cone pair resolves.
            for (uint i = 14; i < 28; ++i) f.Engine.Effect(21, 28445, i, f.Now);
            for (uint i = 14; i < 28; ++i) f.Engine.Effect(20, 28455, i, f.Now);
            f.Render(); Check(f.Draw.Previews.Count == 0, "Second waves leaked.");
        });
        run("Alzadaal void center finish plus point eight retained until actual estate four", () =>
        {
            using var f = Prepare(0x386C); var c = f.Engine.Arena!.Position;
            f.Actors[1] = f.Actors[1] with { Position = c + Vector3.UnitX * 5 };
            f.Actors[10] = new(10, 0x1EB69C, c, EventState: 4); Scan(f); Cast(f, 1, 28441);
            var aoe = Aoes(f, "void").Single();
            Check(aoe.Position == c && aoe.Activation == f.Engine.Casts[1].FinishAt.AddSeconds(.8), "Void time/origin differs.");
            f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null }; Scan(f);
            Check(Aoes(f, "void").Length == 1, "Existing estate four erased fresh void.");
            f.Engine.EventState(10, 4); f.Render(); Check(Aoes(f, "void").Length == 0, "Real estate four did not clear void.");
        });
        foreach (var pattern in new[] { 0, 1, 2 })
            foreach (var targetZ in new[] { -45.5f, -90.5f })
                run($"Alzadaal mana pattern {pattern} target {targetZ} late position source clock and true finish", () =>
                {
                    using var f = Prepare(0x36C1); var c = f.Engine.Arena!.Position;
                    f.Actors[10] = new(10, 0x233C, c); Scan(f);
                    if (pattern != 0) f.Engine.MapEffect(pattern == 1 ? 0x0Du : 9u, 0x00020001, f.Now);
                    f.Engine.Tether(10, 188, true, f.Now, 1);
                    Check(Aoes(f, "mana").Length == 0, "Mana selected before target reached known row.");
                    f.Now = f.Now.AddSeconds(2); f.Actors[10] = f.Actors[10] with { Position = new(110, 0, targetZ) }; Scan(f);
                    var aoes = Aoes(f, "mana"); var first = (targetZ == -45.5f) == (pattern == 1);
                    var points = first ? new Vector2[] { new(119, -68), new(101, -86), new(101, -50) }
                        : [new(119, -50), new(101, -68), new(119, -86)];
                    Check(aoes.Length == 3 && aoes.All(a => a.Activation == f.Start.AddSeconds(11.5)), "Mana clock reset on late position.");
                    for (var i = 0; i < 3; ++i)
                    {
                        var q = new BossMod.WPos(points[i].X, points[i].Y).Quantized();
                        Check(Vector3.Distance(aoes[i].Position, new(q.X, 0, q.Z)) < .001, "Mana pattern differs.");
                        Compare(aoes[i], new BossMod.AOEShapeCircle(15));
                    }
                    Cast(f, 10, 28523); Check(Aoes(f, "mana").Length == 3, "Mana cast duplicated circles.");
                    f.Engine.Finish(10); f.Render(); Check(Aoes(f, "mana").Length == 0, "Mana true ending leaked.");
                });
        foreach (var state in new uint[] { 0x00400004, 0x00800004, 0x00080004 })
            run($"Alzadaal traps persist past finish packet dedup and map {state:X} clears", () =>
            {
                using var f = Prepare(0x36C1); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, 28519);
                f.Engine.StartCast(10, f.Actors[10].Cast! with { Rotation = 1.1f }, f.Now); f.Render();
                Check(Aoes(f, "trap").Length == 1, "Corrected trap duplicated.");
                Compare(Aoes(f, "trap").Single(), new BossMod.AOEShapeRect(6, 3));
                f.Engine.Finish(10); f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(Aoes(f, "trap").Length == 1, "Trap expired without map clear.");
                f.Engine.MapEffect(1, state, f.Now); f.Render(); Check(Aoes(f, "trap").Length == 0, "Map failed trap clear.");
            });
        run("Alzadaal spinning staging mask blade and delayed buster source semantics", () =>
        {
            using var f = Prepare(0x36C1); var c = f.Engine.Arena!.Position;
            Check(Aoes(f, "staging").Length == 4 && f.Snapshot.GoalAreas.Count == 4, "Four staging goals missing.");
            f.Engine.StatusChanged(100, 2973, true, f.Now); f.Render();
            Check(Aoes(f, "staging").Length == 0, "Spinning override did not mask staging.");
            Cast(f, 1, 28520); f.Engine.Finish(1);
            f.Engine.StatusChanged(100, 2973, false, f.Now); f.Render();
            Check(Aoes(f, "staging").Single().Position == c - Vector3.UnitZ * 10, "Blade staging differs.");
            Cast(f, 1, 28528); f.Engine.Finish(1);
            Check(Math.Abs(f.Engine.Arena!.GetBounds().Height - 49) < .001, "Source initial bounds unexpectedly shrank.");
            Cast(f, 1, 28530, 100); f.Engine.Finish(1, completed: true);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Delayed buster removed at cast end.");
            f.Engine.Effect(1, 28530, 1, f.Now, 100); Check(!f.Engine.PredictedDamage.Any(), "Actual delayed buster did not clear.");
        });
        run("Alzadaal initial zero model is creation snapshot not change event", () =>
        {
            using var f = Prepare(0x386C); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x386D, c); Scan(f); f.Engine.Timeline(10, 0x1E43, f.Now);
            f.Engine.Effect(1, 28456, 1, f.Now); f.Actors[20] = new(20, 0x233C, c); Scan(f);
            Check(Aoes(f, "cannon").Length == 0, "Creation zero model produced early cannon.");
            f.Actors[1] = f.Actors[1] with { ModelState = 4 }; Scan(f);
            Check(Aoes(f, "cannon").Length == 1, "Actual model update duplicated cannon.");
        });
        run("Alzadaal snapshot spinning resets next round and self targeted delayed buster locks original target", () =>
        {
            using var f = Prepare(0x36C1); Cast(f, 1, 28520); f.Engine.Finish(1);
            f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Actors[100] = f.Actors[100] with { Statuses = [2973] }; Scan(f);
            f.Actors[100] = f.Actors[100] with { Statuses = [] }; Scan(f);
            Check(Aoes(f, "staging").Length == 4, "Snapshot spinning failed next round reset.");
            f.Actors[1] = f.Actors[1] with { TargetId = 100 }; Scan(f); Cast(f, 1, 28530, 1); f.Engine.Finish(1, completed: true);
            f.Actors[1] = f.Actors[1] with { Cast = null, TargetId = 101 }; Scan(f);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Delayed buster retargeted after self cast.");
        });
        run("Alzadaal persistent traps drive real native solver and reset all outputs", () =>
        {
            using var f = Prepare(0x36C1); var c = f.Engine.Arena!.Position;
            f.Engine.StatusChanged(100, 2973, true, f.Now);
            f.Actors[10] = new(10, 0x233C, c - Vector3.UnitZ * 3); Scan(f); Cast(f, 10, 28519, rotation: 0);
            f.Engine.Finish(10); f.Render(); var s = f.Snapshot;
            var result = new SafeZoneSolver().Solve(s, new(c, 0, 6, false, false, 0, true, s.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 0
                && !Aoes(f, "trap").Single().Shape.Resolve(c - Vector3.UnitZ * 3, 0, null).Contains(path[^1].Position, 0), "Solver failed trap.");
            f.Engine.ResetFight(); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked output.");
        });
    }

    private static DungeonAmbientAoe[] Aoes(Fight f, string kind) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith($"alzadaal/{kind}")).ToArray();
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1050); f.Engine.ResetFight(); var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false); Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f, double duration = 5.3)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(DungeonAmbientAoe aoe, BossMod.AOEShape source)
    {
        var shape = aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -42.137f; x < 42; x += .61f)
            for (var z = -42.231f; z < 42; z += .61f)
            {
                var p = aoe.Position + new Vector3(x, 0, z);
                var expected = source.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation));
                Check(shape.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source AOE differs in a view.");
            }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
