using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> InnocenceAoes()
    {
        if (!IsInnocence || Active == null || Arena == null) yield break;
        foreach (var actor in _actors.Values.Where(a => a.Hostile && !a.Dead && a.Targetable))
            yield return new($"innocence/enemy/{actor.Id}", "无瑕灵君与敌人", new CircleTemplate(actor.HitboxRadius),
                actor.Position, Navigate: false, OutlineOnly: true);
        foreach (var wave in innocenceWaves.Take(8)) yield return wave;
        foreach (var tower in InnocenceTowerAoes()) yield return tower;
        foreach (var line in InnocenceLineAoes()) yield return line;
        foreach (var bait in innocenceBaits.Where(b => b.Action != 16070 && !b.Source.Dead))
        {
            var own = bait.Target.Id == PlayerId && !bait.TargetDestroyed;
            var radius = bait.Action == 16035 ? 3 : 10;
            var id = $"innocence/bait/{bait.Action}/{bait.Key}";
            yield return new(id + "/display", bait.Action == 16035 ? "正义之雷死刑" : "光之滴引导",
                new CircleTemplate(radius), bait.Target.Position, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "避开他人引导", new CircleTemplate(radius), bait.Target.Position,
                    Display: false, Activation: bait.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "引导远离队友", new CircleTemplate(radius), buddy.Position,
                        Display: false, Activation: bait.At);
        }
        foreach (var gaze in _casts.Values.Where(c => c.Rule.Action == 16025))
        {
            var player = _actors.GetValueOrDefault(PlayerId);
            var direction = player == null ? Vector2.Zero : new Vector2(gaze.Location.X - player.Position.X, gaze.Location.Z - player.Position.Z);
            var facing = player == null ? Vector2.Zero : new Vector2(MathF.Sin(player.Rotation), MathF.Cos(player.Rotation));
            var danger = direction.LengthSquared() > 0 && Vector2.Dot(facing, Vector2.Normalize(direction)) >= .707107f;
            var color = danger ? new Vector4(1, .2f, .2f, 1) : new Vector4(.2f, .8f, 1, 1);
            var lens = new List<Vector2>();
            for (var i = 0; i <= 16; ++i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, .35f * (1 - x * x / .49f)));
            }
            for (var i = 15; i > 0; --i)
            {
                var x = -.7f + 1.4f * i / 16;
                lens.Add(new(x, -.35f * (1 - x * x / .49f)));
            }
            yield return new($"innocence/gaze/{gaze.Source}/eye", "背对视线", new LocalPolygonTemplate(lens),
                gaze.Location, Navigate: false, Safe: !danger, Color: color);
            yield return new($"innocence/gaze/{gaze.Source}/pupil", "视线眼睛", new CircleTemplate(.18f),
                gaze.Location, Navigate: false, Color: new Vector4(0, 0, 0, 1));
            if (player != null)
                yield return new($"innocence/gaze/{gaze.Source}/facing", "当前角色面向",
                    new PtDonutSectorTemplate(.98f, 1.02f, MathF.PI / 2), player.Position, player.Rotation,
                    Navigate: false, Color: new Vector4(1, .2f, .2f, 1));
        }
    }

    private IEnumerable<DungeonAmbientAoe> InnocenceTowerAoes()
    {
        if (Arena == null || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var joins = new List<IGeometryShape>();
        var avoids = new List<(int Key, Vector3 Position)>();
        var inCorrect = false;
        foreach (var tower in innocenceTowers)
        {
            var shape = new CircleGeometry(tower.Position, 5);
            var inside = shape.Contains(player.Position);
            var count = party.Count(a => shape.Contains(a.Position));
            var correct = count is 1 or 2;
            yield return new($"innocence/tower/{tower.Key}/display", "罪球塔一至两人", new CircleTemplate(5), tower.Position,
                Navigate: false, Safe: count < 2 || inside && correct, OutlineOnly: count < 2 || inside);
            if (inside && correct) inCorrect = true;
            if (count < 2 || inside && correct) joins.Add(shape);
            else avoids.Add((tower.Key, tower.Position));
        }
        if (joins.Count > 0)
            yield return new("innocence/tower/join", "进入可参与罪球塔", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false);
        else if (!inCorrect)
            foreach (var tower in avoids)
                yield return new($"innocence/tower/{tower.Key}/avoid", "远离满员罪球塔", new CircleTemplate(5), tower.Position, Display: false);
    }

    private static float InnocenceAngle(InnocenceBait line) => line.Source.Id == line.Target.Id ? line.Source.Rotation
        : MathF.Atan2(line.Target.Position.X - line.Source.Position.X, line.Target.Position.Z - line.Source.Position.Z);

    private IEnumerable<DungeonAmbientAoe> InnocenceLineAoes()
    {
        if (Arena == null) yield break;
        var lines = innocenceBaits.Where(b => b.Action == 16070 && !b.Source.Dead).ToArray();
        var marked = lines.Any(b => b.Target.Id == PlayerId && !b.TargetDestroyed);
        var joins = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var angle = InnocenceAngle(line);
            var own = line.Target.Id == PlayerId && !line.TargetDestroyed;
            var shape = RayRectangleTemplate.Symmetric(6, 40);
            var id = $"innocence/line/{line.Key}";
            yield return new(id + "/display", "光柱直线分摊", shape, line.Source.Position, angle,
                Navigate: false, Safe: own || !marked);
            if (!own && !marked) joins.Add(shape.Resolve(line.Source.Position, angle, null));
            else if (!own)
                yield return new(id + "/avoid", "直线分摊点名互避", RayRectangleTemplate.Symmetric(12, 40),
                    line.Source.Position, angle, Display: false, Activation: line.At);
            else
            {
                var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && !lines.Any(l => l.Target.Id == a.Id && !l.TargetDestroyed))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "分摊点名靠近队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (joins.Count > 0)
            yield return new("innocence/line/join", "进入光柱直线分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }
}
