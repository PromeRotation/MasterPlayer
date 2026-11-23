using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using Fight = HalataliRegression.Fight;

internal static class VanguardRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Vanguard client territory discovery trash and Zander both drawing geometry", () =>
        {
            Check(ArrDungeonCatalog.Create(1198).Length == 7 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.vanguard"), "Discovery.");
            var asm = typeof(BossMod.Dawntrail.Dungeon.D04Vanguard.D041CommanderR8.D041CommanderR8).Assembly;
            foreach (var (type, native) in new (string, IGeometryShape)[]
            {
                ("D040VanguardLeptocyon", VanguardArenas.Leptocyon(0)),
                ("D040VanguardSentryR7", VanguardArenas.Sentry(0)),
                ("D040VanguardAerostat2", VanguardArenas.Aerostat(0)),
            })
            {
                var original = (BossMod.ArenaBoundsCustom)asm.GetType($"BossMod.Dawntrail.Dungeon.D04Vanguard.{type}.{type}")!
                    .GetField("arena", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                Compare(native, p => original.Contains(new(p.X - original.Center.X, p.Z - original.Center.Z)), 65, .73f);
            }
            var z = BossMod.Dawntrail.Dungeon.D04Vanguard.D043Zander.D043Zander.StartingBounds;
            Compare(VanguardArenas.ZanderStarting(0), p => z.Contains(new(p.X - z.Center.X, p.Z - z.Center.Z)), 22, .23f);
        });
        foreach (var (boss, alias) in new (uint, uint)[] { (0x447A, 0x4478), (0x4479, 0x41D8), (0x41DA, 0x447B), (0x43D4, 0x41D6) })
            run($"Vanguard {boss:X} trash group survives primary death destruction and disengagement", () =>
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[2] = new(2, alias, c); Scan(f);
                f.Actors[1] = f.Actors[1] with { Dead = true, InCombat = false }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss && f.Engine.PrimaryId == 2, "Group lost after primary death.");
                f.Engine.RemoveActor(1); f.Actors.Remove(1); Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Group lost after original actor cleanup.");
                f.Actors[2] = f.Actors[2] with { Position = c + Vector3.UnitX * 200 }; Scan(f);
                Check(f.Engine.Active?.BossOid == boss, "Alive member outside initial arena ended group.");
                f.Actors[2] = f.Actors[2] with { Dead = true }; Scan(f);
                Check(f.Engine.Active == null, "All-dead group remains.");
            });
        run("Vanguard shared trash oid selects correct arena", () =>
        {
            using var f = Prepare(0x447A); f.Engine.ResetFight(); f.Actors.Remove(1);
            f.Actors[2] = new(2, 0x4478, VanguardArenas.Aerostat(0).Position, InCombat: true);
            f.Actors[100] = f.Actors[100] with { Position = f.Actors[2].Position }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x43D4, "Shared oid assigned to distant group.");
        });
        foreach (var (boss, action, shape) in new (uint, uint, BossMod.AOEShape)[]
        {
            (0x447A, 39017, new BossMod.AOEShapeCone(12, new(MathF.PI / 4))),
            (0x4479, 38451, new BossMod.AOEShapeDonut(4, 10)),
            (0x4479, 39018, new BossMod.AOEShapeCircle(6)),
            (0x41DA, 38452, new BossMod.AOEShapeDonut(3, 12)),
            (0x43D4, 38060, new BossMod.AOEShapeRect(50, 2)),
            (0x411D, 36570, new BossMod.AOEShapeCircle(14)),
            (0x4237, 37156, new BossMod.AOEShapeCircle(3)),
            (0x4237, 39016, new BossMod.AOEShapeCircle(5)),
            (0x4237, 37350, new BossMod.AOEShapeCircle(6)),
            (0x411E, 36575, new BossMod.AOEShapeRect(20, 20)),
            (0x411E, 36589, new BossMod.AOEShapeRect(20, 2)),
            (0x411E, 36583, new BossMod.AOEShapeCone(19, new(MathF.PI / 4))),
        })
            run($"Vanguard {action} exact source shape both views actual finish no timeout", () =>
            {
                using var f = Prepare(boss); Cast(f, 10, action); var aoe = Group(f, "simple").Single();
                Compare(aoe.Shape.Resolve(aoe.Position, aoe.Rotation, null),
                    p => shape.Check(new(p.X, p.Z), new(aoe.Position.X, aoe.Position.Z), new(aoe.Rotation)));
                f.Now = f.Now.AddSeconds(40); f.Engine.Tick(f.Now); f.Render();
                Check(Group(f, "simple").Length == 1, "Clock cleared observed cast.");
                Finish(f, 10); Check(Group(f, "simple").Length == 0, "Finish not cleared.");
            });
        run("Vanguard charges originate at caster and use destination length", () =>
        {
            foreach (var (boss, action) in new (uint, uint)[] { (0x4479, 38051), (0x411D, 36569) })
            {
                using var f = Prepare(boss); var c = f.Engine.Arena!.Position;
                f.Actors[10] = new(10, 0x41BC, c + Vector3.UnitX * 3); Scan(f);
                Cast(f, 10, action, location: c + Vector3.UnitZ * 12);
                var a = Group(f, "simple").Single(); var delta = c + Vector3.UnitZ * 12 - f.Actors[10].Position;
                var shape = new BossMod.AOEShapeRect(delta.Length(), 2.5f);
                Compare(a.Shape.Resolve(a.Position, a.Rotation, null), p => shape.Check(new(p.X, p.Z),
                    new(a.Position.X, a.Position.Z), new(MathF.Atan2(delta.X, delta.Z))), 20);
            }
        });
        run("Vanguard Enhanced Mobility exact two sort and FIFO finish all risky", () =>
        {
            using var f = Prepare(0x411D); Cast(f, 10, 36563, 10); Cast(f, 11, 39140, 5); Cast(f, 12, 37184, 1);
            var aoes = Group(f, "cast");
            Check(aoes.Length == 3 && aoes[0].Id.Contains("/11/") && aoes[1].Id.Contains("/10/") && aoes[2].Id.Contains("/12/")
                && aoes.All(a => a.Navigate), "Enhanced sort or risk.");
            Finish(f, 12); Check(Group(f, "cast").First().Id.Contains("/10/"), "Finish should pop first, not matching caster.");
        });
        foreach (var action in new uint[] { 39140, 39141, 36559, 36560 })
            run($"Vanguard {action} rotary six components fixed rotation timing FIFO individual hits", () =>
            {
                using var f = Prepare(0x411D); Cast(f, 1, action, rotation: 1.3f);
                var aoes = Group(f, "rotary");
                var angle = action is 39140 or 36559 ? -MathF.PI / 3 : MathF.PI / 3;
                Check(aoes.Length == 6 && aoes.All(a => a.Navigate) && Math.Abs(aoes[0].Rotation - angle) < .00001
                    && Near(aoes[0].Activation, f.Now, 6.8) && Near(aoes[4].Activation, f.Now, 7.4), "Rotary timing/rotation.");
                BossMod.AOEShape other = action is 39140 or 36560
                    ? new BossMod.AOEShapeDonutSector(17, 28, new(MathF.PI / 3)) : new BossMod.AOEShapeCone(14, new(MathF.PI / 3));
                Compare(aoes[1].Shape.Resolve(aoes[1].Position, angle, null),
                    p => other.Check(new(p.X, p.Z), new(aoes[1].Position.X, aoes[1].Position.Z), new(angle)), 30);
                Finish(f, 1); Check(Group(f, "rotary").Length == 6, "Boss finish cleared rotary.");
                f.Engine.Effect(20, 36567, 1, f.Now); Check(Group(f, "rotary").Length == 5, "Individual effect popped pair.");
                f.Engine.Effect(20, 36567, 1, f.Now); Check(Group(f, "rotary").Length == 5, "Duplicate advanced.");
                for (uint i = 2; i <= 6; ++i) f.Engine.Effect(20, 36565, i, f.Now);
                Check(Group(f, "rotary").Length == 0, "Six effects not cleared.");
            });
        run("Vanguard simple limits risk danger color and promotion", () =>
        {
            using var f = Prepare(0x4237);
            for (ulong i = 10; i < 15; ++i) Cast(f, i, 37151);
            var aoes = Group(f, "simple");
            Check(aoes.Length == 4 && aoes.Count(a => a.Navigate) == 2 && aoes.Count(a => a.Color != null) == 2, "Blast max/risk/color.");
            Finish(f, 10); Finish(f, 11); Finish(f, 12);
            Check(Group(f, "simple").All(a => a.Navigate && a.Color == null), "SimpleAOEs must recompute risk/color.");
            foreach (var id in new ulong[] { 13, 14 }) Finish(f, id);
            for (ulong i = 20; i < 25; ++i) Cast(f, i, 37155);
            Check(Group(f, "simple").Length == 5 && Group(f, "simple").All(a => a.Navigate)
                && Group(f, "simple").Count(a => a.Color != null) == 4, "Homing count/risk/color.");
        });
        run("Vanguard Battery Circuit caches nine risky predictions and advances 34 matching actual hits", () =>
        {
            using var f = Prepare(0x4237); Cast(f, 10, 37351, rotation: .7f);
            var aoes = Group(f, "battery"); var at = aoes[0].Activation;
            Check(aoes.Length == 9 && aoes.All(a => a.Navigate) && aoes.Last().Id.EndsWith("/0")
                && aoes.Last().Color != null && Near(aoes[0].Activation, f.Now, 5.5), "Battery initial.");
            f.Now = f.Now.AddSeconds(10); f.Engine.Tick(f.Now); f.Render();
            Check(Group(f, "battery")[0].Activation == at, "Cache drifted.");
            Finish(f, 10); Check(Group(f, "battery").Length == 9, "Finish cleared sequence.");
            f.Actors[11] = new(11, 0x233C, f.Engine.Arena!.Position, Rotation: .9f); Scan(f);
            f.Engine.Effect(11, 37344, 1, f.Now); Check(Group(f, "battery").Last().Rotation == .7f, "Nonmatching rotation advanced.");
            for (uint i = 0; i < 34; ++i)
            {
                f.Actors[11] = f.Actors[11] with { Rotation = .7f - i * 11 * MathF.PI / 180 }; Scan(f);
                f.Engine.Effect(11, i == 0 ? 37351u : 37344u, i + 2, f.Now); f.Render();
                if (i < 33) Check(Near(Group(f, "battery").Last().Activation, f.Now, .5), "Actual event didn't resync time.");
                if (i == 32) Check(Group(f, "battery").Single().Color == null, "Last hit color.");
            }
            Check(Group(f, "battery").Length == 0, "Last hit not cleared.");
        });
        run("Vanguard arena shrink waits map and all four fence topology changes after three seconds", () =>
        {
            foreach (var (boss, action, index) in new (uint, uint, uint)[] { (0x411D, 36571, 0xA), (0x4237, 37161, 0xC), (0x411E, 36594, 0) })
            {
                using var f = Prepare(boss); Cast(f, 1, action);
                Check(Near(Group(f, "shrink").Single().Activation, f.Now, boss == 0x411E ? 5.5 : 5.4), "Shrink timing.");
                Finish(f, 1); Check(Group(f, "shrink").Length == 1, "Finish cleared shrink.");
                f.Engine.MapEffect(index, 0x00020001); Check(Group(f, "shrink").Length == 0, "Map not clear.");
            }
            var asm = typeof(BossMod.Dawntrail.Dungeon.D04Vanguard.D042Protector.D042Protector).Assembly;
            var t = asm.GetType("BossMod.Dawntrail.Dungeon.D04Vanguard.D042Protector.ArenaChanges")!;
            var names = new[] { "01000080", "08000400", "00020001", "00200010" };
            for (var i = 0; i < 4; ++i)
            {
                using var f = Prepare(0x4237); f.Engine.MapEffect(0xC, 0x00020001);
                f.Engine.MapEffect(0xD, Convert.ToUInt32(names[i], 16), f.Now);
                var aoe = Group(f, "fence").Single();
                var original = (BossMod.AOEShapeCustom)t.GetField("electricFences" + names[i] + "AOE", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
                // Check each source contour independently: the upstream union's boundary index throws for these disconnected fence shapes.
                var shapes = (IReadOnlyList<BossMod.Shape>)typeof(BossMod.AOEShapeCustom)
                    .GetField("Shapes1", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(original)!;
                var polygons = shapes.Select(s => s.ToPolygon(default)).ToArray();
                bool Fence(Vector3 p) => polygons.Any(poly => poly.Contains(new(p.X, p.Z)));
                Compare(aoe.Shape.Resolve(aoe.Position, 0, null), Fence, 25, .23f);
                f.Now = f.Now.AddSeconds(2.999); Scan(f); Check(Group(f, "fence").Length == 1, "Fence early.");
                f.Engine.MapEffect(0xD, 0x00080004); f.Now = f.Now.AddSeconds(.001); Scan(f);
                Check(Group(f, "fence").Length == 0, "Fence not committed after clear-before-activation.");
                Compare(f.Engine.Arena!, p => MathF.Abs(p.X) <= 12 && MathF.Abs(p.Z + 100) <= 20 && !Fence(p), 25, .23f);
                f.Engine.MapEffect(0xD, 0x00080004); Check(f.Engine.Arena is RectangleGeometry, "Fence reset.");
            }
        });
        run("Vanguard acceleration bomb hint three seconds control strict one second both status ids perspective death loss", () =>
        {
            foreach (var status in new uint[] { 3802, 4144 })
            {
                using var f = Prepare(0x4237); var expires = f.Now.AddSeconds(6);
                f.Engine.StatusChanged(100, status, true, f.Now, expiresAt: expires);
                f.Now = expires.AddSeconds(-3); f.Engine.Tick(f.Now);
                Check(f.Engine.Hints(f.Actors[100].Position).Any(h => h.Contains("加速度")) && !f.Engine.RequireAllStop, "Hint window.");
                f.Now = expires.AddSeconds(-1); f.Engine.Tick(f.Now); Check(!f.Engine.RequireAllStop, "One second equality should not stop.");
                f.Now = f.Now.AddTicks(1); f.Engine.Tick(f.Now); Check(f.Engine.RequireAllStop, "Control missing.");
                f.PlayerId = 101; Scan(f); Check(!f.Engine.RequireAllStop, "Old player hold.");
                f.PlayerId = 100; Scan(f); Check(f.Engine.RequireAllStop, "Perspective restore.");
                f.Now = expires.AddSeconds(2); f.Engine.Tick(f.Now); Check(f.Engine.RequireAllStop, "Expired before actual status lose.");
                f.Engine.StatusChanged(100, status, false, f.Now); Check(!f.Engine.RequireAllStop, "Status lose leaked.");
                f.Engine.StatusChanged(100, status, true, f.Now, expiresAt: f.Now);
                f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f); Check(!f.Engine.RequireAllStop, "Death hold leaked.");
            }
        });
        run("Vanguard bomb snapshot loss reapplication revival and missing event duration recovery", () =>
        {
            using var f = Prepare(0x4237);
            f.Engine.StatusChanged(100, 3802, true, f.Now);
            f.Actors[100] = f.Actors[100] with { Statuses = [3802], StatusExpirations = new Dictionary<uint, DateTime> { [3802] = f.Now.AddSeconds(.5) } };
            Scan(f); Check(f.Engine.RequireAllStop, "Missing duration could not recover.");
            f.Now = f.Now.AddSeconds(1); Scan(f); Check(f.Engine.RequireAllStop, "Expiry alone removed snapshot state.");
            f.Actors[100] = f.Actors[100] with { Statuses = [], StatusExpirations = new Dictionary<uint, DateTime>() };
            Scan(f); Check(!f.Engine.RequireAllStop, "Snapshot loss leaked stop.");
            f.Actors[100] = f.Actors[100] with { Statuses = [4144], StatusExpirations = new Dictionary<uint, DateTime> { [4144] = f.Now } };
            Scan(f); Check(f.Engine.RequireAllStop, "Second snapshot bomb blocked.");
            f.Actors[100] = f.Actors[100] with { Dead = true }; Scan(f); Check(!f.Engine.RequireAllStop, "Dead state hold.");
            f.Actors[100] = f.Actors[100] with { Dead = false }; Scan(f); Check(f.Engine.RequireAllStop, "Revived snapshot recovery.");
            f.Engine.StatusChanged(100, 4144, false, f.Now); Scan(f); Check(!f.Engine.RequireAllStop, "Stale snapshot resurrected removal.");
            f.Now = f.Now.AddSeconds(1); Scan(f); Check(!f.Engine.RequireAllStop, "Unchanged old snapshot resurrected after event grace.");
        });
        run("Vanguard line marker dynamic source real target correction duplicate marks and shared damage", () =>
        {
            using var f = Prepare(0x4237); var c = f.Engine.Arena!.Position;
            f.Actors[10] = new(10, 0x233C, c - Vector3.UnitZ * 10); Scan(f);
            f.Engine.Effect(10, 37347, 1, f.Now, 100); f.Engine.Effect(10, 37347, 2, f.Now, 100);
            Check(Group(f, "line").Count(a => a.Display) == 2 && f.Engine.PredictedDamage.Count(p => p.DamageType == "Shared") == 2, "Duplicate marks/damage.");
            Cast(f, 1, 37345); Finish(f, 1); Check(Group(f, "line").Count(a => a.Display) == 2, "Cast finish cleared event marker.");
            f.Engine.Effect(10, 37345, 3, f.Now, 101); Check(Group(f, "line").Count(a => a.Display) == 2, "Multiple marks corrected target.");
            f.Engine.Effect(10, 37345, 4, f.Now, 100);
            f.Actors[10] = f.Actors[10] with { Position = c + Vector3.UnitX * 2 }; Scan(f);
            var line = Group(f, "line").Single(a => a.Display);
            Check(line.Position == f.Actors[10].Position, "Line replaced source by boss.");
            f.Engine.Effect(10, 37345, 5, f.Now, 101); Check(Group(f, "line").Length == 0, "Single marker correction failed.");
        });
        run("Vanguard Zander combo top two opposite risk persistent FIFO and delayable phase cancellation", () =>
        {
            using var f = Prepare(0x411E);
            Cast(f, 10, 36592, 5, 0); Cast(f, 11, 36591, 8, MathF.PI); Cast(f, 12, 36593, 10, .4f);
            Check(Group(f, "cast").Length == 2 && Group(f, "cast")[0].Navigate && !Group(f, "cast")[1].Navigate, "Combo display/risk.");
            var second = Group(f, "cast")[1];
            Compare(second.Shape.Resolve(second.Position, second.Rotation, null), p => new BossMod.AOEShapeRect(20, 40)
                .Check(new(p.X, p.Z), new(second.Position.X, second.Position.Z), new(second.Rotation)));
            Finish(f, 12); Check(Group(f, "cast").First().Navigate, "Combo promotion.");
            Cast(f, 1, 36595, target: 100); Finish(f, 1);
            Check(f.Engine.PredictedDamage.Count() == 1, "Delayable finish cleared buster.");
            f.Engine.Effect(1, 36576, 1, f.Now); Check(!f.Engine.PredictedDamage.Any(), "Phase didn't cancel buster.");
            Check(f.Engine.ForbiddenTargets.Length == 0 && !f.Engine.RequireAllStop, "Invented shield/stop.");
            f.Engine.ResetFight(); f.Render(); Check(!f.Engine.AmbientAoes().Any() && f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Reset leaked.");
        });
        run("Vanguard active boss survives disengagement P2 and delayable busters resolve by target", () =>
        {
            using var f = Prepare(0x411E);
            Cast(f, 10, 36592);
            f.Actors[1] = f.Actors[1] with { Targetable = false, InCombat = false, TargetId = 100 }; Scan(f);
            Check(f.Engine.Active?.BossOid == 0x411E && Group(f, "cast").Length == 1, "Phase transition cleared session.");
            f.Actors[2] = new(2, 0x41BA, f.Engine.Arena!.Position, InCombat: true); Scan(f);
            Cast(f, 2, 36597, target: 100);
            Check(f.Engine.PredictedDamage.Single().TargetId == 100, "P2 caster not recognized.");
            Finish(f, 2);
            Cast(f, 1, 36595, target: 1); Finish(f, 1);
            f.Now = f.Now.AddSeconds(1); Cast(f, 1, 36595, target: 101); Finish(f, 1);
            Check(f.Engine.PredictedDamage.Count() == 2, "Delayed target list overwritten or self target unresolved.");
            f.Engine.Effect(1, 36595, 10, f.Now, 100);
            Check(f.Engine.PredictedDamage.Single().TargetId == 101, "Actual hit cleared other target.");
            f.Engine.Effect(1, 36595, 11, f.Now, 101); Check(!f.Engine.PredictedDamage.Any(), "Final target not consumed.");
        });
    }

    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 1198); f.Engine.ResetFight();
        var c = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
        f.Actors.Clear(); f.Actors[1] = new(1, boss, c, InCombat: true);
        f.Actors[100] = new(100, 0, c, PartySlot: 0, Hostile: false);
        f.Actors[101] = new(101, 0, c + Vector3.UnitX * 8, PartySlot: 1, Hostile: false);
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
    private static DungeonAmbientAoe[] Group(Fight f, string key) => f.Engine.AmbientAoes().Where(a => a.Id.StartsWith("vanguard/" + key)).ToArray();
    private static bool Near(DateTime? at, DateTime start, double seconds) => at is { } value && Math.Abs((value - start).TotalSeconds - seconds) < .00001;
    private static void Compare(IGeometryShape native, Func<Vector3, bool> expected, float range = 55, float step = 1.17f)
    {
        var world = HostDrawingGeometry.Convert(native);
        for (var x = -range + .137f; x < range; x += step)
            for (var z = -range + .231f; z < range; z += step)
            {
                var p = native.Position + new Vector3(x, 0, z); var wanted = expected(p);
                Check(native.Contains(p, 0) == wanted && world.Contains(p, 0) == wanted, $"Shape at {p}.");
            }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
