using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureIceBubbleRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? p = null) =>
        new(id, oid, p ?? Center, InCombat: true, IsEnemy: true, Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? p = null) =>
        new(id, 0, p ?? Center + Vector3.UnitZ * 5, Player: true, Hostile: false);
    private static DungeonEncounter Encounter(uint oid)
    {
        var e = new DungeonEncounter(oid == 0x3D43 ? 1123u : 924u) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C),
            Player(), Player(101), Player(102)], Player().Position, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, ulong target = 100, Vector3? p = null,
        double started = 0, float rotation = 0) =>
        new(action, p ?? Center, rotation, Start.AddSeconds(started), Start.AddSeconds(started + 5), target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure ice bubble complete active rules and inactive boundaries", Rules);
        run("native treasure Djinn exact25 wall knockback independent generations and no navigation", Djinn);
        run("native treasure Djinn immunity strict expiry and observed ending", DjinnImmunity);
        run("native treasure Worm Source icons independent deadlines and first target text", WormBaits);
        run("native treasure Worm dead source derived edge and destroyed target freeze", WormBaitIdentity);
        run("native treasure Worm created circles unfiltered lock lifetime and global FIFO finish", WormCircles);
        run("native treasure ice bubble creation before frame bootstrap and generation receipts", CreatedGenerations);
        run("native treasure Lyssa created pillars clear all actual finish and never readd", Pillars);
        run("native treasure Lyssa two independent sequences share Finish with exact clocks", IceSequences);
        run("native treasure Lyssa sequence axis match ignores helper rotation cancellation and consumed correction", IceMatching);
        run("native treasure Lyssa exact eight stack targets death revival and actual finish", Stacks);
        run("native treasure ice bubble complete reward priorities colors and All lifecycle", Rewards);
    }
    private static void Rules()
    {
        var d = Encounter(0x300F).Active!.Casts.ToDictionary(c => c.Action);
        Check(d.Count == 8 && d[21654].Shape == null && d[21654].Hint == "Raidwide + Knockback"
            && d[21655].Shape is CircleTemplate { Radius: 6 }
            && d[21657].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 }
            && d[21656].Shape is RayRectangleTemplate { Length: 55, MinLocalX: -20, MaxLocalX: 20 }
            && d[21653].Shape is RayRectangleTemplate { Length: 15, MinLocalX: -2, MaxLocalX: 2 }
            && !d.ContainsKey(21770), "Djinn active components differ.");
        var w = Encounter(0x3029).Active!.Casts.ToDictionary(c => c.Action);
        Check(w.Count == 9 && w[21713].Shape is CircleTemplate { Radius: 8 }
            && w[21711].Shape is RayRectangleTemplate { Length: 46, MinLocalX: -2, MaxLocalX: 2 }
            && w[21710].Shape == null && w[21715].Shape == null && !w.ContainsKey(21714)
            && w.Where(p => p.Key is >= 6448 and <= 6452).All(p => p.Value.Shape is CircleTemplate { Radius: 6.84f }),
            "Worm actions differ or inactive Hydroburst claimed.");
        var l = Encounter(0x3D43).Active!.Casts.ToDictionary(c => c.Action);
        Check(l.Count == 11 && l[32316].Shape is RayRectangleTemplate { Length: 80, MinLocalX: -2, MaxLocalX: 2 }
            && l[32309].Shape is CircleTemplate { Radius: 5 } && l[32317].Shape is CircleTemplate { Radius: 6 }
            && new uint[] { 32315, 32306, 32314, 32310, 32311, 32312, 32313 }.All(a => l[a].Shape == null)
            && !l.ContainsKey(32307) && !l.ContainsKey(32308), "Lyssa rules or inactive visuals differ.");
    }
    private static void Djinn()
    {
        var e = Encounter(0x300F);
        e.StartCast(2, Cast(21654), Start);
        e.StartCast(3, Cast(21654, started: 1), Start.AddSeconds(1));
        Check(e.TreasureWallKnockbackSources.Count == 2 && e.TreasureWallKnockbackSources.All(k => k.Distance == 25 && k.StopAtWall)
            && e.TreasureWallMovements(Player()).Count() == 2 && !e.AmbientAoes().Any(a => a.Navigate),
            "Djinn displacement parameters, simultaneous sources or display-only scope lost.");
        var first = e.TreasureWallMovements(Player()).First();
        Check(first.To.Z > 118 && first.To.Z < 119.1 && first.From == Player().Position, "25y wall projection used radius or direct endpoint.");
        e.Finish(2, 21654, Start.AddSeconds(2));
        Check(e.TreasureWallKnockbackSources.Single().Cast.Source == 3, "Finish cleared wrong source.");
        e.StartCast(2, Cast(21657, started: 3), Start.AddSeconds(3));
        Check(e.TreasureWallKnockbackSources.Count == 1, "Changeless Winds invented KB.");
    }
    private static void DjinnImmunity()
    {
        var e = Encounter(0x300F);
        e.StartCast(2, Cast(21654), Start);
        e.StatusChanged(100, 160, true, Start, expiresAt: Start.AddSeconds(5));
        Check(e.TreasureWallMovements(Player()).Any(), "Immunity expiring exactly at activation incorrectly excluded KB.");
        e.StatusChanged(100, 160, true, Start, expiresAt: Start.AddSeconds(5.01));
        Check(!e.TreasureWallMovements(Player()).Any(), "Strictly later immunity failed.");
        e.StatusChanged(100, 160, false, Start);
        e.Tick(Start.AddSeconds(20));
        e.Effect(2, 21654, 1, Start.AddSeconds(20));
        Check(e.TreasureWallKnockbackSources.Count == 1, "Effect/expiry cleared observed KB.");
        e.RemoveActor(2);
        Check(e.TreasureWallKnockbackSources.Count == 0, "Caster destroy retained KB.");
    }
    private static void WormBaits()
    {
        var e = Encounter(0x3029);
        e.Tick(Start.AddSeconds(1));
        e.Icon(101, 23, Start, actorId: 100);
        e.Tick(Start.AddSeconds(2));
        e.Icon(100, 23, Start, actorId: 101);
        var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/bait/")).ToArray();
        Check(aoes.Single(a => a.Id == "treasure/bait/edge").Activation == Start.AddSeconds(6)
            && aoes.Single(a => a.Id == "treasure/bait/1/avoid").Activation == Start.AddSeconds(7)
            && aoes.Any(a => a.Id == "treasure/bait/0/party/101" && a.Activation == Start.AddSeconds(6))
            && e.Hints(Player().Position).Contains("Bait bubble away!")
            && !e.Hints(Player().Position).Contains("GTFO from baited aoe!")
            && !e.PredictedDamage.Any(), "Icon Source deadlines, base navigation, first text or prediction scope differs.");
        e.Effect(2, 21713, 1, Start.AddSeconds(3));
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/bait/edge"), "Hydrocannon effect cleared bait.");
        e.StartCast(2, Cast(21713, started: 3), Start.AddSeconds(3));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/bait/")), "Any source Start did not clear all baits.");
    }
    private static void WormBaitIdentity()
    {
        var e = Encounter(0x3029);
        e.Icon(101, 23, Start, actorId: 100);
        e.Synchronize([Enemy(1, 0x3029) with { Dead = true }, Enemy(9, 0x301D), Player(), Player(101)],
            Player().Position, Start.AddSeconds(1), playerId: 100);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/bait/")) == 1
            && e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/bait/")).Id == "treasure/bait/edge"
            && e.Hints(Player().Position).Contains("Bait bubble away!"), "Dead source removed derived edge or kept base output.");
        e.RemoveActor(100);
        e.Synchronize([Enemy(1, 0x3029), Player(p: Center + Vector3.UnitX * 15), Player(101)],
            Center + Vector3.UnitX * 15, Start.AddSeconds(2), playerId: 100);
        var display = e.AmbientAoes().Single(a => a.Id == "treasure/bait/0/display");
        Check(display.Position == Player().Position && display.OutlineOnly
            && !e.AmbientAoes().Any(a => a.Id == "treasure/bait/edge")
            && e.AmbientAoes().Any(a => a.Id == "treasure/bait/0/avoid"), "New same-ID target rebound frozen bait identity.");
    }
    private static void WormCircles()
    {
        var e = Encounter(0x3029);
        e.Synchronize([Enemy(1, 0x3029), Enemy(9, 0x302A), Enemy(10, 0x302A, Center + Vector3.UnitX * 5),
            Player()], Player().Position, Start.AddSeconds(1), playerId: 100);
        var original = e.TreasureCreatedCircles.ToArray();
        Check(original.Length == 2 && original.All(c => c.Activation == Start.AddSeconds(6.7) && c.Radius == 10), "Worm creation clock/size lost.");
        e.Synchronize([Enemy(1, 0x3029), Enemy(9, 0x302A, Center + Vector3.UnitX * 15) with
            { Dead = true, Targetable = false, EventState = 7 }, Player()], Player().Position, Start.AddSeconds(20), playerId: 100);
        Check(e.TreasureCreatedCircles.SequenceEqual(original), "Worm create circles moved, died, were destroyed or timed out.");
        e.StartCast(9, Cast(21714, started: 20), Start.AddSeconds(20));
        e.Finish(9, 21714, Start.AddSeconds(21));
        Check(e.TreasureCreatedCircles.Count == 2, "Inactive Hydroburst removed circle.");
        e.StartCast(1, Cast(21715, started: 22), Start.AddSeconds(22));
        e.Finish(1, 21715, Start.AddSeconds(23));
        Check(e.TreasureCreatedCircles.Single() == original[1], "Any caster AquaBurst did not remove global first circle.");
    }
    private static void CreatedGenerations()
    {
        foreach (var (oid, sourceOid, delay) in new[] { (0x3029u, 0x302Au, 5.7), (0x3D43u, 0x3D44u, 3.7) })
        {
            var e = Encounter(oid);
            e.Tick(Start.AddSeconds(1));
            e.ObjectCreated(9, Start, sourceOid, Center + Vector3.UnitX * 9);
            e.Synchronize([Enemy(1, oid), Player()], Player().Position, Start.AddSeconds(1.1), playerId: 100);
            e.Synchronize([Enemy(1, oid), Enemy(9, sourceOid), Player()], Player().Position, Start.AddSeconds(2), playerId: 100);
            Check(e.TreasureCreatedCircles.Count == 1 && e.TreasureCreatedCircles[0].Activation == Start.AddSeconds(1).AddSeconds(delay),
                "Early creation lost before frame or duplicated during first observation.");
            e.Synchronize([Enemy(1, oid), Player()], Player().Position, Start.AddSeconds(3), playerId: 100);
            e.Synchronize([Enemy(1, oid), Enemy(9, sourceOid), Player()], Player().Position, Start.AddSeconds(4), playerId: 100);
            Check(e.TreasureCreatedCircles.Count == 2, "Observed disappearance/recreation failed new circle generation.");
            e = Encounter(oid);
            e.ObjectCreated(9, Start, sourceOid, Center);
            e.RemoveActor(9);
            e.Tick(Start.AddSeconds(1));
            e.ObjectCreated(9, Start.AddSeconds(1), sourceOid, Center + Vector3.UnitX * 5);
            Check(e.TreasureCreatedCircles.Count == 2 && e.TreasureCreatedCircles[1].CreatedAt == Start.AddSeconds(1),
                "Destroy before first snapshot swallowed explicit same-ID new creation.");
        }
    }
    private static void Pillars()
    {
        var e = Encounter(0x3D43);
        e.Synchronize([Enemy(1, 0x3D43), Enemy(9, 0x3D44), Enemy(10, 0x3D44), Player()],
            Player().Position, Start.AddSeconds(1), playerId: 100);
        Check(e.TreasureCreatedCircles.Count == 2 && e.TreasureCreatedCircles.All(c => c.Radius == 6
            && c.Activation == Start.AddSeconds(4.7)), "Lyssa existing pillars did not bootstrap.");
        e.StartCast(1, Cast(32315, started: 2), Start.AddSeconds(2));
        e.Effect(1, 32315, 1, Start.AddSeconds(2));
        Check(e.TreasureCreatedCircles.Count == 2, "Pillar effect cleared creates.");
        e.Finish(1, 32315, Start.AddSeconds(3));
        e.Synchronize([Enemy(1, 0x3D43), Enemy(9, 0x3D44), Enemy(10, 0x3D44), Player()],
            Player().Position, Start.AddSeconds(4), playerId: 100);
        Check(e.TreasureCreatedCircles.Count == 0, "Finish failed global clear or alive pillars recreated consumed circles.");
    }
    private static void IceSequences()
    {
        var e = Encounter(0x3D43);
        e.StartCast(2, Cast(32310, p: Center + Vector3.UnitX * 10, rotation: 1), Start);
        e.StartCast(3, Cast(32312, started: 1, rotation: 2), Start.AddSeconds(1));
        Check(e.TreasureIceSequences.Count == 2 && e.TreasureIceSequences.All(s => Vector3.Distance(s.Origin, Center) < .05f)
            && e.TreasureIceSequences[0].Activation == Start.AddSeconds(5.5)
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/ice/")) == 2, "Lyssa start center, clock or current-only output differs.");
        e.StartCast(1, Cast(32313, started: 2), Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(3));
        e.Finish(1, 32313, Start.AddSeconds(3));
        Check(e.TreasureIceSequences[1].Order == 1 && e.TreasureIceSequences[1].Activation == Start.AddSeconds(5), "Reverse sequence did not advance from consume clock.");
        e.StartCast(1, Cast(32311, started: 4), Start.AddSeconds(4));
        e.Tick(Start.AddSeconds(5));
        e.Finish(1, 32311, Start.AddSeconds(5));
        Check(e.TreasureIceSequences.Count == 1 && e.TreasureIceSequences[0].Order == 1
            && e.TreasureIceSequences[0].Activation == Start.AddSeconds(7)
            && e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/ice/")).Shape is DonutTemplate,
            "One finish failed independent dispatch to both concentric components.");
    }
    private static void IceMatching()
    {
        var e = Encounter(0x3D43);
        var visual = Cast(32310);
        e.StartCast(2, visual, Start);
        e.Finish(2, 32310, Start.AddSeconds(.1));
        e.StartCast(3, Cast(32311, p: Center + Vector3.UnitX * 1.2f, started: 1), Start.AddSeconds(1));
        e.Finish(3, 32311, Start.AddSeconds(1.1));
        Check(e.TreasureIceSequences.Single().Order == 0, "Outside axis tolerance consumed sequence.");
        e.StartCast(3, Cast(32311, started: 2, rotation: .1f), Start.AddSeconds(2));
        e.Finish(3, 32311, Start.AddSeconds(2.1));
        Check(e.TreasureIceSequences.Single().Order == 1, "Source default rotation was replaced by helper rotation.");
        e.StartCast(1, Cast(32310, started: 2.5), Start.AddSeconds(2.5));
        e.StartCast(3, Cast(32311, p: Center + new Vector3(.9f, 0, .9f), started: 3), Start.AddSeconds(3));
        e.RemoveActor(3);
        Check(e.TreasureIceSequences.Count == 2 && e.TreasureIceSequences.All(s => s.Order == 1), "Destroy Finish used radial tolerance or did not advance.");
        e.StartCast(2, visual with { FinishAt = Start.AddSeconds(30) }, Start.AddSeconds(4), fromPacket: true);
        Check(e.TreasureIceSequences.Count == 2 && e.TreasureIceSequences.All(s => s.Order == 1), "Consumed sequence reset by late correction.");
        var active = Encounter(0x3D43);
        active.StartCast(2, visual, Start);
        active.StartCast(3, Cast(32311, started: 1), Start.AddSeconds(1));
        active.Finish(3, 32311, Start.AddSeconds(1.1));
        var consumed = active.TreasureIceSequences.Single();
        active.StartCast(2, visual with { Location = Center + Vector3.UnitX * 3 }, Start.AddSeconds(2), fromPacket: true);
        Check(active.Casts.ContainsKey(2) && active.TreasureIceSequences.Count == 1
            && active.TreasureIceSequences.Single() == consumed,
            "Live visual packet correction reset the consumed order, activation or sequence identity.");
    }
    private static void Stacks()
    {
        var e = Encounter(0x3D43);
        e.StartCast(2, Cast(32314, 101), Start);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/stack/") && a.Display).Shape is CircleTemplate { Radius: 6 }
            && e.Hints(Player().Position).Any(h => h.StartsWith("Stack")) && !e.PredictedDamage.Any(), "Lyssa eight-stack or prediction scope differs.");
        e.Synchronize([Enemy(1, 0x3D43), Enemy(2, 0x233C) with { Cast = Cast(32314, 101) }, Player(), Player(101) with { Dead = true }],
            Player().Position, Start.AddSeconds(1), playerId: 100);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/stack/")), "Dead stack target still displayed.");
        e.Synchronize([Enemy(1, 0x3D43), Enemy(2, 0x233C) with { Cast = Cast(32314, 101) }, Player(), Player(101)],
            Player().Position, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/stack/")), "Stack revival failed.");
        e.Finish(2, 32314, Start.AddSeconds(3));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/stack/")), "Actual stack finish retained marker.");
    }
    private static void Rewards()
    {
        foreach (var (oid, add, expected) in new[] { (0x300Fu, 0x3033u, 3), (0x3029u, 0x301Du, 5), (0x3D43u, 0x3D4Du, 2) })
        {
            var e = Encounter(oid);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(9, add), Enemy(10, 0x233C), Player()],
                Player().Position, Start.AddSeconds(1), playerId: 100);
            Check(e.Active?.BossOid == oid && NativeTreasureCatalog.Priority(oid, add) == expected
                && e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/enemy/")).Color == new Vector4(1, 0, 1, 1),
                "Reward All, priority or color missing.");
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(9, add) with { Dead = true },
                Enemy(10, oid == 0x3029 ? 0x302Au : 0x3D44u), Player()], Player().Position, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Bubble/pillar incorrectly retained encounter.");
        }
    }
}
