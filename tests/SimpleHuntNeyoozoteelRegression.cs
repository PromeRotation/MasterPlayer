using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntNeyoozoteelRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P1", 101)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x4233, Kind = 2, Hp = 1000, InCombat = true, Targetable = true, Position = new(7, 0, 9) };
    private static ReplayActorState Player(ulong id = 100) => new() { Id = id, Position = new(30, 0, 0), Hp = 1000 };
    private static ReplayActorState Helper(ulong id = 999, ReplayCast? cast = null) => new()
        { Id = id, Oid = 0x233C, Kind = 2, Cast = cast, Position = new(50, 0, 60) };
    private static ReplayCast Cast(uint action, double at = 0, float rotation = .2f)
        => new(action, 101, new(10, 0, 20), rotation, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Helper(998), Player(), Player(101)], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Sap(SimpleHuntEncounter e) => e.Areas.Where(a => a.SequenceId?.Contains("/sap/") == true).ToArray();
    private static HuntPendingCast[] Roots(SimpleHuntEncounter e) => e.Areas.Where(a => a.SequenceId?.Contains("/roots/") == true).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Neyoozoteel four omen exact float clocks primary lock and full risky queue", Omens);
        run("SimpleHunts Neyoozoteel overlapping whole queue indexing consumed correction and effect-only lifetime", Overlap);
        run("SimpleHunts Neyoozoteel eight rotating crosses real effect FIFO cached deadlines and geometry", Rotations);
        run("SimpleHunts Neyoozoteel helper stack target FIFO owner replay ordinary casts and cleanup", Lifecycle);
        run("SimpleHunts Neyoozoteel owner activation with simultaneous helper finish and immediate final owner revoke", OwnerTransitions);
    }

    private static void Omens()
    {
        (uint Action, float[] Angles)[] cases =
        [
            (37377, [MathF.PI, -MathF.PI / 2, -MathF.PI / 2]),
            (37376, [MathF.PI / 2, MathF.PI, -MathF.PI / 2]),
            (37378, [-MathF.PI / 2, MathF.PI / 2, MathF.PI]),
            (37379, [MathF.PI / 2, MathF.PI, MathF.PI / 2])
        ];
        foreach (var (action, offsets) in cases)
        {
            var e = Create();
            var cast = Cast(action);
            e.Start(999, cast, false);
            var aoes = Sap(e);
            var angle = cast.Rotation!.Value;
            Check(aoes.Length == 3 && aoes.All(a => a.Risky && a.Location == Boss().Position && a.Source == 1),
                "Omen lost queue entries, risky flags or owner-locked origin.");
            for (var i = 0; i < 3; ++i)
            {
                angle += offsets[i];
                Check(aoes[i].Rotation == angle && aoes[i].Activation == cast.FinishAt.AddSeconds(14.6f + 2.2f * i)
                    && aoes[i].Shape is FanTemplate { Radius: 40 } cone && cone.AngleRadians == 2 * MathF.PI / 3,
                    "Omen changed its direction chain, exact float timing or R40/120 geometry.");
            }
            e.Finish(999, action, Start.AddSeconds(6));
            e.Effect(1, 37308, Start.AddSeconds(7), 1);
            e.Start(1, Cast(37397, 8), false);
            Check(Sap(e).Length == 3, "Visual, ordinary Sap effect or Finish consumed/created prediction.");
            e.Effect(998, 42174, Start.AddSeconds(9), 2);
            e.Effect(998, 42174, Start.AddSeconds(9.01), 2);
            Check(Sap(e).Length == 2, "Helper effect was missed or consumed twice.");
        }
    }

    private static void Overlap()
    {
        foreach (var count in new[] { 1, 2, 3 })
        {
            var e = Create();
            e.Start(999, Cast(37377), false);
            for (var i = 3; i > count; --i) e.Effect(1, 37370, Start.AddSeconds(i), (uint)i);
            var prior = Sap(e);
            var cast = Cast(37379, 1, 1.1f);
            e.Start(998, cast, false);
            var added = Sap(e).Skip(count).ToArray();
            var second = prior[0].Rotation + MathF.PI;
            var thirdBase = count == 1 ? added[0].Rotation : prior[1].Rotation;
            Check(added[0].Rotation == cast.Rotation + MathF.PI / 2 && added[1].Rotation == second
                && added[2].Rotation == thirdBase + MathF.PI / 2, "Overlapping Omen used a local chain instead of whole-queue indexes.");
            var corrected = cast with { Rotation = 1.6f, FinishAt = Start.AddSeconds(20), Location = new(80, 0, 80) };
            e.Start(998, corrected, true);
            var fixedItems = Sap(e).Skip(count).ToArray();
            Check(fixedItems[0].Rotation == added[0].Rotation + .5f && fixedItems[1].Rotation == added[1].Rotation
                && fixedItems[2].Rotation == added[2].Rotation + (count == 1 ? .5f : 0)
                && fixedItems.All(a => a.Location == Boss().Position), "Same-generation correction changed frozen dependencies or primary origin.");
            e.Remove(998, Start.AddSeconds(2));
            e.Synchronize([Boss(), Player(), Player(101)], _ => true, Start.AddSeconds(200), Party);
            Check(Sap(e).Length == count + 3, "Caster destruction or clock expiry consumed the effect-only queue.");
        }
        var consumed = Create();
        consumed.Start(999, Cast(37378), false);
        consumed.Effect(1, 37370, Start.AddSeconds(1), 1);
        consumed.Start(999, Cast(37378) with { FinishAt = Start.AddSeconds(30) }, true);
        Check(Sap(consumed).Length == 2 && Sap(consumed)[0].Stage == 1, "Packet correction resurrected a consumed stage.");
        consumed.Start(999, Cast(37378, rotation: .8f), false);
        Check(Sap(consumed).Length == 5, "A real raw restart failed to append a new Omen generation.");
    }

    private static void Rotations()
    {
        foreach (var action in new uint[] { 37373, 37374 })
        {
            var e = Create();
            var cast = Cast(action);
            e.Start(999, cast, false);
            e.UpdateComponents(Start);
            var roots = Roots(e);
            var increment = action == 37373 ? -MathF.PI / 4 : MathF.PI / 4;
            Check(roots.Length == 2 && roots.All(a => a.Risky && a.Location == cast.Location)
                && roots[0].Rotation == cast.Rotation + increment && roots[1].Rotation == cast.Rotation
                && roots[0].Activation == cast.FinishAt.AddSeconds(2.4f), "Roots initial cache, order or float interval differs.");
            var shape = roots[1].Shape!.Resolve(Vector3.Zero, 0, null);
            Check(shape.Contains(new(0, 0, 29), 0) && shape.Contains(new(29, 0, 0), 0)
                && !shape.Contains(new(4, 0, 4), 0) && !shape.Contains(new(0, 0, 31), 0),
                "Roots is not a half-arm30 half-width3 cross.");
            e.UpdateComponents(Start.AddSeconds(100));
            Check(Roots(e)[0].Activation == roots[0].Activation, "Idle Update drifted the cached future activation.");
            e.Finish(999, action, Start.AddSeconds(6));
            e.UpdateComponents(Start.AddSeconds(6));
            Check(Roots(e)[1].Rotation == cast.Rotation, "Finish advanced an effect-driven root.");
            var rotation = cast.Rotation!.Value;
            for (var hit = 0; hit < 8; ++hit)
            {
                var at = Start.AddSeconds(10 + hit * 3);
                e.Effect(998, 37375, at, (uint)(hit + 1));
                e.Effect(998, 37375, at.AddMilliseconds(1), (uint)(hit + 1));
                e.UpdateComponents(at);
                rotation += increment;
                var remaining = Roots(e);
                Check(hit == 7 ? remaining.Length == 0 : remaining.Last().Rotation == rotation
                    && remaining.Last().Activation == at.AddSeconds(2.4f), "Roots failed exact eight-hit FIFO or effect deduplication.");
            }
        }
        var multi = Create();
        multi.Start(999, Cast(37373), false);
        multi.Start(998, Cast(37374, 1), false);
        multi.UpdateComponents(Start);
        multi.Effect(998, 37374, Start.AddSeconds(9), 1);
        multi.UpdateComponents(Start.AddSeconds(9));
        Check(Roots(multi)[1].Stage == 1 && Roots(multi)[3].Stage == 0,
            "Roots matched caster instead of consuming sequence index zero.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        var first = Cast(37307);
        e.Synchronize([Boss(), Helper(998, first), Helper(999, Cast(37307, 1)), Player(), Player(101)], _ => true, Start, Party);
        Check(e.TargetPreviews(Player()).Count() == 2 && e.TargetPreviews(Player()).All(p => p.Shape is CircleTemplate { Radius: 5 })
            && e.TargetDangers(Player(), Party).Count() == 1 && !e.PredictedDamage(Party).Any(), "Cocopult lost R5 union or invented Shared.");
        var secondOrder = e.Casts.Single(c => c.Source == 999).Order;
        e.Finish(999, 37307, Start.AddSeconds(2), 101);
        Check(e.TargetPreviews(Player()).Single().Order == secondOrder, "Cocopult Finish was bucketed by caster.");
        e.Synchronize([Boss(), Player(), Player(101)], _ => true, Start.AddSeconds(3), Party);
        Check(!e.TargetPreviews(Player()).Any(), "Destroyed helper retained a Cocopult stack.");
        e.Reset();
        var helper = Helper(cast: Cast(37377));
        e.Synchronize([Boss(), helper, Player(), Player(101)], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2) with { Position = new(15, 0, 17) }, helper, Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(Sap(e).Length == 6 && Sap(e).Count(a => a.Location == new Vector3(15, 0, 17)) == 3,
            "New owner did not replay current Omen with its own primary origin.");
        e.Remove(1);
        Check(Sap(e).Length == 3 && Sap(e).All(a => a.Source == 2), "Owner removal lost another component.");
        e.Start(2, Cast(37308, 2), false);
        Check(e.Areas.Single(a => a.SequenceId == null && a.Shape != null).Shape is FanTemplate { Radius: 40 }, "Ordinary Sap used stale R30 comment.");
        e.Start(2, Cast(38331, 3), false);
        Check(e.PredictedDamage(Party).Count() == 2, "Neurotoxify lost raidwide prediction.");
        e.Reset();
        Check(!e.Active && !e.Areas.Any() && !e.TargetPreviews(Player()).Any(), "Reset retained Neyoozoteel output.");
    }

    private static void OwnerTransitions()
    {
        foreach (var action in new uint[] { 37377, 37373, 37307 })
        {
            var e = Create();
            var cast = Cast(action);
            e.Synchronize([Boss(), Helper(cast: cast), Player(), Player(101)], _ => true, Start, Party);
            e.Synchronize([Boss(), Boss(2), Helper(), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
            Check(Sap(e).All(a => a.Source == 1) && Roots(e).All(a => a.Source == 1)
                && !e.TargetPreviews(Player()).Any(), "New owner replayed a helper cast already finished in the same frame.");
            if (action == 37377) Check(Sap(e).Length == 3, "Old owner lost valid effect-only history.");
            if (action == 37373) Check(Roots(e).Length == 2, "Old owner lost valid rotating history.");
        }
        foreach (var action in new uint[] { 37308, 38331 })
        {
            var e = Create();
            e.Start(999, Cast(action), false);
            e.Remove(1, Start.AddSeconds(1));
            Check(!e.Casts.Any() && !e.Areas.Any() && !e.PredictedDamage(Party).Any(),
                "Explicit last owner removal left helper ordinary AOEs or damage prediction until the next scan.");
        }
    }
}
