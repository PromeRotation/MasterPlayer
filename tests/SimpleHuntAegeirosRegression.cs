using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntAegeirosRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Aegeiros Leafstorm Rimestorm dynamic poses observed clock corrections and actual ending", Sequence);
        run("SimpleHunts Aegeiros permanent Canopy outline target specific R100 cones all cast gates and cleanup", Cleave);
    }

    private static void Sequence()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(null);
        encounter.Synchronize([boss], _ => true, Start);
        var leaf = Cast(27708, 0, 6);
        encounter.Start(1, leaf, true, observedAt: Start.AddSeconds(2));
        var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
        Check(areas.Length == 2 && areas.Single(a => a.Shape is CircleTemplate).Activation == leaf.FinishAt
            && areas.Single(a => a.Shape is FanTemplate).Activation == Start.AddSeconds(11.6d),
            "Leafstorm did not create both live shapes or double-counted a cast delay.");
        encounter.Start(1, leaf with { Rotation = .3f }, true, observedAt: Start.AddSeconds(3));
        encounter.Synchronize([boss with { Position = new(10, 0, 20), Cast = leaf with { Rotation = .3f } }], _ => true, Start.AddSeconds(4));
        areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
        Check(areas.Length == 2 && areas.All(a => a.Location == new Vector3(10, 0, 20))
            && areas.Single(a => a.Shape is FanTemplate).Activation == Start.AddSeconds(11.6d),
            "Packet correction/observation changed the observed prediction clock or fixed its origin.");
        encounter.Finish(1);
        encounter.Synchronize([boss with { Position = new(30, 0, 40), Rotation = 1.2f }], _ => true, Start.AddSeconds(20));
        var expected = encounter.Areas.Single(a => a.Shape != null);
        Check(expected.Shape is FanTemplate { Radius: 40, AngleRadians: var angle } && angle == MathF.PI
            && expected.Location == new Vector3(30, 0, 40) && expected.Rotation == 1.2f,
            "Leaf finish/time expiry consumed its predicted half-plane or lost the live actor rotation.");
        encounter.Start(1, Cast(27710, 30, 3) with { Rotation = .8f }, true);
        Check(encounter.Areas.Single(a => a.Shape is FanTemplate).Rotation == .8f,
            "Predicted Rimestorm ignored another current cast's direction.");
        encounter.Finish(1);
        var rime = Cast(27709, 40, 1) with { Rotation = 2.2f };
        encounter.Start(1, rime, true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 1
            && encounter.Areas.Single(a => a.Shape != null).Activation == rime.FinishAt,
            "Actual Rimestorm duplicated the prediction or retained the predicted activation.");
        encounter.Finish(1, 27709);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Actual Rimestorm finish retained a predicted cone.");
        encounter.Start(1, Cast(27709, 50, 1), true);
        Check(encounter.Areas.Single(a => a.Shape != null).Shape is FanTemplate,
            "An actual Rimestorm without history failed to draw.");
    }

    private static void Cleave()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(null) with { TargetId = 100 };
        var target = new ReplayActorState { Id = 100, Position = new(0, 0, 5) };
        var ally = new ReplayActorState { Id = 101, Position = new(0, 0, 7) };
        var distant = new ReplayActorState { Id = 102, Position = new(1000, 0, 0) };
        var party = new ReplayPartyMember[] { new(0, "Tank", 100), new(8, "Alliance", 101), new(24, "NPC", 102) };
        encounter.Synchronize([boss, target, ally, distant], _ => true);
        var preview = encounter.TargetPreviews(target).Single();
        Check(preview.OutlineOnly && preview.Shape is FanTemplate { Radius: 12, AngleRadians: var angle }
            && angle == 2 * MathF.PI / 3 && preview.Activation == default,
            "Canopy required its no-cast action event or lost its real outline.");
        Check(!encounter.TargetHints(target, party).Any() && encounter.TargetHints(ally, party).Contains("GTFO from cleave!"),
            "Canopy lost its main-target hint exemption or quantized ally hit hint.");
        var personal = encounter.TargetDangers(target, party).ToArray();
        Check(personal.Length == 2 && personal.All(a => a.Shape is FanTemplate { Radius: 100 })
            && personal.Any(a => MathF.Abs(a.Rotation - MathF.PI / 2) < .0001f),
            "Canopy main-target navigation failed to avoid every living ally including distant/NPC members.");
        Check(encounter.TargetDangers(ally, party).Single().Shape is FanTemplate { Radius: 12 },
            "Canopy non-target navigation used the main target's R100 cones.");
        encounter.Synchronize([boss with { TargetId = 101 }, target, ally with { Dead = true }, distant], _ => true);
        Check(encounter.TargetPreviews(target).Any()
            && encounter.TargetDangers(target, party).Single().Shape is FanTemplate { Radius: 12 },
            "A dead current target removed Canopy or target replacement retained the former personal strategy.");
        encounter.Synchronize([boss with { Cast = Cast(999, 0, 5) }, target, ally, distant], _ => true);
        Check(!encounter.TargetPreviews(target).Any(), "An unlisted cast did not hide Canopy.");
        encounter.Synchronize([boss with { Cast = Cast(27712, 0, 5) with { Rotation = null } }, target, ally, distant], _ => true);
        Check(!encounter.TargetPreviews(target).Any() && !encounter.TargetDangers(target, party).Any(),
            "A known directional cast rejected for missing facing incorrectly restored idle Canopy.");
        encounter.Synchronize([boss, target, ally, distant], _ => true);
        encounter.Start(1, Cast(27710, 10, 3), true);
        Check(!encounter.TargetPreviews(target).Any(), "Packet-before-object cast did not hide Canopy.");
        encounter.Finish(1);
        Check(encounter.TargetPreviews(target).Any(), "Actual cast finish did not restore the constant cleave.");
        encounter.Synchronize([boss with { Targetable = false }, target, ally, distant], _ => true);
        Check(!encounter.TargetPreviews(target).Any(), "Untargetable boss retained Canopy.");
        encounter.Synchronize([boss, target, ally, distant], _ => false);
        Check(!encounter.TargetDangers(target, party).Any(), "Disabled Aegeiros retained Canopy navigation.");
    }

    private static ReplayCast Cast(uint action, double at, double duration)
        => new(action, 1, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + duration + .3), true, false);
    private static ReplayActorState Boss(ReplayCast? cast)
        => new() { Id = 1, Oid = 0x3671, Position = Vector3.Zero, Targetable = true, InCombat = true, Cast = cast };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
