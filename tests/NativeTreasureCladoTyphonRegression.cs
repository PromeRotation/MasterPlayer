using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureCladoTyphonRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid = 0x233C, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true);
    private static DungeonActor Player(ulong id = 100, Vector3? position = null) =>
        new(id, 0, position ?? Center + Vector3.UnitZ * 10, Player: true, Hostile: false);
    private static DungeonCastObservation Cast(uint action, ulong target = 100, double started = 0,
        double duration = 5, float rotation = .3f) => new(action, Center, rotation,
            Start.AddSeconds(started), Start.AddSeconds(started + duration), target);
    private static DungeonEncounter Create(uint oid)
    {
        var e = new DungeonEncounter(924) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, oid), Enemy(2), Enemy(3), Enemy(4, position: Center + Vector3.UnitX * 7),
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
        run("native treasure Clado Typhon full active rules parameters inactive boundary", Rules);
        run("native treasure Clado rotation pairing both orders actor origin zero facing unknown icon", Pairing);
        run("native treasure Clado rotation appended six real native effects global FIFO consumption clocks", CladoEffects);
        run("native treasure Clado future cache stable late observation rebuild consumed corrections", Cache);
        run("native treasure Typhon bootstrap creation double sequences signs circle independent clocks", Created);
        run("native treasure Typhon hidden outputs continue axis angle matched effects restore cached clocks", Hidden);
        run("native treasure Typhon circle single slot any21691 only clear retains rotations", Circle);
        run("native treasure Typhon destroyed creation receipts stale frame new instance generations", Generations);
        run("native treasure Clado dynamic charge dead target source revival real Finish and prediction", Charge);
        run("native treasure Clado spread8 and Typhon exact eight stack FIFO no forecast", Party);
        run("native treasure Clado Typhon complete All rewards markers and priority", Rewards);
    }
    private static void Rules()
    {
        foreach (var (oid, count) in new[] { (0x3027u, 9), (0x301Au, 12) })
        {
            var e = Create(oid);
            Check(e.Active!.Casts.Length == count && NativeTreasureCatalog.Owns(oid, 924)
                && !NativeTreasureCatalog.Owns(oid, 1279), "Catalog/territory count differs.");
        }
        var rules = Create(0x301A).Active!.Casts.ToDictionary(c => c.Action);
        Check(rules[21695].Shape is CircleTemplate { Radius: 20 } && rules[21772].Shape is RayRectangleTemplate { Length: 40, MinLocalX: -3, MaxLocalX: 3 }
            && !rules.ContainsKey(21687) && !rules.ContainsKey(21782) && !NativeTreasureCatalog.RaidwideAction(21687)
            && !Create(0x3027).Active!.Casts.Any(c => c.Action == 21770), "Inactive/effective source parameters differ.");
    }
    private static void Pairing()
    {
        var e = Create(0x3027);
        e.StartCast(2, Cast(21706), Start);
        e.Icon(101, 168, Start, actorId: 4);
        Check(e.TreasureRotations.Single() is { Source: 4, Remaining: 6, MaxShown: 2 } sequence
            && MathF.Abs(sequence.Origin.X - 107) < .05f && sequence.NextActivation == Start.AddSeconds(5)
            && sequence.Increment == MathF.PI / 3, "Late icon did not use pairing actor/raw direction.");
        var zero = Create(0x3027);
        zero.Icon(100, 167, Start, actorId: 4);
        zero.StartCast(2, Cast(21706, rotation: 0), Start);
        Check(!zero.TreasureRotations.Any(), "Zero-facing source incorrectly initialized.");
        zero.StartCast(2, Cast(21706, rotation: .7f), Start.AddSeconds(.1));
        Check(zero.TreasureRotations.Single().Source == 2
            && zero.TreasureRotations.Single().Rotation == .7f, "Accepted zero-facing correction failed pending pairing.");
        zero.StartCast(2, Cast(21706, rotation: .8f), Start.AddSeconds(.2));
        Check(zero.TreasureRotations.Count == 1, "Consumed same-generation correction rebuilt sequence.");
        zero.Icon(100, 167, Start, actorId: 4);
        zero.StartCast(3, Cast(21706), Start);
        Check(zero.TreasureRotations.Last().Source == 3 && zero.TreasureRotations.Last().Increment < 0, "Icon-first pair failed.");
        var unknown = Create(0x3027);
        unknown.Icon(100, 167, Start, actorId: 4);
        unknown.Icon(100, 999, Start, actorId: 4);
        unknown.StartCast(2, Cast(21706), Start);
        Check(!unknown.TreasureRotations.Any(), "Unknown icon did not reset pending increment.");
    }
    private static void CladoEffects()
    {
        var e = Create(0x3027);
        for (var i = 0; i < 2; ++i)
        {
            e.StartCast((ulong)i + 2, Cast(21706), Start);
            e.Icon(100, 168, Start, actorId: 4);
        }
        e.Finish(2, 21706, Start.AddSeconds(1));
        e.RemoveActor(4);
        Check(e.TreasureRotations.Count == 2 && Find(e, "treasure/rotation/").Length == 4, "Finish/destroy cleared rotation.");
        for (var i = 0; i < 6; ++i)
            e.TreasureEffect(3, i % 2 == 0 ? 21706u : 21707u, (uint)i + 1, 100, Start.AddSeconds(10 + i));
        Check(e.TreasureRotations.Count == 1 && e.TreasureRotations.Single().Remaining == 6, "Effects did not consume first sequence exactly six.");
        e.TreasureEffect(3, 21707, 99, 100, Start.AddSeconds(50));
        Check(e.TreasureRotations.Single().Remaining == 5 && e.TreasureRotations.Single().NextActivation == Start.AddSeconds(50).AddSeconds(2.1d),
            "Next rotation clock not consumption clock plus2.1.");
        e.TreasureEffect(3, 21707, 99, 100, Start.AddSeconds(51));
        Check(e.TreasureRotations.Single().Remaining == 5, "Duplicate native packet advanced rotation.");
    }
    private static void Cache()
    {
        var e = Create(0x3027);
        var cast = Cast(21706);
        e.StartCast(2, cast, Start);
        e.Tick(Start.AddSeconds(20));
        e.Icon(100, 168, Start, actorId: 4);
        var future = Find(e, "treasure/rotation/").Single(a => a.Id.EndsWith("/1"));
        Check(future.Activation == Start.AddSeconds(20).AddSeconds(2.1d), "Late future baseline differs.");
        e.Tick(Start.AddSeconds(30));
        Check(Find(e, "treasure/rotation/").Single(a => a.Id.EndsWith("/1")).Activation == future.Activation, "Future drifted without version change.");
        e.StartCast(2, cast with { Rotation = 1 }, Start.AddSeconds(31));
        Check(e.TreasureRotations.Count == 1, "Accepted correction rebuilt consumed pairing.");
        e.TreasureEffect(3, 21707, 1, 100, Start.AddSeconds(40));
        Check(Find(e, "treasure/rotation/").Single(a => a.Id.EndsWith("/1")).Activation
            == Start.AddSeconds(40).AddSeconds(2.1d).AddSeconds(2.1d), "Effect future clock differs.");
    }
    private static void Created()
    {
        var e = Create(0x301A);
        e.Tick(Start.AddSeconds(2));
        e.ObjectCreated(20, Start, 0x3082, Center, .7f);
        e.ObjectCreated(21, Start, 0x3050, Center, 1);
        e.ObjectCreated(22, Start, 0x3051, Center);
        Check(e.TreasureRotations.Count == 4 && e.TreasureRotations.All(s => s.Remaining == 31 && s.MaxShown == 9
            && s.NextActivation == Start.AddSeconds(2).AddSeconds(14.5d) && s.Interval == 1.1d)
            && e.TreasureRotations[0].Increment > 0 && e.TreasureRotations[2].Increment < 0
            && Find(e, "treasure/rotation/").Length == 36
            && e.TreasureSnortsaultCircle!.Activation == Start.AddSeconds(2).AddSeconds(14.3d), "Creation clock/sign/center/31x9 differs.");
        var first = e.TreasureRotations[0];
        Check(e.TreasureRotations[1].Rotation == first.Rotation + MathF.PI && first.Radius == 20 && first.Angle == MathF.PI / 4,
            "Opposite cone geometry differs.");
    }
    private static void Hidden()
    {
        var e = Create(0x301A);
        e.ObjectCreated(20, Start, 0x3082, Center, .7f);
        e.ObjectCreated(21, Start, 0x3051, Center);
        e.StartCast(2, Cast(21695), Start);
        Check(!Find(e, "treasure/rotation/").Any() && !Find(e, "treasure/snortsault/").Any(), "Lingering did not hide both components.");
        var origin = e.TreasureRotations[0].Origin;
        e.Synchronize([Enemy(1, 0x301A), Enemy(2) with { Cast = Cast(21695) },
            Enemy(3, position: origin + new Vector3(.9f, 0, .9f)) with { Rotation = .74f }, Player(), Player(101)],
            Center, Start.AddSeconds(10), playerId: 100);
        e.TreasureEffect(3, 21781, 1, 100, Start.AddSeconds(10));
        Check(e.TreasureRotations[0].Remaining == 30 && e.TreasureRotations[1].Remaining == 31, "Hidden axis match failed/effect advanced both.");
        e.Finish(2, 21695, Start.AddSeconds(11));
        Check(Find(e, "treasure/rotation/").Length == 18 && Find(e, "treasure/snortsault/").Length == 1
            && e.TreasureRotations[0].NextActivation == Start.AddSeconds(10).AddSeconds(1.1d)
            && e.TreasureSnortsaultCircle!.Activation == Start.AddSeconds(14.3d), "Unhide reset clock or missed hidden progression.");
    }
    private static void Circle()
    {
        var e = Create(0x301A);
        e.ObjectCreated(20, Start, 0x3051, Center);
        e.ObjectCreated(21, Start, 0x3082, Center);
        e.Tick(Start.AddSeconds(3));
        e.ObjectCreated(22, Start, 0x3051, Center);
        e.TreasureEffect(2, 21782, 1, 100);
        Check(e.TreasureSnortsaultCircle!.Source == 22 && Find(e, "treasure/snortsault/").Length == 1
            && e.TreasureSnortsaultCircle.Activation == Start.AddSeconds(3).AddSeconds(14.3d), "Circle not single slot/21782 incorrectly cleared.");
        e.TreasureEffect(3, 21691, 1, 100);
        Check(e.TreasureSnortsaultCircle == null && e.TreasureRotations.Count == 2, "21691 failed circle-only clear.");
    }
    private static void Generations()
    {
        var e = Create(0x301A);
        var body = Enemy(20, 0x3082);
        e.Synchronize([Enemy(1, 0x301A), body, Player()], Center, Start, playerId: 100);
        e.RemoveActor(20);
        e.Synchronize([Enemy(1, 0x301A), body, Player()], Center, Start.AddSeconds(1), playerId: 100);
        Check(e.TreasureRotations.Count == 2, "Stale frame rebuilt destroyed creation.");
        e.Tick(Start.AddSeconds(2));
        e.ObjectCreated(20, Start, 0x3082, Center);
        Check(e.TreasureRotations.Count == 4, "Real new creation failed new generation.");
    }
    private static void Charge()
    {
        var e = Create(0x3027);
        var cast = Cast(21709, 101);
        e.StartCast(2, cast, Start);
        var target = Player(101, Center + new Vector3(6, 20, 8)) with { Dead = true };
        e.Synchronize([Enemy(1, 0x3027), Enemy(2) with { Cast = cast }, Player(), target], Center, Start.AddSeconds(1), playerId: 100);
        Check(Find(e, "treasure/cast-bait/").Single(a => a.Display).Shape is RayRectangleTemplate { Length: 10, MinLocalX: -2.5f, MaxLocalX: 2.5f }
            && e.PredictedDamage.Single().TargetId == 101 && !e.Hints(Player().Position).Contains("Tankbuster cleave"),
            "Charge planar length/dead target/prediction/text differs.");
        e.TreasureEffect(2, 21709, 1, 101);
        Check(Find(e, "treasure/cast-bait/").Any(), "Effect consumed Finish charge.");
        e.Finish(2, 21709, Start.AddSeconds(2));
        Check(!Find(e, "treasure/cast-bait/").Any(), "Charge Finish retained.");
    }
    private static void Party()
    {
        var clado = Create(0x3027);
        clado.StartCast(2, Cast(21708, 101), Start);
        Check(Find(clado, "quest/spread/").Single(a => a.Display).Shape is CircleTemplate { Radius: 8 }
            && Find(clado, "quest/spread/").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 9 }
            && !clado.PredictedDamage.Any(), "Spread8/9 differs.");
        var typhon = Create(0x301A);
        typhon.StartCast(2, Cast(21688), Start);
        Check(Find(typhon, "treasure/stack/").Any() && typhon.Hints(Player().Position).Contains("Stack! (1/8)")
            && !typhon.PredictedDamage.Any(), "Fireball not exact8/no forecast.");
        typhon.StartCast(3, Cast(21690), Start);
        Check(typhon.TreasureWallKnockbackSources.Single() is { Distance: 20, StopAtWall: true }
            && Find(typhon, "treasure/kb/").All(a => !a.Navigate), "Typhon wall20 projected movement broadened.");
    }
    private static void Rewards()
    {
        foreach (var (oid, add) in new[] { (0x3027u, 0x3028u), (0x301Au, 0x3024u) })
        {
            var e = Create(oid);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(20, add), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && NativeTreasureCatalog.Priority(oid, add) == 1
                && Find(e, "treasure/enemy/").Length == 1, "All/add marker/priority differs.");
            e.Synchronize([Enemy(20, 0x301B), Enemy(21, 0x3050), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Unlisted helper extended All.");
        }
    }
}
