using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureCanalsRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(0, 0, -420);
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? p = null) =>
        new(id, oid, p ?? Center, InCombat: true, IsEnemy: true, Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id, Vector3? p = null) =>
        new(id, 0, p ?? Center, Hostile: false, Player: true);
    private static DungeonEncounter Encounter(uint territory, uint oid)
    {
        var e = new DungeonEncounter(territory) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C), Player(100), Player(101), Player(102)],
            Center, Start, playerId: 100);
        return e;
    }
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure canals and Lucky Face exact active rule parameters", Geometry);
        run("native treasure canals and Lucky Face exact arena unions and entrance cuts", Arenas);
        run("native treasure canals cleave source OID and all primary enemy markers", Cleaves);
        run("native treasure canals cleave remains while casting and excludes dead untargetable", CleaveLifetime);
        run("native treasure Icebeast gaze default range locked source and actual end", Gaze);
        run("native treasure Lucky Face spreads radius target FIFO and destroyed reference", Spreads);
        run("native treasure canals rewards priorities and complete All sets", Rewards);
    }
    private static void Geometry()
    {
        var l = Encounter(1000, 0x377F).Active!.Casts.ToDictionary(c => c.Action);
        Check(l.Count == 17 && new uint[] { 27990, 27994, 27992, 27996 }.All(a => l[a].Shape is FanTemplate { Radius: 20 } f && f.AngleRadians == MathF.PI)
            && new uint[] { 27998, 28190 }.All(a => l[a].Shape is CircleTemplate { Radius: 10 })
            && new uint[] { 28189, 28000 }.All(a => l[a].Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 20 })
            && l[28001].Shape is CircleTemplate { Radius: 6 } && l[28002].Shape == null
            && new uint[] { 27988, 27989, 27991, 27993, 27995, 27997, 27999, 28090, 28091, 28045, 27983 }.All(a => !l.ContainsKey(a)),
            "Lucky Face inactive visuals or ordinary shapes differ.");
        var a = Encounter(725, 0x1FCC).Active!.Casts.ToDictionary(c => c.Action);
        Check(a.Count == 11 && a[9892].Shape is FanTemplate { Radius: 20 } f && f.AngleRadians == 2 * MathF.PI / 3
            && a[9894].Shape is CircleTemplate { Radius: 14 }
            && a[9891].Shape is CircleTemplate { Radius: 6 } && a[5352].Shape == a[9891].Shape
            && a[8598].Shape is FanTemplate { Radius: 8.42f }
            && new uint[] { 618, 8727, 8629, 8660, 967, 966, 969, 968, 971 }.All(id => !a.ContainsKey(id)),
            "Airavata component activation differs.");
        var ice = Encounter(712, 0x1F15).Active!.Casts.ToDictionary(c => c.Action);
        Check(ice.Count == 6 && ice[9557].Shape == null && ice[9558].Shape is FanTemplate { Radius: 45.5f } cone && cone.AngleRadians == MathF.PI / 2
            && ice[4486].Shape is CircleTemplate { Radius: 6 } && ice[4487].Shape is CircleTemplate { Radius: 4.56f }
            && !ice.ContainsKey(9556) && !ice.ContainsKey(967), "Icebeast active parameters differ.");
    }
    private static void Arenas()
    {
        var canal = NativeTreasureArenas.Canals(5);
        Check(canal.Contains(new(27, 5, -420)) && canal.Contains(new(0, 5, -447))
            && !canal.Contains(new(8, 5, -444)) && !canal.Contains(new(0, 5, -401))
            && MathF.Abs(canal.Position.Z + 425.425f) < .001f, "Canals union, entrance cut or final center lost.");
        var lucky = NativeTreasureArenas.LuckyFace(5);
        Check(lucky.Contains(new(0, 5, -460)) && !lucky.Contains(new(0, 5, -440.8f))
            && MathF.Abs(lucky.Position.Z + 460.25f) < .001f, "Lucky Face source circle cut or final center lost.");
        Check(!NativeTreasureCatalog.Owns(0x1FCC, 276) && !NativeTreasureCatalog.Owns(0x377F, 819)
            && NativeTreasureCatalog.Owns(0x1F15, 712), "Source CFC used as territory.");
    }
    private static void Cleaves()
    {
        foreach (var (territory, oid, cleaver) in new[] { (725u, 0x1FCCu, 0x2064u), (712u, 0x1F15u, 0x1EBFu) })
        {
            var e = Encounter(territory, oid);
            e.Synchronize([Enemy(1, oid), Enemy(2, cleaver) with { TargetId = 101 }, Enemy(3, 0x2545) with { TargetId = 101 },
                Enemy(4, oid), Player(100), Player(101, Center + Vector3.UnitZ * 4)], Center, Start.AddSeconds(1), playerId: 100);
            var aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cleave/")).ToArray();
            Check(aoes.Length == 2 && aoes.All(a => a.Id.Contains("/2/"))
                && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == (territory == 725 ? 3 : 2),
                "Cleave wrong OID or Airavata all-primary markers differ.");
            e.Synchronize([Enemy(1, oid), Enemy(2, cleaver) with { TargetId = 100 },
                Player(100, Center + Vector3.UnitZ * 4), Player(101, Center + Vector3.UnitX * 4)],
                Center + Vector3.UnitZ * 4, Start.AddSeconds(2), playerId: 100);
            aoes = e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/cleave/")).ToArray();
            Check(aoes.Count(a => a.Display) == 1 && aoes.Count(a => a.Navigate) == 1
                && aoes.Single(a => a.Navigate).Shape is FanTemplate { Radius: 100 },
                "Target viewer did not receive source-party direction constraint.");
        }
    }
    private static void CleaveLifetime()
    {
        var e = Encounter(712, 0x1F15);
        var cast = new DungeonCastObservation(5352, Center, 0, Start, Start.AddSeconds(3.3));
        void Sync(bool dead, bool targetable) => e.Synchronize([Enemy(1, 0x1F15),
            Enemy(2, 0x1EBF) with { TargetId = 101, Cast = cast, Dead = dead, Targetable = targetable },
            Player(100), Player(101, Center + Vector3.UnitZ * 4)], Center, Start.AddSeconds(1), playerId: 100);
        Sync(false, true);
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/cleave/2/avoid"), "Casting disabled activeWhileCasting cleave.");
        Sync(true, true);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cleave/")), "Dead cleaver retained.");
        Sync(false, false);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/cleave/")), "Untargetable cleaver retained.");
    }
    private static void Gaze()
    {
        var e = Encounter(712, 0x1F15);
        var eye = Center + Vector3.UnitZ * 65;
        e.StartCast(1, new(9557, eye, 0, Start, Start.AddSeconds(4.3)), Start);
        e.Tick(Start.AddSeconds(4));
        Check(e.Gazes.Count() == 1 && e.GazeFacing(Center, 0) is float
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/gaze/")) == 3
            && e.Hints(Center).Contains("Turn away from gaze!"), "Default 10000 gaze or PR-facing model missing.");
        e.Tick(Start.AddSeconds(20));
        Check(e.Gazes.Count() == 1, "Expected gaze expiry ended observed cast.");
        e.Finish(1, 9557, Start.AddSeconds(21));
        Check(!e.Gazes.Any() && e.GazeFacing(Center, 0) == null, "Actual end retained facing request.");
    }
    private static void Spreads()
    {
        var e = Encounter(1000, 0x377F);
        var cast = new DungeonCastObservation(28002, Center, 0, Start, Start.AddSeconds(5.3), 101);
        e.StartCast(2, cast, Start);
        e.StartCast(3, cast with { FinishAt = Start.AddSeconds(7.3) }, Start);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("quest/spread/") && a.Display) == 2
            && e.AmbientAoes().Where(a => a.Id.StartsWith("quest/spread/") && a.Navigate).All(a => a.Shape is CircleTemplate { Radius: 7 }),
            "Spread R6 display / R7 navigation differs.");
        e.Tick(Start.AddSeconds(20));
        e.Finish(3, 28002, Start.AddSeconds(21));
        var first = e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display);
        e.RemoveActor(101, Start.AddSeconds(22));
        e.Synchronize([Enemy(1, 0x377F), Enemy(2, 0x233C) with { Cast = cast }, Enemy(3, 0x233C),
            Player(100), Player(101, Center + Vector3.UnitX * 20)], Center, Start.AddSeconds(23), playerId: 101);
        Check(e.AmbientAoes().Single(a => a.Id.StartsWith("quest/spread/") && a.Display).Position == first.Position
            && e.AmbientAoes().Any(a => a.Id.EndsWith("/avoid") && a.Activation == Start.AddSeconds(7.3)),
            "Target FIFO or destroyed spread reference replaced.");
    }
    private static void Rewards()
    {
        Check(NativeTreasureCatalog.EncounterOids(0x1FCC).Length == 16
            && NativeTreasureCatalog.EncounterOids(0x1F15).SequenceEqual([0x1F15u, 0x1EBFu, 0x1F0Eu, 0x1F0Fu])
            && NativeTreasureCatalog.EncounterOids(0x377F).Length == 6, "All sets differ.");
        Check(Enumerable.Range(0, 5).All(i => NativeTreasureCatalog.Priority(0x377F, (uint)(0x3808 + i)) == 6 - i
            && NativeTreasureCatalog.Priority(0x1FCC, (uint)(0x1FCD + i)) == 7 - i)
            && NativeTreasureCatalog.Priority(0x377F, 0x377F) == 1
            && NativeTreasureCatalog.Priority(0x1FCC, 0x2063) == 3 && NativeTreasureCatalog.Priority(0x1FCC, 0x2064) == 2
            && NativeTreasureCatalog.Priority(0x1F15, 0x1EBF) == 2 && NativeTreasureCatalog.Priority(0x1F15, 0x1F0E) == 1,
            "Rewards priorities differ.");
    }
}
