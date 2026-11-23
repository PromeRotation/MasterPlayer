using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ValigAoes()
    {
        if (!IsValigarmanda || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => !a.Dead && a.Targetable
            && (a.Id == PrimaryId || a.Oid is 0x4117 or 0x438B or 0x438C or 0x438A)))
            yield return new($"valig/enemy/{actor.Id}", actor.Id == PrimaryId ? "艳翼蛇鸟" : "场地物体",
                new CircleTemplate(actor.HitboxRadius), actor.Position, Navigate: false, OutlineOnly: true,
                Color: actor.Id == PrimaryId ? null : new Vector4(1, 128f / 255, 0, 1));
        foreach (var aoe in valigLightning) yield return aoe;
        foreach (var aoe in valigChilling) yield return aoe;
        if (valigLightning.Count == 0 && valigCoil != null) yield return valigCoil;
        if (valigNorthern != null) yield return valigNorthern;
        foreach (var aoe in ValigPlatformAoes()) yield return aoe;
        foreach (var aoe in ValigMovementAoes()) yield return aoe;
        foreach (var aoe in ValigTowerAoes()) yield return aoe;
        foreach (var aoe in ValigLineAoes()) yield return aoe;
        foreach (var b in valigTalons.Where(b => !b.Source.Actor.Dead && !b.Target.Actor.Dead))
        {
            var own = b.Target == valigActors.GetValueOrDefault(PlayerId);
            var id = $"valig/talon/{b.Key}";
            yield return new(id + "/display", "冰爪死刑", new CircleTemplate(6), b.Target.Actor.Position,
                Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "远离冰爪死刑", new CircleTemplate(6), b.Target.Actor.Position, Display: false, Activation: b.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "死刑远离队友", new CircleTemplate(6), buddy.Position,
                        Display: false, Activation: b.At);
        }
    }

    private IEnumerable<DungeonAmbientAoe> ValigPlatformAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { PartySlot: >= 0 and < 64 } player || !valigGroundHint[player.PartySlot]) yield break;
        var cells = new List<IGeometryShape>();
        for (var x = 0; x < 2; ++x)
            for (var z = 0; z < 3; ++z)
                if (((x ^ z) & 1) != 0)
                    foreach (var sign in new[] { -1, 1 })
                    {
                        var position = Arena.Position + new Vector3(sign * (5 + 10 * x), 0, -10 + 10 * z);
                        cells.Add(new RectangleGeometry(position, 0, 10, 10));
                        yield return new($"valig/platform/{x}/{z}/{sign}", "离开悬浮地板", new RectangleTemplate(10, 10),
                            position, Activation: valigPlatformAt);
                    }
        if (!valigBolt) yield break;
        foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
            if (cells.Any(c => c.Contains(buddy.Position)))
                yield return new($"valig/bolt/{buddy.Id}", "悬浮队员雷圈", new CircleTemplate(3), buddy.Position);
    }

    private IEnumerable<DungeonDamagePrediction> ValigDamage()
    {
        if (!IsValigarmanda || Active == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var at in valigDisaster.Where(at => at != default))
            foreach (var actor in party) yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, at, "Raidwide");
        if (valigTowers.Count > 0)
            foreach (var actor in party.Where(a => a.PartyRole == "Tank" && valigTowers.Any(t => new CircleGeometry(t.Location, 6).Contains(a.Position))))
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, valigTowers[0].FinishAt, "Raidwide");
        foreach (var b in valigTalons.Where(b => !b.Source.Actor.Dead && !b.Target.Actor.Dead).DistinctBy(b => b.Target))
            if (b.Target.Actor is { PartySlot: >= 0 } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, valigTalons[0].At, "Tankbuster");
        foreach (var marker in new[] { 26708u, 34722u })
        {
            var lines = valigLines.Where(l => l.Marker == marker && !l.Source.Actor.Dead).ToArray();
            if (lines.Length > 0)
                foreach (var actor in party) yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, lines.Min(l => l.At), "Shared");
        }
    }

    private IEnumerable<string> ValigHints()
    {
        if (!IsValigarmanda || Active == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (ValigFreezeAt is var at && at != default && _now >= at.AddSeconds(-3)) yield return "冻结粉尘：持续移动";
        if (valigDisaster.Any(at => at != default)) yield return "三连灾变：全屏伤害";
        if (valigGroundHint.ElementAtOrDefault(player.PartySlot < 0 ? 64 : player.PartySlot)) yield return "留在地面";
        if (valigTalons.Any(b => !b.Target.Actor.Dead)) yield return "冰爪：范围死刑";
        if (WantsValigKnockbackImmunity) yield return "坦克踩塔：准备抗击退";
        foreach (var tower in valigTowers)
        {
            var circle = new CircleGeometry(tower.Location, 6);
            var inside = circle.Contains(player.Position);
            var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && a.PartyRole == "Tank" && circle.Contains(a.Position));
            if (player.PartyRole != "Tank" && inside) yield return "非坦克离开塔";
            else if (player.PartyRole == "Tank" && (inside ? count != 2 : count < 2)) yield return $"双坦踩塔：{count}/2";
        }
        var own = valigActors.GetValueOrDefault(PlayerId);
        foreach (var marker in new[] { 26708u, 34722u })
        {
            var lines = valigLines.Where(l => l.Marker == marker && !l.Source.Actor.Dead).ToArray();
            if (lines.Any(l => l.Target == own))
            {
                if (lines.Any(l => l.Target != own && ValigLineShape(l.Source.Actor, l.Target.Actor).Contains(player.Position)))
                    yield return "离开其他点名的直线分摊";
                continue;
            }
            if (lines.Length > 0 && !lines.Any(l => ValigLineShape(l.Source.Actor, l.Target.Actor).Contains(player.Position)))
                yield return "进入直线分摊";
        }
    }
}
