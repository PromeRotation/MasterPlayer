using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntSlipperyRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x43DC, Kind = 2, Hp = 1000, InCombat = true, Targetable = true };
    private static ReplayActorState Helper(ReplayCast? cast = null, Vector3? position = null) => new()
        { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast, Position = position ?? Vector3.Zero };
    private static ReplayActorState Player(ReplayStatus? status = null) => new()
        { Id = 100, Hp = 1000, Statuses = status == null ? [] : [status] };
    private static ReplayCast Cast(uint action = 38648, double at = 0)
        => new(action, 999, new(100, 0, 100), 0, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player()], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Queue(SimpleHuntEncounter e)
        => e.Areas.Where(c => c.SequenceId?.Contains("/slippery/") == true).ToArray();
    private static float Quantize(float value)
    {
        const float grid = 2000f / 65535f;
        const float inverse = 1f / grid;
        return ((int)MathF.Round(value * inverse) - .5f) * grid;
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Slippery independent cast and caster origins exact delays full risky FIFO and source-position correction", Sequence);
        run("SimpleHunts Slippery march five-second boundary current forced remainder and queue-only landing hint", March);
        run("SimpleHunts Slippery ordinary grouped finish raidwide helper owner replay and cleanup", Lifecycle);
    }

    private static void Sequence()
    {
        var e = Create();
        var cast = Cast();
        e.Start(999, cast, true, Vector3.Zero);
        var queue = Queue(e);
        Check(queue.Length == 2 && queue.All(c => c.Risky)
            && queue[0].Location == new Vector3(100, 0, 100)
            && queue[0].Shape is RayRectangleTemplate { Length: 20, MinLocalX: -5, MaxLocalX: 5 }
            && queue[0].Activation == cast.FinishAt.AddSeconds(.2d)
            && queue[1].Location == new Vector3(Quantize(0), 0, Quantize(20))
            && queue[1].Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 40 }
            && queue[1].Activation == cast.FinishAt.AddSeconds(2.8d),
            "Slippery merged the independent origins, quantized incorrectly, lost delays or previewed its second hit as safe.");
        e.Start(999, cast, true, new(10, 0, 0));
        Check(Queue(e)[1].Location == new Vector3(Quantize(10), 0, Quantize(20))
            && Queue(e)[0].Location == cast.Location, "Source-position-only packet correction was lost or moved the cast origin.");
        e.Start(999, cast, true, new(1.0528725385665894f, 0, 0));
        Check(Queue(e)[1].Location.X == 1.0528725385665894f,
            "Slippery landing used division instead of source float reciprocal multiplication at a rounding tie.");
        e.Finish(999, 38648, Start.AddSeconds(1));
        e.Start(1, Cast(at: 2), false);
        Check(Queue(e).Length == 4 && Queue(e).All(c => c.Risky), "Slippery overlap hid or made future stages non-risky.");
        for (uint i = 1; i <= 4; ++i)
        {
            var action = i % 2 == 0 ? 38650u : 39559u;
            e.Effect(1, action, Start.AddSeconds(20 + i), i);
            e.Effect(1, action, Start.AddSeconds(20.01 + i), i);
            Check(Queue(e).Length == 4 - i, "Slippery effect failed global FIFO or deduplication.");
        }
    }

    private static void March()
    {
        var e = Create();
        var status = new ReplayStatus(2161, 0, 1, Start.AddSeconds(5.0001));
        e.Synchronize([Boss(), Helper(), Player(status)], _ => true, Start, Party);
        Check(!e.MarchMovements(Player()).Any(), "PoisonGasMarch included a status beyond five seconds.");
        e.Synchronize([Boss(), Helper(), Player(status)], _ => true, Start.AddSeconds(.0001), Party);
        Check(e.MarchMovements(Player()).Single().To == new Vector3(0, 0, 19.8f),
            "PoisonGasMarch excluded the exact activation boundary or lost 3s at 6.6 speed.");
        e.Start(999, Cast(), false);
        // Player lands near the ring center, inside its hole.
        Check(!e.Hints(Player()).Contains("Aim for green safe spot!"), "Slippery march used rectangle or ring bounds incorrectly.");
        var moved = Player(status) with { Position = new(15, 0, 0) };
        e.Synchronize([Boss(), Helper(), moved], _ => true, Start.AddSeconds(.0002), Party);
        Check(e.Hints(moved).Contains("Aim for green safe spot!") && !e.TargetDangers(moved, Party).Any()
            && e.Facing(moved, Start) == null && !e.StopEverything(moved, Party, Start),
            "PoisonGasMarch lost queue-specific hint or invented movement/facing controls.");
        e.Effect(1, 39559, Start.AddSeconds(1), 1);
        e.Effect(1, 38650, Start.AddSeconds(2), 2);
        e.Start(999, Cast(40063, 3) with { Location = moved.Position + new Vector3(0, 0, 19.8f) }, false);
        Check(!e.Hints(moved).Contains("Aim for green safe spot!"), "PoisonGasMarch checked an independent BodyPress.");
        var active = new ReplayStatus(1257, 0, 1, Start.AddSeconds(6));
        e.Synchronize([Boss(), Helper(), Player(active)], _ => true, Start.AddSeconds(4), Party);
        Check(e.MarchMovements(Player()).Single().To == new Vector3(0, 0, 13.2f), "Actual forced march lost remaining-time projection.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        e.Start(999, Cast(40063), false);
        e.Finish(999, 38654, Start.AddSeconds(1));
        Check(!e.Areas.Any(c => c.Shape != null), "BodyPress grouped AOE incorrectly required matching Finish AID.");
        e.Start(999, Cast(38651, 2), false);
        Check(e.Areas.Single(c => c.Shape != null).Shape is CircleTemplate { Radius: 15 }, "Second BodyPress missing R15.");
        e.Start(999, Cast(39807, 3), false);
        Check(e.Areas.Single(c => c.Shape != null).Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 40 }, "Scatterscourge geometry differs.");
        e.Start(999, Cast(38652, 4), false);
        Check(!e.Areas.Any(c => c.Shape != null) && e.PredictedDamage(Party).Single().DamageType == "Raidwide", "PoisonGas fabricated a dangerous circle.");
        e.Start(999, Cast(38653, 5), false);
        Check(e.Hints(Player()).Contains("Interrupt!") && !e.PredictedDamage(Party).Any(), "MalignantMucus fabricated damage or missed interrupt hint.");
        e.Start(999, Cast(38654, 6), false);
        Check(e.Areas.Single(c => c.Shape != null).Shape is CircleTemplate { Radius: 6 }, "PoisonMucus lost R6.");
        e.Reset();
        e.Synchronize([Boss(), Helper(Cast()), Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), Helper(Cast()), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Queue(e).Length == 4, "New Slippery owner did not replay current helper cast.");
        e.Remove(1);
        Check(Queue(e).Length == 2 && Queue(e).All(c => c.Source == 2), "Slippery owner removal affected another owner.");
        e.Remove(2);
        Check(!e.Areas.Any() && !e.Casts.Any() && !e.MarchMovements(Player()).Any(), "Slippery last owner retained output.");
    }
}
