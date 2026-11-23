using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Scarmiglione = BossMod.Endwalker.Dungeon.D10FellCourtOfTroia.D103Scarmiglione.D103Scarmiglione;
using Fight = HalataliRegression.Fight;

internal static class TroiaRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Troia seven source modules ownership exact static arenas radar and world", () =>
        {
            foreach (var (name, field, native) in new (string, string, IGeometryShape)[]
            {
                ("D100TroianScavanger", "arena1", TroiaArenas.ScavangerFirst(0)),
                ("D100TroianScavanger", "arena2", TroiaArenas.ScavangerSecond(0)),
                ("D100TroianFootman", "arena", TroiaArenas.Footman(0)),
                ("D100TroianBishop", "arena", TroiaArenas.Bishop(0)),
                ("D100TroianButler", "arena1", TroiaArenas.ButlerFirst(0)),
                ("D100TroianButler", "arena2", TroiaArenas.ButlerSecond(0)),
                ("D101EvilDreamers", "arena", TroiaArenas.Dreamers(0)),
                ("D102Beatrice", "arena", TroiaArenas.Beatrice(0)),
                ("D103Scarmiglione", "StartingArena", TroiaArenas.ScarmiglioneStarting(0)),
            })
            {
                var type = typeof(Scarmiglione).Assembly.GetType($"BossMod.Endwalker.Dungeon.D10FellCourtOfTroia.{name}.{name}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                CompareArena(native, source, .73f);
            }
            var definitions = ArrDungeonCatalog.Create(1070);
            Check(definitions.Length == 7 && definitions.All(d => ArrDungeonCatalog.Owns(d.BossOid, 1070)
                && !ArrDungeonCatalog.Owns(d.BossOid, 1050)) && ArrDungeonCatalog.IsScript("masterplayer.dungeon.troia"), "Ownership differs.");
        });
        foreach (var large in new[] { false, true })
            run($"Troia dynamic {(large ? "large holes" : "small notches")} all walls individual removed alternating and combined source geometry", () =>
            {
                var masks = new[] { 0, 0xFFF, 0x555, 0xAAA, 0x37B, 0x8D2 }
                    .Concat(Enumerable.Range(0, 12).SelectMany(i => new[] { 1 << i, 0xFFF ^ (1 << i) }));
                foreach (var mask in masks)
                {
                    var walls = Scarmiglione.SafeWalls.Where((w, i) => (mask & (1 << i)) != 0).ToArray();
                    BossMod.Polygon[] circle = large ? Scarmiglione.StartingCircle : [new(Scarmiglione.ArenaCenter, 20, 64)];
                    var source = new BossMod.ArenaBoundsCustom(circle, walls);
                    var native = TroiaDynamicArena.Create(0, mask, large);
                    try { CompareArena(native, source, .43f); }
                    catch (InvalidOperationException ex) { throw new InvalidOperationException($"mask={mask:X} large={large}: {ex.Message}", ex); }
                    if (native is PolygonRegionGeometry polygon)
                    {
                        var fill = polygon.FillPolygons.Select(p => new WorldPolygonGeometry(p)).ToArray();
                        for (var x = -59.137f; x < -10; x += 1.13f)
                            for (var z = -322.231f; z < -273; z += 1.13f)
                            {
                                var p = new Vector3(x, 0, z);
                                Check(fill.Any(t => t.Contains(p, 0)) == native.Contains(p, 0), $"Fill paints hole or omits floor mask={mask:X} at {p}.");
                            }
                    }
                }
            });
        foreach (var (boss, oid, action, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x397D, 0x397E, 30212, new BossMod.AOEShapeCone(6, new(MathF.PI / 4))),
            (0x397D, 0x397C, 30214, new BossMod.AOEShapeCircle(5)),
            (0x397D, 0x397D, 30213, new BossMod.AOEShapeCircle(5)),
            (0x399E, 0x399E, 29713, new BossMod.AOEShapeCone(12, new(MathF.PI / 3))),
            (0x399E, 0x397F, 30218, new BossMod.AOEShapeCircle(6)),
            (0x399E, 0x399E, 29712, new BossMod.AOEShapeCircle(12)),
            (0x399E, 0x3981, 30215, new BossMod.AOEShapeCone(6, new(MathF.PI / 3))),
            (0x3983, 0x3982, 30216, new BossMod.AOEShapeCircle(8)),
            (0x3983, 0x3983, 30217, new BossMod.AOEShapeCone(12, new(MathF.PI / 3))),
            (0x3986, 0x3984, 30223, new BossMod.AOEShapeCircle(6)),
            (0x3986, 0x3985, 30221, new BossMod.AOEShapeCircle(6)),
            (0x3986, 0x3987, 30220, new BossMod.AOEShapeCircle(8)),
            (0x3986, 0x3987, 30219, new BossMod.AOEShapeCone(30, new(MathF.PI / 4))),
            (0x3986, 0x3986, 30222, new BossMod.AOEShapeCone(6, new(MathF.PI / 3))),
            (0x3988, 0x3966, 29624, new BossMod.AOEShapeRect(40, 2.5f)),
            (0x3988, 0x3966, 29627, new BossMod.AOEShapeRect(41, 2.5f)),
            (0x396D, 0x396D, 29822, new BossMod.AOEShapeCone(20, new(MathF.PI / 3))),
            (0x396D, 0x233C, 31207, new BossMod.AOEShapeDonut(10, 20)),
            (0x39C5, 0x39C5, 30235, new BossMod.AOEShapeCircle(9)),
            (0x39C5, 0x39C5, 30261, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x39C5, 0x233C, 30241, new BossMod.AOEShapeCircle(10)),
            (0x39C5, 0x233C, 30232, new BossMod.AOEShapeCircle(6)),
        })
            run($"Troia {action} exact source shape both views independent ending", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, oid, c); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.Casts[10]; var native = cast.Rule.Shape!.Resolve(cast.Location, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -42.137f; x < 42; x += .77f)
                    for (var z = -42.231f; z < 42; z += .77f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(c.X, c.Z), new(cast.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Shape differs.");
                    }
                Check(f.Draw.Previews.Count > 0 && f.Snapshot.DangerAreas.Count > 0, "Missing output.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Ending leaked.");
            });
        foreach (var boss in new uint[] { 0x397D, 0x399E, 0x3983, 0x3986 })
            run($"Troia trash {boss:X} companion pull shared aliases and group survival", () =>
            {
                using var f = Prepare(boss); var c = f.Actors[100].Position;
                var alias = f.Engine.Active!.PrimaryAliases![0];
                f.Actors[10] = new(10, alias, c, InCombat: true);
                Scan(f); f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 10, "Primary death reset surviving pack.");
                f.Actors[10] = f.Actors[10] with { InCombat = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Temporary combat flag reset pack.");
                f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null, "Completed pack persisted.");
                if (boss is 0x399E or 0x3983)
                {
                    f.Actors.Clear(); f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
                    f.Actors[11] = new(11, 0x3980, c, InCombat: true); Scan(f);
                    Check(f.Engine.Active?.BossOid == boss, "Shared hound chose wrong arena.");
                }
            });
        foreach (var (action, threshold) in new (uint, double)[] { (29621, 2.5), (29622, 5), (30260, 8) })
            run($"Troia {action} preview starts immediately navigation only at {threshold} seconds left", () =>
            {
                using var f = Prepare(action == 30260 ? 0x39C5u : 0x3988u); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, action == 30260 ? 0x233Cu : 0x3966u, c); Scan(f); Cast(f, 10, action, duration: 11);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 0, "Early risk or missing preview.");
                f.Now = f.Start.AddSeconds(11 - threshold - .001); f.Engine.Tick(f.Now); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 0, "Risk early.");
                f.Now = f.Start.AddSeconds(11 - threshold); f.Engine.Tick(f.Now); f.Render();
                Check(f.Snapshot.DangerAreas.Count == 1, "Risk late.");
                f.Now = f.Start.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1, "Prediction timeout consumed actual ending.");
                f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0, "Ending leaked.");
            });
        run("Troia Dreamers pull by 3966 dead third dreamer gates all cached circles until individual finish", () =>
        {
            using var f = Prepare(0x3988); var c = f.Engine.Arena!.Position;
            Check(f.Engine.PrimaryId != 1 && !f.Actors[1].InCombat, "Expected nonprimary combat pull.");
            f.Actors[10] = new(10, 0x3968, c); f.Actors[11] = new(11, 0x3968, c + Vector3.UnitX * 5); Scan(f);
            Cast(f, 10, 29628); Cast(f, 11, 29628); Check(f.Draw.Previews.Count == 0, "Circle before death.");
            f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 2 && f.Engine.Actors[10].Dead, "Dead gate or dead caster lost.");
            f.Actors[10] = f.Actors[10] with { Cast = null };
            f.Now = f.Start.AddSeconds(1); Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Engine.Actors[10].Dead, "Dead caster ending lost or removed entire group.");
            f.Engine.Finish(11); f.Render(); Check(f.Draw.Previews.Count == 0, "Circle ending leaked.");
        });
        run("Troia Beatrice first eight AOEs and independent gaze cap promote in arrival order facing and views", () =>
        {
            using var f = Prepare(0x396D); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 24; ++i) f.Actors[i] = new(i, 0x233C, c + Vector3.UnitX * (i == 22 ? -10 : 10));
            Scan(f);
            for (ulong i = 10; i < 19; ++i) Cast(f, i, 29817, duration: 30 - (int)i);
            Check(f.Draw.Previews.Count == 8 && !f.Engine.OutputCasts.Any(c => c.Value.Source == 18), "AOE cap/order differs.");
            f.Engine.Finish(10); f.Render(); Check(f.Engine.OutputCasts.Any(c => c.Value.Source == 18), "Ninth AOE not promoted.");
            for (ulong i = 11; i < 19; ++i) f.Engine.Finish(i);
            Cast(f, 20, 29821, duration: 5); Cast(f, 21, 29828, duration: 5); Cast(f, 22, 29828, duration: 5); Cast(f, 23, 29828, duration: 5);
            Check(f.Engine.Gazes.Count() == 3 && f.Draw.Previews.Count == 6, "Independent gaze queues or facing arc missing.");
            f.Now = f.Start.AddSeconds(4.5); f.Engine.Tick(f.Now); f.Render();
            var facing = f.Engine.GazeFacing(c, MathF.PI / 2);
            Check(facing != null && f.Engine.Gazes.All(g => MathF.Abs(MathF.Atan2(MathF.Sin(facing.Value - MathF.Atan2(g.Location.X - c.X, g.Location.Z - c.Z)),
                MathF.Cos(facing.Value - MathF.Atan2(g.Location.X - c.X, g.Location.Z - c.Z)))) > MathF.PI / 4), "Facing enters forbidden gaze cone.");
            f.Engine.Finish(21); f.Render(); Check(f.Engine.Gazes.Any(c => c.Source == 23), "Third gaze not promoted.");
        });
        run("Troia capped queues preserve arrivals after dictionary slot reuse and packet correction", () =>
        {
            using var f = Prepare(0x396D); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i < 24; ++i) f.Actors[i] = new(i, 0x233C, c);
            Scan(f); Cast(f, 10, 31207);
            for (ulong i = 11; i < 14; ++i) Cast(f, i, 29828);
            f.Engine.Finish(10); Cast(f, 14, 29828);
            Check(f.Engine.Gazes.Select(g => g.Source).SequenceEqual(new ulong[] { 11, 12 }), "Gaze reused dictionary hole.");
            f.Engine.StartCast(11, f.Actors[11].Cast! with { Location = c + Vector3.UnitX }, f.Now);
            Check(f.Engine.Gazes.Select(g => g.Source).SequenceEqual(new ulong[] { 11, 12 }), "Correction reordered gaze.");
            for (ulong i = 11; i <= 14; ++i) f.Engine.Finish(i);
            Cast(f, 10, 31207);
            for (ulong i = 11; i < 20; ++i) Cast(f, i, 29817);
            f.Engine.Finish(10); Cast(f, 20, 29817);
            Check(f.Engine.OutputCasts.Select(x => x.Value.Source).ToHashSet().SetEquals(Enumerable.Range(11, 8).Select(i => (ulong)i)),
                "Circle reused dictionary hole.");
        });
        run("Troia late death gate retains navigation after predicted ending until observed ending", () =>
        {
            using var f = Prepare(0x3988); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3968, c); f.Actors[11] = new(11, 0x3968, c); Scan(f);
            Cast(f, 10, 29628, duration: 1);
            f.Now = f.Start.AddSeconds(3); f.Actors[11] = f.Actors[11] with { Dead = true }; Scan(f);
            Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Late gate has drawing but no navigation.");
            f.Now = f.Start.AddSeconds(5); f.Engine.Tick(f.Now); f.Render();
            Check(f.Snapshot.DangerAreas.Count == 1, "Persistent cast risk expired.");
            f.Engine.Finish(10); f.Render(); Check(f.Snapshot.DangerAreas.Count == 0, "Observed ending did not clear risk.");
        });
        run("Troia repeated primary OID selects the actual combatants arena", () =>
        {
            using var f = Prepare(0x3986); f.Engine.ResetFight(); f.Actors.Clear();
            f.Actors[1] = new(1, 0x3986, new(10, 0, -116));
            f.Actors[2] = new(2, 0x3986, new(-74, 0, -175), InCombat: true);
            f.Actors[100] = new(100, 0, new(-74, 0, -175), PartySlot: 0, Hostile: false); Scan(f);
            Check(f.Engine.PrimaryId == 2 && f.Engine.Arena!.Contains(f.Actors[2].Position, 0)
                && !f.Engine.Arena.Contains(f.Actors[1].Position, 0), "Wrong same-OID primary selected arena.");
        });
        run("Troia Doom status gain stale scan pending loss multiple instances and cleanse class range", () =>
        {
            using var f = Prepare(0x396D);
            f.Engine.StatusChanged(100, 3364, true, f.Now); Scan(f);
            Check(f.Engine.TroiaCleanseTarget(24)?.Id == 100 && f.Engine.TroiaCleanseTarget(23)?.Id == 100
                && f.Engine.TroiaCleanseTarget(19) == null, "Doom event lost or class incorrect.");
            f.Actors[100] = f.Actors[100] with { Statuses = [3364, 3364] }; Scan(f);
            f.Engine.StatusChanged(100, 3364, false, f.Now);
            f.Actors[100] = f.Actors[100] with { Statuses = [3364] }; Scan(f);
            Check(f.Engine.TroiaCleanseTarget(24)?.Id == 100, "One instance loss erased Doom.");
            f.Actors[100] = f.Actors[100] with { Statuses = [] }; Scan(f);
            Check(f.Engine.TroiaCleanseTarget(24) == null, "Final loss leaked.");
        });
        foreach (var action in new uint[] { 31208, 30243, 30254 })
            run($"Troia {action} four player stack hard join marked player gather and cleanup", () =>
            {
                using var f = Prepare(action == 31208 ? 0x396Du : 0x39C5u); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, action, 101);
                var constraint = Aoes(f, "stack").Single(a => a.Id.EndsWith("/join")).Shape.Resolve(c, 0, null);
                Check(constraint.Contains(c, 0) && !constraint.Contains(f.Actors[101].Position, 0), "Stack is only a soft goal.");
                f.PlayerId = 101; Scan(f);
                Check(Aoes(f, "stack").Any(a => a.Id.EndsWith("/gather")), "Marked player cannot gather.");
                f.Engine.Finish(10); f.Render(); Check(!Aoes(f, "stack").Any(), "Stack ending leaked.");
            });
        run("Troia all map wall mappings pending does not shrink removal shrinks return branch strict threshold", () =>
        {
            for (uint index = 5; index <= 0x10; ++index)
            {
                using var f = Prepare(0x39C5); var initial = f.Engine.Arena;
                f.Engine.MapEffect(index, 0x00020001); Check(ReferenceEquals(initial, f.Engine.Arena), "Pending removed floor.");
                f.Engine.MapEffect(index, 0x00200010);
                var wall = index <= 0xA ? (int)index + 1 : (int)index - 0xB;
                CompareArena(f.Engine.Arena!, new BossMod.ArenaBoundsCustom([new BossMod.Polygon(Scarmiglione.ArenaCenter, 20, 64)],
                    Scarmiglione.SafeWalls.Where((w, i) => i != wall).ToArray()), .67f);
            }
            using var g = Prepare(0x39C5); g.Engine.MapEffect(5, 0x00200010);
            var small = g.Engine.Arena; g.Engine.MapEffect(4, 0x00020001);
            Check(ReferenceEquals(small, g.Engine.Arena), "Reset event directly changed bounds.");
            g.Actors[100] = g.Actors[100] with { Position = new(-35, 0, -276) }; Scan(g);
            Check(Aoes(g, "return").Length == 1 && g.Engine.Arena!.Contains(g.Actors[100].Position, 0), "Return expansion absent.");
            g.Actors[100] = g.Actors[100] with { Position = new(-35, 0, -278.01f) }; Scan(g);
            Check(Aoes(g, "return").Length == 1, "Return cleared at R20 instead of distance squared 399.");
            g.Actors[100] = g.Actors[100] with { Position = new(-35, 0, -279) }; Scan(g);
            Check(Aoes(g, "return").Length == 0, "Return did not clear.");
        });
        run("Troia VacuumWave locks safe fourteen degree fans and lines ignores immunity true ending", () =>
        {
            using var f = Prepare(0x39C5); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c);
            var direction = Vector3.Normalize(new Vector3(-41.511f, 0, -279.09f) - c);
            f.Actors[100] = f.Actors[100] with { Position = c + direction * 2, Statuses = [160] };
            Scan(f); f.Engine.MapEffect(5, 0x00020001); Cast(f, 10, 30236);
            var fans = Aoes(f, "vacuum/safe"); Check(fans.Length == 11, "Pending wall included in safe fans.");
            var before = Aoes(f, "vacuum").Single(a => a.Id.EndsWith("/landing")).Position;
            f.Engine.MapEffect(6, 0x00020001); f.Engine.MapEffect(6, 0x00200010);
            f.Engine.StartCast(10, f.Actors[10].Cast!, f.Now);
            Check(Aoes(f, "vacuum/safe").Length == 11 && Aoes(f, "vacuum").Single(a => a.Id.EndsWith("/landing")).Position == before, "Walls relocked mid-cast.");
            f.Engine.Finish(10); f.Render(); Check(Aoes(f, "vacuum").Length == 0, "Vacuum ending leaked.");
            var landing = TroiaKnockback.Landing(c + Vector3.UnitX * 2, c, .5f, f.Engine.Arena!, []);
            Check(Math.Abs(landing.X - (c.X + 32)) < .001, "Empty line list clamped full knockback.");
        });
        run("Troia spread navigates around radius seven wall hitboxes displays actual radius and firedamp tracks target", () =>
        {
            using var f = Prepare(0x39C5); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c); f.Actors[11] = new(11, 0x39C8, c + Vector3.UnitZ * 10, HitboxRadius: 1);
            Scan(f); Cast(f, 10, 30233, 100);
            var wall = Aoes(f, "wall");
            Check(wall.Single(a => a.Id.EndsWith("avoid")).Shape is CircleTemplate { Radius: 7 }
                && wall.Single(a => a.Id.EndsWith("display")).Shape is CircleTemplate { Radius: 1 }, "Navigation/display wall sizes merged.");
            Check(!Aoes(f, "return").Any(), "Unreachable opening donut invented.");
            f.Engine.Finish(10); Cast(f, 10, 30263, 101);
            f.Actors[101] = f.Actors[101] with { Position = c + Vector3.UnitZ * 13 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "party/avoid/10").Position == f.Actors[101].Position, "Firedamp locked ground position.");
            Check(f.Engine.PredictedDamage.Single(p => p.DamageType == "Tankbuster").TargetId == 101, "Bait buster prediction missing.");
        });
        run("Troia CorruptorsPitch effect plus eight point one persists through hits one two enrage duplicates until third", () =>
        {
            using var f = Prepare(0x39C5); f.Engine.Effect(1, 30245, 1, f.Now);
            var at = f.Now.AddSeconds(8.1);
            f.Now = f.Now.AddSeconds(1); f.Engine.Effect(1, 30245, 1, f.Now, 101);
            Check(f.Engine.PredictedDamage.All(p => p.Activation == at) && f.Engine.PredictedDamage.Count() == 2, "Duplicate changed prediction.");
            f.Now = f.Now.AddSeconds(20); f.Engine.Tick(f.Now);
            foreach (var action in new uint[] { 30247, 30248, 30250 }) f.Engine.Effect(10, action, action, f.Now);
            Check(f.Engine.PredictedDamage.Count() == 2, "Early effect/clock cleared third hit.");
            f.Engine.Effect(10, 30249, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Third effect did not clear.");
        });
        run("Troia native solver escapes risky circle and reset clears areas previews facing and walls", () =>
        {
            using var f = Prepare(0x3988); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x3966, c); Scan(f); Cast(f, 10, 29621, duration: 2);
            var s = f.Snapshot;
            var solution = new SafeZoneSolver().Solve(s, new(c, 0, 6, false, false, 0, true, s.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
            Check(solution.Status == SolutionStatus.Success && !solution.IsLeastDangerFallback && path.Count > 0
                && Vector3.Distance(path[^1].Position, c) > 6, "Solver did not escape circle.");
            f.Engine.ResetFight(); f.Render();
            Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0 && !f.Engine.Gazes.Any(), "Reset leaked.");
        });
    }

    private static void CompareArena(IGeometryShape native, BossMod.ArenaBoundsCustom source, float step)
    {
        var world = HostDrawingGeometry.Convert(native); var bounds = native.GetBounds();
        for (var x = bounds.MinX + .137f; x < bounds.MaxX; x += step)
            for (var z = bounds.MinZ + .231f; z < bounds.MaxZ; z += step)
            {
                var p = new Vector3(x, native.Position.Y, z);
                var expected = source.Contains(new(x - source.Center.X, z - source.Center.Z));
                Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected,
                    $"Arena differs at {p}, expected={expected} native={native.Contains(p, 0)} world={world.Contains(p, 0)}.");
            }
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1070); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        var primary = boss == 0x397D ? new Vector3(100, 0, 175) : c;
        f.Actors[1] = new(1, boss, primary, InCombat: boss != 0x3988);
        var arena = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(primary);
        var interior = arena.Contains(c, 0) ? c : arena.GetContours()[0].Select(p => p * .99f + c * .01f).First(p => arena.Contains(p, 0));
        f.Actors[1] = f.Actors[1] with { Position = boss == 0x397D ? primary : interior };
        f.Actors[100] = new(100, 0, boss == 0x397D ? new(90, 0, 155) : interior, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, f.Actors[100].Position + Vector3.UnitX * 10, PartySlot: 1, Hostile: false);
        if (boss == 0x3988) f.Actors[2] = new(2, 0x3966, c, InCombat: true);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, double duration = 5)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Aoes(Fight f, string part) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("troia/" + part)).ToArray();
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
