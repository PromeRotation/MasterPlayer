using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal List<BozjaCast> CavalryKnockbacks { get; } = [];
    internal List<BozjaRush> SteelBaits { get; } = [];
    internal List<(BozjaActor Target, DateTime Activation)> SteelDelays { get; } = [];
    internal List<(BozjaActor Target, uint Id)> SteelTethers { get; } = [];
    internal int NumSteelCasts { get; private set; }

    private void ClearCavalry()
    {
        CavalryKnockbacks.Clear();
        SteelBaits.Clear();
        SteelDelays.Clear();
        SteelTethers.Clear();
        NumSteelCasts = 0;
    }

    private void StartCavalry(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.Charge)
        {
            var delta = new Vector2(spell.Location.X - actor.Actor.Position.X, spell.Location.Z - actor.Actor.Position.Z);
            Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position),
                MathF.Atan2(delta.X, delta.Y), Activation(actor))
            {
                ShapeOverride = RayRectangleTemplate.Symmetric(
                    rule.Shape is RayRectangleTemplate width ? width.MaxLocalX - width.MinLocalX : 6,
                    delta.Length()), Action = spell.ActionId,
            });
        }
        else if (rule.Kind == BozjaRuleKind.ForwardKnockback)
            CavalryKnockbacks.Add(new(++serial, rule, actor, spell.Location, spell.Rotation ?? 0, Activation(actor)));
        else if (rule.Kind == BozjaRuleKind.SideKnockback)
        {
            CavalryKnockbacks.RemoveAll(c => c.Rule == rule);
            CavalryKnockbacks.Add(new(++serial, rule, actor, Center,
                (spell.Rotation ?? 0) + MathF.PI / 2, Activation(actor)));
            CavalryKnockbacks.Add(new(++serial, rule, actor, Center,
                (spell.Rotation ?? 0) - MathF.PI / 2, Activation(actor)));
        }
    }

    private void FinishCavalry(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.SideKnockback) CavalryKnockbacks.RemoveAll(c => c.Rule == rule);
        else
        {
            var index = CavalryKnockbacks.FindIndex(c => c.Rule == rule && c.Source.Actor.Id == actor.Actor.Id);
            if (index >= 0) CavalryKnockbacks.RemoveAt(index);
        }
    }

    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> CavalryMovements(BozjaActor viewer, BozjaRule rule)
    {
        // MaxCasts limits successful movements, after testing the shape. Both
        // sources ignore immunity; the old consumer has no AI forbidden zone.
        foreach (var cast in CavalryKnockbacks.Where(c => c.Rule == rule))
        {
            if (rule.Kind == BozjaRuleKind.SideKnockback && !cast.Contains(viewer.Actor.Position)) continue;
            var (sin, cos) = Math.SinCos(cast.Rotation);
            var distance = rule.Kind == BozjaRuleKind.ForwardKnockback ? 35 : 12;
            var from = viewer.Actor.Position;
            yield return (cast, from, from + distance * new Vector3((float)sin, 0, (float)cos));
            yield break;
        }
    }

    private (BozjaActor? Player, BozjaActor? Enemy) SteelSides(BozjaActor source, (uint Id, ulong Target) tether,
        bool creating = false)
    {
        if (tether.Id is not (57 or 1) || Actors.GetValueOrDefault(tether.Target) is not { } target) return default;
        // Raid.WithoutSlot excludes dead members. Ownership is by original
        // actor reference, independently of whether either side is an NPC.
        // Feed creates the actor and submits its initial tether before applying
        // shared Dead diffs. A later cold activation uses the final state.
        var member = Party.Any(p => p.EntityId == source.Actor.Id) && (creating || !source.Actor.Dead);
        return member ? (source, target) : (target, source);
    }

    private void CavalryTethered(BozjaActor actor, (uint Id, ulong Target) tether, bool creating = false)
    {
        if (!Active || Definition.Rules.FirstOrDefault(r => r.Kind == BozjaRuleKind.ChargeTether) is not { } rule) return;
        var (player, enemy) = SteelSides(actor, tether, creating);
        if (player == null || enemy == null) return;
        var cached = SteelDelays.FindIndex(d => ReferenceEquals(d.Target, player));
        if (cached < 0)
        {
            cached = SteelDelays.Count;
            SteelDelays.Add((player, Now.AddSeconds(5d)));
        }
        SteelBaits.Add(new(++serial, rule, enemy, player, 0, SteelDelays[cached].Activation));
        SteelTethers.Add((player, tether.Id));
    }

    private void CavalryUntethered(BozjaActor actor, (uint Id, ulong Target) tether)
    {
        if (!Active || !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.ChargeTether)) return;
        var (player, enemy) = SteelSides(actor, tether);
        if (player == null || enemy == null) return;
        SteelBaits.RemoveAll(b => ReferenceEquals(b.Source, enemy) && ReferenceEquals(b.Target, player));
        // Source removes the world's tether.Target, even when the player is
        // the source side. Keep this asymmetric removal and the original ref.
        if (Actors.GetValueOrDefault(tether.Target) is { } target) SteelTethers.Remove((target, tether.Id));
    }

    private void CavalryEffect(uint action, ulong target)
    {
        if (action != 23943 || !Definition.Rules.Any(r => r.Kind == BozjaRuleKind.ChargeTether)) return;
        ++NumSteelCasts;
        var index = SteelBaits.FindIndex(b => b.Target.Actor.Id == target);
        if (index >= 0) SteelBaits.RemoveAt(index);
    }

    private void UpdateCavalry()
    {
        SteelDelays.RemoveAll(d => d.Activation.AddSeconds(1d) <= Now);
        foreach (var bait in SteelBaits)
            bait.Length = new Vector2(bait.Target.Actor.Position.X - bait.Source.Actor.Position.X,
                bait.Target.Actor.Position.Z - bait.Source.Actor.Position.Z).Length();
    }

    internal static BozjaCast SteelView(BozjaRush bait)
    {
        var delta = bait.Target.Actor.Position - bait.Source.Actor.Position;
        var rotation = ReferenceEquals(bait.Source, bait.Target) ? bait.Source.Actor.Rotation : MathF.Atan2(delta.X, delta.Z);
        return new(bait.Generation, bait.Rule, bait.Source, bait.Source.Actor.Position, rotation, bait.Activation)
        {
            Target = bait.Target, ShapeOverride = RayRectangleTemplate.Symmetric(4, bait.Length),
        };
    }

    private bool SteelClipsOther(BozjaRush bait)
        => Party.Any(p => p.EntityId != bait.Target.Actor.Id && Actors.GetValueOrDefault(p.EntityId) is { Actor.Dead: false } member
            && SteelView(bait).Contains(member.Actor.Position));

    private IEnumerable<BozjaHint> SteelHints(BozjaActor viewer)
    {
        if (!SteelBaits.Any(b => !b.Source.Actor.Dead)) yield break;
        var first = SteelBaits.FirstOrDefault(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, viewer));
        if (first != null)
        {
            var distanceSquared = DistanceSquared(first.Source.Actor.Position, viewer.Actor.Position);
            if (distanceSquared < 225 && SteelTethers.Contains((viewer, 57))) yield return new("Stretch tether further!");
            else if (distanceSquared >= 225 || SteelTethers.Contains((viewer, 1))) yield return new("Tether is stretched!", false);
        }
        // BaitAwayChargeTether calls StretchTetherDuo.AddHints, not the generic
        // bait implementation; its own final loop includes raw inactive baits.
        if (SteelBaits.Any(b => b.Target.Actor.Id == viewer.Actor.Id && SteelClipsOther(b)))
            yield return new("Bait away from raid!");
    }

    private IEnumerable<BozjaZone> SteelNavigation(BozjaActor viewer)
    {
        foreach (var bait in SteelBaits.Where(b => !b.Source.Actor.Dead))
        {
            if (!ReferenceEquals(bait.Target, viewer))
            {
                var cast = SteelView(bait);
                yield return new($"steel/{bait.Generation}/avoid", bait.Rule.Component, bait.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
            }
            else
            {
                if (!ReferenceEquals(bait.Source, bait.Target))
                    foreach (var party in Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot))
                        if (Actors.GetValueOrDefault(party.EntityId) is { Actor.Dead: false } member
                            && !ReferenceEquals(member, viewer))
                        {
                            var delta = member.Actor.Position - bait.Source.Actor.Position;
                            yield return new($"steel/{bait.Generation}/bait/{party.Slot}", bait.Rule.Component, bait.Activation,
                                new FanTemplate(100, 2 * (float)Math.Asin(2 / new Vector2(delta.X, delta.Z).Length())),
                                bait.Source.Actor.Position, MathF.Atan2(delta.X, delta.Z));
                        }
            }
        }
        foreach (var bait in SteelBaits.Where(b => !b.Source.Actor.Dead && ReferenceEquals(b.Target, viewer)))
            yield return new($"steel/{bait.Generation}/stretch", bait.Rule.Component, bait.Activation,
                new CircleTemplate(15), bait.Source.Actor.Position, 0);
    }

    private BozjaDamage? SteelDamage()
    {
        ulong mask = 0;
        foreach (var bait in SteelBaits.Where(b => !b.Source.Actor.Dead))
        {
            var slot = Party.Where(p => p.Slot is >= 0 and < 64).OrderBy(p => p.Slot)
                .FirstOrDefault(p => p.EntityId == bait.Target.Actor.Id)?.Slot ?? -1;
            if (slot >= 0) mask |= 1UL << slot;
        }
        return mask == 0 ? null : new(mask, SteelBaits[0].Activation, "Tankbuster");
    }
}
