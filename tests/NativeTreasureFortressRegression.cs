using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureFortressRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Player => new(100, 0, Center + Vector3.UnitX * 15, PartySlot: 0, Player: true, Hostile: false);
    private static DungeonEncounter Create(uint oid)
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([new(1, oid, Center, InCombat: true, Name: "Bitter Eye"), Player,
            Player with { Id = 101, PartySlot = 1, Position = Center + Vector3.UnitZ * 10 },
            .. Enumerable.Range(2, 80).Select(i => new DungeonActor((ulong)i, 0x233C, Center))],
            Player.Position, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, double finish = 5, Vector3? location = null, float rotation = 0,
        ulong target = 100, double start = 0, float? total = null)
        => new(action, location ?? Center, rotation, Start.AddSeconds(start), Start.AddSeconds(finish), target, TotalTime: total);
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static DungeonAmbientAoe[] Aoes(DungeonEncounter e, string key) => e.AmbientAoes().Where(a => a.Id.StartsWith(key)).ToArray();
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Bitter17 Fortress19 source actions geometry and ownership", Rules);
        run("native treasure Bitter global overwrites exact center capped source remaining7.1 and actual clear", Bitter);
        run("native treasure Bitter pull50 min4.9 no wall immunity and projection only", Pull);
        run("native treasure Bitter Fortress dynamic baits effect-only finish retention death cleanup", Baits);
        run("native treasure Fortress Carpet expected4 sort exact8 persistent display4 risky2 and IV reset", Carpet);
        run("native treasure Fortress Quiet source prefix deadline3.5 rotation conjunction and partial recording", Quiet);
        run("native treasure Fortress Systematic only16 sort max9 global Finish and sticky risky danger", Systematic);
        run("native treasure Bitter Fortress rewards full lifecycle source priority and interrupt name", Rewards);
    }
    private static void Rules()
    {
        var b = Create(0x48AA).Active!.Casts.ToDictionary(c => c.Action);
        var m = Create(0x48B2).Active!.Casts.ToDictionary(c => c.Action);
        uint[] bitter = [43675, 43678, 43683, 43680, 43682, 43681, 43677, 43685, 43684, 43676,
            43727, 43726, 32301, 32302, 32303, 32304, 32305];
        uint[] fortress = [43732, 43744, 43746, 43734, 43735, 43736, 43737, 43742, 43748, 43739, 43740, 43741,
            43727, 43726, 32301, 32302, 32303, 32304, 32305];
        Check(b.Keys.Order().SequenceEqual(bitter.Order()) && m.Keys.Order().SequenceEqual(fortress.Order())
            && b[43675].Shape is CircleTemplate { Radius: 11 } && b[43683].Shape is CircleTemplate { Radius: 10 }
            && b[43678].Shape is FanTemplate { Radius: 40, AngleRadians: MathF.PI / 3 }
            && b[43681].Shape is RayRectangleTemplate { Length: 20, MaxLocalX: 3.5f }
            && b[43685].Shape is RayRectangleTemplate { Length: 19, MaxLocalX: 3.5f }
            && b[43677].Shape == null && m[43746].Shape is CircleTemplate { Radius: 6 }
            && NativeTreasureCatalog.Owns(0x48AA, 1279) && NativeTreasureCatalog.Owns(0x48B2, 1279)
            && !NativeTreasureCatalog.Owns(0x48B2, 1209), "Source active IDs, shape or ownership differs.");
    }
    private static void Bitter()
    {
        var early = Create(0x48AA);
        early.StartCast(2, Cast(43683, 4.3, Center, total: 4), Start);
        early.Tick(Start.AddSeconds(1));
        early.Finish(2, 43683, Start.AddSeconds(1), completed: false);
        Check(early.TreasureBitterPrediction!.FinishAt == Start.AddSeconds(1).AddSeconds((4f + .3f - 1f) + 7.1d),
            "Early cancel lost the old positive NPCRemainingTime.");
        var e = Create(0x48AA);
        e.StartCast(2, Cast(43684), Start);
        Check(Aoes(e, "treasure/bitter/").Single().Shape is CircleTemplate { Radius: 13 }
            && e.TreasureBitterPrediction!.FinishAt == Start.AddSeconds(9.6), "Pull visual failed circle13/F+4.6.");
        e.Finish(2, 43684, Start.AddSeconds(10));
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureBitterPrediction != null, "Pull Finish or timeout removed prediction.");
        e.StartCast(3, Cast(43683, 110, new(99.99231f, 0, 99.99231f), start: 100), Start.AddSeconds(100));
        e.Finish(3, 43683, Start.AddSeconds(111));
        Check(!Aoes(e, "treasure/bitter/").Any(a => a.Shape is DonutTemplate), "Exact center leap overwrote old prediction.");
        e.StartCast(4, Cast(43683, 115, Center + Vector3.UnitX, start: 110), Start.AddSeconds(110));
        e.Tick(Start.AddSeconds(150));
        e.Finish(4, 43683, Start.AddSeconds(150));
        var total = (float)(5 - .3d);
        Check(e.TreasureBitterPrediction!.FinishAt == Start.AddSeconds(150).AddSeconds((total + .3f - total) + 7.1d)
            && Aoes(e, "treasure/bitter/").Single().Shape is DonutTemplate { InnerRadius: 5, OuterRadius: 40 },
            "Late leap Finish failed source capped NPCRemainingTime+7.1.");
        e.StartCast(5, Cast(43680, 160, Center, start: 151), Start.AddSeconds(151));
        Check(e.TreasureBitterPrediction!.FinishAt == Start.AddSeconds(160), "Eye Start failed global overwrite.");
        e.StartCast(6, Cast(43676, 155, Center, start: 152), Start.AddSeconds(152));
        Check(e.TreasureBitterPrediction != null, "Hundred Start cleared prediction.");
        e.Finish(6, 43676, Start.AddSeconds(155));
        Check(e.TreasureBitterPrediction == null, "Any Hundred Finish failed global clear.");
    }
    private static void Pull()
    {
        var e = Create(0x48AA);
        e.StartCast(2, Cast(43684), Start);
        var source = e.TreasureWallKnockbackSources.Single();
        var movement = e.TreasureWallMovements(Player).Single();
        Check(source.Distance == 50 && source.MinDistance == 4.9f && source.TowardsOrigin
            && !source.StopAtWall && source.EffectShape == null
            && MathF.Abs(movement.To.X - 104.9f) < .0001f
            && Aoes(e, "treasure/kb/").All(a => !a.Navigate), "Pull parameters, landing or source display-only boundary differs.");
        Check(!e.TreasureWallMovements(Player with { Statuses = [160], StatusExpirations = new Dictionary<uint, DateTime> { [160] = Start.AddSeconds(20) } }).Any(),
            "Pull ignored knockback immunity.");
        e.Finish(2, 43684, Start.AddSeconds(1));
        Check(!e.TreasureWallKnockbackSources.Any() && e.TreasureBitterPrediction != null, "Pull and prediction lifetimes conflated.");
    }
    private static void Baits()
    {
        foreach (var (oid, action, length, width) in new[] { (0x48AAu, 43682u, 65f, 4f), (0x48B2u, 43748u, 46f, 5f) })
        {
            var e = Create(oid);
            e.StartCast(2, Cast(action), Start);
            var bait = Aoes(e, "treasure/cast-bait/").Single(a => a.Display);
            Check(bait.Shape is RayRectangleTemplate r && r.Length == length && r.MaxLocalX == width, "Effect bait dimensions differ.");
            e.Finish(2, action, Start.AddSeconds(1));
            Check(Aoes(e, "treasure/cast-bait/").Any(), "Finish removed effect-only bait.");
            e.TreasureEffect(2, action, 1, 100);
            Check(!Aoes(e, "treasure/cast-bait/").Any(), "Real Effect retained bait.");
        }
    }
    private static void Carpet()
    {
        var e = Create(0x48B2);
        for (var i = 0; i < 3; ++i) e.StartCast((ulong)i + 2, Cast((uint)(43734 + i), 20 - i), Start);
        Check(e.TreasureCarpet[0].Source == 2 && Aoes(e, "treasure/carpet/").Length == 2, "Carpet sorted before4.");
        e.StartCast(5, Cast(43737, 17), Start);
        Check(e.TreasureCarpet[0].Source == 5 && Aoes(e, "treasure/carpet/").Count(a => a.Navigate) == 1, "Carpet expected4 initial mode differs.");
        for (var i = 0; i < 4; ++i) e.StartCast((ulong)i + 6, Cast((uint)(43734 + i), 30 - i), Start);
        e.Tick(Start);
        Check(Aoes(e, "treasure/carpet/").Length == 4 && Aoes(e, "treasure/carpet/").Count(a => a.Navigate) == 2
            && Aoes(e, "treasure/carpet/").Take(2).All(a => a.Color == new Vector4(1, 1, 0, 1)), "Exact8 expanded mode differs.");
        e.Finish(2, 43734, Start.AddSeconds(1));
        e.Tick(Start.AddSeconds(1));
        Check(e.TreasureCarpet.Count == 7 && Aoes(e, "treasure/carpet/").Length == 4, "Seven reverted expanded mode.");
        e.Finish(5, 43737, Start.AddSeconds(2));
        Check(Aoes(e, "treasure/carpet/").Length == 2 && Aoes(e, "treasure/carpet/").Count(a => a.Navigate) == 1,
            "IV Finish failed reset.");
    }
    private static void Quiet()
    {
        var e = Create(0x48B2);
        e.StartCast(2, Cast(43732, 5, rotation: .5f), Start);
        Check(Aoes(e, "treasure/quiet/").Length == 1, "Partial recording crashed or lost sole rectangle.");
        e.StartCast(3, Cast(43732, 6, rotation: .5f), Start);
        e.StartCast(4, Cast(43732, 7, rotation: .5f), Start);
        Check(Aoes(e, "treasure/quiet/").Length == 2, "Repeated equal pair failed prefix stop.");
        e = Create(0x48B2);
        for (var i = 0; i < 5; ++i) e.StartCast((ulong)i + 2, Cast(43732, 5 + i, rotation: i % 2), Start);
        Check(Aoes(e, "treasure/quiet/").Length == 4, "Different first angles incorrectly triggered equality conjunction.");
        e = Create(0x48B2);
        e.StartCast(2, Cast(43732, 5), Start);
        e.StartCast(3, Cast(43732, 8.5), Start);
        Check(Aoes(e, "treasure/quiet/").Length == 1, "Exact3.5 cutoff should exclude boundary.");
    }
    private static void Systematic()
    {
        var e = Create(0x48B2);
        for (var i = 0; i < 15; ++i) e.StartCast((ulong)i + 2, Cast((uint)(43739 + i % 3), 40 - i * 2), Start);
        Check(e.TreasureSystematic[0].Cast.Source == 2 && Aoes(e, "treasure/systematic/").Length == 15,
            "Systematic sorted or limited before16.");
        e.StartCast(17, Cast(43739, 5), Start);
        Check(e.TreasureSystematic[0].Cast.Source == 17 && Aoes(e, "treasure/systematic/").Length == 9,
            "Exact16 sort or max9 differs.");
        e.Finish(2, 43739, Start.AddSeconds(1));
        Check(e.TreasureSystematic.Count == 15 && e.TreasureSystematic[0].Cast.Source == 16
            && Aoes(e, "treasure/systematic/").Length == 15, "Finish matched source instead of global first.");
        e = Create(0x48B2);
        for (var i = 0; i < 8; ++i) e.StartCast((ulong)i + 2, Cast(43739, 5 + i * .1), Start);
        Check(Aoes(e, "treasure/systematic/").All(a => a.Navigate && a.Color != null), "Near first deadline failed persistent promotion.");
        e.Finish(2, 43739, Start.AddSeconds(1));
        Check(Aoes(e, "treasure/systematic/").All(a => a.Color != null), "At7 reverted source sticky Danger.");
        e.StartCast(30, Cast(43741, 100), Start);
        Check(!Aoes(e, "treasure/systematic/").Last().Navigate, "Far future rectangle incorrectly risky.");
        e.TreasureEffect(3, 43739, 1, 100);
        Check(e.TreasureSystematic.Count == 8, "Effect advanced Finish-only Systematic.");
    }
    private static void Rewards()
    {
        var b = Create(0x48AA);
        b.StartCast(1, Cast(43677), Start);
        Check(b.Hints(Player.Position).Any(h => h.Contains("Bitter Eye") && h.Contains("Interrupt"))
            && b.PredictedDamage.Count() == 2 && !b.OutputCasts.Any(c => c.Value.Rule.Shape != null),
            "Named interrupt/raidwide output differs.");
        Check(NativeTreasureCatalog.Priority(0x48AA, 0x48B9) == 7 && NativeTreasureCatalog.Priority(0x48AA, 0x48B7) == 3
            && NativeTreasureCatalog.Priority(0x48AA, 0x48AB) == 1 && !NativeTreasureCatalog.Vulnerable(0x48AA, 0x48AB)
            && NativeTreasureCatalog.Priority(0x48B2, 0x48B9) == 6 && NativeTreasureCatalog.Priority(0x48B2, 0x48B8) == 1,
            "Source priority or marker color differs.");
        b.Synchronize([new(1, 0x48AA, Center, Dead: true), new(2, 0x48AB, Center), Player], Player.Position, Start.AddSeconds(1), playerId: 100);
        Check(b.Active != null, "Eyeclops did not keep module alive.");
        b.Synchronize([new(1, 0x48AA, Center, Dead: true), new(2, 0x48AB, Center, Dead: true), Player], Player.Position, Start.AddSeconds(2), playerId: 100);
        Check(b.Active == null, "AllDeadOrDestroyed failed.");
    }
}
