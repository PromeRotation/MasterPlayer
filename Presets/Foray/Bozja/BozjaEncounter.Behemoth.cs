using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaRush> Horns { get; } = [];
    internal List<BozjaCast> Meteors { get; } = [];
    internal List<BozjaCast> JuiceEvents { get; } = [];
    internal DateTime WildBoltActivation { get; private set; }
    internal int MissingJuicePositionCount { get; private set; }
    private readonly List<BozjaActor> meteorCasters = [];
    private List<BozjaActor>? corpses;
    private List<BozjaActor>? juiceObjects;
    private Vector3? meteorOrigin;

    private List<BozjaActor> Corpses => corpses ??= Actors.Values.Where(a => a.Actor.Oid == 0x2DCC).ToList();
    private uint JuiceOid => Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Juice)?.EnemyOid ?? 0;
    private List<BozjaActor> JuiceObjects => juiceObjects ??= Actors.Values.Where(a => a.Actor.Oid == JuiceOid).ToList();

    private void ClearBehemoth()
    {
        Horns.Clear();
        Meteors.Clear();
        JuiceEvents.Clear();
        meteorCasters.Clear();
        WildBoltActivation = default;
        MissingJuicePositionCount = 0;
        corpses = juiceObjects = null;
        meteorOrigin = null;
    }

    private void BehemothCreated(BozjaActor actor)
    {
        if (actor.Actor.Oid == 0x2DCC) corpses?.Add(actor);
        if (actor.Actor.Oid == JuiceOid) juiceObjects?.Add(actor);
    }

    private void BehemothDestroyed(BozjaActor actor)
    {
        corpses?.Remove(actor);
        juiceObjects?.Remove(actor);
    }

    private void RefreshMeteor()
    {
        BozjaActor? active = null;
        var remaining = float.MaxValue;
        foreach (var actor in meteorCasters)
            if (actor.Cast is { } spell && Total(spell) - actor.Elapsed < remaining)
            {
                remaining = Total(spell) - actor.Elapsed;
                active = actor;
            }
        meteorOrigin = active?.Cast?.Location;
        _ = Corpses;
    }

    private void StartBehemoth(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.Meteor)
        {
            meteorCasters.Add(actor);
            RefreshMeteor();
            if (meteorOrigin is { } origin && Corpses.Count > 0)
                Meteors.Add(new(++serial, rule, actor, Center, 0, Activation(actor))
                {
                    ShapeOverride = new BozjaMeteorTemplate(new(Center, origin, Corpses)),
                });
        }
        else if (rule.Kind == BozjaRuleKind.DelayedRaidwide)
            WildBoltActivation = Activation(actor, spell.ActionId == 23828 ? (double).8f : .9d);
        else if (rule.Kind == BozjaRuleKind.Horn)
        {
            if (Actors.GetValueOrDefault(spell.TargetId) is { } target)
                Horns.Add(new(++serial, rule, actor, target, 0, Activation(actor)));
        }
        else if (rule.Kind == BozjaRuleKind.Juice)
            Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)));
    }

    private void FinishBehemoth(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.Meteor)
        {
            meteorCasters.Remove(actor);
            RefreshMeteor();
            if (Meteors.Count > 0) Meteors.RemoveAt(0);
        }
        else if (rule.Kind == BozjaRuleKind.Juice)
        {
            var first = Casts.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (first >= 0) Casts.RemoveAt(first);
        }
        else if (rule.Kind == BozjaRuleKind.Thunderbolt)
        {
            if (spell.ActionId == 20197)
            {
                Casts.RemoveAll(c => c.Rule == rule);
                return;
            }
            foreach (var corpse in Corpses)
            {
                var pos = corpse.Actor.Position;
                var inside = spell.ActionId == 20196 ? DistanceSquared(pos, spell.Location) <= 36
                    : DungeonSourceGeometry.Contains(RayRectangleTemplate.Symmetric(14.7f, 60),
                        spell.Location, spell.Rotation ?? 0, pos);
                if (!inside) continue;
                Casts.Add(new(++serial, rule, corpse, DungeonEncounter.QuantizedDungeonPosition(pos),
                    corpse.Actor.Rotation, Now.AddSeconds(12.8d)));
                if (spell.ActionId == 20196) break;
            }
        }
    }

    private void BehemothEffect(BozjaActor source, uint action, ulong target, Vector3? position)
    {
        if (action is 20201 or 23829 && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.DelayedRaidwide))
            WildBoltActivation = default;
        if (action == 20193)
        {
            var first = Horns.FindIndex(b => b.Source.Actor.Id == source.Actor.Id);
            if (first >= 0) Horns.RemoveAt(first);
        }
        if (Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.Juice && r.Actions.Contains(action)) is { } rule)
        {
            var origin = Actors.GetValueOrDefault(target)?.Actor.Position ?? position;
            if (origin is { } point) JuiceEvents.Add(new(++serial, rule, source, point, 0, Now.AddSeconds(action == 20196 ? .9d : 0d)));
            else ++MissingJuicePositionCount;
        }
    }

    private void UpdateBehemoth()
    {
        Horns.RemoveAll(b => b.Source.Destroyed || b.Source.Actor.Dead || b.Target.Actor.Dead);
        if (JuiceEvents.Count == 0) return;
        foreach (var source in JuiceObjects.Where(a => a.Actor.EventState != 7))
        {
            var first = JuiceEvents.FindIndex(c => DistanceSquared(c.Origin, source.Actor.Position) <= 4);
            if (first >= 0) JuiceEvents.RemoveAt(first);
        }
    }

    private IEnumerable<BozjaCast> JuiceAoes(BozjaRule rule)
    {
        foreach (var cast in Casts.Where(c => c.Rule == rule))
            if (cast.Source.Cast is { } spell)
                yield return cast with { Origin = Actors.GetValueOrDefault(spell.TargetId)?.Actor.Position ?? spell.Location };
        foreach (var cast in JuiceEvents) yield return cast;
        foreach (var actor in JuiceObjects.Where(a => a.Actor.EventState != 7))
            yield return new(-actor.BirthOrder, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), 0, default);
    }

    internal static BozjaCast HornView(BozjaRush bait)
    {
        var source = bait.Source.Actor;
        var target = bait.Target.Actor;
        var rotation = ReferenceEquals(bait.Source, bait.Target) ? source.Rotation
            : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        return new(bait.Generation, bait.Rule, bait.Source, source.Position, rotation, bait.Activation)
            { Target = bait.Target };
    }

    private IEnumerable<BozjaHint> HornHints(BozjaActor viewer)
    {
        var active = Horns.Where(b => !b.Source.Actor.Dead).ToArray();
        if (active.Any(b => b.Target.Actor.Id == viewer.Actor.Id
            && Party.Any(p => p.EntityId != b.Target.Actor.Id
                && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member
                && HornView(b).Contains(member.Actor.Position))))
            yield return new("Bait away from raid!");
        if (active.Any(b => b.Target.Actor.Id != viewer.Actor.Id && HornView(b).Contains(viewer.Actor.Position)))
            yield return new("GTFO from baited aoe!");
    }

    private IEnumerable<BozjaZone> HornNavigation(BozjaActor viewer)
    {
        foreach (var bait in Horns.Where(b => !b.Source.Actor.Dead))
        {
            var cast = HornView(bait);
            if (!ReferenceEquals(bait.Target, viewer))
            {
                yield return new($"horn/{bait.Generation}/avoid", bait.Rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
                continue;
            }
            if (ReferenceEquals(bait.Source, bait.Target)) continue;
            foreach (var member in Party.Where(p => p.Slot is >= 0 and < 64))
            {
                if (Actors.GetValueOrDefault(member.EntityId) is not { Actor.Dead: false } actor
                    || ReferenceEquals(actor, viewer)) continue;
                var delta = actor.Actor.Position - bait.Source.Actor.Position;
                yield return new($"horn/{bait.Generation}/party/{member.Slot}", bait.Rule.Component, bait.Activation,
                    new FanTemplate(100, 2 * (60f * (MathF.PI / 180f))), cast.Origin, MathF.Atan2(delta.X, delta.Z));
            }
        }
    }

    private IEnumerable<BozjaDamage> HornDamage()
    {
        ulong mask = 0;
        foreach (var bait in Horns.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.FirstOrDefault(p => p.EntityId == bait.Target.Actor.Id && p.Slot is >= 0 and < 64)?.Slot;
            if (slot is { } i) mask |= 1UL << i;
        }
        if (mask != 0) yield return new(mask, Horns[0].Activation, "Tankbuster");
    }
}
