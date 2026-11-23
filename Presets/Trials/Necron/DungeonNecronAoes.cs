using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> NecronAoes()
    {
        if (!IsNecron || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Oid is 0x4870 or 0x4908 or 0x4904 or 0x4905 or 0x4906 or 0x4909
            && a.Targetable && !a.Dead))
            yield return new($"necron/enemy/{actor.Id}", actor.Name, new CircleTemplate(actor.HitboxRadius), actor.Position,
                Navigate: false, OutlineOnly: true);
        foreach (var aoe in necronHands.Concat(necronFear).Concat(necronCold).Concat(necronRects).Concat(necronInvitations)) yield return aoe;
        if (necronMemento != null) yield return necronMemento;
        if (necronShrink != null) yield return necronShrink;
        if (necronShow)
        {
            if (necronBlights.Count > 0) yield return necronBlights[0];
            if (necronBlights.Count > 1 && necronBlights[1].Shape is DonutTemplate)
                yield return new("necron/blight/prepare", "连续环前保持近身", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(necronBlights[1].Position, 21)]), Arena.Position, Display: false, Activation: necronBlights[0].Activation);
        }
        if (necronPrimary is { Actor.Dead: false } source)
            foreach (var bait in necronShockwaves)
            {
                var own = bait.Target == necronActors.GetValueOrDefault(PlayerId);
                var caster = source.Actor; var target = bait.Target.Actor;
                var angle = MathF.Atan2(target.Position.X - caster.Position.X, target.Position.Z - caster.Position.Z);
                var shape = new FanTemplate(100, 100 * MathF.PI / 180);
                var id = $"necron/shockwave/{bait.Key}";
                yield return new(id + "/display", "蓝色冲击波诱导", shape, caster.Position, angle, Navigate: false, OutlineOnly: own);
                if (!own)
                    yield return new(id + "/avoid", "避开蓝色冲击波", shape, caster.Position, angle, Display: false, Activation: bait.At);
                else
                    foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    {
                        var direction = MathF.Atan2(buddy.Position.X - caster.Position.X, buddy.Position.Z - caster.Position.Z);
                        yield return new(id + $"/buddy/{buddy.Id}", "冲击波远离队友", shape, caster.Position, direction, Display: false, Activation: bait.At);
                    }
            }
        foreach (var aoe in NecronTowerAoes()) yield return aoe;
        foreach (var aoe in NecronPrisonAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> NecronDamage()
    {
        if (!IsNecron || Active == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var activation in new[] { necronDarkness, necronNeutron }.Where(at => at != default))
            foreach (var actor in party)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, activation, "Raidwide");
        if (necronShockwaves.Count > 0)
            foreach (var target in necronShockwaves.Select(b => b.Target.Actor).DistinctBy(a => a.Id).Where(a => a.PartySlot >= 0))
                yield return new(target.Id, target.PartySlot, target.PartyRole, target.Name, necronShockwaves[0].At, "Raidwide");
        if (necronTowers.Count > 0)
            foreach (var actor in party.Where(a => necronTowers.Any(t => new CircleGeometry(t.Position, 3).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, necronTowers[0].At, "Raidwide");
    }

    private IEnumerable<string> NecronHints()
    {
        if (!IsNecron || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (necronStored.Count > 0) yield return "储存顺序：" + string.Join(" → ", necronStored.Select(s => s == "Circle" ? "圆" : "环"));
        if (necronDarkness != default || necronNeutron != default) yield return "全屏伤害";
        foreach (var bait in necronShockwaves)
        {
            var source = necronPrimary?.Actor;
            if (source == null || source.Dead) continue;
            var target = bait.Target.Actor;
            var own = bait.Target == necronActors.GetValueOrDefault(PlayerId);
            var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var shape = new FanTemplate(100, 100 * MathF.PI / 180).Resolve(source.Position, angle, null);
            if (own ? _actors.Values.Any(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId && shape.Contains(a.Position))
                : shape.Contains(player.Position)) yield return "蓝色冲击波远离队友";
        }
        var towers = necronTowers;
        if (towers.Count > 0)
        {
            int Count(Vector3 position) => _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && new CircleGeometry(position, 3).Contains(a.Position));
            var own = towers.FindIndex(t => new CircleGeometry(t.Position, 3).Contains(player.Position));
            if (own >= 0)
            {
                if (Count(towers[own].Position) != 4) yield return "每塔四人";
            }
            else if (towers.Any(t => Count(t.Position) < 4)) yield return "每塔四人";
        }
    }
}
