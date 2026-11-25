using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureWaterRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true, Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 5, Hostile: false, Player: true);
    private static DungeonEncounter Encounter(uint oid)
    {
        var e = new DungeonEncounter(794) { TreasurePartyInput = [100, 101, 102] };
        e.Synchronize([Enemy(1, oid), Enemy(2, 0x233C), Enemy(3, 0x233C), Player(), Player(101), Player(102)],
            Player().Position, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, ulong target = 100, Vector3? p = null) =>
        new(action, p ?? Center, 0, Start, Start.AddSeconds(5), target);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure altar water complete rules and HydroPush has no KB", Rules);
        run("native treasure altar Airavata icon Source latest target dynamic KB and actual finish", Icons);
        run("native treasure altar Airavata destroyed icon generation never rebinds", IconGeneration);
        run("native treasure altar Airavata first cast bait primary origin source direction text asymmetry", Baits);
        run("native treasure altar Airavata cast bait death retention effect and first source finish", BaitLifetime);
        run("native treasure altar Kelpie creation locked puddles capacity expiry and global finish", Puddles);
        run("native treasure altar Kelpie packet creation preceding observation and recreated identity", PuddleGeneration);
        run("native treasure altar Kelpie wall KB landing hazard and arena-centered navigation despite immunity", KelpieKnockback);
        run("native treasure altar water delayable FIFO not consumed by cancel and reward All", Delayed);
    }
    private static void Rules()
    {
        var a = Encounter(0x2543).Active!.Casts.ToDictionary(c => c.Action);
        Check(a.Count == 7 && a[13372].Shape is CircleTemplate { Radius: 6 }
            && a[13373].Shape is FanTemplate { Radius: 30 } && a[13375].Shape is CircleTemplate { Radius: 14 }
            && a[13371].Shape == null && a[13374].Shape == null, "Airavata active rules differ.");
        var e = Encounter(0x2537);
        var k = e.Active!.Casts.ToDictionary(c => c.Action);
        Check(k.Count == 12 && k[13439].Shape is CircleTemplate { Radius: 5 }
            && k[13442].Shape is RayRectangleTemplate { Length: 49.4f, MinLocalX: -22, MaxLocalX: 22 }
            && k[13440].Shape == null && k[13443].Shape == null && !k.ContainsKey(13441), "Kelpie rules or inactive Gallop differ.");
        e.StartCast(1, Cast(13442), Start);
        Check(!e.TreasureWallKnockbackSources.Any(), "Hydro Push invented an unactivated KB component.");
    }
    private static void Icons()
    {
        var e = Encounter(0x2543);
        e.Tick(Start.AddSeconds(1));
        e.Icon(101, 23, Start, actorId: 100);
        Check(e.TreasureBuffetTarget is { } icon && icon.Target.Id == 100 && icon.StartedAt == Start.AddSeconds(1)
            && icon.Activation == Start.AddSeconds(1).AddSeconds(3.1) && icon.Distance == 20 && icon.StopAtWall
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/buffet/kb/")) == 2
            && e.AmbientAoes().Any(a => a.Id == "treasure/buffet/edge" && a.Navigate),
            "Icon packet target used instead of Source, clock or raw displacement model missing.");
        e.Synchronize([Enemy(1, 0x2543, Center + Vector3.UnitZ * 2), Player(position: Center + Vector3.UnitX * 4), Player(101)],
            Center + Vector3.UnitX * 4, Start.AddSeconds(2), playerId: 100);
        var landing = e.AmbientAoes().Single(a => a.Id == "treasure/buffet/kb/player").Position;
        Check(landing.X > 104 && landing.Z < 100 && e.TreasureBuffetTarget!.Activation == Start.AddSeconds(1).AddSeconds(3.1),
            "Icon KB did not track primary and target independently of activation.");
        e.Tick(Start.AddSeconds(20));
        Check(e.TreasureBuffetTarget != null, "Icon target expired by clock.");
        e.Icon(100, 23, Start, actorId: 101);
        Check(e.TreasureBuffetTarget!.Target.Id == 101 && !e.AmbientAoes().Any(a => a.Id == "treasure/buffet/edge"),
            "New icon did not replace previous target.");
        e.StartCast(1, Cast(13374, 101) with { StartedAt = Start.AddSeconds(20), FinishAt = Start.AddSeconds(25) }, Start.AddSeconds(20));
        e.Finish(1, 13374, Start.AddSeconds(21));
        Check(e.TreasureBuffetTarget == null && !e.TreasureBuffetBaits.Any(), "Real Buffet finish did not clear both states.");
    }
    private static void IconGeneration()
    {
        var e = Encounter(0x2543);
        e.Icon(101, 23, Start, actorId: 100);
        e.RemoveActor(100);
        e.Synchronize([Enemy(1, 0x2543), Player(position: Center + Vector3.UnitX * 15), Player(101)],
            Center + Vector3.UnitX * 15, Start.AddSeconds(1), playerId: 100);
        Check(e.TreasureBuffetTarget is { Removed: true } && !e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/buffet/kb/")),
            "Destroyed icon target rebound same-ID actor.");
        e.Icon(101, 23, Start, actorId: 100);
        Check(e.TreasureBuffetTarget is { Removed: false } && e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/buffet/kb/")),
            "Fresh icon did not capture new generation.");
        e.StatusChanged(100, 160, true, Start.AddSeconds(1.1), expiresAt: Start.AddSeconds(10));
        Check(e.AmbientAoes().Any(a => a.Id == "treasure/buffet/edge")
            && !e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/buffet/kb/")), "Immunity removed bait-away navigation.");
    }
    private static void Baits()
    {
        var e = Encounter(0x2543);
        var cast = Cast(13374, 101);
        e.Synchronize([Enemy(1, 0x2543), Enemy(2, 0x233C, Center + Vector3.UnitX * 10) with { Cast = cast },
            Player(position: Center + Vector3.UnitZ * 2), Player(101, Center + Vector3.UnitZ * 10), Player(102)],
            Center + Vector3.UnitZ * 2, Start.AddSeconds(1), playerId: 100);
        var cone = e.AmbientAoes().Single(a => a.Id == "treasure/buffet/cone");
        Check(cone.Position == Center && MathF.Abs(cone.Rotation + MathF.PI / 4) < .001f && !cone.OutlineOnly
            && e.AmbientAoes().Any(a => a.Id == "treasure/buffet/avoid")
            && !e.Hints(Player().Position).Contains("GTFO from baited aoe!") && !e.PredictedDamage.Any(),
            "Primary origin, caster rotation, target-centered text or no Buffet prediction differs.");
        e.Synchronize([Enemy(1, 0x2543), Enemy(2, 0x233C, Center + Vector3.UnitX * 10) with { Cast = cast },
            Player(), Player(101, Center + Vector3.UnitZ * 10), Player(102)], Center + Vector3.UnitZ * 10,
            Start.AddSeconds(2), playerId: 101);
        Check(e.AmbientAoes().Single(a => a.Id == "treasure/buffet/cone").OutlineOnly
            && !e.AmbientAoes().Any(a => a.Id == "treasure/buffet/avoid" || a.Id.Contains("/party/")),
            "Bait target received base party-direction navigation.");
    }
    private static void BaitLifetime()
    {
        var e = Encounter(0x2543);
        var cast = Cast(13374, 101);
        e.StartCast(2, cast, Start);
        e.StartCast(3, cast with { TargetId = 102, FinishAt = Start.AddSeconds(3) }, Start);
        e.TreasureEffect(2, 13374, 1, 101, Start.AddSeconds(1));
        e.Synchronize([Enemy(1, 0x2543), Enemy(2, 0x233C) with { Cast = cast, Dead = true },
            Enemy(3, 0x233C) with { Cast = cast with { TargetId = 102, FinishAt = Start.AddSeconds(3) } },
            Player(), Player(101) with { Dead = true }, Player(102)], Player().Position, Start.AddSeconds(10), playerId: 100);
        Check(e.TreasureBuffetBaits.Count == 2 && e.AmbientAoes().Count(a => a.Id == "treasure/buffet/cone") == 1,
            "Death, effect or expected expiry incorrectly cleared baits.");
        e.Finish(3, 13374, Start.AddSeconds(11));
        Check(e.TreasureBuffetBaits.Count == 1 && e.TreasureBuffetBaits[0].Source.Id == 2, "Finish removed wrong source bait.");
        e.RemoveActor(101);
        e.Synchronize([Enemy(1, 0x2543), Enemy(2, 0x233C) with { Cast = cast },
            Player(), Player(101, Center + Vector3.UnitX * 15)], Player().Position, Start.AddSeconds(12), playerId: 101);
        Check(e.TreasureBuffetBaits[0].TargetRemoved && e.TreasureBuffetBaits[0].Target.Position == Player(101).Position
            && !e.AmbientAoes().Single(a => a.Id == "treasure/buffet/cone").OutlineOnly,
            "Destroyed cast target lost frozen reference or rebound.");
    }
    private static void Puddles()
    {
        var e = Encounter(0x2537);
        e.Synchronize([Enemy(1, 0x2537), .. Enumerable.Range(2, 4).Select(i => Enemy((ulong)i, 0x255B, Center + Vector3.UnitX * i)), Player()],
            Player().Position, Start.AddSeconds(1), playerId: 100);
        var originals = e.TreasureHydrospheres.ToArray();
        Check(originals.Length == 4 && originals.All(p => p.CreatedAt == Start.AddSeconds(1)
            && p.Activation == Start.AddSeconds(9.6)), "Bootstrap creation timing or no maximum count lost.");
        e.Synchronize([Enemy(1, 0x2537), .. Enumerable.Range(2, 4).Select(i =>
            Enemy((ulong)i, 0x255B, Center + Vector3.UnitZ * 10) with { Dead = true, Targetable = false, EventState = 7 }), Player()],
            Player().Position, Start.AddSeconds(20), playerId: 100);
        Check(e.TreasureHydrospheres.SequenceEqual(originals)
            && e.AmbientAoes().Count(a => a.Id.StartsWith("treasure/hydrosphere/") && a.Navigate && a.Display) == 4,
            "Puddle followed entity, died or expired.");
        e.StartCast(1, Cast(13443), Start.AddSeconds(20));
        e.Effect(1, 13443, 1, Start.AddSeconds(21));
        Check(e.TreasureHydrospheres.Count == 4, "Effect cleared all puddles.");
        e.Finish(1, 13443, Start.AddSeconds(22));
        Check(e.TreasureHydrospheres.Count == 0, "Any-caster real Bloody Puddle end did not clear all.");
    }
    private static void PuddleGeneration()
    {
        var e = Encounter(0x2537);
        e.Tick(Start.AddSeconds(1));
        e.ObjectCreated(9, Start, 0x255B, Center + Vector3.UnitX * 9);
        e.Synchronize([Enemy(1, 0x2537), Player()], Player().Position, Start.AddSeconds(1.1), playerId: 100);
        e.Synchronize([Enemy(1, 0x2537), Enemy(9, 0x255B, Center + Vector3.UnitZ * 9), Player()],
            Player().Position, Start.AddSeconds(2), playerId: 100);
        Check(e.TreasureHydrospheres.Count == 1 && e.TreasureHydrospheres[0].Activation == Start.AddSeconds(9.6),
            "Delayed object observation duplicated packet creation or rewrote its clock.");
        e.RemoveActor(9);
        Check(e.TreasureHydrospheres.Count == 1, "Idle destroyed sphere cleared prediction.");
        e.Synchronize([Enemy(1, 0x2537), Enemy(9, 0x255B, Center + Vector3.UnitZ * 9), Player()],
            Player().Position, Start.AddSeconds(3), playerId: 100);
        Check(e.TreasureHydrospheres.Count == 2 && e.TreasureHydrospheres[1].CreatedAt == Start.AddSeconds(3),
            "New sphere generation did not add independent prediction.");
    }
    private static void KelpieKnockback()
    {
        var e = Encounter(0x2537);
        e.Synchronize([Enemy(1, 0x2537), Enemy(9, 0x255B, Center + Vector3.UnitZ * 19), Player()], Player().Position,
            Start.AddSeconds(1), playerId: 100);
        e.StartCast(1, Cast(13440, p: Center - Vector3.UnitX * 2), Start.AddSeconds(1));
        Check(e.TreasureWallMovements(Player()).Single().Unsafe, "Predicted puddle ignored before its activation.");
        var nav = e.AmbientAoes().Single(a => a.Id.StartsWith("treasure/rising-seas/"));
        Check(nav.Position == Center && nav.Activation == Start.AddSeconds(5) && !nav.Display
            && nav.Shape is FanTemplate { Radius: 20 } fan && MathF.Abs(fan.AngleRadians - 75 * MathF.PI / 180) < .00001,
            "Kelpie source-centered or wrong-width forbidden sectors.");
        e.StatusChanged(100, 160, true, Start.AddSeconds(2), expiresAt: Start.AddSeconds(6));
        Check(!e.TreasureWallMovements(Player()).Any() && e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/rising-seas/")),
            "Immunity suppressed custom navigation.");
        e.Finish(1, 13440, Start.AddSeconds(3));
        Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("treasure/rising-seas/"))
            && e.TreasureHydrospheres.Count == 1, "KB finish cleared puddles or retained sectors.");
    }
    private static void Delayed()
    {
        foreach (var (oid, action) in new[] { (0x2543u, 13371u), (0x2537u, 13438u) })
        {
            var e = Encounter(oid);
            e.StartCast(1, Cast(action, 101), Start);
            e.Finish(1, action, Start.AddSeconds(1));
            Check(e.PredictedDamage.Count() == 1, "Cancelled delayable prediction consumed.");
            e.TreasureEffect(2, action, 1, 101, Start.AddSeconds(2));
            Check(!e.PredictedDamage.Any(), "Actual effect from different caster failed target FIFO.");
            Check(NativeTreasureCatalog.Priority(oid, 0x2544) == 2 && NativeTreasureCatalog.Priority(oid, 0x2545) == 1
                && !NativeTreasureCatalog.EncounterOids(oid).Contains(0x255Bu), "Reward priority or water sphere All inclusion differs.");
        }
    }
}
