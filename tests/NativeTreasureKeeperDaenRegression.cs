using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureKeeperDaenRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid = 0x233C, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 10, Player: true, Hostile: false);
    private static DungeonCastObservation Cast(uint action, ulong target = 100, double started = 0,
        double duration = 5, Vector3? position = null, float rotation = 0) =>
        new(action, position ?? Center, rotation, Start.AddSeconds(started), Start.AddSeconds(started + duration), target);
    private static DungeonEncounter Create(uint oid, int helpers = 16)
    {
        var e = new DungeonEncounter(924) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, oid), .. Enumerable.Range(2, helpers).Select(i => Enemy((ulong)i)),
            Player(), Player(101, Center + Vector3.UnitX * 5)], Player().Position, Start, playerId: 100);
        return e;
    }
    private static DungeonAmbientAoe[] Find(DungeonEncounter e, string key) =>
        e.AmbientAoes().Where(a => a.Id.StartsWith(key)).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Keeper Daen full rules and active component boundary", Rules);
        run("native treasure Keeper pull cached slot zero hitboxes view change immune cone max one", Pull);
        run("native treasure Keeper pull skips ineffective first source min distance and no expiry", PullFiltering);
        run("native treasure Keeper exact double poison native target lock one live consumption", Poison);
        run("native treasure Keeper all sources persistent casting R12 cleave dynamic targets", Cleave);
        run("native treasure Ultros wall knockback only splatter unsafe immunity independent direction", Ultros);
        run("native treasure Ultros spread target FIFO no extra prediction", Spread);
        run("native treasure Echidna one dynamic gaze cross source finish consumed correction", GazeSlot);
        run("native treasure Echidna packet target correction preserves later and cleared gaze slot", GazePacketTarget);
        run("native treasure Echidna eight stack exact radius excludes eighth player outside boundary", StackBoundary);
        run("native treasure Echidna gaze original reference death destroy same id freeze", GazeIdentity);
        run("native treasure Echidna missing target overwrites slot boss gaze independent facing", MissingGaze);
        run("native treasure Echidna VoidStream expected99 first four all clocks promotion", VoidStream);
        run("native treasure Keeper Daen full rewards colors and All exclusion", Rewards);
    }
    private static void Rules()
    {
        foreach (var (oid, count) in new[] { (0x3023u, 7), (0x3030u, 17), (0x3032u, 18) })
        {
            var e = Create(oid);
            Check(e.Active!.Casts.Length == count && NativeTreasureCatalog.Owns(oid, 924)
                && !NativeTreasureCatalog.Owns(oid, 1279) && e.Arena!.Contains(Center) && !e.Arena.Contains(Center + Vector3.UnitX * 19.1f),
                "Complete catalog/ownership/48-gon differs.");
        }
        var u = Create(0x3030).Active!.Casts.ToDictionary(c => c.Action);
        var echi = Create(0x3032).Active!.Casts.ToDictionary(c => c.Action);
        Check(u[21751].Shape is FanTemplate { Radius: 13 } f && f.AngleRadians == MathF.PI / 2
            && u[21755].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -5, MaxLocalX: 5 }
            && u[6448].Shape is CircleTemplate { Radius: 6.84f } && u[21749].Shape == null
            && !NativeTreasureCatalog.RaidwideAction(21749) && !u.ContainsKey(21741)
            && echi[21760].Shape == null && echi[21762].Shape == null && !echi.ContainsKey(21759),
            "Source parameters or inactive boundary differs.");
    }
    private static void Pull()
    {
        var e = Create(0x3023);
        var cast = Cast(21677);
        e.Synchronize([Enemy(1, 0x3023), Enemy(2) with { HitboxRadius = 3.99f },
            Player() with { HitboxRadius = .5f }, Player(101) with { HitboxRadius = 2 }], Center, Start, playerId: 101);
        e.StartCast(2, cast, Start);
        var source = e.TreasureWallKnockbackSources.Single();
        Check(source.TowardsOrigin && !source.StopAtWall && source.MinDistance == 4.49f
            && MathF.Abs(e.TreasureWallMovements(Player()).Single().To.Z - 104.49f) < .001f,
            "Pull used view hitbox or outward/wall semantics.");
        e.Synchronize([Enemy(1, 0x3023), Enemy(2) with { HitboxRadius = 9, Cast = cast },
            Player() with { HitboxRadius = 8 }, Player(101)], Center, Start.AddSeconds(1), playerId: 100);
        e.StartCast(2, cast with { Rotation = .1f }, Start.AddSeconds(1.1));
        Check(e.TreasureWallKnockbackSources.Single().MinDistance == 4.49f, "Correction/view change recomputed min distance.");
        var immune = Player() with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(6) } };
        Check(!e.TreasureWallMovements(immune).Any() && e.OutputCasts.Any(c => c.Value.Rule.Action == 21677),
            "Immunity removed Inhale ordinary cone.");
        e.Finish(2, 21677, Start.AddSeconds(2));
        Check(!e.TreasureWallKnockbackSources.Any(), "Pull Finish failed.");
    }
    private static void PullFiltering()
    {
        var e = Create(0x3023);
        e.StartCast(2, Cast(21677, rotation: MathF.PI), Start);
        e.StartCast(3, Cast(21677), Start);
        e.StartCast(4, Cast(21677), Start);
        e.Tick(Start.AddSeconds(50));
        Check(e.TreasureWallMovements(Player()).Single().Source.Cast.Source == 3, "max1 applied before filtering or pull expired.");
        Check(!e.TreasureWallMovements(Player(position: Center)).Any(), "Zero-distance pull displaced.");
    }
    private static void Poison()
    {
        var e = Create(0x3023);
        e.StartCast(2, Cast(21679), Start);
        Check(Find(e, "trial/voidzone/cast/").Single().Position == Player().Position, "Poison cast not target-centered.");
        e.Finish(2, 21679, Start.AddSeconds(.1));
        Check(!Find(e, "trial/voidzone/").Any(), "Cancel created unobserved event poison.");
        e.Tick(Start.AddSeconds(2));
        e.TreasureEffect(2, 21679, 1, 100, Start.AddSeconds(2));
        e.TreasureEffect(3, 21679, 1, 100, Start.AddSeconds(2));
        Check(Find(e, "trial/voidzone/event/").All(a => a.Activation == Start.AddSeconds(2).AddSeconds(1.4d)
            && a.Shape is CircleTemplate { Radius: 6 }), "Double-precision 1.4 delay differs.");
        e.Synchronize([Enemy(1, 0x3023), Enemy(20, 0x1E8FC7, Player().Position) with { Dead = true, Targetable = false }, Player()],
            Center, Start.AddSeconds(3), playerId: 100);
        Check(Find(e, "trial/voidzone/event/").Length == 1 && Find(e, "trial/voidzone/live/").Length == 1,
            "One live source consumed all predictions or dead source filtered.");
        e.Synchronize([Enemy(1, 0x3023), Enemy(20, 0x1E8FC7, Player().Position) with { EventState = 7 }, Player()],
            Center, Start.AddSeconds(4), playerId: 100);
        Check(Find(e, "trial/voidzone/event/").Length == 1 && Find(e, "trial/voidzone/live/").Length == 0,
            "State7 consumed/displayed poison.");
    }
    private static void Cleave()
    {
        var e = Create(0x3023);
        e.Synchronize([Enemy(1, 0x3023) with { TargetId = 100, Cast = Cast(21681) },
            Enemy(20, 0x3023) with { TargetId = 100 }, Player(), Player(101)], Center, Start.AddSeconds(1), playerId: 100);
        Check(Find(e, "treasure/cleave/").Count(a => a.Display && a.OutlineOnly && a.Shape is FanTemplate { Radius: 12 }) == 2
            && Find(e, "treasure/cleave/").Count(a => a.Navigate) == 2,
            "Casting/secondary Keeper cleave or party directions lost.");
    }
    private static void Ultros()
    {
        var e = Create(0x3030);
        e.StartCast(2, Cast(21749), Start);
        e.StartCast(3, Cast(21749, duration: 4), Start);
        var landing = e.TreasureWallMovements(Player()).First().To;
        e.StartCast(4, Cast(21745, position: landing), Start);
        Check(!e.TreasureWallMovements(Player()).First().Unsafe && !e.PredictedDamage.Any(), "Unrelated AOE became KB unsafe/Raidwide.");
        e.StartCast(5, Cast(21750, position: landing), Start);
        Check(e.TreasureWallMovements(Player()).First().Unsafe
            && Find(e, "treasure/turmoil/").Single().Activation == Start.AddSeconds(5), "Splatter unsafe/first KB clock differs.");
        e.Synchronize([Enemy(1, 0x3030), Enemy(2) with { Cast = Cast(21749) }, Enemy(3) with { Cast = Cast(21749, duration: 4) },
            Enemy(5) with { Cast = Cast(21750, position: landing) }, Player() with { Statuses = [160],
                StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(8) } }], Center, Start.AddSeconds(1), playerId: 100);
        Check(Find(e, "treasure/kb/").Length == 0 && Find(e, "treasure/turmoil/").Length == 1, "Immunity removed independent direction.");
        e.Finish(2, 21749, Start.AddSeconds(2));
        Check(Find(e, "treasure/turmoil/").Single().Activation == Start.AddSeconds(4), "Direction deadline did not promote first KB.");
    }
    private static void Spread()
    {
        var e = Create(0x3030);
        e.StartCast(2, Cast(21747, 101), Start);
        e.StartCast(3, Cast(21747, 101, started: 1), Start.AddSeconds(1));
        Check(Find(e, "quest/spread/").Count(a => a.Display && a.Shape is CircleTemplate { Radius: 8 }) == 2
            && Find(e, "quest/spread/").Count(a => a.Navigate && a.Shape is CircleTemplate { Radius: 9 }) == 2 && !e.PredictedDamage.Any(),
            "Ultros spread radii/prediction differs.");
        e.Finish(3, 21747, Start.AddSeconds(2));
        Check(Find(e, "quest/spread/").Single(a => a.Navigate).Activation == Start.AddSeconds(6), "Spread FIFO used source.");
    }
    private static void GazeSlot()
    {
        var e = Create(0x3032);
        var first = Cast(21762, 100);
        var second = Cast(21760, 101, started: 1);
        e.StartCast(2, first, Start);
        e.StartCast(3, second, Start.AddSeconds(1));
        e.StartCast(2, first with { Location = Center + Vector3.UnitX }, Start.AddSeconds(1.1));
        Check(e.Gazes.Single().TargetId == 101 && e.Gazes.Single().Location == Player(101, Center + Vector3.UnitX * 5).Position,
            "Earlier correction replaced dynamic single slot.");
        e.Finish(2, 21762, Start.AddSeconds(2));
        Check(!e.Gazes.Any() && Find(e, "treasure/stack/").Any(), "Cross-source Finish did not clear gaze or removed stack.");
        e.StartCast(3, second with { Location = Center + Vector3.UnitZ }, Start.AddSeconds(2.1));
        Check(!e.Gazes.Any(), "Consumed correction revived globally cleared eye.");
    }
    private static void GazeIdentity()
    {
        var e = Create(0x3032);
        var cast = Cast(21762);
        e.StartCast(2, cast, Start);
        var pos = Center + Vector3.UnitX * 7;
        e.Synchronize([Enemy(1, 0x3032), Enemy(2) with { Cast = cast }, Player(position: pos) with { Dead = true }],
            Center, Start.AddSeconds(1), playerId: 101);
        Check(e.Gazes.Single().Location == pos, "Dead dynamic target lost gaze.");
        e.RemoveActor(100);
        e.Synchronize([Enemy(1, 0x3032), Enemy(2) with { Cast = cast }, Player(position: Center - Vector3.UnitZ * 9), Player(101)],
            Center, Start.AddSeconds(2), playerId: 101);
        Check(e.Gazes.Single().Location == pos && e.TreasurePlayerGaze!.Removed, "Recreated target rebound old eye.");
        e.RemoveActor(2);
        Check(!e.Gazes.Any(), "Casting source Destroy failed global gaze clear.");
    }
    private static void StackBoundary()
    {
        var e = Create(0x3032);
        e.TreasurePartyInput = Enumerable.Range(100, 8).Select(i => (ulong)i).ToArray();
        const float grid = 2000f / 65535;
        var q = new Vector3(((int)MathF.Round(Center.X / grid) - .5f) * grid, 0,
            ((int)MathF.Round(Center.Z / grid) - .5f) * grid);
        e.Synchronize([Enemy(1, 0x3032), Enemy(2), .. Enumerable.Range(100, 7).Select(i => Player((ulong)i, Center)),
            Player(107, q + Vector3.UnitX * 6.000005f)], Center, Start, playerId: 100);
        e.StartCast(2, Cast(21760, 100), Start);
        Check(e.Hints(Center).Contains("Stack! (7/8)"), "Out-of-radius eighth member counted via geometry epsilon.");
    }
    private static void GazePacketTarget()
    {
        foreach (var cleared in new[] { false, true })
        {
            var e = Create(0x3032);
            var first = Cast(21760, 100);
            e.StartCast(2, first, Start, fromPacket: true);
            e.StartCast(3, Cast(21762, 101, started: 1), Start.AddSeconds(1), fromPacket: true);
            if (cleared) e.Finish(3, 21762, Start.AddSeconds(2));
            e.StartCast(2, first with { TargetId = 101 }, Start.AddSeconds(2.1), fromPacket: true);
            Check(cleared ? !e.Gazes.Any() : e.Gazes.Single().Source == 3,
                "Packet target correction fabricated Finish/Start and overwrote globally owned eye.");
            Check(Find(e, "treasure/stack/").Single(a => a.Display).Position == e.Actors[101].Position
                || Vector3.Distance(Find(e, "treasure/stack/").Single(a => a.Display).Position, e.Actors[101].Position) < .05f,
                "Packet correction left stack target inconsistent with Finish target.");
            e.Finish(2, 21760, Start.AddSeconds(3));
            Check(!Find(e, "treasure/stack/").Any(), "Corrected stack target survived real Finish.");
        }
        var missing = Create(0x3032);
        var cast = Cast(21760, 100);
        missing.StartCast(2, cast, Start);
        missing.StartCast(2, cast with { TargetId = 999 }, Start.AddSeconds(.1));
        Check(!Find(missing, "treasure/stack/").Any(), "Missing corrected target retained stack.");
        missing.StartCast(2, cast with { TargetId = 101 }, Start.AddSeconds(.2));
        Check(Find(missing, "treasure/stack/").Any() && missing.Gazes.Single().TargetId == 101,
            "Different valid correction failed to recover unresolved stack.");
        missing.Finish(2, 21760, Start.AddSeconds(.3));
        Check(!Find(missing, "treasure/stack/").Any(), "Recovered stack survived Finish.");
    }
    private static void MissingGaze()
    {
        var e = Create(0x3032);
        e.StartCast(2, Cast(21762), Start);
        e.StartCast(3, Cast(21762, 999, started: 1), Start.AddSeconds(1));
        Check(!e.Gazes.Any(), "Missing target did not overwrite eye.");
        e.StartCast(4, Cast(21761, started: 2, position: Center + Vector3.UnitX * 100), Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(7));
        Check(e.Gazes.Single().Location.X == 200 && e.GazeFacing(Center, MathF.PI / 2) is { } heading
            && MathF.Abs(MathF.Atan2(MathF.Sin(heading - MathF.PI / 2), MathF.Cos(heading - MathF.PI / 2))) > MathF.PI / 4
            && !e.PredictedDamage.Any(), "Boss 10000 gaze/facing independent of missing dynamic slot differs.");
    }
    private static void VoidStream()
    {
        var e = Create(0x3032, 100);
        for (var i = 0; i < 12; ++i) e.StartCast((ulong)i + 2, Cast((uint)(22797 + i % 4), duration: 110 - i), Start);
        Check(e.TreasureVaultGroups.First().Source == 2 && Find(e, "treasure/vault-group/").Length == 4,
            "VoidStream sorted at twelve or lost max4.");
        e.Finish(2, 22797, Start.AddSeconds(1));
        Check(Find(e, "treasure/vault-group/").Last().Id.EndsWith("/6"), "Fifth VoidStream failed promotion.");
        for (var i = 12; i < 100; ++i) e.StartCast((ulong)i + 2, Cast((uint)(22797 + i % 4), duration: 110 - i), Start);
        Check(e.TreasureVaultGroups.Count == 99 && e.TreasureVaultGroups[0].Source == 101, "Expected99 sort boundary differs.");
    }
    private static void Rewards()
    {
        foreach (var oid in new uint[] { 0x3023, 0x3030, 0x3032 })
        {
            var e = Create(oid);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(20, 0x3033), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && NativeTreasureCatalog.Priority(oid, 0x3033) == 2
                && NativeTreasureCatalog.Priority(oid, 0x3034) == 1
                && Find(e, "treasure/enemy/").Single().Color == new Vector4(1, 0, 1, 1), "Reward phase/priority/color lost.");
            e.Synchronize([Enemy(30, 0x3031), Enemy(31, 0x1E8FC7), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Tentacle/poison extended All.");
        }
    }
}
