using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class TenderValleyRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("TenderValley native discovery territory ownership four encounters", () =>
        {
            Check(ArrDungeonCatalog.Create(1203).Length == 4 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.tender-valley"), "Discovery.");
            foreach (var boss in new uint[] { 0x4234, 0x41BE, 0x4253, 0x4164 })
                Check(ArrDungeonCatalog.Owns(boss, 1203) && !ArrDungeonCatalog.Owns(boss, 1199), "Precise ownership.");
        });
        foreach (var (boss, type) in new (uint, Type)[]
        {
            (0x41BE, typeof(BossMod.Dawntrail.Dungeon.D07TenderValley.D072Anthracite.D072Anthracite)),
            (0x4253, typeof(BossMod.Dawntrail.Dungeon.D07TenderValley.D073YokHuyAttestant.D073YokHuyAttestant)),
        })
            run($"TenderValley {boss:X} exact holed arena radar and world match resolved source polygons", () =>
            {
                using var f = Prepare(boss);
                var tuple = type.GetMethod("BuildArena", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
                var bounds = (BossMod.ArenaBoundsCustom)tuple.GetType().GetField("Item2")!.GetValue(tuple)!;
                Compare(f.Engine.Arena!, p => bounds.Contains(new(p.X - bounds.Center.X, p.Z - bounds.Center.Z)), 27, .29f);
            });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x4234, 37386, new BossMod.AOEShapeCone(36, new(MathF.PI * 25 / 180))),
            (0x4234, 39154, new BossMod.AOEShapeCone(36, new(MathF.PI * 165 / 180))),
            (0x4234, 39155, new BossMod.AOEShapeCone(36, new(MathF.PI * 165 / 180))),
            (0x41BE, 36401, new BossMod.AOEShapeCircle(10)), (0x41BE, 36402, new BossMod.AOEShapeCircle(10)),
            (0x41BE, 36545, new BossMod.AOEShapeRect(40, 3)), (0x41BE, 36551, new BossMod.AOEShapeRect(40, 3)),
            (0x4253, 39221, new BossMod.AOEShapeCircle(8)), (0x4253, 38539, new BossMod.AOEShapeCircle(6)),
            (0x4164, 36749, new BossMod.AOEShapeCircle(9)), (0x4164, 36750, new BossMod.AOEShapeRect(52, 2.5f)),
            (0x4164, 36754, new BossMod.AOEShapeCircle(6)),
        })
            run($"TenderValley {action} native geometry dual view real finish and no timeout consumption", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action);
                var aoe = Group(f, "simple").Single();
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null),
                    p => shape.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation)));
                f.Engine.Effect(10, action, 1, f.Now); Check(Group(f, "simple").Length == 1, "Effect consumed cast.");
                f.Now = f.Now.AddSeconds(90); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "simple").Length == 1, "Time consumed source caster.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Real finish retained caster.");
            });
        foreach (var (boss, action, index, offset) in new (uint, uint, uint, double)[] { (0x4234, 37384, 3, .7), (0x4164, 36748, 0, 4.8) })
            run($"TenderValley {boss:X} different shrink trigger actual finish clock and map confirmation", () =>
            {
                using var f = Prepare(boss); var initial = f.Now; var arena = f.Engine.Arena;
                Cast(f, 10, action);
                Check(Group(f, "shrink").Length == (boss == 0x4234 ? 1 : 0), "Shrink start trigger.");
                f.Now = f.Now.AddSeconds(6); Finish(f, 10);
                Check(Near(Group(f, "shrink").Single().Activation, initial, 5 + offset), "Shrink used received finish time.");
                f.Now = f.Now.AddSeconds(30); f.Engine.Tick(f.Now);
                Check(ReferenceEquals(arena, f.Engine.Arena), "Prediction shrank arena.");
                f.Engine.MapEffect(index, 0x00020001, f.Now); f.Render();
                Check(Group(f, "shrink").Length == 0 && !ReferenceEquals(arena, f.Engine.Arena), "Map did not confirm.");
            });
        run("TenderValley cactus first eight snapshot clock new wave default and finish FIFO hide", () =>
        {
            using var f = Prepare(0x4234); var c = f.Engine.Arena!.Position; var initial = f.Now;
            for (ulong i = 20; i < 30; ++i) f.Actors[i] = new(i, i % 2 == 0 ? 0x1EBBF0u : 0x1EBBF1u, c + Vector3.UnitX * (i - 20));
            Scan(f); Check(Group(f, "cactus").Length == 0, "Inactive creation displayed.");
            Cast(f, 1, 37387, 3);
            var q = Group(f, "cactus"); Check(q.Length == 8 && q.All(a => Near(a.Activation, initial, 16.7)), "Cactus first wave clock/window.");
            var locked = q[0].Position; f.Actors[20] = f.Actors[20] with { Position = c + Vector3.UnitZ * 9, Dead = true }; Scan(f);
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f);
            Check(Group(f, "cactus")[0].Position == locked, "Creation snapshot removed or moved.");
            Cast(f, 10, 37388); f.Engine.Effect(10, 37388, 1, f.Now); Check(Group(f, "cactus").Length == 8, "Effect consumed cacti.");
            Finish(f, 10); Check(Group(f, "cactus").Length == 0, "First finish did not hide remaining wave.");
            Finish(f, 1); f.Now = f.Now.AddSeconds(.3); Cast(f, 1, 37387, 3);
            Check(Group(f, "cactus").Length == 8 && Group(f, "cactus")[0].Shape is CircleTemplate { Radius: 11 }, "FIFO matched caster or size.");
        });
        run("TenderValley cactus display pause needles knockback risk and immunity independent AI", () =>
        {
            using var f = Prepare(0x4234); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x1EBBF0, c + Vector3.UnitX * 12); Scan(f); Cast(f, 1, 37387); Finish(f, 1);
            Cast(f, 10, 37390); Check(Group(f, "cactus").Single() is { Display: true, Navigate: false }, "KB hid display or retained risk.");
            Check(Group(f, "kb/nav").Length == 1, "KB custom AI.");
            Cast(f, 11, 37386); Check(Group(f, "cactus").Length == 0 && Group(f, "kb/nav").Length == 1, "Needles did not hide only cactus output.");
            Finish(f, 11); f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(Group(f, "cactus").Single().Navigate && Group(f, "kb").Length == 0, "Immune risk/custom preview.");
            f.Actors[100] = f.Actors[100] with { StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(5) } }; Scan(f);
            Check(!Group(f, "cactus").Single().Navigate && Group(f, "kb/nav").Length == 1, "Expiration equality should not be immune.");
        });
        run("TenderValley cactus knockback source SDF uses raw first eight expanded radii even inactive", () =>
        {
            using var f = Prepare(0x4234); var c = f.Engine.Arena!.Position;
            for (ulong i = 20; i < 29; ++i) f.Actors[i] = new(i, i % 2 == 0 ? 0x1EBBF0u : 0x1EBBF1u, c + new Vector3(((long)i - 24) * 3, 0, 11));
            Scan(f); Cast(f, 10, 37390, location: c + Vector3.UnitX * 2);
            var nav = Group(f, "kb/nav").Single().Shape.Resolve(c, 0, null);
            var circles = Enumerable.Range(0, 8).Select(i => (new BossMod.WPos(c.X + (i - 4) * 3, c.Z + 11).Quantized(), i % 2 == 0 ? 7f : 12f)).ToArray();
            var source = new BossMod.SDKnockbackInAABBSquareAwayFromOriginPlusAOECirclesMixedRadii(new(c.X, c.Z), new(c.X + 2, c.Z), 20, 19, circles, 8);
            CompareContains(nav, p => source.Contains(new(p.X, p.Z)), 24, .43f);
            Check(Group(f, "cactus").Length == 0, "Inactive cacti displayed.");
        });
        run("TenderValley sixteen real needle effects threshold persists across waves first origin", () =>
        {
            using var f = Prepare(0x4234); var c = f.Engine.Arena!.Position;
            for (var i = 0; i < 16; ++i) { Cast(f, 10, 37386); Finish(f, 10); f.Now = f.Now.AddSeconds(.3); }
            Cast(f, 11, 37386, location: c + Vector3.UnitX * 2); Check(Group(f, "needles").Length == 0, "Finish counted as effect.");
            for (var i = 0; i < 16; ++i) f.Engine.Effect(11, 37386, (uint)i + 1, f.Now);
            var center = (SohrKhaiWallHintTemplate)Group(f, "needles").Single().Shape;
            Check(center.Shelters.Single() is CircleGeometry { Radius: 3 } && center.Shelters[0].Position == c + Vector3.UnitX * 2, "Threshold first origin.");
            Finish(f, 11); Cast(f, 12, 37386); Check(Group(f, "needles").Length == 1, "Count reset per wave.");
        });
        foreach (var (x, z, length) in new[] { (-112.5f, -486.5f, 22f), (-147.5f, -471.5f, 22f), (-147.5f, -486.5f, 12f), (-112.5f, -471.5f, 12f), (-130f, -490f, 35f) })
            run($"TenderValley statue tether at {x} {z} snapshot {length} actual event clears all", () =>
            {
                using var f = Prepare(0x4253); f.Actors[20] = new(20, 0x4254, new(x, 0, z), Rotation: .7f); Scan(f);
                f.Engine.Tether(1, 37, true, f.Now, 20); f.Engine.Tether(1, 37, true, f.Now, 20);
                var a = Group(f, "statue"); Check(a.Length == 2 && Near(a[0].Activation, f.Now, 8.1), "Tether duplicate/time.");
                Compare(a[0].Shape.Resolve(a[0].Position, a[0].Rotation, null), p => new BossMod.AOEShapeRect(length, 4).Check(new(p.X, p.Z), new BossMod.WPos(x, z).Quantized(), new(.7f)));
                f.Actors[20] = f.Actors[20] with { Position = new(-130, 0, -475), Rotation = 2 }; Scan(f);
                f.Engine.Tether(1, 37, false, f.Now, 20); f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now);
                Check(Group(f, "statue").Length == 2 && Group(f, "statue")[0].Position == a[0].Position, "Snapshot timed out or untether moved it.");
                f.Engine.Effect(20, 39824, 1, f.Now); Check(Group(f, "statue").Length == 0, "Event did not clear all.");
            });
        run("TenderValley existing statue tethers restore exactly once on activation", () =>
        {
            using var f = Prepare(0x4253); f.Actors[20] = new(20, 0x4254, new(-112.5f, 0, -486.5f), Tethers: new Dictionary<uint, ulong> { [37] = 1 });
            Scan(f); Scan(f); Check(Group(f, "statue").Length == 1, "Existing tether restoration.");
            f.Engine.Effect(20, 39825, 1, f.Now); Scan(f); Check(Group(f, "statue").Length == 0, "Stale tether restored after resolve.");
        });
        foreach (var (state, index) in new (uint, int)[] { (0x01000080, 0), (0x04000200, 1), (0x10000800, 2), (0x00020001, 3) })
            run($"TenderValley labyrinth {index} exact hazard dual view green tiles hard delayed union", () =>
            {
                using var f = Prepare(0x4164); var c = f.Engine.Arena!.Position;
                f.Engine.MapEffect(1, state, f.Now); f.Render();
                var a = Group(f, "labyrinth");
                var sourceType = typeof(BossMod.Dawntrail.Dungeon.D07TenderValley.D074GreatestSerpentOfTural.D074GreatestSerpentOfTural).Assembly
                    .GetType("BossMod.Dawntrail.Dungeon.D07TenderValley.D074GreatestSerpentOfTural.GreatestLabyrinth")!;
                var shape = ((BossMod.AOEShapeCustom[])sourceType.GetField("forbiddenShapes", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)[index];
                var danger = a.Single(x => x.Id.EndsWith("/danger"));
                Compare(danger.Shape.Resolve(c, 0, null), p => shape.Check(new(p.X, p.Z), new(c.X, c.Z), default), 15, .39f);
                Check(a.Count(x => x.Safe && x.Display) == 2 && a.All(x => !x.Safe || !x.Navigate), "Green tiles became soft goals.");
                var enter = a.Single(x => x.Id.EndsWith("/enter"));
                Check(Near(enter.Activation, f.Now, 10) && enter.Shape.Resolve(c, 0, null).Contains(c, 0), "Delayed hard union allows middle.");
                f.Now = f.Now.AddSeconds(90); f.Engine.Tick(f.Now); Check(Group(f, "labyrinth").Length == 4, "Labyrinth timed out.");
                f.Engine.MapEffect(1, 0x01000080, f.Now); Check(Group(f, "labyrinth").Length == 8, "Second map replaced queue.");
                f.Engine.MapEffect(1, 0x00100004, f.Now); Check(Group(f, "labyrinth").Length == 0, "Clear state.");
            });
        run("TenderValley sludge first per oid state seven blocks later actors destruction reveals next", () =>
        {
            using var f = Prepare(0x4164); var c = f.Engine.Arena!.Position;
            f.Actors[20] = new(20, 0x1EBA86, c, EventState: 7); f.Actors[21] = new(21, 0x1EBA86, c + Vector3.UnitX * 4);
            f.Actors[22] = new(22, 0x1EBA87, c, Dead: true); f.Actors[23] = new(23, 0x1EBA88, c); Scan(f);
            var a = Group(f, "sludge"); Check(a.Length == 2 && a.All(x => x.Obstacle), "Skipped inactive first or dead first.");
            f.Engine.RemoveActor(20); f.Actors.Remove(20); Scan(f); a = Group(f, "sludge");
            Check(a.Length == 3 && a.Single(x => x.Id.EndsWith("/21")).Shape is CircleTemplate { Radius: 6 }, "Destroy failed to promote.");
            f.Actors[21] = f.Actors[21] with { Position = c + Vector3.UnitX * 8 }; Scan(f);
            Check(Group(f, "sludge").Single(x => x.Id.EndsWith("/21")).Position == c + Vector3.UnitX * 8, "Voidzone not dynamic.");
        });
        run("TenderValley torrent first ten promotion simple bombs retain arrival ordering", () =>
        {
            using var f = Prepare(0x4164);
            for (ulong i = 10; i < 22; ++i) Cast(f, i, 36754, 30 - i);
            Check(Group(f, "simple").Length == 10 && Group(f, "simple")[0].Id.Contains("/10/"), "Torrent window sorted.");
            Finish(f, 10); Check(Group(f, "simple").Length == 10 && Group(f, "simple").Any(a => a.Id.Contains("/20/")), "Torrent not promoted.");
        });
        run("TenderValley grouped bomb helpers retain arrival order until ninety nine then persist sorted order", () =>
        {
            using var f = Prepare(0x41BE);
            for (ulong i = 10; i < 108; ++i) Cast(f, i, i % 2 == 0 ? 36401u : 36402u, 200 - i);
            Check(Group(f, "simple")[0].Id.Contains("/10/"), "Grouped helpers sorted before threshold.");
            Cast(f, 108, 36402, 2);
            Check(Group(f, "simple")[0].Id.Contains("/108/"), "Grouped ninety ninth did not sort.");
            Finish(f, 108);
            Check(Group(f, "simple")[0].Id.Contains("/107/"), "Sorted group lost order after consumption.");
        });
        foreach (var (icon, action, radius) in new (uint, uint, float)[] { (62, 39983, 6), (542, 39982, 5), (543, 39981, 4) })
            run($"TenderValley icon {icon} independent stack radius float clock target fallback and no invented prediction", () =>
            {
                using var f = Prepare(0x4164);
                f.Engine.Icon(100, icon, f.Now); f.Engine.Icon(101, icon == 62 ? 542u : 62u, f.Now);
                var display = Group(f, "stack").Single(a => a.Display && a.Shape is CircleTemplate s && s.Radius == radius);
                var gather = Group(f, "stack").Single(a => a.Id.EndsWith("/gather"));
                Check(((SohrKhaiWallHintTemplate)gather.Shape).Shelters.Any(s => s.Position == f.Actors[101].Position)
                    && Near(gather.Activation, f.Now, (double)4.6f), "Independent component candidates/float delay.");
                Check(Group(f, "stack/join").Length == 1 && !f.Engine.PredictedDamage.Any(), "Cross size marked/Shared guard.");
                f.Engine.Effect(10, action, 1, f.Now, 0); Check(Group(f, "stack").Count(a => a.Display) == 1, "Fallback cleared across components.");
            });
        run("TenderValley icon spread and bait prediction clocks resolve FIFO and no elapsed cleanup", () =>
        {
            using var f = Prepare(0x4164); f.Engine.Icon(100, 341, f.Now); f.Engine.Icon(101, 139, f.Now);
            Check(Group(f, "bait").Single(a => a.Display).OutlineOnly && Near(Group(f, "bait").First(a => a.Navigate).Activation, f.Now, 5.3), "Bait own/time.");
            Check(f.Engine.PredictedDamage.Any(d => d.TargetId == 100 && d.DamageType == "Tankbuster")
                && f.Engine.PredictedDamage.Any(d => d.TargetId == 101 && d.DamageType == "Raidwide" && Near(d.Activation, f.Now, 5.1)), "Predictions.");
            f.Now = f.Now.AddSeconds(60); f.Engine.Tick(f.Now);
            Check(Group(f, "bait").Any() && Group(f, "spread").Any(), "Icon timeout.");
            f.Engine.Effect(10, 36757, 1, f.Now, 999); f.Engine.Effect(10, 36755, 2, f.Now, 0);
            Check(!Group(f, "bait").Any() && !Group(f, "spread").Any(), "Resolve fallback.");
        });
        foreach (var (boss, action) in new (uint, uint)[] { (0x4234, 37391), (0x41BE, 36555), (0x41BE, 36553) })
            run($"TenderValley {action} cast target FIFO corrections no damage and destruction cache", () =>
            {
                using var f = Prepare(boss);
                Cast(f, 10, action, 9, target: 101); var old = f.Actors[10].Cast!;
                Cast(f, 11, action, 4, target: 101); Finish(f, 11);
                var group = action == 36553 ? "spread" : "stack";
                Check(Group(f, group).Count(a => a.Display) == 1, "Cast target FIFO.");
                f.Engine.StartCast(10, old with { Location = old.Location + Vector3.UnitX }, f.Now);
                Check(Group(f, group).Count(a => a.Display) == 1 && !f.Engine.PredictedDamage.Any(), "Correction resurrection or invented damage.");
                f.Engine.RemoveActor(101); f.Actors.Remove(101); Scan(f);
                Check(Group(f, group).Count(a => a.Display) == 1, "Destroyed target last state lost.");
                Finish(f, 10); Check(Group(f, group).Length == 0, "FIFO cleanup.");
            });
        run("TenderValley cast spread death hides then revives icon spread death consumes permanently", () =>
        {
            using (var f = Prepare(0x41BE))
            {
                Cast(f, 10, 36553, target: 101);
                f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
                Check(Group(f, "spread").Length == 0, "Dead cast target still active.");
                f.Actors[101] = f.Actors[101] with { Dead = false }; Scan(f);
                Check(Group(f, "spread").Length == 2, "Cast target cache lost on death.");
            }
            using (var f = Prepare(0x4164))
            {
                f.Engine.Icon(101, 139, f.Now);
                f.Actors[101] = f.Actors[101] with { Dead = true }; Scan(f);
                f.Actors[101] = f.Actors[101] with { Dead = false }; Scan(f);
                Check(Group(f, "spread").Length == 0, "Icon spread restored after death.");
            }
        });
        run("TenderValley flood first custom inset eleven immunity and raidwide independent", () =>
        {
            using var f = Prepare(0x4164); var c = f.Engine.Arena!.Position;
            Cast(f, 10, 36756, location: c + Vector3.UnitX * 3); Cast(f, 11, 36756, location: c - Vector3.UnitX * 3);
            var source = new BossMod.SDKnockbackInAABBSquareAwayFromOrigin(new(c.X, c.Z), new(c.X + 3, c.Z), 15, 11);
            CompareContains(Group(f, "kb/nav").Single().Shape.Resolve(c, 0, null), p => source.Contains(new(p.X, p.Z)), 15, .37f);
            f.Actors[100] = f.Actors[100] with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = f.Now.AddSeconds(10) } }; Scan(f);
            Check(Group(f, "kb").Length == 0 && f.Engine.PredictedDamage.Count() == 8, "Immunity disabled raidwide.");
        });
        foreach (var boss in new uint[] { 0x4234, 0x41BE, 0x4253, 0x4164 })
            run($"TenderValley {boss:X} transient disengage death owner cleanup no invented action controls", () =>
            {
                using var f = Prepare(boss); f.Actors[1] = f.Actors[1] with { InCombat = false, Targetable = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.Gazes.Any(), "Transient flags or invented restrictions.");
                f.Actors[1] = f.Actors[1] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null && f.Draw.Previews.Count == 0 && !f.Engine.AmbientAoes().Any(), "Death owner cleanup.");
            });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1203); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        for (var i = 0; i < 4; ++i) f.Actors[(ulong)(100 + i)] = new((ulong)(100 + i), 0, c + Vector3.UnitX * (i * 4), PartySlot: i, Hostile: false);
        Scan(f); return f;
    }
    private static void Scan(Fight f) { f.Engine.Synchronize(f.Actors.Values, f.Actors[100].Position, f.Now, playerId: f.PlayerId); f.Render(); }
    private static void Cast(Fight f, ulong id, uint action, double duration = 5, float rotation = .7f, Vector3? location = null, ulong target = 0)
    {
        if (!f.Actors.ContainsKey(id)) f.Actors[id] = new(id, 0x233C, f.Engine.Arena!.Position);
        var cast = new DungeonCastObservation(action, location ?? f.Actors[id].Position, rotation, f.Now, f.Now.AddSeconds(duration), target);
        f.Actors[id] = f.Actors[id] with { Cast = cast }; Scan(f); f.Engine.StartCast(id, cast, f.Now); f.Render();
    }
    private static void Finish(Fight f, ulong id)
    {
        f.Actors[id] = f.Actors[id] with { Cast = null }; f.Engine.Finish(id, endedAt: f.Now); f.Render();
    }
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("tender/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static void CompareContains(IGeometryShape native, Func<Vector3, bool> expected, float range, float step)
    {
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z);
                Check(native.Contains(p, 0) == expected(p), $"Navigation at {p}.");
            }
    }
    private static void Compare(IGeometryShape native, Func<Vector3, bool> expected, float range = 55, float step = 1.17f)
    {
        var world = HostDrawingGeometry.Convert(native);
        CompareContains(native, expected, range, step);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z);
                Check(world.Contains(p, 0) == expected(p), $"World at {p}.");
            }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
