using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AitiascopeRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Aitiascope exact Livia Amon starting and Curtain hole arenas both views", () =>
        {
            foreach (var (boss, type, field, curtain) in new (uint, string, string, bool)[]
            {
                (0x3469, "D051Livia.D051Livia", "arena", false), (0x346E, "D053Amon.D053Amon", "arena", false),
                (0x346E, "D053Amon.CurtainCallArenaChange", "CurtaincallArena", true),
            })
            {
                using var f = Prepare(boss); if (curtain) f.Engine.MapEffect(5, 0x00020001);
                var source = (BossMod.ArenaBoundsCustom)Source(type).GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var arena = f.Engine.Arena!; var world = HostDrawingGeometry.Convert(arena);
                for (var x = -23.137f; x < 23; x += .31f)
                    for (var z = -23.231f; z < 23; z += .31f)
                    {
                        var p = arena.Position + new Vector3(x, 0, z);
                        Check(arena.Contains(p, 0) == source.Contains(new(x, z)) && world.Contains(p, 0) == arena.Contains(p, 0), "Arena differs.");
                    }
            }
            Check(ArrDungeonCatalog.IsScript("masterplayer.dungeon.aitiascope") && new uint[] { 0x3469, 0x346B, 0x346E }
                .All(o => ArrDungeonCatalog.Owns(o, 978) && !ArrDungeonCatalog.Owns(o, 974)), "Ownership leaked.");
        });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x3469, 0x233C, 25668, new BossMod.AOEShapeCone(20, new(MathF.PI / 4))),
            (0x3469, 0x346A, 25670, new BossMod.AOEShapeRect(20, 3)),
            (0x3469, 0x346A, 25671, new BossMod.AOEShapeRect(20, 3)),
            (0x3469, 0x3469, 25674, new BossMod.AOEShapeCone(40, new(MathF.PI / 3))),
            (0x3469, 0x233C, 25676, new BossMod.AOEShapeCircle(6)),
            (0x346B, 0x233C, 25679, new BossMod.AOEShapeRect(14, 20)),
            (0x346B, 0x346B, 25680, new BossMod.AOEShapeRect(40, 7)),
            (0x346B, 0x346B, 25687, new BossMod.AOEShapeRect(40, 3)),
            (0x346E, 0x233C, 27756, new BossMod.AOEShapeCircle(6)),
            (0x346E, 0x233C, 25695, new BossMod.AOEShapeRect(70, 6)),
            (0x346E, 0x233C, 25704, new BossMod.AOEShapeCircle(8)),
            (0x346E, 0x346E, 25697, new BossMod.AOEShapeRect(40, 10)),
            (0x346E, 0x346E, 25696, new BossMod.AOEShapeRect(40, 10)),
            (0x346E, 0x346E, 25690, new BossMod.AOEShapeCircle(15)),
            (0x346E, 0x233C, 25691, new BossMod.AOEShapeCone(20, new(MathF.PI / 8))),
            (0x346E, 0x233C, 25692, new BossMod.AOEShapeCone(20, new(MathF.PI / 8))),
        })
            run($"Aitiascope {action} source geometry radar world finish", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position; f.Actors[10] = new(10, oid, c); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.Casts[10]; Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Cast missing.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Finish leaked.");
            });
        run("Aitiascope Livia shot return strict five second lead latest finish clock dynamic caster and second cast clear", () =>
        {
            using var f = Prepare(0x3469); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x346A, c); f.Actors[11] = new(11, 0x346A, c); Scan(f);
            Cast(f, 10, 25670); Cast(f, 11, 25670);
            f.Now = f.Start.AddSeconds(5); f.Engine.Finish(10, endedAt: f.Now); f.Actors[10] = f.Actors[10] with { Cast = null };
            f.Now = f.Start.AddSeconds(5.2); f.Engine.Finish(11, endedAt: f.Now); f.Actors[11] = f.Actors[11] with { Cast = null };
            f.Now = f.Start.AddSeconds(10.2); f.Engine.Tick(f.Now); f.Render(); Check(f.Draw.Previews.Count == 0, "Lead must be strictly less than five.");
            f.Now = f.Now.AddMilliseconds(1); f.Engine.Tick(f.Now);
            f.Actors[10] = f.Actors[10] with { Position = c + Vector3.UnitX, Rotation = 1 }; Scan(f);
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 2 && aoes.All(a => Math.Abs((a.Activation!.Value - f.Start.AddSeconds(15.2)).TotalMilliseconds) < .001)
                && aoes[0].Position == c + Vector3.UnitX && aoes[0].Rotation == 1, "Return deadline or dynamic source differs.");
            Cast(f, 10, 25671); Check(f.Draw.Previews.Count == 1 && !f.Engine.AmbientAoes().Any(a => a.Id.Contains("/return/")), "Actual return did not clear ghost sources.");
        });
        run("Aitiascope Livia tankbuster dynamic cone event only finish damage prediction and perspective", () =>
        {
            using var f = Prepare(0x3469); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 25673, 100); f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null }; f.Render();
            Check(f.Engine.PredictedDamage.Single().DamageType == "Tankbuster" && f.Draw.Previews.Count == 1, "Cast finish incorrectly cleared bait.");
            f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 8 }; Scan(f);
            var aoe = f.Engine.AmbientAoes().Single(a => a.Id == "aitiascope/bite");
            Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeCone(9, new(MathF.PI / 3)));
            Check(Math.Abs(aoe.Rotation + MathF.PI / 2) < .001 && aoe.OutlineOnly && !aoe.Navigate, "Bait did not follow target.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Single(a => a.Id == "aitiascope/bite").Navigate, "Other perspective lacks danger.");
            f.Engine.Effect(1, 25673, 1, f.Now); f.Render(); Check(f.Draw.Previews.Count == 0 && !f.Engine.PredictedDamage.Any(), "Real event failed to consume bait.");
        });
        foreach (var own in new[] { false, true })
            run($"Aitiascope Livia stack target {own} mandatory real solver", () =>
            {
                using var f = Prepare(0x3469); var c = f.Engine.Arena!.Position;
                f.Actors[100] = f.Actors[100] with { Position = c - Vector3.UnitX * 10 }; f.Actors[10] = new(10, 0x233C, c); Scan(f);
                Cast(f, 10, 25677, own ? 100UL : 101UL);
                Solve(f, p => Vector3.Distance(p, f.Actors[101].Position) <= (own ? 3.01f : 6.01f));
            });
        foreach (var index in new uint[] { 1, 2 })
            run($"Aitiascope Rhitahtyn pocket pattern {index} union exact both views safe deadline skewer risk toggle solver restore", () =>
            {
                using var f = Prepare(0x346B); f.Engine.MapEffect(0, 0x00020001); f.Render();
                Check(!f.Engine.Arena!.Contains(new(19.5f, 0, 152)), "Narrow arena not applied.");
                f.Engine.MapEffect(index, 0x00200010, f.Start.AddSeconds(-1)); f.Render();
                var centers = index == 1 ? new Vector2[] { new(19.5f, 152), new(2.5f, 136) } : [new(2.5f, 152), new(19.5f, 136)];
                var source = new BossMod.ArenaBoundsCustom([new BossMod.Rectangle(new(11, 144), 6.5f, 19.5f),
                    .. centers.Select(p => new BossMod.Rectangle(new(p.X, p.Y), 2, 1.5f))]);
                var world = HostDrawingGeometry.Convert(f.Engine.Arena!);
                for (var x = -10.137f; x < 32; x += .31f)
                    for (var z = 123.231f; z < 165; z += .31f)
                    {
                        var p = new Vector3(x, 0, z);
                        Check(f.Engine.Arena!.Contains(p, 0) == source.Contains(new(x - source.Center.X, z - source.Center.Z))
                            && world.Contains(p, 0) == f.Engine.Arena.Contains(p, 0), "Pocket arena differs.");
                    }
                var deadline = f.Engine.AmbientAoes().Single(a => a.Id == "aitiascope/pockets/enter").Activation;
                Check(Math.Abs((deadline!.Value - f.Start.AddSeconds(6.3)).TotalMilliseconds) < .001, "Pocket timing differs.");
                Cast(f, 1, 25680); Check(!f.Engine.AmbientAoes().Single(a => a.Id == "aitiascope/skewer").Navigate, "Skewer risky before pockets settle.");
                Solve(f, p => centers.Any(c => Vector2.Distance(c, new(p.X, p.Z)) <= 5.01f));
                f.Engine.MapEffect(index, 0x00080004); f.Render(); Check(f.Engine.AmbientAoes().Single(a => a.Id == "aitiascope/skewer").Navigate
                    && !f.Engine.AmbientAoes().Any(a => a.Id == "aitiascope/pockets/enter"), "Skewer risk failed to restore.");
                f.Engine.MapEffect(0, 0x00080004); Check(f.Engine.Arena!.Contains(new(29, 0, 162)), "Full arena not restored.");
            });
        run("Aitiascope Rhitahtyn delayable buster locks original target survives completed cast and waits for real effect", () =>
        {
            using var f = Prepare(0x346B); f.Actors[1] = f.Actors[1] with { TargetId = 100 }; Scan(f); Cast(f, 1, 25686, 1);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Initial buster target missing.");
            f.Engine.Finish(1, completed: true); f.Actors[1] = f.Actors[1] with { Cast = null, TargetId = 101 }; Scan(f);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "Delayed buster changed target or vanished at cast finish.");
            f.Engine.Effect(1, 25686, 1, f.Now, 100); Check(!f.Engine.PredictedDamage.Any(), "Actual delayed buster failed to clear.");
        });
        foreach (var pattern in new[] { "SWNE", "NWSE", "W", "E" })
            run($"Aitiascope Rhitahtyn shrapnel {pattern} eighteen source positions eight second clock individual true finish", () =>
            {
                using var f = Prepare(0x346B);
                var type = Source("D052Rhitahtyn.Shrapnel");
                var positions = (BossMod.WPos[])type.GetField("shrapnelPositions" + pattern, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                var marker = pattern switch
                {
                    "SWNE" => new DungeonActor(10, 0x346C, new(11, 0, 140), Rotation: .01f),
                    "NWSE" => new DungeonActor(10, 0x346C, new(11, 0, 140), Rotation: -.01f),
                    "W" => new DungeonActor(10, 0x346D, new(10, 0, 144)),
                    _ => new DungeonActor(10, 0x346D, new(12, 0, 144)),
                };
                f.Actors[10] = marker; Scan(f);
                var aoes = f.Engine.AmbientAoes().ToArray();
                Check(aoes.Length == 18 && f.Draw.Previews.Count == 18 && aoes.All(a => a.Activation == f.Start.AddSeconds(8)), "Shrapnel count/time differs.");
                for (var i = 0; i < 18; ++i)
                {
                    Check(Vector2.Distance(new(aoes[i].Position.X, aoes[i].Position.Z), new(positions[i].X, positions[i].Z)) < .0001f, "Shrapnel coordinate differs.");
                    Compare(aoes[i].Shape, aoes[i].Position, 0, new BossMod.AOEShapeCircle(6));
                    f.Actors[(ulong)(20 + i)] = new((ulong)(20 + i), 0x233C, aoes[i].Position);
                }
                f.Actors.Remove(10); Scan(f);
                for (var i = 0; i < 18; ++i)
                {
                    Cast(f, (ulong)(20 + i), 25684); Check(f.Draw.Previews.Count == 18 - i, "Helper duplicated prediction.");
                    f.Engine.Finish((ulong)(20 + i)); f.Actors[(ulong)(20 + i)] = f.Actors[(ulong)(20 + i)] with { Cast = null }; f.Render();
                    Check(f.Draw.Previews.Count == 17 - i, "Individual finish failed to consume matched location.");
                }
            });
        run("Aitiascope Amon eight caster sorting threshold four cap promotion and packet correction", () =>
        {
            using var f = Prepare(0x346E); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 19; ++i) f.Actors[i] = new(i, 0x233C, c); Scan(f);
            for (ulong i = 10; i < 17; ++i) Cast(f, i, 25692, seconds: 20 - (int)i);
            Check(f.Engine.AmbientAoes().Select(a => a.Id).SequenceEqual(new[] { 10, 11, 12, 13 }.Select(i => $"aitiascope/cone/{i}")), "Sorted before eight.");
            Cast(f, 17, 25691, seconds: 2);
            Check(f.Engine.AmbientAoes().Select(a => a.Id).SequenceEqual(new[] { 17, 16, 15, 14 }.Select(i => $"aitiascope/cone/{i}")), "Eight did not sort by activation.");
            f.Engine.Finish(17); f.Actors[17] = f.Actors[17] with { Cast = null }; f.Render();
            Check(f.Engine.AmbientAoes().Last().Id == "aitiascope/cone/13", "Next cone failed to promote.");
            var cast = f.Actors[16].Cast!; f.Engine.StartCast(16, cast with { Rotation = 1.2f }, f.Now); f.Render();
            Check(Math.Abs(f.Engine.AmbientAoes().First().Rotation - 1.2f) < .001, "Packet correction stale.");
        });
        run("Aitiascope Amon LoS map gate ice snapshot safe shadow actual solver and no early or late blocker refresh", () =>
        {
            using var f = Prepare(0x346E); var c = f.Engine.Arena!.Position;
            f.Actors[1] = f.Actors[1] with { Position = c - Vector3.UnitZ * 20 };
            f.Actors[20] = new(20, 0x3470, c, HitboxRadius: 6); Scan(f); Cast(f, 1, 25702);
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "LoS active before map.");
            f.Engine.MapEffect(5, 0x00020001); f.Render(); Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "LoS map gate missing.");
            var safe = f.Engine.AmbientAoes().Single(a => a.Safe).Shape.Resolve(default, 0, null);
            Check(safe.Contains(c + Vector3.UnitZ * 12, 0) && !safe.Contains(c + Vector3.UnitX * 12, 0), "Wrong ice fan.");
            f.Actors[100] = f.Actors[100] with { Position = c + Vector3.UnitX * 12 }; Scan(f); Solve(f, p => safe.Contains(p, .01f));
            f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitX * 12 }; Scan(f);
            f.Engine.StartCast(1, f.Actors[1].Cast! with { TargetId = 100 }, f.Now); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Safe).Shape.Resolve(default, 0, null).Contains(c + Vector3.UnitZ * 12, 0), "Snapshot changed when ice moved.");
            f.Engine.MapEffect(5, 0x00080004); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "LoS hidden map leaked.");
            f.Engine.Finish(1); f.Engine.MapEffect(5, 0x00020001); f.Render(); Check(f.Draw.Previews.Count == 0, "Finished LoS revived.");
        });
        run("Aitiascope Amon no ice cast creates no forbidden full arena and no late phantom shelter", () =>
        {
            using var f = Prepare(0x346E); var c = f.Engine.Arena!.Position;
            Cast(f, 1, 25702); f.Engine.MapEffect(5, 0x00020001); f.Render();
            f.Actors[20] = new(20, 0x3470, c, HitboxRadius: 6); Scan(f);
            f.Engine.StartCast(1, f.Actors[1].Cast! with { TargetId = 100 }, f.Now); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Late ice rebuilt source cast snapshot.");
        });
    }

    private static Type Source(string type) => typeof(BossMod.WorldState).Assembly.GetType("BossMod.Endwalker.Dungeon.D05Aitiascope." + type)!;
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 978); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, float rotation = .7f, double seconds = 5)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, rotation, f.Now, f.Now.AddSeconds(seconds + .3), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -42.137f; x < 42; x += .91f)
            for (var z = -42.231f; z < 42; z += .91f)
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
            $"Real solver failed: {solution.Status}, fallback={solution.IsLeastDangerFallback}.");
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
