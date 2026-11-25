using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeFateChiMicaRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static DungeonActor Boss(uint territory) => new(1, territory == 960 ? 0x34CBu : 0x43EBu,
        Center(territory), InCombat: true, Hp: 1000);
    private static Vector3 Center(uint territory) => territory == 960 ? new(650, 0, 0) : new(791, 0, 593);
    private static DungeonActor Player(uint territory, ulong id = 100, int slot = 0) =>
        new(id, 0, Center(territory), Hostile: false, PartySlot: slot, Player: true);
    private static DungeonEncounter Encounter(uint territory, params DungeonActor[] extra)
    {
        var e = new DungeonEncounter(territory);
        e.Synchronize([Boss(territory), Player(territory), .. extra], Center(territory), Start, playerId: 100);
        return e;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    private static void Cast(DungeonEncounter e, ulong source, uint action, double at, double finish,
        Vector3? position = null, float rotation = 0, ulong target = 0) =>
        e.StartCast(source, new(action, position ?? Center(e.Definitions[0].BossOid == 0x34CB ? 960u : 1192u),
            rotation, Start.AddSeconds(at), Start.AddSeconds(finish), TargetId: target), Start.AddSeconds(at));
    private static DungeonAmbientAoe[] Chi(DungeonEncounter e, string family) =>
        e.AmbientAoes().Where(a => a.Id.StartsWith($"chi/{family}/")).ToArray();
    private static DungeonAmbientAoe[] Mica(DungeonEncounter e) =>
        e.AmbientAoes().Where(a => a.Id.StartsWith("mica/aoe/")).ToArray();
    private static Vector3 Quantized(Vector3 p)
    {
        const float grid = 2000f / 65535f;
        return new(((int)MathF.Round(p.X * (1 / grid)) - .5f) * grid, p.Y,
            ((int)MathF.Round(p.Z * (1 / grid)) - .5f) * grid);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("native fate Chi Mica exact territory fixed arenas strict source slot pull persistent combat and owner reset", Lifecycle);
        run("native fate Chi all six combo starts eight effects two clocks risk promotion source origin and axis band", Combos);
        run("native fate Chi created Bunker and Bomb global FIFO inclusive windows strict risky frozen generation and no effect consumption", Created);
        run("native fate Chi exact nine ordinary rules thermobaric25 tankbuster bait and raidwide no invented visual", Ordinary);
        run("native fate Chi Mica dynamic original target baits self teammate navigation death destroy new generation and real finish", Baits);
        run("native fate Mica cards safe index cached FIFO first persistent actor quantized origin default clock and fake finish", Cards);
        run("native fate Mica partial card five slots no synthetic missing actors no backfill and current actor on cache promotion", MissingCards);
        run("native fate Mica Flourish swaps only first pair FIFO clocks cancellation promotion and no effect clear", Flourish);
        run("native fate Mica pizza six threshold first three hoops first four original clocks actual finish", Groups);
        run("native fate Mica rolling count stages frozen geometry all risky effects FIFO retained danger and packet correction", Rolling);
        run("native fate Chi packet correction isolates overlapping generations and keeps second cone start origin after source moves", ComboCorrection);
        run("native fate Mica card deletion new insertion retains oldest surviving same OID and equal clock pizza source ordering", PersistentOrder);
        run("native fate Chi Created packet bootstrap receipts strict risky boundary inclusive display deadline and no duplicate late sample", CreatedEvents);
        run("native fate Chi Mica same-clock raw replacement packet correction retains original generation FIFO and consumed stages", SameClockReplacement);
        run("native fate Chi Mica Created OID generations override stale raw and preserve first packet positions original queues and removal", CreatedGenerations);
    }

    private static void Lifecycle()
    {
        foreach (var territory in new uint[] { 960, 1192 })
        {
            var center = Center(territory);
            var limit = territory == 960 ? 30 : MathF.Sqrt(420);
            var e = new DungeonEncounter(territory);
            var player = Player(territory) with { Position = center + new Vector3(limit + .01f, 0, 0) };
            e.Synchronize([Boss(territory), player], center, Start, playerId: 100);
            Check(e.Active == null && e.AmbientAoes().All(a => !a.Navigate), "Viewer incorrectly replaced source slot0 pull.");
            e.Synchronize([Boss(territory), Player(territory)], center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && e.Arena!.Position == center
                && e.Arena.Contains(center + new Vector3(territory == 960 ? 29.4f : 20.4f, 0, 0), 0),
                "Fixed real arena/pull missing.");
            e.Synchronize([Boss(territory) with { InCombat = false }, Player(territory) with { Dead = true }],
                center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active != null, "Non-SimpleBoss deaggro or viewer death ended fight.");
            e.Synchronize([Boss(territory) with { Hp = 0 }, Player(territory)], center, Start.AddSeconds(3), playerId: 100);
            Check(e.Active == null, "Primary HP0 did not finish.");
            e.ResetFight();
            Check(e.AmbientAoes().All(a => !a.Navigate), "Owner reset retained navigation.");
        }
    }

    private static void Combos()
    {
        foreach (var action in new uint[] { 25957, 25955, 25958, 25956, 25963, 25960 })
        {
            var e = Encounter(960);
            var origin = new Vector3(655, 0, 7);
            Cast(e, 1, action, 0, 8, origin, .3f);
            var combo = Chi(e, "combo").Where(a => a.Display).ToArray();
            Check(combo.Length == 2 && combo[0].Navigate && !combo[1].Navigate
                && combo[0].Activation == Start.AddSeconds(8) && combo[1].Activation == Start.AddSeconds(8).AddSeconds(3.1),
                $"Combo {action} stage clocks or risky state differ: {string.Join(';', combo.Select(a => $"{a.Navigate}/{a.Activation:O}"))}.");
            Check(combo[1].Position == (action is 25957 or 25958 ? Center(960) : origin),
                "Rect second cone origin must use caster position.");
            var expectedRotation = .3f + (action is 25958 or 25956 or 25963 or 25960 ? MathF.PI : 0);
            Check(MathF.Abs(combo[1].Rotation - expectedRotation) < .00001f, "Backwards cone differs.");
            var band = Chi(e, "combo").Single(a => !a.Display).Shape.Resolve(Center(960), 0, null);
            Check(band.Contains(Center(960), 0) == false && band.Contains(Center(960) + new Vector3(10, 0, 20), 0),
                "Inverted axis band is not a hard close-to-boss navigation constraint.");
            e.Finish(1, action, completed: false);
            Check(Chi(e, "combo").Count(a => a.Display) == 2, "Cancel consumed effect-only combo.");
            e.Effect(80, action, 1, Start.AddSeconds(9));
            Check(Chi(e, "combo").Single(a => a.Display).Navigate, "Second stage failed risky promotion.");
            e.Effect(80, action, 1, Start.AddSeconds(9));
            Check(Chi(e, "combo").Count(a => a.Display) == 1, "Duplicate native receipt consumed second stage.");
            e.Effect(90, action is 25958 or 25956 ? 25964u : 25961u, 2, Start.AddSeconds(12));
            Check(Chi(e, "combo").Length == 0, "Second actual effect failed FIFO.");
        }
    }

    private static void Created()
    {
        var a = new DungeonActor(10, 0x361E, new(650.125f, 0, 3.25f));
        var b = new DungeonActor(11, 0x3505, new(658, 0, 2));
        var e = Encounter(960, a, b);
        var shown = Chi(e, "bunker");
        Check(shown.Length == 2 && shown[0].Navigate && !shown[1].Navigate
            && shown[0].Position == a.Position && shown[0].Activation == Start.AddSeconds(14.8)
            && MathF.Abs(shown[0].Rotation - -.003f * MathF.PI / 180) < 1e-8f,
            "Created Bunker delay/frozen unquantized source/cardinal/risk window differs.");
        e.Synchronize([Boss(960), Player(960), b, a with { Position = Center(960), Dead = true }],
            Center(960), Start.AddSeconds(1), playerId: 100);
        Check(Chi(e, "bunker")[0].Position == a.Position, "Snapshot order/death moved copied Bunker.");
        e.RemoveActor(10);
        Check(Chi(e, "bunker").Length == 2, "Source destruction erased copied Bunker.");
        e.Effect(11, 25976, 1, Start.AddSeconds(2));
        Check(Chi(e, "bunker").Length == 2, "Effect masqueraded as real finish.");
        Cast(e, 11, 25976, 2, 12);
        e.Finish(11, 25976, completed: false);
        Check(Chi(e, "bunker").Single().Position == b.Position, "Different-source cancel failed global FIFO.");
        var bombs = Encounter(960, new(20, 0x364C, Center(960)), new(21, 0x364D, Center(960)));
        Check(Chi(bombs, "bomb").Length == 2 && Chi(bombs, "bomb").All(a => a.Navigate),
            "Queue was sorted by activation instead of Created insertion order.");
        Cast(bombs, 20, 27485, 0, 5);
        bombs.Finish(20, 27485);
        Check(Chi(bombs, "bomb").Single().Activation == Start.AddSeconds(5.7), "Bomb promotion reset original activation.");
    }

    private static void Ordinary()
    {
        foreach (var rule in NativeFateCatalog.Create(960).Single().Casts.Where(r => r.Shape != null))
        {
            var e = Encounter(960);
            Cast(e, 1, rule.Action, 0, 10);
            Check(e.OutputCasts.Single().Value.Rule == rule, $"Ordinary rule {rule.Action} missing.");
            e.Finish(1, rule.Action, completed: false);
            Check(!e.OutputCasts.Any(), "Ordinary cancel retained output.");
        }
        var raid = Encounter(960, Player(960, 101, 1));
        Cast(raid, 1, 25969, 0, 4);
        Check(raid.PredictedDamage.Count() == 2 && !raid.OutputCasts.Any(c => c.Value.Rule.Shape != null),
            "Raidwide prediction became danger or lost party.");
        Check(NativeFateCatalog.Create(960).Single().Casts.Single(r => r.Action == 25966).Shape is CircleTemplate { Radius: 25 },
            "Thermobaric used comment R55.");
    }

    private static void Baits()
    {
        foreach (var territory in new uint[] { 960, 1192 })
        {
            var action = territory == 960 ? 25972u : 38705u;
            var e = Encounter(territory, Player(territory, 101, 1));
            Cast(e, 1, action, 0, 5, target: 100);
            Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/bait"))
                && e.AmbientAoes().Any(a => a.Id.Contains("/party/")),
                $"Initial bait {territory} missing: {string.Join(',', e.AmbientAoes().Select(a => a.Id))}.");
            Check(e.AmbientAoes().Single(a => a.Id.EndsWith("/display") && a.Id.StartsWith("fate/bait")).OutlineOnly
                && e.AmbientAoes().Single(a => a.Id.Contains("/party/")).Position == Center(territory)
                && e.PredictedDamage.Single().TargetId == 100, "Self bait lost teammate navigation or damage.");
            e.RemoveActor(100);
            var source = Boss(territory) with
            {
                Cast = new(action, Center(territory), 0, Start, Start.AddSeconds(5), TargetId: 100),
            };
            e.Synchronize([source, Player(territory) with { Position = Center(territory) + new Vector3(15, 0, 0) },
                Player(territory, 101, 1)], Center(territory), Start.AddSeconds(1), playerId: 100);
            Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/bait")),
                $"Restored bait {territory} missing after real source CastInfo disappeared.");
            Check(e.AmbientAoes().Single(a => a.Id.EndsWith("/display") && a.Id.StartsWith("fate/bait")).Position == Center(territory)
                && e.AmbientAoes().Any(a => a.Id.EndsWith("/avoid")), "New ID generation rebound original bait.");
            e.Effect(1, action, 1, Start.AddSeconds(2));
            Check(e.AmbientAoes().Any(a => a.Id.StartsWith("fate/bait")), "Effect cleared finish-only bait.");
            e.Finish(1, action, completed: false);
            Check(!e.AmbientAoes().Any(a => a.Id.StartsWith("fate/bait")), "Actual finish did not clear bait.");
        }
    }

    private static DungeonActor[] Cards(uint territory = 1192) => Enumerable.Range(0, 6)
        .Select(i => new DungeonActor((ulong)(20 + i), (uint)(0x43EE + i), Center(territory) + new Vector3(i * 14, 0, 0),
            Targetable: false)).ToArray();

    private static void Cards()
    {
        var cards = Cards(1192);
        var e = Encounter(1192, cards);
        Cast(e, 1, 38666, 0, 4);
        var initial = Mica(e);
        Check(initial.Length == 5 && initial.All(a => !a.Navigate && a.Activation == default(DateTime))
            && initial[0].Position == Quantized(cards[1].Position + new Vector3(0, 0, -10))
            && initial[0].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -7, MaxLocalX: 7 },
            "Draw did not exclude safe index0 or preserve default clock and exact rectangular origin.");
        e.Finish(1, 38666);
        Cast(e, 1, 38673, 5, 6.5);
        e.Finish(1, 38673);
        Cast(e, 1, 38678, 7, 11);
        Check(Mica(e).Length == 5 && Mica(e).All(a => a.Navigate && a.Activation == Start.AddSeconds(11).AddSeconds(.6)),
            "CardTrick .6 activation or cached draw replacement differs.");
        e.Finish(1, 38678);
        Cast(e, 25, 39156, 12, 13);
        e.Finish(25, 39156, completed: false);
        Check(Mica(e).Length == 5 && Mica(e).All(a => !a.Navigate)
            && Mica(e)[0].Position == Quantized(cards[0].Position + new Vector3(0, 0, -10)), "Fake finish did not promote cached safe index1.");
    }

    private static void MissingCards()
    {
        var cards = Cards(1192);
        var e = Encounter(1192, cards[1]);
        Cast(e, 1, 38666, 0, 4);
        Check(Mica(e).Length == 1, "Missing card slots synthesized hazards.");
        e.Finish(1, 38666);
        e.Synchronize([Boss(1192), Player(1192), .. cards.Reverse()], Center(1192), Start.AddSeconds(5), playerId: 100);
        Check(Mica(e).Length == 1, "Existing five-slot draw backfilled missing actors.");
        Cast(e, 1, 38667, 5, 9);
        e.Finish(1, 38667);
        Cast(e, 25, 39156, 10, 11);
        e.Finish(25, 39156);
        Check(Mica(e).Length == 5, "Cached promotion did not sample current card set.");
    }

    private static void Flourish()
    {
        var e = Encounter(1192, new(10, 0x43EC, Center(1192)), new(11, 0x43EC, Center(1192)));
        Cast(e, 10, 38697, 0, 7);
        Cast(e, 11, 38701, 0, 5);
        Check(Mica(e).Single().Shape is DonutTemplate && Mica(e).Single().Activation == Start.AddSeconds(5),
            "Flourish did not swap first two by real activation.");
        e.Effect(11, 38701, 1, Start.AddSeconds(5));
        Check(Mica(e).Single().Shape is DonutTemplate, "Effect consumed Flourish.");
        e.Finish(10, 38697, completed: false);
        Check(Mica(e).Single().Shape is CircleTemplate && Mica(e).Single().Activation == Start.AddSeconds(7),
            "Finish must remove global head, not matching source.");
    }

    private static void Groups()
    {
        var helpers = Enumerable.Range(10, 11).Select(id => new DungeonActor((ulong)id, 0x43EC, Center(1192))).ToArray();
        var e = Encounter(1192, helpers);
        for (var i = 0; i < 5; ++i) Cast(e, (ulong)(10 + i), 38694, 0, 10 - i);
        Check(Mica(e).Select(a => a.Activation).SequenceEqual(new[] { 10, 9, 8 }.Select(t => (DateTime?)Start.AddSeconds(t))),
            "Pizzas sorted before six.");
        Cast(e, 15, 38695, 0, 5);
        Check(Mica(e).Select(a => a.Activation).SequenceEqual(new[] { 5, 6, 7 }.Select(t => (DateTime?)Start.AddSeconds(t))),
            "Pizzas six threshold or first3 differs.");
        e.Finish(15, 38695, completed: false);
        Check(Mica(e).First().Activation == Start.AddSeconds(6), "Pizzas finish did not promote original clock.");
        for (var i = 0; i < 5; ++i) Cast(e, (ulong)(16 + i), 38692, 0, 20 + i);
        Check(Mica(e).Count(a => a.Shape is RayRectangleTemplate { Length: 42 }) == 4, "Hoops first4 cap differs.");
    }

    private static void Rolling()
    {
        var e = Encounter(1192, new(10, 0x43EC, Center(1192)), new(11, 0x43EC, Center(1192)));
        Cast(e, 10, 38996, 0, 2);
        Cast(e, 11, 38999, 0, 2, new(800, 0, 600), .5f);
        var aoes = Mica(e);
        Check(aoes.Length == 2 && aoes.All(a => a.Navigate) && aoes[0].Activation == Start.AddSeconds(7)
            && aoes[1].Activation == Start.AddSeconds(7.6) && aoes[1].Shape is RayRectangleTemplate { Length: 52 },
            "Rolling count stages or all-risky source differs.");
        e.Finish(10, 38996, completed: false);
        Check(Mica(e).Length == 2, "Visual finish consumed real charge.");
        e.Effect(1, 38687, 1, Start.AddSeconds(7));
        var next = Mica(e).Single();
        Check(next.Activation == Start.AddSeconds(7.6) && next.Position == new Vector3(800, 0, 600), "Promotion reset stage clock.");
        e.Effect(1, 38688, 2, Start.AddSeconds(8));
        Check(Mica(e).Length == 0, "Remaining real charge did not clear.");
    }

    private static void ComboCorrection()
    {
        var e = Encounter(960);
        Cast(e, 1, 25957, 0, 8, new(651, 0, 2));
        var first = Chi(e, "combo").Where(a => a.Display).ToArray();
        e.Finish(1, 25957, completed: false);
        var b = new DungeonCastObservation(25957, new(652, 0, 3), .2f, Start.AddSeconds(10), Start.AddSeconds(18));
        e.StartCast(1, b, Start.AddSeconds(10), fromPacket: true);
        e.Synchronize([Boss(960) with { Position = new(660, 0, 0), Cast = b }, Player(960)], Center(960),
            Start.AddSeconds(10.1), playerId: 100);
        e.StartCast(1, b with { Location = new(654, 0, 4), Rotation = .4f }, Start.AddSeconds(10.2), fromPacket: true);
        var all = Chi(e, "combo").Where(a => a.Display).ToArray();
        Check(all.Length == 4 && all[0] == first[0] && all[1] == first[1]
            && all[2].Position == new Vector3(654, 0, 4) && all[3].Position == Center(960)
            && all[3].Rotation == .4f, "Correction rewrote old generation or moved second cone's locked source origin.");
        for (uint i = 1; i <= 3; ++i) e.Effect(1, 25957, i, Start.AddSeconds(11 + i));
        e.StartCast(1, b with { Location = new(655, 0, 5), Rotation = .6f }, Start.AddSeconds(14.2), fromPacket: true);
        Check(Chi(e, "combo").Count(a => a.Display) == 1, "Correction resurrected consumed stages.");
    }

    private static void PersistentOrder()
    {
        var cards = Cards(1192);
        var old = cards[1] with { Id = 80, Position = new(801, 0, 590) };
        var survivor = cards[1] with { Id = 81, Position = new(802, 0, 590) };
        var fresh = cards[1] with { Id = 82, Position = new(803, 0, 590) };
        var e = Encounter(1192, old, survivor);
        e.RemoveActor(old.Id);
        e.Synchronize([Boss(1192), Player(1192), fresh, survivor], Center(1192), Start.AddSeconds(1), playerId: 100);
        Cast(e, 1, 38666, 1, 5);
        Check(Mica(e).Single().Position == Quantized(survivor.Position + new Vector3(0, 0, -10)),
            "New actor reused removed dictionary order before surviving first card.");
        var helpers = Enumerable.Range(10, 6).Select(i => new DungeonActor((ulong)i, 0x43EC, Center(1192))).ToArray();
        e = Encounter(1192, helpers);
        for (var i = 0; i < 6; ++i) Cast(e, (ulong)(10 + i), 38694, 0, 10, new(790 + i, 0, 593));
        Check(Mica(e).Select(a => a.Position.X).SequenceEqual(new float[] { 795, 790, 791 }),
            "Equal-clock right-pivot source sorting differs from F/A/B.");
    }

    private static void CreatedEvents()
    {
        var e = Encounter(960);
        foreach (var (id, ticks) in new[] { (10ul, 0L), (11ul, 10_000_000L), (12ul, 25_000_000L), (13ul, 25_000_001L) })
        {
            var now = Start.AddTicks(ticks);
            e.Tick(now);
            e.ObjectCreated(id, now, 0x361E, new Vector3(640 + id, 0, 0));
        }
        var shown = Chi(e, "bunker");
        Check(shown.Length == 3 && shown.Count(a => a.Navigate) == 1,
            "Created strict +1 risky or inclusive +2.5 display end differs.");
        e.Synchronize([Boss(960), Player(960), new(10, 0x361E, new(660, 0, 0))], Center(960),
            Start.AddSeconds(3), playerId: 100);
        Check(Chi(e, "bunker").Length == 3 && Chi(e, "bunker")[0].Position == new Vector3(650, 0, 0),
            "Late first raw sample duplicated/relocked a captured Created packet.");
    }

    private static void SameClockReplacement()
    {
        foreach (var territory in new uint[] { 960, 1192 })
        {
            var e = Encounter(territory);
            var action = territory == 960 ? 25957u : 38996u;
            var a = new DungeonCastObservation(action, Center(territory), 0, Start, Start.AddSeconds(8), TotalTime: 8);
            e.StartCast(1, a, Start, fromPacket: false);
            var old = territory == 960 ? Chi(e, "combo").Where(x => x.Display).ToArray() : Mica(e);
            var b = a with { Location = Center(territory) + new Vector3(1, 0, 0), Rotation = .2f };
            e.StartCast(1, b, Start.AddSeconds(.1), fromPacket: false);
            e.StartCast(1, b with { Rotation = .4f }, Start.AddSeconds(.2), fromPacket: true);
            var all = territory == 960 ? Chi(e, "combo").Where(x => x.Display).ToArray() : Mica(e);
            Check(all.Length == old.Length * 2 && all.Take(old.Length).Select(x => (x.Position, x.Rotation, x.Activation))
                .SequenceEqual(old.Select(x => (x.Position, x.Rotation, x.Activation)))
                && all[old.Length].Rotation == .4f, "Same-clock correction rewrote the older raw generation.");
            e.Effect(1, territory == 960 ? 25957u : 38687u, 1, Start.AddSeconds(1));
            e.StartCast(1, b with { Rotation = .6f }, Start.AddSeconds(1.1), fromPacket: true);
            all = territory == 960 ? Chi(e, "combo").Where(x => x.Display).ToArray() : Mica(e);
            Check(all.Length == old.Length * 2 - 1 && all[^1].Rotation == .6f,
                "Correction resurrected a consumed stage or failed to follow its actual record.");
        }
    }

    private static void CreatedGenerations()
    {
        var old = new DungeonActor(10, 0x3505, new(640, 0, 0));
        var e = Encounter(960, old);
        e.Tick(Start.AddSeconds(1));
        e.ObjectCreated(10, Start.AddSeconds(1), 0x361E, new Vector3(650, 0, 1));
        e.Synchronize([Boss(960), Player(960), old], Center(960), Start.AddSeconds(2), playerId: 100);
        var bunkers = Chi(e, "bunker");
        Check(bunkers.Length == 2 && bunkers[1].Position == new Vector3(650, 0, 1)
            && bunkers[1].Activation == Start.AddSeconds(1).AddSeconds(14.8),
            "New Created OID was discarded or reverted by the lagging old raw.");
        e.Synchronize([Boss(960), Player(960), old with { Oid = 0x361E, Position = new(660, 0, 0) }],
            Center(960), Start.AddSeconds(3), playerId: 100);
        Check(Chi(e, "bunker").Length == 2 && Chi(e, "bunker")[1].Position == new Vector3(650, 0, 1),
            "Confirmed new raw duplicated or moved a copied Created prediction.");

        e = Encounter(960);
        e.ObjectCreated(10, Start, 0x3505, old.Position);
        e.Synchronize([Boss(960), Player(960), old with { Oid = 0x361E }],
            Center(960), Start.AddSeconds(1), playerId: 100);
        Check(Chi(e, "bunker").Length == 2
            && Chi(e, "bunker")[1].Activation == Start.AddSeconds(1).AddSeconds(14.8),
            "First different-OID raw was postponed until the second scan.");

        e = Encounter(960);
        e.ObjectCreated(10, Start, 0x361E, old.Position);
        e.ResetFight();
        e.Synchronize([Boss(960), Player(960)], Center(960), Start.AddSeconds(2), playerId: 100);
        Check(Chi(e, "bunker").Single().Activation == Start.AddSeconds(2).AddSeconds(14.8),
            "New component activation did not bootstrap the still-present packet-only world actor.");
        e.Synchronize([Boss(960), Player(960), old with { Oid = 0x361E }], Center(960),
            Start.AddSeconds(3), playerId: 100);
        Check(Chi(e, "bunker").Length == 1, "Late raw duplicated the activation bootstrap.");

        var mica = Encounter(1192);
        mica.ObjectCreated(80, Start, 0x43EC, Center(1192));
        mica.ObjectCreated(80, Start, 0x43EF, new Vector3(800, 0, 590));
        Cast(mica, 1, 38666, 0, 4);
        Check(Mica(mica).Single().Position == Quantized(new Vector3(800, 0, 580)),
            "Packet-only same-ID helper to card creation failed before the first sample.");
        mica.RemoveActor(80);
        mica.ObjectCreated(80, Start, 0x43EF, new Vector3(810, 0, 590));
        mica.Finish(1, 38666);
        Cast(mica, 1, 38666, 1, 5);
        mica.Finish(1, 38666);
        Cast(mica, 1, 39156, 2, 3);
        mica.Finish(1, 39156);
        Check(Mica(mica).Single().Position == Quantized(new Vector3(810, 0, 580)),
            "Removed packet-only card survived in the persistent world order.");
    }
}
