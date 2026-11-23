using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> LunarAoes()
    {
        if (territory != LunarCatalog.TERRITORY || Arena == null) yield break;
        if (lunarShrink != null) yield return lunarShrink;
        if (lunarCrosses.Count > 0 && !_casts.Values.Any(c => c.Rule.Action == 34787))
        {
            var risky = lunarCrosses[0].Activation!.Value.AddSeconds(-5) < _now;
            foreach (var cross in lunarCrosses) yield return cross with { Navigate = risky };
        }
        for (var i = 0; i < lunarMarches.Count; ++i)
        {
            if (i == 0 && lunarMarches.Count > 1) lunarMarches[i] = lunarMarches[i] with { Color = new Vector4(1, 1, 0, 1) };
            yield return lunarMarches[i];
        }
        for (var i = 0; i < Math.Min(16, lunarBatteries.Count); ++i)
        {
            var a = lunarBatteries[i];
            if (lunarBatteries.Count > 16) a = a with { Color = new Vector4(1, 1, 0, 1) };
            lunarBatteries[i] = a with { Navigate = true };
        }
        foreach (var a in lunarBatteries) yield return a;
        var towers = lunarTowers.Select(a => a.Shape.Resolve(a.Position, a.Rotation, null)).ToArray();
        foreach (var tower in lunarTowers)
            yield return tower with { Navigate = lunarTowerDanger && (lunarKnockbacks.Count == 0 || towers.Length != 2) };
        if (lunarKnockbacks.Count > 0 && towers.Length == 2)
        {
            var enter = MathF.Round(MathF.Abs(lunarTowers[0].Position.Z - lunarTowers[1].Position.Z)) is 10 or 30;
            if (enter)
                yield return new("lunar/tower/join", "击退前进入倒塔条带", new SohrKhaiWallHintTemplate(Arena, towers),
                    Arena.Position, Display: false, Activation: lunarKnockbacks[0].FinishAt);
            else
                for (var i = 0; i < towers.Length; ++i)
                    yield return lunarTowers[i] with { Id = $"lunar/tower/avoid/{i}", Display = false,
                        Navigate = true, Activation = lunarKnockbacks[0].FinishAt };
        }
        foreach (var cast in lunarKnockbacks)
        {
            var length = lunarAntlionSmall ? 40 : 50;
            var direction = new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation));
            yield return new($"lunar/knockback/{cast.Source}/avoid", "避免击退出场", RayRectangleTemplate.Symmetric(10, 20),
                cast.Location + direction * (length - 20), cast.Rotation, Display: false, Activation: cast.FinishAt);
        }
        if (_actors.TryGetValue(PlayerId, out var player))
        {
            var start = player.Position;
            foreach (var cast in lunarKnockbacks)
            {
                if (KnockbackImmune(player, cast.FinishAt)
                    || !RayRectangleTemplate.Symmetric(10, 40).Resolve(cast.Location, cast.Rotation, null).Contains(start, 0)) continue;
                var landing = start + new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 20;
                foreach (var a in DungeonMovementPreview($"lunar/knockback/{cast.Source}", "定向击退20码落点", start, landing)) yield return a;
                start = landing;
            }
        }
        var quicksand = lunarQuicksandOrder.Select(id => _actors.GetValueOrDefault(id)).OfType<DungeonActor>().ToArray();
        if (quicksand.Length > 0 && quicksand[0].EventState != 7)
            foreach (var zone in quicksand)
                yield return new($"lunar/quicksand/{zone.Id}", "流沙障碍", new CircleTemplate(10), zone.Position, Obstacle: true);
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 34788 or 35882
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead))
            foreach (var a in ShisuiBait($"lunar/spread/{cast.Source}", cast.TargetId, 6, cast.FinishAt))
                yield return a with { Shape = a.Display ? a.Shape : new CircleTemplate(7), OutlineOnly = a.Display,
                    Position = QuantizedDungeonPosition(a.Position) };
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var stacks = _casts.Values.Where(c => c.Rule.Action == 34822
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead).ToArray();
        var stackTargets = stacks.Select(c => c.TargetId).ToHashSet();
        var spreadTargets = _casts.Values.Where(c => c.Rule.Action is 34788 or 35882).Select(c => c.TargetId).ToHashSet();
        var ownStack = stackTargets.Contains(PlayerId);
        var stackJoin = new List<IGeometryShape>();
        foreach (var cast in stacks)
        {
            var target = _actors[cast.TargetId];
            var count = party.Count(a => Vector2.DistanceSquared(new(a.Position.X, a.Position.Z), new(target.Position.X, target.Position.Z)) <= 100);
            var inside = _actors.TryGetValue(PlayerId, out var p) && Vector2.DistanceSquared(new(p.Position.X, p.Position.Z), new(target.Position.X, target.Position.Z)) <= 100;
            var safe = cast.TargetId == PlayerId || !ownStack && !spreadTargets.Contains(PlayerId) && (inside ? count <= 4 : count < 4);
            yield return new($"lunar/stack/{cast.Source}/display", "四人分摊", new CircleTemplate(10), QuantizedDungeonPosition(target.Position),
                Navigate: false, OutlineOnly: true, Safe: safe);
            if (cast.TargetId != PlayerId)
            {
                if (safe)
                    stackJoin.Add(new CircleGeometry(target.Position, 10));
                else
                    yield return new($"lunar/stack/{cast.Source}/avoid", "避免重叠分摊", new CircleTemplate(ownStack ? 20 : 10), target.Position,
                        Display: false, Activation: cast.FinishAt);
            }
            else
            {
                var candidates = party.Where(a => !stackTargets.Contains(a.Id) && !spreadTargets.Contains(a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 5)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"lunar/stack/{cast.Source}/gather", "分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
        }
        if (stackJoin.Count > 0)
            yield return new("lunar/stack/join", "进入四人分摊", new DungeonExclusiveJoinTemplate(Arena, stackJoin.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].FinishAt);
        var lines = lunarLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var join = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var delta = line.Target.Position - line.Source.Position; var rotation = MathF.Atan2(delta.X, delta.Z);
            var safe = line.Target.Id == PlayerId || !marked;
            yield return new($"lunar/line/{line.Serial}/display", "直线分摊", RayRectangleTemplate.Symmetric(8, 50), line.Source.Position,
                rotation, Navigate: false, Safe: safe);
            if (line.Target.Id != PlayerId)
            {
                if (!marked) join.Add(RayRectangleTemplate.Symmetric(8, 50).Resolve(line.Source.Position, rotation, null));
                else
                    yield return new($"lunar/line/{line.Serial}/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 50),
                        line.Source.Position, rotation, Display: false, Activation: line.At);
            }
            else
            {
                var buddies = party.Where(a => !lines.Any(l => l.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new($"lunar/line/{line.Serial}/gather", "直线分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (join.Count > 0)
            yield return new("lunar/line/join", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, join.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> LunarDamage()
    {
        if (territory != LunarCatalog.TERRITORY) yield break;
        var lines = lunarLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var member in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(member.Id, member.PartySlot, member.PartyRole, member.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> LunarHints()
    {
        if (territory != LunarCatalog.TERRITORY) yield break;
        if (lunarDoom.Count > 0) yield return "死宣：及时驱散";
        if (lunarLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
        if (!_actors.TryGetValue(PlayerId, out var player) || Arena == null) yield break;
        var p = player.Position;
        foreach (var cast in lunarKnockbacks)
        {
            if (KnockbackImmune(player, cast.FinishAt)
                || !RayRectangleTemplate.Symmetric(10, 40).Resolve(cast.Location, cast.Rotation, null).Contains(p, 0)) continue;
            p += new Vector3(MathF.Sin(cast.Rotation), 0, MathF.Cos(cast.Rotation)) * 20;
            if (!Arena.Contains(p, 0) || lunarTowers.Any(t => t.Shape.Resolve(t.Position, t.Rotation, null).Contains(p, 0)))
                yield return "击退落点危险";
        }
    }
}
