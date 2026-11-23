using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TroiaAoes()
    {
        if (territory != TroiaCatalog.TERRITORY || Arena == null) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 29621 or 29622 or 30260))
        {
            var threshold = cast.Rule.Action switch { 29621 => 2.5, 29622 => 5, _ => 8 };
            yield return new($"troia/risky/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                Navigate: cast.FinishAt <= _now.AddSeconds(threshold), Activation: cast.FinishAt);
        }
        if (troiaReturning)
            yield return new("troia/return", "返回20码场内", new DonutTemplate(20, 25), TroiaDynamicArena.Center with { Y = Arena.Position.Y });
        if (troiaVacuum is { } vacuum)
        {
            if (troiaShelterHint)
            {
                yield return new("troia/vacuum/avoid", "利用安全墙抵挡击退", new SohrKhaiWallHintTemplate(Arena, troiaShelters),
                    Arena.Position, Display: false, Activation: vacuum.FinishAt);
                for (var i = 0; i < troiaShelters.Length; ++i)
                    yield return new($"troia/vacuum/safe/{i}", "安全墙站位", new DungeonShapeTemplate(troiaShelters[i]),
                        Arena.Position, Navigate: false, Safe: true);
            }
            if (_actors.TryGetValue(PlayerId, out var player))
                foreach (var preview in DungeonMovementPreview("troia/vacuum", "安全墙击退预计落点", player.Position,
                    TroiaKnockback.Landing(player.Position, vacuum.Location, player.HitboxRadius, Arena, troiaLockedLines)))
                    yield return preview;
        }
        if (_casts.Values.Any(c => c.Rule.Action == 30233 && c.TargetId == PlayerId))
        {
            var activation = _casts.Values.First(c => c.Rule.Action == 30233).FinishAt;
            foreach (var wall in _actors.Values.Where(a => a.Oid == 0x39C8))
            {
                yield return new($"troia/wall/{wall.Id}/avoid", "分散避开安全墙", new CircleTemplate(7), wall.Position,
                    Display: false, Activation: activation);
                yield return new($"troia/wall/{wall.Id}/display", "安全墙碰撞体", new CircleTemplate(wall.HitboxRadius), wall.Position,
                    Navigate: false, OutlineOnly: true);
            }
        }
        foreach (var stackGroup in _casts.Values.Where(c => c.Rule.Action is 31208 or 30243 or 30254).GroupBy(c => c.Rule.Action))
        {
            var stacks = stackGroup.Where(c => _actors.TryGetValue(c.TargetId, out var a) && !a.Dead).ToArray();
            var targets = stacks.Select(c => c.TargetId).ToHashSet();
            var own = stacks.FirstOrDefault(c => c.TargetId == PlayerId);
            if (own != null)
            {
                var candidates = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && !targets.Contains(a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"troia/stack/{stackGroup.Key}/gather", "四人分摊点名靠拢队友",
                        new SohrKhaiWallHintTemplate(Arena, candidates), Arena.Position, Display: false, Activation: own.FinishAt);
            }
            var join = new List<IGeometryShape>();
            foreach (var stack in stacks)
            {
                var target = _actors[stack.TargetId];
                yield return new($"troia/stack/{stack.Source}/display", "四人分摊", new CircleTemplate(6), target.Position,
                    Navigate: false, OutlineOnly: true, Safe: true);
                if (target.Id == PlayerId) continue;
                var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && Vector3.DistanceSquared(a.Position, target.Position) <= 36);
                var inside = _actors.TryGetValue(PlayerId, out var p) && Vector3.DistanceSquared(p.Position, target.Position) <= 36;
                if (own == null && (inside ? count <= 4 : count < 4))
                    join.Add(new CircleGeometry(target.Position, 6));
                else
                    yield return new($"troia/stack/{stack.Source}/separate", "避免重叠分摊", new CircleTemplate(own == null ? 6 : 12),
                        target.Position, Display: false, Activation: stack.FinishAt);
            }
            if (join.Count > 0)
                yield return new($"troia/stack/{stackGroup.Key}/join", "进入四人分摊", new SohrKhaiWallHintTemplate(Arena, join.ToArray()),
                    Arena.Position, Display: false, Activation: stacks[0].FinishAt);
        }
        if (_actors.TryGetValue(PlayerId, out var viewing))
            foreach (var eye in TroiaGazes())
                yield return new($"troia/gaze/{eye.Source}/facing", "视线禁向45度", new FanTemplate(1.5f, MathF.PI / 2),
                    viewing.Position, MathF.Atan2(eye.Location.X - viewing.Position.X, eye.Location.Z - viewing.Position.Z),
                    Navigate: false, OutlineOnly: true);
    }

    private IEnumerable<DungeonDamagePrediction> TroiaDamage()
    {
        if (territory != TroiaCatalog.TERRITORY || troiaPitch is not { } activation) yield break;
        foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
            yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
    }

    private IEnumerable<string> TroiaHints()
    {
        if (territory != TroiaCatalog.TERRITORY) yield break;
        if (troiaDoom.Count > 0) yield return "死宣：及时驱散";
        if (troiaPitch != null) yield return "即将受到全屏三连伤害";
        if (troiaShelterHint) yield return "进入安全扇，利用安全墙抵挡击退";
        if (_casts.Values.Any(c => c.Rule.Action == 30233 && c.TargetId == PlayerId)) yield return "分散，避开安全墙碰撞体";
        if (_casts.Values.Any(c => c.Rule.Action is 31208 or 30243 or 30254)) yield return "四人分摊";
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 29630))
            yield return $"狂暴剩余 {Math.Max(0, (cast.FinishAt - _now).TotalSeconds):F1} 秒";
    }
}
