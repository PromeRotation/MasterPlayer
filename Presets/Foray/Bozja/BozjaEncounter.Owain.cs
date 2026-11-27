using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsOwain => Definition.PrimaryOid == 0x265D;
    internal bool IsBaldesion => IsArt || IsOwain || IsRaiden || IsVirtue;
    internal BozjaDelubrumTimeline? OwainTimeline { get; private set; }
    internal List<BozjaActor> OwainSpiritTargets { get; } = [];
    internal BozjaCast? OwainSpiritStack { get; private set; }
    internal List<(BozjaActor? Target, BozjaActor Source)> OwainTethers { get; } = [];
    internal List<BozjaAoe> OwainElements { get; } = [];
    internal bool OwainImbasActive { get; private set; }
    private List<BozjaActor>? owainSpears;
    private List<BozjaActor>? owainPalms;

    private void ClearOwain()
    {
        OwainTimeline?.Stop();
        OwainTimeline = null;
        OwainSpiritTargets.Clear();
        OwainSpiritStack = null;
        OwainTethers.Clear();
        OwainElements.Clear();
        owainSpears = owainPalms = null;
        OwainImbasActive = false;
    }

    internal void SetOwainImbas(bool enabled)
    {
        var rule = Definition.Rules.Single(r => r.Component == "LegendaryImbas");
        Casts.RemoveAll(c => c.Rule == rule);
        OwainImbasActive = enabled;
        if (enabled)
            foreach (var actor in Actors.Values)
                if (actor.Cast != null) Start(actor, rule);
    }

    private void OwainCreated(BozjaActor actor)
    {
        if (!IsOwain) return;
        if (actor.Actor.Oid == 0x265E) owainSpears?.Add(actor);
        if (actor.Actor.Oid == 0x266B) owainPalms?.Add(actor);
    }

    private void OwainDestroyed(BozjaActor actor)
    {
        owainSpears?.Remove(actor);
        owainPalms?.Remove(actor);
    }

    private void StartOwain(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        if (rule.Kind == BozjaRuleKind.OwainCastHint)
            Casts.Add(new(++serial, rule, actor, spell.Location, 0, Activation(actor)));
        if (rule.Kind != BozjaRuleKind.OwainElemental || spell.ActionId is not (14650 or 14651)) return;
        owainSpears ??= Actors.Values.Where(a => a.Actor.Oid == 0x265E).ToList();
        var status = spell.ActionId == 14650 ? 1783u : 1784u;
        foreach (var spear in owainSpears.Where(a => a.Statuses.Any(s => s?.Id == status)))
            OwainElements.Add(new(new(++serial, rule, actor,
                DungeonEncounter.QuantizedDungeonPosition(spear.Actor.Position), 0, Activation(actor)), false, true));
        OwainElements.Add(new(new(++serial, rule, actor, spell.Location, 0, Activation(actor)), false, true));
    }

    private void OwainEffect(uint action)
    {
        if (IsOwain && OwainElements.Count != 0 && action is 14650 or 14651 or 14652 or 14653)
            OwainElements.RemoveAt(0);
    }

    private void OwainIcon(BozjaActor actor, uint icon)
    {
        if (!IsOwain || !Active || icon != 55) return;
        OwainSpiritTargets.Add(actor);
        OwainSpiritStack ??= new(++serial, Definition.Rules.Single(r => r.Kind == BozjaRuleKind.OwainSpirit),
            Primary!, default, 0, Now.AddSeconds(5.1d)) { Target = actor };
    }

    private void OwainStatus(ReplayStatus status, bool gain)
    {
        if (!IsOwain || gain || status.Id != 1753) return;
        OwainSpiritTargets.Clear();
        OwainSpiritStack = null;
    }

    private void OwainTether(BozjaActor source, (uint Id, ulong Target) tether, bool remove)
    {
        if (!IsOwain || !Active) return;
        var entry = (Actors.GetValueOrDefault(tether.Target), source);
        if (remove) OwainTethers.Remove(entry);
        else OwainTethers.Add(entry);
    }

    internal IEnumerable<BozjaEye> OwainEyes(BozjaActor? viewer)
    {
        if (!IsOwain || !Active || viewer == null) yield break;
        foreach (var tether in OwainTethers)
            if (ReferenceEquals(tether.Target, viewer) && !tether.Source.Actor.Dead)
            {
                yield return new(-tether.Source.BirthOrder, tether.Source.Actor.Position with { Y = 0 },
                    default, true, tether.Source.Actor.Id, Precise: true);
                yield break;
            }
    }

    internal void ObserveOwainDrawing(BozjaActor viewer)
    {
        if (!IsOwain) return;
        owainPalms ??= Actors.Values.Where(a => a.Actor.Oid == 0x266B).ToList();
        if (OwainSpiritStack == null || !OwainSpiritTargets.Contains(viewer)) return;
        BozjaActor? nearest = null;
        var minimum = float.MaxValue;
        foreach (var target in OwainSpiritTargets)
        {
            if (ReferenceEquals(target, viewer)) continue;
            var distance = DistanceSquared(viewer.Actor.Position, target.Actor.Position);
            if (distance < minimum) { minimum = distance; nearest = target; }
        }
        OwainSpiritStack = OwainSpiritStack with { Target = nearest ?? viewer };
    }

    internal IEnumerable<BozjaCast> OwainCleaves()
    {
        if (!IsOwain || !Active) yield break;
        owainPalms ??= Actors.Values.Where(a => a.Actor.Oid == 0x266B).ToList();
        var rule = Definition.Rules.Single(r => r.Kind == BozjaRuleKind.OwainCleave);
        foreach (var source in owainPalms)
        {
            if (source.Actor.Dead || source.Statuses.Any(s => s?.Id == 610)
                || Actors.GetValueOrDefault(source.Actor.TargetId) is not { } target) continue;
            var delta = target.Actor.Position - source.Actor.Position;
            yield return new(-source.BirthOrder, rule, source, DungeonEncounter.QuantizedDungeonPosition(source.Actor.Position),
                MathF.Atan2(delta.X, delta.Z), default) { Target = target };
        }
    }

    private IEnumerable<BozjaHint> OwainHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.OwainGaze && OwainEyes(viewer).Any(e => EyeDanger(e, viewer.Actor)))
            yield return new("Face the hand to petrify it!");
        if (rule.Kind == BozjaRuleKind.OwainCleave && OwainCleaves().Any(c => !ReferenceEquals(c.Target, viewer)
            && (c with { Origin = c.Source.Actor.Position }).Contains(DungeonEncounter.QuantizedDungeonPosition(viewer.Actor.Position))))
            yield return new("GTFO from cleave!");
        if (rule.Kind != BozjaRuleKind.OwainSpirit || !OwainSpiritTargets.Contains(viewer)
            || OwainSpiritStack is not { Target: { Actor.Dead: false } target } stack) yield break;
        var center = StackCenter(stack);
        var members = Members();
        if (ReferenceEquals(target, viewer))
        {
            var count = 1 + members.Count(a => !ReferenceEquals(a, viewer) && DistanceSquared(a.Actor.Position, center) <= 2.25f);
            yield return new("Stack!", count is < 24 or > 24);
        }
        else if (DistanceSquared(viewer.Actor.Position, center) <= 2.25f) yield return new("Stack!", false);
        else if (1 + members.Count(a => !ReferenceEquals(a, target) && DistanceSquared(a.Actor.Position, center) <= 2.25f) < 24)
            yield return new("Stack!");
    }

    internal bool OwainSpiritSafe(BozjaActor viewer)
    {
        if (OwainSpiritStack is not { Target: { } target } stack) return false;
        if (ReferenceEquals(target, viewer)) return true;
        var center = StackCenter(stack);
        var count = Members().Count(a => DistanceSquared(a.Actor.Position, center) <= 2.25f);
        var inside = DistanceSquared(viewer.Actor.Position, center) <= 2.25f;
        return inside ? count <= 24 : count < 24;
    }

    private IEnumerable<BozjaZone> OwainNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.OwainElemental)
            foreach (var aoe in OwainElements)
                yield return new($"owain/element/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                    aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
        if (rule.Kind == BozjaRuleKind.OwainCleave)
            foreach (var cleave in OwainCleaves())
                if (!ReferenceEquals(cleave.Target, viewer))
                    yield return new($"owain/cleave/{cleave.Generation}", rule.Component, default,
                        cleave.Shape, cleave.Origin, cleave.Rotation);
                else
                    foreach (var actor in Members().Where(a => !ReferenceEquals(a, viewer)))
                    {
                        var delta = actor.Actor.Position - cleave.Source.Actor.Position;
                        yield return new($"owain/cleave/{cleave.Generation}/party/{actor.Actor.Id}", rule.Component, default,
                            new FanTemplate(100, 2 * (60f * (MathF.PI / 180f))), cleave.Origin, MathF.Atan2(delta.X, delta.Z));
                    }
        if (rule.Kind != BozjaRuleKind.OwainSpirit || !OwainSpiritTargets.Contains(viewer)
            || OwainSpiritStack is not { Target: { Actor.Dead: false } target } stack || Members().Length < 2) yield break;
        if (ReferenceEquals(target, viewer))
        {
            var buddies = Members().Where(a => !ReferenceEquals(a, viewer)).Select(a => a.Actor.Position).ToArray();
            if (buddies.Length > 0)
                yield return new($"owain/spirit/{stack.Generation}/buddy", rule.Component, stack.Activation,
                    null, default, 0, buddies, JoinRadius: .75f);
        }
        else if (OwainSpiritSafe(viewer))
            yield return new($"owain/spirit/{stack.Generation}/join", rule.Component, stack.Activation,
                null, default, 0, [StackCenter(stack)], JoinRadius: 1.5f, Goal: true);
        else
            yield return new($"owain/spirit/{stack.Generation}/avoid", rule.Component, stack.Activation,
                stack.Shape, StackCenter(stack), 0);
    }
}
