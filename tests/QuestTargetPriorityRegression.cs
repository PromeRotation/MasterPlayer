using System.Numerics;
using MasterPlayer.Mechanics.Quests;
using MasterPlayer.Replay;

internal static class QuestTargetPriorityRegression
{
    internal static void Run(Action<string, Action> run)
    {
        run("HW quest target priority four territories exact OIDs and inclusive AST bounds", Oids);
        run("HW quest target priority legal enemy slots and passive targets", Candidates);
        run("HW quest target priority current target tie selection and release", Selection);
        run("HW quest target priority recording preserves native classification index deltas and reverse rebuild", Recording);
        run("ARR quest target priorities override passive pending and height defaults with zero priority fallback", ArrPriorities);
        run("Quest target priority added zones preserve zero fallback and Pointless setter protection", AddedZones);
        run("Quest target priority partial overrides preserve enmity party invincibility Fate and sticky Pointless", Defaults);
        run("Quest target priority explicit zero negative and unmatched assignments preserve source partial setters", ExplicitAssignments);
        run("Quest target priority Sollerets and Perplexity preserve scalar overload 677 and Pointless protection", ScalarAssignments);
    }

    private static ReplayActorState Enemy(ulong id, uint oid) => new()
    {
        Id = id, Oid = oid, Kind = 2, ObjectIndex = 2, Targetable = true, Hostile = true, IsEnemy = true, Hp = 100,
    };

    private static void ScalarAssignments()
    {
        foreach (var (territory, oid, priority) in new (uint, uint, int)[] { (498, 0x11D2, 5), (267, 0x2A4, 677) })
        {
            var specified = Enemy(1, oid);
            var unmatched = Enemy(2, 0x2A5);
            Check(QuestTargetPriority.OwnsTerritory(territory) && QuestTargetPriority.PartialOverride(territory)
                && QuestTargetPriority.Priority(territory, specified) == priority
                && QuestTargetPriority.Select(territory, [specified, unmatched with { AggroPlayer = true }], 2, default) == 1,
                "Scalar OID/priority overload or actual territory mapping changed.");
            Check(QuestTargetPriority.Select(territory, [unmatched], 0, default) == 0
                && QuestTargetPriority.Select(territory, [unmatched with { AggroPlayer = true }], 0, default) == 2,
                "The scalar priority argument was interpreted as a second OID or replaced the fallback base.");
            Check(QuestTargetPriority.Select(territory, [specified with { Hp = 1 }], 0, default) == 0
                && QuestTargetPriority.Select(territory, [specified with { ObjectIndex = 3 }], 0, default) == 0,
                "Explicit positive assignment bypassed Pointless or native object-slot eligibility.");
        }
    }

    private static void ExplicitAssignments()
    {
        foreach (var (territory, oid) in new (uint, uint)[] { (317, 0x6B1), (314, 0x815), (323, 0x5EC) })
        {
            var specified = Enemy(1, oid);
            var ordinary = Enemy(2, 0xFFFF);
            Check(QuestTargetPriority.OwnsTerritory(territory) && QuestTargetPriority.PartialOverride(territory),
                "New quest did not register its actual territory and partial override.");
            Check(QuestTargetPriority.Select(territory, [specified, ordinary], 2, default) == 1,
                "Explicit zero or five did not override a passive candidate's negative base priority.");
            Check(QuestTargetPriority.Select(territory, [ordinary], 0, default) == 0,
                "An unmatched passive target received the explicit zero override.");
            Check(QuestTargetPriority.Select(territory, [ordinary with { AggroPlayer = true }], 0, default) == 2,
                "An unmatched enemy lost its enmity base priority.");
            Check(QuestTargetPriority.Select(territory, [specified with { Hp = 1 }], 0, default) == 0,
                "Explicit assignment overrode Pointless setter protection.");
            Check(QuestTargetPriority.Select(territory, [specified with { Position = new(0, 13, 0), FateId = 55,
                Statuses = [new(671, 0, 1, DateTime.MaxValue)] }], 0, default) == 1,
                "Explicit zero failed to override source height/Fate/invincibility defaults.");
            if (territory != 323)
                Check(QuestTargetPriority.Select(territory, [specified, ordinary with { AggroPlayer = true }], 2, default) == 2,
                    "Explicit zero incorrectly became a positive priority over the current ordinary target.");
        }
        var excluded = Enemy(1, 0x6E9) with { AggroPlayer = true };
        var favored = Enemy(2, 0x6EA);
        var fallback = Enemy(3, 0xFFFF) with { AggroPlayer = true };
        Check(QuestTargetPriority.Select(307, [excluded], 1, default) == 0
            && QuestTargetPriority.ExplicitlyPointless(307, excluded),
            "Explicit -1 was treated as an unmatched setter or an attackable zero.");
        Check(QuestTargetPriority.Select(307, [excluded, favored, fallback], 1, default) == 2
            && QuestTargetPriority.Select(307, [excluded, fallback], 1, default) == 3,
            "The Lominsan Way lost its priority-one target or ordinary legal fallback.");
        Check(!QuestTargetPriority.ExplicitlyPointless(307, excluded with { IsEnemy = false })
            && !QuestTargetPriority.ExplicitlyPointless(307, excluded with { ObjectIndex = null }),
            "An allied or unknown object slot was marked for clearing by an OID coincidence.");
    }

