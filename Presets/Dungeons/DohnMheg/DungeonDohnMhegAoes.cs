using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> DohnAoes()
    {
        if (territory != DohnMhegCatalog.TERRITORY || Arena == null) yield break;
        if (dohnSwinge != null) yield return dohnSwinge;
        if (dohnBile != null) yield return dohnBile;
        if (dohnTentacles != null) yield return dohnTentacles;
        if (dohnGeysers.Count > 0)
        {
            var deadline = dohnGeysers[0].Aoe.Activation!.Value.AddSeconds(1);
            foreach (var (_, aoe) in dohnGeysers)
            {
                var imminent = aoe.Activation < deadline;
                yield return aoe with { Navigate = imminent, OutlineOnly = !imminent };
            }
        }
        foreach (var (id, activation) in dohnStacks)
            if (_actors.TryGetValue(id, out var target))
            {
                yield return new($"dohn/stack/{id}/display", "四人分摊", new CircleTemplate(4), target.Position,
                    Navigate: false, Safe: true, Activation: activation);
                if (id != PlayerId) yield return new($"dohn/stack/{id}/enter", "进入四人分摊",
                    new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 4)]),
                    Arena.Position, Display: false, Activation: activation);
            }
        foreach (var (sourceId, tether) in dohnTethers)
            if (_actors.TryGetValue(sourceId, out var source) && _actors.TryGetValue(tether.Target, out var target))
            {
                var delta = target.Position - source.Position;
                var length = new Vector2(delta.X, delta.Z).Length();
                yield return new($"dohn/tether/{sourceId}", "截取以太连线", RayRectangleTemplate.Symmetric(.12f, length),
                    source.Position, MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: target.PartySlot >= 0);
            }
        if (Active?.BossOid == 0x98E && dohnTethers.Count > 0 && _actors.TryGetValue(PlayerId, out var player)
            && player.PartySlot >= 0 && player.PartySlot < dohnSaplings.Count
            && _actors.TryGetValue(dohnSaplings[player.PartySlot], out var sapling) && _actors.TryGetValue(PrimaryId, out var boss))
        {
            var direction = sapling.Position - boss.Position; direction.Y = 0;
            if (direction.LengthSquared() > .0001f)
            {
                var start = boss.Position + Vector3.Normalize(direction) * (boss.HitboxRadius + .1f);
                var length = Vector3.Distance(start, sapling.Position);
                var angle = MathF.Atan2(sapling.Position.X - start.X, sapling.Position.Z - start.Z);
                var shape = RayRectangleTemplate.Symmetric(1, length);
                yield return new("dohn/feeding/display", "按队伍槽位截取连线", shape, start, angle,
                    Navigate: false, Safe: true, Activation: dohnFeedingAt);
                yield return new("dohn/feeding/enter", "进入指定接线走廊",
                    new SohrKhaiWallHintTemplate(Arena, [shape.Resolve(start, angle, null)]), Arena.Position,
                    Display: false, Activation: dohnFeedingAt);
            }
        }
        foreach (var root in _actors.Values.Where(a => Active?.BossOid == 0x98E && a.Oid == 0xF08 && !a.Dead))
            yield return new($"dohn/root/{root.Id}", "优先击破缠绕树根", new CircleTemplate(root.HitboxRadius),
                root.Position, Navigate: false, Safe: true, OutlineOnly: true);
        if (DohnChasm && DohnLyre is { } lyre)
        {
            yield return new("dohn/lyre/display", "沿独木桥接近谎言之琴", new CircleTemplate(lyre.HitboxRadius + 1),
                lyre.Position, Navigate: false, Safe: true, OutlineOnly: true);
            yield return new("dohn/lyre/goal", "摧毁谎言之琴", new CircleTemplate(lyre.HitboxRadius + 1),
                lyre.Position, Display: false, Safe: true);
        }
        if (_actors.TryGetValue(PlayerId, out var perspective))
        {
            foreach (var pull in _casts.Values.Where(c => c.Rule.Action == 13519))
                if (_actors.TryGetValue(pull.Source, out var helper))
                {
                    var delta = helper.Position - perspective.Position; delta.Y = 0;
                    var length = delta.Length();
                    var distance = Math.Clamp(length - helper.HitboxRadius - perspective.HitboxRadius, 0, 50);
                    var landing = length > .001f ? perspective.Position + delta / length * distance : perspective.Position;
                    foreach (var preview in DungeonMovementPreview("dohn/pull", "拉近独木桥起点", perspective.Position, landing)) yield return preview;
                }
            var previous = perspective.Position;
            var index = 0;
            foreach (var point in DohnBridgeRoute())
            {
                var delta = point - previous;
                yield return new($"dohn/bridge/{index++}", "独木桥行走路线",
                    RayRectangleTemplate.Symmetric(.1f, new Vector2(delta.X, delta.Z).Length()), previous,
                    MathF.Atan2(delta.X, delta.Z), Navigate: false, Safe: true);
                previous = point;
            }
        }
    }
}
