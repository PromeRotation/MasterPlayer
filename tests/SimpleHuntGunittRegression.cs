using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntGunittRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static readonly ReplayPartyMember[] Party =
        [new(0, "P", 100), new(1, "P1", 101), new(2, "P2", 102), new(3, "P3", 103)];
    private static ReplayActorState Boss(ReplayCast? cast = null)
        => new() { Id = 1, Oid = 0x2852, Kind = 2, Hp = 1000, InCombat = true, Targetable = true, Cast = cast };
    private static ReplayActorState Player(ulong id, bool dead = false)
        => new() { Id = id, Dead = dead, Position = new((id - 100) * 3, 0, -15) };
    private static ReplayActorState Helper(ReplayCast? cast = null) => new() { Id = 999, Oid = 0x233C, Kind = 2, Cast = cast };
    private static ReplayCast Cast(uint action, double at = 0)
        => new(action, 101, new(5, 0, 6), .3f, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static ReplayStatus Debuff => new(1138, 0, 1, Start.AddSeconds(60));
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Gunitt exact source geometry gaze and non-invented damage", Geometry);
        run("SimpleHunts Gunitt icon FIFO exact clock live head debuffs and component shared mask", Queue);
        run("SimpleHunts Gunitt slot history event acknowledgement death and helper finish deduplication", SlotsAndFinish);
        run("SimpleHunts Gunitt snapshot roster ordering stale finishes unseen generations and refreshed gain acknowledgement", Reconciliation);
    }

    private static void Geometry()
    {
        Check(SimpleHuntCatalog.Find(0x2852)!.Casts.Length == 6, "Gunitt source cast coverage differs.");
        foreach (var action in new uint[] { 17356, 17357, 17358, 17360, 17351, 17361 })
        {
            var e = new SimpleHuntEncounter();
            var cast = Cast(action);
            var player = Player(100);
            e.Synchronize([Boss(), Helper(cast), player, Player(101)], _ => true, Start, Party);
            Check(e.Casts.Single().Activation == cast.FinishAt && e.Handles(999, action, _ => true), "Gunitt lost helper cast or input time.");
            if (action == 17357)
                Check(e.Areas.Single().Shape is RayRectangleTemplate { MinLocalX: -1, MaxLocalX: 1, Length: 40 }, "Gunitt rectangle differs.");
            if (action == 17351)
                Check(e.Areas.Single().Shape is FanTemplate { Radius: 20 } cone && cone.AngleRadians == MathF.PI / 3
                    && e.Hints(player).Contains("Targets 5 random players after initial hit"), "Gunitt cone or five-player hint differs.");
            if (action == 17360)
                Check(e.Gazes(player).Single().Rule.GazeRange == 10000 && e.Facing(player, cast.FinishAt) != null, "Gunitt gaze lacks native facing plan.");
            var damage = e.PredictedDamage(Party).ToArray();
            Check(damage.Length == (action == 17358 ? 2 : action == 17356 ? 1 : 0), "Gunitt invented or dropped source damage.");
            Check(!e.Areas.Any(a => a.Shape is CircleTemplate) && !e.StopEverything(player, Party, Start), "Gunitt invented raidwide circles or movement hold.");
        }
    }

    private static void Queue()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(100);
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102), Player(103)], _ => true, Start, Party);
        e.Icon(101, 93, Start);
        e.GunittStatus(100, Debuff, Start.AddMilliseconds(10));
        e.UpdateComponents();
        e.Icon(102, 93, Start.AddSeconds(1));
        e.GunittStatus(103, Debuff, Start.AddSeconds(2));
        e.UpdateComponents();
        var previews = e.TargetPreviews(player).ToArray();
        Check(previews.Length == 2 && previews.All(p => p.Shape is CircleTemplate { Radius: 10 } && p.OutlineOnly && !p.Risky)
            && previews[0].Activation == Start.AddSeconds(5d) && previews[1].Activation == Start.AddSeconds(6d), "Gunitt icon queue geometry or exact +5s activation differs.");
        var damage = e.PredictedDamage(Party).ToArray();
        Check(damage.Length == 3 && damage.All(d => d.TargetId != 100 && d.DamageType == "Shared" && d.Activation == Start.AddSeconds(5d))
            && damage.Any(d => d.TargetId == 103), "Gunitt must combine forbidden masks once across the entire active component.");
        Check(e.TargetDangers(player, Party).Count() == 2 && e.TargetHints(player, Party).Count() <= 1, "Gunitt forbidden stack navigation or deduplicated hints missing.");
        e.Effect(1, 17361, Start.AddSeconds(5));
        Check(e.TargetPreviews(player).Count() == 2, "Gunitt effect consumed a Finish-only queue.");
        e.Synchronize([Boss(), Helper(), player, Player(101, true), Player(102), Player(103)], _ => true, Start.AddSeconds(6), Party);
        Check(e.TargetPreviews(player).Count() == 1 && e.PredictedDamage(Party).Any(d => d.TargetId == 103),
            "Dead Gunitt head was removed or the tail mask refreshed early.");
        e.Finish(999, 17361, Start.AddSeconds(6.1));
        Check(e.PredictedDamage(Party).Any(d => d.TargetId == 103), "Gunitt promoted mask changed before component Update.");
        e.UpdateComponents();
        Check(!e.PredictedDamage(Party).Any(d => d.TargetId == 103), "Gunitt promoted head did not receive the latest mask on Update.");
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102), Player(103)], _ => false, Start.AddSeconds(7), Party);
        Check(!e.TargetPreviews(player).Any() && !e.PredictedDamage(Party).Any(), "Gunitt disabled component retained output.");
    }

    private static void SlotsAndFinish()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(100);
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start, Party);
        e.GunittStatus(100, Debuff, Start);
        e.Icon(102, 93, Start);
        var swapped = new ReplayPartyMember[] { new(1, "P", 100), new(0, "P1", 101), new(2, "P2", 102) };
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(1), swapped);
        Check(e.PredictedDamage(swapped).Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 102 }),
            "Gunitt historical forbidden slot followed the actor.");
        e.GunittStatus(100, null, Start.AddSeconds(1.1));
        e.UpdateComponents();
        Check(!e.PredictedDamage(swapped).Any(d => d.TargetId == 101), "Gunitt Lose cleared historical slot instead of the actor's current slot.");
        e.Start(999, Cast(17361, 2), false, observedAt: Start.AddSeconds(2));
        e.Icon(101, 93, Start.AddSeconds(3));
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(8), swapped);
        e.Finish(999, 17361, Start.AddSeconds(7.98));
        e.Finish(999, 17361, Start.AddSeconds(8.02));
        Check(e.TargetPreviews(player).Single().TargetId == 101, "Gunitt helper finish did not consume exactly one head.");
        e.Finish(999, 17361, Start.AddSeconds(9));
        Check(!e.TargetPreviews(player).Any(), "Gunitt later trusted finish was permanently blocked by a previous ended cast.");
        e.Icon(101, 93, Start.AddSeconds(9));
        e.Icon(102, 93, Start.AddSeconds(10));
        e.Start(999, Cast(17361, 11), false, observedAt: Start.AddSeconds(11));
        e.Remove(999);
        Check(e.TargetPreviews(player).Single().TargetId == 102, "Gunitt observed helper destruction lost the final Finish or popped more than one head.");
        e.Reset();
        Check(!e.Active && !e.TargetPreviews(player).Any() && !e.PredictedDamage(Party).Any(), "Gunitt reset retained queue or masks.");
    }

    private static void Reconciliation()
    {
        var e = new SimpleHuntEncounter();
        var player = Player(100);
        e.Synchronize([Boss(), Helper(), player with { Statuses = [Debuff] }, Player(101), Player(102)], _ => true, Start, Party);
        e.Icon(102, 93, Start);
        var swapped = new ReplayPartyMember[] { new(1, "P", 100), new(0, "P1", 101), new(2, "P2", 102) };
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(1), swapped);
        Check(!e.PredictedDamage(swapped).Any(), "Snapshot Lose used the replacement roster instead of the pre-update slot.");
        e.Icon(101, 93, Start.AddSeconds(2));
        e.Start(999, Cast(17361, 10), false, observedAt: Start.AddSeconds(10));
        e.Finish(999, 17361, Start.AddSeconds(9));
        Check(e.TargetPreviews(player).Count() == 2 && e.Casts.Count == 1, "Stale Finished consumed a new-generation stack.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start, Party);
        e.GunittStatus(100, Debuff, Start);
        e.Icon(102, 93, Start);
        e.Synchronize([Boss(), Helper(), player with { Statuses = [Debuff with { ExpiresAt = Start.AddSeconds(62) }] },
            Player(101), Player(102)], _ => true, Start.AddSeconds(1), Party);
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(63), Party);
        Check(!e.PredictedDamage(Party).Any(), "An unacknowledged gain hid the later snapshot refresh and actual loss forever.");
        e.Reset();
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start, Party);
        e.GunittStatus(100, Debuff, Start);
        e.Icon(102, 93, Start);
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(1), Party);
        Check(e.PredictedDamage(Party).Any(), "A snapshot preceding Gain acknowledgement cleared a live forbidden mask.");
        e.Synchronize([Boss(), Helper(), player, Player(101), Player(102)], _ => true, Start.AddSeconds(61), Party);
        Check(!e.PredictedDamage(Party).Any(), "An expired Gain with no confirming snapshot retained its forbidden mask.");
    }
}
