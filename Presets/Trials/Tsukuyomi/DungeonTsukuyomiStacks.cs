using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TsukuyomiLineAoes()
    {
        if (Arena == null) yield break;
        var lines = tsukuyomiLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var angle = line.Source.Id == line.Target.Id ? line.Source.Rotation
                : MathF.Atan2(line.Target.Position.X - line.Source.Position.X, line.Target.Position.Z - line.Source.Position.Z);
            var own = line.Target.Id == PlayerId;
            var shape = RayRectangleTemplate.Symmetric(8, 70);
            var id = $"tsukuyomi/line/{line.Key}";
            yield return new(id + "/display", "黄泉之弹直线分摊", shape, line.Source.Position, angle,
                Navigate: false, Safe: own || !marked);
            if (!own && !marked) joins.Add(shape.Resolve(line.Source.Position, angle, null));
            else if (!own)
                yield return new(id + "/avoid", "分摊点名互不重叠", RayRectangleTemplate.Symmetric(16, 70),
                    line.Source.Position, angle, Display: false, Activation: line.At);
            else
            {
                var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && !tsukuyomiLines.Any(l => l.Target.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "直线分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (joins.Count > 0)
            yield return new("tsukuyomi/line/join", "进入直线分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
        // The source LineStack stores 8/8 metadata but GenericBaitStack does not enforce a count.
    }

    private IEnumerable<DungeonAmbientAoe> TsukuyomiLunacyAoes()
    {
        if (Arena == null) yield break;
        var stacks = tsukuyomiLunacy.Select(c => (Cast: c, Target: tsukuyomiActors.GetValueOrDefault(c.TargetId)))
            .Where(s => s.Target is { Dead: false }).ToArray();
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var marked = stacks.Any(s => s.Target!.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var (cast, targetValue) in stacks)
        {
            var target = targetValue!;
            var position = QuantizedDungeonPosition(target.Position);
            var circle = new CircleGeometry(position, 6);
            var count = party.Count(a => circle.Contains(a.Position));
            var inside = _actors.GetValueOrDefault(PlayerId) is { } player && circle.Contains(player.Position);
            var canJoin = inside ? count <= 8 : count < 8;
            var id = $"tsukuyomi/lunacy/{cast.Source}";
            yield return new(id + "/display", "八人分摊", new CircleTemplate(6), position,
                Navigate: false, OutlineOnly: true, Safe: target.Id == PlayerId || !marked && canJoin);
            if (party.Length < 2) continue;
            if (target.Id == PlayerId)
            {
                var buddies = party.Where(a => a.Id != PlayerId && !stacks.Any(s => s.Target!.Id == a.Id))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 3)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: cast.FinishAt);
            }
            else if (canJoin) joins.Add(circle);
            else
                yield return new(id + "/avoid", "避免超员分摊", new CircleTemplate(marked ? 12 : 6), position,
                    Display: false, Activation: cast.FinishAt);
        }
        if (joins.Count > 0)
            yield return new("tsukuyomi/lunacy/join", "进入八人分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: stacks[0].Cast.FinishAt);
    }
}
