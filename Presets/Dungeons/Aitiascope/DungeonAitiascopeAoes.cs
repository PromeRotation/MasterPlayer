using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AitiascopeAoes()
    {
        if (territory != AitiascopeCatalog.TERRITORY || Arena == null) yield break;
        foreach (var cast in aitiascopeShots)
            yield return new($"aitiascope/shot/{cast.Source}", "往返光线", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (aitiascopeShots.Count == 0 && (aitiascopeReturnAt - _now).TotalSeconds < 5)
            foreach (var id in aitiascopeReturnSources)
                if (_actors.TryGetValue(id, out var caster))
                    yield return new($"aitiascope/return/{id}", "回返光线预告", RayRectangleTemplate.Symmetric(6, 20), caster.Position,
                        caster.Rotation, Activation: aitiascopeReturnAt);
        if (aitiascopeBite is { } bait && _actors.TryGetValue(bait.Source, out var source) && _actors.TryGetValue(bait.TargetId, out var target))
        {
            var rotation = source.Id == target.Id ? source.Rotation : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            yield return new("aitiascope/bite", "死刑扇形引导", bait.Rule.Shape!, source.Position, rotation,
                Navigate: target.Id != PlayerId, OutlineOnly: target.Id == PlayerId, Activation: bait.FinishAt);
            if (target.Id == PlayerId)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"aitiascope/bite/away/{other.Id}", "死刑远离队友", new FanTemplate(100, MathF.Tau / 3), source.Position,
                        MathF.Atan2(other.Position.X - source.Position.X, other.Position.Z - source.Position.Z), Display: false, Activation: bait.FinishAt);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25677))
        {
            IGeometryShape[] regions = [];
            if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var stackTarget))
                regions = [new CircleGeometry(stackTarget.Position, 6)];
            if (regions.Length > 0)
                yield return new($"aitiascope/stack/{cast.Source}", "四人集合分摊", new SohrKhaiWallHintTemplate(Arena, regions), Arena.Position,
                    Display: false, Activation: cast.FinishAt);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25680).Take(1))
            yield return new("aitiascope/skewer", "盾冲直线", cast.Rule.Shape!, cast.Location, cast.Rotation,
                Navigate: !aitiascopeSafespots, Activation: cast.FinishAt);
        if (aitiascopeSafespots && aitiascopePockets.Count >= 2)
            yield return new("aitiascope/pockets/enter", "前往即将开放的凹口", new SohrKhaiWallHintTemplate(Arena,
                aitiascopePockets.Take(2).Select(p => (IGeometryShape)new CircleGeometry(p, 5)).ToArray()),
                Arena.Position, Display: false, Activation: aitiascopeSafespotAt);
        foreach (var aoe in aitiascopeShrapnel) yield return aoe;
        foreach (var sourceId in aitiascopeCones.Take(4))
            if (_casts.TryGetValue(sourceId, out var cast))
                yield return new($"aitiascope/cone/{sourceId}", "连续雷扇", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        if (aitiascopeCurtainArena && aitiascopeCurtain is { } curtain && aitiascopeIce.Length > 0)
        {
            yield return new("aitiascope/curtain/safe", "冰块后的安全扇影",
                new DungeonLosTemplate(new(curtain.Location, aitiascopeIce, true, 60, false)), curtain.Location,
                Navigate: false, Safe: true, Activation: curtain.FinishAt);
            yield return new("aitiascope/curtain/enter", "躲到冰块背后",
                new DungeonLosTemplate(new(curtain.Location, aitiascopeIce, false, 60, false)), curtain.Location,
                Display: false, Activation: curtain.FinishAt);
        }
    }

    private IEnumerable<DungeonDamagePrediction> AitiascopeDamage()
    {
        if (territory == AitiascopeCatalog.TERRITORY && aitiascopeBite is { } bait && _actors.TryGetValue(bait.TargetId, out var target) && target.PartySlot >= 0)
            yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, bait.FinishAt, "Tankbuster");
    }
}