    private static void Oids()
    {
        foreach (var (territory, oid) in new (uint, uint)[] { (476, 0x1268), (476, 0x126D), (488, 0x1236), (484, 0x11E4), (466, 0x1231) })
        {
            Check(QuestTargetPriority.OwnsTerritory(territory) && QuestTargetPriority.Priority(territory, Enemy(1, oid)) == 5, "Missing quest target priority.");
            Check(QuestTargetPriority.Select(territory, [Enemy(1, 1), Enemy(2, oid)], 1, default) == 2, "An ordinary target displaced priority five.");
        }
        foreach (var oid in new uint[] { 0x1267, 0x126E })
            Check(QuestTargetPriority.Priority(476, Enemy(1, oid)) == 0, "AST target range leaked outside inclusive boundaries.");
        Check(QuestTargetPriority.Select(999, [Enemy(1, 0x1231)], 0, default) == 0, "Quest priorities leaked to another territory.");
    }

    private static void Candidates()
    {
        var enemy = Enemy(1, 0x1231);
        Check(QuestTargetPriority.Select(466, [enemy], 0, default) == 1, "Passive priority target incorrectly required combat.");
        Check(QuestTargetPriority.Select(466, [enemy with { Hostile = false }], 0, default) == 1,
            "The Hostile flag overrode native target classification.");
        foreach (var invalid in new[] { enemy with { Dead = true }, enemy with { IsEnemy = false }, enemy with { IsEnemy = null }, enemy with { Targetable = false },
            enemy with { Kind = 1 }, enemy with { ObjectIndex = 201 }, enemy with { ObjectIndex = 3 }, enemy with { ObjectIndex = null },
            enemy with { Id = 0 }, enemy with { Id = 0xE0000000 } })
            Check(QuestTargetPriority.Select(466, [invalid], 0, default) == 0, "An illegal candidate produced a target request.");
    }

    private static void Selection()
    {
        var near = Enemy(1, 0x1231);
        var far = Enemy(2, 0x1231) with { Position = new(10, 0, 0) };
        Check(QuestTargetPriority.Select(466, [near, far], 2, Vector3.Zero) == 2, "Equal priority changed a valid current target.");
        Check(QuestTargetPriority.Select(466, [far, near], 0, Vector3.Zero) == 1, "Unselected equal-priority targets did not use distance.");
        Check(QuestTargetPriority.Select(466, [], 2, default) == 0, "Missing target retained its request.");
        Check(QuestTargetPriority.Select(466, [Enemy(3, 1)], 3, default) == 0, "Unspecified ordinary priority was overwritten.");
    }

    private static void ArrPriorities()
    {
        var low = Enemy(1, 0x224) with { Position = new(0, 100, 0), Hp = 0 };
        var middle = Enemy(2, 0x376);
        var high = Enemy(3, 0x999);
        Check(QuestTargetPriority.OwnsTerritory(233) && QuestTargetPriority.OwnsTerritory(252),
            "ARR CFC mapping did not register the actual territories.");
        Check(QuestTargetPriority.Select(233, [low, middle, high], 1, default) == 3,
            "An unlisted OID did not receive Chasing Shadows priority two.");
        Check(QuestTargetPriority.Select(233, [low, middle], 1, default) == 2,
            "Priority one did not override the current priority zero target.");
        Check(QuestTargetPriority.Select(233, [low], 0, default) == 1,
            "Zero HP, height or passive defaults prevented a legal source priority zero target.");
        Check(QuestTargetPriority.Select(233, [low with { Dead = true }, middle with { IsEnemy = false }], 0, default) == 0,
            "ARR priority overrides admitted dead or allied objects.");
        var grotto = Enemy(4, 0x386);
        Check(QuestTargetPriority.Select(252, [grotto, high], 4, default) == 3,
            "Lurkers failed to prioritize all other legal OIDs.");
        Check(QuestTargetPriority.Select(252, [grotto, high with { Targetable = false }], 0, default) == 4,
            "Lurkers priority zero fallback was treated as forbidden.");
        Check(QuestTargetPriority.Select(252, [grotto with { ObjectIndex = null }], 0, default) == 0,
            "Old snapshots guessed an enemy object slot.");
    }

