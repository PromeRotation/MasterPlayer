using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static (DungeonEncounter Encounter, DungeonActor Boss, DungeonActor Player) BekkoOrcus(uint territory)
    {
        var definition = JobQuestCatalog.Create(territory).Single();
        var center = definition.Arena(Vector3.Zero).Position;
        var e = new DungeonEncounter(territory);
        DungeonActor boss = new(1, definition.BossOid, center, InCombat: true);
        DungeonActor player = new(100, 0, center, PartySlot: 0);
        e.Synchronize([boss, player], center, Start, playerId: 100);
        return (e, boss, player);
    }

    private static void BekkoRings()
    {
        var (e, boss, player) = BekkoOrcus(711);
        var center = boss.Position;
        DungeonCastObservation Cast(uint action, Vector3? point = null, float angle = 0, double time = 0)
            => new(action, point ?? center, angle, Start.AddSeconds(time), Start.AddSeconds(time + 2.5));
        DungeonAmbientAoe Ring() => e.AmbientAoes().Single(a => a.Id.StartsWith("bekko/ring/"));
        DungeonActor helper = new(2, 0x1BF9, center, Cast: Cast(8439, angle: MathF.Tau - .01f));
        e.Synchronize([boss, player, helper], center, Start, playerId: 100);
        e.StartCast(2, Cast(8439, center + Vector3.UnitX * .1f, -.01f), Start.AddSeconds(.1));
        Check(Ring().Position.X == center.X + .1f && Ring().Shape.Resolve(center, 0, null).Contains(center + Vector3.UnitZ * 4),
            "Bekko packet correction duplicated or lost first ring.");
        e.Tick(Start.AddSeconds(20));
        Check(Ring().Activation == Start.AddSeconds(2.5), "Bekko ring advanced from elapsed clock.");
        e.Finish(2, 8439, Start.AddSeconds(20), completed: false);
        Check(Ring().Activation == Start.AddSeconds(22.5), "Bekko cancellation did not advance using consumption clock.");
        e.Synchronize([boss, player, new(3, 0x1BF9, center, Cast: Cast(8440, angle: .06f, time: 21))],
            center, Start.AddSeconds(21), playerId: 100);
        e.Finish(3, 8440, Start.AddSeconds(21.1));
        Check(Ring().Activation == Start.AddSeconds(22.5), "Bekko mismatched rotation advanced sequence.");
        var shifted = center + new Vector3(.9f, 0, .9f);
        e.Synchronize([boss, player, new(4, 0x1BF9, shifted, Cast: Cast(8440, shifted, .01f, 22))],
            center, Start.AddSeconds(22), playerId: 100);
        e.RemoveActor(4);
        Check(Ring().Activation == Start.AddSeconds(24.5)
            && Ring().Shape.Resolve(center, 0, null).Contains(center + Vector3.UnitZ * 14),
            "Bekko destruction/per-axis wrapped-angle matching did not advance.");
        e.Synchronize([boss, player, new(5, 0x1BF9, center, Cast: Cast(8441, time: 23))],
            center, Start.AddSeconds(23), playerId: 100);
        e.Finish(5, 8441, Start.AddSeconds(23.1));
        e.Tick(Start.AddSeconds(100));
        Check(Ring().Shape.Resolve(center, 0, null).Contains(center + Vector3.UnitZ * 20), "Bekko final ring expired.");
        e.Synchronize([boss, player, new(6, 0x1BF9, center, Cast: Cast(8442, time: 101))],
            center, Start.AddSeconds(101), playerId: 100);
        e.Finish(6, 8442, Start.AddSeconds(101.1));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("bekko/ring/")), "Bekko fourth finish leaked sequence.");
        var (isolated, otherBoss, _) = BekkoOrcus(711);
        isolated.Synchronize([otherBoss, new(2, 0x1BF9, center, Cast: Cast(8440))], center, Start);
        Check(!isolated.AmbientAoes().Any(a => a.Id.StartsWith("bekko/ring/")), "Later Bekko ring invented first-stage history.");
    }

    private static void BekkoLines()
    {
        var (e, boss, player) = BekkoOrcus(711);
        var center = boss.Position;
        var helpers = Enumerable.Range(2, 10).Select(i => new DungeonActor((ulong)i, 0x1BF9, center,
            Cast: new(8446, center, 0, Start, Start.AddSeconds(2.5)))).ToArray();
        DungeonActor floor = new(50, 0x1E8EA9, center + new Vector3(.123f, 0, .234f), Dead: true, Targetable: false);
        e.Synchronize([boss, player, floor, .. helpers], center, Start, playerId: 100);
        Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 2, 3, 4, 5, 6, 7, 8, 9 }),
            "Bekko first-eight cast order.");
        e.Finish(2, 8446, Start.AddSeconds(.1));
        e.Synchronize([boss, player, floor, .. helpers.Skip(1), new(20, 0x1BF9, center,
            Cast: new(8446, center, 0, Start.AddSeconds(1), Start.AddSeconds(3.5)))], center, Start.AddSeconds(1), playerId: 100);
        Check(e.OutputCasts.Select(c => c.Value.Source).SequenceEqual(new ulong[] { 3, 4, 5, 6, 7, 8, 9, 10 }),
            "Bekko dictionary slot reuse displaced older line.");
        var zone = e.AmbientAoes().Single(a => a.Id.StartsWith("bekko/voidzone/"));
        Check(zone.Obstacle && zone.Position == floor.Position && zone.Shape is CircleTemplate { Radius: 4 },
            "Bekko persistent floor lost raw position, obstacle or dead/targetable semantics.");
        e.Synchronize([boss, player, floor with { EventState = 7 }], center, Start.AddSeconds(2), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("bekko/voidzone/")), "Bekko inactive floor retained obstacle.");
        e.StartCast(1, new(8447, center, 0, Start.AddSeconds(3), Start.AddSeconds(5)), Start.AddSeconds(3));
        Check(e.PredictedDamage.Single().DamageType == "Raidwide" && e.OutputCasts.All(c => c.Value.Rule.Shape == null),
            "Bekko raidwide geometry/damage.");
    }

    private static void OrcusRings()
    {
        var (e, boss, player) = BekkoOrcus(1021);
        var center = boss.Position;
        uint[] actions = [27602, 27600, 27603, 27601, 27602];
        var helpers = actions.Select((a, i) => new DungeonActor((ulong)(i + 2), 0x233C, center,
            Cast: new(a, center, 0, Start, Start.AddSeconds(3.5)))).ToArray();
        DungeonAmbientAoe[] Rings() => e.AmbientAoes().Where(a => a.Id.StartsWith("orcus/ring/")).ToArray();
        e.Synchronize([boss, player, .. helpers], center, Start, playerId: 100);
        Check(Rings().Select(a => a.Id).SequenceEqual(new[] { "orcus/ring/2/1", "orcus/ring/3/2", "orcus/ring/4/3", "orcus/ring/5/4" }),
            "Orcus first-four insertion order.");
        e.StartCast(3, helpers[1].Cast! with { Location = center + Vector3.UnitX }, Start.AddSeconds(.05));
        Check(Rings().Length == 4 && Rings()[1].Position == center + Vector3.UnitX, "Orcus packet correction duplicated FIFO item.");
        e.Finish(4, 27603, Start.AddSeconds(.1));
        Check(Rings().Select(a => a.Id).SequenceEqual(new[] { "orcus/ring/3/2", "orcus/ring/4/3", "orcus/ring/5/4", "orcus/ring/6/5" }),
            "Orcus non-head caster finish did not pop queue head.");
        e.StartCast(2, helpers[0].Cast! with { Location = center + Vector3.UnitZ }, Start.AddSeconds(.2));
        Check(Rings().All(a => !a.Id.StartsWith("orcus/ring/2/")), "Orcus correction resurrected consumed FIFO item.");
        e.RemoveActor(5);
        Check(Rings().Select(a => a.Id).SequenceEqual(new[] { "orcus/ring/4/3", "orcus/ring/5/4", "orcus/ring/6/5" }),
            "Orcus destruction did not pop exactly one head.");
        e.Tick(Start.AddSeconds(100));
        Check(Rings().Length == 3, "Orcus FIFO expired by prediction clock.");
        e.StartCast(4, new(27603, center, 0, Start.AddSeconds(101), Start.AddSeconds(104.5)), Start.AddSeconds(101));
        Check(Rings().Length == 4 && Rings().Select(a => a.Id).Distinct().Count() == 4,
            "Orcus reused caster overwrote an older FIFO drawing/navigation item.");
    }

    private static void OrcusPoison()
    {
        var (e, boss, player) = BekkoOrcus(1021);
        var center = boss.Position;
        DungeonActor target = new(101, 0, center + Vector3.UnitX * 7, PartySlot: 1);
        DungeonActor helper = new(2, 0x233C, center, Cast: new(27593, center - Vector3.UnitX * 7, 0, Start, Start.AddSeconds(3.5), 101));
        DungeonAmbientAoe[] Poison() => e.AmbientAoes().Where(a => a.Id.StartsWith("trial/voidzone/")).ToArray();
        e.Synchronize([boss, player, target, helper], center, Start, playerId: 100);
        target = target with { Position = center + Vector3.UnitZ * 8 };
        e.Synchronize([boss, player, target, helper], center, Start.AddSeconds(1), playerId: 100);
        Check(Poison().Single().Position == target.Position, "Orcus cast target did not move dynamically.");
        e.StartCast(2, helper.Cast! with { TargetId = 100 }, Start.AddSeconds(1.1));
        Check(Poison().Single().Position == player.Position, "Orcus authoritative target correction lost to old actor CastInfo.");
        e.Effect(2, 27593, 1, Start.AddSeconds(2), target: 101, location: center, occurredAt: Start.AddSeconds(-10));
        e.Effect(3, 27593, 2, Start.AddSeconds(2), target: 101, location: center);
        Check(Poison().Count(a => a.Id.Contains("/event/") && a.Activation == Start.AddSeconds(2) && a.Position == target.Position) == 2,
            "Orcus event did not use current MainTarget and consumption clock.");
        e.Finish(2, 27593, Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(100));
        Check(Poison().Length == 2, "Orcus poison event prediction expired by clock.");
        DungeonActor zone = new(50, 0x1E963D, target.Position + Vector3.UnitX * 1.99f, Dead: true, Targetable: false);
        e.Synchronize([boss, player, target, helper with { Cast = null }, zone], center, Start.AddSeconds(101), playerId: 100);
        Check(Poison().Length == 2 && Poison().Count(a => a.Id.Contains("/event/")) == 1, "Orcus entity consumed multiple predictions.");
        e.Synchronize([boss, player, target, helper with { Cast = null }, zone], center, Start.AddSeconds(102), playerId: 100);
        Check(Poison().Length == 1 && Poison()[0].Id.Contains("/live/"), "Orcus second observation failed to hand over remaining prediction.");
        e.Synchronize([boss, player, target, zone with { EventState = 7 }], center, Start.AddSeconds(103), playerId: 100);
        Check(Poison().Length == 0, "Orcus inactive live floor retained output.");
    }

    private static void BekkoOrcusGeometry()
    {
        foreach (var territory in new uint[] { 711, 1021 })
        {
            var definition = JobQuestCatalog.Create(territory).Single();
            var arena = definition.Arena(Vector3.Zero);
            Check(arena.Contains(arena.Position), "Bekko/Orcus center outside boundary.");
            foreach (var rule in definition.Casts.Where(r => r.Shape != null))
            {
                var geometry = rule.Shape!.Resolve(Vector3.Zero, 0, null);
                var inside = new Vector3(0, 0, rule.Action == 8448 ? 6 : 1);
                Check(geometry.Contains(inside) && !geometry.Contains(new(200, 0, 200)), $"Bekko/Orcus shape {rule.Action}.");
            }
        }
        var orcus = JobQuestCatalog.Create(1021).Single();
        var boundary = orcus.Arena(Vector3.Zero);
        Check(boundary.Contains(new(-69.569f, 0, -369.1f)) && !boundary.Contains(new(-69.569f, 0, -368.7f))
            && boundary.Position.Z < -388.1f && boundary.Position.Z > -388.3f, "Orcus entrance difference or AABB recenter.");
        var (e, boss, player) = BekkoOrcus(1021);
        e.Synchronize([boss, player, new(2, 0x3666, boss.Position, ObjectIndex: 2), new(3, 0x3667, boss.Position, ObjectIndex: 4)],
            boss.Position, Start, playerId: 100);
        Check(e.PriorityTarget(1, boss.Position) == 2 && e.PriorityTarget(3, boss.Position) == 3
            && e.AmbientAoes().Count(a => a.Id.StartsWith("job-quest/enemy/")) == 3, "Orcus adds priority/markers.");
        e.StartCast(2, new(27606, boss.Position, 0, Start, Start.AddSeconds(20)), Start);
        Check(!e.OutputCasts.Any(), "Inactive Explosion component was claimed.");
    }
}
