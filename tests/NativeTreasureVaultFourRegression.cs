using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureVaultFourRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid = 0x233C, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 5, Player: true, Hostile: false);
    private static DungeonCastObservation Cast(uint action, ulong target = 100, double started = 0,
        double duration = 5, Vector3? position = null, float rotation = 0) =>
        new(action, position ?? Center, rotation, Start.AddSeconds(started), Start.AddSeconds(started + duration), target);
    private static DungeonEncounter Create(uint oid, int helpers = 16)
    {
        var e = new DungeonEncounter(1279) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, oid), .. Enumerable.Range(2, helpers).Select(i => Enemy((ulong)i)),
            Player(), Player(101, Center + Vector3.UnitZ * 8)], Player().Position, Start, playerId: 100);
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
        run("native treasure Vault four complete rules source parameters inactive boundaries", Rules);
        run("native treasure Vault Violet insertion first four promotion clocks correction", Violet);
        run("native treasure Vault Prism real triangle expected eight source equal clock sort", Prism);
        run("native treasure Vault Uplift twelve and sixteen all display independent risky color window", Uplift);
        run("native treasure Vault Tremblor three real finishes axis matching consumption clock", Tremblor);
        run("native treasure Vault Gwyddneu created balls lock clock destroy retain clear all", Balls);
        run("native treasure Vault Gwyddneu same id different orb generation no stale resurrection", BallGenerations);
        run("native treasure Vault CatCollar world offset rotation every fourth finish promotion", Beams);
        run("native treasure Vault CatCollar counts empty queue finishes and bootstrap once", BeamEmpty);
        run("native treasure Vault four circle bait dynamic target finish only prediction", CircleBaits);
        run("native treasure Vault Roadripper charge dynamic distance dead target revive source finish once", Charge);
        run("native treasure Vault Roadripper corpses first cached boss and rewards lifecycle", Lifecycle);
        run("native treasure Vault four reward priorities colors All and helper exclusions", Rewards);
        run("native treasure Vault Roadripper Wheel captured target native main target FIFO", Wheel);
        run("native treasure Vault GreatGimmeCat spread radius target FIFO death revival", Spread);
    }
    private static void Rules()
    {
        foreach (var (oid, count) in new[] { (0x48A4u, 13), (0x48ACu, 7), (0x48A1u, 14), (0x48A8u, 17) })
        {
            var e = Create(oid);
            Check(e.Active!.Casts.Length == count && NativeTreasureCatalog.Owns(oid, 1279)
                && !NativeTreasureCatalog.Owns(oid, 1123) && e.Arena!.Contains(Center + new Vector3(19.49f, 0, 19.49f))
                && !e.Arena.Contains(Center + Vector3.UnitX * 19.6f), "Complete catalog/ownership/square differs.");
        }
        var g = Create(0x48A4).Active!.Casts.ToDictionary(c => c.Action);
        var c = Create(0x48AC).Active!.Casts.ToDictionary(c => c.Action);
        var r = Create(0x48A1).Active!.Casts.ToDictionary(c => c.Action);
        Check(g[43645].Shape is FanTemplate { Radius: 70 } fan && MathF.Abs(fan.AngleRadians - MathF.PI / 4) < .001f
            && !g.ContainsKey(43655) && g[43656].Shape == null && g[43652].Shape is CircleTemplate { Radius: 6 }
            && c[43692].Shape is CircleTemplate { Radius: 5 } && !c.ContainsKey(32301)
            && r[43637].Shape is DonutTemplate { InnerRadius: 3, OuterRadius: 12 }
            && r[43641].Shape is FanTemplate { Radius: 40 } f && f.AngleRadians == MathF.PI,
            "Rule parameters or inactive scope differs.");
    }
    private static void Violet()
    {
        var e = Create(0x48A4);
        for (ulong id = 2; id <= 6; ++id) e.StartCast(id, Cast(43645, duration: 12 - id), Start);
        Check(e.TreasureVaultGroups.Select(c => c.Source).SequenceEqual(new ulong[] { 2, 3, 4, 5, 6 })
            && Find(e, "treasure/vault-group/").Length == 4 && !e.OutputCasts.Any(), "Violet sorted or duplicated.");
        e.StartCast(2, Cast(43645, duration: 10, position: Center + Vector3.UnitX), Start.AddSeconds(.1));
        Check(e.TreasureVaultGroups.Count == 5 && e.TreasureVaultGroups[0].Location.X == 101, "Correction appended/lost lock.");
        e.Finish(2, 43645, Start.AddSeconds(1));
        Check(Find(e, "treasure/vault-group/").Last().Id.EndsWith("/6")
            && Find(e, "treasure/vault-group/").Last().Activation == Start.AddSeconds(6), "Hidden Violet failed promotion.");
    }
    private static void Prism()
    {
        var e = Create(0x48AC);
        for (ulong id = 2; id <= 8; ++id) e.StartCast(id, Cast(43687), Start);
        Check(e.TreasureVaultGroups[0].Source == 2, "Prism sorted before expected eight.");
        e.StartCast(9, Cast(43688), Start);
        Check(e.TreasureVaultGroups.Select(c => c.Source).SequenceEqual(new ulong[] { 9, 2, 3, 4, 5, 6, 7, 8 }),
            "Equal-clock source quicksort changed.");
        var triangle = Find(e, "treasure/vault-group/")[0].Shape.Resolve(Center, 0, null);
        Check(triangle.Contains(Center + Vector3.UnitZ * 10) && !triangle.Contains(Center + Vector3.UnitZ * 18)
            && !triangle.Contains(Center - Vector3.UnitZ), "Prism became circular fan or wrong orientation.");
        e.Finish(9, 43688, Start.AddSeconds(1));
        Check(Find(e, "treasure/vault-group/").Select(a => a.Id.Split('/').Last()).SequenceEqual(new[] { "2", "3", "4", "5" }),
            "Prism hidden queue promotion differs.");
    }
    private static void Uplift()
    {
        foreach (var total in new[] { 12, 16 })
        {
            var e = Create(0x48A8);
            for (var i = 0; i < total; ++i)
                e.StartCast((ulong)i + 2, Cast((uint)(43659 + i % 4), duration: 5 + i % 4 * 2), Start);
            var all = Find(e, "treasure/vault-group/");
            Check(all.Length == total && all.All(a => !a.Navigate)
                && all.Count(a => a.Color == new Vector4(1, 1, 0, 1)) == 4
                && !e.Hints(Center).Contains("GTFO from aoe!"), "Uplift early risk/display/color differs.");
            e.Tick(Start.AddSeconds(2));
            Check(Find(e, "treasure/vault-group/").Count(a => a.Navigate) == total / 4, "Uplift F-3 boundary differs.");
            e.Tick(Start.AddSeconds(100));
            Check(Find(e, "treasure/vault-group/").All(a => a.Navigate), "Uplift timed out.");
            foreach (var cast in e.TreasureVaultGroups.ToArray()[..(total - 4)])
                e.Finish(cast.Source, cast.Rule.Action, Start.AddSeconds(101));
            Check(Find(e, "treasure/vault-group/").Length == 4 && Find(e, "treasure/vault-group/").All(a => a.Color == null),
                "Final four retained danger color.");
        }
    }
    private static void Tremblor()
    {
        var e = Create(0x48A8);
        e.StartCast(2, Cast(43665), Start);
        Check(e.TreasureIceSequences.Count == 0, "Later Tremblor created first stage.");
        var first = Cast(43664, position: Center + Vector3.UnitX * 3);
        e.StartCast(3, first, Start);
        e.StartCast(4, Cast(43664, position: Center + new Vector3(3.9f, 0, .9f), rotation: 2), Start);
        e.Tick(Start.AddSeconds(10));
        e.Finish(4, 43664, Start.AddSeconds(1));
        Check(e.TreasureIceSequences[0].Order == 1 && e.TreasureIceSequences[0].Activation == Start.AddSeconds(13),
            "Tremblor used event clock/rotation/radial match.");
        e.StartCast(3, first with { Location = Center }, Start.AddSeconds(10.1));
        Check(e.TreasureIceSequences[0].Order == 1, "Live consumed correction restarted Tremblor.");
        e.StartCast(5, Cast(43665, started: 11, position: first.Location), Start.AddSeconds(11));
        e.Tick(Start.AddSeconds(12));
        e.Finish(5, 43665, Start.AddSeconds(12));
        Check(Find(e, "treasure/ice/")[0].Shape is DonutTemplate { InnerRadius: 20, OuterRadius: 30 }
            && e.TreasureIceSequences[0].Activation == Start.AddSeconds(15), "Third Tremblor shape/clock differs.");
        e.StartCast(6, Cast(43666, started: 13, position: first.Location), Start.AddSeconds(13));
        e.Finish(6, 43666, Start.AddSeconds(14));
        Check(e.TreasureIceSequences.Count == 1, "Tremblor final removed unrelated sequence.");
    }
    private static void Balls()
    {
        var e = Create(0x48A4);
        e.Tick(Start.AddSeconds(1));
        e.ObjectCreated(20, Start, 0x48A7, Center + Vector3.UnitX);
        e.ObjectCreated(21, Start, 0x48A6, Center - Vector3.UnitX);
        e.RemoveActor(20);
        e.Tick(Start.AddSeconds(50));
        Check(e.TreasureCreatedCircles.Count == 2 && e.TreasureCreatedCircles[0].Activation == Start.AddSeconds(8.9)
            && e.TreasureCreatedCircles[0].Radius == 16 && e.TreasureCreatedCircles[1].Radius == 8,
            "Created balls clock/radius/destroy retention differs.");
        e.StartCast(2, Cast(43655), Start.AddSeconds(50));
        e.Finish(2, 43655, Start.AddSeconds(51));
        Check(e.TreasureCreatedCircles.Count == 2, "Inactive Shock cleared balls.");
        e.StartCast(3, Cast(43656, started: 51), Start.AddSeconds(51));
        e.Finish(3, 43656, Start.AddSeconds(52));
        Check(e.TreasureCreatedCircles.Count == 0, "Any Electrify Finish failed clear all.");
    }
    private static void BallGenerations()
    {
        var e = Create(0x48A4);
        e.Synchronize([Enemy(1, 0x48A4), Enemy(20, 0x48A6), Player()], Center, Start.AddSeconds(1), playerId: 100);
        e.Synchronize([Enemy(1, 0x48A4), Enemy(20, 0x48A7), Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.TreasureCreatedCircles.Count == 2 && e.TreasureCreatedCircles[1].Radius == 16, "New orb OID did not create generation.");
        e.RemoveActor(20);
        e.Synchronize([Enemy(1, 0x48A4), Enemy(20, 0x48A7), Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(e.TreasureCreatedCircles.Count == 2, "Stale snapshot resurrected orb generation.");
    }
    private static void Beams()
    {
        var e = Create(0x48AC);
        for (ulong id = 20; id < 28; ++id) e.ObjectCreated(id, Start, 0x48AD, Center + Vector3.UnitX * (id - 20), .7f);
        Check(e.TreasureBeams.Count == 8 && Find(e, "treasure/beam/").Length == 4
            && MathF.Abs(e.TreasureBeams[0].Origin.Z - 79) < .05f && e.TreasureBeams[0].Rotation == .7f
            && e.TreasureBeams[0].Activation == Start.AddSeconds(7.7), "Beam offset/rotation/clock/limit differs.");
        e.RemoveActor(20);
        for (ulong id = 2; id < 6; ++id)
        {
            e.StartCast(id, Cast(43690), Start);
            e.Finish(id, 43690, Start.AddSeconds(1));
        }
        Check(e.TreasureBeams.Count == 4 && e.TreasureBeams[0].Source == 24, "Fourth Finish failed promote original tail.");
        e.Tick(Start.AddSeconds(100));
        Check(Find(e, "treasure/beam/").Length == 4, "Beams expired.");
    }
    private static void BeamEmpty()
    {
        var e = Create(0x48AC);
        for (ulong id = 2; id < 5; ++id) { e.StartCast(id, Cast(43690), Start); e.Finish(id, 43690, Start.AddSeconds(1)); }
        var actors = new[] { Enemy(1, 0x48AC), Enemy(5) }.Concat(Enumerable.Range(20, 4).Select(i => Enemy((ulong)i, 0x48AD)))
            .Append(Player()).ToArray();
        e.Synchronize(actors, Center, Start.AddSeconds(2), playerId: 100);
        e.Synchronize(actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(e.TreasureBeams.Count == 4, "Bootstrap duplicated beams.");
        e.StartCast(5, Cast(43690, started: 3), Start.AddSeconds(3));
        e.Finish(5, 43690, Start.AddSeconds(4));
        Check(e.TreasureBeams.Count == 0, "Empty queue finishes failed contribute to fourth.");
    }
    private static void CircleBaits()
    {
        foreach (var (oid, action) in new[] { (0x48A4u, 43650u), (0x48ACu, 43698u), (0x48A8u, 43668u) })
        {
            var e = Create(oid);
            var cast = Cast(action);
            e.StartCast(2, cast, Start);
            e.Synchronize([Enemy(1, oid), Enemy(2) with { Cast = cast }, Player(position: Center + Vector3.UnitX * 7),
                Player(101)], Center, Start.AddSeconds(1), playerId: 100);
            Check(Find(e, "treasure/cast-bait/").Single(a => a.Display).Position == Center + Vector3.UnitX * 7
                && Find(e, "treasure/cast-bait/").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 5 }
                && e.PredictedDamage.Single().DamageType == "Tankbuster" && e.Hints(Center).Contains("Tankbuster cleave"),
                "Circle bait dynamic origin, teammate avoidance or prediction differs.");
            e.TreasureEffect(2, action, 1, 100, Start.AddSeconds(2));
            Check(Find(e, "treasure/cast-bait/").Length == 2, "Effect consumed Finish-only bait.");
            e.Finish(2, action, Start.AddSeconds(3));
            Check(Find(e, "treasure/cast-bait/").Length == 0 && !e.PredictedDamage.Any(), "Finish-only bait retained.");
        }
    }
    private static void Charge()
    {
        var e = Create(0x48A1);
        var cast = Cast(43640);
        e.StartCast(2, cast, Start);
        var actors = new[] { Enemy(1, 0x48A1), Enemy(2) with { Cast = cast },
            Player(position: Center + new Vector3(6, 20, 8)) with { Dead = true }, Player(101) };
        e.Synchronize(actors, Center, Start.AddSeconds(1), playerId: 100);
        Check(Find(e, "treasure/cast-bait/").Single(a => a.Display).Shape is RayRectangleTemplate { Length: 10, MinLocalX: -4, MaxLocalX: 4 }
            && !e.Hints(Center).Contains("Tankbuster cleave"), "Charge used height/extra hitbox or dropped dead target.");
        e.Synchronize(actors.Select(a => a.Id == 2 ? a with { Dead = true } : a).ToArray(), Center, Start.AddSeconds(2), playerId: 100);
        Check(Find(e, "treasure/cast-bait/").Length == 0, "Dead source not hidden.");
        e.Synchronize(actors, Center, Start.AddSeconds(3), playerId: 100);
        Check(Find(e, "treasure/cast-bait/").Length == 2, "Source revival lost retained charge.");
        e.RemoveActor(2, Start.AddSeconds(4));
        Check(Find(e, "treasure/cast-bait/").Length == 0, "Casting source destroy did not finish charge exactly once.");
    }
    private static void Lifecycle()
    {
        var e = Create(0x48A1);
        e.Synchronize([Enemy(1, 0x48A1) with { Dead = true }, Enemy(20, 0x48A3), Enemy(21, 0x48A3), Player()],
            Center, Start.AddSeconds(1), playerId: 100);
        Check(e.Active != null, "Roadripper corpse ended AllDestroyed.");
        e.Synchronize([Enemy(20, 0x48A3), Enemy(21, 0x48A3), Player()], Center, Start.AddSeconds(2), playerId: 100);
        Check(e.Active != null, "Roadripper cache failed retention.");
        e.Synchronize([Enemy(20, 0x48A3) with { Dead = true }, Enemy(21, 0x48A3), Player()], Center, Start.AddSeconds(3), playerId: 100);
        Check(e.Active == null, "Duplicate Roadripper replaced first cache.");
    }
    private static void Rewards()
    {
        foreach (var oid in new uint[] { 0x48A4, 0x48AC, 0x48A1, 0x48A8 })
        {
            var e = Create(oid);
            var fast = oid == 0x48A8;
            Check(NativeTreasureCatalog.Priority(oid, 0x48B7) == (oid == 0x48AC ? 1 : fast ? 3 : 2)
                && (oid == 0x48AC || Enumerable.Range(0, 5).All(i => NativeTreasureCatalog.Priority(oid, (uint)(0x48B9 + i)) == (fast ? 7 : 6) - i)),
                "Reward priorities differ.");
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(20, 0x48B7), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && Find(e, "treasure/enemy/").Single(a => a.Id.EndsWith("/20")).Color == new Vector4(1, 0, 1, 1),
                "Goldy All/color lost.");
            e.Synchronize([Enemy(30, 0x48A6), Enemy(31, 0x48AD), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Unowned orb/collar kept phase alive.");
        }
    }
    private static void Wheel()
    {
        var e = Create(0x48A1);
        e.StartCast(2, Cast(43638), Start);
        e.Finish(2, 43638, Start.AddSeconds(1));
        Check(e.PredictedDamage.Single().TargetId == 100, "Wheel Finish removed captured target.");
        e.TreasureEffect(2, 43638, 1, 101, Start.AddSeconds(2));
        Check(e.PredictedDamage.Any(), "Wrong MainTarget consumed Wheel.");
        e.TreasureEffect(2, 43638, 2, 100, Start.AddSeconds(3));
        Check(!e.PredictedDamage.Any(), "Actual MainTarget failed Wheel consume.");
    }
    private static void Spread()
    {
        var e = Create(0x48AC);
        var one = Cast(43696, 101);
        var two = Cast(43696, 101, started: 1);
        e.StartCast(2, one, Start);
        e.StartCast(3, two, Start.AddSeconds(1));
        Check(Find(e, "quest/spread/").Count(a => a.Display && a.Shape is CircleTemplate { Radius: 5 }) == 2
            && Find(e, "quest/spread/").Count(a => a.Navigate && a.Shape is CircleTemplate { Radius: 6 }) == 2
            && !e.PredictedDamage.Any(), "Cat spread radii or prediction differs.");
        e.Synchronize([Enemy(1, 0x48AC), Enemy(2) with { Cast = one }, Enemy(3) with { Cast = two },
            Player(), Player(101) with { Dead = true }], Center, Start.AddSeconds(2), playerId: 100);
        Check(Find(e, "quest/spread/").Length == 0, "Dead Cat spread displayed.");
        e.Synchronize([Enemy(1, 0x48AC), Enemy(2) with { Cast = one }, Enemy(3) with { Cast = two },
            Player(), Player(101)], Center, Start.AddSeconds(3), playerId: 100);
        e.Finish(3, 43696, Start.AddSeconds(3));
        Check(Find(e, "quest/spread/").Single(a => a.Navigate).Activation == Start.AddSeconds(6),
            "Cat spread revival or target FIFO differs.");
    }
}
