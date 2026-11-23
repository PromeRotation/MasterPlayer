using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class KtisisRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Ktisis exact three polygon arenas and territory ownership", () =>
        {
            foreach (var (boss, name, field, small) in new (uint, string, string, bool)[]
            {
                (0x3323, "D041Lyssa.D041Lyssa", "arena", false), (0x348A, "D043Hermes.D043Hermes", "StartingBounds", false),
                (0x348A, "D043Hermes.D043Hermes", "DefaultBounds", true),
            })
            {
                using var f = Prepare(boss); if (small) f.Engine.MapEffect(8, 0x00020001);
                var type = typeof(BossMod.WorldState).Assembly.GetType("BossMod.Endwalker.Dungeon.D04KtisisHyperboreia." + name)!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -25.137f; x < 25; x += .31f)
                    for (var z = -25.231f; z < 25; z += .31f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.ktisis") && new uint[] { 0x3323, 0x3425, 0x348A }
                .All(o => ArrDungeonCatalog.Owns(o, 974) && !ArrDungeonCatalog.Owns(o, 970)), "Ownership leaked.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x3323, 0x3324, 25179, new BossMod.AOEShapeCircle(4)),
            (0x3323, 0x3324, 25375, new BossMod.AOEShapeRect(40, 2)),
            (0x3425, 0x233C, 25745, new BossMod.AOEShapeCross(50, 2)),
            (0x348A, 0x348C, 25891, new BossMod.AOEShapeCircle(10)),
            (0x348A, 0x233C, 25901, new BossMod.AOEShapeRect(40, 3)),
            (0x348A, 0x233C, 25898, new BossMod.AOEShapeCircle(6)),
            (0x348A, 0x348B, 25889, new BossMod.AOEShapeRect(50, 5)),
            (0x348A, 0x233C, 25906, new BossMod.AOEShapeCircle(4)),
        })
            run($"Ktisis {action} exact source shape both views cancellation", () =>
            {
                using var f = Prepare(boss); f.Actors[10] = new(10, oid, f.Actors[1].Position); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.Casts[10]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast missing.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel leaked.");
            });
        foreach (var (index, state, x, z, angle) in new (uint, uint, float, float, float)[]
        {
            (0, 0x00200010, -154.825f, 42.75f, 60), (0, 0x01000080, -154.825f, 55.25f, 119.997f),
            (0, 0x00020001, -144, 36.5f, -.003f), (1, 0x00200010, -144, 61.5f, -180),
            (1, 0x01000080, -133.175f, 55.25f, -120.003f), (1, 0x00020001, -133.175f, 42.75f, -60.005f),
        })
            run($"Ktisis Lyssa map {index} {state:X} first thirteen later sixteen finish clear", () =>
            {
                using var f = Prepare(0x3323); f.Engine.MapEffect(index, state, f.Start.AddSeconds(-1)); f.Render();
                var aoe = f.Engine.AmbientAoes().Single(); var expected = new BossMod.WPos(x, z).Quantized();
                Check(aoe.Activation == f.Start.AddSeconds(12) && Math.Abs(aoe.Position.X - expected.X) < .0001f
                    && Math.Abs(aoe.Position.Z - expected.Z) < .0001f && Math.Abs(aoe.Rotation - angle * MathF.PI / 180) < .0001f, "Map pair/time differs.");
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(50, 25));
                f.Actors[10] = new(10, 0x233C, f.Actors[1].Position); Scan(f); Cast(f, 10, 25177);
                Check(f.Draw.Previews.Count == 1, "Helper duplicated map preview.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Slice finish did not clear.");
                f.Engine.MapEffect(index, state, f.Start.AddSeconds(20)); f.Render();
                Check(f.Engine.AmbientAoes().Single().Activation == f.Start.AddSeconds(36), "Later slice delay differs.");
            });
        run("Ktisis Lyssa pillar retimed by HeavySmash and repeated observation retains original override", () =>
        {
            using var f = Prepare(0x3323); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3324, c + Vector3.UnitX * 12); Scan(f); Cast(f, 10, 25375);
            f.Now = f.Start.AddSeconds(2); Cast(f, 1, 25180, 100);
            var activation = f.Engine.Casts[1].FinishAt; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/pillar/10").Activation == activation, "Pillar not retimed or observation overwrote it.");
            f.Engine.Finish(1); f.Render();
            Check(f.Engine.AmbientAoes().Single().Activation == activation, "Smash finish erased pillar retime.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Pillar finish leaked.");
        });
        foreach (var buddy in new[] { false, true })
            foreach (var own in new[] { false, true })
                run($"Ktisis HeavySmash buddy {buddy} own {own} mandatory branch and real solver", () =>
                {
                    using var f = Prepare(0x3323); var c = f.Engine.Arena!.Position;
                    f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 10 };
                    f.Actors[101] = f.Actors[101] with { Position = new(-139, c.Y, 47), Buddy = buddy }; Scan(f);
                    Cast(f, 1, 25180, own ? 100UL : 101UL);
                    var constraint = f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/stack/1");
                    Check(constraint.Activation == f.Engine.Casts[1].FinishAt && constraint.Navigate && !constraint.Safe, "Stack must preserve deadline.");
                    Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (buddy ? 5.01f : own ? 3.01f : 6.01f));
                });
        foreach (var (first, second, offset) in new (uint, uint, float)[]
        {
            (2812,2814,-120), (2813,2814,-240), (2812,2813,120), (2814,2813,240), (2813,2812,-120), (2814,2812,120),
        })
            run($"Ktisis Ladon head order {first} {second} two breaths cast finish real second and duplicates", () =>
            {
                using var f = Prepare(0x3425); f.Engine.StatusChanged(1, first, true, f.Now);
                f.Engine.StatusChanged(1, first, true, f.Now);
                f.Engine.StatusChanged(1, second, true, f.Now); Cast(f, 1, 25734);
                var aoes = f.Engine.AmbientAoes().ToArray(); var cast = f.Engine.Casts[1];
                Check(aoes.Length == 2 && aoes[0].Activation == cast.FinishAt && aoes[1].Activation == cast.FinishAt.AddSeconds(2.1)
                    && Math.Abs(aoes[1].Rotation - (.7f + offset * MathF.PI / 180)) < .0001f, "Head order/time differs.");
                foreach (var aoe in aoes) Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeCone(40, new(MathF.PI / 3)));
                f.Engine.Finish(1); f.Render();
                Check(f.Engine.AmbientAoes().Count() == 1, "Cast finish removed both breaths.");
                f.Now = f.Start.AddSeconds(20); f.Engine.Tick(f.Now); f.Render(); Check(f.Engine.AmbientAoes().Count() == 1, "Prediction timer replaced true second hit.");
                f.Engine.Effect(1, 25737, 1, f.Now); f.Engine.Effect(1, 25737, 1, f.Now); f.Render();
                Check(!f.Engine.AmbientAoes().Any(), "Second breath resolve leaked.");
            });
        run("Ktisis removed head ignores stale snapshot before next single breath", () =>
        {
            using var f = Prepare(0x3425);
            f.Actors[1] = f.Actors[1] with { Statuses = [2812] }; Scan(f); Cast(f, 1, 25734);
            f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null };
            f.Engine.StatusChanged(1, 2812, false, f.Now); Scan(f);
            f.Actors[1] = f.Actors[1] with { Statuses = [] }; Scan(f);
            f.Engine.StatusChanged(1, 2813, true, f.Now); f.Now = f.Now.AddSeconds(1); Cast(f, 1, 25735);
            Check(f.Engine.AmbientAoes().Count() == 1, "Stale removed head generated a second breath.");
        });
        run("Ktisis packet corrects recovered double breath and meteor LoS without duplicating stages", () =>
        {
            using (var f = Prepare(0x3425))
            {
                f.Engine.StatusChanged(1, 2812, true, f.Now); f.Engine.StatusChanged(1, 2814, true, f.Now);
                Cast(f, 1, 25734); var old = f.Actors[1].Cast!;
                f.Engine.StartCast(1, old with { Rotation = 1.2f, Location = old.Location + Vector3.UnitX, FinishAt = old.FinishAt.AddSeconds(.1) }, f.Now);
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Length == 2 && aoes.All(a => a.Position == old.Location + Vector3.UnitX)
                    && Math.Abs(aoes[0].Rotation - 1.2f) < .001
                    && Math.Abs(aoes[1].Rotation - (1.2f - MathF.Tau / 3)) < .001
                    && aoes[0].Activation == old.FinishAt, "Corrected packet left old breath geometry.");
            }
            using (var f = Prepare(0x348A))
            {
                var c = f.Engine.Arena!.Position; f.Actors[10] = new(10, 0x348B, c);
                f.Actors[20] = new(20, 0x348C, c + Vector3.UnitX * 5, HitboxRadius: 2.4f); Scan(f); Cast(f, 10, 27836, rotation: 0);
                var old = f.Actors[10].Cast!;
                f.Engine.StartCast(10, old with { Rotation = MathF.PI / 2, FinishAt = old.FinishAt.AddSeconds(.1) }, f.Now);
                var shadow = f.Engine.AmbientAoes().Single(a => a.Safe);
                Check(Math.Abs(shadow.Rotation - MathF.PI / 2) < .001 && Vector3.Distance(shadow.Position, f.Actors[20].Position + Vector3.UnitX * .2f) < .001
                    && shadow.Activation == old.FinishAt, "Corrected packet left old LoS geometry.");
            }
        });
        run("Ktisis Hermes shrink starts at cast plus half second then map owns exact arena", () =>
        {
            using var f = Prepare(0x348A); Cast(f, 1, 25886);
            var ring = f.Engine.AmbientAoes().Single();
            Check(ring.Activation == f.Engine.Casts[1].FinishAt.AddSeconds(.5), "Hermes ring time differs.");
            f.Engine.Finish(1); f.Render(); Check(f.Engine.AmbientAoes().Single() == ring, "Ring cleared without map.");
            f.Engine.MapEffect(8, 0x00020001); f.Render(); Check(!f.Engine.AmbientAoes().Any(), "Map did not clear ring.");
        });
        run("Ktisis Hermes first four wind lines stable arrival promotion and source destruction", () =>
        {
            using var f = Prepare(0x348A); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 16; ++i) f.Actors[i] = new(i, 0x348B, c); Scan(f);
            for (ulong i = 10; i < 16; ++i) Cast(f, i, 27837);
            Check(f.Draw.Previews.Count == 4 && f.Snapshot.DangerAreas.Count == 4, "Wind cap differs.");
            f.Engine.RemoveActor(10); f.Actors.Remove(10); f.Render();
            f.Actors[20] = new(20, 0x348B, c); Scan(f); Cast(f, 20, 27837);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "ktisis/wind/14") && !f.Engine.AmbientAoes().Any(a => a.Id == "ktisis/wind/20"), "New caster jumped ahead.");
        });
        run("Ktisis Hermes rectangular meteor LoS intact selection snapshot and actual solver", () =>
        {
            using var f = Prepare(0x348A); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x348B, c - Vector3.UnitZ * 20);
            f.Actors[20] = new(20, 0x348C, c + Vector3.UnitX * 10, AnimationState2: 1, HitboxRadius: 2.4f);
            f.Actors[21] = new(21, 0x348C, c - Vector3.UnitX * 5, HitboxRadius: 2.4f);
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 7 }; Scan(f);
            Cast(f, 10, 27836, rotation: 0);
            var shadow = f.Engine.AmbientAoes().Single(a => a.Safe); var shape = shadow.Shape.Resolve(shadow.Position, shadow.Rotation, null);
            Check(Math.Abs(shadow.Position.X - f.Actors[21].Position.X) < .001f && Math.Abs(shadow.Position.Z - (c.Z + .2f)) < .001f, "LoS used broken/incorrect meteor.");
            Compare(shadow.Shape, shadow.Position, shadow.Rotation, new BossMod.AOEShapeRect(49.8f, 2.4f));
            Solve(f, p => shape.Contains(p, .01f));
            f.Actors[21] = f.Actors[21] with { AnimationState2 = 1 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Safe) == shadow, "LoS snapshot changed during cast.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "LoS cast finish leaked.");
        });
        run("Ktisis Hermes three broken meteor goal hitbox range waits for explosion and no false obstacle", () =>
        {
            using var f = Prepare(0x348A); var c = f.Engine.Arena!.Position;
            for (ulong i = 20; i < 24; ++i) f.Actors[i] = new(i, 0x348C, c + Vector3.UnitX * ((float)i - 21), AnimationState2: (byte)(i == 23 ? 0 : 1), HitboxRadius: 2.4f);
            Scan(f); var goal = f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/meteor/near");
            Check(((CircleTemplate)goal.Shape).Radius == 7.4f && f.Snapshot.HardObstacles.Count == 0, "Meteor goal radius/impassability differs.");
            Cast(f, 20, 25891); Check(!f.Engine.AmbientAoes().Any(a => a.Id == "ktisis/meteor/near"), "Meteor blast failed to suppress approach.");
        });
        run("Ktisis Hermes aero bait dynamic target own directional navigation actual effect cleanup", () =>
        {
            using var f = Prepare(0x348A); var c = f.Engine.Arena!.Position;
            f.Engine.Effect(10, 25887, 1, f.Now, 100, occurredAt: f.Start.AddSeconds(-1)); f.Render();
            var bait = f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/aero/0");
            Check(Math.Abs((bait.Activation!.Value - f.Start.AddSeconds(4.7)).TotalMilliseconds) < .001 && !bait.Navigate && bait.OutlineOnly, "Own bait timing differs.");
            f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 10 }; Scan(f);
            Check(Math.Abs(f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/aero/0").Rotation + MathF.PI / 2) < .001, "Bait target did not follow.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/aero/0").Navigate, "Other perspective not avoiding bait.");
            f.Engine.Effect(10, 25900, 2, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "First Aero hit did not clear bait.");
        });
        run("Ktisis Hermes tornado icon original clock prediction real effect and death cleanup", () =>
        {
            using var f = Prepare(0x348A); f.Engine.Icon(100, 218, f.Start.AddSeconds(-1)); f.Render();
            var bait = f.Engine.AmbientAoes().Single(a => a.Id == "ktisis/tornado/0");
            Check(Math.Abs((bait.Activation!.Value - f.Start.AddSeconds(4.1)).TotalMilliseconds) < .001
                && f.Engine.PredictedDamage.Single().DamageType == "Tankbuster", "Tornado clock/prediction missing.");
            f.Engine.Effect(10, 25905, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0 && !f.Engine.PredictedDamage.Any(), "Tornado effect did not clear.");
            f.Engine.Icon(100, 218); f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f);
            Check(!f.Engine.PredictedDamage.Any(), "Dead tornado target retained.");
        });
        run("Ktisis interrupt job range target gate and spell finish", () =>
        {
            using var f = Prepare(0x348A); Cast(f, 1, 25907);
            Check(f.Engine.KtisisInterruptTarget(19)?.Id == 1 && f.Engine.KtisisInterruptTarget(24) == null
                && KtisisCatalog.InterruptAction(23) == (uint)BossMod.ClassShared.AID.HeadGraze
                && KtisisCatalog.InterruptAction(19) == (uint)BossMod.ClassShared.AID.Interject, "Interrupt jobs differ.");
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[1].Position + Vector3.UnitX * 20 }; Scan(f);
            Check(f.Engine.KtisisInterruptTarget(19) == null && f.Engine.KtisisInterruptTarget(23) != null, "Interrupt range differs.");
            f.Engine.Finish(1); Check(f.Engine.KtisisInterruptTarget(23) == null, "Ended spell still requests interrupt.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 974); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(5.3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -52.137f; x < 52; x += .71f)
            for (var z = -52.231f; z < 52; z += .71f)
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
            $"Real solver failed: {solution.Status}, fallback={solution.IsLeastDangerFallback}, end={(path.Count > 0 ? path[^1].Position : player)}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
