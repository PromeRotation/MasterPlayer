using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntTargetRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);
    private static readonly ReplayPartyMember[] Party = [new(0, "Target", 100), new(8, "Alliance", 101), new(24, "NPC", 102)];

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Supay and Mudman target baits self ally avoidance live targets and source predictions", Baits);
        run("SimpleHunts target bait missing destroyed ID reuse packet and observed finish lifecycle", TargetLifecycle);
        run("SimpleHunts Petalodus spread exact R6 drawing R7 personal navigation dead filtering and pure interrupt", Spreads);
        run("SimpleHunts concurrent spread targets avoid each other once and single ally gate", MultipleSpreads);
        run("SimpleHunts LilMurderer exact self ground conditional delayed slash effect clear and spread20", LilMurderer);
        run("SimpleHunts Nuckelavee target-snapshotted Torpedo until actual main target and Spite effect", Nuckelavee);
    }

    private static void Baits()
    {
        foreach (var (oid, action, radius) in new[] { (0x2839u, 17858u, 8f), (0x281Fu, 16829u, 6f) })
        {
            var encounter = new SimpleHuntEncounter();
            var target = new ReplayActorState { Id = 100, Position = Vector3.Zero };
            var ally = new ReplayActorState { Id = 101, Position = new(20, 0, 20) };
            var npc = new ReplayActorState { Id = 102, Position = new(40, 0, 40) };
            var boss = Boss(1, oid, Cast(action));
            encounter.Synchronize([boss, target, ally, npc], _ => true);
            var preview = encounter.TargetPreviews(target).Single();
            Check(preview.Shape is CircleTemplate { Radius: var drawn } && drawn == radius && preview.OutlineOnly,
                "Own bait lost its exact outline.");
            var selfDangers = encounter.TargetDangers(target, Party).ToArray();
            Check(selfDangers.Length == 2 && selfDangers.All(a => a.Location != target.Position)
                && selfDangers.Any(a => a.Location == ally.Position) && selfDangers.Any(a => a.Location == npc.Position),
                "Bait target fled its own moving circle or failed to avoid alliance/NPC allies.");
            var otherDanger = encounter.TargetDangers(ally, Party).Single();
            Check(otherDanger.Location == target.Position && !encounter.TargetPreviews(ally).Single().OutlineOnly,
                "Other players lost the filled target bait and its navigation.");
            Check(encounter.PredictedDamage(Party).Single() is { TargetId: 100, DamageType: "Raidwide" },
                "Bait default source prediction became full-party damage or lost its target.");
            Check(encounter.Facing(target, Start.AddSeconds(4)) == null,
                "The Howl bait hint invented an actual gaze constraint.");
            target = target with { Position = ally.Position, Dead = true };
            encounter.Synchronize([boss, target, ally, npc], _ => true);
            Check(encounter.TargetPreviews(ally).Single().Location == target.Position
                && encounter.TargetHints(target, Party).Any(h => h.StartsWith("Bait away")),
                "Bait did not follow its moved/dead target or lost the target-specific hint.");
            encounter.Finish(1, action);
            Check(!encounter.TargetPreviews(target).Any() && !encounter.TargetDangers(target, Party).Any(),
                "Observed bait finish retained navigation/drawing.");
        }
    }

    private static void TargetLifecycle()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x2839, null);
        var cast = Cast(17858);
        var target = new ReplayActorState { Id = 100, Position = new(30, 0, 40) };
        encounter.Synchronize([boss], _ => true);
        encounter.Start(1, cast, true);
        Check(!encounter.TargetPreviews(target).Any(), "An unknown target invented a bait at zero.");
        encounter.Synchronize([boss, target], _ => true);
        Check(!encounter.TargetPreviews(target).Any(), "A later target bound without a first cast observation.");
        encounter.Synchronize([boss with { Cast = cast }, target], _ => true);
        Check(encounter.TargetPreviews(target).Single().Location == target.Position,
            "Packet-before-object cast failed to acquire its first observed target.");
        encounter.Remove(100);
        encounter.Start(1, cast with { Rotation = .2f }, true);
        encounter.Synchronize([boss with { Cast = cast }, target with { Position = new(70, 0, 80) }], _ => true);
        Check(encounter.TargetPreviews(target).Single().Location == target.Position,
            "A target bait rebound to a replacement actor with the same entity ID.");
        var replacement = target with { Position = new(70, 0, 80) };
        Check(encounter.TargetDangers(replacement, Party).Single().Location == target.Position
            && !encounter.TargetHints(replacement, Party).Any(h => h.StartsWith("Bait away")),
            "A replacement actor inherited the old target's self-navigation or personal bait hint.");
        encounter.Synchronize([boss, target], _ => true);
        Check(!encounter.TargetPreviews(target).Any(), "The real observed cast ending retained the bait.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = cast }, target], _ => true);
        encounter.Synchronize([boss with { Cast = cast }, target], _ => false);
        Check(!encounter.TargetPreviews(target).Any(), "Disabled bait retained output.");
    }

    private static void Spreads()
    {
        var encounter = new SimpleHuntEncounter();
        var target = new ReplayActorState { Id = 100, Position = new(10, 0, 20) };
        var ally = new ReplayActorState { Id = 101, Position = new(30, 0, 40) };
        var boss = Boss(1, 0x35FB, Cast(27067));
        encounter.Synchronize([boss, target, ally], _ => true);
        var preview = encounter.TargetPreviews(target).Single();
        Check(preview.Shape is CircleTemplate { Radius: 6 } && preview.OutlineOnly,
            "Spread preview lost R6 or its outline.");
        var own = encounter.TargetDangers(target, Party).Single();
        var other = encounter.TargetDangers(ally, Party).Single();
        Check(own.Shape is CircleTemplate { Radius: 7 } && other.Shape is CircleTemplate { Radius: 7 }
            && Vector3.Distance(own.Location, ally.Position) < .04f && Vector3.Distance(other.Location, target.Position) < .04f,
            "Spread self/other R7 navigation or quantized positions changed.");
        Check(!encounter.PredictedDamage(Party).Any(), "Source spread unexpectedly created predicted damage.");
        encounter.Synchronize([boss, target with { Dead = true }, ally], _ => true);
        Check(!encounter.TargetPreviews(ally).Any() && !encounter.TargetDangers(ally, Party).Any(),
            "Dead spread targets retained output.");
        encounter.Synchronize([boss, target, ally], _ => true);
        Check(encounter.TargetPreviews(ally).Any(), "A revived spread target did not restore its still-active marker.");
        encounter.Start(1, Cast(27063, 10), true);
        Check(!encounter.Areas.Any(a => a.Shape != null) && !encounter.PredictedDamage(Party).Any()
            && encounter.Hints(target).Contains("Interrupt! (Raidwide x3)"),
            "MarineMayhem created an invented R40 danger or three fabricated damage clocks.");
    }

    private static void MultipleSpreads()
    {
        var encounter = new SimpleHuntEncounter();
        var target = new ReplayActorState { Id = 100, Position = Vector3.Zero };
        var other = new ReplayActorState { Id = 101, Position = new(20, 0, 20) };
        var ally = new ReplayActorState { Id = 102, Position = new(40, 0, 40) };
        var first = Boss(1, 0x35FB, Cast(27067));
        var second = Boss(2, 0x35FB, Cast(27067) with { TargetId = 101 });
        encounter.Synchronize([first, second, target, other, ally], _ => true);
        var danger = encounter.TargetDangers(target, Party).ToArray();
        Check(danger.Length == 2 && danger.Select(a => a.Location).Distinct().Count() == 2,
            "Spread target double-counted other spread targets instead of avoiding each once.");
        encounter.Synchronize([first, second, target, other with { Dead = true }, ally with { Dead = true }], _ => true);
        Check(!encounter.TargetDangers(target, Party).Any(), "A lone living spread target generated ally avoidance.");
        encounter.Reset();
        encounter.Synchronize([first, second, target, other, ally], _ => true);
        encounter.Remove(101);
        encounter.Synchronize([first, second, target, other with { Position = new(60, 0, 60) }, ally], _ => true);
        Check(encounter.TargetDangers(target, Party).Count() == 3,
            "A replacement ally inherited a removed spread target's exemption.");
    }

    private static void LilMurderer()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Cast(17492);
        var boss = Boss(1, 0x28B4, cast) with { Position = new(0, 5, 0) };
        var target = new ReplayActorState { Id = 100 };
        var ally = new ReplayActorState { Id = 101, Position = new(50, 0, 50) };
        encounter.Synchronize([boss, target, ally], _ => true);
        var areas = encounter.Areas.Where(a => a.Shape != null).ToArray();
        Check(areas.Length == 2 && areas.Single(a => a.SequenceId != null).Activation == cast.FinishAt.AddSeconds(2.6),
            "Exact XZ self-targeted thunder failed to predict Slash at Finish+2.6.");
        encounter.Synchronize([boss with { Cast = null, Position = new(30, 0, 40) }, target, ally], _ => true);
        Check(encounter.Areas.Single(a => a.Shape != null).Location == Vector3.Zero, "Slash moved with the boss or ended with thunder.");
        encounter.Effect(1, 17489, Start.AddSeconds(8), 1);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Actual Slash did not clear its conditional prediction.");
        encounter.Reset();
        encounter.Synchronize([boss with { Cast = cast with { Location = new(.001f, 0, 0) } }, target, ally], _ => true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 1, "A near-self thunder guessed a Slash despite exact source equality.");
        encounter.Start(1, Cast(17493, 10), true);
        Check(encounter.TargetPreviews(target).Single().Shape is CircleTemplate { Radius: 20 }
            && encounter.TargetDangers(target, Party).Single().Shape is CircleTemplate { Radius: 21 },
            "GobthunderIII spread drawing/navigation radii changed.");
        encounter.Start(1, Cast(17488, 20), true);
        Check(encounter.PredictedDamage(Party).Single() is { TargetId: 100, DamageType: "Tankbuster" },
            "GoblinPunch lost its single-target source prediction.");
        encounter.Start(1, Cast(17491, 30), true);
        Check(encounter.Hints(target).Contains("Attack speed buff") && !encounter.PredictedDamage(Party).Any(),
            "Gobhaste lost its pure hint or invented damage.");
        encounter.Reset();
        encounter.Synchronize([boss with { Position = Vector3.Zero, Cast = null }, target], _ => true);
        encounter.Start(1, cast, true);
        encounter.Synchronize([boss with { Position = Vector3.Zero }, target], _ => true);
        encounter.Start(1, cast with { Location = new(.001f, 0, 0) }, true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 1, "A true-to-false corrected self condition retained Slash.");
        encounter.Start(1, cast, true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 2, "A false-to-true corrected self condition failed to add Slash.");
        encounter.Effect(1, 17489, Start.AddSeconds(8), 1);
        encounter.Start(1, cast with { Rotation = .3f }, true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 1, "A corrected packet resurrected an effect-consumed Slash.");
        encounter.Synchronize([boss with { Position = Vector3.Zero, Cast = cast with { Rotation = .3f } }, target], _ => true);
        Check(encounter.Areas.Count(a => a.Shape != null) == 2,
            "Actual changed raw CastInfo failed to start the source Slash prediction again.");
        encounter.Finish(1);
        encounter.Start(1, Cast(17492, 20), true);
        encounter.Effect(1, 17489, Start.AddSeconds(8), 2);
        Check(encounter.Areas.Count(a => a.Shape != null) == 2, "A late old Slash effect consumed a newer prediction.");
    }

    private static void Nuckelavee()
    {
        var encounter = new SimpleHuntEncounter();
        var boss = Boss(1, 0x288F, null) with { TargetId = 100 };
        var target = new ReplayActorState { Id = 100 };
        var ally = new ReplayActorState { Id = 101, Position = new(30, 0, 40) };
        encounter.Synchronize([boss, target, ally], _ => true);
        var torpedo = Cast(16964) with { TargetId = 1 };
        encounter.Start(1, torpedo, true);
        encounter.Finish(1, 16964);
        encounter.Synchronize([boss with { TargetId = 101 }, target, ally], _ => true);
        Check(encounter.PredictedDamage(Party).Single() is { TargetId: 100, DamageType: "Tankbuster" }
            && encounter.Hints(target).Contains("Tankbuster"), "Torpedo cast ending/current-target change lost its snapshotted pending damage.");
        encounter.Effect(1, 16964, Start.AddSeconds(6), 1, 101);
        Check(encounter.PredictedDamage(Party).Count() == 1, "Torpedo effect on a different main target consumed the pending target.");
        encounter.Start(1, Cast(16964, 10) with { TargetId = 101 }, true);
        encounter.Finish(1, 16964);
        Check(encounter.PredictedDamage(Party).Count() == 2, "A later Torpedo replaced an unresolved earlier target.");
        encounter.Effect(1, 16964, Start.AddSeconds(16), 2, 100);
        Check(encounter.PredictedDamage(Party).Single().TargetId == 101, "Torpedo main-target effect removed the wrong queued target.");
        encounter.Effect(1, 16964, Start.AddSeconds(17), 3, 101);
        Check(!encounter.PredictedDamage(Party).Any() && !encounter.Hints(target).Contains("Tankbuster"), "Resolved Torpedo retained damage/hint.");
        var gallop = Cast(16967, 20) with { Location = new(50, 0, 60) };
        encounter.Start(1, gallop, true);
        encounter.Finish(1, 16967);
        encounter.Synchronize([boss with { Position = new(100, 0, 100) }, target, ally], _ => true);
        var spite = encounter.Areas.Single(a => a.Shape != null);
        Check(spite.Location == gallop.Location && spite.Activation == gallop.FinishAt, "Gallop finish cleared/moved Spite or changed its clock.");
        encounter.Effect(1, 18037, Start.AddSeconds(26), 4);
        Check(!encounter.Areas.Any(a => a.Shape != null), "Actual Spite did not clear its prediction.");
        encounter.Start(1, Cast(16965, 30), true);
        Check(encounter.TargetPreviews(target).Single().Shape is CircleTemplate { Radius: 5 }
            && encounter.TargetDangers(target, Party).Single().Shape is CircleTemplate { Radius: 6 },
            "BogBody spread radii changed.");
        encounter.Reset();
        encounter.Synchronize([boss, target, ally], _ => true);
        encounter.Start(1, Cast(16964), true);
        encounter.Effect(1, 16964, Start.AddSeconds(1), 5, 100);
        encounter.Synchronize([boss with { Cast = Cast(16964) }, target, ally], _ => true);
        Check(!encounter.PredictedDamage(Party).Any(), "First object observation resurrected a Torpedo already consumed by its actual effect.");
        encounter.Reset();
        encounter.Synchronize([boss, target, ally], _ => true);
        encounter.Start(1, torpedo, true);
        encounter.Synchronize([boss with { Cast = torpedo, TargetId = 101 }, target, ally], _ => true);
        Check(encounter.PredictedDamage(Party).Single().TargetId == 100,
            "A Torpedo first object observation reselected the boss's new current target.");
        encounter.Start(1, torpedo with { Rotation = .2f }, true);
        Check(encounter.PredictedDamage(Party).Single().TargetId == 100,
            "A Torpedo clock/pose correction reselected a self-target fallback.");
        encounter.Synchronize([boss with { Cast = torpedo with { Rotation = .2f }, TargetId = 101 }, target, ally], _ => true);
        Check(encounter.PredictedDamage(Party).Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 101 }),
            "Actual changed raw CastInfo failed to append a new Torpedo using the current boss target.");
        encounter.Synchronize([boss with { Cast = torpedo with { Rotation = .2f }, TargetId = 101 }, ally], _ => true);
        encounter.Start(1, torpedo with { Rotation = .3f }, true);
        Check(encounter.PredictedDamage(Party).Select(d => d.TargetId).Order().SequenceEqual(new ulong[] { 100, 101 }),
            "A packet correction discarded an unresolved historical Torpedo target.");
        encounter.Effect(1, 16964, Start.AddSeconds(6), 6, 100);
        encounter.Synchronize([boss with { Cast = torpedo with { Rotation = .3f }, TargetId = 101 }, target, ally], _ => true);
        Check(encounter.PredictedDamage(Party).Count() == 2 && encounter.PredictedDamage(Party).All(d => d.TargetId == 101),
            "Actual changed raw CastInfo did not append the next target independently of the consumed old target.");
        encounter.Reset();
        encounter.Synchronize([boss, target, ally], _ => true);
        encounter.Start(1, Cast(16964, 10), true);
        encounter.Finish(1);
        encounter.Start(1, Cast(16964, 20), true);
        encounter.Effect(1, 16964, Start.AddSeconds(10), 2, 100);
        Check(encounter.PredictedDamage(Party).Count() == 1
            && encounter.PredictedDamage(Party).Single().Activation == Cast(16964, 20).FinishAt,
            "An old Torpedo event consumed a newer unresolved target.");
    }

    private static ReplayCast Cast(uint action, double start = 0)
        => new(action, 100, Vector3.Zero, 0, Start.AddSeconds(start), Start.AddSeconds(start + 5.3), true, false) { TotalTime = 5 };
    private static ReplayActorState Boss(ulong id, uint oid, ReplayCast? cast)
        => new() { Hp = 1000, Id = id, Oid = oid, InCombat = true, Targetable = true, Cast = cast };
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
