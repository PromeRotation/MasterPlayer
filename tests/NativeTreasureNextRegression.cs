using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureNextRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Fuath and Gargantuan active geometry and exclusions", Geometry);
        run("native treasure Fuath exact eight minimum and capacity navigation", Capacity);
        run("native treasure Fuath marked viewer colors differ from permitted joins", MultipleStacks);
        run("native treasure Fuath stack observed finish target FIFO and no effect clear", StackLifetime);
        run("native treasure Fuath dead target revival and destroyed reference freeze", StackIdentity);
        run("native treasure Gargantuan dead corpses retain AllDestroyed phase", GargantuanCorpses);
        run("native treasure Gargantuan cached first actor governs phase not later duplicate", GargantuanCache);
        run("native treasure Gargantuan cache freezes on destroy and checks completion before first cache", GargantuanGeneration);
        run("native treasure Gargantuan delayed Mash original target and reward ranks", GargantuanDamage);
        run("native treasure Gargantuan cached marker survives packet DTO replacement", GargantuanPacketMarker);
        run("native treasure Fuath dead marked viewer retains other stack danger color", DeadMarkedViewer);
    }
    private static DungeonActor Enemy(ulong id, uint oid) => new(id, oid, Center, InCombat: true, IsEnemy: true,
        ObjectIndex: (ushort)(id * 2), Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null, bool dead = false) =>
        new(id, 0, position ?? Center, Dead: dead, Hostile: false, Player: true);
    private static DungeonActor[] Party(int count) => [.. Enumerable.Range(0, count).Select(i => Player((ulong)(100 + i)))];
    private static DungeonEncounter Fuath(int count = 8)
    {
        var e = new DungeonEncounter(924) { TreasurePartyInput = [.. Enumerable.Range(0, count).Select(i => (ulong)(100 + i))] };
        e.Synchronize([Enemy(1, 0x302F), Enemy(2, 0x233C), Enemy(3, 0x233C), .. Party(count)], Center, Start, playerId: 100);
        return e;
    }
    private static DungeonAmbientAoe[] Stacks(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/stack/")).ToArray();
    private static DungeonAmbientAoe[] Enemies(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/enemy/")).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Geometry()
    {
        var f = Fuath();
        var rules = f.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 5 && rules[21736].Shape is CircleTemplate { Radius: 8 }
            && rules[21740].Shape is DungeonCrossTemplate { Length: 40, HalfWidth: 2.5f }
            && rules[21737].Shape == null && rules[21738].Hint == "Calls adds"
            && !rules.ContainsKey(21739) && !rules.ContainsKey(21735), "Fuath active components differ.");
        Check(NativeTreasureCatalog.EncounterOids(0x302F).SequenceEqual([0x302Fu, 0x3033u])
            && NativeTreasureCatalog.Priority(0x302F, 0x3033) == 1
            && NativeTreasureCatalog.Vulnerable(0x302F, 0x3033), "Fuath All/reward differs.");
        var g = new DungeonEncounter(1279);
        g.Synchronize([Enemy(1, 0x4898), Player()], Center, Start, playerId: 100);
        rules = g.Active!.Casts.ToDictionary(c => c.Action);
        Check(rules.Count == 14 && rules[43612].Shape is FanTemplate { Radius: 9 } fan && fan.AngleRadians == 3 * MathF.PI / 2
            && rules[43613].Shape == rules[43612].Shape && rules[43614].Shape is CircleTemplate { Radius: 6 }
            && rules[43618].Shape is FanTemplate { Radius: 15 } && rules[43619].Shape is CircleTemplate { Radius: 11 }
            && rules[43615].Shape == null && rules[43616].Shape == null
            && rules[43727].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -1.5f, MaxLocalX: 1.5f }
            && Enumerable.Range(32301, 5).All(a => rules[(uint)a].Shape is CircleTemplate { Radius: 7 })
            && !rules.ContainsKey(43617) && !rules.ContainsKey(43611), "Gargantuan source geometry or inactive Inhale differs.");
        Check(!NativeTreasureCatalog.Owns(0x489A, 1279) && NativeTreasureCatalog.Owns(0x4898, 1279),
            "Entry ownership must be Crab, not Gargantuan.");
    }
    private static void Capacity()
    {
        var e = Fuath(7);
        var cast = new DungeonCastObservation(21737, Center, 0, Start, Start.AddSeconds(5.3), 100);
        e.StartCast(2, cast, Start);
        Check(e.Hints(Center).Contains("Stack! (7/8)") && !e.PredictedDamage.Any(), "Minimum eight lowered or invented shared damage.");
        e.TreasurePartyInput = [.. Enumerable.Range(0, 8).Select(i => (ulong)(100 + i))];
        e.Synchronize([Enemy(1, 0x302F), Enemy(2, 0x233C) with { Cast = cast }, .. Party(8)],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Hints(Center).Contains("Stack (8/8)") && Stacks(e).Single(a => a.Display).Safe
            && Stacks(e).Any(a => a.Id.EndsWith("/buddy")), "Eight did not satisfy minimum or own buddy output missing.");
        e.Synchronize([Enemy(1, 0x302F), Enemy(2, 0x233C) with { Cast = cast }, .. Party(8), Player(108, Center + Vector3.UnitX * 12)],
            Center + Vector3.UnitX * 12, Start.AddSeconds(2), playerId: 108);
        Check(!Stacks(e).Single(a => a.Display).Safe && Stacks(e).Single(a => a.Navigate).Shape is CircleTemplate { Radius: 6 },
            "Outside ninth viewer joined full eight-person stack.");
        e.Synchronize([Enemy(1, 0x302F), Enemy(2, 0x233C) with { Cast = cast }, .. Party(8)],
            Center, Start.AddSeconds(3), playerId: 101);
        Check(Stacks(e).Single(a => a.Display).Safe && Stacks(e).Single(a => a.Navigate).Id.EndsWith("/join"),
            "Inside eighth participant was forced out.");
        e = Fuath(1);
        e.StartCast(2, cast, Start);
        Check(Stacks(e).Count(a => a.Display) == 1 && !Stacks(e).Any(a => a.Navigate), "Solo stack invented navigation.");
    }
    private static void MultipleStacks()
    {
        var e = Fuath();
        e.StartCast(2, new(21737, Center, 0, Start, Start.AddSeconds(5.3), 100), Start);
        e.StartCast(3, new(21737, Center, 0, Start, Start.AddSeconds(6.3), 101), Start);
        var aoes = Stacks(e);
        Check(aoes.Count(a => a.Display && a.Safe) == 1 && aoes.Count(a => a.Display && !a.Safe) == 1
            && aoes.Any(a => a.Id.EndsWith("/join")) && aoes.Any(a => a.Id.EndsWith("/buddy"))
            && e.Hints(Center).Contains("Stack! (8/8)"), "Marked observer colors, overlaps or capacity joins collapsed.");
        e = Fuath();
        e.StartCast(2, new(21737, Center, 0, Start, Start.AddSeconds(5.3), 999), Start);
        Check(Stacks(e).Length == 0, "Unresolved initial target created delayed mark.");
    }
    private static void StackLifetime()
    {
        var e = Fuath();
        e.StartCast(2, new(21737, Center, 0, Start, Start.AddSeconds(5.3), 100), Start);
        e.StartCast(3, new(21737, Center, 0, Start, Start.AddSeconds(7.3), 100), Start);
        e.Effect(2, 21737, 1, Start.AddSeconds(5), 100);
        e.Tick(Start.AddSeconds(30));
        Check(Stacks(e).Count(a => a.Display) == 2, "Effect or predicted time cleared stack.");
        e.Finish(3, 21737, Start.AddSeconds(31));
        Check(Stacks(e).Count(a => a.Display) == 1
            && Stacks(e).Single(a => a.Navigate).Activation == Start.AddSeconds(7.3), "Finish failed target FIFO.");
        e.RemoveActor(2, Start.AddSeconds(32));
        Check(Stacks(e).Length == 0, "Source destroy failed final actual finish.");
    }
    private static void StackIdentity()
    {
        var e = Fuath();
        var cast = new DungeonCastObservation(21737, Center, 0, Start, Start.AddSeconds(5.3), 100);
        e.StartCast(2, cast, Start);
        void Sync(double t, params DungeonActor[] party) => e.Synchronize(
            [Enemy(1, 0x302F), Enemy(2, 0x233C) with { Cast = cast }, .. party], Center, Start.AddSeconds(t), playerId: 100);
        Sync(1, Player(100, dead: true), Player(101));
        Check(Stacks(e).Length == 0, "Dead stack target not hidden.");
        var old = Center + Vector3.UnitX * 2;
        Sync(2, Player(100, old), Player(101));
        var position = Stacks(e).Single(a => a.Display).Position;
        Check(Stacks(e).Single(a => a.Display).Safe, "Same reference revival did not restore safe outline.");
        e.RemoveActor(100, Start.AddSeconds(3));
        Sync(4, Player(100, Center + Vector3.UnitZ * 12), Player(101));
        Check(Stacks(e).Single(a => a.Display).Position == position && !Stacks(e).Any(a => a.Id.EndsWith("/buddy"))
            && Stacks(e).Any(a => a.Id.EndsWith("/join")), "Same ID replaced frozen target or inherited old self branch.");
    }
    private static void GargantuanCorpses()
    {
        var e = new DungeonEncounter(1279);
        var crab = Enemy(1, 0x4898);
        e.Synchronize([crab, Player()], Center, Start, playerId: 100);
        e.Synchronize([crab with { Dead = true }, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null && !Enemies(e).Any(), "AllDestroyed was replaced by all dead.");
        e.Synchronize([Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "No cached Gargantuan failed null completion.");
    }
    private static void GargantuanCache()
    {
        var e = new DungeonEncounter(1279);
        var crab = Enemy(1, 0x4898);
        var first = Enemy(2, 0x489A);
        var later = Enemy(3, 0x489A);
        e.Synchronize([crab, first, later, Enemy(4, 0x4898), Enemy(5, 0x4899), Player()], Center, Start, playerId: 100);
        Check(Enemies(e).Length == 4 && Enemies(e).Any(a => a.Id.EndsWith("/2")) && !Enemies(e).Any(a => a.Id.EndsWith("/3")),
            "Cached first Gargantuan or all trash markers differ.");
        e.Synchronize([first, later, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null, "Cached live Gargantuan failed to retain phase after All destroy.");
        e.Synchronize([first with { Dead = true }, later, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active == null, "Later Gargantuan substituted for first cached reference.");
    }
    private static void GargantuanGeneration()
    {
        var e = new DungeonEncounter(1279);
        var crab = Enemy(1, 0x4898);
        var first = Enemy(2, 0x489A);
        e.Synchronize([crab, first, Player()], Center, Start, playerId: 100);
        e.RemoveActor(2, Start.AddSeconds(1));
        e.Synchronize([crab, first, Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(Enemies(e).Length == 1, "Recreated ID rebound destroyed cache.");
        e.Synchronize([first, Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Recreated Gargantuan retained old cached phase.");
        e = new DungeonEncounter(1279);
        e.Synchronize([crab, Player()], Center, Start, playerId: 100);
        e.Synchronize([first, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active == null, "First cache incorrectly preceded completion check.");
    }
    private static void GargantuanDamage()
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, 0x4898), Enemy(2, 0x489A), Player(), Player(101)], Center, Start, playerId: 100);
        e.StartCast(2, new(43615, Center, 0, Start, Start.AddSeconds(5.3), 100), Start);
        e.Finish(2, 43615, Start.AddSeconds(1));
        e.RemoveActor(2, Start.AddSeconds(2));
        Check(e.PredictedDamage.Single().TargetId == 100, "Mash history cleared on cancellation or source removal.");
        e.TreasureEffect(99, 43615, 1, 101, Start.AddSeconds(3));
        Check(e.PredictedDamage.Count() == 1, "Nonmatching main target consumed Mash.");
        e.TreasureEffect(99, 43615, 2, 100, Start.AddSeconds(4));
        Check(!e.PredictedDamage.Any(), "Matching cross-source Mash effect failed FIFO.");
        Check(Enumerable.Range(0x48B9, 5).Select(oid => NativeTreasureCatalog.Priority(0x4898, (uint)oid)).SequenceEqual([6, 5, 4, 3, 2])
            && NativeTreasureCatalog.Priority(0x4898, 0x48B7) == 2 && NativeTreasureCatalog.Priority(0x4898, 0x48B8) == 1
            && NativeTreasureCatalog.Priority(0x4898, 0x9999) == 0, "Reward priorities differ.");
    }

    private static void GargantuanPacketMarker()
    {
        var e = new DungeonEncounter(1279);
        e.Synchronize([Enemy(1, 0x4898), Enemy(2, 0x489A), Player()], Center, Start, playerId: 100);
        Check(Enemies(e).Any(a => a.Id.EndsWith("/2")), "Initial cached marker missing.");
        e.StartCast(2, new(43618, Center, 0, Start, Start.AddSeconds(4.3)), Start);
        Check(Enemies(e).Any(a => a.Id.EndsWith("/2")), "Packet replaced DTO and temporarily hid cached marker.");
        e.RemoveActor(2, Start.AddSeconds(1));
        Check(!Enemies(e).Any(a => a.Id.EndsWith("/2")), "Destroyed cache remained drawn.");
    }

    private static void DeadMarkedViewer()
    {
        var e = Fuath();
        var first = new DungeonCastObservation(21737, Center, 0, Start, Start.AddSeconds(5.3), 100);
        var second = first with { TargetId = 101 };
        e.StartCast(2, first, Start);
        e.StartCast(3, second, Start);
        e.Synchronize([Enemy(1, 0x302F), Enemy(2, 0x233C) with { Cast = first },
            Enemy(3, 0x233C) with { Cast = second }, .. Party(8).Select(a => a.Id == 100 ? a with { Dead = true } : a)],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(Stacks(e).Single(a => a.Display).Safe == false
            && Stacks(e).Any(a => a.Id.EndsWith("/join")) && !Stacks(e).Any(a => a.Id.EndsWith("/buddy")),
            "Dead stored mark must affect display color, not active navigation marks.");
    }
}
