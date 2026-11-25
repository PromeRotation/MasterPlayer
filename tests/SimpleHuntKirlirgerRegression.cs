using System.Numerics;
using MasterPlayer.Mechanics.Hunts;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

internal static class SimpleHuntKirlirgerRegression
{
    private static readonly DateTime Start = DateTime.UnixEpoch.AddDays(20000);
    private static ReplayActorState Boss(ReplayCast? cast = null) => new()
    {
        Id = 1, Oid = 0x452A, Targetable = true, InCombat = true, Hp = 1000, Cast = cast
    };
    private static ReplayCast Cast(uint action, ulong target = 100, double at = 0)
        => new(action, target, Vector3.Zero, 0, Start.AddSeconds(at), Start.AddSeconds(at + 5.3), false, false);
    private static ReplayActorState Player(ulong id, Vector3 position) => new() { Id = id, Position = position };
    private static HuntPendingCast[] Shapes(SimpleHuntEncounter e) => e.Areas.Where(a => a.Shape != null).ToArray();

    internal static void Run(Action<string, Action> run)
    {
        run("SimpleHunts Kirlirger source groups 270 cone exact geometry and any finish without clearing ordinary casts", Groups);
        run("SimpleHunts Kirlirger minimum eight unlimited maximum stack and no forbidden or shared damage", Capacity);
        run("SimpleHunts Kirlirger missing start target death revival frozen identity and first target matching finish", Targets);
        run("SimpleHunts Kirlirger old targetless finish never borrows an unrelated current cast and packet correction isolates orders", LateFinish);
    }

    private static void Groups()
    {
        var definition = SimpleHuntCatalog.Find(0x452A)!;
        Check(definition.Casts.Length == 15 && !definition.Casts.Any(c => c.Action is >= 39453 and <= 39458),
            "Kirlirger rules include unused visual actions or omit an activated component.");
        foreach (var action in new uint[] { 39466, 39477, 39473, 39467, 39538, 39877, 39464, 39459, 39484, 39463, 39465, 39875 })
        {
            var encounter = new SimpleHuntEncounter();
            var cast = Cast(action);
            encounter.Synchronize([Boss(cast)], _ => true, Start);
            Check(Shapes(encounter).Length == 1, "Kirlirger AOE group failed to start.");
            var shape = Shapes(encounter)[0].Shape!;
            if (shape is FanTemplate fan)
            {
                Check(MathF.Abs(fan.AngleRadians - 3 * MathF.PI / 2) < .00001f, "Flourish total angle differs.");
                var geometry = fan.Resolve(default, 0, null);
                Check(geometry.Contains(new(20, 0, -19), 0) && !geometry.Contains(new(19, 0, -20), 0), "Flourish rear safe sector differs.");
            }
            if (shape is CircleTemplate circle) Check(circle.Radius == 20, "Fullmoon radius differs.");
            if (shape is DonutTemplate donut) Check(donut.InnerRadius == 10 && donut.OuterRadius == 40, "DiscordantMoon radii differ.");
            encounter.Finish(1, 999999, Start.AddSeconds(1));
            Check(!Shapes(encounter).Any() && encounter.Casts.Count == 1, "Any group finish failed to clear only its AOE.");
            encounter.Start(1, cast with { Location = new(10, 0, 20) }, true);
            Check(!Shapes(encounter).Any(), "A later packet resurrected a group consumed by an actual finish.");
            encounter.Synchronize([Boss(cast with { Location = new(10, 0, 20) })], _ => true, Start.AddSeconds(2));
            Check(Shapes(encounter).Length == 1, "A genuine raw restart did not create the new group.");
        }
        foreach (var action in new uint[] { 39524, 39481, 39480 })
        {
            var encounter = new SimpleHuntEncounter();
            encounter.Synchronize([Boss(Cast(action)), Player(100, default)], _ => true, Start);
            encounter.Finish(1, 999999, Start.AddSeconds(1));
            Check(encounter.Casts.Single().Rule.Action == action, "Group any-finish handling leaked into an ordinary component.");
            if (action == 39524) Check(Shapes(encounter).Length == 1, "Unknown finish cleared FlyingFist.");
            if (action == 39480) Check(encounter.TargetPreviews(Player(100, default)).Count() == 1, "Unknown finish cleared the stack.");
        }
    }

