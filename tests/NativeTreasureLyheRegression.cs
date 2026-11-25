using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureLyheRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(0, 5, -390);

    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Lyhe active geometry and inactive component boundaries", Geometry);
        run("native treasure Lyhe fixed concave room and primary ownership", Arena);
        run("native treasure Goliath water is hard obstacle with EventState-only filtering", Water);
        run("native treasure Goliath exact eight stack and viewer navigation", StackCapacity);
        run("native treasure Goliath stack finish FIFO and target generation", StackLifetime);
        run("native treasure Lyhe independent cast clocks and observed end", CastClocks);
        run("native treasure Lyhe AllDeadOrDestroyed rewards and enemy markers", Lifecycle);
        run("native treasure Lyhe single target and raidwide source party damage", Damage);
    }

    private static DungeonActor Enemy(ulong id, uint oid) => new(id, oid, Center, InCombat: true, IsEnemy: true,
        ObjectIndex: (ushort)(id * 2), Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center, Hostile: false, Player: true);
    private static DungeonActor[] Party(int count = 8) => [.. Enumerable.Range(0, count).Select(i => Player((ulong)(100 + i)))];
    private static DungeonEncounter Encounter(uint oid = 0x2BA5)
    {
        var e = new DungeonEncounter(879) { TreasurePartyInput = [.. Enumerable.Range(0, 8).Select(i => (ulong)(100 + i))] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C), .. Party()], Center, Start, playerId: 100);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static DungeonAmbientAoe[] Stacks(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/stack/")).ToArray();

    private static void Geometry()
    {
        var a = Encounter(0x2BA7).Active!.Casts.ToDictionary(c => c.Action);
        Check(a.Count == 15 && a[17885].Shape is CircleTemplate { Radius: 6 }
            && a[17883].Shape is DonutTemplate { InnerRadius: 5, OuterRadius: 10 } && a[17887].Shape == a[17883].Shape
            && a[17881].Shape is FanTemplate { Radius: 43.3f } big && big.AngleRadians == 2 * MathF.PI / 3
            && a[17886].Shape is FanTemplate { Radius: 19.9f } small && small.AngleRadians == MathF.PI / 3
            && a[17882].Shape == null && a[17880].Shape == null && !a.ContainsKey(17884), "Alichino active shapes differ.");
        var g = Encounter().Active!.Casts.ToDictionary(c => c.Action);
        Check(g.Count == 14 && g[17874].Shape is CircleTemplate { Radius: 15 }
            && g[17879].Shape is DungeonCrossTemplate { Length: 100, HalfWidth: 3.5f }
            && g[17878].Shape is RayRectangleTemplate { Length: 102.1f, MinLocalX: -3.5f, MaxLocalX: 3.5f }
            && g[17877].Shape == null && !g.ContainsKey(17875), "Goliath active components differ.");
        foreach (var rules in new[] { a, g })
            Check(rules[17854].Shape is CircleTemplate { Radius: 11 }
                && rules[17852].Shape is RayRectangleTemplate { Length: 15.23f, MinLocalX: -2, MaxLocalX: 2 }
                && rules[17853].Shape is FanTemplate { Radius: 15 } fan && fan.AngleRadians == 2 * MathF.PI / 3
                && Enumerable.Range(6448, 5).All(id => rules[(uint)id].Shape is CircleTemplate { Radius: 6.84f })
                && !rules.ContainsKey(17855), "Reward AOE or inactive attract differs.");
    }

    private static void Arena()
    {
        var e = Encounter();
        var arena = e.Arena!;
        var contour = arena.GetContours().Single();
        Check(contour.Count == 130 && arena.Contains(Center) && !arena.Contains(new(0, 5, -415))
            && arena.Contains(new(21, 5, -410)) && !arena.Contains(new(24, 5, -390))
            && MathF.Abs(arena.Position.X - .005f) < .001f && MathF.Abs(arena.Position.Z + 393.74f) < .001f,
            $"Real concave room or AABB center differs: vertices={contour.Count}, center={arena.Position}, " +
            $"contains={arena.Contains(Center)}/{arena.Contains(new(0, 5, -415))}/{arena.Contains(new(21, 5, -410))}/{arena.Contains(new(24, 5, -390))}.");
        Check(NativeTreasureCatalog.Owns(0x2BA5, 879) && NativeTreasureCatalog.Owns(0x2BA7, 879)
            && !NativeTreasureCatalog.Owns(0x2BA6, 879) && !NativeTreasureCatalog.Owns(0x2BAA, 879)
            && !NativeTreasureCatalog.Owns(0x2BA5, 688), "CFC confused with territory or add ownership expanded.");
    }

    private static void Water()
    {
        var e = Encounter();
        void Sync(byte state, bool dead, bool targetable) => e.Synchronize([Enemy(1, 0x2BA5), Enemy(9, 0x1E9998) with
            { EventState = state, Dead = dead, Targetable = targetable }, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Sync(0, true, false);
        var water = e.AmbientAoes().Single(a => a.Id == "treasure/water/9");
        Check(water.Shape is CircleTemplate { Radius: 8 } && water.Obstacle && water.Navigate && water.Display,
            "Water became timed danger or filtered dead/untargetable.");
        Sync(7, false, true);
        Check(!e.AmbientAoes().Any(a => a.Id == "treasure/water/9"), "EventState seven water retained.");
    }

    private static void StackCapacity()
    {
        var e = Encounter();
        var cast = new DungeonCastObservation(17877, Center, 0, Start, Start.AddSeconds(5.3), 100);
        e.StartCast(1, cast, Start);
        Check(e.Hints(Center).Contains("Stack (8/8)") && Stacks(e).Single(a => a.Display).Safe
            && Stacks(e).Any(a => a.Id.EndsWith("/buddy")) && !e.PredictedDamage.Any(), "Eight stack or own navigation differs.");
        e.Synchronize([Enemy(1, 0x2BA5) with { Cast = cast }, .. Party(7)], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Hints(Center).Contains("Stack! (7/8)"), "Minimum was relaxed below eight.");
        e.Synchronize([Enemy(1, 0x2BA5) with { Cast = cast }, .. Party(), Player(108, Center + Vector3.UnitX * 15)],
            Center + Vector3.UnitX * 15, Start.AddSeconds(2), playerId: 108);
        Check(!Stacks(e).Single(a => a.Display).Safe && Stacks(e).Single(a => a.Navigate).Shape is CircleTemplate { Radius: 6 },
            "Ninth viewer entered full stack.");
    }

    private static void StackLifetime()
    {
        var e = Encounter();
        var cast = new DungeonCastObservation(17877, Center, 0, Start, Start.AddSeconds(5.3), 101);
        e.StartCast(1, cast, Start);
        e.StartCast(2, cast with { FinishAt = Start.AddSeconds(7.3) }, Start);
        e.Tick(Start.AddSeconds(20));
        e.Effect(1, 17877, 1, Start.AddSeconds(20), 101);
        Check(Stacks(e).Count(a => a.Display) == 2, "Prediction time or effect cleared stack.");
        e.Finish(2, 17877, Start.AddSeconds(21));
        Check(Stacks(e).Count(a => a.Display) == 1 && Stacks(e).First(a => a.Navigate).Activation == Start.AddSeconds(7.3),
            "Finish must consume first original target entry.");
        e.RemoveActor(101, Start.AddSeconds(22));
        e.Synchronize([Enemy(1, 0x2BA5) with { Cast = cast }, Enemy(2, 0x233C), Player(),
            Player(101, Center + Vector3.UnitX * 20)], Center, Start.AddSeconds(23), playerId: 101);
        Check(Vector3.Distance(Stacks(e).Single(a => a.Display).Position, Center) < .05f && !Stacks(e).Any(a => a.Id.EndsWith("/buddy")),
            $"Same ID rebound destroyed target or new viewer inherited old mark: {string.Join(';', Stacks(e).Select(a => $"{a.Id}@{a.Position}"))}.");
        e.Finish(1, 17877, Start.AddSeconds(24));
        Check(Stacks(e).Length == 0, "Final finish retained stack.");
    }

    private static void CastClocks()
    {
        var e = Encounter(0x2BA7);
        e.StartCast(2, new(17881, Center, 0, Start, Start.AddSeconds(3.3)), Start);
        e.StartCast(3, new(17887, Center, 1, Start, Start.AddSeconds(7.3)), Start);
        e.Tick(Start.AddSeconds(20));
        Check(e.Casts.Count == 2 && e.Casts.Values.Select(c => c.FinishAt).Distinct().Count() == 2, "Independent real cast clocks expired.");
        e.Finish(2, 17881, Start.AddSeconds(21));
        Check(e.Casts.Count == 1 && e.Casts.Values.Single().Rule.Action == 17887, "One cast finish cleared another source.");
    }

    private static void Lifecycle()
    {
        foreach (var (oid, add) in new[] { (0x2BA7u, 0x2BA8u), (0x2BA5u, 0x2BA6u) })
        {
            var e = Encounter(oid);
            e.Synchronize([Enemy(1, oid), Enemy(2, add), Enemy(3, 0x2A05), Enemy(4, 0x2A06), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 4
                && NativeTreasureCatalog.Priority(oid, add) == 1 && NativeTreasureCatalog.Priority(oid, 0x2A05) == 2
                && Enumerable.Range(0, 5).All(i => NativeTreasureCatalog.Priority(oid, (uint)(0x2A06 + i)) == 6 - i),
                "All enemies or reward ranks differ.");
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(2, add) with { Dead = true },
                Enemy(3, 0x2A05) with { Dead = true }, Enemy(4, 0x2A06), Enemy(5, 0x2BAA), Player()],
                Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active != null, "Live reward failed to retain phase.");
            e.RemoveActor(4, Start.AddSeconds(3));
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(2, add) with { Dead = true },
                Enemy(3, 0x2A05) with { Dead = true }, Enemy(5, 0x2BAA), Player()],
                Center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null, "Dead corpses or helper incorrectly retained AllDeadOrDestroyed.");
        }
    }

    private static void Damage()
    {
        foreach (var (oid, single, raid) in new[] { (0x2BA7u, 17880u, 17882u), (0x2BA5u, 17873u, 17876u) })
        {
            var e = Encounter(oid);
            e.StartCast(1, new(single, Center, 0, Start, Start.AddSeconds(4.3), 101), Start);
            e.StartCast(2, new(raid, Center, 0, Start, Start.AddSeconds(6.3)), Start);
            Check(e.PredictedDamage.Count() == 9 && e.PredictedDamage.Count(d => d.TargetId == 101) == 2,
                "Single target or eight-slot raidwide damage differs.");
        }
    }
}
