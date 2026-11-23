using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> LapisAoes()
    {
        if (territory != LapisCatalog.TERRITORY || Arena == null) yield break;
        foreach (var (lane, index) in lapisStampedes.Select((lane, index) => (lane, index)))
            yield return lane.Update($"lapis/stampede/{index}", _actors.Values);
        foreach (var (source, shadow) in lapisRoars)
        {
            var activation = _casts.GetValueOrDefault(source)?.FinishAt;
            yield return new($"lapis/roar/{source}/safe", "冰块安全影区", shadow.Safe, Arena.Position, Navigate: false, Safe: true);
            yield return new($"lapis/roar/{source}/avoid", "躲在冰块后", shadow.Danger, Arena.Position, Display: false, Activation: activation);
        }
        for (var i = 0; i < Math.Min(2, lapisCrosses.Count); ++i)
            yield return lapisCrosses[i] with { Navigate = i == 0,
                Color = i == 0 && lapisCrosses.Count > 1 ? new Vector4(1, 1, 0, 1) : null };
        foreach (var sequence in lapisCycles)
            yield return new($"lapis/cycle/{sequence.FirstAction}/{sequence.Current.Source}", sequence.Current.Rule.Name,
                sequence.Current.Rule.Shape!, sequence.Current.Location, Activation: sequence.Current.FinishAt);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var available = new List<IGeometryShape>();
        var firstTowerAt = lapisTowers.Count > 0 ? lapisTowers[0].Activation : default;
        for (var i = 0; i < lapisTowers.Count; ++i)
        {
            var tower = lapisTowers[i];
            var inside = party.Where(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(tower.Position.X, tower.Position.Z)) <= 25).ToArray();
            var mine = inside.Any(a => a.Id == PlayerId);
            yield return new($"lapis/tower/{i}/display", "单人塔", new CircleTemplate(5), tower.Position,
                Navigate: false, Safe: inside.Length == 1, OutlineOnly: inside.Length == 1);
            if (inside.Length == 0 || mine && inside.Length == 1) available.Add(new CircleGeometry(tower.Position, 5));
            else
                yield return new($"lapis/tower/{i}/avoid", "避开已满塔", new CircleTemplate(5), tower.Position,
                    Display: false, Activation: tower.Activation);
        }
        if (available.Count > 0)
            yield return new("lapis/towers/join", "进入单人塔", new SohrKhaiWallHintTemplate(Arena, available.ToArray()),
                Arena.Position, Display: false, Activation: firstTowerAt);
        foreach (var eye in LapisGazes())
            if (_actors.TryGetValue(PlayerId, out var viewing))
                yield return new($"lapis/gaze/{eye.Source}/arc", "背对视线", new FanTemplate(1.5f, MathF.PI / 2), viewing.Position,
                    MathF.Atan2(eye.Location.X - viewing.Position.X, eye.Location.Z - viewing.Position.Z), Navigate: false, OutlineOnly: true);
        if (lapisArenaChange != null) yield return lapisArenaChange;
        for (var i = 0; i < lapisRams.Count; ++i)
            yield return lapisRams[i] with { Color = i == 0 && lapisRams.Count > 1 ? new Vector4(1, 1, 0, 1) : null };
        for (var i = 0; i < lapisBombs.Count; ++i)
            yield return lapisBombs[i] with { Color = i < 2 && lapisBombs.Count > 2 ? new Vector4(1, 1, 0, 1) : null };
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 31122))
        {
            if (lapisAntediluvian >= 4 && cast.Source == _casts.Values.First(c => c.Rule.Action == 31122).Source)
                yield return new("lapis/knockback/inside", "击退前进入源10码", new SohrKhaiWallHintTemplate(Arena,
                    [new CircleGeometry(cast.Location, 10)]), Arena.Position, Display: false, Activation: cast.FinishAt);
            if (_actors.TryGetValue(PlayerId, out var player))
            {
                var delta = new Vector2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
                if (delta.LengthSquared() < .000001f) continue;
                delta = Vector2.Normalize(delta) * 10;
                var landing = player.Position + new Vector3(delta.X, 0, delta.Y);
                foreach (var preview in DungeonMovementPreview($"lapis/knockback/{cast.Source}", "击退10码预计落点", player.Position, landing))
                    yield return preview;
            }
        }
        foreach (var (id, activation) in lapisSpreads)
            foreach (var aoe in ShisuiBait($"lapis/spread/{id}", id, 6, activation))
                yield return aoe;
        var stackTargets = lapisStacks.Keys.ToHashSet();
        var stackJoin = new List<IGeometryShape>();
        foreach (var (id, activation) in lapisStacks)
        {
            if (!_actors.TryGetValue(id, out var target)) continue;
            yield return new($"lapis/stack/{id}/display", "至少四人分摊", new CircleTemplate(6), target.Position,
                Navigate: false, OutlineOnly: true, Safe: true);
            if (stackTargets.Contains(PlayerId))
            {
                if (id != PlayerId)
                    yield return new($"lapis/stack/{id}/avoid", "避免重叠分摊", new CircleTemplate(12), target.Position, Display: false, Activation: activation);
                else
                {
                    var others = party.Where(a => !stackTargets.Contains(a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                    if (others.Length > 0)
                        yield return new($"lapis/stack/{id}/gather", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, others),
                            Arena.Position, Display: false, Activation: activation);
                }
            }
            else stackJoin.Add(new CircleGeometry(target.Position, 6));
        }
        if (stackJoin.Count > 0)
            yield return new("lapis/stacks/join", "进入四人分摊", new SohrKhaiWallHintTemplate(Arena, stackJoin.ToArray()),
                Arena.Position, Display: false, Activation: lapisStacks.Values.First());
        foreach (var (sourceId, tether) in lapisTethers)
            if (_actors.TryGetValue(sourceId, out var source) && _actors.TryGetValue(tether.Target, out var target))
            {
                foreach (var aoe in SohrBait($"lapis/tether/{sourceId}", "连线诱导", source, target.Id, new FanTemplate(50, MathF.PI / 6)))
                    yield return aoe with { OutlineOnly = aoe.Display && target.Id == PlayerId };
                var delta = target.Position - source.Position;
                yield return new($"lapis/tether/{sourceId}/line", "连线", RayRectangleTemplate.Symmetric(.15f, new Vector2(delta.X, delta.Z).Length()),
                    source.Position, MathF.Atan2(delta.X, delta.Z), Navigate: false);
            }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 31118))
            if (_actors.TryGetValue(cast.Source, out var source))
                foreach (var aoe in SohrBait($"lapis/torrent/{cast.Source}", "矩形诱导死刑", source,
                    cast.TargetId == source.Id ? source.TargetId : cast.TargetId, RayRectangleTemplate.Symmetric(8, 60), cast.FinishAt, 4))
                    yield return aoe with { OutlineOnly = aoe.Display && (cast.TargetId == PlayerId || cast.TargetId == source.Id && source.TargetId == PlayerId) };
    }

    private IEnumerable<DungeonDamagePrediction> LapisDamage()
    {
        if (territory != LapisCatalog.TERRITORY) yield break;
        foreach (var activation in lapisTowers.Select(t => t.Activation).Distinct())
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead
                && lapisTowers.Any(t => t.Activation == activation && Vector2.DistanceSquared(new(a.Position.X, a.Position.Z),
                    new(t.Position.X, t.Position.Z)) <= 25)))
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
        foreach (var activation in lapisStacks.Values.Concat(lapisTsunami is { } tsunami ? [tsunami] : []))
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, activation, "Raidwide");
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 32785))
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, cast.FinishAt, "Raidwide");
    }

    private IEnumerable<string> LapisHints()
    {
        if (territory != LapisCatalog.TERRITORY) yield break;
        if (_casts.Values.Any(c => c.Rule.Action == 32785)) yield return "眩晕阻止WindsOfWinter";
        if (lapisRoars.Count > 0) yield return "躲在冰块后";
        if (lapisTowers.Count > 0) yield return "每塔一人";
        if (lapisDoom.Count > 0) yield return "死宣：及时驱散";
        if (lapisTsunami != null) yield return "即将受到海啸全屏伤害";
        if (Arena != null && _actors.TryGetValue(PlayerId, out var player))
            foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 31122))
            {
                var delta = new Vector2(player.Position.X - cast.Location.X, player.Position.Z - cast.Location.Z);
                if (delta.LengthSquared() < .000001f) continue;
                delta = Vector2.Normalize(delta) * 10;
                var landing = player.Position + new Vector3(delta.X, 0, delta.Y);
                if (!Arena.Contains(landing, 0) || _casts.Values.Any(c => c.Rule.Action == 31120
                    && Vector2.DistanceSquared(new(landing.X, landing.Z), new(c.Location.X, c.Location.Z)) <= 225))
                    yield return "击退落点危险";
            }
    }
}