    private static void Capacity()
    {
        var encounter = new SimpleHuntEncounter();
        var party = Enumerable.Range(0, 10).Select(i => new ReplayPartyMember(i, $"P{i}", (ulong)(100 + i))).ToArray();
        var target = Player(100, default);
        var outside = Player(109, new(20, 0, 0)) with
        {
            Statuses = [new ReplayStatus(1435, 1, 1, Start.AddSeconds(100))]
        };
        foreach (var insideCount in new[] { 7, 8, 9 })
        {
            var actors = Enumerable.Range(0, insideCount).Select(i => Player((ulong)(100 + i), Vector3.Zero)).ToArray();
            encounter.Reset();
            encounter.Synchronize([Boss(Cast(39480)), .. actors, outside], _ => true, Start, party);
            var preview = encounter.TargetPreviews(outside).Single();
            var navigation = encounter.TargetDangers(outside, party).Single();
            Check(preview.Safe && !navigation.Shape!.Resolve(default, 0, null).Contains(default, 0)
                && navigation.Shape.Resolve(default, 0, null).Contains(outside.Position, 0),
                "An eighth/ninth participant or SID1435 made the unlimited stack forbidden.");
            Check(!encounter.PredictedDamage(party).Any(), "Kirlirger stack invented Shared or Raidwide predictions.");
            var personal = encounter.TargetDangers(target, party).Single();
            Check(!personal.Shape!.Resolve(default, 0, null).Contains(Vector3.Zero, 0),
                "The stack target lost R3 buddy-union navigation.");
        }
        encounter.Reset();
        encounter.Synchronize([Boss(Cast(39480)), target], _ => true, Start, [party[0]]);
        Check(!encounter.TargetDangers(target, [party[0]]).Any(), "One participant produced stack navigation.");
    }

