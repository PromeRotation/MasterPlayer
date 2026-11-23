using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ShisuiBait(string key, ulong targetId, float radius, DateTime? activation)
    {
        if (!_actors.TryGetValue(targetId, out var target)) yield break;
        yield return new(key + "/display", "点名远离队友", new CircleTemplate(radius), target.Position, Navigate: false, OutlineOnly: targetId == PlayerId);
        if (targetId != PlayerId)
            yield return new(key + "/avoid", "躲避队友点名", new CircleTemplate(radius), target.Position, Display: false, Activation: activation);
        else
            foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                yield return new(key + $"/party/{other.Id}", "点名避免覆盖队友", new CircleTemplate(radius), other.Position, Display: false, Activation: activation);
    }

    private IEnumerable<DungeonAmbientAoe> ShisuiAoes()
    {
        if (territory != ShisuiCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x1B0C)
        {
            foreach (var zone in _actors.Values.Where(a => a.Oid is 0x1EA0D7 or 0x1E950D && a.EventState != 7))
                yield return new($"shisui/pool/{zone.Id}", "持续黏液", new CircleTemplate(zone.Oid == 0x1EA0D7 ? 3 : 5), QuantizedDungeonPosition(zone.Position));
            foreach (var aoe in shisuiPools) yield return aoe;
            foreach (var id in shisuiBindTargets)
                foreach (var aoe in ShisuiBait($"shisui/bind/{id}", id, 3, null)) yield return aoe;
            foreach (var (id, activation) in shisuiDigestTargets)
            {
                foreach (var aoe in ShisuiBait($"shisui/digest/{id}", id, 5, activation)) yield return aoe;
                if (id == PlayerId) yield return new("shisui/digest/edge", "消化液场边诱导", new CircleTemplate(17.5f), Arena.Position, Display: false);
            }
        }
        if (Active?.BossOid == 0x1B0E)
        {
            var old = _actors.TryGetValue(PlayerId, out var player) && HasStatus(player, 1259);
            var closed = shisuiChests.Values.Where(c => !c.Open).Select(c => (IGeometryShape)new CircleGeometry(c.Position, 2.5f)).ToArray();
            foreach (var (id, chest) in shisuiChests)
                yield return new($"shisui/chest/{id}", "变老宝箱", new CircleTemplate(2.5f), chest.Position,
                    Navigate: chest.Open || !shisuiSeduce || old, Safe: !chest.Open && shisuiSeduce && !old, OutlineOnly: true);
            if (shisuiSeduce && !old && closed.Length > 0)
                yield return new("shisui/chest/enter", "进入关闭宝箱取得变老", new SohrKhaiWallHintTemplate(Arena, closed), Arena.Position, Display: false);
            if (shisuiKick != null) yield return shisuiKick;
            foreach (var (id, activation) in shisuiChaseTargets)
                if (id == PlayerId) yield return new("shisui/chase/edge", "追踪爆炸场边诱导", new CircleTemplate(18), Arena.Position, Display: false, Activation: activation);
            foreach (var chase in shisuiChasers)
            {
                var position = ShisuiChasePosition(chase);
                yield return new($"shisui/chase/{chase.Source}/display", $"追踪爆炸：剩余{chase.Remaining}次", new CircleTemplate(4), position, Navigate: false, Activation: chase.Activation);
                if (_actors.TryGetValue(chase.Target, out var target))
                {
                    var self = chase.Target == PlayerId;
                    var avoid = self ? position - new Vector3(MathF.Sin(target.Rotation), 0, MathF.Cos(target.Rotation)) * 4 : position;
                    yield return new($"shisui/chase/{chase.Source}/avoid", "连续追踪避让", new CircleTemplate(self ? 7 : 5), avoid, Display: false, Activation: chase.Activation);
                    var delta = target.Position - chase.Previous;
                    var length = new Vector2(delta.X, delta.Z).Length();
                    if (length > .001f)
                        yield return new($"shisui/chase/{chase.Source}/line", "追踪方向", new RectangleTemplate(.12f, length),
                            (target.Position + chase.Previous) * .5f, MathF.Atan2(delta.X, delta.Z), Navigate: false);
                }
            }
        }
        if (Active?.BossOid != 0x1B0F) yield break;
        if (shisuiSplashes >= 10 && _actors.TryGetValue(shisuiSplashHelper, out var helper))
            yield return new("shisui/black-tide", "黑潮：水花后爆发", new CircleTemplate(13), QuantizedDungeonPosition(helper.Position), Activation: shisuiBlackTideAt);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 8069 or 8070))
        {
            if (!_actors.TryGetValue(cast.Source, out var source) || !_actors.TryGetValue(cast.TargetId, out var target)) continue;
            var delta = target.Position - source.Position;
            var length = new Vector2(delta.X, delta.Z).Length();
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(5, length);
            yield return new($"shisui/charge/{cast.Source}/display", "点名冲锋死刑", shape, source.Position, rotation, Navigate: false, OutlineOnly: target.Id == PlayerId);
            if (target.Id != PlayerId)
                yield return new($"shisui/charge/{cast.Source}/avoid", "避开点名冲锋", shape, source.Position, rotation, Display: false, Activation: cast.FinishAt);
            else
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                {
                    var direction = other.Position - source.Position;
                    var distance = new Vector2(direction.X, direction.Z).Length();
                    var halfAngle = MathF.Asin(Math.Clamp(2.5f / Math.Max(.001f, distance), 0, 1));
                    yield return new($"shisui/charge/{cast.Source}/party/{other.Id}", "冲锋避免覆盖队友", new FanTemplate(100, 2 * halfAngle),
                        source.Position, MathF.Atan2(direction.X, direction.Z), Display: false, Activation: cast.FinishAt);
                }
        }
    }
}
