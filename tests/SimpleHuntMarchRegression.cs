using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntMarchRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts forced march status expiry order cumulative directions active remainder events and no cutoff", Statuses);
        run("SimpleHunts Yilan predicted MiniLight live origin actual correction finish and final-only march safety", Yilan);
        run("SimpleHunts NarrowRift dynamic queue arbitrary primary finish beam effects no timeout and cleanup", Narrow);
    }

    private static void Statuses()
    {
        var state = new HuntMarchState();
        var player = new ReplayActorState { Id = 100 };
        state.Status(100, 1960, Start, Status(1960, 20));
        state.Status(100, 1958, Start, Status(1958, 10));
        state.Synchronize(new Dictionary<ulong, ReplayActorState> { [100] = player });
        var moves = state.Movements(player, Start).ToArray();
        Check(moves.Length == 2 && Near(moves[0].To, new(0, 0, 13.2f)) && Near(moves[1].To, new(13.2f, 0, 13.2f)),
            "March sorted by arrival, reset direction each segment or imposed an eight-second cutoff.");
        player = player with { Statuses = [Status(1958, 10), Status(1960, 20)] };
        state.Synchronize(new Dictionary<ulong, ReplayActorState> { [100] = player });
        Check(state.Movements(player, Start).Count() == 2, "Snapshot confirmation appended duplicate pending moves.");
        state.Status(100, 1958, Start.AddSeconds(1), null);
        state.Status(100, 1960, Start.AddSeconds(1), null);
        state.Status(100, 1257, Start.AddSeconds(1), Status(1257, 3) with { Param = 8 });
        state.Status(100, 1961, Start.AddSeconds(1), Status(1961, 5));
        player = player with { Rotation = MathF.PI / 2 };
        moves = state.Movements(player, Start.AddSeconds(2)).ToArray();
        Check(moves.Length == 2 && Near(moves[0].To, new(6.6f, 0, 0)) && Near(moves[1].To, new(6.6f, 0, 13.2f)),
            "Active march reapplied Param rotation or lost its remaining-duration projection.");
        state.Status(100, 1961, Start, Status(1961, 5));
        Check(state.Movements(player, Start.AddSeconds(2)).Count() == 2, "Stale event duplicated an accepted move.");
        state.Status(100, 1257, Start.AddSeconds(3), null);
        state.Status(100, 1961, Start.AddSeconds(3), null);
        Check(!state.Movements(player, Start.AddSeconds(3)).Any(), "Status loss retained pending/active motion.");
        state.Status(100, 3629, Start.AddSeconds(4), Status(3629, 6));
        Check(state.Movements(player, Start.AddSeconds(5)).Count() == 1, "NPC forced status was not accepted.");
        state.Remove(100);
        Check(!state.Movements(player, Start).Any(), "Removed actor retained forced motion.");
        player = player with { Statuses = [Status(1958, 10)] };
        var actors = new Dictionary<ulong, ReplayActorState> { [100] = player };
        state.Synchronize(actors);
        state.Status(100, 1958, Start, Status(1958, 10));
        state.Synchronize(actors);
        Check(state.Movements(player, Start).Count() == 1, "A delayed event duplicated a status already accepted from a snapshot.");
        state.Status(100, 1958, Start.AddSeconds(1), null);
        actors[100] = player with { Statuses = [] };
        state.Synchronize(actors);
        Check(!state.Movements(player, Start).Any(), "Snapshot-first status loss retained a ghost segment.");
    }

    private static void Yilan()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100, Position = new(0, 0, 20) };
        var cast = Cast(27230, 0, 5);
        encounter.Synchronize([Boss(0x35BF, cast), player], _ => true, Start);
        Check(encounter.Areas.Single(c => c.Shape != null) is { Shape: CircleTemplate { Radius: 18 } } prediction
            && prediction.Activation == cast.FinishAt.AddSeconds(12.1f), "Soundstorm invented R30 or changed the float delay.");
        encounter.Synchronize([Boss(0x35BF, null, new(10, 0, 10)), player], _ => true, Start.AddSeconds(20));
        Check(encounter.Areas.Single(c => c.Shape != null).Location == new Vector3(10, 0, 10),
            "Soundstorm finish/estimated expiry removed MiniLight or stopped following the boss.");
        var actual = Cast(27231, 21, 7);
        encounter.Synchronize([Boss(0x35BF, actual), player], _ => true, Start.AddSeconds(21));
        Check(encounter.Areas.Single(c => c.Shape != null).Activation == actual.FinishAt, "Actual MiniLight failed to replace its estimate.");
        encounter.Synchronize([Boss(0x35BF, null), player], _ => true, Start.AddSeconds(30));
        Check(!encounter.Areas.Any(c => c.Shape != null), "Actual MiniLight ending did not clear.");
        encounter.MarchStatus(100, 1959, Start.AddSeconds(30), Status(1959, 40));
        Check(encounter.Hints(player).Contains("Aim for safe spot!"), "Yilan did not test march landing when MiniLight was hidden.");
        encounter.MarchStatus(100, 1959, Start.AddSeconds(31), null);
        player = player with { Position = new(0, 0, -20) };
        encounter.Synchronize([Boss(0x35BF, null), player], _ => true, Start.AddSeconds(31));
        encounter.MarchStatus(100, 1958, Start.AddSeconds(31), Status(1958, 40));
        encounter.MarchStatus(100, 1958, Start.AddSeconds(32), Status(1958, 41));
        encounter.MarchStatus(100, 1958, Start.AddSeconds(33), Status(1958, 42));
        Check(!encounter.Hints(player).Contains("Aim for safe spot!"), "Intermediate crossing replaced the source final-only landing check.");
    }

    private static void Narrow()
    {
        var encounter = new SimpleHuntEncounter();
        var player = new ReplayActorState { Id = 100 };
        encounter.Synchronize([Boss(0x35DB, Cast(27331, 0, 12)), player], _ => true, Start);
        Check(encounter.Areas.Single(c => c.Shape != null).Shape is CircleTemplate { Radius: 10 }, "Narrow first stage missing.");
        encounter.Synchronize([Boss(0x35DB, null, new(10, 0, 20)), player], _ => true, Start.AddSeconds(13));
        var second = encounter.Areas.Single(c => c.Shape != null);
        Check(second.Shape is DonutTemplate && second.Location == new Vector3(10, 0, 20) && second.Activation == default,
            "Narrow pre-estimated the second clock or lost dynamic origin.");
        encounter.Synchronize([Boss(0x35DB, Cast(999, 14, 1)), player], _ => true, Start.AddSeconds(14));
        Check(encounter.Areas.Single(c => c.Shape != null).Activation == Cast(999, 14, 1).FinishAt,
            "An unlisted current cast failed to supply the promise clock.");
        encounter.Synchronize([Boss(0x35DB, null), player], _ => true, Start.AddSeconds(16));
        Check(!encounter.Areas.Any(c => c.Shape != null), "An arbitrary primary finish failed to pop the remaining promise.");
        encounter.Effect(1, 27333, Start.AddSeconds(17), 1);
        encounter.Synchronize([Boss(0x35DB, null, new(30, 0, 40)) with { Rotation = .7f }, player], _ => true, Start.AddSeconds(40));
        var beam = encounter.Areas.Single(c => c.Shape != null);
        Check(beam.Activation == Start.AddSeconds(21) && beam.Location == new Vector3(30, 0, 40) && beam.Rotation == .7f,
            "Beam timed out or fixed its effect pose.");
        encounter.Effect(1, 27519, Start.AddSeconds(41), 2);
        Check(encounter.Areas.Any(c => c.Shape != null), "Visual end consumed the beam.");
        encounter.Effect(1, 27516, Start.AddSeconds(42), 3);
        Check(!encounter.Areas.Any(c => c.Shape != null), "Actual VanishingRay failed to consume the beam.");
        encounter.Reset();
        Check(!encounter.MarchMovements(player).Any() && !encounter.Areas.Any(), "Reset leaked narrow/march state.");
    }

    private static ReplayActorState Boss(uint oid, ReplayCast? cast, Vector3 position = default)
        => new() { Hp = 1000, Id = 1, Oid = oid, Position = position, Targetable = true, InCombat = true, Cast = cast };
    private static ReplayCast Cast(uint action, double at, double duration)
        => new(action, 1, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + duration + .3), false, false) { TotalTime = (float)duration };
    private static ReplayStatus Status(uint status, double expiry) => new(status, 0, 1, Start.AddSeconds(expiry));
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
