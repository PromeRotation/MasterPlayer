using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntPedantryRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P1", 101), new(2, "P2", 102)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x298A, Kind = 2, Hp = 1000, InCombat = true, Targetable = true, TargetId = 102 };
    private static ReplayActorState Player(ulong id = 100, Vector3? position = null) => new()
        { Id = id, Hp = 1000, Position = position ?? new(0, 0, 10) };
    private static ReplayActorState Helper(ReplayCast? cast = null) => new()
        { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast, Position = new(20, 0, 0) };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 100)
        => new(action, target, new(10, 0, 20), .2f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player(), Player(101, new(1, 0, 10)), Player(102, new(20, 0, 0))],
            _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Cheeks(SimpleHuntEncounter e) => e.Areas.Where(a => a.SequenceId?.Contains("/cheek/") == true).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts ForgivenPedantry Cheek exact 3.1 double queue effect FIFO and exact-count promotion", CheekQueue);
        run("SimpleHunts ForgivenPedantry Fever dynamic target cleave finish persistence effect-by-source and damage clocks", Fever);
        run("SimpleHunts ForgivenPedantry Witch main target primary origin dynamic rectangle cancel and immutable target identity", Witch);
        run("SimpleHunts ForgivenPedantry Witch ending raw target shared by owners and previous-generation replacement", EndingTarget);
        run("SimpleHunts ForgivenPedantry ordinary casts gaze owner replay helper destruction and cleanup", Lifecycle);
    }

    private static void CheekQueue()
    {
        foreach (var action in new uint[] { 17446, 17448 })
        {
            var e = Create();
            var cast = Cast(action);
            e.Start(999, cast, false);
            var queue = Cheeks(e);
            Check(queue.Length == 2 && queue[0].Risky && !queue[1].Risky
                && queue[1].Activation == cast.FinishAt.AddSeconds(3.1d)
                && queue[1].Rotation == cast.Rotation + MathF.PI
                && queue.All(a => a.Shape is FanTemplate { Radius: 60, AngleRadians: MathF.PI }),
                "Cheek pair lost exact double delay, opposite angle, geometry or risk.");
            e.Finish(999, action, Start.AddSeconds(1));
            Check(Cheeks(e).Length == 2, "Cheek Finish consumed an effect-only queue.");
            e.Effect(1, 17447, Start.AddSeconds(2), 1);
            e.Effect(1, 17447, Start.AddSeconds(2.01), 1);
            Check(Cheeks(e).Single().Risky, "Cheek effect failed deduplication or exact-two promotion.");
            e.Effect(999, 17449, Start.AddSeconds(3), 2);
            Check(Cheeks(e).Length == 0, "Opposite Cheek effect failed FIFO.");
        }
        var overlap = Create();
        overlap.Start(999, Cast(17446), false);
        overlap.Start(1, Cast(17448, 1), false);
        overlap.Effect(1, 17448, Start.AddSeconds(2), 1);
        Check(Cheeks(overlap).Length == 3 && !Cheeks(overlap)[0].Risky,
            "Overlapping Cheek incorrectly promoted a tail when total count was not two.");
        overlap.Effect(999, 17447, Start.AddSeconds(3), 2);
        overlap.Effect(999, 17447, Start.AddSeconds(4), 3);
        Check(Cheeks(overlap).Single().Risky, "Remaining exact-two Cheek pair did not promote.");
    }

    private static void Fever()
    {
        var e = Create();
        e.Start(1, Cast(17440), false);
        e.Finish(1, 17440, Start.AddSeconds(1));
        var own = e.TargetPreviews(Player()).Single();
        Check(own.Shape is FanTemplate { Radius: 15 } fan && fan.AngleRadians == 2 * MathF.PI / 3
            && own.OutlineOnly && e.TargetDangers(Player(), Party).All(a => a.Shape is FanTemplate { Radius: 100 })
            && e.TargetHints(Player(), Party).Contains("Tankbuster cleave"), "Fever lost dynamic cone, own navigation or persistent hint.");
        e.Start(1, Cast(17440, 2, 101), false);
        var damage = e.PredictedDamage(Party).ToArray();
        Check(damage.Length == 2 && damage.All(d => d.Activation == Cast(17440).FinishAt && d.DamageType == "Tankbuster"),
            "Fever prediction was expanded to raidwide or used per-bait clocks.");
        e.Effect(999, 17440, Start.AddSeconds(3), 1);
        Check(e.TargetPreviews(Player()).Count() == 2, "Fever effect from another source removed a bait.");
        e.Effect(1, 17440, Start.AddSeconds(4), 2);
        Check(e.TargetPreviews(Player()).Single().TargetId == 101, "Fever effect cleared more than the first matching source.");
        e.Synchronize([Boss(), Helper(), Player(), Player(101) with { Dead = true }, Player(102)], _ => true, Start.AddSeconds(5), Party);
        Check(!e.TargetPreviews(Player()).Any(), "Dead Fever target retained an effect-only bait.");
        e.Start(999, Cast(17440, 6), false);
        e.Remove(999, Start.AddSeconds(7));
        e.UpdateComponents(Start.AddSeconds(7));
        Check(!e.TargetPreviews(Player()).Any(), "Destroyed Fever source retained its bait.");
        e.Start(1, Cast(17440, 8, 1), false);
        Check(!e.TargetDangers(Boss(), Party).Any() && !e.PredictedDamage(Party).Any(),
            "Self-target Fever was retargeted to current aggro or added own-source navigation.");
    }

    private static void Witch()
    {
        var e = Create();
        e.Synchronize([Boss(), Helper(), Player(position: new(30, 0, 0)), Player(101, new(0, 0, 20)), Player(102)],
            _ => true, Start, Party);
        e.Effect(999, 17900, Start.AddSeconds(1), 1, 100);
        e.UpdateComponents(Start.AddSeconds(1));
        var first = e.TargetPreviews(Player()).Single();
        Check(first.Location == Vector3.Zero && first.Shape is RayRectangleTemplate { Length: 30, MinLocalX: -5, MaxLocalX: 5 }
            && first.Rotation == MathF.PI / 2 && first.TargetId == 100 && first.Activation == default,
            "Witch did not use main target, primary source, dynamic full-width10 or default activation.");
        e.Effect(999, 17900, Start.AddSeconds(1.1), 1, 101);
        Check(e.TargetPreviews(Player()).Single().TargetId == 100, "Duplicated full packet changed Witch main target.");
        e.Start(999, Cast(17444, 2, 101), false);
        e.Finish(999, 17444, Start.AddSeconds(3));
        e.UpdateComponents(Start.AddSeconds(3));
        Check(e.TargetPreviews(Player()).Single().TargetId == 101, "Witch Finish/cancel did not replace the previous bait.");
        e.Synchronize([Boss() with { TargetId = 102 }, Helper(), Player(), Player(101, new(0, 0, 40)) with { Dead = true }, Player(102)],
            _ => true, Start.AddSeconds(4), Party);
        Check(e.TargetPreviews(Player()).Single().Shape is RayRectangleTemplate { Length: 40 }
            && e.TargetPreviews(Player()).Single().TargetId == 101, "Witch switched tanks or removed a dead target.");
        e.Remove(101);
        e.Synchronize([Boss(), Helper(), Player(), Player(101, new(0, 0, 80)), Player(102)], _ => true, Start.AddSeconds(5), Party);
        Check(e.TargetPreviews(Player()).Single().Shape is RayRectangleTemplate { Length: 40 },
            "Witch rebound a destroyed target's ID to a replacement object.");
        Check(e.TargetHints(Player(101, new(80, 0, 0)), Party).Contains("Bait away from raid!"),
            "Witch personal hint incorrectly used detached Actor identity instead of InstanceID.");
        e.Effect(1, 17445, Start.AddSeconds(6), 2);
        Check(!e.TargetPreviews(Player()).Any(), "Witch second effect did not clear all baits.");
    }

    private static void EndingTarget()
    {
        var e = Create();
        var packet = Cast(17444, target: 100);
        var raw = packet with { TargetId = 101 };
        e.Start(999, packet, true);
        e.Synchronize([Boss(), Boss(2), Helper(raw), Player(), Player(101), Player(102)],
            _ => true, Start.AddSeconds(.1), Party);
        e.Synchronize([Boss(), Boss(2), Helper(), Player(), Player(101), Player(102)],
            _ => true, Start.AddSeconds(1), Party);
        Check(e.TargetPreviews(Player()).Count() == 2
            && e.TargetPreviews(Player()).All(c => c.TargetId == 101),
            "Witch Finish inherited owner-specific Started targets rather than ending raw target.");
        e.Reset();
        e.Synchronize([Boss(), Helper(packet), Player(), Player(101), Player(102)], _ => true, Start, Party);
        e.Synchronize([Boss(), Helper(raw), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(1), Party);
        Check(e.TargetPreviews(Player()).Single().TargetId == 100,
            "Witch raw replacement finished against the new generation target.");
        e.Synchronize([Boss(), Helper(), Player(), Player(101), Player(102)], _ => true, Start.AddSeconds(2), Party);
        Check(e.TargetPreviews(Player()).Single().TargetId == 101,
            "Witch replacement generation did not finish against its own target.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        e.Start(999, Cast(17443), false);
        Check(e.Areas.Single(a => a.Shape != null).Shape is CircleTemplate { Radius: 18 }, "TheStake lost R18.");
        e.Start(999, Cast(17441, 1), false);
        Check(e.Areas.Single(a => a.Shape != null).Shape is RayRectangleTemplate { Length: 40, MinLocalX: -4, MaxLocalX: 4 },
            "SecondCircle lost front40/full-width8.");
        e.Start(999, Cast(17442, 2), false);
        Check(e.PredictedDamage(Party).Count() == 3 && !e.Areas.Any(a => a.Shape != null), "CleansingFire classification differs.");
        var gaze = Cast(17955, 3) with { Location = new(0, 0, 100) };
        e.Start(999, gaze, false);
        Check(e.Gazes(Player()).Single().Rule.GazeRange == 10000
            && HuntDrawing.Collect(e, Player()).Count(p => p.Id.StartsWith("gaze/")) == 3
            && e.Facing(Player(), Start.AddSeconds(8)) != null,
            "Pedantry helper gaze lost default range, eye/pupil/view arc or facing output.");
        e.Reset();
        var helper = Helper(Cast(17446));
        e.Synchronize([Boss(), helper, Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), helper, Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Cheeks(e).Length == 4, "New Pedantry owner did not replay current helper cast.");
        e.Remove(1);
        Check(Cheeks(e).Length == 2 && Cheeks(e).All(a => a.Source == 2), "Removing one owner lost another owner's queue.");
        e.Remove(2);
        Check(!e.Casts.Any() && !e.Areas.Any() && !e.TargetPreviews(Player()).Any(), "Last Pedantry owner retained helper outputs.");
        e.Reset();
    }
}