    private static void Targets()
    {
        var encounter = new SimpleHuntEncounter();
        var cast = Cast(39480);
        var target = Player(100, new(5, 0, 5));
        var other = Player(101, new(20, 0, 20));
        encounter.Synchronize([Boss()], _ => true, Start);
        encounter.Start(1, cast, true);
        encounter.Synchronize([Boss(cast), target], _ => true, Start.AddSeconds(1));
        Check(!encounter.TargetPreviews(target).Any(), "A target absent at semantic Start was retroactively attached.");
        encounter.Reset();
        encounter.Synchronize([Boss(), target], _ => true, Start);
        encounter.Start(1, cast, true);
        encounter.Start(1, cast with { TargetId = 101 }, true);
        encounter.Synchronize([Boss(cast with { TargetId = 101 }), target, other], _ => true, Start.AddSeconds(1));
        Check(!encounter.TargetPreviews(other).Any(), "A correction to a missing target bypassed the no-retry Start rule.");
        encounter.Reset();
        encounter.Synchronize([Boss(cast), target], _ => true, Start);
        encounter.Synchronize([Boss(cast), target with { Dead = true }], _ => true, Start.AddSeconds(1));
        Check(!encounter.TargetPreviews(target).Any(), "A dead stack target stayed active.");
        encounter.Synchronize([Boss(cast), target], _ => true, Start.AddSeconds(2));
        Check(encounter.TargetPreviews(target).Count() == 1, "A revived target did not recover the same marker.");
        encounter.Remove(100);
        encounter.Synchronize([Boss(cast), target with { Position = new(90, 0, 90) }], _ => true, Start.AddSeconds(3));
        var frozen = encounter.TargetPreviews(other).Single();
        Check(Vector3.Distance(frozen.Location, target.Position) < .05f, "A same-ID replacement hijacked the frozen target reference.");
        encounter.Finish(1, 39480, Start.AddSeconds(4), 101);
        Check(encounter.Casts.Count == 0 && encounter.TargetPreviews(other).Count() == 1, "A mismatched target finish removed another target's stack.");
        encounter.Synchronize([Boss(), target, other], _ => true, Start.AddSeconds(9));
        encounter.Start(1, Cast(39480, 101, 10), true);
        Check(encounter.TargetPreviews(other).Count() == 2
            && encounter.TargetPreviews(other).Select(a => a.AreaId).Distinct().Count() == 2,
            "A new Start overwrote a preserved stack or collided output IDs.");
        var outsider = Player(102, new(50, 0, 50));
        var party = new ReplayPartyMember[] { new(0, "A", 100), new(1, "B", 101), new(2, "C", 102) };
        encounter.Synchronize([Boss(), target, other, outsider], _ => true, Start.AddSeconds(10.5), party);
        var constraints = encounter.TargetDangers(outsider, party).ToArray();
        Check(constraints.Length == 1 && constraints[0].Activation == cast.FinishAt,
            "Multiple stacks did not aggregate with the first active stack's activation.");
        var union = constraints[0].Shape!.Resolve(default, 0, null);
        Check(!union.Contains(frozen.Location, 0) && !union.Contains(other.Position, 0) && union.Contains(outsider.Position, 0),
            "Multiple stack joins required both disjoint circles instead of either.");
        var markedConstraints = encounter.TargetDangers(other, party).ToArray();
        Check(markedConstraints.Length == 2 && markedConstraints.All(a => a.Shape is HuntStackJoinTemplate)
            && markedConstraints.Any(a => a.Shape is HuntStackJoinTemplate { Radius: 6 }),
            "An unlimited stack target incorrectly received a double-radius avoidance circle.");
        encounter.Finish(1, 39480, Start.AddSeconds(11), 100);
        Check(encounter.TargetPreviews(other).Single().TargetId == 101, "Finish did not remove the first matching frozen target ID.");
        encounter.Finish(1, 39480, Start.AddSeconds(12), 101);
        Check(!encounter.TargetPreviews(other).Any(), "An old stack could not finish after the primary cast was already cleared.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void LateFinish()
    {
        var encounter = new SimpleHuntEncounter();
        var player = Player(100, default);
        var other = Player(101, new(20, 0, 20));
        encounter.Synchronize([Boss(), player, other], _ => true, Start);
        encounter.Start(1, Cast(39480), true);
        encounter.Finish(1, 39480, Start.AddSeconds(1), 999);
        var second = Cast(39480, 101, 2);
        encounter.Start(1, second, true);
        var old = encounter.TargetPreviews(player).Single(a => a.TargetId == 100);
        var corrected = second with { FinishAt = second.FinishAt.AddMilliseconds(50) };
        encounter.Start(1, corrected, true);
        var previews = encounter.TargetPreviews(player).ToArray();
        Check(previews.Single(a => a.TargetId == 100).Activation == old.Activation
            && previews.Single(a => a.TargetId == 101).Activation == corrected.FinishAt,
            "A current packet correction dragged a retained older stack.");
        encounter.Finish(1, 39480, Start.AddSeconds(3), 999);
        encounter.Start(1, Cast(39524, 101, 10), true);
        encounter.Finish(1, 39480, Start.AddSeconds(5));
        encounter.Finish(1, 39480, Start.AddSeconds(11));
        Check(encounter.TargetPreviews(player).Count() == 2 && encounter.Casts.Single().Rule.Action == 39524,
            "A targetless stale or mismatched finish borrowed FlyingFist's target to consume a retained stack.");
        encounter.Finish(1, 39480, Start.AddSeconds(6), 100);
        Check(encounter.TargetPreviews(player).Single().TargetId == 101 && encounter.Casts.Single().Rule.Action == 39524,
            "An explicit older stack target could not finish independently of a newer cast.");
    }
}
