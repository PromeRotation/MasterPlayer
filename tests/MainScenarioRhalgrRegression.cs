using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

internal static partial class JobQuestRegression
{
    private static void RhalgrTermini()
    {
        var (e, boss, player) = MainScenario(671, 0x1A88);
        var center = boss.Position;
        DungeonActor a = new(2, 0x1BCA, center, Rotation: .3f, Targetable: false);
        DungeonActor b = new(3, 0x1BCA, center + Vector3.UnitX * 3, Rotation: 1.2f, Dead: true);
        e.Synchronize([boss, player, a, b], player.Position, Start, playerId: 100);
        var aoes = e.AmbientAoes().Where(x => x.Id.StartsWith("rhalgr/terminus/")).ToArray();
        Check(aoes.Length == 2 && aoes.All(x => x.Activation == Start.AddSeconds(10))
            && e.AmbientAoes().Count(x => x.Id.StartsWith("rhalgr/marker/")) == 1,
            "Termini create bootstrap or dead-vs-targetable marker contract.");
        e.Tick(Start.AddSeconds(1));
        Check(e.AmbientAoes().Where(x => x.Id.StartsWith("rhalgr/terminus/")).All(x => x.Activation == Start.AddSeconds(11)),
            "Termini fallback clock was frozen at creation.");
        a = a with { Position = center + Vector3.UnitZ * 4, Rotation = .8f };
        e.Synchronize([boss, player, a, b], player.Position, Start.AddSeconds(2), playerId: 100);
        Check(e.AmbientAoes().Single(x => x.Id.StartsWith("rhalgr/terminus/2/")) is { Rotation: .8f } moved
            && moved.Position == a.Position && moved.Shape is RayRectangleTemplate { MinLocalX: -2, MaxLocalX: 2, Length: 41 },
            "Termini failed dynamic position/facing.");
        var order = new DungeonCastObservation(8370, center, 0, Start.AddSeconds(3), Start.AddSeconds(6.3));
        e.StartCast(1, order, Start.AddSeconds(3));
        e.Finish(1, 8370, Start.AddSeconds(4));
        e.Tick(Start.AddSeconds(40));
        Check(e.AmbientAoes().Where(x => x.Id.StartsWith("rhalgr/terminus/")).All(x => x.Activation == order.FinishAt),
            "Termini cancellation or timeout reset shared activation.");
        e.Effect(3, 8337, 1, Start.AddSeconds(41));
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("rhalgr/terminus/")) == 1, "Termini effect did not remove matched reference.");
        e.RemoveActor(2);
        var frozen = e.AmbientAoes().Single(x => x.Id.StartsWith("rhalgr/terminus/2/"));
        a = a with { Position = center - Vector3.UnitX * 6, Rotation = 2 };
        e.Synchronize([boss, player, a, b], player.Position, Start.AddSeconds(42), playerId: 100);
        Check(e.AmbientAoes().Count(x => x.Id.StartsWith("rhalgr/terminus/2/")) == 2
            && e.AmbientAoes().Any(x => x.Id == frozen.Id && x.Position == frozen.Position),
            "Termini destroyed old reference rebound to recreated ID.");
        e.Effect(2, 8337, 2, Start.AddSeconds(43));
        Check(e.AmbientAoes().Single(x => x.Id.StartsWith("rhalgr/terminus/2/")).Id == frozen.Id,
            "New generation effect deleted old frozen Terminus.");
    }

    private static void RhalgrVoidzones()
    {
        var (e, boss, player) = MainScenario(671, 0x1A88);
        var center = boss.Position;
        player = player with { Position = center + Vector3.UnitX * 5 };
        DungeonActor poison = new(4, 0x1EA4D9, center + Vector3.UnitX * 15, EventState: 7, Dead: true, Targetable: false);
        var gunblade = new DungeonCastObservation(8310, center, 0, Start, Start.AddSeconds(5), 999);
        e.Synchronize([boss with { Cast = gunblade }, player, poison], player.Position, Start, playerId: 100);
        Check(e.AmbientAoes().Any(x => x.Id == "rhalgr/kb/avoid/4")
            && !e.Hints(player.Position).Contains("About to be knocked into danger!")
            && !e.AmbientAoes().Any(x => x.Id == "trial/voidzone/live/4"),
            "Rhalgr state7 poison must affect navigation but not destination danger.");
        DungeonActor helper = new(2, 0x1A57, center,
            Cast: new(8346, poison.Position, 0, Start, Start.AddSeconds(3)));
        e.Synchronize([boss with { Cast = gunblade }, player, poison, helper], player.Position, Start.AddSeconds(1), playerId: 100);
        Check(e.Hints(player.Position).Contains("About to be knocked into danger!"), "Gunblade landing ignored cast prediction.");
        e.StartCast(2, helper.Cast! with { Location = center - Vector3.UnitX * 15 }, Start.AddSeconds(1.1));
        Check(e.AmbientAoes().Single(x => x.Id == "trial/voidzone/cast/2").Position == center - Vector3.UnitX * 15
            && !e.Hints(player.Position).Contains("About to be knocked into danger!"), "Rhalgr packet poison location was not authoritative.");
        boss = boss with { Position = center + Vector3.UnitZ * 3, Cast = gunblade };
        e.Synchronize([boss, player, poison, helper], player.Position, Start.AddSeconds(1.2), playerId: 100);
        Check(Vector3.Distance(e.AmbientAoes().Single(x => x.Id == "rhalgr/kb/avoid/4").Position, center) < .04f,
            "Gunblade actor movement rebound locked source.");
        Check(e.AmbientAoes().Single(x => x.Id == "trial/voidzone/cast/2").Position == center - Vector3.UnitX * 15
            && !e.Hints(player.Position).Contains("About to be knocked into danger!"),
            "Unchanged stale object scan reverted accepted Rhalgr packet location.");
        helper = helper with { Cast = helper.Cast! with { Location = center + Vector3.UnitX * 15 + Vector3.UnitZ * 2 } };
        e.Synchronize([boss, player, poison, helper], player.Position, Start.AddSeconds(1.3), playerId: 100);
        Check(e.AmbientAoes().Single(x => x.Id == "trial/voidzone/cast/2").Position == helper.Cast.Location,
            "Real subsequent CastInfo location change failed to replace packet correction.");
        e.Effect(2, 8346, 1, Start.AddSeconds(2), location: poison.Position, occurredAt: Start.AddSeconds(1));
        e.Finish(2, 8346, Start.AddSeconds(2));
        Check(e.AmbientAoes().Single(x => x.Id.StartsWith("trial/voidzone/event/")).Activation == Start.AddSeconds(2.5),
            "Rhalgr event poison used packet occurrence instead of consumption clock plus .5.");
        poison = poison with { EventState = 0 };
        e.Synchronize([boss, player, poison], player.Position, Start.AddSeconds(3), playerId: 100);
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("trial/voidzone/event/"))
            && e.AmbientAoes().Any(x => x.Id == "trial/voidzone/live/4")
            && e.Hints(player.Position).Contains("About to be knocked into danger!"),
            "Rhalgr live poison failed individual prediction consumption or landing warning.");
        e.StatusChanged(100, 160, true, Start.AddSeconds(3.1), expiresAt: gunblade.FinishAt.AddTicks(1));
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("rhalgr/kb/"))
            && e.AmbientAoes().Any(x => x.Id == "trial/voidzone/live/4"), "Rhalgr immunity hid live poison or retained KB.");
        e.Finish(1, 8310, Start.AddSeconds(3.2));
        e.StatusChanged(100, 160, false, Start.AddSeconds(3.3));
        Check(!e.AmbientAoes().Any(x => x.Id.StartsWith("rhalgr/kb/")), "Rhalgr cancelled Gunblade retained KB.");
    }
}