    private static void Recording()
    {
        var actor = Enemy(1, 0x1231) with { Hostile = false, FateId = 123 };
        var initial = new ReplayWorldFrame { Full = true, Territory = 466, Actors = [actor], AllowedFateId = 123 };
        var changed = initial with { Full = false, AllowedFateId = 0, Actors = [actor with { IsEnemy = false, ObjectIndex = 4, FateId = 124 }] };
        var delta = ReplayWorldFrame.Delta(initial, changed);
        Check(delta.Actors.Length == 1 && delta.Actors[0].ObjectIndex == 4, "Native classification or index change was omitted from the actor delta.");
        using var stream = new MemoryStream();
        using var writer = new ReplayRecordingWriter(stream);
        writer.WriteHeader(new(2, 466, DateTime.UnixEpoch, "quest-targets"));
        writer.WriteWorldFrame(0, initial);
        writer.WriteWorldFrame(1, delta);
        stream.Position = 0;
        var loaded = ReplayRecordingReader.Load(stream);
        var playback = new ReplayPlayback(loaded);
        playback.AdvanceTo(0, _ => { }, _ => { });
        Check(playback.World.AllowedFateId == 123 && playback.World.Actors[1].FateId == 123,
            "Recorded actor/allowed Fate fields were not restored.");
        Check(QuestTargetPriority.Select(466, playback.World.Actors.Values, 0, default) == 1,
            "Serialized native enemy classification was replaced by the Hostile flag.");
        playback.AdvanceTo(1, _ => { }, _ => { });
        Check(playback.World.AllowedFateId == 0 && playback.World.Actors[1].FateId == 124,
            "Zero allowed Fate or changed actor Fate failed to replace the old values.");
        Check(QuestTargetPriority.Select(466, playback.World.Actors.Values, 0, default) == 0, "Recorded allied delta retained an enemy.");
        playback = new(loaded);
        playback.AdvanceTo(0, _ => { }, _ => { });
        Check(playback.World.Actors[1].ObjectIndex == 2 && playback.World.Actors[1].IsEnemy == true, "Reverse rebuild retained later classification or index.");
        Check(playback.World.AllowedFateId == 123 && playback.World.Actors[1].FateId == 123,
            "Reverse rebuild retained later Fate inputs.");
        Check(new ReplayActorState().IsEnemy == null && new ReplayActorState().ObjectIndex == null, "Old snapshots guessed native fields.");
        Check(new ReplayActorState().FateId == null && new ReplayWorldFrame().AllowedFateId == null,
            "Old snapshots guessed unrecorded Fate context.");
    }

    private static void Defaults()
    {
        foreach (var (territory, oid) in new (uint, uint)[] { (476, 0x1268), (488, 0x1236), (484, 0x11E4), (466, 0x1231), (273, 0x3BC) })
        {
            var ordinary = Enemy(1, 0x999);
            var preferred = Enemy(2, oid);
            var party = new HashSet<ulong> { 100, 124 };
            ulong Select(params ReplayActorState[] actors) => QuestTargetPriority.Select(territory, actors, 0, default, party, 123);
            Check(Select(ordinary) == 0, "A passive ordinary enemy was promoted by a partial override.");
            Check(Select(ordinary with { AggroPlayer = true }) == 1, "Enmity fallback incorrectly required combat.");
            Check(Select(ordinary with { InCombat = true, TargetId = 100 }) == 1
                && Select(ordinary with { InCombat = true, TargetId = 124 }) == 1, "An enemy attacking a party player or NPC lost basic priority zero.");
            Check(Select(ordinary with { TargetId = 100 }) == 0
                && Select(ordinary with { InCombat = true, TargetId = 999 }) == 0, "Party fallback ignored enemy combat or target membership.");
            var invincible = ordinary with { AggroPlayer = true, Statuses = [new(671, 0, 0, DateTime.MaxValue)] };
            Check(Select(invincible) == 0 && Select(invincible with { Oid = oid }) == 1,
                "Invincibility was ignored for ordinary targets or made unconditionally sticky against the zone setter.");
            Check(Select(preferred, ordinary with { AggroPlayer = true }) == 2, "Basic priority zero displaced the zone's positive priority.");
            Check(Select(preferred with { Hp = 1 }) == 0 && Select(preferred with { Hp = 1, Position = new(0, 13, 0) }) == 2,
                "Pointless setter protection or height-before-death order differs.");
            Check(Select(ordinary with { AggroPlayer = true, FateId = 123 }) == 1
                && Select(ordinary with { AggroPlayer = true, FateId = 124 }) == 0, "Allowed/mismatched Fate basic priority differs.");
            Check(Select(preferred with { Hp = 1, FateId = 124 }) == 2,
                "Fate mismatch was applied after Pointless instead of before it.");
            Check(Select(preferred with { Hp = 1, FateId = 123 }) == 0, "Allowed Fate erroneously bypassed Pointless.");
            Check(QuestTargetPriority.Select(territory, [ordinary with { AggroPlayer = true, FateId = 123 }],
                0, default, party, 0) == 0, "Leaving a Fate retained the prior allowed ID.");
        }
    }

