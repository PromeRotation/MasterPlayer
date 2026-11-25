using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;

internal static class SimpleHuntSequenceRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Angada simultaneous Butcher Rip locked poses actual effect and independent instances", Angada);
        run("SimpleHunts Gamma real personal Pyretic events snapshots expiry view and inactive cleanup", Gamma);
    }

    private static void Angada()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = new ReplayCast(8191, 1, new(10, 0, 20), 0, Start, Start.AddSeconds(3.3), false, false);
        var first = Boss(1, 0x1AC0, cast);
        var second = Boss(2, 0x1AC0, cast);
        encounter.Synchronize([first, second], _ => true);
        Check(encounter.Casts.Count == 2 && encounter.Areas.Count() == 4
            && encounter.Areas.Select(c => c.AreaId).Distinct().Count() == 4, "Butcher and Rip did not coexist per instance.");
        foreach (var area in encounter.Areas)
        {
            var shape = area.Shape!.Resolve(area.Location, area.Rotation, null);
            Check(area.Activation == Start.AddSeconds(3.3)
                && shape.Contains(new(10, 0, 31.39f), 0) && !shape.Contains(new(10, 0, 31.41f), 0)
                && shape.Contains(cast.Location + new Vector3(MathF.Sin(1.04f), 0, MathF.Cos(1.04f)) * 8, 0)
                && !shape.Contains(cast.Location + new Vector3(MathF.Sin(1.05f), 0, MathF.Cos(1.05f)) * 8, 0),
                "Angada exact R11.4/120 degree geometry or shared activation changed.");
        }
        encounter.Synchronize([first with { Position = new(500, 0, 500), Rotation = 2, Cast = null }, second], _ => true);
        Check(encounter.Casts.Count == 1 && encounter.Areas.Count() == 3
            && encounter.Areas.Single(c => c.Source == 1).Location == cast.Location, "Butcher finish removed or moved its Rip.");
        encounter.Finish(2, 8191, Start.AddSeconds(1));
        Check(encounter.Casts.Count == 0 && encounter.Areas.Count() == 2, "Cancellation incorrectly removed Rip.");
        encounter.Effect(1, 8192, Start.AddSeconds(10));
        encounter.Effect(1, 8192, Start.AddSeconds(10));
        Check(encounter.Areas.Single().Source == 2, "Rip effect deleted another instance or was not idempotent.");
        encounter.Synchronize([first, second], _ => true);
        Check(encounter.Areas.Single().Source == 2, "A stale cast resurrected resolved Rip.");
        encounter.Synchronize([first, second], _ => false);
        Check(!encounter.Areas.Any(), "Disabling Angada retained Rip.");

        encounter.Reset();
        encounter.Synchronize([first with { Cast = null }], _ => true);
        encounter.Start(1, cast, true);
        encounter.Effect(1, 8192, Start.AddSeconds(1));
        encounter.Synchronize([first], _ => true);
        Check(encounter.Areas.Count() == 1 && !encounter.Areas.Single().Followup,
            "First object observation resurrected an already resolved packet Rip.");
        var newer = cast with { StartedAt = Start.AddSeconds(5), FinishAt = Start.AddSeconds(8.3), Location = Vector3.Zero };
        encounter.Start(1, newer, true);
        encounter.Effect(1, 8192, Start.AddSeconds(1));
        Check(encounter.Areas.Count() == 2 && encounter.Areas.All(c => c.Location == Vector3.Zero),
            "Old effect cleared a new Rip or a new Butcher did not replace its old geometry.");
        encounter.Remove(1);
        Check(!encounter.Areas.Any(), "Object removal leaked Rip.");
        foreach (var (action, radius) in new[] { (8190u, 9.4f), (8193u, 6f) })
        {
            encounter.Synchronize([Boss(1, 0x1AC0, cast with { ActionId = action })], _ => true);
            var area = encounter.Areas.Single();
            var shape = area.Shape!.Resolve(area.Location, area.Rotation, null);
            Check(shape.Contains(cast.Location + Vector3.UnitX * (radius - .01f), 0)
                && !shape.Contains(cast.Location + Vector3.UnitX * (radius + .01f), 0)
                && !encounter.Knockbacks(new() { Position = cast.Location + Vector3.UnitX }).Any(),
                "Angada circle changed or an unimplemented source knockback was invented.");
            encounter.Reset();
        }
    }

    private static void Gamma()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = new ReplayCast(7913, 1, Vector3.Zero, 0, Start, Start.AddSeconds(1.8), false, false);
        var boss = Boss(1, 0x1AB2, cast);
        var player = new ReplayActorState { Id = 100 };
        var other = new ReplayActorState { Id = 101 };
        ReplayPartyMember[] party = [new(0, "Tank", 100), new(1, "Healer", 101)];
        encounter.Synchronize([boss, player, other], _ => true);
        Check(encounter.Hints(player).Contains("Applies Pyretic - STOP everything until it runs out!")
            && !encounter.StopEverything(player, party, Start)
            && encounter.Casts.Single().Shape == null && encounter.PredictedDamage(party).Count() == 2,
            "Flamehook failed its global warning/prediction or prematurely stopped actions.");
        encounter.Pyretic(101, Start.AddSeconds(1), new(960, 0, 0, Start.AddSeconds(10)));
        Check(!encounter.StopEverything(player, party, Start.AddSeconds(2)), "Party member Pyretic stopped the current player.");
        encounter.Pyretic(100, Start.AddSeconds(2), new(960, 0, 0, Start.AddSeconds(10)));
        encounter.Synchronize([boss with { Cast = null }, player, other], _ => true);
        Check(encounter.StopEverything(player, party, Start.AddSeconds(3)) && !encounter.Hints(player).Any(),
            "Status-before-snapshot was lost or cast end removed personal Pyretic.");
        Check(!encounter.StopEverything(player with { Dead = true }, party, Start.AddSeconds(3))
            && !encounter.StopEverything(player with { Id = 999 }, party, Start.AddSeconds(3)),
            "Death or viewing an unaffected actor retained personal control.");
        Check(!encounter.StopEverything(player, party, Start.AddSeconds(10)), "Real status expiry failed to release Pyretic.");
        encounter.Pyretic(100, Start.AddSeconds(4), null);
        encounter.Pyretic(100, Start.AddSeconds(3), new(960, 0, 0, Start.AddSeconds(20)));
        var affected = player with { Statuses = [new(960, 0, 0, Start.AddSeconds(15))] };
        Check(!encounter.StopEverything(affected, party, Start.AddSeconds(5)), "A stale gain or stale snapshot overrode StatusLose.");
        encounter.Synchronize([boss with { Cast = null }, player, other], _ => true);
        encounter.Synchronize([boss with { Cast = null }, affected, other], _ => true);
        Check(encounter.StopEverything(affected, party, Start.AddSeconds(5)), "Existing status at module takeover was not restored.");
        encounter.Pyretic(100, Start.AddSeconds(6), new(960, 0, 1, Start.AddSeconds(20)));
        encounter.Pyretic(100, Start.AddSeconds(6), null, 0);
        var replaced = player with { Statuses = [new(960, 0, 1, Start.AddSeconds(20))] };
        encounter.Synchronize([boss with { Cast = null }, replaced, other], _ => true);
        Check(encounter.StopEverything(replaced, party, Start.AddSeconds(7)), "Old-source removal overwrote a new-source Pyretic gain at the same timestamp.");
        encounter.Pyretic(100, Start.AddSeconds(19.95), new(960, 0, 1, Start.AddSeconds(30)));
        encounter.Synchronize([boss with { Cast = null }, replaced, other], _ => true);
        Check(encounter.StopEverything(replaced, party, Start.AddSeconds(20.01)), "An old pre-refresh snapshot discarded the new status expiry.");
        encounter.Synchronize([boss with { Cast = null }, player with { Dead = true }, other], _ => true);
        encounter.Synchronize([boss with { Cast = null }, player, other], _ => true);
        Check(!encounter.StopEverything(player, party, Start.AddSeconds(21)), "Revival resurrected an unobserved gain from before death.");
        encounter.Synchronize([boss with { Cast = null }, player, other], _ => false);
        Check(!encounter.StopEverything(affected, party, Start.AddSeconds(5)), "Disabled Gamma retained personal control.");
    }

    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Hp = 1000, Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
