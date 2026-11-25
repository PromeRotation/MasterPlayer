using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;
using PromeRotation.LogSystem;

internal static class NativeTreasureSequenceRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    internal static void Run(Action<string, Action> run)
    {
        run("native treasure Basket and Porxie active components geometry and ownership", Geometry);
        run("native treasure Basket concentric finish timing and locked geometry", StrikeTiming);
        run("native treasure Basket first spatial angular match ignores caster identity", StrikeMatching);
        run("native treasure Basket mismatched finish and effects cannot advance", StrikeFailures);
        run("native treasure Basket raw changes start new sequences and packet corrections stay in generation", StrikeGenerations);
        run("native treasure Basket delayed raidwide only real native effect clears", Earthquake);
        run("native treasure Porxie four explosions three shown and effect position authority", SweepAdvance);
        run("native treasure Porxie square tolerance first matching line and source independence", SweepMatching);
        run("native treasure Porxie cancellation destroy and long pause preserve lines", SweepLifetime);
        run("native treasure Porxie future timestamps cache until line version changes", SweepCache);
        run("native treasure Basket and Porxie All rewards and excluded helper lifecycle", Groups);
        run("native treasure Basket old packet correction cannot reclaim or resurrect earthquake", EarthquakeCorrection);
        run("native treasure Basket delayed cancel and removal use processing clock", LateFinish);
        run("native treasure Porxie packet source lock correction preserves accepted origin", SweepOriginCorrection);
    }

    private static DungeonActor Enemy(ulong id, uint oid, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true,
            ObjectIndex: (ushort)(id * 2), Hp: 100, MaxHp: 100);
    private static DungeonActor Player(ulong id = 100, bool dead = false) =>
        new(id, 0, Center, Dead: dead, Hostile: false, Player: true);
    private static DungeonEncounter Create(uint oid)
    {
        var e = new DungeonEncounter(924) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, oid), .. Enumerable.Range(2, 7).Select(i => Enemy((ulong)i, 0x233C)), Player(), Player(101)],
            Center, Start, playerId: 100);
        return e;
    }
    private static DungeonCastObservation Cast(uint action, double time = 0, Vector3? location = null, float rotation = 0) =>
        new(action, location ?? Center, rotation, Start.AddSeconds(time), Start.AddSeconds(time + 4.3));
    private static DungeonAmbientAoe[] Strikes(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/strike/")).ToArray();
    private static DungeonAmbientAoe[] Sweeps(DungeonEncounter e) => e.AmbientAoes().Where(a => a.Id.StartsWith("treasure/sweep/")).ToArray();
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    private static void Finish(DungeonEncounter e, ulong source, uint action, DateTime time)
    {
        e.Tick(time);
        e.Finish(source, action, time);
    }
    private static void Geometry()
    {
        var b = Create(0x302D).Active!.Casts.ToDictionary(c => c.Action);
        Check(b.Count == 16 && b[21722].Shape is CircleTemplate { Radius: 8 }
            && b[21732].Shape is RayRectangleTemplate { Length: 15, MinLocalX: -2, MaxLocalX: 2 }
            && b[21728].Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 20 }
            && new uint[] { 21724, 21725, 21726, 21729 }.All(a => b[a].Shape == null)
            && !b.ContainsKey(21727) && !b.ContainsKey(21770), "Basket active components differ.");
        var p = Create(0x3014).Active!.Casts.ToDictionary(c => c.Action);
        Check(p.Count == 12 && p[21670].Shape is FanTemplate { Radius: 5 } cone && cone.AngleRadians == MathF.PI / 3
            && p[21671].Shape is CircleTemplate { Radius: 6 } && p[22936].Shape is CircleTemplate { Radius: 6 }
            && p[22937].Shape == null && !p.ContainsKey(22935) && !p.ContainsKey(21770),
            "Porxie active components differ or inactive visual/knockback added.");
        Check(new uint[] { 6448, 6449, 6450, 6451, 6452 }.All(a => p[a].Shape is CircleTemplate { Radius: 6.84f }),
            "Mandragora grouped radius changed.");
    }
    private static void StrikeTiming()
    {
        var e = Create(0x302D);
        var origin = Center + new Vector3(.123f, 0, .231f);
        e.StartCast(2, Cast(21724, location: origin, rotation: .2f), Start);
        Check(Strikes(e).Single() is { Shape: FanTemplate { Radius: 6.5f }, Position: var pos, Rotation: .2f } && pos == origin
            && Strikes(e).Single().Activation == Start.AddSeconds(4.3), "First strike failed exact lock or activation.");
        Finish(e, 2, 21724, Start.AddSeconds(1));
        var second = Strikes(e).Single();
        Check(second.Shape is PtDonutSectorTemplate { Inner: 6.5f, Outer: 12.5f }
            && second.Position == origin && second.Rotation == .2f && second.Activation == Start.AddSeconds(2.1),
            "Cancellation should advance to second stage at processing now+1.1.");
        e.StartCast(3, Cast(21725, 2, origin, .2f), Start.AddSeconds(2));
        Check(Strikes(e).Single().Activation == Start.AddSeconds(2.1), "Later Start overrode predicted stage.");
        Finish(e, 3, 21725, Start.AddSeconds(3));
        Check(Strikes(e).Single().Shape is PtDonutSectorTemplate { Inner: 12.5f, Outer: 18.5f }
            && Strikes(e).Single().Activation == Start.AddSeconds(3).AddSeconds(1.1d),
            $"Third stage interval differs: {Strikes(e).Single()}.");
        e.StartCast(4, Cast(21726, 4, origin, .2f), Start.AddSeconds(4));
        Finish(e, 4, 21726, Start.AddSeconds(5));
        Check(Strikes(e).Length == 0, "Final finish did not remove sequence.");
    }
    private static void StrikeMatching()
    {
        var e = Create(0x302D);
        e.StartCast(2, Cast(21724), Start);
        e.StartCast(3, Cast(21724, rotation: MathF.Tau), Start);
        Finish(e, 3, 21724, Start.AddSeconds(1));
        Check(Strikes(e).Length == 2 && Strikes(e)[0].Shape is PtDonutSectorTemplate && Strikes(e)[1].Shape is FanTemplate,
            "Cross-source Finish did not advance first spatial match.");
        e.StartCast(4, Cast(21725, 2, Center + new Vector3(.9f, 0, .9f), .049f), Start.AddSeconds(2));
        Finish(e, 4, 21725, Start.AddSeconds(3));
        Check(Strikes(e)[0].Shape is PtDonutSectorTemplate { Inner: 12.5f }, "Axis-wise .9,.9 or angular tolerance was reduced.");
        Finish(e, 2, 21724, Start.AddSeconds(4));
        Check(Strikes(e).All(a => a.Shape is PtDonutSectorTemplate), "Second overlapping first-stage record lost.");
    }
    private static void StrikeFailures()
    {
        var e = Create(0x302D);
        e.StartCast(2, Cast(21724), Start);
        Finish(e, 2, 21724, Start.AddSeconds(1));
        e.StartCast(3, Cast(21725, 2, Center + Vector3.UnitX * 1.01f), Start.AddSeconds(2));
        Finish(e, 3, 21725, Start.AddSeconds(3));
        e.StartCast(4, Cast(21725, 4, rotation: .051f), Start.AddSeconds(4));
        Finish(e, 4, 21725, Start.AddSeconds(5));
        e.Effect(99, 21725, 1, Start.AddSeconds(6));
        e.Tick(Start.AddSeconds(100));
        Check(Strikes(e).Single().Shape is PtDonutSectorTemplate { Inner: 6.5f }
            && e.Hints(Center + Vector3.UnitZ * 9).Contains("GTFO from aoe!"),
            "Mismatch/effect/timeout consumed sequence or active hint missing.");
        e.StartCast(5, Cast(21725, 101), Start.AddSeconds(101));
        e.Tick(Start.AddSeconds(102));
        e.RemoveActor(5, Start.AddSeconds(102));
        Check(Strikes(e).Single().Shape is PtDonutSectorTemplate { Inner: 12.5f }
            && Strikes(e).Single().Activation == Start.AddSeconds(102).AddSeconds(1.1d),
            $"Destroy failed actual Finish progression: {Strikes(e).Single()}.");
    }
    private static void StrikeGenerations()
    {
        var e = Create(0x302D);
        var cast = Cast(21724);
        e.StartCast(2, cast, Start, false);
        e.StartCast(2, cast with { Rotation = .01f }, Start.AddSeconds(.1));
        Check(Strikes(e).Length == 1 && Strikes(e).Single().Rotation == .01f, "Packet correction appended a sequence.");
        e.StartCast(2, cast with { TotalTime = 4.1f }, Start.AddSeconds(.2), false);
        Check(Strikes(e).Length == 2 && Strikes(e)[0].Shape is PtDonutSectorTemplate
            && Strikes(e)[1].Shape is FanTemplate, "True raw replacement failed Finish then fresh Start.");
    }
    private static void Earthquake()
    {
        var e = Create(0x302D);
        e.StartCast(2, Cast(21729), Start);
        Check(e.PredictedDamage.Count() == 2 && e.PredictedDamage.All(p => p.Activation == Start.AddSeconds(4.3).AddSeconds(1.2d))
            && e.Hints(Center).Contains("Raidwide") && !Strikes(e).Any(),
            $"Delayed raidwide prediction differs: {string.Join(';', e.PredictedDamage)}.");
        Finish(e, 2, 21729, Start.AddSeconds(1));
        e.RemoveActor(2, Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(100));
        e.Effect(99, 21730, 1, Start.AddSeconds(100));
        Check(e.PredictedDamage.Count() == 2, "Cancel/destroy/time/expanded effect cleared native prediction.");
        e.TreasureEffect(99, 21730, 1, 0, Start.AddSeconds(101));
        Check(!e.PredictedDamage.Any() && !e.Hints(Center).Contains("Raidwide"), "Native effect did not clear across sources.");
        e.StartCast(3, Cast(21729, 102), Start.AddSeconds(102));
        e.StartCast(4, Cast(21729, 103), Start.AddSeconds(103));
        Check(e.PredictedDamage.Count() == 2 && e.PredictedDamage.All(p => p.Activation == Start.AddSeconds(107.3).AddSeconds(1.2d)),
            "Component-wide activation became per-caster queue.");
    }
    private static void SweepAdvance()
    {
        var e = Create(0x3014);
        e.StartCast(2, Cast(22937, location: Center + Vector3.UnitX * 30), Start);
        var aoes = Sweeps(e);
        Check(aoes.Length == 3 && aoes.All(a => a.Navigate && a.Display && a.Shape is CircleTemplate { Radius: 6 })
            && aoes.Last().Id.EndsWith("/0") && aoes.Last().Color == new Vector4(1, 0, 0, 1)
            && Math.Abs((aoes.Last().Activation!.Value - Start).TotalSeconds - 5.2) < .00001
            && aoes.All(a => a.Position.X < 101), "Sweep did not use source lock/three risk circles/extra .9.");
        e.TreasureEffect(1, 22937, 1, 0, Start.AddSeconds(5));
        e.Effect(1, 21672, 1, Start.AddSeconds(5));
        Check(Sweeps(e).Last().Position.Z < 101, "Start action or expanded event advanced Sweep.");
        e.TreasureEffect(1, 21672, 1, 0, Start.AddSeconds(6));
        Check(Sweeps(e).Length == 3 && Sweeps(e).Last().Activation == Start.AddSeconds(10.5)
            && Sweeps(e).Last().Position.Z > 111, "Native current source position failed first advance.");
        for (var i = 1; i < 4; ++i)
        {
            e.Synchronize([Enemy(1, 0x3014, Center + Vector3.UnitZ * (12 * i)), Player(), Player(101)],
                Center, Start.AddSeconds(6 + i), playerId: 100);
            e.TreasureEffect(1, 21672, (uint)(i + 1), 0, Start.AddSeconds(6 + i));
            Check(Sweeps(e).Length == Math.Min(3, 3 - i), "Four effects failed remaining count/window.");
        }
    }
    private static void SweepMatching()
    {
        var e = Create(0x3014);
        e.StartCast(2, Cast(22937), Start);
        e.StartCast(3, Cast(22937), Start);
        Finish(e, 2, 22937, Start.AddSeconds(1));
        Finish(e, 3, 22937, Start.AddSeconds(1));
        e.Synchronize([Enemy(1, 0x3014), Enemy(4, 0x233C, Center + new Vector3(.9f, 0, .9f)), Player()],
            Center, Start.AddSeconds(2), playerId: 100);
        e.TreasureEffect(4, 21672, 1, 0, Start.AddSeconds(2));
        Check(Sweeps(e).Count(a => a.Id.EndsWith("/0") && a.Position.Z < 101) == 1
            && Sweeps(e).Any(a => a.Id.EndsWith("/0") && a.Position.Z > 112),
            "Square tolerance, actual impact correction or first-line/source-independent match differs.");
        var before = Sweeps(e).ToArray();
        e.TreasureEffect(4, 21672, 1, 0, Start.AddSeconds(3));
        Check(Sweeps(e).SequenceEqual(before), "Duplicate native packet consumed second line.");
    }
    private static void SweepLifetime()
    {
        var e = Create(0x3014);
        e.StartCast(2, Cast(22937), Start);
        Finish(e, 2, 22937, Start.AddSeconds(1));
        e.RemoveActor(2, Start.AddSeconds(2));
        e.Tick(Start.AddSeconds(100));
        Check(Sweeps(e).Length == 3 && e.Hints(Center).Contains("GTFO from exaflare!"), "Cancel/destroy/timeout removed Sweep.");
        e.ResetFight();
        Check(Sweeps(e).Length == 0 && !e.PredictedDamage.Any(), "Phase reset retained sequence outputs.");
    }
    private static void SweepCache()
    {
        var e = Create(0x3014);
        e.StartCast(2, Cast(22937), Start);
        var future = Sweeps(e).First().Activation;
        e.Tick(Start.AddSeconds(100));
        Check(Sweeps(e).First().Activation == future, "No line version change slid future activation.");
        e.StartCast(3, Cast(22937, 100), Start.AddSeconds(100));
        Check(Sweeps(e).First().Activation == Start.AddSeconds(104.5), "Line addition did not clamp overdue future cache.");
        var count = Sweeps(e).Length;
        e.StartCast(3, Cast(22937, 100, rotation: .1f), Start.AddSeconds(100.1));
        Check(Sweeps(e).Length == count, "Same-generation correction added another Sweep.");
    }
    private static void Groups()
    {
        foreach (var oid in new uint[] { 0x302D, 0x3014 })
        {
            var e = Create(oid);
            var keeper = Enemy(20, 0x3034);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, keeper, Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null, "Reward failed to hold group phase.");
            e.Synchronize([keeper with { Dead = true }, Enemy(21, 0x3033), Enemy(22, 0x30F3), Player()],
                Center, Start.AddSeconds(2), playerId: 100);
            Check((e.Active != null) == (oid == 0x302D), "Porxie included Fuath/brooms or Basket excluded Fuath.");
        }
        Check(NativeTreasureCatalog.Priority(0x302D, 0x3033) == 3 && NativeTreasureCatalog.Priority(0x302D, 0x302E) == 1
            && NativeTreasureCatalog.Priority(0x3014, 0x3033) == 0 && !NativeTreasureCatalog.Vulnerable(0x3014, 0x3033)
            && NativeTreasureCatalog.Priority(0x3014, 0x301D) == 5, "Reward priority/color boundary differs.");
    }
    private static void EarthquakeCorrection()
    {
        var e = Create(0x302D);
        var first = Cast(21729);
        e.StartCast(2, first, Start);
        e.StartCast(3, Cast(21729, 1), Start.AddSeconds(1));
        var activation = e.PredictedDamage.First().Activation;
        e.StartCast(2, first with { Location = Center + Vector3.UnitX }, Start.AddSeconds(2));
        Check(e.PredictedDamage.All(p => p.Activation == activation), "Older source correction reclaimed global activation.");
        e.TreasureEffect(99, 21730, 1, 0, Start.AddSeconds(3));
        e.StartCast(3, Cast(21729, 1, Center + Vector3.UnitX), Start.AddSeconds(4));
        Check(!e.PredictedDamage.Any(), "Old current-source correction resurrected consumed earthquake.");
    }
    private static void LateFinish()
    {
        foreach (var remove in new[] { false, true })
        {
            var e = Create(0x302D);
            e.StartCast(2, Cast(21724), Start);
            var adapter = new DungeonEventAdapter(e, () => { }, c => c.SourceRotation, _ => { });
            LogSystemEvent ev = remove
                ? new LogSystemObjectRemoveEvent { SourceId = 2, Timestamp = Start.AddSeconds(4) }
                : new LogSystemCancelCastEvent { SourceId = 2, ActionId = 21724, Timestamp = Start.AddSeconds(4) };
            adapter.Handle(ev, Start.AddSeconds(10));
            Check(Strikes(e).Single().Activation == Start.AddSeconds(10).AddSeconds(1.1d),
                "Queued Finish used old event timestamp instead of processing clock.");
        }
    }
    private static void SweepOriginCorrection()
    {
        var e = Create(0x3014);
        var original = Center + Vector3.UnitX * 2;
        var corrected = Center + Vector3.UnitX * 5;
        var cast = Cast(22937) with { SourcePosition = original };
        e.StartCast(2, cast, Start);
        e.StartCast(2, cast with { SourcePosition = corrected }, Start.AddSeconds(.1));
        Check(Sweeps(e).Last().Position.X > 104, "Only-SourcePosition packet correction was discarded.");
        e.Synchronize([Enemy(1, 0x3014), Enemy(2, 0x233C, Center + Vector3.UnitX * 15) with { Cast = cast }, Player()],
            Center, Start.AddSeconds(.2), playerId: 100);
        e.StartCast(2, cast with { SourcePosition = null, Rotation = .1f }, Start.AddSeconds(.3));
        Check(Sweeps(e).Length == 3 && Sweeps(e).Last().Position.X is > 104 and < 106,
            "Missing-origin packet correction replaced accepted source lock with current moving position.");
    }
}
