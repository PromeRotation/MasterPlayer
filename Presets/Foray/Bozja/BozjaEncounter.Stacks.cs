using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal BozjaActor? Hand(BozjaActor viewer)
        => Active && Definition.Rules.Any(r => r.Component == "GraspingRancor")
            ? hands?.FirstOrDefault(a => a.Tether?.TargetId == viewer.Actor.Id) : null;

    private BozjaActor[] Members()
        => Party.Where(p => p.Slot is >= 0 and < 64).Select(p => Actors.GetValueOrDefault(p.EntityId))
            .OfType<BozjaActor>().Where(a => !a.Actor.Dead).ToArray();

    internal BozjaCast[] Stacks(BozjaRule rule)
        => Active ? Casts.Where(c => c.Rule == rule && c.Target is { Actor.Dead: false }).ToArray() : [];

    internal static Vector3 StackCenter(BozjaCast stack) => DungeonEncounter.QuantizedDungeonPosition(stack.Target!.Actor.Position);

    internal IEnumerable<BozjaHint> StackHints(BozjaActor viewer, BozjaRule rule)
    {
        var stacks = Stacks(rule);
        var members = Members();
        var radius = ((CircleTemplate)rule.Shape!).Radius;
        var radiusSquared = radius * radius;
        if (stacks.FirstOrDefault(s => ReferenceEquals(s.Target, viewer)) is { } own)
        {
            var center = StackCenter(own);
            var count = 1 + members.Count(a => !ReferenceEquals(a, viewer)
                && DistanceSquared(a.Actor.Position, center) <= radiusSquared);
            var overlaps = members.Any(a => !ReferenceEquals(a, viewer)
                && Casts.Any(s => s.Rule == rule && ReferenceEquals(s.Target, a))
                && DistanceSquared(a.Actor.Position, center) <= radiusSquared);
            yield return new("Stack!", count < rule.MinStackSize || overlaps);
        }
        else
        {
            var participating = stacks.Count(s => DistanceSquared(viewer.Actor.Position, StackCenter(s)) <= radiusSquared);
            var unsatisfied = stacks.Any(s => 1 + members.Count(a => !ReferenceEquals(a, s.Target)
                && DistanceSquared(a.Actor.Position, StackCenter(s)) <= radiusSquared) < rule.MinStackSize);
            if (participating > 1 || participating == 0 && unsatisfied) yield return new("Stack!");
            else if (participating == 1) yield return new("Stack!", false);
        }
    }

    internal bool StackSafe(BozjaCast stack, BozjaActor viewer)
        => ReferenceEquals(stack.Target, viewer) || !Casts.Any(s => s.Rule == stack.Rule && ReferenceEquals(s.Target, viewer));

    private IEnumerable<BozjaZone> StackNavigation(BozjaActor viewer, BozjaRule rule)
    {
        var stacks = Stacks(rule);
        var members = Members();
        var radius = ((CircleTemplate)rule.Shape!).Radius;
        if (stacks.Length == 0 || members.Length < 2) yield break;
        foreach (var own in stacks.Where(s => ReferenceEquals(s.Target, viewer)))
        {
            var buddies = members.Where(a => !ReferenceEquals(a, viewer)
                && !stacks.Any(s => ReferenceEquals(s.Target, a))).Select(a => a.Actor.Position).ToArray();
            if (buddies.Length > 0)
                yield return new($"stack/{own.Generation}/buddy", rule.Component, own.Activation,
                    null, default, 0, buddies, JoinRadius: radius * .5f);
        }
        var join = stacks.Where(s => !ReferenceEquals(s.Target, viewer)).Select(StackCenter).ToArray();
        // With unbounded capacity and no forbidden players these are always
        // join candidates. The old bridge turns SDOutsideOfUnion into a goal
        // union; the raw per-stack clocks intentionally collapse to the first.
        if (join.Length > 0)
            yield return new($"stack/{rule.Component}/join", rule.Component, stacks[0].Activation,
                null, default, 0, join, JoinRadius: radius, Goal: true);
    }
}
