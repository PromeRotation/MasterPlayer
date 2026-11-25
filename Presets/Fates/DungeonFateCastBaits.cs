using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureCastBait> fateCastBaits = [];
    private long fateCastBaitId;
    private bool IsFateCastBait(uint action) => IsChi && action == 25972 || IsMica && action == 38705;
    private static float FateBaitRadius(uint action) => action == 25972 ? 5 : 6;

    private void FateBaitCast(DungeonPendingCast cast, bool correction)
    {
        if (!IsFateCastBait(cast.Rule.Action)) return;
        var index = correction ? fateCastBaits.FindIndex(b => b.Cast.Source == cast.Source) : -1;
        if (correction && index < 0) return;
        var old = index >= 0 ? fateCastBaits[index] : null;
        var sameTarget = old?.Target.Id == cast.TargetId;
        var target = sameTarget ? old!.Target : _actors.GetValueOrDefault(cast.TargetId);
        if (target == null)
        {
            if (index >= 0) fateCastBaits.RemoveAt(index);
            return;
        }
        var source = _actors[cast.Source];
        var bait = new TreasureCastBait(old?.Id ?? ++fateCastBaitId, old?.Source ?? source, target, cast,
            sameTarget && old!.TargetRemoved, sameTarget ? old!.SameActor : source.Id == target.Id,
            old?.SourceRemoved ?? false);
        if (index >= 0) fateCastBaits[index] = bait;
        else fateCastBaits.Add(bait);
    }

    private void FateBaitObserve()
    {
        for (var i = 0; i < fateCastBaits.Count; ++i)
        {
            var bait = fateCastBaits[i];
            var source = bait.SourceRemoved ? null : _actors.GetValueOrDefault(bait.Source.Id);
            var target = bait.TargetRemoved ? null : _actors.GetValueOrDefault(bait.Target.Id);
            if (source?.Oid != bait.Source.Oid) source = null;
            if (target?.Oid != bait.Target.Oid) target = null;
            fateCastBaits[i] = bait with
            {
                Source = source ?? bait.Source, SourceRemoved = bait.SourceRemoved || source == null,
                Target = target ?? bait.Target, TargetRemoved = bait.TargetRemoved || target == null,
            };
        }
    }

    private void FateBaitRemoved(ulong id)
    {
        for (var i = 0; i < fateCastBaits.Count; ++i)
            fateCastBaits[i] = fateCastBaits[i] with
            {
                SourceRemoved = fateCastBaits[i].SourceRemoved || fateCastBaits[i].Source.Id == id,
                TargetRemoved = fateCastBaits[i].TargetRemoved || fateCastBaits[i].Target.Id == id,
            };
    }

    private void FateBaitFinish(DungeonPendingCast cast)
    {
        if (!IsFateCastBait(cast.Rule.Action)) return;
        var index = fateCastBaits.FindIndex(b => b.Cast.Source == cast.Source);
        if (index >= 0) fateCastBaits.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> FateBaitAoes()
    {
        foreach (var bait in fateCastBaits.Where(b => !b.Source.Dead))
        {
            var shape = new CircleTemplate(FateBaitRadius(bait.Cast.Rule.Action));
            var self = !bait.TargetRemoved && bait.Target.Id == PlayerId;
            yield return new($"fate/bait/{bait.Id}/display", bait.Cast.Rule.Name, shape, bait.Target.Position,
                Navigate: false, OutlineOnly: self);
            if (!self)
                yield return new($"fate/bait/{bait.Id}/avoid", bait.Cast.Rule.Name, shape, bait.Target.Position,
                    Display: false, Activation: bait.Cast.FinishAt);
            else if (!bait.SameActor)
                foreach (var member in FateParty.Where(a => a.Id != PlayerId))
                    yield return new($"fate/bait/{bait.Id}/party/{member.Id}", "诱导远离队友", shape,
                        member.Position, Display: false, Activation: bait.Cast.FinishAt);
        }
    }

    private IEnumerable<string> FateBaitHints(Vector3 player)
    {
        if (fateCastBaits.Count > 0) yield return "Tankbuster cleave";
        foreach (var bait in fateCastBaits.Where(b => !b.Source.Dead))
        {
            var geometry = new CircleTemplate(FateBaitRadius(bait.Cast.Rule.Action)).Resolve(bait.Target.Position, 0, null);
            if (!bait.TargetRemoved && bait.Target.Id == PlayerId)
            {
                if (FateParty.Any(a => a.Id != PlayerId && geometry.Contains(a.Position, 0)))
                    yield return "Bait away from raid!";
            }
            else if (geometry.Contains(player, 0))
                yield return "GTFO from baited aoe!";
        }
    }

    private IEnumerable<DungeonDamagePrediction> FateBaitDamage()
    {
        if (fateCastBaits.Count == 0) yield break;
        foreach (var bait in fateCastBaits.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
            if (!bait.TargetRemoved && FateParty.FirstOrDefault(a => a.Id == bait.Target.Id) is { } target)
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name,
                    fateCastBaits[0].Cast.FinishAt, "Tankbuster");
    }
}
