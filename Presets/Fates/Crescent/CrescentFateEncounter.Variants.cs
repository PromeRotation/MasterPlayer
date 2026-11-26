using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Fates;

internal sealed partial class CrescentFateEncounter
{
    private readonly HashSet<uint> freefallSequences = [];
    private readonly HashSet<uint> puddleSequences = [];
    private readonly List<CrescentFateCast> concentricDisplay = [];
    private static readonly GeometryTemplate[] concentricShapes =
        [new CircleTemplate(10), new DonutTemplate(10, 20), new DonutTemplate(20, 30)];

    private void Created(CrescentFateActor actor, CrescentFateRule? only = null)
    {
        if (actor.Actor.Oid != 0x46D8) return;
        foreach (var rule in Definition.Rules.Where(r => r.Kind == CrescentFateRuleKind.CreatedCircle
            && (only == null || ReferenceEquals(r, only))))
            Casts.Add(new(++nextId, actor, rule, 41386, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                0, now.AddSeconds(9.8d), now));
    }

    private bool StartVariant(CrescentFateActor source, CrescentFateRule rule)
    {
        var cast = source.Cast!;
        var origin = cast.Location;
        var rotation = cast.Rotation ?? source.Actor.Rotation;
        var activation = Activation(source);
        GeometryTemplate? shape = null;
        switch (rule.Kind)
        {
            case CrescentFateRuleKind.CreatedCircle:
            case CrescentFateRuleKind.StatusCircle:
            case CrescentFateRuleKind.Observer:
                return true;
            case CrescentFateRuleKind.Concentric:
                if (Array.IndexOf(rule.Actions, cast.ActionId) % 3 != 0) return true;
                rotation = 0;
                break;
            case CrescentFateRuleKind.Freefall:
                if (cast.ActionId != 47598 || source.EventHappened) return true;
                Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule));
                freefallSequences.Clear();
                foreach (var delay in new[] { .23d, 3.06d, 5.84d })
                    Casts.Add(new(++nextId, source, rule, cast.ActionId, origin, 0, Activation(source, delay),
                        cast.StartedAt, Risky: false));
                return true;
            case CrescentFateRuleKind.DelayedStorm:
                if (cast.ActionId is not (49756 or 49757)) return true;
                Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule));
                origin = source.Actor.Position;
                rotation = 0;
                shape = cast.ActionId == 49757 ? new DonutTemplate(10, 40) : rule.Shape;
                activation = Activation(source, 1d);
                break;
            case CrescentFateRuleKind.Puddle:
                if (Casts.Any(c => ReferenceEquals(c.Rule, rule) && c.Source.Actor.Id == source.Actor.Id)) return true;
                rotation = 0;
                activation = Activation(source, -1f);
                break;
            case CrescentFateRuleKind.Charge:
                var delta = Xz(cast.Location - source.Actor.Position);
                shape = RayRectangleTemplate.Symmetric(8, delta.Length());
                origin = DungeonEncounter.QuantizedDungeonPosition(source.Actor.Position);
                rotation = MathF.Atan2(delta.X, delta.Y);
                break;
            case CrescentFateRuleKind.DelayedChargeCircle:
                if (cast.ActionId != 42179) return true;
                Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule));
                rotation = 0;
                activation = Activation(source, 5.2d);
                break;
            default:
                return false;
        }
        Casts.Add(new(++nextId, source, rule, cast.ActionId, origin, rotation, activation, cast.StartedAt, shape));
        if (rule.Kind == CrescentFateRuleKind.Charge)
            SortRule(rule, onlyAt99: true);
        return true;
    }

    private bool FinishVariant(CrescentFateActor source, ReplayCast cast, CrescentFateRule rule)
    {
        switch (rule.Kind)
        {
            case CrescentFateRuleKind.CreatedCircle:
            case CrescentFateRuleKind.StatusCircle:
                if (rule.Actions.Contains(cast.ActionId)) RemoveFirst(rule, source.Actor.Id);
                return true;
            case CrescentFateRuleKind.Concentric:
                var order = Array.IndexOf(rule.Actions, cast.ActionId);
                if (order < 0) return true;
                order %= 3;
                // Source sequences match origin/order, independently of the caster.
                var index = Casts.FindIndex(c => ReferenceEquals(c.Rule, rule) && c.Order == order
                    && MathF.Abs(c.Origin.X - cast.Location.X) <= 1f && MathF.Abs(c.Origin.Z - cast.Location.Z) <= 1f
                    && MathF.Abs(c.Rotation) <= .05f);
                if (index >= 0)
                {
                    if (order == 2) Casts.RemoveAt(index);
                    else Casts[index] = Casts[index] with { Order = order + 1,
                        ShapeOverride = concentricShapes[order + 1], Activation = now.AddSeconds(2d) };
                }
                return true;
            case CrescentFateRuleKind.DelayedStorm:
                if (!source.EventHappened && !cast.EffectSeen)
                    Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule) && c.Action == cast.ActionId);
                return true;
            case CrescentFateRuleKind.DelayedChargeCircle:
                if (cast.ActionId == 42180) Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule));
                return true;
            case CrescentFateRuleKind.Freefall:
            case CrescentFateRuleKind.Puddle:
            case CrescentFateRuleKind.Observer:
                return true;
            default:
                return false;
        }
    }

    internal void Effect(ulong sourceId, uint action, uint globalSequence = 0)
    {
        if (!Actors.TryGetValue(sourceId, out var source) || !NativeNpc(source.Actor)) return;
        // OpCastEvent sets this even when no phase is currently active; a later
        // cold activation must see the event history of this original cast.
        if (source.Cast?.ActionId == action) source.EventHappened = true;
        if (!Active) return;
        foreach (var rule in Definition.Rules)
        {
            switch (rule.Kind)
            {
                case CrescentFateRuleKind.Freefall when action == 47584:
                    if (globalSequence == 0 || freefallSequences.Add(globalSequence))
                    {
                        var first = Casts.FindIndex(c => ReferenceEquals(c.Rule, rule));
                        if (first >= 0) Casts.RemoveAt(first);
                    }
                    break;
                case CrescentFateRuleKind.DelayedStorm:
                    Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule)
                        && (c.Action == 49756 && action == 49766 || c.Action == 49757 && action == 49767));
                    break;
                case CrescentFateRuleKind.Puddle when rule.Actions.Contains(action):
                    if (globalSequence == 0 || puddleSequences.Add(globalSequence))
                        Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule) && c.Source.Actor.Id == sourceId);
                    break;
            }
        }
    }

    internal void Update()
    {
        if (!Active) return;
        foreach (var rule in Definition.Rules)
        {
            if (rule.Kind == CrescentFateRuleKind.Freefall)
                while (Casts.FindIndex(c => ReferenceEquals(c.Rule, rule)) is var index && index >= 0
                    && now > Casts[index].Activation.AddSeconds(1d))
                    Casts.RemoveAt(index);
            else if (rule.Kind == CrescentFateRuleKind.DelayedStorm)
                Casts.RemoveAll(c => ReferenceEquals(c.Rule, rule) && now > c.Activation.AddSeconds(1d));
        }
        concentricDisplay.Clear();
        concentricDisplay.AddRange(Casts.Where(c => c.Rule.Kind == CrescentFateRuleKind.Concentric));
    }

    internal CrescentFateCast[] VisibleAoes()
    {
        var visible = new List<CrescentFateCast>();
        foreach (var rule in Definition.Rules)
        {
            if (rule.Kind is CrescentFateRuleKind.Gaze or CrescentFateRuleKind.Raidwide) continue;
            if (rule.Kind == CrescentFateRuleKind.Observer)
            {
                // Enemies(allObservers) concatenates each OID's insertion-order list.
                foreach (var oid in new uint[] { 0x47DD, 0x4818 })
                    foreach (var actor in Actors.Values.Where(a => a.Actor.Oid == oid && !a.Actor.Dead))
                        visible.Add(new(-((long)actor.Actor.Id + 1), actor, rule, 0, actor.Actor.Position,
                            actor.Actor.Rotation, now.AddSeconds(1.1d), default));
                continue;
            }
            if (rule.Kind == CrescentFateRuleKind.Concentric)
            {
                visible.AddRange(concentricDisplay.Where(c => ReferenceEquals(c.Rule, rule)));
                continue;
            }
            if (rule.Kind is CrescentFateRuleKind.FirstWaveCone or CrescentFateRuleKind.Puddle) SortRule(rule);
            var group = Casts.Where(c => ReferenceEquals(c.Rule, rule)).ToArray();
            var cross = rule.Kind == CrescentFateRuleKind.DeferredCircle
                ? Casts.FirstOrDefault(c => c.Rule.Component == "WildWildBreath") : null;
            for (var i = 0; i < group.Length; ++i)
            {
                var cast = group[i];
                if (rule.Kind == CrescentFateRuleKind.Freefall)
                {
                    visible.Add(cast);
                    break;
                }
                if (rule.Kind == CrescentFateRuleKind.DeferredCircle)
                {
                    var risky = cross == null || cross.Activation >= cast.Activation;
                    cast = cast with { Risky = risky, Danger = false, AoeColor = !risky };
                }
                else if (rule.Kind is CrescentFateRuleKind.FirstWaveCone or CrescentFateRuleKind.Puddle)
                {
                    var risky = cast.Activation <= group[0].Activation.AddSeconds(.2d);
                    cast = cast with { Risky = risky, Danger = risky, AoeColor = !risky };
                }
                var index = Casts.FindIndex(c => c.Id == cast.Id);
                Casts[index] = cast;
                visible.Add(cast);
            }
        }
        return visible.ToArray();
    }

    internal CrescentFateCast[] ObserverExtras()
        => VisibleAoes().Where(c => c.Rule.Kind == CrescentFateRuleKind.Observer)
            .Select(c => c with { Id = long.MinValue + c.Id * -1, ShapeOverride = new FanTemplate(6, MathF.PI / 2),
                Activation = default }).ToArray();

    internal IGeometryShape NavigationBoundary(ReplayActorState player)
        => Definition.Name == "ADelicateBalance" ? new CircleGeometry(player.Position, 30)
            : new CircleGeometry(Center with { Y = player.Position.Y }, Radius);

    internal Vector3? ChargeGoal => Active && Primary?.Cast?.ActionId == 42178 ? Primary.Cast.Location : null;

    internal float ChargeGoalScore(Vector3 position)
        => ChargeGoal is { } origin && Vector2.DistanceSquared(Xz(origin), Xz(position)) <= 25f ? 5f : 0f;

    internal int? TargetPriority(ReplayActorState actor)
    {
        if (Definition.Name != "ADelicateBalance") return null;
        if (actor.Oid == Definition.PrimaryOid) return 1;
        return actor.InCombat ? 0 : -3;
    }

    private void SortRule(CrescentFateRule rule, bool onlyAt99 = false)
    {
        var group = Casts.Where(c => ReferenceEquals(c.Rule, rule)).ToList();
        if (onlyAt99 && group.Count != 99) return;
        if (onlyAt99) DungeonAoeOrder.Sort(group, c => c.Activation);
        else group.Sort(static (a, b) => a.Activation.CompareTo(b.Activation));
        var index = 0;
        for (var i = 0; i < Casts.Count; ++i)
            if (ReferenceEquals(Casts[i].Rule, rule)) Casts[i] = group[index++];
    }

    private void RemoveFirst(CrescentFateRule rule, ulong sourceId)
    {
        var index = Casts.FindIndex(c => ReferenceEquals(c.Rule, rule) && c.Source.Actor.Id == sourceId);
        if (index >= 0) Casts.RemoveAt(index);
    }

    private void ObserveStatuses(CrescentFateActor actor)
    {
        var next = new ReplayStatus?[60];
        for (var i = 0; i < actor.Actor.Statuses.Length; ++i)
        {
            var status = actor.Actor.Statuses[i];
            var slot = status.SlotIndex ?? i;
            if (slot is >= 0 and < 60 && status.Id != 0)
                next[slot] = status with
                {
                    SourceId = Sanitize(status.SourceId),
                    ExpiresAt = status.ZeroRemainingTime == true ? now
                        : status.ExpiresAt == DateTime.MaxValue ? now.AddSeconds(100000)
                        : now.AddSeconds(Math.Min(Math.Abs((status.ExpiresAt - now).TotalSeconds), 100000)),
                };
        }
        for (var i = 0; i < 60; ++i)
        {
            var old = actor.Statuses[i];
            var value = next[i];
            actor.Statuses[i] = value;
            if (!Active || actor.IsPet || value == null) continue;
            if (old == null || old.Id != value.Id || old.SourceId != value.SourceId || old.Param != value.Param
                || (value.ExpiresAt - old.ExpiresAt).TotalSeconds > 1d) Gain(actor, value);
        }
    }

    private void Gain(CrescentFateActor actor, ReplayStatus status, CrescentFateRule? only = null)
    {
        if (status.Id != 4221 || status.Param != 2) return;
        foreach (var rule in Definition.Rules.Where(r => r.Kind == CrescentFateRuleKind.StatusCircle
            && (only == null || ReferenceEquals(r, only))))
            Casts.Add(new(++nextId, actor, rule, 29808, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                0, now.AddSeconds(7.1d), now));
    }
}
