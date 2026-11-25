using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureGymnasionTrioRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 5, Player: true, Hostile: false);
    private static DungeonCastObservation Cast(uint action, ulong target = 100, Vector3? position = null,
        double started = 0, double duration = 5, float rotation = 0) =>
        new(action, position ?? Center, rotation, Start.AddSeconds(started), Start.AddSeconds(started + duration), target);
    private static DungeonEncounter Encounter(uint oid)
    {
        var e = new DungeonEncounter(1123) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C), Enemy(4, 0x233C),
            Player(), Player(101, Center + Vector3.UnitX * 5), Player(102, Center - Vector3.UnitX * 5)],
            Player().Position, Start, playerId: 100);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure gymnasion trio complete rules and inactive boundary", Rules);
        run("native treasure Pithekos Source icon independent clocks clear once and dead first", Icons);
        run("native treasure Styphnolobion concentric center clocks matching and consumed correction", Concentric);
        run("native treasure Styphnolobion cast bait actual effects FIFO Raidwide targets", CastBaits);
        run("native treasure Styphnolobion destroyed target freeze and permanent death removal", BaitIdentity);
        run("native treasure Meganereis knockback immunity leaves directions independent first deadline", Knockback);
        run("native treasure Meganereis landing detects only Hydrobomb and no raidwide prediction", Landing);
        run("native treasure Meganereis spread target FIFO death revival and single party", Spread);
        run("native treasure gymnasion trio reward priority colors All and helper exclusion", Rewards);
    }
    private static void Rules()
    {
        var p = Encounter(0x3D2B).Active!.Casts.ToDictionary(c => c.Action);
        var s = Encounter(0x3D37).Active!.Casts.ToDictionary(c => c.Action);
        var m = Encounter(0x3D39).Active!.Casts.ToDictionary(c => c.Action);
        Check(p.Count == 12 && p[32216].Shape is DonutTemplate { InnerRadius: 14, OuterRadius: 30 }
            && p[32212].Shape is CircleTemplate { Radius: 3 } && p[32213].Shape is CircleTemplate { Radius: 18 }
            && !p.ContainsKey(32214), "Pithekos parameters or inactive boundary differs.");
        Check(s.Count == 13 && s[32253].Shape is CircleTemplate { Radius: 6 }
            && new uint[] { 32245, 32246, 32251, 32247, 32248, 32249 }.All(a => s[a].Shape == null)
            && !s.ContainsKey(32250) && !s.ContainsKey(32252) && !s.ContainsKey(32254), "Styphnolobion rules differ.");
        Check(m.Count == 14 && m[32259].Shape is CircleTemplate { Radius: 10 }
            && m[32262].Shape is CircleTemplate { Radius: 8 }
            && m[32256].Shape is RayRectangleTemplate { Length: 27, MinLocalX: -3, MaxLocalX: 3 }
            && m[32264].Shape is RayRectangleTemplate { Length: 17, MinLocalX: -1.5f, MaxLocalX: 1.5f }
            && m[32258].Shape == null && !NativeTreasureCatalog.RaidwideAction(32258)
            && !m.ContainsKey(32257) && !m.ContainsKey(32261), "Meganereis rules differ.");
    }
    private static void Icons()
    {
        var e = Encounter(0x3D2B);
        e.Tick(Start.AddSeconds(1));
        e.Icon(101, 111, Start, actorId: 100);
        e.Tick(Start.AddSeconds(2));
        e.Icon(100, 111, Start, actorId: 101);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/bait/")).ToArray();
        Check(aoes.Single(a => a.Id == "treasure/bait/edge").Activation == Start.AddSeconds(7)
            && aoes.Single(a => a.Id == "treasure/bait/1/avoid").Activation == Start.AddSeconds(8)
            && aoes.Single(a => a.Id == "treasure/bait/0/display").Shape is CircleTemplate { Radius: 18 }
            && e.Hints(Player().Position).Contains("Bait levinorb away!")
            && !e.Hints(Player().Position).Contains("GTFO from baited aoe!") && !e.PredictedDamage.Any(),
            "Pithekos icon source, independent clocks, radius or text differs.");
        e.StartCast(2, Cast(32212, started: 3), Start.AddSeconds(3));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/bait/")), "Thundercall Start failed clear.");
        e.Tick(Start.AddSeconds(3.1));
        e.Icon(101, 111, Start, actorId: 100);
        e.StartCast(2, Cast(32212, position: Center + Vector3.UnitX, started: 3), Start.AddSeconds(3.2));
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/bait/edge"), "Packet correction cleared later icon.");
        e.Synchronize([Enemy(1, 0x3D2B) with { Dead = true }, Enemy(9, 0x3D4F), Player() with { Dead = true }, Player(101)],
            Player().Position, Start.AddSeconds(4), playerId: 100);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/bait/")).Id == "treasure/bait/edge"
            && e.Hints(Player().Position).Contains("Bait levinorb away!"), "Dead source/first target split lost.");
    }
    private static void Concentric()
    {
        var e = Encounter(0x3D37);
        var visual = Cast(32247, position: Center + Vector3.UnitX * 9);
        e.StartCast(2, visual, Start);
        Check(e.TreasureIceSequences.Single().Activation == Start.AddSeconds(5.5)
            && Vector3.Distance(e.TreasureIceSequences.Single().Origin, Center) < .05f, "Earth Quaker initial lock/clock differs.");
        e.StartCast(3, Cast(32248, position: Center + new Vector3(.9f, 0, .9f), started: 1, rotation: .1f), Start.AddSeconds(1));
        e.Tick(Start.AddSeconds(8));
        e.Finish(3, 32248, Start.AddSeconds(2));
        var sequence = e.TreasureIceSequences.Single();
        Check(sequence.Order == 1 && sequence.Activation == Start.AddSeconds(10)
            && e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/ice/")).Shape is DonutTemplate,
            "Earth Quaker used radial tolerance, helper rotation or event clock.");
        e.StartCast(2, visual with { Location = Center + Vector3.UnitZ }, Start.AddSeconds(8.1));
        Check(e.TreasureIceSequences.Single() == sequence, "Consumed live visual correction restarted Earth Quaker.");
        e.StartCast(4, Cast(32249, started: 9), Start.AddSeconds(9));
        e.Effect(4, 32249, 1, Start.AddSeconds(9.1));
        Check(e.TreasureIceSequences.Count == 1, "Effect advanced Finish-only sequence.");
        e.Finish(4, 32249, Start.AddSeconds(10));
        Check(e.TreasureIceSequences.Count == 0, "Earth Quaker final Finish failed.");
    }
    private static void CastBaits()
    {
        var e = Encounter(0x3D37);
        e.StartCast(2, Cast(32251), Start);
        e.StartCast(3, Cast(32251, 101, started: 1), Start.AddSeconds(1));
        e.Finish(2, 32251, Start.AddSeconds(2));
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cast-bait/")).ToArray();
        Check(aoes.Count(a => a.Display) == 2
            && aoes.Where(a => a.Display).All(a => a.Shape is FanTemplate { Radius: 60 } f && MathF.Abs(f.AngleRadians - MathF.PI / 6) < .001f)
            && aoes.Where(a => a.Id.Contains("/party/")).All(a => a.Shape is FanTemplate { Radius: 100 } f && MathF.Abs(f.AngleRadians - MathF.PI / 6) < .001f)
            && !e.Hints(Player().Position).Contains("Tankbuster cleave")
            && e.PredictedDamage.Count() == 2 && e.PredictedDamage.All(d => d.DamageType == "Raidwide" && d.Activation == Start.AddSeconds(5)),
            "Earth Shaker geometry, Finish retention, target-only Raidwide or first clock differs.");
        e.TreasureEffect(2, 32251, 1, 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/cast-bait/") && a.Display) == 1
            && e.PredictedDamage.Single().TargetId == 101, "Earth Shaker source FIFO removed unrelated caster.");
        e.TreasureEffect(2, 32251, 1, 101);
        Check(e.PredictedDamage.Count() == 1, "Expanded target effect duplicated consumption.");
    }
    private static void BaitIdentity()
    {
        var e = Encounter(0x3D37);
        e.StartCast(2, Cast(32251), Start);
        e.RemoveActor(100);
        e.Synchronize([Enemy(1, 0x3D37), Enemy(2, 0x233C), Player(position: Center + Vector3.UnitX * 15), Player(101)],
            Center + Vector3.UnitX * 15, Start.AddSeconds(1), playerId: 100);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cast-bait/")).ToArray();
        Check(aoes.Any(a => a.Id.EndsWith("/avoid")) && !aoes.Any(a => a.Id.Contains("/party/")),
            "Same-ID new target acquired old bait personal identity.");
        e = Encounter(0x3D37);
        e.StartCast(2, Cast(32251), Start);
        e.Synchronize([Enemy(1, 0x3D37), Enemy(2, 0x233C), Player() with { Dead = true }, Player(101)],
            Player().Position, Start.AddSeconds(1), playerId: 100);
        e.Synchronize([Enemy(1, 0x3D37), Enemy(2, 0x233C), Player(), Player(101)],
            Player().Position, Start.AddSeconds(2), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cast-bait/")), "Dead bait resurrected after target revival.");
    }
    private static void Knockback()
    {
        var e = Encounter(0x3D39);
        e.StartCast(2, Cast(32258), Start);
        e.StartCast(3, Cast(32258, started: 1), Start.AddSeconds(1));
        e.StartCast(4, Cast(32259, position: Center + Vector3.UnitX * 12, started: 2), Start.AddSeconds(2));
        var directions = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/turmoil/")).ToArray();
        Check(directions.Length == 1 && !directions[0].Display && directions[0].Activation == Start.AddSeconds(5)
            && directions[0].Shape is FanTemplate { Radius: 20 } f && MathF.Abs(f.AngleRadians - MathF.PI / 3) < .001f,
            "Meganereis direction angle, visibility or first deadline differs.");
        var immune = Player() with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(8) } };
        Check(!e.TreasureWallMovements(immune).Any(), "Immune actor still projected knockback.");
        e.Synchronize([Enemy(1, 0x3D39), Enemy(2, 0x233C) with { Cast = Cast(32258) },
            Enemy(3, 0x233C) with { Cast = Cast(32258, started: 1) },
            Enemy(4, 0x233C) with { Cast = Cast(32259, position: Center + Vector3.UnitX * 12, started: 2) }, immune, Player(101)],
            immune.Position, Start.AddSeconds(3), playerId: 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/turmoil/"))
            && !e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/kb/")), "Immunity removed independent directions.");
        e.Finish(2, 32258, Start.AddSeconds(3.1));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/turmoil/")).Activation == Start.AddSeconds(6),
            "First Finish did not transfer direction deadline to second source.");
        var boundary = Player() with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(6) } };
        Check(e.TreasureWallMovements(boundary).Count() == 1, "Equal immunity expiry incorrectly suppresses displacement.");
    }
    private static void Landing()
    {
        var e = Encounter(0x3D39);
        e.StartCast(2, Cast(32258), Start);
        var landing = e.TreasureWallMovements(Player()).Single().To;
        e.StartCast(3, Cast(32262, position: landing, started: 1), Start.AddSeconds(1));
        Check(!e.TreasureWallMovements(Player()).Single().Unsafe && !e.PredictedDamage.Any(), "Waterspout or KB added unsupported warning/prediction.");
        e.StartCast(4, Cast(32259, position: landing, started: 2), Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(20));
        Check(e.TreasureWallMovements(Player()).Single().Unsafe && e.Hints(Player().Position).Contains("About to be knocked into danger!"),
            "Hydrobomb expired from landing checks without observed Finish.");
    }
    private static void Spread()
    {
        var e = Encounter(0x3D39);
        e.StartCast(2, Cast(32260, 101), Start);
        e.StartCast(3, Cast(32260, 101, started: 1), Start.AddSeconds(1));
        var marks = e.AmbientAoes().Where(a => a.Id.StartsWith("quest/spread/")).ToArray();
        Check(marks.Count(a => a.Display && a.Shape is CircleTemplate { Radius: 8 } && a.OutlineOnly) == 2
            && marks.Count(a => !a.Display && a.Shape is CircleTemplate { Radius: 9 }) == 2
            && !e.PredictedDamage.Any(), "FallingWater visual/navigation radii or prediction scope differs.");
        e.Finish(3, 32260, Start.AddSeconds(2));
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && !a.Display).Activation == Start.AddSeconds(6),
            "Spread Finish consumed by source rather than first matching target.");
        e.Synchronize([Enemy(1, 0x3D39), Enemy(2, 0x233C), Enemy(3, 0x233C), Player(), Player(101) with { Dead = true }],
            Player().Position, Start.AddSeconds(3), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("quest/spread/")), "Dead spread still displayed.");
    }
    private static void Rewards()
    {
        foreach (var (oid, add) in new[] { (0x3D2Bu, 0x3D2Cu), (0x3D37u, 0x3D38u), (0x3D39u, 0x3D3Au) })
        {
            var e = Encounter(oid);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(9, 0x3D4F), Enemy(10, add), Player()],
                Player().Position, Start.AddSeconds(1), playerId: 100);
            Check(e.Active?.BossOid == oid && NativeTreasureCatalog.Priority(oid, 0x3D4F) == 6
                && NativeTreasureCatalog.Priority(oid, add) == 1
                && e.AmbientAoes().Single(a => a.Id == "treasure/enemy/9").Color == new Vector4(1, 0, 1, 1),
                "Reward priority, color or All lost.");
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(11, 0x3E90), Enemy(12, 0x233C), Player()],
                Player().Position, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Ball/helper extended encounter.");
        }
    }
}
