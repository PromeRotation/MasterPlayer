using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Replay;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    internal bool IsVirtue => Definition.PrimaryOid == 0x25DC;
    internal BozjaDelubrumTimeline? VirtueTimeline { get; private set; }
    private readonly List<BozjaRule> virtueRules = [];
    private readonly Dictionary<string, int> virtueCounts = [];
    private readonly Dictionary<BozjaRule, List<(BozjaActor Source, BozjaActor? Target, ulong TargetId)>> virtueTethers = [];
    internal int VirtueCount(string name) => virtueCounts.GetValueOrDefault(name);
    internal int VirtueCasters(string name) => Casts.Count(c => c.Rule.Component == name);
    internal IEnumerable<IGeometryShape> ConvertNavigation(BozjaZone zone, Vector3 center, float radius)
        => IsIrminsul && zone.Shape is BozjaMeteorTemplate sight
            ? [new IrminsulSightDangerGeometry(sight.Geometry, center, radius)]
            : zone.Join != null && (IsVirtue || (IsAllianceStandard || IsArachne) && !zone.Goal
                && Definition.Rules.Any(r => r.Kind == BozjaRuleKind.Stack && r.Component == zone.Component))
            ? [new BozjaVirtueTowerGeometry(zone, center, radius)]
            : zone.Convert(center, radius);
    internal bool VirtueInvalidTower(BozjaRule rule)
        => IsVirtue && rule.Kind == BozjaRuleKind.Tower
            && Casts.Any(c => c.Rule == rule && c.AllowedSoakers?.Contains(null!) == true);

    internal IEnumerable<string> VirtueDiagnostics(BozjaActor viewer)
    {
        if (!IsVirtue) yield break;
        foreach (var rule in virtueRules)
        {
            if (VirtueInvalidTower(rule))
                yield return $"{rule.Component}：连线目标未解析，源塔查询异常；保留原引用并暂停其导航与伤害预测";
            if (rule.Kind == BozjaRuleKind.VirtueExplosion
                && VirtueTethers(rule).Count(t => t.TargetId == viewer.Actor.Id) > 1)
                yield return $"{rule.Component}：重复目标连线产生源空形状，保留诊断项并跳过其绘制";
        }
    }

    internal BozjaActor? VirtueTowerSource(BozjaActor viewer, BozjaRule rule)
        => IsVirtue && rule.Kind == BozjaRuleKind.Tower
            && !VirtueInvalidTower(rule)
            ? VirtueTethers(rule).FirstOrDefault(t => ReferenceEquals(t.Target, viewer)).Source : null;

    private void BeginVirtue()
    {
        virtueRules.AddRange(Definition.Rules.Take(10));
        VirtueTimeline = new(this);
        VirtueTimeline.Begin();
    }

    private void ClearVirtue()
    {
        VirtueTimeline?.Stop();
        VirtueTimeline = null;
        virtueRules.Clear();
        virtueCounts.Clear();
        virtueTethers.Clear();
    }

    internal void SetVirtueCounter(string component, bool active)
    {
        var rule = Definition.Rules.Single(r => r.Component == component);
        virtueRules.Remove(rule);
        virtueCounts[component] = 0;
        if (active) virtueRules.Add(rule);
    }

    private void UpdateVirtue()
    {
        if (!IsVirtue) return;
        if (Primary == null || PrimaryPhaseComplete(Primary))
        {
            ClearVirtue();
            Casts.Clear();
            Active = false;
            Completed = true;
            return;
        }
        VirtueTimeline?.Update();
        foreach (var rule in virtueRules.Where(r => r.Kind == BozjaRuleKind.Tower))
        {
            var allowed = VirtueTethers(rule).Select(t => t.Target!).ToHashSet();
            foreach (var tower in Casts.Where(c => c.Rule == rule)) tower.AllowedSoakers = allowed;
        }
    }

    private List<(BozjaActor Source, BozjaActor? Target, ulong TargetId)> VirtueTethers(BozjaRule rule)
    {
        if (!virtueTethers.TryGetValue(rule, out var value)) virtueTethers.Add(rule, value = []);
        return value;
    }

    private void StartVirtue(BozjaActor actor, BozjaRule rule)
    {
        var spell = actor.Cast!;
        var shape = rule.Shape;
        if (rule.Kind == BozjaRuleKind.VirtueCounter) return;
        if (rule.Kind == BozjaRuleKind.VirtueAurora)
        {
            var status = spell.ActionId is 14218 or 14231 ? 1711u : 1710u;
            if (!actor.Statuses.Any(s => s?.Id == status)) return;
        }
        if (rule.Kind == BozjaRuleKind.VirtueRays)
        {
            var status = spell.ActionId is 14222 or 14223 ? 1711u : 1710u;
            if (Primary!.Statuses.Any(s => s?.Id == status)) shape = new CircleTemplate(16);
        }
        Casts.Add(new(++serial, rule, actor, spell.Location,
            rule.Kind == BozjaRuleKind.VirtueAurora ? spell.Rotation ?? 0 : 0, Activation(actor)) { ShapeOverride = shape });
    }

    private void FinishVirtue(BozjaActor actor, BozjaRule rule, ReplayCast spell)
    {
        if (rule.Kind == BozjaRuleKind.VirtueAurora)
        {
            if (spell.ActionId is 14217 or 14230)
            {
                var index = Casts.FindIndex(c => c.Rule == rule);
                if (index >= 0) Casts.RemoveAt(index);
            }
        }
        else if (rule.Kind == BozjaRuleKind.VirtueRays)
        {
            if (rule.Actions.Contains(spell.ActionId)) Casts.RemoveAll(c => c.Rule == rule);
        }
    }

    private void VirtueEffect(uint action)
    {
        if (!IsVirtue) return;
        foreach (var rule in virtueRules.Where(r => r.Kind == BozjaRuleKind.VirtueCounter && r.Actions.Contains(action)))
            virtueCounts[rule.Component] = VirtueCount(rule.Component) + 1;
    }

    private void VirtueTether(BozjaActor source, (uint Id, ulong Target) tether, bool remove, BozjaRule? only = null)
    {
        if (!IsVirtue || !Active) return;
        foreach (var rule in virtueRules.Where(r => only == null || r == only))
        {
            if (rule.Kind == BozjaRuleKind.VirtueExplosion)
            {
                var list = VirtueTethers(rule);
                if (remove)
                {
                    var index = list.FindIndex(t => ReferenceEquals(t.Source, source) && t.TargetId == tether.Target);
                    if (index >= 0) list.RemoveAt(index);
                }
                else list.Add((source, null, tether.Target));
            }
            if (rule.Kind != BozjaRuleKind.Tower
                || tether.Id != (rule.Component == "DarkAuroraTether" ? 1u : 2u)) continue;
            var target = Actors.GetValueOrDefault(tether.Target);
            var towers = VirtueTethers(rule);
            if (remove)
            {
                var index = towers.FindIndex(t => ReferenceEquals(t.Source, source) && ReferenceEquals(t.Target, target));
                if (index >= 0) towers.RemoveAt(index);
            }
            else towers.Add((source, target, tether.Target));
        }
    }

    private void VirtueAnimation(ulong source, uint state)
    {
        if (!IsVirtue || !Active || state != 0x00040008 || Actors.GetValueOrDefault(source) is not { } actor) return;
        foreach (var rule in virtueRules.Where(r => r.Kind == BozjaRuleKind.Tower && r.EnemyOid == actor.Actor.Oid))
        {
            var index = Casts.FindIndex(c => c.Rule == rule && c.Origin.X == actor.Actor.Position.X && c.Origin.Z == actor.Actor.Position.Z);
            if (index >= 0) Casts.RemoveAt(index);
        }
    }

    private IEnumerable<BozjaAoe> VirtueAoes(BozjaActor viewer, BozjaRule rule)
    {
        if (rule.Kind is BozjaRuleKind.VirtueAurora or BozjaRuleKind.VirtueRays)
            foreach (var cast in Casts.Where(c => c.Rule == rule)) yield return new(cast, false, true);
        if (rule.Kind != BozjaRuleKind.VirtueExplosion) yield break;
        var list = VirtueTethers(rule);
        var own = list.Any(t => t.TargetId == viewer.Actor.Id);
        foreach (var tether in list.Where(t => t.TargetId != viewer.Actor.Id))
            yield return new(new(-tether.Source.BirthOrder, rule, tether.Source,
                DungeonEncounter.QuantizedDungeonPosition(tether.Source.Actor.Position), 0, default), false, !own);
        // Duplicate targetIDs leave a default AOE tail in the source array.
        // Keep that diagnostic entry rather than silently deduplicating input.
        var empty = list.Count - (own ? 1 : 0) - list.Count(t => t.TargetId != viewer.Actor.Id);
        for (var i = 0; i < empty; ++i)
            yield return new(new(0, rule with { Shape = null }, Primary!, default, 0, default), false, false);
    }

    private IEnumerable<BozjaHint> VirtueHints(BozjaActor viewer, BozjaRule rule)
    {
        if (!IsVirtue) yield break;
        if (rule.Kind == BozjaRuleKind.Tower && VirtueTethers(rule).Any(t => ReferenceEquals(t.Target, viewer)))
        {
            yield return new("Stand in a tower of opposite tether element!",
                !Casts.Any(c => c.Rule == rule && Soakers(c).Contains(viewer) && Inside(c, viewer)));
        }
    }

    private IEnumerable<BozjaZone> VirtueNavigation(BozjaActor viewer, BozjaRule rule)
    {
        foreach (var aoe in VirtueAoes(viewer, rule).Where(a => a.Risky))
            yield return new($"virtue/aoe/{aoe.Cast.Generation}", rule.Component, aoe.Cast.Activation,
                aoe.Cast.Shape, aoe.Cast.Origin, aoe.Cast.Rotation);
    }
}