    private static void AddedZones()
    {
        foreach (var (territory, low, high) in new (uint, uint, uint)[]
        {
            (260, 0x2BA, 0x2BD), (260, 0x2BA, 0x2BE), (275, 0x3C7, 0x999), (315, 0x640, 0x999), (461, 0x999, 0x106C),
            (414, 0xDBE, 0xDC9), (414, 0xDBE, 0x999), (669, 0x1AE8, 0x999),
            (673, 0x1C81, 0x1C82), (673, 0x1C81, 0x1EB4), (673, 0x999, 0x1C81), (410, 0x999, 0xDB7)
        })
        {
            var first = Enemy(1, low);
            var preferred = Enemy(2, high);
            Check(QuestTargetPriority.OwnsTerritory(territory)
                && QuestTargetPriority.Select(territory, [first, preferred], 1, default) == 2,
                "New zone priority failed to override an ordinary current target.");
            Check(QuestTargetPriority.Select(territory, [first], 0, default) == 1,
                "New zone zero-priority fallback became forbidden.");
            Check(QuestTargetPriority.Select(territory, [first, preferred with { Hp = 1 }], 2, default) == 1,
                "Priority assignment overwrote a protected Pointless target.");
            var lethal = preferred with { IncomingEffects = [new(0, 1, 0, false, false) { HpDelta = -99 }] };
            Check(QuestTargetPriority.Select(territory, [first, lethal], 2, default) == 1,
                "Incoming lethal damage did not protect Pointless priority.");
            Check(QuestTargetPriority.Select(territory, [preferred with { Hp = 1, NameId = 541 }], 0, default) == 2,
                "Striking dummy incorrectly received Pointless.");
            Check(QuestTargetPriority.Select(territory, [preferred with { Hp = 0, Position = new(0, 12.01f, 0) }], 0, default) == 2,
                "Height default was applied after PendingDead instead of before it.");
            Check(QuestTargetPriority.Select(territory, [preferred with { Hp = 1, Position = new(0, 12, 0) }], 0, default) == 0,
                "Inclusive twelve-unit height boundary skipped Pointless.");
            Check(QuestTargetPriority.Select(territory, [preferred with
            {
                Hp = 1, IncomingEffects = [new(0, 1, 0, false, false) { HpDelta = 10 }]
            }], 0, default) == 2, "Pending healing failed to preserve a viable preferred target.");
        }
        Check(QuestTargetPriority.Priority(260, Enemy(1, 0x999)) == 1, "Starcrossed unlisted OID priority differs.");
        foreach (var territory in new uint[] { 251, 304, 309 })
        {
            var first = Enemy(1, 0x999);
            var second = Enemy(2, 0xABC);
            Check(QuestTargetPriority.OwnsTerritory(territory)
                && QuestTargetPriority.Select(territory, [first, second], 2, default) == 2,
                "PrioritizeAll failed to retain an equally ranked current target.");
            Check(QuestTargetPriority.Select(territory, [first, second with { Hp = 1 }], 2, default) == 1,
                "PrioritizeAll overwrote Pointless.");
            Check(QuestTargetPriority.Select(territory, [first with { Hp = 0, Position = new(0, 13, 0) }], 0, default) == 1,
                "PrioritizeAll failed to override the earlier height priority.");
        }
        var oneHp = Enemy(2, 0xDB7) with { Hp = 1, NameId = 541 };
        Check(QuestTargetPriority.Priority(410, oneHp) == 0
            && QuestTargetPriority.Select(410, [Enemy(1, 0x999), oneHp], 2, default) == 1,
            "Reason Roaille applied OID priority before its exact-HP branch.");
        Check(QuestTargetPriority.Select(233, [Enemy(1, 0x999) with { Hp = 1 }], 0, default) == 0,
            "Original ARR branch still overwrote protected Pointless.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
