using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    internal bool IsNybeth => territory == HeavenswardIntroCatalog.NYBETH;
    private readonly List<PalaceNybethBait> nybethBaits = [];
    private long nybethBaitGeneration;
    internal IReadOnlyList<PalaceNybethBait> NybethBaits => nybethBaits;

    private void NybethCast(DungeonPendingCast cast, bool correction)
    {
        if (!IsNybeth || cast.Rule.Action != 6872) return;
        if (correction)
        {
            var index = nybethBaits.FindIndex(b => b.Cast.Source == cast.Source);
            if (index >= 0)
            {
                var previous = nybethBaits[index];
                if (previous.Target.Actor.Id == cast.TargetId)
                    nybethBaits[index] = previous with { Cast = cast };
                else if (palaceGuideActors.TryGetValue(cast.TargetId, out var correctedTarget))
                    nybethBaits[index] = new(++nybethBaitGeneration, previous.Source, correctedTarget, cast);
                else nybethBaits.RemoveAt(index);
            }
            return;
        }
        if (palaceGuideActors.TryGetValue(cast.Source, out var source) && palaceGuideActors.TryGetValue(cast.TargetId, out var target))
            nybethBaits.Add(new(++nybethBaitGeneration, source, target, cast));
    }

    private void NybethFinish(DungeonPendingCast cast)
    {
        if (!IsNybeth || cast.Rule.Action != 6872) return;
        var index = nybethBaits.FindIndex(b => b.Cast.Source == cast.Source);
        if (index >= 0) nybethBaits.RemoveAt(index);
    }

    private IEnumerable<DungeonAmbientAoe> NybethAoes()
    {
        if (!IsNybeth || !palaceGuideEnabled) yield break;
        if (Active == null && palaceGuideLoaded is { Started: false } loaded)
        {
            var primary = loaded.Primary.Actor;
            var arena = Definitions[0].Arena(primary.Position);
            yield return new("nybeth/precombat/arena", "场地", new LocalPolygonTemplate(
                arena.GetContours()[0].Select(p => MasterPlayer.Spatial.Geometry.GeometryHelper.WorldToLocal(p, arena.Position, 0))),
                arena.Position, Navigate: false, OutlineOnly: true);
            if (!primary.Dead && primary.Targetable)
                yield return HeavenswardIntroEnemyMarker(primary, arena, "nybeth/precombat/enemy", new(1, 0, 0, 1));
        }
        if (Active == null) yield break;
        foreach (var bait in nybethBaits.Where(b => !b.Source.Actor.Dead))
        {
            var prefix = $"nybeth/abyss/{bait.Generation}";
            yield return new(prefix + "/display", "Abyss", new CircleTemplate(6), bait.Target.Actor.Position,
                Navigate: false, OutlineOnly: bait.Target.Actor.Id == PlayerId);
            var personal = palaceGuideActors.TryGetValue(PlayerId, out var viewerState) && ReferenceEquals(bait.Target, viewerState);
            if (!personal)
                yield return new(prefix + "/avoid", "Abyss", new CircleTemplate(6), bait.Target.Actor.Position,
                    Display: false, Activation: bait.Cast.FinishAt);
            else if (!ReferenceEquals(bait.Source, bait.Target))
                foreach (var member in _actors.Values.Where(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId))
                    yield return new($"{prefix}/party/{member.Id}", "Bait away from raid!", new CircleTemplate(6),
                        member.Position, Display: false, Activation: bait.Cast.FinishAt);
        }
    }

    private IEnumerable<string> NybethHints(Vector3? position)
    {
        if (PalaceGuidePrecombatHint is { } guide) yield return guide;
        if (!IsNybeth || Active == null || position is not { } player) yield break;
        foreach (var bait in nybethBaits.Where(b => !b.Source.Actor.Dead))
        {
            if (bait.Target.Actor.Id == PlayerId)
            {
                if (_actors.Values.Any(a => a.PartySlot is >= 0 and < 8 && !a.Dead && a.Id != PlayerId
                    && NativeEnemyDistanceSquared(a.Position, bait.Target.Actor.Position) <= 36))
                    yield return "Bait away from raid!";
            }
            else if (NativeEnemyDistanceSquared(player, bait.Target.Actor.Position) <= 36)
                yield return "GTFO from baited aoe!";
        }
    }

    private IEnumerable<DungeonDamagePrediction> NybethDamage()
    {
        if (!IsNybeth || Active == null || nybethBaits.Count == 0) yield break;
        foreach (var target in nybethBaits.Where(b => !b.Source.Actor.Dead).Select(b => b.Target.Actor).DistinctBy(a => a.Id))
        {
            var member = CurrentPartyMembers.FirstOrDefault(m => m.EntityId == target.Id);
            var slot = CurrentPartyMembers.Count > 0 ? member?.Slot ?? -1 : _actors.GetValueOrDefault(target.Id)?.PartySlot ?? -1;
            if (slot is >= 0 and < 8)
                yield return new(target.Id, slot, target.PartyRole, target.Name, nybethBaits[0].Cast.FinishAt, "Raidwide");
        }
    }

    private void ResetNybeth()
    {
        nybethBaits.Clear();
        nybethBaitGeneration = 0;
        // Guide lifetime follows the loaded source module, not a combat reset.
    }
}
