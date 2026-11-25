using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZoneReborn.Solver;

internal static class SimpleHuntStackRegression
{
    private static readonly DateTime Start = new(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc);

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Nariphon exact eight capacity seven eight nine target buddy goals and forbidden counts", Capacity);
        run("SimpleHunts Nariphon cast-start frozen status identities packet corrections and shared full-mask guard", Frozen);
        run("SimpleHunts Nariphon dead revive detached ID reuse singleton invalid slots bait and cleanup", Lifecycle);
        run("SimpleHunts Nariphon concurrent bosses keep buddy eligibility and Shared full-mask guard independent", Independent);
        run("SimpleHunts Nariphon mandatory union deadlines defeat settled follow goals and real solver chooses one buddy", Solver);
    }

    private static void Capacity()
    {
        foreach (var inside in new[] { 7, 8, 9 })
        {
            var encounter = new SimpleHuntEncounter();
            var members = Enumerable.Range(0, inside).Select(i => new ReplayActorState { Id = (ulong)(100 + i), Position = new(.2f, 0, .2f) }).ToArray();
            var outside = new ReplayActorState { Id = 200, Position = new(20, 0, 20) };
            var party = members.Append(outside).Select((a, i) => new ReplayPartyMember(i, "Ally", a.Id)).ToArray();
            encounter.Synchronize([Boss(Cast()), .. members, outside], _ => true, Start, party);
            Check(Joins(encounter.TargetDangers(outside, party).Single()) == (inside < 8)
                && Joins(encounter.TargetDangers(members[1], party).Single()) == (inside <= 8),
                "Stack capacity lost its outside<8 / inside<=8 distinction.");
            Check(encounter.TargetPreviews(outside).Single().Safe == (inside < 8)
                && encounter.TargetPreviews(members[1]).Single().Safe == (inside <= 8),
                "Stack outline color disagreed with its exact capacity.");
            var goals = encounter.TargetDangers(members[0], party).ToArray();
            Check(goals.Length == 1 && goals[0].Shape is HuntStackJoinTemplate { Radius: 3 } join && join.Centers.Length == inside,
                "The marked target did not receive any-buddy R3 goals including distant allies.");
            Check(!encounter.PredictedDamage(party).Any(), "Empty Forbidden invented Shared damage despite the source full-mask guard.");
            encounter.Reset();
            var forbidden = members[1] with { Statuses = [Status()] };
            encounter.Synchronize([Boss(Cast()), .. members.Where(a => a.Id != forbidden.Id), forbidden, outside], _ => true, Start, party);
            Check(!Joins(encounter.TargetDangers(forbidden, party).Single())
                && encounter.TargetHints(forbidden, party).Contains("GTFO from forbidden stacks!")
                && !encounter.TargetPreviews(forbidden).Single().Safe,
                "A frozen Forbidden member was allowed into the stack.");
            Check(Joins(encounter.TargetDangers(outside, party).Single()) == (inside - 1 < 8),
                "Forbidden members counted against the eight eligible participants.");
        }
    }

    private static void Frozen()
    {
        var encounter = new SimpleHuntEncounter();
        var target = new ReplayActorState { Id = 100 };
        var first = new ReplayActorState { Id = 101, Position = new(1, 0, 1), Statuses = [Status()] };
        var second = new ReplayActorState { Id = 102, Position = new(2, 0, 2) };
        var party = new ReplayPartyMember[] { new(0, "Target", 100), new(8, "Alliance", 101), new(24, "NPC", 102) };
        encounter.Synchronize([Boss(null), target, first, second], _ => true, Start, party);
        var cast = Cast();
        encounter.Start(1, cast, true);
        encounter.StackStatus(101, Start.AddSeconds(1), false);
        encounter.StackStatus(102, Start.AddSeconds(1), true);
        var reordered = new ReplayPartyMember[] { new(24, "Target", 100), new(0, "Alliance", 101), new(8, "NPC", 102) };
        first = first with { Statuses = [] };
        second = second with { Statuses = [Status()] };
        encounter.Synchronize([Boss(cast), target, first, second], _ => true, Start.AddSeconds(1), reordered);
        encounter.Start(1, cast with { Rotation = .2f }, true);
        encounter.Synchronize([Boss(cast), target, first, second], _ => true, Start.AddSeconds(2), reordered);
        Check(!Joins(encounter.TargetDangers(first, reordered).Single())
            && Joins(encounter.TargetDangers(second, reordered).Single()),
            "Post-start status changes, slot reordering or packet observation re-froze Forbidden.");
        Check(encounter.PredictedDamage(reordered).Select(d => d.TargetId).Order().SequenceEqual([100UL, 102UL])
            && encounter.PredictedDamage(reordered).All(d => d.DamageType == "Shared" && d.Activation == cast.FinishAt),
            "Shared prediction failed to expand the frozen complement or changed its activation.");
        encounter.Synchronize([Boss(cast with { Rotation = .2f }), target, first, second], _ => true, Start.AddSeconds(2.1), reordered);
        Check(Joins(encounter.TargetDangers(first, reordered).Single())
            && !Joins(encounter.TargetDangers(second, reordered).Single())
            && encounter.PredictedDamage(reordered).Select(d => d.TargetId).Order().SequenceEqual([100UL, 101UL]),
            "Actual changed raw CastInfo failed to freeze a new Forbidden mask at its real Start.");
        encounter.Finish(1);
        encounter.Start(1, Cast(10), true);
        Check(Joins(encounter.TargetDangers(first, reordered).Single()) && !Joins(encounter.TargetDangers(second, reordered).Single()),
            "A real later stack did not capture the updated Forbidden list.");
        encounter.Finish(1);
        encounter.StackStatus(102, Start.AddSeconds(20), false);
        encounter.StackStatus(102, Start.AddSeconds(19), true);
        encounter.Start(1, Cast(30), true);
        Check(!encounter.PredictedDamage(reordered).Any(), "An older status gain replaced the accepted loss watermark.");
    }

    private static void Lifecycle()
    {
        var encounter = new SimpleHuntEncounter();
        var target = new ReplayActorState { Id = 100 };
        var ally = new ReplayActorState { Id = 101, Position = new(1, 0, 1) };
        var party = new ReplayPartyMember[] { new(0, "Target", 100), new(1, "Ally", 101) };
        var cast = Cast();
        encounter.Synchronize([Boss(cast), target, ally], _ => true, Start, party);
        encounter.Synchronize([Boss(cast), target with { Dead = true }, ally], _ => true, Start.AddSeconds(1), party);
        Check(!encounter.TargetPreviews(ally).Any() && !encounter.TargetDangers(ally, party).Any(),
            "Dead stack target retained output.");
        encounter.Synchronize([Boss(cast), target, ally], _ => true, Start.AddSeconds(2), party);
        Check(encounter.TargetPreviews(ally).Any(), "Revived target failed to restore its active stack.");
        encounter.Remove(100);
        target = target with { Position = new(30, 0, 30) };
        encounter.Synchronize([Boss(cast), target, ally], _ => true, Start.AddSeconds(3), party);
        var detached = encounter.TargetDangers(target, party).Single();
        Check(detached.Shape is HuntStackJoinTemplate { Radius: 6 } detachedJoin && Vector3.Distance(detachedJoin.Centers.Single(), Vector3.Zero) < .1f,
            "A replacement actor inherited the old stack target's marker or buddy strategy.");
        encounter.Finish(1);
        encounter.Start(1, Cast(10) with { ActionId = 16972, TargetId = 100 }, true);
        Check(encounter.TargetHints(target, party).Contains("Bait away!")
            && encounter.TargetPreviews(target).Single().Shape is CircleTemplate { Radius: 6 },
            "AllergenInjection lost its unconditional personal bait hint or R6.");
        encounter.Reset();
        ally = ally with { Statuses = [Status()] };
        var invalid = new ReplayPartyMember[] { new(0, "Target", 100), new(-1, "Invalid", 101), new(64, "Invalid", 101) };
        encounter.Synchronize([Boss(cast), target, ally], _ => true, Start, invalid);
        Check(!encounter.PredictedDamage(invalid).Any() && !encounter.TargetDangers(target, invalid).Any(),
            "Invalid slots polluted Forbidden or a solo target received ally navigation.");
        encounter.Synchronize([Boss(cast), target, ally], _ => false, Start, party);
        Check(!encounter.TargetPreviews(target).Any() && !encounter.TargetDangers(target, party).Any(), "Disabled stack retained output.");
    }

    private static ReplayStatus Status() => new(1435, 0, 1, Start.AddMinutes(1));

    private static void Independent()
    {
        var encounter = new SimpleHuntEncounter();
        var first = new ReplayActorState { Id = 100 };
        var second = new ReplayActorState { Id = 101, Position = new(1, 0, 1), Statuses = [Status()] };
        var third = new ReplayActorState { Id = 102, Position = new(2, 0, 2) };
        var party = new ReplayPartyMember[] { new(0, "First", 100), new(1, "Second", 101), new(2, "Third", 102) };
        encounter.Synchronize([Boss(Cast()), Boss(null) with { Id = 2 }, first, second, third], _ => true, Start, party);
        encounter.StackStatus(101, Start.AddSeconds(1), false);
        encounter.StackStatus(102, Start.AddSeconds(1), true);
        encounter.Start(2, Cast(1) with { TargetId = 101 }, true);
        Check(encounter.TargetDangers(second, party).Any(c => c.Source == 2 && c.Shape is HuntStackJoinTemplate { Radius: 3 } join
                && join.Centers.Contains(first.Position)),
            "An independent boss excluded another boss's stack target from its buddy goals.");
        var damage = encounter.PredictedDamage(party).ToArray();
        Check(damage.Count(d => d.Source == 1) == 2 && damage.Count(d => d.Source == 2) == 2
            && damage.Any(d => d.Source == 1 && d.TargetId == 102) && damage.Any(d => d.Source == 2 && d.TargetId == 101),
            "Independent full-mask guards or forbidden complements were merged across bosses.");
    }
    private static ReplayCast Cast(double at = 0)
        => new(16971, 100, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), true, false) { TotalTime = 5 };
    private static ReplayActorState Boss(ReplayCast? cast)
        => new() { Hp = 1000, Id = 1, Oid = 0x2890, Targetable = true, InCombat = true, Cast = cast };
    private static bool Joins(HuntPendingCast cast) => cast.Shape is HuntStackJoinTemplate;

    private static void Solver()
    {
        var options = new SolveOptions
        {
            GridResolution = .25f, HardSafetyMargin = 0, ComfortSafetyMargin = 0,
            DangerStartSafetyTime = 0, DangerEndSafetyTime = 0, GoalDirectedTemporalPlanning = true
        };
        var player = new Vector3(-10, 0, 0);
        var geometry = new HuntStackJoinTemplate([Vector3.Zero], 6).Resolve(Vector3.Zero, 0, null);
        SafeZoneSnapshot Snapshot(double delay) => new(2, Start, null, [],
            [new(default, geometry, [new(Start.AddSeconds(delay), null, Start)])],
            [new(default, new CircleGeometry(player, 2), [GoalWindow.Persistent(Start, 101)])]);
        var old = SafeZoneSolution.Succeeded(1, Start, Start, null, null, settledTargetPosition: player);
        var solution = new SafeZoneSolver().Solve(Snapshot(2), new(player, 0, 6, false, false, 0, true, 2),
            new(6, 6, 0, false, 6, 6), options, Start, old);
        var path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.IsSuccess && !solution.IsLeastDangerFallback && path.Count > 1 && !geometry.Contains(path[^1].Position)
            && solution.EscapeDeadline == Start.AddSeconds(2) && solution.EscapeDuration < TimeSpan.FromSeconds(2),
            "A settled high-priority follow goal defeated a mandatory timely stack join.");
        var farMap = RasterizedMap.Build(Snapshot(5), player, options, Start);
        var nearMap = RasterizedMap.Build(Snapshot(.5), player, options, Start);
        var farCell = farMap.WorldToCell(player);
        var nearCell = nearMap.WorldToCell(player);
        Check(Math.Abs(farMap.PixelMaxGAt(farCell.Col, farCell.Row) - 30) < .01
            && Math.Abs(nearMap.PixelMaxGAt(nearCell.Col, nearCell.Row) - 3) < .01,
            "Stack deadlines became identical permanent goals or unbounded danger was not rasterized.");
        var union = new HuntStackJoinTemplate([new(-8, 0, 0), new(8, 0, 0)], 3).Resolve(Vector3.Zero, 0, null);
        Check(!union.Contains(new(-8, 0, 0)) && !union.Contains(new(8, 0, 0)) && union.Contains(Vector3.Zero)
            && union.IntersectsSegment(new(-8, 0, 0), new(8, 0, 0)) && !union.IntersectsSegment(new(-9, 0, 0), new(-7, 0, 0)),
            "Buddy circles became an intersection or a path through their unsafe gap was accepted.");
        var snapshot = new SafeZoneSnapshot(3, Start, null, [], [new(default, union, [new(Start.AddSeconds(3), null, Start)])], []);
        solution = new SafeZoneSolver().Solve(snapshot, new(Vector3.Zero, 0, 6, false, false, 0, true, 3),
            new(6, 6, 0, false, 6, 6), options, Start);
        path = solution.ContinuationPath.Count > 0 ? solution.ContinuationPath : solution.ExecutablePath;
        Check(solution.IsSuccess && !solution.IsLeastDangerFallback && path.Count > 1 && !union.Contains(path[^1].Position),
            "The real solver failed to select one of two disjoint valid buddy circles.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
