using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntIhnuxokiyRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P1", 101)];
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x4582, Kind = 2, Hp = 1000, InCombat = true, Targetable = true, TargetId = 101 };
    private static ReplayActorState Player(ulong id = 100) => new() { Id = id, Position = Vector3.Zero, Hp = 1000 };
    private static ReplayActorState Helper(ReplayCast? cast = null) => new()
        { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast, Position = new(10, 0, 20) };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 100)
        => new(action, target, new(10, 0, 20), .3f, Start.AddSeconds(at), Start.AddSeconds(at + 8.3), false, false);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static SimpleHuntEncounter Create()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper(), Player(), Player(101)], _ => true, Start, Party);
        return e;
    }
    private static HuntPendingCast[] Areas(SimpleHuntEncounter e) => e.Areas.Where(a => a.Shape != null).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Ihnuxokiy stored circle ring overwrite cancellation exact float delay and helper effect FIFO", Storage);
        run("SimpleHunts Ihnuxokiy correction consumed stages raw restarts and all queue previews", Generations);
        run("SimpleHunts Ihnuxokiy three second march cumulative active remainder final hint and no added execution", March);
        run("SimpleHunts Ihnuxokiy single target ChaoticStorm observed helper lifetime owner replay and cleanup", Lifecycle);
    }

    private static void Storage()
    {
        foreach (var storage in new uint[] { 0, 39778, 39779 })
        {
            var e = Create();
            if (storage != 0)
            {
                e.Start(999, Cast(39778), false);
                if (storage == 39779) e.Start(999, Cast(39779, 1), false);
                e.Finish(999, storage, Start.AddSeconds(2));
            }
            var cast = Cast(39773, 3);
            e.Start(999, cast, false);
            var areas = Areas(e);
            Check(areas.Length == (storage == 0 ? 1 : 2) && areas[0].Shape is FanTemplate { Radius: 40 } cone
                && cone.AngleRadians == MathF.PI && areas[0].Risky, "Ihnuxokiy guessed missing history or lost its cone.");
            if (storage != 0)
            {
                Check(areas[1].Activation == cast.FinishAt.AddSeconds(2.2f) && areas[1].Rotation == 0 && !areas[1].Risky
                    && (storage == 39778 ? areas[1].Shape is CircleTemplate { Radius: 12 }
                        : areas[1].Shape is DonutTemplate { InnerRadius: 8, OuterRadius: 40 }),
                    "Ihnuxokiy stored shape, float delay, neutral rotation or preview risk differs.");
            }
            e.Finish(999, 39773, Start.AddSeconds(4));
            Check(Areas(e).Length == areas.Length, "Finish consumed an effect-only queue.");
            e.Effect(999, 39773, Start.AddSeconds(5), 10);
            e.Effect(999, 39773, Start.AddSeconds(5.01), 10);
            Check(Areas(e).Length == areas.Length - 1 && Areas(e).All(a => a.Risky), "Helper effect failed to consume exactly once or promote the tail.");
            e.Effect(1, 39781, Start.AddSeconds(6), 11);
            Check(Areas(e).Length == 0, "Another valid caster could not consume the component FIFO.");
        }
    }

    private static void Generations()
    {
        var e = Create();
        e.Start(999, Cast(39779), false);
        var cast = Cast(39828, 1);
        e.Start(999, cast, false);
        e.Effect(1, 39828, Start.AddSeconds(2), 1);
        var corrected = cast with { FinishAt = Start.AddSeconds(12), Location = new(30, 0, 40), Rotation = 1 };
        e.Start(999, corrected, true);
        Check(Areas(e).Length == 1 && Areas(e)[0].Shape is DonutTemplate
            && Areas(e)[0].Activation == corrected.FinishAt.AddSeconds(2.2f) && Areas(e)[0].Location == corrected.Location,
            "Ihnuxokiy correction resurrected a consumed stage or changed stored shape.");
        e.Start(999, cast with { Rotation = .6f }, false);
        Check(Areas(e).Length == 2 && Areas(e)[0].Shape is DonutTemplate && Areas(e)[1].Shape is FanTemplate,
            "Real raw restart reused already consumed storage or replaced earlier queue entries.");
        e.Start(1, Cast(39778, 3), false);
        e.Start(1, Cast(39773, 4), false);
        Check(Areas(e).Length == 4 && Areas(e).Count(a => a.Risky) == 1, "Ihnuxokiy capped its queue or made a future item risky.");
        e.Synchronize([Boss(), Helper(), Player(), Player(101)], _ => true, Start.AddSeconds(100), Party);
        Check(Areas(e).Length == 4, "Clock passage or observed Finish cleared stored effects.");
    }

    private static void March()
    {
        var e = Create();
        var player = Player();
        var left = new ReplayStatus(2163, 0, 1, Start.AddSeconds(30));
        var forward = new ReplayStatus(2161, 0, 1, Start.AddSeconds(40));
        e.MarchStatus(100, 2163, Start, left);
        e.MarchStatus(100, 2161, Start, forward);
        var movements = e.MarchMovements(player).ToArray();
        Check(movements.Length == 2 && MathF.Abs(movements[0].To.X - 19.8f) < .0001f
            && MathF.Abs(movements[1].To.X - 39.6f) < .0001f && e.Hints(player).Contains("Aim for safe spot!"),
            "Ihnuxokiy march duration, cumulative direction, unbounded preview or final R30 hint differs.");
        e.MarchStatus(100, 2163, Start.AddSeconds(1), null);
        e.MarchStatus(100, 2161, Start.AddSeconds(1), null);
        Check(!e.MarchMovements(player).Any(), "Ihnuxokiy direction Lose retained a segment.");
        e.MarchStatus(100, 1257, Start.AddSeconds(2), new(1257, 0, 1, Start.AddSeconds(5)));
        e.Synchronize([Boss(), Helper(), player, Player(101)], _ => true, Start.AddSeconds(3), Party);
        Check(MathF.Abs(e.MarchMovements(player).Single().To.Z - 13.2f) < .0001f,
            "Ihnuxokiy active march ignored the remaining duration.");
        Check(!e.StopEverything(player, Party, Start.AddSeconds(3)) && e.Facing(player, Start) == null
            && !e.TargetDangers(player, Party).Any(), "Source display-only march invented hold, facing or navigation.");
        e.Synchronize([Boss() with { Dead = true }, player], _ => true, Start.AddSeconds(4), Party);
        Check(!e.MarchMovements(player).Any(), "Ihnuxokiy owner death retained march output.");
        e.Reset();
        e.Synchronize([Boss(), player], _ => true, Start, Party);
        e.MarchStatus(100, 2161, Start, forward);
        e.Remove(1, Start.AddSeconds(1));
        Check(!e.MarchMovements(player).Any(), "Explicit removal of the last march owner retained a player projection until the next scan.");
    }

    private static void Lifecycle()
    {
        var e = Create();
        e.Start(1, Cast(39777, target: 1), false);
        var predictions = e.PredictedDamage(Party).ToArray();
        Check(predictions.Length == 1 && predictions[0].TargetId == 101 && predictions[0].DamageType == "Tankbuster"
            && e.Hints(Player()).Contains("Raidwide + forced march debuffs"),
            "ChaoticStorm was expanded from source SingleTargetCast to a raidwide prediction.");
        e.Finish(1, 39777, Start.AddSeconds(1));
        e.Start(999, Cast(39774, 2), false);
        Check(Areas(e).Single().Shape is RayRectangleTemplate { Length: 50, MinLocalX: -6, MaxLocalX: 6 },
            "RazorZephyr is not front50 full-width12.");
        e.Remove(999, Start.AddSeconds(3));
        Check(Areas(e).Length == 0, "Ordinary helper destruction retained RazorZephyr.");
        e.Synchronize([Boss(), Helper(Cast(39773, 4)), Player()], _ => true, Start.AddSeconds(4), Party);
        Check(Areas(e).Length == 1, "Observed helper did not broadcast AbyssalSmog.");
        e.Synchronize([Boss(), Player()], _ => true, Start.AddSeconds(5), Party);
        Check(Areas(e).Length == 1, "Observed helper destruction prematurely consumed effect-only history.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), Player()], _ => true, Start, Party);
        e.Start(999, Cast(39778), false);
        e.Finish(999, 39778, Start.AddSeconds(.5));
        e.Start(999, Cast(39773), true);
        e.Remove(999, Start.AddSeconds(1));
        Check(Areas(e).Length == 2 && Areas(e)[1].Shape is CircleTemplate,
            "Packet helper destruction discarded the effect-only queue and its consumed storage.");
        e.Effect(1, 39773, Start.AddSeconds(2), 1);
        e.Effect(1, 39780, Start.AddSeconds(3), 2);
        Check(Areas(e).Length == 0, "Surviving caster effects failed to resolve the destroyed helper's queue.");
        e.Reset();
        e.Synchronize([Boss(), Helper(Cast(39773)), Player()], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), Helper(Cast(39773)), Player()], _ => true, Start.AddSeconds(1), Party);
        Check(Areas(e).Length == 2, "A new owner failed to replay the helper's current Smog.");
        e.MarchStatus(100, 2161, Start.AddSeconds(1), new(2161, 0, 1, Start.AddSeconds(30)));
        e.Remove(1, Start.AddSeconds(2));
        Check(Areas(e).Single().Source == 2 && e.MarchMovements(Player()).Any(),
            "Removing one owner discarded the surviving owner's Smog or shared march state.");
        e.Remove(2, Start.AddSeconds(3));
        Check(!e.MarchMovements(Player()).Any(), "Removing the final owner retained shared march state.");
        e.Reset();
        Check(!e.Active && Areas(e).Length == 0 && !e.MarchMovements(Player()).Any(), "Reset retained Ihnuxokiy state.");
    }
}
