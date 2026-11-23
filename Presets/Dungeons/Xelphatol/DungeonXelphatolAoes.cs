using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> XelAoes()
    {
        if (territory != XelphatolCatalog.TERRITORY || Active == null || Arena == null) yield break;
        if (xelArenaWarning != null) yield return xelArenaWarning;
        if (xelHotBlast != null && _now < xelHotBlast.FinishAt.AddSeconds(1))
        {
            var circle = new CircleGeometry(xelHotBlast.Location, 4);
            yield return new("xel/hot/display", "进入首领脚下", new CircleTemplate(4), xelHotBlast.Location, Navigate: false, Safe: true);
            yield return new("xel/hot/avoid", "首领脚下之外危险", new SohrKhaiWallHintTemplate(Arena, [circle]),
                Arena.Position, Display: false, Activation: xelHotBlast.FinishAt);
        }
        if (Active.BossOid == 0x179F)
        {
            var whirlwinds = _actors.Values.Where(a => a.Oid == 0x17A0).Select(a => a.Position).ToArray();
            foreach (var wind in _actors.Values.Where(a => a.Oid == 0x17A0))
                yield return new($"xel/wind/{wind.Id}", "TerribleFlurry", new CircleTemplate(6), wind.Position);
            if (xelHaste && _actors.TryGetValue(PrimaryId, out var boss) && boss.Targetable)
                foreach (var aoe in SohrBait("xel/haste", "Swiftfeather顺劈", boss, boss.TargetId, new FanTemplate(10.98f, MathF.PI * 2 / 3)))
                    yield return aoe;
            if (xelKnockback != null)
            {
                var immune = _actors.TryGetValue(PlayerId, out var player)
                    && new uint[] { 160, 1209, 2663, 1722, 2345, 3054 }.Any(s => HasStatus(player, s)
                        && player.StatusExpirations?.TryGetValue(s, out var expiry) == true && expiry > xelKnockback.FinishAt);
                if (!immune)
                {
                    yield return new("xel/knockback/avoid", "击退落点危险", new XelphatolKnockbackTemplate(Arena,
                        xelKnockback.Location, whirlwinds, player?.HitboxRadius ?? .5f), Arena.Position, Display: false, Activation: xelKnockback.FinishAt);
                    if (player != null)
                        foreach (var aoe in DungeonMovementPreview("xel/knockback", "径向击退30码", player.Position,
                            XelphatolKnockback.Landing(Arena, xelKnockback.Location, player.Position, player.HitboxRadius))) yield return aoe;
                }
                for (var i = 0; i < xelShelters.Length; ++i)
                    yield return new($"xel/shelter/{i}", "利用墙段挡击退", new DungeonShapeTemplate(xelShelters[i]),
                        xelShelters[i].Position, Navigate: false, Safe: true);
                yield return new("xel/shelter/avoid", "墙段安全扇区之外危险", new SohrKhaiWallHintTemplate(Arena, xelShelters),
                    Arena.Position, Display: false, Activation: xelKnockback.FinishAt);
            }
        }
        foreach (var (id, activation) in xelSpreads)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            yield return new($"xel/spread/{id}", "DarkWings", new CircleTemplate(6), target.Position, Navigate: false, OutlineOnly: true);
            if (id != PlayerId)
                yield return new($"xel/spread/{id}/avoid", "DarkWings", new CircleTemplate(6), target.Position, Display: false, Activation: activation);
            else
                foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"xel/spread/party/{member.Id}", "分散避开队友", new CircleTemplate(6), member.Position, Display: false, Activation: activation);
        }
        foreach (var (id, activation) in xelStacks)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            yield return new($"xel/stack/{id}", "Ingurgitate四人分摊", new CircleTemplate(5), target.Position, Navigate: false, OutlineOnly: true, Safe: true);
            if (id != PlayerId)
                yield return new($"xel/stack/{id}/goal", "Ingurgitate四人分摊", new CircleTemplate(5), target.Position, Display: false, Safe: true, Activation: activation);
        }
    }
}
