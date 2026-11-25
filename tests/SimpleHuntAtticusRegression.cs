using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntAtticusRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayActorState Boss(ulong id = 1) => new()
        { Id = id, Oid = 0x416D, Kind = 2, Hp = 1000, Targetable = true, InCombat = true };
    private static ReplayActorState Helper(ulong id = 999, ReplayCast? cast = null) => new()
        { Id = id, Oid = 0x233C, Kind = 2, Position = new(3, 0, 4), Rotation = .2f, Cast = cast };
    private static ReplayActorState Player(ulong id = 100) => new() { Id = id, Position = new((id - 100) * 20, 0, -20) };
    private static ReplayCast Cast(uint action, double at = 0, ulong target = 101)
        => new(action, target, new(20, 0, 30), .3f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static readonly ReplayPartyMember[] Party = [new(0, "P", 100), new(1, "P1", 101), new(2, "P2", 102)];
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Atticus all thirty yells locked pose exact placeholder no guessed history", Yells);
        run("SimpleHunts Atticus all pending clocks correction hidden promotion real effects and restart", Sequence);
        run("SimpleHunts Atticus component stack helper FIFO targets native union and lifecycle", Stacks);
        run("SimpleHunts Atticus snapshot helper destruction and later owner activation replay current casts", Lifecycle);
    }

    private static void Yells()
    {
        uint[][] yells =
        [
            [16874, 16881, 16882, 16885, 16888, 16889, 16896, 16897, 16900, 16903],
            [16876, 16877, 16880, 16883, 16884, 16891, 16892, 16895, 16898, 16899],
            [16875, 16878, 16879, 16886, 16887, 16890, 16893, 16894, 16901, 16902]
        ];
        for (var direction = 0; direction < yells.Length; ++direction)
        foreach (var yell in yells[direction])
        {
            var e = new SimpleHuntEncounter();
            var actor = Helper();
            e.Synchronize([Boss(), actor], _ => true, Start);
            e.NpcYell(999, (ushort)yell, Start);
            var area = e.Areas.Single();
            var offset = direction == 0 ? 0 : direction == 1 ? 2 * MathF.PI / 3 : -2 * MathF.PI / 3;
            Check(area.Shape is FanTemplate { Radius: 40 } shape && shape.AngleRadians == 2 * MathF.PI / 3
                && area.Location == actor.Position && area.Rotation == actor.Rotation + offset
                && area.Activation == Start.AddSeconds(10.5d) && area.Risky, "Atticus yell mapping, geometry or initial clock differs.");
            e.NpcYell(999, 16904, Start.AddSeconds(1));
            e.NpcYell(999, 1, Start.AddSeconds(2));
            e.Synchronize([Boss(), actor with { Position = new(80, 0, 90), Rotation = 2 }], _ => true, Start.AddSeconds(20));
            Check(e.Areas.Single().Location == actor.Position && e.Areas.Single().Rotation == area.Rotation,
                "Unknown yell, time passage or actor movement consumed/changed Atticus history.");
        }
        var noHistory = new SimpleHuntEncounter();
        noHistory.Synchronize([Boss(), Helper(cast: Cast(39003))], _ => true, Start);
        Check(!noHistory.Areas.Any(a => a.Shape != null), "Atticus invented a breath without yell history.");
    }

    private static void Sequence()
    {
        var e = new SimpleHuntEncounter();
        e.Synchronize([Boss(), Helper()], _ => true, Start);
        foreach (var yell in new ushort[] { 16874, 16876, 16875 }) e.NpcYell(999, yell, Start);
        var cast = Cast(39004, 2);
        e.Start(999, cast, false, observedAt: Start.AddSeconds(2));
        var first = e.Areas.Where(a => a.Shape != null).ToArray();
        Check(first.Length == 2 && first.All(a => a.Risky) && first[0].Color == new Vector4(1, 1, 0, 1)
            && first[0].Activation == cast.FinishAt && first[1].Activation == cast.FinishAt.AddSeconds(2.3d),
            "Atticus first-two risk/color or double-precision clocks differ.");
        e.Finish(999, 39004, Start.AddSeconds(3));
        Check(e.Areas.Count(a => a.Shape != null) == 2, "Atticus cancellation consumed its effect-only queue.");
        e.Start(999, Cast(39005, 4), false, observedAt: Start.AddSeconds(4));
        e.Effect(999, 39011, Start.AddSeconds(5), 10);
        e.Effect(999, 39011, Start.AddSeconds(5.01), 10);
        var visible = e.Areas.Where(a => a.Shape != null).ToArray();
        Check(visible.Length == 2 && visible[0].Rotation > 2 && visible[1].Rotation < -1
            && visible[1].Activation == Cast(39005, 4).FinishAt.AddSeconds(4.6d),
            "Atticus unmatched valid effect did not promote its hidden third item exactly once.");
        e.NpcYell(999, 16874, Start.AddSeconds(5));
        var corrected = Cast(39005, 4) with { FinishAt = Start.AddSeconds(11), Location = new(90, 0, 90), Rotation = 2 };
        e.Start(999, corrected, true);
        Check(e.Areas.First(a => a.Shape != null).Activation == corrected.FinishAt.AddSeconds(2.3d)
            && e.Areas.First(a => a.Shape != null).Location == Helper().Position, "Atticus correction shifted stage indices or overwrote yell pose.");
        e.Effect(999, 39003, Start.AddSeconds(6), 11);
        e.Effect(999, 39009, Start.AddSeconds(7), 12);
        Check(e.Areas.Single(a => a.Shape != null).Activation == Start.AddSeconds(15.5d), "Correction retimed a later yell that was not present at Start.");
        for (var i = 0; i < 8; ++i) e.NpcYell(999, 16874, Start.AddSeconds(8));
        uint sequence = 100;
        foreach (var action in new uint[] { 39003, 39004, 39005, 39009, 39010, 39011, 39003, 39004, 39005 })
            e.Effect(999, action, Start.AddSeconds(20), ++sequence);
        Check(!e.Areas.Any(a => a.Shape != null), "Atticus capped history at six or failed to consume all six effect AIDs.");
    }

    private static void Stacks()
    {
        var e = new SimpleHuntEncounter();
        var player = Player();
        var first = Cast(39013);
        var second = Cast(39013, 1);
        e.Synchronize([Boss(), Helper(998, first), Helper(999, second), player, Player(101), Player(102)], _ => true, Start, Party);
        var previews = e.TargetPreviews(player).ToArray();
        Check(previews.Length == 2 && previews.All(p => p.Shape is CircleTemplate { Radius: 6 } && p.OutlineOnly)
            && e.TargetDangers(player, Party).Count() == 1 && !e.PredictedDamage(Party).Any(),
            "Atticus helper stacks did not share one native union component or invented Shared.");
        var secondOrder = e.Casts.Single(c => c.Source == 999).Order;
        e.Finish(999, 39013, Start.AddSeconds(2), 101);
        Check(e.TargetPreviews(player).Single().Order == secondOrder,
            "Atticus Finish matched caster instead of removing the component's first matching target.");
        e.Remove(1, Start.AddSeconds(3));
        Check(!e.Active && !e.Casts.Any() && !e.TargetPreviews(player).Any(), "Atticus owner removal retained helper output.");
        e.Synchronize([Boss(), player], _ => true, Start.AddSeconds(4), Party);
        e.Start(1, Cast(39012, 4), false);
        Check(e.Hints(player).Contains("Applies Haste") && !e.PredictedDamage(Party).Any(), "Brutality invented damage or lost its hint.");
        e.Start(1, Cast(39014, 5), false);
        Check(e.PredictedDamage(Party).Count() == 1 && e.Hints(player).Contains("Raidwide")
            && !e.Areas.Any(a => a.Shape != null), "Intimidation lost its prediction or invented a raidwide circle.");
    }

    private static void Lifecycle()
    {
        var e = new SimpleHuntEncounter();
        var helper = Helper(cast: Cast(39013));
        var player = Player();
        e.Synchronize([Boss(), helper, player, Player(101)], _ => true, Start, Party);
        e.Synchronize([Boss(), player, Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(!e.TargetPreviews(player).Any() && !e.TargetDangers(player, Party).Any()
            && !e.Casts.Any(), "Snapshot helper destruction retained Atticus stack output.");

        e.Reset();
        e.Synchronize([Boss(), helper, player, Player(101)], _ => true, Start, Party);
        e.Synchronize([Boss(), Boss(2), helper, player, Player(101)], _ => true, Start.AddSeconds(1), Party);
        Check(e.TargetPreviews(player).Count() == 2, "New Atticus component did not replay an already observed helper cast.");
        e.Remove(1, Start.AddSeconds(2));
        Check(e.Active && e.TargetPreviews(player).Single().Source == 2
            && e.TargetDangers(player, Party).Count() == 1,
            "Removing the first owner lost the surviving owner's shared helper stack.");
        e.Synchronize([Boss(2), player, Player(101)], _ => true, Start.AddSeconds(3), Party);
        Check(!e.TargetPreviews(player).Any(), "Surviving owner retained the disappeared helper stack.");
    }
}
