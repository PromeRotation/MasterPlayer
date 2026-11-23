using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> QitanaAoes()
    {
        if (territory != QitanaCatalog.TERRITORY || Arena == null) yield break;
        foreach (var aoe in qitanaStatues.Values.Concat(qitanaPreliminaryTowers.Values).Concat(qitanaTowers.Values)) yield return aoe;
        if (qitanaLight != null) yield return qitanaLight;
        foreach (var poison in qitanaPoison) yield return poison;
        var zones = _actors.Values.Where(a => Active?.BossOid == 0x27B1 && a.Oid == 0x1E972C && a.EventState != 7 && !a.Dead).ToArray();
        foreach (var zone in zones)
            yield return new($"qitana/poison/{zone.Id}", "持续毒池", new CircleTemplate(6), zone.Position);
        foreach (var (key, tether) in qitanaHounds)
            if (_actors.TryGetValue(key.Source, out var enemy) && _actors.TryGetValue(key.Target, out var target))
            {
                var delta = target.Position - enemy.Position;
                yield return new($"qitana/hound/{key.Source}/{key.Target}", tether.Id == 1 ? "连线已拉远" : "拉远连线",
                    RayRectangleTemplate.Symmetric(.12f, MathF.Max(.01f, new Vector2(delta.X, delta.Z).Length())), enemy.Position,
                    MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: tether.Id == 1, Activation: tether.Activation);
                // This encounter does not opt into tether knockback immunity.
                if (key.Target == PlayerId) yield return new($"qitana/hound/avoid/{key.Source}", "离连线来源至少十九码",
                    new CircleTemplate(19), enemy.Position, Display: false, Activation: tether.Activation);
            }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 15525))
            if (_actors.TryGetValue(cast.TargetId, out var target) && !target.Dead)
            {
                yield return new($"qitana/stack/{cast.Source}", "四人分摊", new CircleTemplate(6), target.Position,
                    Navigate: false, Safe: true, Activation: cast.FinishAt);
                if (PlayerId != target.Id) yield return new($"qitana/stack/enter/{cast.Source}", "进入四人分摊",
                    new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 6)]), Arena.Position,
                    Display: false, Activation: cast.FinishAt);
            }
        if (!_actors.TryGetValue(PlayerId, out var player) || player.Dead) yield break;
        foreach (var bait in _casts.Values.Where(c => c.Rule.Action == 15517 && c.TargetId == PlayerId))
            yield return new($"qitana/bait/edge/{bait.Source}", "去场地两侧诱导毒液", new RectangleTemplate(26, 40),
                new(17, player.Position.Y, -538), Display: false, Activation: bait.FinishAt);
        var poisons = zones.Select(z => z.Position).Concat(qitanaPoison.Select(p => p.Position))
            .Concat(_casts.Values.Where(c => c.Rule.Action == 15518).Select(c => c.Location)).ToArray();
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 17168 or 15520))
        {
            if (KnockbackImmune(player, cast.FinishAt)) continue;
            var pull = cast.Rule.Action == 17168;
            var template = new QitanaDisplacementTemplate(Arena, cast.Location, cast.Rotation, pull,
                poisons.Select(p => (IGeometryShape)new CircleGeometry(p, 6)).ToArray());
            yield return new($"qitana/displacement/{cast.Source}", pull ? "拉近后避开毒池" : "击退后避开毒池",
                template, Arena.Position, Display: false, Activation: cast.FinishAt);
            // Preserve the source positioning lanes, including every poison pool.
            var heading = pull ? _actors.GetValueOrDefault(PrimaryId)?.Rotation ?? cast.Rotation : 0;
            for (var i = 0; i < poisons.Length; ++i)
                yield return new($"qitana/displacement/lane/{cast.Source}/{i}", "避开毒池击退通道",
                    pull ? RayRectangleTemplate.Symmetric(12, 40) : new RectangleTemplate(12, 80),
                    poisons[i], heading, Display: false, Activation: cast.FinishAt);
            foreach (var preview in DungeonMovementPreview($"qitana/displacement/preview/{cast.Source}", "强制位移落点",
                player.Position, template.Landing(player.Position))) yield return preview;
        }
    }
}
