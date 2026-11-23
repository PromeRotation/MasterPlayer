using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Arkas = BossMod.Endwalker.Dungeon.D12Aetherfont.D122Arkas.D122Arkas;
using Fight = HalataliRegression.Fight;

internal static class AetherfontRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Aetherfont eight modules exact source arenas including inward octopus bridges and both views", () =>
        {
            var definitions = ArrDungeonCatalog.Create(1126);
            Check(definitions.Length == 8 && definitions.All(d => ArrDungeonCatalog.Owns(d.BossOid, 1126)
                && !ArrDungeonCatalog.Owns(d.BossOid, 1097)) && ArrDungeonCatalog.IsScript("masterplayer.dungeon.aetherfont"), "Ownership.");
            foreach (var (typeName, field, native) in new (string, string, IGeometryShape)[]
            {
                ("D120HaamOtodus", "arena", AetherfontArenas.Otodus(0)),
                ("D120HaamHalonid", "arena", AetherfontArenas.Halonid(0)),
                ("D120HaamTroll", "arena", AetherfontArenas.Troll(0)),
                ("D120HaamCrystal", "arena", AetherfontArenas.Crystal(0)),
                ("D120HaamForlorn", "arena", AetherfontArenas.Forlorn(0)),
                ("D121Lyngbakr", "arena", AetherfontArenas.Lyngbakr(0)),
                ("D122Arkas", "DefaultBounds", AetherfontArenas.Arkas(0)),
                ("D122Arkas", "SmallerBounds", AetherfontArenas.ArkasSmall(0)),
                ("D123Octomammoth", "arena", AetherfontArenas.Octomammoth(0)),
            })
            {
                var type = typeof(Arkas).Assembly.GetType($"BossMod.Endwalker.Dungeon.D12Aetherfont.{typeName}.{typeName}")!;
                var source = (BossMod.ArenaBoundsCustom)type.GetField(field, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
                var world = HostDrawingGeometry.Convert(native); var c = native.Position;
                Check(c.X == source.Center.X && c.Z == source.Center.Z, "Wrong resolved center " + typeName);
                for (var x = -source.Radius - .137f; x < source.Radius; x += .43f)
                    for (var z = -source.Radius - .231f; z < source.Radius; z += .43f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = source.Contains(new(p.X - source.Center.X, p.Z - source.Center.Z));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected,
                            $"Arena {typeName} mismatch at {p}.");
                    }
            }
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x3EC7, 33992, new BossMod.AOEShapeCircle(8)),
            (0x3EC7, 33991, new BossMod.AOEShapeCone(16, new(MathF.PI / 4))),
            (0x3EC9, 33991, new BossMod.AOEShapeCone(16, new(MathF.PI / 4))),
            (0x3ECB, 33994, new BossMod.AOEShapeCone(11, new(MathF.PI / 3))),
            (0x3ECD, 33995, new BossMod.AOEShapeCone(12, new(MathF.PI / 3))),
            (0x3ECD, 33993, new BossMod.AOEShapeCircle(6)),
            (0x3ED1, 33996, new BossMod.AOEShapeRect(12, 2)),
            (0x3EEB, 33344, new BossMod.AOEShapeCone(40, new(MathF.PI / 2))),
            (0x3EEA, 33367, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 33360, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 34713, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 34321, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 34714, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 33362, new BossMod.AOEShapeCircle(10)),
            (0x3EEA, 33365, new BossMod.AOEShapeCircle(6)),
            (0x3EEA, 33363, new BossMod.AOEShapeRect(30, 4)),
            (0x3EAA, 33346, new BossMod.AOEShapeRect(22, 4)),
            (0x3EAA, 33348, new BossMod.AOEShapeCone(16, new(MathF.PI / 3))),
            (0x3EAA, 34551, new BossMod.AOEShapeCone(35, new(MathF.PI / 2))),
            (0x3EAA, 33354, new BossMod.AOEShapeCone(35, new(MathF.PI / 2))),
            (0x3EAA, 33355, new BossMod.AOEShapeDonut(20, 26)),
            (0x3EAA, 33353, new BossMod.AOEShapeCircle(8)),
            (0x3EAA, 33351, new BossMod.AOEShapeCircle(12)),
        })
            run($"Aetherfont {boss:X}/{action} exact shape radar world observed ending no premature timeout", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x233C, c); Scan(f); Cast(f, 10, action);
                var cast = f.Engine.OutputCasts.Single().Value; var native = cast.Rule.Shape!.Resolve(c, cast.Rotation, null);
                var world = HostDrawingGeometry.Convert(native);
                for (var x = -41.137f; x < 41; x += 1.1f)
                    for (var z = -41.231f; z < 41; z += 1.1f)
                    {
                        var p = c + new Vector3(x, 0, z);
                        var expected = shape.Check(new(p.X, p.Z), new(c.X, c.Z), new(cast.Rotation));
                        Check(native.Contains(p, 0) == expected && world.Contains(p, 0) == expected, "Source shape differs.");
                    }
                f.Now = f.Start.AddSeconds(20); f.Engine.Tick(f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "Prediction timeout erased active cast.");
                Finish(f, 10); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Real ending leaked.");
            });
        foreach (var boss in new uint[] { 0x3EC7, 0x3EC9, 0x3ECB, 0x3ECD, 0x3ED1 })
            run($"Aetherfont trash {boss:X} source square pull shared aliases primary death and all members ending", () =>
            {
                using var f = Prepare(boss); var definition = f.Engine.Active!; var c = f.Engine.Arena!.Position;
                var alias = definition.PrimaryAliases![0];
                f.Engine.ResetFight(); f.Actors[1] = f.Actors[1] with { InCombat = false };
                f.Actors[10] = new(10, alias, c, InCombat: true); Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Alias did not start correct source group.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 10, "Primary death lost survivor.");
                f.Actors[10] = f.Actors[10] with { InCombat = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Temporary combat flag ended group.");
                f.Actors[10] = f.Actors[10] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null, "All dead group retained.");
            });
        run("Aetherfont crystal prefix strict one second only eleven sort real source endings and packet correction", () =>
        {
            using var f = Prepare(0x3EEB); var c = f.Engine.Arena!.Position;
            for (ulong i = 10; i <= 21; ++i) f.Actors[i] = new(i, 0x233C, c + Vector3.UnitX * (i - 10)); Scan(f);
            Cast(f, 10, 33340, duration: 10); Cast(f, 11, 33339, duration: 5);
            Check(Aoes(f, "crystal").Length == 2 && Aoes(f, "crystal")[0].Position == f.Actors[10].Position, "Premature sorting.");
            for (ulong i = 12; i <= 20; ++i) Cast(f, i, 33339, duration: 5 + (i - 12) * .25);
            Check(Aoes(f, "crystal").Length == 5 && Aoes(f, "crystal").All(a => a.Activation < f.Start.AddSeconds(6)), "Eleven sort/prefix strict boundary.");
            Cast(f, 21, 33339, duration: 3);
            Check(Aoes(f, "crystal").Length == 5, "Twelfth cast resorted or non-prefix filtered.");
            var observed = f.Actors[11].Cast!;
            f.Engine.StartCast(11, observed with { Location = c - Vector3.UnitX * 8 }, f.Now);
            Check(Aoes(f, "crystal").Length == 5 && Aoes(f, "crystal").First().Position == c - Vector3.UnitX * 8, "Correction duplicated/reordered.");
            f.Now = f.Start.AddSeconds(30); f.Engine.Tick(f.Now); f.Render();
            Check(Aoes(f, "crystal").Length == 5 && f.Snapshot.DangerAreas.Count == 5, "Clock cleared mixed crystals.");
            foreach (var source in new ulong[] { 11, 12, 13, 14, 15 }) Finish(f, source);
            Check(Aoes(f, "crystal").Length == 4, "Next prefix not promoted.");
            f.Engine.RemoveActor(16, f.Now); f.Actors.Remove(16); f.Render();
            Check(Aoes(f, "crystal").Length == 4 && Aoes(f, "crystal").First().Position == f.Actors[17].Position,
                "Destroyed crystal caster not removed or next prefix not promoted.");
        });
        foreach (var index in new uint[] { 1, 2, 3, 4 })
            foreach (var state in new uint[] { 0x00200010, 0x00020001 })
                run($"Aetherfont fissure map {index}/{state:X} exact source reverse sixteen FIFO cap append no effect timeout", () =>
                {
                    using var f = Prepare(0x3EEA); var c = f.Engine.Arena!.Position;
                    var type = typeof(Arkas).Assembly.GetType("BossMod.Endwalker.Dungeon.D12Aetherfont.D122Arkas.ForkedFissures")!;
                    var pair = type.GetField($"pattern0x0{index}", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                    var starts = (BossMod.WPos[])pair.GetType().GetField("Item1")!.GetValue(pair)!;
                    var ends = (BossMod.WPos[])pair.GetType().GetField("Item2")!.GetValue(pair)!;
                    f.Engine.MapEffect(index, state, f.Now); f.Render();
                    var aoes = Aoes(f, "fissure"); Check(aoes.Length == 16 && f.Draw.Previews.Count == 16 && f.Snapshot.DangerAreas.Count == 16, "Sixteen outputs.");
                    for (var i = 0; i < 16; ++i)
                    {
                        var source = 15 - i; var delta = ends[source] - starts[source]; var a = aoes[i];
                        Check(a.Position.X == starts[source].X && a.Position.Z == starts[source].Z
                            && a.Shape is RayRectangleTemplate rectangle && MathF.Abs(rectangle.Length - delta.Length()) < .0001f
                            && MathF.Abs(a.Rotation - MathF.Atan2(delta.X, delta.Z)) < .0001f
                            && a.Activation == f.Start.AddSeconds(6), "Wrong reverse pattern or timing.");
                        var sourceShape = new BossMod.AOEShapeRect(delta.Length(), 2);
                        var geometry = HostDrawingGeometry.Convert(a.Shape.Resolve(a.Position, a.Rotation, null));
                        var p = a.Position + new Vector3(delta.X, 0, delta.Z) * .5f;
                        Check(geometry.Contains(p, 0) && sourceShape.Check(new(p.X, p.Z), starts[source], new(a.Rotation)), "World strip shape.");
                    }
                    f.Now = f.Start.AddSeconds(10); f.Engine.MapEffect(index, state, f.Now);
                    f.Engine.Effect(1, 33361, 1, f.Now); f.Render(); Check(Aoes(f, "fissure").Length == 16, "Effect cleared FIFO.");
                    f.Actors[10] = new(10, 0x233C, c); Scan(f);
                    for (var i = 0; i < 16; ++i) { f.Now = f.Now.AddSeconds(2); Cast(f, 10, 33361); Finish(f, 10); }
                    Check(Aoes(f, "fissure").Length == 16 && Aoes(f, "fissure").All(a => a.Activation == f.Start.AddSeconds(16)),
                        "Second map group lost.");
                    f.Now = f.Now.AddSeconds(2);
                    Cast(f, 10, 33361); f.Engine.RemoveActor(10, f.Now); f.Actors.Remove(10); f.Render();
                    Check(Aoes(f, "fissure").Length == 15, "Helper destruction did not advance FIFO.");
                });
        run("Aetherfont Arkas only BattleCry2 shrink prediction map confirms restores exact centers", () =>
        {
            using var f = Prepare(0x3EEA); Cast(f, 1, 34605); Check(Aoes(f, "shrink").Length == 0, "Wrong cry predicts shrink."); Finish(f, 1);
            f.Now = f.Now.AddSeconds(1); Cast(f, 1, 33364);
            Check(Math.Abs((Aoes(f, "shrink").Single().Activation!.Value - f.Now).TotalSeconds - 5.8) < .00001, "Shrink delay.");
            Finish(f, 1); f.Engine.MapEffect(0, 0x00080004, f.Now); f.Render();
            Check(Aoes(f, "shrink").Length == 1 && f.Engine.Arena!.Position.Z == AetherfontArenas.Arkas(0).Position.Z, "Restore cleared prediction.");
            f.Engine.MapEffect(0, 0x00020001, f.Now); f.Render();
            Check(Aoes(f, "shrink").Length == 0 && f.Engine.Arena!.Position.Z == -440
                && !f.Engine.Arena.Contains(new(438, 0, -440), 0), "Map shrink not applied.");
            f.Engine.MapEffect(0, 0x00080004, f.Now); f.Render();
            Check(f.Engine.Arena!.Contains(new(438, 0, -440), 0), "Restore bounds.");
        });
        foreach (var (boss, action, radius) in new (uint, uint, float)[] { (0x3EEB, 33342, 5), (0x3EAA, 34436, 6) })
            run($"Aetherfont {action} dynamic spread source extra one navigation perspective and ending", () =>
            {
                using var f = Prepare(boss); Cast(f, 1, action, 101);
                var display = Aoes(f, "spread").Single(a => a.Display); var avoid = Aoes(f, "spread").Single(a => a.Navigate);
                Check(display.Shape is CircleTemplate { Radius: var r } && r == radius && display.OutlineOnly
                    && avoid.Shape is CircleTemplate { Radius: var extra } && extra == radius + 1, "Spread navigation threshold.");
                f.Actors[101] = f.Actors[101] with { Position = f.Actors[100].Position + Vector3.UnitX * 4 }; Scan(f);
                Check(Aoes(f, "spread").Single(a => a.Display).Position == Quantized(f.Actors[101].Position), "Spread frozen.");
                f.PlayerId = 101; Scan(f);
                Check(Aoes(f, "spread").Single(a => a.Navigate).Position == Quantized(f.Actors[100].Position), "Perspective wrong personal constraint.");
                Finish(f, 1); Check(!Aoes(f, "spread").Any(), "Spread ending leaked.");
            });
        run("Aetherfont hard four stack cast and actor icon independent effect cleanup real solver", () =>
        {
            using var f = Prepare(0x3EEB); Cast(f, 1, 33343, 101);
            Check(Aoes(f, "stack/join").Length == 1 && !f.Engine.PredictedDamage.Any() && f.Snapshot.GoalAreas.Count == 0, "Stack only soft target or invented raidwide.");
            var snapshot = f.Snapshot;
            var result = new SafeZoneSolver().Solve(snapshot, new(f.Actors[100].Position, 0, 6, false, false, 0, true, snapshot.Version),
                new(6, 6, 0, false, 6, 6), new SolveOptions { GridResolution = .25f, HardSafetyMargin = 0, ArenaSafetyMargin = 0 }, f.Now);
            var path = result.ContinuationPath.Count > 0 ? result.ContinuationPath : result.ExecutablePath;
            Check(result.Status == SolutionStatus.Success && !result.IsLeastDangerFallback && path.Count > 0
                && Vector3.Distance(path[^1].Position, f.Actors[101].Position) <= 6, "Hard stack solver failed.");
            Finish(f, 1); Check(!Aoes(f, "stack").Any(), "Cast stack finish.");
            using var arkas = Prepare(0x3EEA);
            arkas.Engine.Icon(100, 161, arkas.Now, 101); arkas.Render();
            Check(Aoes(arkas, "stack").Single(a => a.Display).Position == Quantized(arkas.Actors[101].Position)
                && Aoes(arkas, "stack/join").Single().Activation == arkas.Now.AddSeconds(5.2), "Actor icon timing.");
            arkas.Engine.Effect(1, 33366, 1, arkas.Now); Check(Aoes(arkas, "stack").Any(), "Visual hit cleared stack.");
            arkas.Engine.Effect(1, 34712, 2, arkas.Now, 101); arkas.Render();
            Check(!Aoes(arkas, "stack").Any() && !arkas.Engine.PredictedDamage.Any(), "Real stack effect leaked.");
        });
        run("Aetherfont stack capacity colors outline both views and marked exclusion", () =>
        {
            using var f = Prepare(0x3EEB); Cast(f, 1, 33343, 101);
            Check(Aoes(f, "stack").Single(a => a.Display).Safe, "Unmarked player could not join unfilled stack.");
            for (ulong id = 102; id <= 104; ++id)
                f.Actors[id] = new(id, 0, f.Actors[101].Position, PartySlot: (int)id - 100, Hostile: false);
            Scan(f);
            Check(!Aoes(f, "stack").Single(a => a.Display).Safe && Aoes(f, "stack/join").Length == 0,
                "Outside player allowed to overfill stack.");
            f.PlayerId = 101; Scan(f);
            Check(Aoes(f, "stack").Single(a => a.Display).Safe, "Own marker must remain safe colored.");
            var preview = f.Draw.Previews.Values.Single();
            Check(preview.OutlineOnly && preview.Style(new()).FillColor == Vector4.Zero, "Stack outline filled in shared radar/world style.");
        });
        run("Aetherfont Octomammoth player enters platforms without boss combat targetability priority and no water routes", () =>
        {
            using var f = Prepare(0x3EAA); var boss = f.Actors[1]; f.Engine.ResetFight();
            f.Actors[1] = boss with { Targetable = false, InCombat = false };
            f.Actors[100] = f.Actors[100] with { Position = new(-370, 0, -368) }; Scan(f);
            Check(f.Engine.Active == null, "Water pulled octopus.");
            f.Actors[100] = f.Actors[100] with { Position = new(-370, 0, -343) }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x3EAA, "Platform entry required boss combat/targetability.");
            f.Actors[1] = f.Actors[1] with { Targetable = true };
            f.Actors[10] = new(10, 0x3EAB, f.Actors[100].Position); Scan(f);
            Check(f.Engine.PriorityTarget(10, f.Actors[100].Position) == 1 && !f.Engine.ForbiddenTargets.Contains(10UL), "Boss priority or invented tentacle ban.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1126); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, boss == 0x3EAA ? new(-370, 0, -343) : c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, f.Actors[100].Position + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[f.PlayerId].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong source, uint action, ulong target = 0, double duration = 5)
    {
        var cast = new DungeonCastObservation(action, f.Actors[source].Position, .7f, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[source] = f.Actors[source] with { Cast = cast }; f.Engine.StartCast(source, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong source)
    {
        f.Actors[source] = f.Actors[source] with { Cast = null }; f.Engine.Finish(source, endedAt: f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Aoes(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("aetherfont/" + key)).ToArray();
    private static Vector3 Quantized(Vector3 p)
    {
        const float grid = 2000f / 65535f;
        return new((MathF.Round(p.X / grid) - .5f) * grid, p.Y, (MathF.Round(p.Z / grid) - .5f) * grid);
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
