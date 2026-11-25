using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<TreasureCastBait> treasureCastBaits = [];
    private readonly Dictionary<ulong, long> treasureCastBaitCurrent = [];
    private long treasureCastBaitId;

    private void TreasureCastBaitStart(DungeonPendingCast cast, bool correction, bool initialRaw)
    {
        if (!IsNativeTreasure || !NativeTreasureCatalog.CastBaitAction(cast.Rule.Action)) return;
        var hasCurrent = treasureCastBaitCurrent.TryGetValue(cast.Source, out var id);
        var index = correction && hasCurrent ? treasureCastBaits.FindIndex(b => b.Id == id) : -1;
        if (correction && index < 0 && (!initialRaw || hasCurrent)) return;
        var old = index >= 0 ? treasureCastBaits[index] : null;
        var sameTarget = old?.Target.Id == cast.TargetId;
        var target = sameTarget ? old!.Target : _actors.GetValueOrDefault(cast.TargetId);
        if (target == null)
        {
            if (index >= 0) treasureCastBaits.RemoveAt(index);
            return;
        }
        var source = _actors[cast.Source];
        var bait = new TreasureCastBait(old?.Id ?? ++treasureCastBaitId, old?.Source ?? source,
            sameTarget ? old!.Target : target, cast, sameTarget && old!.TargetRemoved,
            sameTarget ? old!.SameActor : source.Id == target.Id, old?.SourceRemoved ?? false);
        if (index < 0) treasureCastBaits.Add(bait);
        else treasureCastBaits[index] = bait;
        treasureCastBaitCurrent[cast.Source] = bait.Id;
    }

    private void TreasureCastBaitObserve()
    {
        for (var i = treasureCastBaits.Count - 1; i >= 0; --i)
        {
            var bait = treasureCastBaits[i];
            var finishOnly = NativeTreasureCatalog.FinishBaitAction(bait.Cast.Rule.Action);
            var source = bait.SourceRemoved ? null : _actors.GetValueOrDefault(bait.Source.Id);
            if (source?.Oid != bait.Source.Oid) source = null;
            var sourceRemoved = bait.SourceRemoved || source == null;
            if (!finishOnly && (source == null || source.Dead))
            {
                treasureCastBaits.RemoveAt(i);
                continue;
            }
            var target = !bait.TargetRemoved ? _actors.GetValueOrDefault(bait.Target.Id) : null;
            if (target?.Oid != bait.Target.Oid) target = null;
            var targetRemoved = bait.TargetRemoved || target == null;
            target ??= bait.Target;
            if (!finishOnly && target.Dead)
            {
                treasureCastBaits.RemoveAt(i);
                continue;
            }
            treasureCastBaits[i] = bait with
            {
                Source = source ?? bait.Source,
                Target = target,
                TargetRemoved = targetRemoved,
                SourceRemoved = sourceRemoved,
            };
        }
    }

    private void TreasureCastBaitRemoved(ulong id)
    {
        treasureCastBaitCurrent.Remove(id);
        for (var i = treasureCastBaits.Count - 1; i >= 0; --i)
        {
            var bait = treasureCastBaits[i];
            if (bait.Source.Id == id && !NativeTreasureCatalog.FinishBaitAction(bait.Cast.Rule.Action))
                treasureCastBaits.RemoveAt(i);
            else
                treasureCastBaits[i] = bait with
                {
                    SourceRemoved = bait.SourceRemoved || bait.Source.Id == id,
                    TargetRemoved = bait.TargetRemoved || bait.Target.Id == id,
                };
        }
    }

    private void TreasureCastBaitEffect(ulong source, uint action)
    {
        if (NativeTreasureCatalog.FinishBaitAction(action)) return;
        var index = treasureCastBaits.FindIndex(b => b.Source.Id == source && b.Cast.Rule.Action == action);
        if (index >= 0) treasureCastBaits.RemoveAt(index);
    }

    private void TreasureCastBaitFinish(DungeonPendingCast cast)
    {
        if (!NativeTreasureCatalog.FinishBaitAction(cast.Rule.Action)) return;
        var index = treasureCastBaits.FindIndex(b => b.Source.Id == cast.Source && b.Cast.Rule.Action == cast.Rule.Action);
        if (index >= 0) treasureCastBaits.RemoveAt(index);
    }

    private static GeometryTemplate TreasureCastBaitShape(TreasureCastBait bait) => bait.Cast.Rule.Action switch
    {
        43650 or 43698 or 43668 or 43768 => new CircleTemplate(5),
        43784 => new CircleTemplate(6),
        43682 => RayRectangleTemplate.Symmetric(8, 65),
        43748 => RayRectangleTemplate.Symmetric(10, 46),
        43640 => RayRectangleTemplate.Symmetric(8, Vector2.Distance(new(bait.Source.Position.X, bait.Source.Position.Z),
            new(bait.Target.Position.X, bait.Target.Position.Z))),
        21709 => RayRectangleTemplate.Symmetric(5, Vector2.Distance(new(bait.Source.Position.X, bait.Source.Position.Z),
            new(bait.Target.Position.X, bait.Target.Position.Z))),
        38281 => RayRectangleTemplate.Symmetric(3, Vector2.Distance(new(bait.Source.Position.X, bait.Source.Position.Z),
            new(bait.Target.Position.X, bait.Target.Position.Z))),
        43607 => new FanTemplate(13, MathF.PI / 2),
        32251 => new FanTemplate(60, MathF.PI / 6),
        43630 => RayRectangleTemplate.Symmetric(8, 60),
        _ => RayRectangleTemplate.Symmetric(8, 40),
    };
    private static Vector3 TreasureCastBaitOrigin(TreasureCastBait bait) =>
        NativeTreasureCatalog.CircleBaitAction(bait.Cast.Rule.Action) ? bait.Target.Position : bait.Source.Position;

    private static float TreasureCastBaitRotation(TreasureCastBait bait) => bait.SameActor
        ? bait.Source.Rotation
        : MathF.Atan2(bait.Target.Position.X - bait.Source.Position.X, bait.Target.Position.Z - bait.Source.Position.Z);

    private IEnumerable<DungeonAmbientAoe> TreasureCastBaitAoes()
    {
        foreach (var bait in treasureCastBaits.Where(b => !b.Source.Dead))
        {
            var shape = TreasureCastBaitShape(bait);
            var angle = TreasureCastBaitRotation(bait);
            var origin = TreasureCastBaitOrigin(bait);
            yield return new($"treasure/cast-bait/{bait.Id}/display", bait.Cast.Rule.Name, shape, origin, angle,
                Navigate: false, OutlineOnly: bait.Target.Id == PlayerId);
            if (bait.TargetRemoved || bait.Target.Id != PlayerId)
            {
                yield return new($"treasure/cast-bait/{bait.Id}/avoid", bait.Cast.Rule.Name, shape, origin, angle,
                    Display: false, Activation: bait.Cast.FinishAt);
                continue;
            }
            if (bait.SameActor) continue;
            foreach (var id in treasureParty)
            {
                if (id == PlayerId || _actors.GetValueOrDefault(id) is not { Dead: false } member) continue;
                if (NativeTreasureCatalog.CircleBaitAction(bait.Cast.Rule.Action))
                {
                    yield return new($"treasure/cast-bait/{bait.Id}/party/{id}", "诱导远离队友",
                        shape, member.Position, Display: false, Activation: bait.Cast.FinishAt);
                    continue;
                }
                var delta = member.Position - origin;
                var distance = new Vector2(delta.X, delta.Z).Length();
                var halfAngle = bait.Cast.Rule.Action switch
                {
                    43607 => MathF.PI / 4,
                    32251 => MathF.PI / 12,
                    21709 => MathF.Asin(2.5f / distance),
                    38281 => MathF.Asin(1.5f / distance),
                    43748 => MathF.Asin(5 / distance),
                    _ => MathF.Asin(4 / distance),
                };
                yield return new($"treasure/cast-bait/{bait.Id}/party/{id}", "诱导远离队友",
                    new FanTemplate(100, 2 * halfAngle), origin, MathF.Atan2(delta.X, delta.Z),
                    Display: false, Activation: bait.Cast.FinishAt);
            }
        }
    }

    private IEnumerable<string> TreasureCastBaitHints(Vector3? player)
    {
        if (treasureCastBaits.Count == 0) yield break;
        if (treasureCastBaits.Any(b => b.Cast.Rule.Action is not (32251 or 43640 or 21709 or 38281))) yield return "Tankbuster cleave";
        if (player is not { } position) yield break;
        foreach (var bait in treasureCastBaits.Where(b => !b.Source.Dead))
        {
            var shape = TreasureCastBaitShape(bait).Resolve(TreasureCastBaitOrigin(bait), TreasureCastBaitRotation(bait), null);
            if (bait.Target.Id == PlayerId && treasureParty.Any(id => id != bait.Target.Id
                && _actors.GetValueOrDefault(id) is { Dead: false } member && shape.Contains(member.Position)))
                yield return "Bait away from raid!";
            if (bait.Target.Id != PlayerId && shape.Contains(position))
                yield return "GTFO from baited aoe!";
        }
    }

    private IEnumerable<DungeonDamagePrediction> TreasureCastBaitDamage()
    {
        if (treasureCastBaits.Count == 0) yield break;
        foreach (var bait in treasureCastBaits.Where(b => !b.Source.Dead).DistinctBy(b => b.Target.Id))
        {
            var slot = Array.IndexOf(treasureParty, bait.Target.Id);
            var target = _actors.GetValueOrDefault(bait.Target.Id) ?? bait.Target;
            if (slot >= 0)
                yield return new(target.Id, slot, target.PartyRole, target.Name, treasureCastBaits[0].Cast.FinishAt,
                    bait.Cast.Rule.Action == 32251 ? "Raidwide" : "Tankbuster");
        }
    }
}
