using System.Numerics;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal sealed class HuntSugrivaState
{
    private readonly ulong owner;
    private readonly List<HuntStackState> stacks = [];
    private readonly List<HuntCastBait> butchers = [];
    private readonly List<HuntCastBait> preys = [];
    private readonly HuntStatusInput preyInput;
    private HuntPendingCast? knockback;
    private ReplayActorState? knockbackTarget;
    private bool targetAttached;
    private int rockCasts;
    internal IEnumerable<HuntStackState> Stacks => stacks;
    internal IEnumerable<HuntCastBait> Baits => butchers.Concat(preys);
    internal bool StackKnockbackHint => knockbackTarget != null;
    internal bool TankbusterHint => butchers.Count != 0;

    internal HuntSugrivaState(ulong owner)
    {
        this.owner = owner;
        // Prey Gain has no source mechanic action; only Lose clears the complete bait list.
        preyInput = new(2939, static (_, _) => { }, _ => preys.Clear());
    }

    internal void Start(HuntPendingCast cast, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        var action = cast.Rule.Action;
        if (action == 27229) rockCasts = 0;
        if (action == 27226 && ++rockCasts == 2) preys.Clear();
        if (!actors.TryGetValue(cast.Source, out var source) || !actors.TryGetValue(cast.TargetId, out var target)) return;
        if (action == 27219)
        {
            var stack = new HuntStackState(8, 8, 8);
            stack.Start(cast with { Source = owner }, actors, []);
            stacks.Add(stack);
            knockback = cast;
            knockbackTarget = target;
            targetAttached = true;
        }
        else if (action == 27223)
            butchers.Add(new(owner, cast, source, target, new FanTemplate(8, 2 * MathF.PI / 3)));
        else if (action == 27229 && !(target.Kind == 2 && target.SubKind == 3))
            preys.Add(new(owner, cast with { Activation = cast.Activation.AddSeconds(1.1d) }, source, target,
                new CircleTemplate(6), centerAtTarget: true));
    }

    internal void Correct(HuntPendingCast previous, HuntPendingCast updated, IReadOnlyDictionary<ulong, ReplayActorState> actors)
    {
        foreach (var stack in stacks)
            stack.Correct(previous with { Source = owner }, updated with { Source = owner }, actors);
        foreach (var bait in butchers) bait.Correct(previous, updated, actors);
        foreach (var bait in preys)
            bait.Correct(previous, updated with { Activation = updated.Activation.AddSeconds(1.1d) }, actors);
        if (knockback?.Source != previous.Source || knockback.Order != previous.Order) return;
        if (knockback.TargetId != updated.TargetId && actors.TryGetValue(updated.TargetId, out var target))
        {
            knockbackTarget = target;
            targetAttached = true;
        }
        knockback = updated;
    }

    internal void Finish(uint action, ulong target)
    {
        if (action != 27219) return;
        knockback = null;
        knockbackTarget = null;
        var index = stacks.FindIndex(s => s.TargetId == target);
        if (index >= 0) stacks.RemoveAt(index);
    }

    internal void Effect(ulong source)
    {
        var index = butchers.FindIndex(b => b.Cast.Source == source);
        if (index >= 0) butchers.RemoveAt(index);
    }

    internal void Update(IReadOnlyDictionary<ulong, ReplayActorState> actors, DateTime now)
    {
        foreach (var bait in Baits) bait.Update(actors);
        butchers.RemoveAll(b => b.SourceDead || b.SourceDestroyed || b.TargetDead);
        foreach (var stack in stacks) stack.Update(actors);
        if (targetAttached && knockbackTarget != null)
        {
            if (actors.TryGetValue(knockbackTarget.Id, out var target)) knockbackTarget = target;
            else targetAttached = false;
        }
    }

    internal void Status(ReplayActorState actor, ReplayStatus? value, ulong source, DateTime at)
        => preyInput.Status(actor, value, source, at);

    internal void RemoveMissing(IReadOnlySet<ulong> actors) => preyInput.RemoveMissing(actors);
    internal void ObserveActor(IReadOnlyDictionary<ulong, ReplayActorState> actors, ulong actor, DateTime at)
        => preyInput.Observe(actors, at, actor);

    internal void Remove(ulong actor)
    {
        preyInput.Remove(actor);
        foreach (var bait in Baits) bait.Remove(actor);
        butchers.RemoveAll(b => b.SourceDestroyed);
        foreach (var stack in stacks) stack.Remove(actor);
        if (knockbackTarget?.Id == actor) targetAttached = false;
    }

    internal IEnumerable<HuntMovementProjection> Knockbacks(ReplayActorState player)
    {
        if (knockbackTarget == null || knockback == null || targetAttached && player.Id == knockbackTarget.Id
            || player.Statuses.Any(s => s.ExpiresAt > knockback.Activation && s.Id is 3054 or 160 or 1209 or 1722 or 2663 or 2345))
            yield break;
        var direction = player.Position - knockbackTarget.Position;
        direction.Y = 0;
        if (direction.LengthSquared() > 64 || direction.LengthSquared() == 0) yield break;
        yield return new(owner, player.Position, player.Position + Vector3.Normalize(direction) * 20);
    }

    internal IEnumerable<HuntDamagePrediction> PredictedDamage(IReadOnlyList<ReplayPartyMember> party)
    {
        if (butchers.Count == 0) yield break;
        var ids = party.Where(p => p.Slot is >= 0 and < 64).Select(p => p.EntityId).ToHashSet();
        foreach (var id in butchers.Where(b => b.Active && ids.Contains(b.TargetId)).Select(b => b.TargetId).Distinct())
            yield return new(owner, id, butchers[0].Cast.Activation, "Tankbuster");
    }
}
