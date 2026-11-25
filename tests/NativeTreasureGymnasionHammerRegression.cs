using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static class NativeTreasureGymnasionHammerRegression
{
    private static readonly DateTime Start = new(2026, 11, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Vector3 Center = new(100, 0, 100);
    private static DungeonActor Enemy(ulong id, uint oid = 0x233C, Vector3? position = null) =>
        new(id, oid, position ?? Center, InCombat: true, IsEnemy: true);
    private static DungeonActor Player(ulong id = 100) => new(id, 0, Center + Vector3.UnitZ * 8, Player: true, Hostile: false);
    private static DungeonCastObservation Cast(uint action, double duration = 5, float rotation = .7f,
        ulong target = 100, Vector3? location = null) => new(action, location ?? Center, rotation, Start,
            Start.AddSeconds(duration), target);
    private static DungeonEncounter Create(uint oid)
    {
        var e = new DungeonEncounter(1123) { TreasurePartyInput = [100, 101] };
        e.Synchronize([Enemy(1, oid), Enemy(2), Enemy(3, position: Center + new Vector3(3.12345f, 0, 4.98765f)),
            Enemy(4), Player(), Player(101)], Center, Start, playerId: 100);
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
        run("native treasure gymnasion hammers full rules parameters and inactive boundary", Rules);
        run("native treasure gymnasion double hammer absolute offsets clocks first risk all previews", Double);
        run("native treasure gymnasion double hammer global Finish FIFO ignores source and expiry", DoubleFinish);
        run("native treasure gymnasion quadruple pair orders zero facing raw actor origin", Quad);
        run("native treasure gymnasion double and quadruple independent concurrent Finish progression", Concurrent);
        run("native treasure gymnasion hammers corrections preserve consumed pairing and missing zero", Corrections);
        run("native treasure gymnasion Hippomenes four orderings relative rotation independent clocks", BlasterOrders);
        run("native treasure gymnasion Hippomenes native effect FIFO ignores Finish destroy timer", BlasterEffects);
        run("native treasure gymnasion Hippomenes first only navigation and center5 until last", BlasterNavigation);
        run("native treasure gymnasion hammers All rewards predictions and radius6 spread", Rewards);
    }
    private static void Rules()
    {
        foreach (var (oid, count) in new[] { (0x3D3Eu, 19), (0x3D49u, 22) })
        {
            var e = Create(oid);
            Check(e.Active!.Casts.Length == count && NativeTreasureCatalog.Owns(oid, 1123)
                && !NativeTreasureCatalog.Owns(oid, 924), "Full active rules/territory differ.");
        }
        var rules = Create(0x3D3E).Active!.Casts.ToDictionary(c => c.Action);
        Check(rules[32279].Shape is CircleTemplate { Radius: 13 }
            && rules[32286].Shape is FanTemplate { Radius: 10 }
            && !rules.ContainsKey(32282) && !rules.ContainsKey(32277), "Inactive visuals/ordinary shape differ.");
        Check(!Create(0x3D49).Active!.Casts.Any(c => c.Action == 32298), "Inactive Hypnotize claimed.");
    }
    private static void Double()
    {
        foreach (var action in new[] { 32284u, 32281u })
        {
            var e = Create(0x3D3E);
            var cast = Cast(action, rotation: 2);
            e.StartCast(2, cast, Start);
            var cones = e.TreasureDoubleHammers;
            Check(cones.Count == 2 && cones[0].Origin == Center
                && cones[0].Activation == cast.FinishAt.AddSeconds((double).8f)
                && cones[1].Activation == cast.FinishAt.AddSeconds((double)3.7f), "Double hammer lock clocks differ.");
            Check(MathF.Abs(cones[0].Rotation - (action == 32284 ? -90.004f : 89.999f) * MathF.PI / 180) < 1e-6f,
                "Double hammer unexpectedly relative to caster rotation.");
            var aoes = Find(e, "treasure/double-hammer/");
            Check(aoes.Length == 2 && aoes[0].Navigate && !aoes[1].Navigate
                && aoes[0].Color == new Vector4(1, 0, 0, 1), "Future double hammer risky/color differs.");
        }
    }
    private static void DoubleFinish()
    {
        var e = Create(0x3D3E);
        e.StartCast(2, Cast(32284), Start);
        e.StartCast(3, Cast(32281, duration: 9), Start);
        e.StartCast(4, Cast(32860), Start);
        e.Tick(Start.AddSeconds(20));
        e.Finish(4, 32860, Start.AddSeconds(20));
        Check(e.TreasureDoubleHammers.Count == 3 && e.TreasureDoubleHammers[0].Cast.Source == 2
            && e.TreasureDoubleHammers[0].Order == 1, "Unrelated helper Finish not global FIFO.");
        e.Finish(2, 32284, Start.AddSeconds(21));
        Check(e.TreasureDoubleHammers.Count == 3, "Visual Finish cleared queued strikes.");
    }
    private static void Quad()
    {
        foreach (var firstRotation in new[] { false, true })
        {
            var e = Create(0x3D3E);
            e.StartCast(2, Cast(firstRotation ? 32858u : 32283u, duration: 7), Start);
            e.StartCast(3, Cast(firstRotation ? 32280u : 32858u, duration: 9), Start);
            var sequence = e.TreasureRotations.Single();
            Check(sequence.Origin == Enemy(3, position: Center + new Vector3(3.12345f, 0, 4.98765f)).Position
                && sequence.NextActivation == Start.AddSeconds(9).AddSeconds(.8d)
                && sequence.Remaining == 4 && sequence.Interval == (double)3.3f
                && (sequence.Increment < 0) == firstRotation, "Quad pair origin/last callback/clock/sign differ.");
        }
        var zero = Create(0x3D3E);
        zero.StartCast(2, Cast(32283), Start);
        zero.StartCast(3, Cast(32858, rotation: 0), Start);
        Check(!zero.TreasureRotations.Any(), "Quad zero sentinel ignored.");
    }
    private static void Concurrent()
    {
        var e = Create(0x3D3E);
        e.StartCast(1, Cast(32284), Start);
        e.StartCast(2, Cast(32283), Start);
        e.StartCast(3, Cast(32858), Start);
        e.Tick(Start.AddSeconds(10));
        e.StartCast(4, Cast(32860), Start.AddSeconds(10));
        e.Finish(4, 32860, Start.AddSeconds(10));
        Check(e.TreasureDoubleHammers.Count == 1 && e.TreasureRotations.Single().Remaining == 3
            && e.TreasureRotations.Single().NextActivation == Start.AddSeconds(10).AddSeconds((double)3.3f),
            "Shared Finish did not independently advance both components on consumption clock.");
        e.RemoveActor(3, Start.AddSeconds(11));
        Check(e.TreasureRotations.Single().Remaining == 2 && e.TreasureDoubleHammers.Count == 1,
            "Active first hammer destruction did not invoke real Finish independently.");
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureRotations.Single().Remaining == 2, "Quad expired by timer.");
    }
    private static void Corrections()
    {
        var e = Create(0x3D3E);
        e.StartCast(2, Cast(32283), Start);
        e.StartCast(3, Cast(32858, rotation: 0), Start);
        e.StartCast(3, Cast(32858, rotation: .8f), Start.AddSeconds(.1));
        Check(e.TreasureRotations.Count == 1 && e.TreasureRotations[0].Rotation == .8f,
            "Quad zero-to-valid pending correction failed.");
        e.StartCast(3, Cast(32858, rotation: .9f), Start.AddSeconds(.2));
        e.StartCast(2, Cast(32283) with { Location = Center + Vector3.UnitX }, Start.AddSeconds(.3));
        Check(e.TreasureRotations.Count == 1, "Consumed quad correction resurrected pair.");
        var hippo = Create(0x3D49);
        hippo.StartCast(2, Cast(33164), Start);
        hippo.TreasureEffect(4, 32345, 1, 100, Start.AddSeconds(1));
        hippo.StartCast(2, Cast(33164, rotation: 1), Start.AddSeconds(1.1));
        Check(hippo.TreasureBlasters.Count == 1 && hippo.TreasureBlasters[0].Order == 1
            && hippo.TreasureBlasters[0].Rotation == 1 + MathF.PI, "Blaster correction revived consumed head.");
    }
    private static void BlasterOrders()
    {
        foreach (var (action, offsets) in new[]
        {
            (33164u, new[] { 0f, MathF.PI }),
            (32342u, new[] { 0f, MathF.PI, MathF.PI / 2, -MathF.PI / 2 }),
            (32343u, new[] { 0f, -MathF.PI / 2, MathF.PI, MathF.PI / 2 }),
            (32344u, new[] { 0f, MathF.PI / 2, -MathF.PI / 2, MathF.PI }),
        })
        {
            var e = Create(0x3D49);
            e.StartCast(2, Cast(action), Start);
            Check(e.TreasureBlasters.Count == offsets.Length, "Charge order length differs.");
            for (var i = 0; i < offsets.Length; ++i)
                Check(e.TreasureBlasters[i].Rotation == .7f + offsets[i]
                    && e.TreasureBlasters[i].Activation == Start.AddSeconds(5).AddSeconds(6f + 3f * i),
                    "Charge order angle/time differs.");
        }
    }
    private static void BlasterEffects()
    {
        var e = Create(0x3D49);
        e.StartCast(2, Cast(33164), Start);
        e.StartCast(3, Cast(32342), Start);
        e.Finish(2, 33164, Start.AddSeconds(1));
        e.RemoveActor(3);
        e.Tick(Start.AddSeconds(100));
        Check(e.TreasureBlasters.Count == 6, "Visual Finish/destroy/timer cleared blaster.");
        foreach (var (action, index) in new uint[] { 32345, 33165, 32346, 32348, 32349, 32347 }.Select((a, i) => (a, i)))
        {
            e.TreasureEffect(4, action, (uint)index + 1, 100, Start.AddSeconds(101 + index));
            Check(e.TreasureBlasters.Count == 5 - index, "Native effect not global FIFO.");
        }
    }
    private static void BlasterNavigation()
    {
        var e = Create(0x3D49);
        e.StartCast(2, Cast(33164), Start);
        var aoes = Find(e, "treasure/blaster/");
        Check(aoes.Length == 2 && aoes.Count(a => a.Display) == 1 && aoes.All(a => a.Navigate)
            && aoes[0].Shape is FanTemplate { Radius: 30 } && aoes[1].Activation == aoes[0].Activation,
            "Blaster head-only output/center clock differ.");
        var center = aoes[1].Shape.Resolve(aoes[1].Position, 0, null);
        Check(!center.Contains(Center, 0) && center.Contains(Center + Vector3.UnitX * 6, 0), "Center restriction is not outside5.");
        e.TreasureEffect(3, 32345, 1, 100);
        Check(Find(e, "treasure/blaster/").Length == 1, "Last blaster retained center restriction.");
    }
    private static void Rewards()
    {
        foreach (var (oid, add) in new[] { (0x3D3Eu, 0x3D3Fu), (0x3D49u, 0x3D4Fu) })
        {
            var e = Create(oid);
            e.Synchronize([Enemy(1, oid) with { Dead = true }, Enemy(2, add), Player()], Center, Start.AddSeconds(1), playerId: 100);
            Check(e.Active != null && Find(e, "treasure/enemy/").Length == 1, "All/reward phase differs.");
            e.Synchronize([Enemy(4, 0x3D4A), Player()], Center, Start.AddSeconds(2), playerId: 100);
            Check(e.Active == null, "Levin/helper incorrectly preserved stage.");
        }
        Check(NativeTreasureCatalog.Priority(0x3D3E, 0x3D4F) == 7 && NativeTreasureCatalog.Priority(0x3D49, 0x3D4F) == 5
            && NativeTreasureCatalog.Priority(0x3D49, 0x3D4D) == 1, "Source reward order differs.");
        var hippo = Create(0x3D49);
        hippo.StartCast(2, Cast(32354, target: 101), Start);
        Check(Find(hippo, "quest/spread/").Single(a => a.Display).Shape is CircleTemplate { Radius: 6 }
            && Find(hippo, "quest/spread/").Single(a => a.Navigate).Shape is CircleTemplate { Radius: 7 }
            && !hippo.PredictedDamage.Any(), "Hippomenes spread6/7/no forecast differs.");
        hippo.StartCast(3, Cast(32356), Start);
        hippo.StartCast(4, Cast(32341), Start);
        Check(hippo.PredictedDamage.Count() == 3, "Raidwide/current-target prediction missing.");
    }
}
