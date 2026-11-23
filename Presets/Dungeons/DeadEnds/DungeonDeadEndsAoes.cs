using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> DeadEndsAoes()
    {
        if (territory != DeadEndsCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in deadEndsExaAoes) yield return aoe with { Navigate = deadEndsWave == null };
        if (deadEndsWave is { } wave)
        {
            yield return new("deadends/wave/donut", "恶心月环", wave.Rule.Shape!, wave.Location, Activation: wave.FinishAt);
            if (deadEndsWaveWind != 0)
            {
                var safe = new DeadEndsWaveGeometry(wave.Location, deadEndsWaveWind == 1 ? MathF.PI : 0);
                yield return new("deadends/wave/safe", "等待月环结算的安全缺口", new DungeonShapeTemplate(safe), wave.Location,
                    Navigate: false, Safe: true, Activation: wave.FinishAt);
                yield return new("deadends/wave/enter", "进入月环安全缺口", new SohrKhaiWallHintTemplate(Arena, [safe]), Arena.Position,
                    Display: false, Activation: wave.FinishAt);
            }
        }
        if (deadEndsSolitude != 0 && _actors.TryGetValue(deadEndsSolitude, out var solitude))
        {
            yield return new("deadends/solitude", "四人紧密集合", new CircleTemplate(1.5f), solitude.Position, Navigate: false, Safe: true, OutlineOnly: true);
            var centers = deadEndsSolitude == PlayerId
                ? _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId).Select(a => a.Position).ToArray()
                : [solitude.Position];
            var regions = centers.Select(p => (IGeometryShape)new CircleGeometry(p, deadEndsSolitude == PlayerId ? .75f : 1.5f)).ToArray();
            if (regions.Length > 0)
            {
                yield return new("deadends/solitude/enter", "集合解除胆怯", new SohrKhaiWallHintTemplate(Arena, regions), Arena.Position,
                    Display: false, Activation: deadEndsSolitudeAt);
                if (deadEndsSolitude != PlayerId)
                    yield return new("deadends/solitude/goal", "集合解除胆怯", new CircleTemplate(1.5f), solitude.Position, Display: false, Safe: true);
            }
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 25922 or 25946))
        {
            IGeometryShape[] regions = [];
            if (cast.TargetId == PlayerId)
                regions = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
            else if (_actors.TryGetValue(cast.TargetId, out var target)) regions = [new CircleGeometry(target.Position, 6)];
            if (regions.Length > 0)
                yield return new($"deadends/stack/{cast.Source}", "四人集合分摊", new SohrKhaiWallHintTemplate(Arena, regions), Arena.Position,
                    Display: false, Activation: cast.FinishAt);
        }
        if (deadEndsRing != null) yield return deadEndsRing;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 28360))
        {
            var position = _actors.TryGetValue(cast.TargetId, out var target) ? target.Position : cast.Location;
            yield return new($"deadends/void/cast/{cast.Source}", "电场读条", cast.Rule.Shape!, position, Activation: cast.FinishAt);
        }
        foreach (var aoe in deadEndsVoidPredictions) yield return aoe;
        foreach (var zone in DeadEndsVoidzones())
            yield return new($"deadends/void/actor/{zone.Id}", "持续电场", new CircleTemplate(9), QuantizedDungeonPosition(zone.Position));
        if (deadEndsElimination is { } bait && _actors.TryGetValue(bait.Source, out var source) && _actors.TryGetValue(bait.TargetId, out var baitTarget))
        {
            var rotation = source.Id == baitTarget.Id ? source.Rotation : MathF.Atan2(baitTarget.Position.X - source.Position.X, baitTarget.Position.Z - source.Position.Z);
            yield return new("deadends/elimination", "死刑直线引导", bait.Rule.Shape!, source.Position, rotation,
                Navigate: baitTarget.Id != PlayerId, OutlineOnly: baitTarget.Id == PlayerId, Activation: bait.FinishAt);
            if (baitTarget.Id == PlayerId)
                yield return new("deadends/elimination/away", "死刑直线远离队友",
                    new CastrumBaitTemplate(Arena, source.Position, _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId)
                        .Select(a => a.Position).ToArray(), 100, 5), Arena.Position, Display: false, Activation: bait.FinishAt);
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 25931).Take(1))
        {
            yield return new("deadends/knockback/center", "在中心准备击退", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(Arena.Position, 4)]), Arena.Position, Display: false, Activation: cast.FinishAt);
            var peacefire = _casts.Values.Where(c => c.Rule.Action == 25934).ToArray();
            foreach (var fire in peacefire)
                yield return new($"deadends/knockback/cone/{fire.Source}", "避开火圈方向击退", new FanTemplate(16, MathF.PI * .4f), Arena.Position,
                    MathF.Atan2(fire.Location.X - Arena.Position.X, fire.Location.Z - Arena.Position.Z), Display: false, Activation: cast.FinishAt);
            if (_actors.TryGetValue(PlayerId, out var player) && !KnockbackImmune(player, cast.FinishAt))
            {
                var projection = new VanaspatiKnockbackTemplate(Arena, cast.Location,
                    peacefire.Select(c => c.Rule.Shape!.Resolve(c.Location, c.Rotation, null)).ToArray(), 11);
                var landing = projection.Landing(player.Position);
                yield return new("deadends/knockback/landing", "击退落点", new CircleTemplate(.6f), landing, Navigate: false, OutlineOnly: true);
                var delta = landing - player.Position;
                yield return new("deadends/knockback/line", "击退方向", RayRectangleTemplate.Symmetric(.12f, delta.Length()), player.Position,
                    MathF.Atan2(delta.X, delta.Z), Navigate: false, OutlineOnly: true);
            }
        }
        foreach (var id in deadEndsLightOrder.Take(3))
            if (_casts.TryGetValue(id, out var cast))
                yield return new($"deadends/light/{id}", "连续圆形光", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
    }

    private IEnumerable<DungeonDamagePrediction> DeadEndsDamage()
    {
        if (territory == DeadEndsCatalog.TERRITORY && deadEndsElimination is { } bait
            && _actors.TryGetValue(bait.TargetId, out var target) && target.PartySlot >= 0)
            yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, bait.FinishAt, "Tankbuster");
    }

    private IEnumerable<string> DeadEndsHints()
    {
        if (territory != DeadEndsCatalog.TERRITORY) yield break;
        if (deadEndsWave != null && deadEndsLines.Count > 0) yield return "在标记安全缺口等待月环结算";
        if (_actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 2965))) yield return "坏死：及时驱散队友";
        if (Active?.BossOid != 0x34C7 || !_actors.TryGetValue(PlayerId, out var player)) yield break;
        var doomed = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && HasStatus(a, 1769)).ToArray();
        if (doomed.Any(a => a.Id == PlayerId)) yield return "死宣：尽快恢复至满血";
        else if (player.ClassJob is 6 or 24 or 28 or 33 or 40)
            foreach (var member in doomed) yield return $"死宣：将 {member.Name} 恢复至满血";
    }
}
