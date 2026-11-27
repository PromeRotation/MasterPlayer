using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsRaiden => Definition.PrimaryOid == 0x2605;
    internal int RaidenPhase { get; private set; }
    internal bool RaidenReduced { get; private set; }
    internal bool RaidenTethersAssigned { get; private set; }
    internal BozjaDelubrumTimeline? RaidenTimeline { get; private set; }
    private readonly List<BozjaRule> raidenRules = [];
    private readonly Dictionary<string, int> raidenCounts = [];
    private readonly BozjaActor?[] raidenPartners = new BozjaActor?[64];
    internal int RaidenCount(string component) => raidenCounts.GetValueOrDefault(component);
    internal int RaidenCasters(string component) => Casts.Count(c => c.Rule.Component == component);

    private void ClearRaiden()
    {
        RaidenTimeline?.Stop();
        RaidenTimeline = null;
        RaidenPhase = 0;
        RaidenReduced = RaidenTethersAssigned = false;
        raidenRules.Clear();
        raidenCounts.Clear();
        Array.Clear(raidenPartners);
    }

    private void BeginRaiden()
    {
        raidenRules.AddRange(Definition.Rules.Take(5));
        RaidenTimeline = new(this);
        RaidenTimeline.Begin();
    }

    internal void ResetRaiden(string component)
    {
        var rule = Definition.Rules.Single(r => r.Component == component);
        raidenRules.Remove(rule);
        Casts.RemoveAll(c => c.Rule == rule);
        raidenCounts[component] = 0;
        if (rule.Kind == BozjaRuleKind.RaidenChains)
        {
            Array.Clear(raidenPartners);
            RaidenTethersAssigned = false;
        }
        raidenRules.Add(rule);
        foreach (var actor in Actors.Values)
        {
            if (actor.NativeNpc) RaidenCreated(actor, rule);
            if (actor.Cast != null) Start(actor, rule);
            if (rule.Kind == BozjaRuleKind.RaidenChains && actor.ObservedTether.Id != 0)
                RaidenTether(actor, actor.ObservedTether, false);
        }
    }

    private void UpdateRaiden()
    {
        if (!IsRaiden || Primary is not { } primary) return;
        if (RaidenPhase == 0 && (primary.Actor.Dead || primary.Destroyed || !primary.Actor.Targetable))
        {
            RaidenTimeline?.Stop();
            var arenaRule = Definition.Rules[0];
            raidenRules.Remove(arenaRule);
            Casts.RemoveAll(c => c.Rule == arenaRule);
            RaidenPhase = 1;
            foreach (var rule in Definition.Rules.Skip(5)) ResetRaiden(rule.Component);
            RaidenTimeline = new(this);
            RaidenTimeline.Begin();
        }
        if (RaidenPhase == 1 && PrimaryPhaseComplete(primary))
        {
            ClearRaiden();
            Active = false;
            Completed = true;
            Casts.Clear();
            ClearHashmal();
            return;
        }
        RaidenTimeline?.Update();
        // IconStackSpread.Update follows the state machine. Dead entries can
        // still block a component condition during this framework update.
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread && c.Target?.Actor.Dead == true);
    }

    private void RaidenCreated(BozjaActor actor, BozjaRule rule)
    {
        if (!IsRaiden || !Active || !actor.NativeNpc || actor.Actor.Oid != 0x2607) return;
        if (rule.Kind == BozjaRuleKind.RaidenSpread) Casts.RemoveAll(c => c.Rule == rule);
        if (rule.Kind == BozjaRuleKind.RaidenLancing)
            Casts.Add(new(++serial, rule, actor, DungeonEncounter.QuantizedDungeonPosition(actor.Actor.Position), 0, Now.AddSeconds(1)));
    }

    private void RaidenCreated(BozjaActor actor)
    {
        if (IsRaiden)
            foreach (var rule in raidenRules) RaidenCreated(actor, rule);
    }

    private void StartRaiden(BozjaActor actor, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.RaidenArena)
        {
            Casts.RemoveAll(c => c.Rule == rule);
            Casts.Add(new(++serial, rule, actor, Center, 0, Activation(actor, 3.4d)));
        }
    }

    private void RaidenAnimation(ulong source, uint state)
    {
        if (!IsRaiden || !Active || state != 0x00010002
            || !raidenRules.Any(r => r.Kind == BozjaRuleKind.RaidenArena)
            || Actors.GetValueOrDefault(source)?.Actor.Oid != 0x1EA1A1) return;
        var reduced = (LocalPolygonGeometry)BaldesionArenas.RaidenDefault(0);
        // MiniArena.Bounds does not replace MiniArena.Center.
        Arena = new LocalPolygonGeometry(Center, 0, reduced.LocalPoints);
        RaidenReduced = true;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.RaidenArena);
    }

    private void RaidenIcon(BozjaActor actor, uint icon)
    {
        if (!IsRaiden || !Active || icon != 138
            || raidenRules.FirstOrDefault(r => r.Kind == BozjaRuleKind.RaidenSpread) is not { } rule) return;
        Casts.Add(new(++serial, rule, Primary!, default, 0, Now.AddSeconds(6)) { Target = actor });
    }

    private void RaidenEffect(uint action, ulong target)
    {
        if (!IsRaiden) return;
        foreach (var rule in raidenRules.Where(r => r.Actions.Contains(action)))
            raidenCounts[rule.Component] = RaidenCount(rule.Component) + 1;
        if (action != 14455 || !raidenRules.Any(r => r.Kind == BozjaRuleKind.RaidenLancing)) return;
        Casts.RemoveAll(c => c.Rule.Kind == BozjaRuleKind.RaidenLancing);
        var index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread && c.Target?.Actor.Id == target);
        if (index < 0) index = Casts.FindIndex(c => c.Rule.Kind == BozjaRuleKind.RaidenSpread);
        if (index >= 0) Casts.RemoveAt(index);
    }

    private void RaidenTether(BozjaActor source, (uint Id, ulong Target) tether, bool remove)
    {
        if (!IsRaiden || !Active || tether.Id != 18
            || !raidenRules.Any(r => r.Kind == BozjaRuleKind.RaidenChains)) return;
        if (remove) { Set(source.Actor.Id, null); Set(tether.Target, null); }
        else
        {
            RaidenTethersAssigned = true;
            if (Actors.GetValueOrDefault(tether.Target) is not { } target) return;
            Set(source.Actor.Id, target);
            Set(target.Actor.Id, source);
        }
        void Set(ulong id, BozjaActor? target)
        {
            var slot = Party.FirstOrDefault(p => p.EntityId == id)?.Slot;
            if (slot is >= 0 and < 64) raidenPartners[slot.Value] = target;
        }
    }

    internal BozjaActor? RaidenPartner(BozjaActor viewer)
    {
        var slot = Party.FirstOrDefault(p => p.EntityId == viewer.Actor.Id)?.Slot;
        return slot is >= 0 and < 64 ? raidenPartners[slot.Value] : null;
    }

    private IEnumerable<BozjaHint> RaidenHints(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind == BozjaRuleKind.RaidenChains && RaidenPartner(viewer) != null) yield return new("Break the chains!");
        if (rule.Kind != BozjaRuleKind.RaidenSpread) yield break;
        var spreads = Spreads(rule).ToArray();
        if (spreads.FirstOrDefault(c => ReferenceEquals(c.Target, viewer)) is { } own)
            yield return new("Spread!", Members().Any(a => !ReferenceEquals(a, viewer)
                && DistanceSquared(a.Actor.Position, DungeonEncounter.QuantizedDungeonPosition(own.Target!.Actor.Position)) <= 100));
        if (spreads.Any(c => !ReferenceEquals(c.Target, viewer) && DistanceSquared(viewer.Actor.Position,
            DungeonEncounter.QuantizedDungeonPosition(c.Target!.Actor.Position)) <= 100)) yield return new("GTFO from spreads!");
    }

    private IEnumerable<BozjaZone> RaidenNavigation(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind is BozjaRuleKind.RaidenArena or BozjaRuleKind.RaidenLancing)
            foreach (var cast in Casts.Where(c => c.Rule == rule))
                yield return new($"raiden/aoe/{cast.Generation}", rule.Component, cast.Activation,
                    cast.Shape, cast.Origin, cast.Rotation);
        if (rule.Kind == BozjaRuleKind.RaidenChains && RaidenPartner(viewer) is { } partner)
            yield return new("raiden/chains", rule.Component, Now.AddSeconds(10),
                new CircleTemplate(MathF.Sqrt(DistanceSquared(partner.Actor.Position, viewer.Actor.Position)) + 1),
                partner.Actor.Position, 0);
        if (rule.Kind == BozjaRuleKind.RaidenSpread)
            foreach (var zone in CrystalSpreadNavigation(viewer, rule)) yield return zone;
    }

    private IEnumerable<BozjaDamage> RaidenDamage(BozjaRule rule)
    {
        if (rule.Kind != BozjaRuleKind.RaidenSpread) yield break;
        var spreads = Spreads(rule).ToArray();
        ulong mask = 0;
        foreach (var spread in spreads)
        {
            var slot = Party.FirstOrDefault(p => p.EntityId == spread.Target!.Actor.Id)?.Slot;
            if (slot is >= 0 and < 64) mask |= 1UL << slot.Value;
        }
        if (mask != 0) yield return new(mask, spreads.Min(c => c.Activation), "Raidwide");
    }
}
