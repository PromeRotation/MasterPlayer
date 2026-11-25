using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureWallRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? p = null) =>
        new(id, oid, p ?? Center, InCombat: true, IsEnemy: true, Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? p = null) =>
        new(id, 0, p ?? Center + Vector3.UnitZ * 5, Hostile: false, Player: true);
    private static DungeonEncounter Encounter(uint oid)
    {
        var e = new DungeonEncounter(794) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C), Player(), Player(101), Player(102)],
            Center + Vector3.UnitZ * 5, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, Vector3? origin = null, double finish = 5, float rotation = 0) =>
        new(action, origin ?? Center, rotation, Start, Start.AddSeconds(finish));
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure altar wall exact active rules and inactive visual", Rules);
        run("native treasure altar wall knockback model locked origin generation actual end", Lifetime);
        run("native treasure altar Dullahan sequential insertion order correction and wall hitbox", Sequence);
        run("native treasure altar Winged first effective knockback cone gate and immunity boundary", Winged);
        run("native treasure altar immunity event precedes stale snapshots and exact shape range", ImmunityEvents);
        run("native treasure altar Dullahan Terror Eye only unsafe landing display without KB navigation", Landing);
        run("native treasure altar Dullahan exact eight stack and target tracking", Stack);
        run("native treasure altar wall All rewards Feather exclusion and view switch", Rewards);
    }
    private static void Rules()
    {
        var d = Encounter(0x2533).Active!.Casts.ToDictionary(c => c.Action);
        Check(d.Count == 12 && d[13316].Shape is FanTemplate { Radius: 11.8f } fan
            && fan.AngleRadians == 2 * MathF.PI / 3 && d[13477].Shape is CircleTemplate { Radius: 6 }
            && d[13644].Shape == d[13477].Shape && d[13315].Shape == null && d[13314].Shape == null,
            "Dullahan active component shape differs.");
        var w = Encounter(0x253D).Active!.Casts.ToDictionary(c => c.Action);
        Check(w.Count == 12 && w[13376].Shape is RayRectangleTemplate { Length: 11.36f, MinLocalX: -2, MaxLocalX: 2 }
            && w[13377].Shape is FanTemplate { Radius: 43.36f } cone && cone.AngleRadians == MathF.PI / 3
            && w[13379].Shape is CircleTemplate { Radius: 6 }
            && w[13381].Shape is RayRectangleTemplate { Length: 40.5f, MinLocalX: -1.5f, MaxLocalX: 1.5f }
            && w[13380].Shape == null && !w.ContainsKey(13378), "Winged active rules differ.");
    }
    private static void Lifetime()
    {
        var e = Encounter(0x2533);
        var origin = Center - Vector3.UnitZ * 4;
        e.StartCast(1, Cast(13314, origin), Start);
        var source = e.TreasureWallKnockbackSources.Single();
        Check(source.Distance == 20 && source.StopAtWall && source.EffectShape == null
            && source.Cast.Location == origin && source.Cast.StartedAt == Start && source.Cast.FinishAt == Start.AddSeconds(5),
            "Raw source identity, timing, lock or displacement rule missing.");
        e.Tick(Start.AddSeconds(30));
        Check(e.TreasureWallKnockbackSources.Count == 1, "Predicted finish cleared live knockback.");
        e.Finish(1, 13314, Start.AddSeconds(31));
        Check(e.TreasureWallKnockbackSources.Count == 0, "Actual finish retained knockback.");
        e.StartCast(2, Cast(13314) with { StartedAt = Start.AddSeconds(32), FinishAt = Start.AddSeconds(37) }, Start.AddSeconds(32));
        e.RemoveActor(2, Start.AddSeconds(33));
        Check(e.TreasureWallKnockbackSources.Count == 0, "Destroyed caster retained knockback.");
        e.StartCast(3, Cast(13314) with { StartedAt = Start.AddSeconds(34), FinishAt = Start.AddSeconds(39) }, Start.AddSeconds(34));
        e.ResetFight();
        Check(e.TreasureWallKnockbackSources.Count == 0, "Fight reset retained source.");
    }
    private static void Sequence()
    {
        var e = Encounter(0x2533);
        e.StartCast(2, Cast(13314, Center, 8), Start);
        e.StartCast(3, Cast(13314, Center + Vector3.UnitX * 10, 4), Start);
        var movements = e.TreasureWallMovements(Player()).ToArray();
        Check(movements.Length == 2 && movements[0].Source.Cast.Source == 2 && movements[1].Source.Cast.Source == 3
            && movements[1].From == movements[0].To && Vector3.Distance(movements[0].To, Center + Vector3.UnitZ * 18.999f) < .02f,
            "Sources sorted by finish, not chained or wall truncation lost.");
        e.StartCast(2, Cast(13314, Center - Vector3.UnitZ * 2, 8), Start.AddSeconds(.1));
        Check(e.TreasureWallKnockbackSources.Count == 2 && e.TreasureWallKnockbackSources[0].Cast.Source == 2
            && e.TreasureWallKnockbackSources[0].Cast.Location == Center - Vector3.UnitZ * 2,
            "Same-generation correction duplicated or reordered source.");
        var normal = e.TreasureWallMovements(Player() with { HitboxRadius = .5f }).First().To;
        var large = e.TreasureWallMovements(Player() with { HitboxRadius = 2 }).First().To;
        Check(MathF.Abs(normal.Z - large.Z - 1.5f) < .002f, "Hitbox wall margin not preserved.");
    }
    private static void Winged()
    {
        var e = Encounter(0x253D);
        e.StartCast(2, Cast(13377, rotation: MathF.PI), Start);
        e.StartCast(3, Cast(13377, finish: 7), Start);
        var movements = e.TreasureWallMovements(Player()).ToArray();
        Check(movements.Length == 1 && movements[0].Source.Cast.Source == 3, "Max1 counted shape-missed source.");
        Check(!e.TreasureWallMovements(Player(p: Center)).Any(), "Zero direction produced movement.");
        var later = Encounter(0x253D);
        later.StartCast(2, Cast(13377), Start);
        later.StartCast(3, Cast(13377, finish: 7), Start);
        Check(later.TreasureWallMovements(Player() with
            { StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(6) } }).Single().Source.Cast.Source == 3,
            "Max1 counted an immune first source.");
        var zeroFirst = Encounter(0x253D);
        zeroFirst.StartCast(2, Cast(13377, Player().Position), Start);
        zeroFirst.StartCast(3, Cast(13377), Start);
        Check(zeroFirst.TreasureWallMovements(Player()).Single().Source.Cast.Source == 3,
            "Max1 counted zero-direction first source.");
        var until = Start.AddSeconds(7);
        Check(e.TreasureWallMovements(Player() with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = until } }).Any()
            && !e.TreasureWallMovements(Player() with { StatusExpirations = new Dictionary<uint, DateTime> { [1209] = until.AddTicks(1) } }).Any(),
            "Immunity expiry must be strictly later than activation.");
        e.Finish(2, 13377, Start.AddSeconds(1));
        e.Finish(3, 13377, Start.AddSeconds(1));
        e.StartCast(2, Cast(13377) with { StartedAt = Start.AddSeconds(2), FinishAt = Start.AddSeconds(7) }, Start.AddSeconds(2));
        Check(e.TreasureWallKnockbackSources.Single().Cast.StartedAt == Start.AddSeconds(2), "New cast generation reused old timing.");
    }
    private static void Landing()
    {
        var e = Encounter(0x2533);
        e.StartCast(1, Cast(13314), Start);
        var landing = e.TreasureWallMovements(Player()).Single().To;
        e.StartCast(2, Cast(13477, landing), Start);
        Check(!e.Hints(Player().Position).Contains("About to be knocked into danger!"), "Cloudcover incorrectly treated as Terror Eye.");
        e.StartCast(3, Cast(13644, landing), Start);
        Check(e.TreasureWallMovements(Player()).Single().Unsafe
            && e.Hints(Player().Position).Contains("About to be knocked into danger!")
            && e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/kb/")).Count() == 2
            && e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/kb/")).All(a => a.Display && !a.Navigate),
            "Terror Eye destination hint or display-only projection differs.");
        e.Finish(3, 13644, Start.AddSeconds(2));
        Check(!e.TreasureWallMovements(Player()).Single().Unsafe, "Finished Terror Eye retained landing danger.");
        e.StartCast(2, Cast(13314, Center + Vector3.UnitZ * 40) with
            { StartedAt = Start.AddSeconds(2.1), FinishAt = Start.AddSeconds(7.1) }, Start.AddSeconds(2.1));
        e.StartCast(3, Cast(13644, landing) with
            { StartedAt = Start.AddSeconds(2.1), FinishAt = Start.AddSeconds(7.1) }, Start.AddSeconds(2.1));
        var chain = e.TreasureWallMovements(Player()).ToArray();
        Check(chain.Length == 2 && chain[0].Unsafe && !chain[1].Unsafe
            && e.Hints(Player().Position).Contains("About to be knocked into danger!"),
            "Safe final destination hid unsafe intermediate landing.");
    }
    private static void ImmunityEvents()
    {
        var e = Encounter(0x253D);
        e.StartCast(2, Cast(13377), Start);
        Check(!e.TreasureWallMovements(Player(p: Center + Vector3.UnitZ * (43.36f + .00005f))).Any(),
            "Public geometry tolerance enlarged knockback effect shape.");
        e.Tick(Start.AddSeconds(1));
        e.StatusChanged(100, 160, true, Start.AddSeconds(1), expiresAt: Start.AddSeconds(6));
        Check(!e.TreasureWallMovements(Player()).Any(), "Status gain waited for world snapshot.");
        e.Tick(Start.AddSeconds(1.1));
        e.StatusChanged(100, 160, false, Start.AddSeconds(1.1));
        Check(e.TreasureWallMovements(Player() with
            { StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(6) } }).Any(),
            "Status loss retained stale snapshot immunity.");
    }
    private static void Stack()
    {
        var e = Encounter(0x2533);
        e.StartCast(2, Cast(13315) with { TargetId = 101 }, Start);
        Check(e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/stack/") && a.Display) == 1
            && e.AmbientAoes().Any(a => a.Id == "treasure/stack/join" && a.Navigate),
            "Eight stack display / join missing.");
        e.Finish(2, 13315, Start.AddSeconds(1));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/stack/")), "Stack finish leaked.");
    }
    private static void Rewards()
    {
        Check(NativeTreasureCatalog.EncounterOids(0x2533).SequenceEqual([0x2533u, 0x2563u, 0x2547u, 0x2548u, 0x2546u, 0x2549u, 0x254Au, 0x2545u])
            && !NativeTreasureCatalog.EncounterOids(0x253D).Contains(0x253Eu)
            && NativeTreasureCatalog.Priority(0x2533, 0x2563) == 1
            && NativeTreasureCatalog.Priority(0x2533, 0x2545) == 2
            && NativeTreasureCatalog.Priority(0x253D, 0x2544) == 2
            && NativeTreasureCatalog.Priority(0x253D, 0x2545) == 1, "All or reward priority differs.");
        var e = Encounter(0x253D);
        e.StartCast(2, Cast(13377), Start);
        e.Synchronize([Enemy(1, 0x253D), Enemy(2, 0x233C) with { Cast = Cast(13377) },
            Enemy(3, 0x253E), Player(), Player(101, Center - Vector3.UnitZ * 5)], Center - Vector3.UnitZ * 5,
            Start.AddSeconds(1), playerId: 101);
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/kb/"))
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/enemy/")) == 1
            && e.TreasureWallKnockbackSources.Count == 1, "View switch lost model or Feather incorrectly marked.");
    }
}
