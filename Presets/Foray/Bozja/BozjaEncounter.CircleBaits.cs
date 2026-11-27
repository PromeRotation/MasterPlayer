using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    private void StartCircleBait(BozjaActor source, BozjaRule rule)
    {
        if (Actors.GetValueOrDefault(source.Cast!.TargetId) is not { } target) return;
        Casts.Add(new(++serial, rule, source, default, 0, Activation(source)) { Target = target });
    }

    private void FinishCircleBait(BozjaActor source, BozjaRule rule, ReplayCast spell)
    {
        if (!rule.Actions.Contains(spell.ActionId)) return;
        var index = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == source.Actor.Id);
        if (index >= 0) Casts.RemoveAt(index);
    }

    private IEnumerable<BozjaCast> CircleBaits(BozjaRule rule)
        => Casts.Where(c => c.Rule == rule && !c.Source.Actor.Dead);

    internal IEnumerable<(BozjaCast Cast, bool Outline)> CircleBaitViews(BozjaActor viewer, BozjaRule rule)
    {
        foreach (var bait in CircleBaits(rule))
            yield return (bait with { Origin = bait.Target!.Actor.Position }, bait.Target!.Actor.Id == viewer.Actor.Id);
    }

    private IEnumerable<BozjaHint> CircleBaitHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.CircleBait) yield break;
        var radius = ((CircleTemplate)rule.Shape!).Radius;
        var baits = CircleBaits(rule).ToArray();
        if (baits.Any(c => c.Target!.Actor.Id == viewer.Actor.Id && Members().Any(a => a.Actor.Id != c.Target.Actor.Id
            && DistanceSquared(a.Actor.Position, c.Target.Actor.Position) <= radius * radius))) yield return new("Bait away from raid!");
        if (baits.Any(c => c.Target!.Actor.Id != viewer.Actor.Id
            && DistanceSquared(viewer.Actor.Position, c.Target.Actor.Position) <= radius * radius)) yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaZone> CircleBaitNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.CircleBait) yield break;
        var baits = CircleBaits(rule).ToArray();
        foreach (var bait in baits)
        {
            if (!ReferenceEquals(bait.Target, viewer))
                yield return new($"bait/circle/{bait.Generation}", rule.Component, bait.Activation, bait.Shape,
                    bait.Target!.Actor.Position, 0);
            else if (!ReferenceEquals(bait.Source, bait.Target))
                foreach (var member in Members().Where(a => !ReferenceEquals(a, viewer)))
                    yield return new($"bait/circle/{bait.Generation}/{member.BirthOrder}", rule.Component,
                        bait.Activation, bait.Shape, member.Actor.Position, 0);
        }
        if (IsLightArtillery && baits.Any(c => ReferenceEquals(c.Target, viewer)))
            yield return new("light/homing/edge", rule.Component, default, new RectangleTemplate(46, 46), Center, 0);
    }

    private IEnumerable<BozjaDamage> CircleBaitDamage(BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.CircleBait) yield break;
        ulong mask = 0;
        foreach (var bait in CircleBaits(rule))
        {
            var slot = Party.FirstOrDefault(p => p.EntityId == bait.Target!.Actor.Id)?.Slot;
            if (slot is >= 0 and < 64) mask |= 1UL << slot.Value;
        }
        // GenericBaitAway uses the first raw bait clock, including a dead
        // source retained ahead of the currently active baits.
        if (mask != 0) yield return new(mask, Casts.First(c => c.Rule == rule).Activation, IsVirtue ? "Tankbuster" : "Raidwide");
    }
}
