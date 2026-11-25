using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool IsTreasureAiravata => IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.ALTAR_AIRAVATA;
    private TreasureBuffetIcon? treasureBuffetIcon;
    private DungeonActor? treasureBuffetPrimary;
    private bool treasureBuffetPrimaryRemoved;
    private readonly List<TreasureCastBait> treasureBuffetBaits = [];
    internal TreasureBuffetIcon? TreasureBuffetTarget => treasureBuffetIcon;
    internal IReadOnlyList<TreasureCastBait> TreasureBuffetBaits => treasureBuffetBaits;

    private void TreasureBuffetObserve()
    {
        if (!IsTreasureAiravata) return;
        if (treasureBuffetPrimary == null) treasureBuffetPrimary = _actors.GetValueOrDefault(PrimaryId);
        else if (!treasureBuffetPrimaryRemoved)
        {
            if (_actors.TryGetValue(treasureBuffetPrimary.Id, out var primary) && primary.Oid == treasureBuffetPrimary.Oid)
                treasureBuffetPrimary = primary;
            else treasureBuffetPrimaryRemoved = true;
        }
        if (treasureBuffetIcon is { Removed: false } icon)
        {
            var target = _actors.GetValueOrDefault(icon.Target.Id);
            treasureBuffetIcon = target?.Oid == icon.Target.Oid ? icon with { Target = target } : icon with { Removed = true };
        }
        for (var i = 0; i < treasureBuffetBaits.Count; ++i)
        {
            var bait = treasureBuffetBaits[i];
            var source = _actors.GetValueOrDefault(bait.Source.Id);
            var target = !bait.TargetRemoved ? _actors.GetValueOrDefault(bait.Target.Id) : null;
            treasureBuffetBaits[i] = bait with
            {
                Source = source?.Oid == bait.Source.Oid ? source : bait.Source,
                Target = target?.Oid == bait.Target.Oid ? target : bait.Target,
                TargetRemoved = bait.TargetRemoved || target?.Oid != bait.Target.Oid,
            };
        }
    }

    private void TreasureBuffetRemoved(ulong id)
    {
        if (treasureBuffetPrimary?.Id == id) treasureBuffetPrimaryRemoved = true;
        if (treasureBuffetIcon?.Target.Id == id) treasureBuffetIcon = treasureBuffetIcon with { Removed = true };
        for (var i = 0; i < treasureBuffetBaits.Count; ++i)
            if (treasureBuffetBaits[i].Target.Id == id)
                treasureBuffetBaits[i] = treasureBuffetBaits[i] with { TargetRemoved = true };
    }

    private void TreasureBuffetIconReceived(ulong actorId, uint icon)
    {
        if (IsTreasureAiravata && icon == 23 && _actors.GetValueOrDefault(actorId) is { } target)
            treasureBuffetIcon = new(target, _now, _now.AddSeconds(3.1), false);
    }

    private void TreasureBuffetStart(DungeonPendingCast cast, bool correction, bool initialRaw)
    {
        if (!IsTreasureAiravata || cast.Rule.Action != 13374) return;
        var index = correction ? treasureBuffetBaits.FindIndex(b => b.Cast.Source == cast.Source) : -1;
        if (correction && index < 0 && !initialRaw) return;
        var old = index >= 0 ? treasureBuffetBaits[index] : null;
        var sameTarget = old?.Target.Id == cast.TargetId;
        var target = sameTarget ? old!.Target : _actors.GetValueOrDefault(cast.TargetId);
        if (target == null)
        {
            if (index >= 0) treasureBuffetBaits.RemoveAt(index);
            return;
        }
        var source = old?.Source ?? _actors[cast.Source];
        var bait = new TreasureCastBait(old?.Id ?? ++treasureCastBaitId, source, target, cast,
            sameTarget && old!.TargetRemoved, source.Id == target.Id);
        if (index >= 0) treasureBuffetBaits[index] = bait;
        else treasureBuffetBaits.Add(bait);
    }

    private void TreasureBuffetFinish(DungeonPendingCast cast)
    {
        if (cast.Rule.Action != 13374) return;
        treasureBuffetIcon = null;
        var index = treasureBuffetBaits.FindIndex(b => b.Source.Id == cast.Source);
        if (index >= 0) treasureBuffetBaits.RemoveAt(index);
    }

    private bool TreasureBuffetViewerTarget => treasureBuffetBaits.Any(b =>
        !b.Source.Dead && !b.TargetRemoved && b.Target.Id == PlayerId);

    private Vector3 TreasureBuffetOrigin(TreasureCastBait bait)
    {
        var primary = treasureBuffetPrimary!;
        var delta = bait.Target.Position - primary.Position;
        delta.Y = 0;
        var length = delta.Length();
        var max = bait.Target.HitboxRadius + primary.HitboxRadius;
        var min = length > max ? length : Math.Clamp(length, 0, max);
        var direction = length > 0 ? delta / length : Vector3.Zero;
        return bait.Target.Position - min * direction;
    }

    private IEnumerable<DungeonAmbientAoe> TreasureBuffetAoes()
    {
        if (treasureBuffetPrimary is not { } primary || Arena == null) yield break;
        if (treasureBuffetBaits.FirstOrDefault() is { } bait)
        {
            var self = TreasureBuffetViewerTarget;
            var origin = TreasureBuffetOrigin(bait);
            var rotation = TreasureCastBaitRotation(bait);
            var shape = new FanTemplate(30, 2 * MathF.PI / 3);
            yield return new("treasure/buffet/cone", "Buffet", shape, origin, rotation, Navigate: false, OutlineOnly: self);
            if (!self)
                yield return new("treasure/buffet/avoid", "Buffet", shape, origin, rotation, Display: false);
        }
        if (treasureBuffetIcon is not { Removed: false } icon || icon.Target.Id != PlayerId) yield break;
        // Bait-away navigation is unconditional; immunity only suppresses the displacement projection.
        yield return new("treasure/buffet/edge", "Bait away!", new CircleTemplate(17.5f), Arena.Position, Display: false);
        if (WhorlImmune(icon.Target, icon.Activation)) yield break;
        var deltaToPrimary = icon.Target.Position - primary.Position;
        var directionAngle = MathF.Atan2(deltaToPrimary.X, deltaToPrimary.Z);
        var direction = new Vector3(MathF.Sin(directionAngle), 0, MathF.Cos(directionAngle));
        var landing = TreasureWallLanding(icon.Target.Position, direction, icon.Distance, icon.Target.HitboxRadius);
        foreach (var aoe in QuestKnockbackProjection("treasure/buffet/kb", icon.Target, icon.Target.Position, landing))
            yield return aoe;
    }

    private IEnumerable<string> TreasureBuffetHints(Vector3? player)
    {
        if (treasureBuffetIcon is { Removed: false } icon && icon.Target.Id == PlayerId) yield return "Bait away!";
        if (player is not { } position) yield break;
        // Base BaitAwayCast text uses target-centered geometry, unlike Buffet2's drawing/navigation.
        foreach (var bait in treasureBuffetBaits.Where(b => !b.Source.Dead))
        {
            var shape = new FanTemplate(30, 2 * MathF.PI / 3).Resolve(bait.Target.Position, TreasureCastBaitRotation(bait), null);
            if (bait.Target.Id == PlayerId && treasureParty.Any(id => id != bait.Target.Id
                && _actors.GetValueOrDefault(id) is { Dead: false } member && shape.Contains(member.Position, 0)))
                yield return "Bait away from raid!";
            if (bait.Target.Id != PlayerId && shape.Contains(position, 0)) yield return "GTFO from baited aoe!";
        }
    }
}
