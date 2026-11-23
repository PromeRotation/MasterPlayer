using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> ZeleniaAoes()
    {
        if (!IsZelenia || Active == null || Arena == null) yield break;
        if (_actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
            yield return new("zelenia/enemy", "泽莲尼娅", new CircleTemplate(boss.HitboxRadius),
                boss.Position, Navigate: false, OutlineOnly: true);
        if (zeleniaShrink != null) yield return zeleniaShrink;
        foreach (var aoe in zeleniaThunderIV.Take(1)) yield return aoe;
        var tiles = new HashSet<int>();
        foreach (var entry in zeleniaThunderIII)
            if (entry.Tile < 0 || tiles.Add(entry.Tile)) yield return entry.Aoe;
        foreach (var aoe in zeleniaShock) yield return aoe;
        if (zeleniaAscension.Count > 1)
            foreach (var aoe in zeleniaAscension.Take(2)) yield return aoe;
        foreach (var cast in zeleniaHoly.Take(2))
            yield return new($"zelenia/holy/{cast.Source}", "神圣危机", cast.Rule.Shape!,
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        for (var i = 0; i < Math.Min(4, zeleniaSlash.Count); ++i)
        {
            var cast = zeleniaSlash[i];
            yield return new($"zelenia/slash/{cast.Source}", "雷鸣斩", cast.Rule.Shape!,
                cast.Location, cast.Rotation, Activation: cast.FinishAt,
                Color: zeleniaSlash.Count > 2 && i < 2 ? new Vector4(1, 1, 0, 1) : null);
        }
        if (zeleniaKnockbacks.FirstOrDefault() is { } first)
            yield return new("zelenia/knockback/join", "进入击退中心六码", new SohrKhaiWallHintTemplate(Arena,
                [new CircleGeometry(first.Location, 6)]), Arena.Position, Display: false, Activation: first.FinishAt);
        if (_actors.GetValueOrDefault(PlayerId) is { } player)
        {
            var start = player.Position;
            // 此机制 ignoreImmunes=true，抗击退状态不改变轨迹。
            foreach (var cast in zeleniaKnockbacks)
            {
                var delta = new Vector3(start.X - cast.Location.X, 0, start.Z - cast.Location.Z);
                var landing = delta.LengthSquared() > 0 ? start + Vector3.Normalize(delta) * 10 : start;
                foreach (var aoe in DungeonMovementPreview($"zelenia/knockback/{cast.Source}", "蔷薇血击退", start, landing))
                    yield return aoe;
                start = landing;
            }
        }
        foreach (var bait in zeleniaBaits.Where(b => !b.Source.Actor.Dead))
        {
            var source = bait.Source.Actor;
            var target = bait.Target.Actor;
            var angle = source.Id == target.Id ? source.Rotation
                : MathF.Atan2(target.Position.X - source.Position.X, target.Position.Z - source.Position.Z);
            var own = bait.Target == zeleniaActors.GetValueOrDefault(PlayerId);
            var id = $"zelenia/specter/{bait.Key}";
            yield return new(id + "/display", "亡者幻影", new FanTemplate(50, MathF.PI / 4),
                source.Position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "避开扇形死刑", new FanTemplate(50, MathF.PI / 4),
                    source.Position, angle, Display: false, Activation: bait.At);
            else if (source.Id != target.Id)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "死刑引导远离队友", new FanTemplate(100, MathF.PI / 4),
                        source.Position, MathF.Atan2(buddy.Position.X - source.Position.X, buddy.Position.Z - source.Position.Z),
                        Display: false, Activation: bait.At);
        }
        foreach (var aoe in ZeleniaPartyAoes()) yield return aoe;
    }

    private IEnumerable<DungeonDamagePrediction> ZeleniaDamage()
    {
        if (!IsZelenia || Active == null) yield break;
        var spreads = zeleniaMarks.Where(m => m.Action == 581 && !m.Target.Actor.Dead).ToArray();
        foreach (var spread in spreads.DistinctBy(m => m.Target))
            if (spread.Target.Actor is { PartySlot: >= 0 } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name,
                    spreads.Min(m => m.At), "Raidwide");
        foreach (var bait in zeleniaBaits.Where(b => !b.Source.Actor.Dead).DistinctBy(b => b.Target))
            if (bait.Target.Actor is { PartySlot: >= 0 } actor)
                yield return new(actor.Id, actor.PartySlot, actor.PartyRole, actor.Name, zeleniaBaits[0].At, "Tankbuster");
    }

    private IEnumerable<string> ZeleniaHints()
    {
        if (!IsZelenia || Active == null || Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        if (zeleniaBaits.Any(b => !b.Source.Actor.Dead)) yield return "扇形死刑：远离队友";
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        foreach (var mark in zeleniaMarks.Where(m => !m.Target.Actor.Dead))
        {
            var position = QuantizedDungeonPosition(mark.Target.Actor.Position);
            var own = mark.Target == zeleniaActors.GetValueOrDefault(PlayerId);
            if (mark.Action == 581)
            {
                if (own ? party.Any(a => a.Id != PlayerId && Inside(a.Position, position, 4)) : Inside(player.Position, position, 4))
                    yield return "震击：分散远离队友";
            }
            else if (own)
            {
                var count = party.Count(a => Inside(a.Position, position, 6));
                if (count != 8) yield return $"四连分摊人数不足或超员：{count}/8";
                if (party.Any(a => a.Id != PlayerId && Inside(a.Position, position, 6)
                    && zeleniaMarks.Any(m => m.Action == 590 && !m.Target.Actor.Dead && m.Target == zeleniaActors.GetValueOrDefault(a.Id))))
                    yield return "分摊点名重叠";
            }
        }
        var stacks = zeleniaMarks.Where(m => m.Action == 590 && !m.Target.Actor.Dead).ToArray();
        if (!stacks.Any(m => m.Target == zeleniaActors.GetValueOrDefault(PlayerId)))
        {
            var participating = stacks.Count(m => Inside(player.Position, QuantizedDungeonPosition(m.Target.Actor.Position), 6));
            if (participating > 1) yield return "同时位于多个分摊圈";
            if (participating == 0 && stacks.Any(m => 1 + party.Count(a => zeleniaActors.GetValueOrDefault(a.Id) != m.Target
                && Inside(a.Position, QuantizedDungeonPosition(m.Target.Actor.Position), 6)) < 8))
                yield return "进入人数不足的分摊圈";
        }
        var start = player.Position;
        foreach (var cast in zeleniaKnockbacks)
        {
            var delta = new Vector3(start.X - cast.Location.X, 0, start.Z - cast.Location.Z);
            var landing = delta.LengthSquared() > 0 ? start + Vector3.Normalize(delta) * 10 : start;
            if (!Arena.Contains(landing)) yield return "击退后将出界";
            start = landing;
        }
        static bool Inside(Vector3 a, Vector3 b, float radius) =>
            Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) <= radius * radius;
    }
}
