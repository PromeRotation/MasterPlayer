using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> DomaLineBait(string key, DungeonActor source, DungeonActor target, float halfWidth, float length)
    {
        var angle = MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
        var shape = RayRectangleTemplate.Symmetric(2 * halfWidth, length);
        yield return new(key + "/display", "矩形诱导", shape, QuantizedDungeonPosition(source.Position), angle, Navigate: false, OutlineOnly: true);
        if (PlayerId != target.Id)
            yield return new(key + "/avoid", "避开矩形诱导", shape, QuantizedDungeonPosition(source.Position), angle, Display: false);
        else
            foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
            {
                var offset = other.Position - source.Position;
                var half = MathF.Asin(Math.Clamp(halfWidth / Math.Max(.001f, new Vector2(offset.X, offset.Z).Length()), 0, 1));
                yield return new(key + $"/party/{other.Id}", "矩形不要覆盖队友", new FanTemplate(100, 2 * half),
                    QuantizedDungeonPosition(source.Position), MathF.Atan2(offset.X, offset.Z), Display: false);
            }
    }

    private IEnumerable<DungeonAmbientAoe> DomaAoes()
    {
        if (territory != DomaCatalog.TERRITORY || Arena == null) yield break;
        if (Active?.BossOid == 0x1BCC)
        {
            if (_actors.TryGetValue(PrimaryId, out var boss) && boss.Targetable && boss.Cast == null && !_casts.ContainsKey(PrimaryId)
                && _actors.TryGetValue(boss.TargetId, out var target))
                foreach (var aoe in DomaLineBait("doma/cleave", boss, target, 3, 43.5f)) yield return aoe;
            foreach (var mine in _actors.Values.Where(a => a.Oid == 0x1BCE && !a.Dead))
            {
                yield return new($"doma/mine/{mine.Id}/display", "移动地雷", new DungeonCapsuleTemplate(.9f, 10), mine.Position, mine.Rotation, Navigate: false);
                yield return new($"doma/mine/{mine.Id}/obstacle", "地雷实体", new CircleTemplate(.9f), mine.Position, Display: false, Obstacle: true);
                foreach (var (length, seconds) in new (float, double)[] { (5, 1.1), (10, 3), (20, 10), (30, double.PositiveInfinity) })
                    yield return new($"doma/mine/{mine.Id}/future/{length}", "移动地雷前方", new DungeonCapsuleTemplate(.9f, length), mine.Position, mine.Rotation,
                        Display: false, Activation: double.IsPositiveInfinity(seconds) ? DateTime.MaxValue : _now.AddSeconds(seconds));
            }
        }
        var suppressChainNavigation = Active?.BossOid == 0x1BD4 && _casts.Values.Count(c => c.Rule.Action == 8367) > 1;
        foreach (var aoe in domaChains.Values) yield return aoe with { Navigate = !suppressChainNavigation };
        foreach (var aoe in domaCharges.Values) yield return aoe;
        if (domaSaw != null) yield return domaSaw;
        foreach (var (id, activation) in domaStacks)
        {
            if (!_actors.TryGetValue(id, out var target) || target.Dead) continue;
            yield return new($"doma/stack/{id}/display", "四人分摊", new CircleTemplate(6), target.Position, Navigate: false, Safe: true, Activation: activation);
            if (id != PlayerId)
                yield return new($"doma/stack/{id}/enter", "进入四人分摊", new SohrKhaiWallHintTemplate(Arena, [new CircleGeometry(target.Position, 6)]),
                    Arena.Position, Display: false, Activation: activation);
        }
        var towers = _casts.Values.Where(c => c.Rule.Action == 8357).ToArray();
        var available = new List<IGeometryShape>();
        foreach (var tower in towers)
        {
            var inside = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(tower.Location.X, tower.Location.Z)) <= 36).ToArray();
            var safe = inside.Length == 0 || inside.Length == 1 && inside[0].Id == PlayerId;
            yield return new($"doma/tower/{tower.Source}", "一人导弹塔", new CircleTemplate(6), tower.Location,
                Navigate: false, Safe: safe, OutlineOnly: safe, Activation: tower.FinishAt);
            if (safe) available.Add(new CircleGeometry(tower.Location, 6));
            else yield return new($"doma/tower/{tower.Source}/avoid", "避开已满导弹塔", new CircleTemplate(6), tower.Location, Display: false, Activation: tower.FinishAt);
        }
        if (available.Count > 0)
            yield return new("doma/tower/enter", "进入未满导弹塔", new SohrKhaiWallHintTemplate(Arena, available.ToArray()), Arena.Position,
                Display: false, Activation: towers[0].FinishAt);
        foreach (var (id, activation) in domaSpreads)
        {
            if (!_actors.TryGetValue(id, out var target) || target.Dead) continue;
            yield return new($"doma/spread/{id}", "延时炸弹分散", new CircleTemplate(6), target.Position,
                Navigate: id != PlayerId, OutlineOnly: id == PlayerId, Activation: activation);
            if (id == PlayerId)
                foreach (var other in _actors.Values.Where(a => a.PartySlot >= 0 && a.Id != PlayerId && !a.Dead))
                    yield return new($"doma/spread/{id}/party/{other.Id}", "分散远离队友", new CircleTemplate(6), other.Position, Display: false, Activation: activation);
        }
        foreach (var (id, activation) in domaPrey)
        {
            if (!_actors.TryGetValue(id, out var target) || target.Dead) continue;
            yield return new($"doma/prey/{id}", "热压弹诱导", new CircleTemplate(30), target.Position, Navigate: false, OutlineOnly: id == PlayerId, Activation: activation);
            if (id == PlayerId)
                yield return new("doma/prey/edge", "热压弹场角诱导", new CircleTemplate(23), Arena.Position, Display: false);
        }
        if (domaGunsawTarget != 0 && _actors.TryGetValue(PrimaryId, out var gunBoss)
            && _actors.TryGetValue(domaGunsawTarget, out var gunTarget) && !gunTarget.Dead)
            foreach (var aoe in DomaLineBait("doma/gunsaw", gunBoss, gunTarget, 1, 60.9f)) yield return aoe;
    }
}
