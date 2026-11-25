using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntQueenHawkRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayCast Cast(uint action, double at = 0)
        => new(action, 100, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + 4.3), false, false);
    private static ReplayActorState Boss(ReplayCast? cast = null) => new()
    {
        Id = 1, Oid = 0x452B, Hp = 1000, InCombat = true, Targetable = true, Cast = cast
    };
    private static ReplayActorState Player(params ReplayStatus[] statuses) => new() { Id = 100, Statuses = statuses };
    private static HuntPendingCast[] Bees(SimpleHuntEncounter e) => e.Areas.Where(a => a.AreaId.EndsWith("/bee")).ToArray();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts QueenHawk bee exact delay survives cast cancel timeout and either status loss", Bee);
        run("SimpleHunts QueenHawk three second directional march cumulative active and last landing only", March);
        run("SimpleHunts QueenHawk status observation acknowledgement no stale revival and ordinary cast classification", Statuses);
    }

    private static void Bee()
    {
        var e = new SimpleHuntEncounter();
        var cast = Cast(39482);
        e.Synchronize([Boss(cast)], _ => true, Start);
        var initial = Bees(e).Single();
        Check(initial.Shape is CircleTemplate { Radius: 12 } && initial.Activation == cast.FinishAt.AddSeconds(.8d)
            && initial.Rotation == 0, "QueenHawk circle delay or shape differs.");
        e.Finish(1, 39482, Start.AddSeconds(1));
        e.Effect(1, 39482, Start.AddSeconds(2), 1);
        e.Synchronize([Boss()], _ => true, Start.AddSeconds(30));
        Check(Bees(e).Single() == initial, "QueenHawk cancel/effect/timeout consumed BeeBe.");
        e.Start(1, Cast(39483, 31), true);
        Check(Bees(e).Single().Shape is DonutTemplate { InnerRadius: 10, OuterRadius: 40 }, "New bee cast appended instead of replacing circle.");
        e.BeeStatusLost(999, 4147, Start.AddSeconds(32));
        Check(!Bees(e).Any(), "Unrelated actor opposite status loss failed to clear bee.");
        e.Start(1, Cast(39482, 40), true);
        e.BeeStatusLost(999, 4148, Start.AddSeconds(32));
        Check(Bees(e).Count() == 1, "Old status loss cleared a newer cast generation.");
        e.BeeStatusLost(999, 4148, Start.AddSeconds(41));
        Check(!Bees(e).Any(), "Either status loss failed to clear bee.");
    }

    private static void March()
    {
        var e = new SimpleHuntEncounter();
        var status = new ReplayStatus(2163, 0, 1, Start.AddSeconds(5));
        var player = Player(status);
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
        var movement = e.MarchMovements(player).Single();
        Check(Vector3.Distance(movement.To, Vector3.UnitX * (6.6f * 3)) < .00001f,
            "QueenHawk left march used old IDs, two seconds or wrong direction.");
        Check(!e.Hints(player).Contains("Aim outside of the AOE!"), "Crossing a circle before a safe final landing gave a march warning.");
        var inward = player with { Position = new(-20, 0, 0) };
        Check(e.Hints(inward).Contains("Aim outside of the AOE!"), "Final bee landing danger omitted.");
        e.BeeStatusLost(1, 4148, Start.AddSeconds(1));
        Check(!e.Hints(inward).Contains("Aim outside of the AOE!"), "Absent bee used bounds or ordinary AOEs as a march hazard.");
        var multi = Player(new(2161, 99, 1, Start.AddSeconds(4)), new(2162, 0, 1, Start.AddSeconds(6)),
            new(1257, 0, 1, Start.AddSeconds(2)));
        e.Synchronize([Boss(), multi], _ => true, Start.AddSeconds(1));
        var moves = e.MarchMovements(multi).ToArray();
        Check(moves.Length == 3 && MathF.Abs(moves[0].To.Z - 6.6f) < .00001f
            && MathF.Abs(moves[1].To.Z - (6.6f + 6.6f * 3)) < .00001f
            && Vector3.Distance(moves[2].To, new(0, 0, 6.6f)) < .0001f,
            "QueenHawk active and ordered pending march did not accumulate from current position.");
        e.MarchStatus(100, 3629, Start.AddSeconds(1.1), new(3629, 0, 1, Start.AddSeconds(8)));
        e.MarchStatus(100, 1257, Start.AddSeconds(1.2), null);
        Check(e.MarchMovements(multi).Count() == 2, "Losing either active march status did not clear the single forced end.");
        e.Reset();
        e.Synchronize([Boss(), player], _ => true, Start);
        e.Synchronize([Boss(), Player(status with { Param = 1 })], _ => true, Start.AddSeconds(1));
        Check(e.MarchMovements(player).Count() == 2, "Extra-only status change failed to append a true Gain.");
        e.Synchronize([Boss(), Player(status with { Param = 1, SourceId = 2 })], _ => true, Start.AddSeconds(2));
        Check(e.MarchMovements(player).Count() == 2, "Source replacement did not Lose the first direction before Gain.");
    }

    private static void Statuses()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(new ReplayStatus(4147, 0, 1, Start.AddSeconds(20)));
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
        e.BeeStatusLost(100, 4147, Start.AddSeconds(1));
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start.AddSeconds(2));
        Check(!Bees(e).Any(), "Stale status snapshot resurrected consumed bee.");
        var replacement = Cast(39483, 3);
        e.Start(1, replacement, true);
        e.Synchronize([Boss(replacement), Player()], _ => true, Start.AddSeconds(3.1));
        Check(Bees(e).Single().Shape is DonutTemplate, "Acknowledging old loss consumed a newer bee.");
        e.Synchronize([Boss(replacement), player], _ => true, Start.AddSeconds(4));
        e.Synchronize([Boss(replacement), Player()], _ => true, Start.AddSeconds(5));
        Check(!Bees(e).Any(), "Observed status loss failed to clear current bee.");
        e.Reset();
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
        e.Synchronize([Boss(Cast(39482)), Player(player.Statuses[0] with { SourceId = 2 })], _ => true, Start.AddSeconds(1));
        Check(!Bees(e).Any(), "Replacing a bee status source did not deliver its old Lose.");
        e.Reset();
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
        e.Remove(100);
        Check(!Bees(e).Any(), "Destroying an actor with bee status did not deliver StatusLose.");
        e.Reset();
        e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
        e.BeeStatusLost(100, 4147, Start.AddSeconds(1));
        var next = Cast(39483, 2);
        e.Start(1, next, true);
        e.Synchronize([Boss(next), Player(player.Statuses[0] with { SourceId = 2 })], _ => true, Start.AddSeconds(2.1));
        e.Synchronize([Boss(next), Player()], _ => true, Start.AddSeconds(3));
        Check(!Bees(e).Any(), "Old unconfirmed removal masked a new-source Gain and its observed Lose.");
        foreach (var stale in new[] { Player(), player })
        {
            e.Reset();
            e.Synchronize([Boss(Cast(39482)), player], _ => true, Start);
            e.BeeStatusLost(100, 4147, Start.AddSeconds(1));
            e.Start(1, next, true);
            var gain = player.Statuses[0] with { SourceId = 2 };
            e.BeeStatusGained(100, gain, Start.AddSeconds(2));
            e.Synchronize([Boss(next), stale], _ => true, Start.AddSeconds(2.1));
            Check(Bees(e).Single().Shape is DonutTemplate, "Unacknowledged Gain was treated as a snapshot loss.");
            e.BeeStatusLost(100, 4147, Start.AddSeconds(1.5));
            Check(Bees(e).Any(), "Older loss cleared a newer event Gain.");
            e.Synchronize([Boss(next), Player(gain)], _ => true, Start.AddSeconds(2.2));
            Check(Bees(e).Any(), "Acknowledging new Gain consumed the current Bee.");
            e.Synchronize([Boss(next), Player()], _ => true, Start.AddSeconds(3));
            Check(!Bees(e).Any(), "Acknowledged Gain prevented a later real snapshot loss.");
        }
        foreach (var action in new uint[] { 39486, 39489, 39490 })
        {
            e.Reset();
            e.Synchronize([Boss(Cast(action)), Player()], _ => true, Start);
            Check(e.Areas.Count(a => a.Shape != null) == (action == 39490 ? 1 : 0), "QueenHawk pure damage became an AOE.");
            if (action != 39490) Check(e.PredictedDamage([new(0, "P", 100)]).Count() == 1, "QueenHawk damage prediction missing.");
        }
    }
}
