using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntFinalTrioRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "T", 101)];
    private static ReplayActorState Boss(uint oid, ulong id = 1) => new() { Id = id, Oid = oid, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Helper(ulong id = 999, ReplayCast? cast = null) => new() { Id = id, Oid = 0x233C, Kind = 2, Hp = 1000, Cast = cast };
    private static ReplayActorState Player(ulong id = 100, Vector3 position = default, params ReplayStatus[] statuses)
        => new() { Id = id, Kind = 1, Hp = 1000, Position = position, Statuses = statuses };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 101, Vector3 location = default, float rotation = .4f)
        => new(action, target, location, rotation, Start.AddSeconds(at), Start.AddSeconds(at + 5), false, false) { TotalTime = 5 };
    private static SimpleHuntEncounter Create(uint oid)
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(oid), Helper(), Helper(998), Helper(997), Player(), Player(101, new(0, 0, 10))], _ => true, Start, Party);
        return e;
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    private static string[] Hints(SimpleHuntEncounter e, ReplayActorState? player = null) => e.Hints(player ?? Player()).ToArray();
    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts final trio exact source rule sets missing facing and ordinary real ends", Rules);
        run("SimpleHunts Arch global Finish FIFO both combo branches precision cancellation retained future", Arch);
        run("SimpleHunts Arch independent Stay Move preparations real resolving Lose and packet expiry", StayMove);
        run("SimpleHunts Arch status raw confirmation death revival and occupied expired slot", StayDeath);
        run("SimpleHunts Sansheya three stages reference risk two views nonexpected effect FIFO", Sansheya);
        run("SimpleHunts Sansheya same-generation correction consumed history helper destruction owner cleanup", Corrections);
        run("SimpleHunts Sansheya dynamic charge target destruction frozen old generation and source Finish", Charge);
        run("SimpleHunts final trio stacks target FIFO death revival no invented Shared and raidwide prediction", Stacks);
        run("SimpleHunts Urna four polarity geometry clocks and independent real finish single slots", Urna);
        run("SimpleHunts Urna dynamic primary static AOE dual charges source immunity override and slot boundaries", Magnetism);
        run("SimpleHunts Urna event raw status receipts preserve current polarity after old Lose", ChargeStatuses);
        run("SimpleHunts final trio late owner raw bootstrap no resurrection and mechanism off", Owners);
        run("SimpleHunts StayMove native slot replacement and cross slot transition order", StatusSlots);
        run("SimpleHunts existing final owners use old party for actor statuses then new party for queries", PartyChanges);
        run("SimpleHunts Sansheya zero sequence effect timestamps and unconfirmed packet source destruction", PacketEnds);
    }
    private static void PacketEnds()
    {
        var e = Create(0x43DD);
        e.Start(999, Cast(39292), true);
        e.Effect(997, 39557, Start.AddSeconds(1));
        e.Effect(997, 39557, Start.AddSeconds(1));
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Same zero-sequence delivery advanced twice.");
        e.Effect(997, 39557, Start.AddSeconds(2));
        Check(e.Areas.Single(c => c.Shape != null).Shape is CircleTemplate, "Later zero-sequence hit was lost.");
        foreach (var action in new[] { 39285u, 39296u })
        {
            e = Create(0x43DD);
            e.Start(999, Cast(action), true);
            e.Remove(999);
            Check(!e.TargetPreviews(Player()).Any() && !e.TargetDangers(Player(), Party).Any() && !e.PredictedDamage(Party).Any(), "Packet source removal left charge/stack.");
            e = Create(0x43DD);
            e.Start(999, Cast(action), true);
            e.Synchronize([Boss(0x43DD), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
            Check(!e.TargetPreviews(Player()).Any() && !e.TargetDangers(Player(), Party).Any() && !e.PredictedDamage(Party).Any(), "Absent packet source left charge/stack.");
        }
    }
    private static void StatusSlots()
    {
        foreach (var (oid, preparation, resolving) in new[] { (0x4544u, 4141u, 960u), (0x4544u, 4142u, 2540u), (0x43DDu, 4140u, 960u) })
        {
            var e = Create(oid);
            var old = new ReplayStatus(resolving, 0, 1, Start.AddSeconds(30)) { SlotIndex = 4 };
            var next = new ReplayStatus(preparation, 0, 1, Start.AddSeconds(20)) { SlotIndex = 4 };
            e.Synchronize([Boss(oid), Player(statuses: [old]), Player(101)], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Player(statuses: [next]), Player(101)], _ => true, Start.AddSeconds(1), Party);
            Check(Hints(e).Any(h => h.Contains("activation=" + next.ExpiresAt.ToString("O"))),
                "Same native slot replacement lost the newly prepared requirement.");
            e.FinalHuntStatus(100, resolving, null, 1, Start.AddSeconds(1));
            e.FinalHuntStatus(100, preparation, next, 1, Start.AddSeconds(1));
            Check(Hints(e).Any(h => h.StartsWith("SpecialMode:")), "Raw confirmation packets replayed an old Lose.");

            e = Create(oid);
            old = old with { SlotIndex = 8 };
            next = next with { SlotIndex = 2 };
            e.Synchronize([Boss(oid), Player(statuses: [old]), Player(101)], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Player(statuses: [next]), Player(101)], _ => true, Start.AddSeconds(1), Party);
            Check(!Hints(e).Any(h => h.StartsWith("SpecialMode:")), "Later native slot Lose did not clear earlier Gain.");
            e = Create(oid);
            next = next with { SlotIndex = 8 };
            e.Synchronize([Boss(oid), Player(statuses: [next]), Player(101)], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Player(statuses: [next with { SlotIndex = 2 }]), Player(101)],
                _ => true, Start.AddSeconds(1), Party);
            Check(Hints(e).Any(h => h.StartsWith("SpecialMode:"))
                && !Hints(e).Any(h => h.StartsWith("Heatstroke") || h.StartsWith("Cold Sweats") || h.StartsWith("Boiling")),
                "Occupied preparation moving to an earlier native slot skipped the later old-slot Lose.");
        }
    }
    private static void PartyChanges()
    {
        foreach (var oid in new[] { 0x4544u, 0x43DDu })
        {
            var e = Create(oid);
            var preparation = oid == 0x4544 ? 4141u : 4140u;
            ReplayStatus[] statuses = [new(preparation, 0, 1, Start.AddSeconds(20)), new(960, 0, 1, Start.AddSeconds(30))];
            e.Synchronize([Boss(oid), Player(statuses: statuses), Player(101), Player(102)], _ => true, Start, Party);
            e.Synchronize([Boss(oid), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(1),
                [new(0, "New", 102), new(1, "T", 101)]);
            Check(!Hints(e, Player(102)).Any(h => h.StartsWith("SpecialMode:") || h.StartsWith("Heatstroke") || h.StartsWith("Boiling")),
                "Old slot requirement leaked to the replacement party member.");
        }
        var urna = Create(0x416F);
        urna.Start(999, Cast(39109), true);
        urna.Synchronize([Boss(0x416F), Helper(cast: Cast(39109)),
            Player(position: new(0, 0, 2), statuses: [new(4071, 0, 1, Start.AddSeconds(20))]), Player(101, new(0, 0, 3))],
            _ => true, Start.AddSeconds(1), [new(0, "T", 101), new(1, "P", 100)]);
        Check(!urna.Knockbacks(Player(position: new(0, 0, 2))).Any()
            && urna.Knockbacks(Player(101, new(0, 0, 3))).Any(),
            "Same-frame polarity Gain used the new roster rather than old slot.");
    }
    private static void Rules()
    {
        Check(SimpleHuntCatalog.Find(0x4544)!.Casts.Length == 20 && SimpleHuntCatalog.Find(0x43DD)!.Casts.Length == 12
            && SimpleHuntCatalog.Find(0x416F)!.Casts.Length == 11, "Source rule counts differ.");
        var e = Create(0x4544);
        e.Start(999, Cast(39829), true);
        e.Finish(999, 39838, Start.AddSeconds(1));
        Check(!e.Areas.Any(c => c.Shape != null), "Simple group foreign Finish retained fire.");
        e.Start(998, Cast(39509, 2) with { Rotation = null }, true);
        Check(!e.Areas.Any(c => c.Shape != null), "Combo guessed packet facing.");
        e.Start(999, Cast(39516, 3), true);
        Check(e.Areas.Single(c => c.Shape != null).Shape is RayRectangleTemplate { Length: 40, MinLocalX: -5, MaxLocalX: 5 },
            "Meltdown exact width differs.");
        e.Finish(999, 39516, Start.AddSeconds(4));
        Check(!e.Areas.Any(c => c.Shape != null), "Ordinary Finish retained Meltdown.");
    }
    private static void Arch()
    {
        var e = Create(0x4544);
        e.Start(999, Cast(39509), true);
        e.Start(998, Cast(39510, 1, location: new(20, 0, 10)), true);
        var q = e.Areas.Where(c => c.Shape != null).ToArray();
        Check(q.Length == 4 && q.All(c => c.Risky) && q[0].Color == new Vector4(1, 1, 0, 1)
            && q[1].Shape is CircleTemplate { Radius: 15 } && q[1].Rotation == 0
            && q[1].Activation == Start.AddSeconds(7.5f) && q[3].Shape is DonutTemplate { InnerRadius: 6, OuterRadius: 40 },
            "Arch shapes, list visibility/risk or F+2.5 differs.");
        e.Finish(998, 39510, Start.AddSeconds(2));
        Check(e.Areas.First(c => c.Shape != null).Shape is CircleTemplate, "Other caster Finish did not consume global first.");
        e.Finish(997, 39514, Start.AddSeconds(3));
        Check(e.Areas.Count(c => c.Shape != null) == 2 && e.Areas.First(c => c.Shape != null).Source == 1,
            "Unknown-start short Finish did not consume one or output lost owner.");
        e.Effect(997, 39514, Start.AddSeconds(4), 2);
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Effect advanced Finish-only queue.");
        e.UpdateComponents(Start.AddDays(1));
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Forecast expiry cleared remaining queue.");
    }
    private static void StayMove()
    {
        var e = Create(0x4544);
        e.FinalHuntStatus(100, 4141, new(4141, 3, 1, Start.AddSeconds(10)), 1, Start);
        e.FinalHuntStatus(100, 4142, new(4142, 0, 1, Start.AddSeconds(20)), 1, Start);
        var hints = Hints(e);
        Check(hints.Any(h => h.Contains("Pyretic activation=" + Start.AddSeconds(10).ToString("O")))
            && hints.Any(h => h.Contains("Freezing activation=" + Start.AddSeconds(20).ToString("O")))
            && !hints.Contains("Stop everything!") && !hints.Contains("Move!"), "Independent packet expiry/mode/window lost.");
        e.FinalHuntStatus(100, 4141, null, 1, Start.AddSeconds(1));
        Check(!Hints(e).Any(h => h.StartsWith("Heatstroke")) && Hints(e).Any(h => h.StartsWith("SpecialMode: Pyretic")),
            "Preparation Lose cleared actual Stay.");
        e.FinalHuntStatus(100, 960, null, 999, Start.AddSeconds(2));
        Check(!Hints(e).Any(h => h.StartsWith("SpecialMode: Pyretic"))
            && Hints(e).Any(h => h.StartsWith("SpecialMode: Freezing")), "960 Lose cleared wrong independent state or required matching source.");
        e.FinalHuntStatus(100, 2540, null, 0, Start.AddSeconds(3));
        Check(!Hints(e).Any(h => h.StartsWith("SpecialMode:")), "Resolving Lose retained Move.");
        e = Create(0x43DD);
        e.FinalHuntStatus(100, 4140, new(4140, 0, 1, Start.AddSeconds(10)), 1, Start);
        Check(Hints(e).Contains("Stop everything!") && !e.StopEverything(Player(), Party, Start),
            "Boiling diagnostic window or unsupported execution boundary differs.");
    }
    private static void StayDeath()
    {
        var e = Create(0x4544);
        var s = new ReplayStatus(4141, 3, 1, Start.AddSeconds(10));
        e.FinalHuntStatus(100, 4141, s, 1, Start);
        e.Synchronize([Boss(0x4544), Player(statuses: [s]), Player(101)], _ => true, Start.AddSeconds(11), Party);
        Check(Hints(e).Contains("Stop everything!"), "Occupied expired status synthesized Lose.");
        _ = Hints(e, Player(statuses: [s]) with { Dead = true });
        e.Synchronize([Boss(0x4544), Player(statuses: [s]), Player(101)], _ => true, Start.AddSeconds(12), Party);
        Check(!Hints(e).Any(h => h.StartsWith("SpecialMode:")), "Raw confirmation revived death-cleared state.");
        e = Create(0x4544);
        e.FinalHuntStatus(100, 960, new(960, 0, 1, Start.AddSeconds(30)), 1, Start);
        Check(!Hints(e).Any(h => h.StartsWith("SpecialMode:")), "Resolving status Gain invented a requirement.");
    }
    private static void Sansheya()
    {
        var e = Create(0x43DD);
        e.Start(999, Cast(39292, location: new(10, 0, 20), rotation: MathF.PI / 2), true);
        var q = e.Areas.Where(c => c.Shape != null).ToArray();
        Check(q.Length == 2 && q[0].Risky && !q[1].Risky && q[0].Rotation == MathF.PI / 2
            && q[1].Rotation == MathF.PI / 2 + MathF.PI && q[1].Activation == Start.AddSeconds(5).AddSeconds(2.3f),
            "Sansheya stage direction, exact float timing or reference risk differs.");
        e.Finish(999, 39292, Start.AddSeconds(1));
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Visual Finish cleared effect sequence.");
        e.Effect(997, 39557, Start.AddSeconds(2), 1);
        q = e.Areas.Where(c => c.Shape != null).ToArray();
        Check(q.Length == 2 && q.All(c => c.Risky) && q[1].Shape is CircleTemplate { Radius: 15 }
            && q[1].Activation == Start.AddSeconds(9.5f), "Future circle failed Risky promotion.");
        e.Effect(997, 39557, Start.AddSeconds(2.1), 1);
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Duplicate effect advanced twice.");
        e.Effect(998, 39294, Start.AddSeconds(3), 2);
        Check(e.Areas.Single(c => c.Shape != null).Shape is CircleTemplate, "Nonexpected action did not consume FIFO.");
    }
    private static void Corrections()
    {
        var e = Create(0x43DD);
        var cast = Cast(39289);
        e.Start(999, cast, true);
        e.Effect(997, 39528, Start.AddSeconds(1), 1);
        e.Start(999, cast with { Location = new(20, 0, 10), Rotation = .9f }, true);
        var q = e.Areas.Where(c => c.Shape != null).ToArray();
        Check(q.Length == 2 && q[0].Stage == 1 && q[1].Stage == 2 && q.All(c => c.Location == new Vector3(20, 0, 10))
            && q[0].Rotation == .9f + MathF.PI, "Correction revived consumed stage or lost remaining pose.");
        e.Remove(999, Start.AddSeconds(2));
        e.UpdateComponents(Start.AddDays(1));
        Check(e.Areas.Count(c => c.Shape != null) == 2, "Helper destruction/expiry cleared visual queue.");
        e.Remove(1);
        Check(!e.Areas.Any(c => c.Shape != null), "Owner destruction retained state.");
    }
    private static void Charge()
    {
        var e = Create(0x43DD);
        e.Start(999, Cast(39285), false);
        e.Synchronize([Boss(0x43DD), Helper() with { Position = new(2, 0, 0), Cast = Cast(39285) },
            Helper(998), Player(), Player(101, new(12, 0, 0))], _ => true, Start.AddSeconds(1), Party);
        var bait = e.TargetPreviews(Player()).Single();
        Check(bait.Shape is RayRectangleTemplate { Length: 10, MinLocalX: -3, MaxLocalX: 3 }
            && bait.Location == new Vector3(2, 0, 0) && bait.Rotation == MathF.PI / 2, "Dynamic charge used locked packet geometry.");
        e.Remove(101);
        e.Synchronize([Boss(0x43DD), Helper(cast: Cast(39285)), Helper(998), Player(), Player(101, new(100, 0, 100))],
            _ => true, Start.AddSeconds(2), Party);
        Check(e.TargetPreviews(Player()).Single().Shape is RayRectangleTemplate { Length: 12 },
            "Destroyed target rebound to same-ID replacement.");
        e.Finish(998, 39285, Start.AddSeconds(3));
        Check(e.TargetPreviews(Player()).Any(), "Foreign source Finish consumed charge.");
        e.Finish(999, 39285, Start.AddSeconds(4));
        Check(!e.TargetPreviews(Player()).Any(), "Source Finish retained charge.");
    }
    private static void Stacks()
    {
        foreach (var (oid, action, raidwide) in new[] { (0x4544u, 39515u, 39840u), (0x43DDu, 39296u, 39295u), (0x416Fu, 39115u, 39108u) })
        {
            var e = Create(oid);
            e.Start(999, Cast(action), true);
            e.Start(998, Cast(action, 1), true);
            Check(e.TargetPreviews(Player()).Count() == 2 && e.TargetDangers(Player(), Party).Any()
                && !e.PredictedDamage(Party).Any(), "Stack list/navigation or empty-mask prediction differs.");
            e.Finish(997, action, Start.AddSeconds(2), 101);
            Check(e.TargetPreviews(Player()).Count() == 1, "Finish did not remove first target-matching stack.");
            e.Start(997, Cast(raidwide, 3), true);
            Check(e.PredictedDamage(Party).Count(p => p.DamageType == "Raidwide") == 2, "Raidwide prediction missing.");
        }
    }
    private static void Urna()
    {
        foreach (var (action, pull, ring, radius) in new[] { (39109u, false, false, 10f), (39111u, true, false, 30f),
            (39110u, false, true, 1f), (39112u, true, true, 18f) })
        {
            var e = Create(0x416F);
            e.FinalHuntStatus(100, 4071, new(4071, 0, 1, Start), 1, Start);
            e.Start(999, Cast(action, location: new(30, 0, 0)), true);
            var aoe = e.Areas.Single(c => c.Shape != null);
            Check(aoe.Activation == Start.AddSeconds(7.5) && aoe.Location == new Vector3(30, 0, 0)
                && (ring ? aoe.Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 60 } : aoe.Shape is CircleTemplate { Radius: 20 }),
                "Magnetic AOE shape or independent clock differs.");
            Check(!e.NavigationAreas(Player()).Any(c => c.Shape != null), "Ordinary circle navigation not suppressed for charged slot.");
            var nav = e.TargetDangers(Player(), Party).Single();
            Check(ring ? nav.Shape is HuntStackJoinTemplate j && j.Radius == radius : nav.Shape is CircleTemplate c && c.Radius == radius,
                "Polarity dedicated navigation radius differs.");
            var move = e.Knockbacks(Player(position: new(0, 0, 15))).Single();
            Check(move.To == new Vector3(0, 0, pull ? 5 : 25), "Magnetism displacement wrong.");
            e.Finish(997, action, Start.AddSeconds(1));
            Check(!e.Knockbacks(Player(position: new(0, 0, 15))).Any()
                && e.NavigationAreas(Player()).Any(c => c.Shape != null), "Magnetic Finish removed AOE or retained dedicated navigation.");
            e.Start(998, Cast(39113, 2), true);
            Check(e.Areas.Single(c => c.Shape != null).Activation == aoe.Activation, "Short Start recreated circle clock.");
            e.Finish(998, 39114, Start.AddSeconds(3));
            Check(!e.Areas.Any(c => c.Shape != null), "Cross-action ordinary Finish failed global clear.");
        }
    }
    private static void Magnetism()
    {
        var e = Create(0x416F);
        var positive = new ReplayStatus(4071, 0, 1, Start.AddSeconds(30));
        var negative = new ReplayStatus(4072, 0, 1, Start.AddSeconds(30));
        var immune = new ReplayStatus(160, 0, 100, Start.AddSeconds(30));
        e.Synchronize([Boss(0x416F), Helper(), Player(statuses: [positive, negative, immune]), Player(101)], _ => true, Start, Party);
        e.Start(999, Cast(39110, location: new(40, 0, 0)), true);
        e.Synchronize([Boss(0x416F) with { Position = new(10, 0, 0) }, Helper(),
            Player(position: new(10, 0, 15), statuses: [positive, negative, immune]), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(e.Knockbacks(Player(position: new(10, 0, 15), statuses: [immune])).Single().To == new Vector3(10, 0, 25)
            && e.TargetDangers(Player(), Party).Single().Shape is HuntStackJoinTemplate { Radius: 18 },
            "Dual-bit priority or source immunity callback override changed.");
        Check(e.Areas.Single(c => c.Shape != null).Location == new Vector3(40, 0, 0)
            && e.TargetDangers(Player(), Party).Single().Location == new Vector3(10, 0, 0), "Dynamic primary contaminated locked AOE origin.");
        Check(e.NavigationAreas(Player(333)).Any(c => c.Shape != null) && !e.Knockbacks(Player(333)).Any(),
            "Unslotted perspective acquired polarity navigation.");
    }
    private static void ChargeStatuses()
    {
        var e = Create(0x416F);
        var charge = new ReplayStatus(4071, 0, 1, Start.AddSeconds(30));
        e.FinalHuntStatus(100, 4071, charge, 1, Start);
        e.FinalHuntStatus(100, 4071, null, 1, Start.AddSeconds(1));
        e.Synchronize([Boss(0x416F), Helper(), Player(), Player(101)], _ => true, Start.AddSeconds(1.1), Party);
        e.Start(999, Cast(39109, 2), true);
        Check(!e.Knockbacks(Player(position: Vector3.UnitZ * 15)).Any(), "Acknowledged old Lose resurrected polarity.");
        e.FinalHuntStatus(100, 4072, charge with { Id = 4072 }, 1, Start.AddSeconds(3));
        Check(e.Knockbacks(Player(position: Vector3.UnitZ * 15)).Single().To == Vector3.UnitZ * 5,
            "Fresh negative Gain failed.");
    }
    private static void Owners()
    {
        var e = Create(0x43DD);
        var cast = Cast(39601);
        e.Start(999, cast, true);
        e.Effect(997, 39528, Start.AddSeconds(.1), 1);
        e.Effect(997, 39529, Start.AddSeconds(.2), 2);
        e.Synchronize([Boss(0x43DD), Boss(0x43DD, 2), Helper(cast: cast), Player(), Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(e.Areas.Count(c => c.Shape != null && c.Source == 1) == 0
            && e.Areas.Count(c => c.Shape != null && c.Source == 2) == 2, "Raw confirmation resurrected consumed old owner or missed new owner.");
        e.Synchronize([Boss(0x43DD), Boss(0x43DD, 2), Helper(cast: cast), Player(), Player(101)], _ => false, Start.AddSeconds(2), Party);
        Check(!e.Areas.Any(c => c.Shape != null), "Mechanism disabled retained output.");
    }
}
