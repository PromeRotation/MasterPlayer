using System.Numerics;
using System.Reflection;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Radar;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;
using Fight = HalataliRegression.Fight;

internal static class AmaurotRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("Amaurot all 256 Therion platform masks exact source rounded inset both views", () =>
        {
            for (var mask = 0; mask < 256; ++mask) CompareArena(AmaurotArenas.Therion(mask, 0), SourceArena(mask), .63f);
            Check(AmaurotCatalog.Create().Length == 3 && ArrDungeonCatalog.IsScript("masterplayer.dungeon.amaurot"), "Three entries/default enable missing.");
            foreach (var d in AmaurotCatalog.Create()) Check(ArrDungeonCatalog.Owns(d.BossOid, 838) && !ArrDungeonCatalog.Owns(d.BossOid, 822), "Ownership escaped.");
        });
        run("Amaurot Bellwether exact source polygon subtraction both views", () =>
        {
            var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon.D06Amaurot.D062Bellwether.D062Bellwether")!;
            var source = (BossMod.ArenaBoundsCustom)type.GetField("arena", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            CompareArena(AmaurotArenas.Bellwether(0), source, .17f);
        });
        foreach (var (boss, action, oid, shape) in new (uint, uint, uint, BossMod.AOEShape)[]
        {
            (0x27B6, 15566, 0x27B6, new BossMod.AOEShapeCone(9, new(MathF.PI / 3))),
            (0x27B6, 15558, 0x233C, new BossMod.AOEShapeCircle(6)),
            (0x27B6, 15562, 0x233C, new BossMod.AOEShapeCircle(10)),
            (0x27B6, 17108, 0x29DC, new BossMod.AOEShapeCircle(10)),
            (0x27B6, 15564, 0x18D6, new BossMod.AOEShapeRect(35, 20)),
            (0x27B6, 15565, 0x27B6, new BossMod.AOEShapeCircle(10)),
            (0x27B6, 13642, 0x18D6, new BossMod.AOEShapeCircle(6)),
            (0x27B8, 15571, 0x2879, new BossMod.AOEShapeRect(40, 4)),
            (0x27B8, 15572, 0x18D6, new BossMod.AOEShapeCircle(4)),
            (0x27B8, 16765, 0x27BC, new BossMod.AOEShapeCircle(5)),
            (0x27C1, 15578, 0x27C1, new BossMod.AOEShapeCircle(20)),
            (0x27C1, 15586, 0x233C, new BossMod.AOEShapeCircle(6)),
        })
            run($"Amaurot action {action} source dimensions packet heading both views and cancel", () =>
            {
                using var f = Prepare(boss); var id = oid == boss ? 1UL : 10UL;
                if (id == 10) { f.Actors[10] = new(10, oid, f.Actors[1].Position); Scan(f); }
                Cast(f, id, action, 5, .7f); var cast = f.Engine.Casts[id];
                Compare(cast.Rule.Shape!, cast.Location, cast.Rotation, shape);
                Check(f.Draw.Previews.Count == 1 && f.Snapshot.DangerAreas.Count == 1, "AOE duplicate or missing.");
                f.Engine.Finish(id); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Cancel leaked.");
            });
        run("Amaurot meteor icon original clock edge placement actual solver birth handoff and viewer", () =>
        {
            using var f = Prepare(0x27B6); var center = f.Actors[1].Position;
            f.Now = f.Start.AddSeconds(1); f.Engine.Icon(100, 57, f.Start); f.Render();
            Check(f.Engine.AmbientAoes().Single(a => a.Id == "amaurot/meteor/100").Activation == f.Start.AddSeconds(8.1), "Icon original time differs.");
            Solve(f, p => Math.Abs(p.Z - center.Z) >= 14 && Math.Abs(p.Z - center.Z) <= 16 && Math.Abs(p.X - center.X) <= 16);
            f.PlayerId = 101; Scan(f);
            Check(!f.Engine.AmbientAoes().Any(a => a.Id == "amaurot/meteor/edge")
                && f.Engine.AmbientAoes().Single(a => a.Id == "amaurot/meteor/100").Navigate, "Meteor viewer output stale.");
            f.Actors[10] = new(10, 0x29DC, center + Vector3.UnitZ * 14); Scan(f); Check(f.Draw.Previews.Count == 0, "Star birth did not clear baits.");
            f.Engine.Icon(101, 57, f.Now); Scan(f); Check(f.Draw.Previews.Count == 1, "Old star snapshot erased new icon.");
            f.Engine.RemoveActor(101); f.Render(); Check(f.Draw.Previews.Count == 0, "Removed marked player leaked.");
        });
        run("Amaurot FinalSky actual meteor LOS excludes hitbox locks cast blockers real solver and cleanup", () =>
        {
            using var f = Prepare(0x27B6); var center = f.Actors[1].Position;
            var star = center + Vector3.UnitZ * 8;
            f.Actors[10] = new(10, 0x29DC, star, HitboxRadius: 2.4f); Scan(f);
            Cast(f, 1, 15563, 12); var aoes = f.Engine.AmbientAoes().ToArray();
            var safe = aoes.Single(a => a.Id.StartsWith("amaurot/los/safe")).Shape.Resolve(center, 0, null);
            var danger = aoes.Single(a => a.Id.StartsWith("amaurot/los/enter")).Shape.Resolve(center, 0, null);
            Check(safe.Contains(center + Vector3.UnitZ * 13) && !safe.Contains(star) && danger.Contains(star), "Meteor hitbox treated as safe.");
            Check(danger.Contains(center + Vector3.UnitX * 13), "Uncovered side considered safe.");
            var world = HostDrawingGeometry.Convert(safe);
            Check(world.Contains(center + Vector3.UnitZ * 13) && !world.Contains(star), "World LOS filled meteor hitbox.");
            Solve(f, p => safe.Contains(p));
            f.Actors[10] = f.Actors[10] with { Position = center - Vector3.UnitZ * 8 }; Scan(f);
            Check(f.Engine.AmbientAoes().Single(a => a.Id.StartsWith("amaurot/los/enter")).Shape.Resolve(center, 0, null)
                .Contains(center - Vector3.UnitZ * 13), "Locked blocker snapshot changed midcast.");
            f.Engine.Finish(1, completed: true); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "LOS finish leaked.");
        });
        run("Amaurot BurningSky per-player radius six spread switch and cleanup", () =>
        {
            using var f = Prepare(0x27B6); var center = f.Actors[1].Position;
            f.Actors[10] = new(10, 0x233C, center); Scan(f); Cast(f, 10, 15560, 5.2, target: 100);
            Check(f.Engine.AmbientAoes().Any(a => a.Id == "party/separate/10/101"), "Marked player did not separate.");
            f.PlayerId = 101; Scan(f); Check(f.Engine.AmbientAoes().Any(a => a.Id == "party/avoid/10"), "Unmarked viewer did not avoid.");
            f.Engine.Finish(10); f.Render(); Check(f.Draw.Previews.Count == 0 && f.Snapshot.DangerAreas.Count == 0, "Spread leaked.");
        });
        foreach (var action in new uint[] { 15575, 17107 })
            run($"Amaurot beam {action} natural finish retained first effect ignored four subsequent effects and cancel", () =>
            {
                using var f = Prepare(0x27C1); var source = action == 15575 ? 1UL : 10UL;
                if (source == 10) { f.Actors[10] = new(10, 0x233C, f.Actors[1].Position); Scan(f); }
                Cast(f, source, action, 6, .4f);
                var aoe = f.Engine.AmbientAoes().Single();
                Compare(aoe.Shape, aoe.Position, aoe.Rotation, new BossMod.AOEShapeRect(action == 15575 ? 76 : 60, action == 15575 ? 10 : 3));
                f.Engine.Finish(source, completed: true); f.Engine.Effect(source, action == 15575 ? 15575u : 15583u, 0, f.Now); f.Render();
                Check(f.Draw.Previews.Count == 1, "First hit or natural finish erased beam.");
                for (var i = 0; i < 4; ++i)
                {
                    var rest = action == 15575 ? 15577u : 15585u;
                    f.Engine.Effect(50, rest, (uint)(i + 1), f.Now); f.Engine.Effect(50, rest, (uint)(i + 1), f.Now); f.Render();
                    Check(f.Draw.Previews.Count == (i == 3 ? 0 : 1), "Four rest hits/dedup differs.");
                }
                f.Now = f.Start.AddSeconds(20); Cast(f, source, action, 6); f.Engine.Finish(source); f.Render();
                Check(f.Draw.Previews.Count == 0, "Cancelled repeat beam retained.");
            });
        run("Amaurot Faces four sources five hits each second heading plus eight point five exact promotion", () =>
        {
            using var f = Prepare(0x27C1);
            for (var i = 0; i < 4; ++i) f.Actors[(ulong)(10 + i)] = new((ulong)(10 + i), 0x27C3,
                new(i < 2 ? -10 : 10, 0, -68 + i * 8), Rotation: i < 2 ? MathF.PI / 2 : -MathF.PI / 2);
            Scan(f); var timestamp = f.Start.AddSeconds(2); f.Now = timestamp.AddSeconds(.2);
            f.Engine.Effect(10, 15580, 0, f.Now, occurredAt: timestamp); f.Render();
            var aoes = f.Engine.AmbientAoes().ToArray();
            Check(aoes.Length == 4 && aoes.Count(a => a.Navigate) == 2
                && aoes.Where(a => !a.Navigate).All(a => a.Activation == timestamp.AddSeconds(8.5)), "Face initial group/clock differs.");
            for (var face = 0; face < 4; ++face)
                for (var hit = face == 0 ? 1 : 0; hit < 5; ++hit)
                {
                    var id = (ulong)(10 + face); var action = hit == 0 ? 15580u : 15581u; var seq = (uint)(face * 10 + hit + 1);
                    f.Engine.Effect(id, action, seq, f.Now); f.Engine.Effect(id, action, seq, f.Now); f.Render();
                    if (hit == 4) Check(!f.Engine.AmbientAoes().Any(a => a.Id == $"amaurot/face/{id}"), "Face not cleared after fifth.");
                    if (face == 1 && hit == 4) Check(f.Engine.AmbientAoes().Count(a => a.Navigate) == 2, "Second group not promoted.");
                }
            Scan(f); Check(f.Draw.Previews.Count == 0, "Consumed face wave revived from objects.");
        });
        foreach (var order in new[] { Enumerable.Range(0, 8).ToArray(), Enumerable.Range(0, 8).Reverse().ToArray(), new[] { 2, 7, 1, 4, 0, 3, 5, 6 } })
            run($"Amaurot platform removal order {string.Join('-', order)} inset main cuts breaking risk and repeat", () =>
            {
                using var f = Prepare(0x27C1); var mask = 0;
                foreach (var i in order)
                {
                    var p = DungeonEncounter.AmaurotPlatforms[i]; var id = (ulong)(20 + i);
                    f.Actors[id] = new(id, 0, new(p.X, 0, p.Y)); Scan(f);
                    f.Engine.Animation(id, 0x00100020, f.Now); f.Render();
                    Check(f.Engine.AmbientAoes().Any(a => a.Id == $"amaurot/break/{i}" && a.Navigate), "Breaking platform not risky before beam.");
                    Cast(f, 1, 15575, 6, MathF.PI);
                    f.Engine.Effect(50, 15577, (uint)(i + 1), f.Now); f.Render();
                    Check(f.Engine.AmbientAoes().Single(a => a.Id == $"amaurot/break/{i}").Navigate == false, "Breaking platform remained current danger after first rest hit.");
                    f.Engine.Finish(1); f.Actors[1] = f.Actors[1] with { Cast = null };
                    f.Engine.Animation(id, 0x00040008, f.Now.AddSeconds(.1)); mask |= 1 << i; f.Render();
                    CompareArena(f.Engine.Arena!, SourceArena(mask), 1.37f);
                    f.Engine.Animation(id, 0x00040008, f.Now.AddSeconds(.1)); f.Render();
                    Check(!f.Engine.AmbientAoes().Any(a => a.Id == $"amaurot/break/{i}"), "Duplicate removal warning revived.");
                    f.Now = f.Now.AddSeconds(10);
                }
                f.Engine.ResetFight(); Scan(f); CompareArena(f.Engine.Arena!, SourceArena(0), .63f);
            });
    }

    private static BossMod.ArenaBoundsCustom SourceArena(int mask)
    {
        var type = typeof(BossMod.BossModule).Assembly.GetType("BossMod.Shadowbringers.Dungeon.D06Amaurot.D063Therion.Border")!;
        var union = ((IEnumerable<BossMod.Shape>)type.GetMethod("Union", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, null)!).ToList();
        var alcoves = (BossMod.Square[])type.GetField("alcoves", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        for (var i = 0; i < 8; ++i) if ((mask & (1 << i)) != 0) union.Remove(alcoves[i]);
        var cuts = (BossMod.Square[])type.GetField("differences", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        return new([.. union], [.. cuts.Take(Math.Min(2, BitOperations.PopCount((uint)mask) / 2))], Offset: -1);
    }
    private static void CompareArena(IGeometryShape shape, BossMod.ArenaBoundsCustom source, float step)
    {
        var world = HostDrawingGeometry.Convert(shape);
        for (var x = -35f; x <= 35; x += step)
            for (var z = -40f; z <= 40; z += step)
            {
                var p = new Vector3(source.Center.X + x, 0, source.Center.Z + z); var expected = source.Contains(new(x, z));
                Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source arena or world polygon differs.");
            }
    }
    private static Fight Prepare(uint boss)
    {
        var f = new Fight(boss, 838); f.Engine.ResetFight(); var p = f.Engine.Definitions.Single(d => d.BossOid == boss).Arena(Vector3.Zero).Position;
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
    private static void Compare(GeometryTemplate template, Vector3 origin, float rotation, BossMod.AOEShape source)
    {
        var shape = template.Resolve(origin, rotation, null); var world = HostDrawingGeometry.Convert(shape);
        for (var x = -80f; x <= 80; x += 1.13f)
            for (var z = -80f; z <= 80; z += 1.13f)
            {
                var p = origin + new Vector3(x, 0, z); var expected = source.Check(new(p.X, p.Z), new(origin.X, origin.Z), new(rotation));
                Check(shape.Contains(p) == expected && world.Contains(p) == expected, "Source dimensions or world conversion differs.");
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
