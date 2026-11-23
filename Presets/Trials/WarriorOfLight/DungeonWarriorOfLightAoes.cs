using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private static float WolAngle(WarriorOfLightBait b) => b.Source.Id == b.Target.Id ? b.Source.Rotation
        : MathF.Atan2(b.Target.Position.X - b.Source.Position.X, b.Target.Position.Z - b.Source.Position.Z);
    private bool WolOwn(WarriorOfLightBait b) => b.Target.Id == PlayerId && !b.TargetDestroyed;

    private IEnumerable<DungeonAmbientAoe> WolAoes()
    {
        if (!IsWarriorOfLight || Active == null || Arena == null) yield break;
        if (_actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
            yield return new("wol/enemy", "光之战士", new CircleTemplate(boss.HitboxRadius), boss.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in wolBrimstone.Take(4)) yield return aoe;
        foreach (var aoe in wolSwords) yield return aoe;
        foreach (var cast in wolDecimation.Take(4))
            yield return new($"wol/decimation/{cast.Source}", "完美破坏连续扇形", cast.Rule.Shape!, cast.Location,
                cast.Rotation, Activation: cast.FinishAt);
        foreach (var b in wolBaits.Where(b => b.Action != 20248 && !b.Source.Dead))
        {
            var cone = b.Action is 21076 or 20260;
            GeometryTemplate shape = cone ? new FanTemplate(60, MathF.PI * (b.Action == 21076 ? 92 : 90) / 180)
                : new CircleTemplate(b.Action == 20256 ? 20 : 5);
            var position = cone ? b.Source.Position : b.Target.Position;
            var angle = WolAngle(b);
            var own = WolOwn(b);
            var id = $"wol/bait/{b.Action}/{b.Key}";
            yield return new(id + "/display", "光之战士引导", shape, position, angle, Navigate: false, OutlineOnly: own);
            if (!own)
                yield return new(id + "/avoid", "远离他人引导", shape, position, angle, Display: false, Activation: b.At);
            else if (b.Source.Id != b.Target.Id)
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId))
                    yield return new(id + $"/buddy/{buddy.Id}", "引导远离队友",
                        cone ? new FanTemplate(100, MathF.PI * (b.Action == 21076 ? 92 : 90) / 180) : shape,
                        cone ? b.Source.Position : buddy.Position,
                        cone ? MathF.Atan2(buddy.Position.X - b.Source.Position.X, buddy.Position.Z - b.Source.Position.Z) : 0,
                        Display: false, Activation: b.At);
            if (b.Action == 20260)
            {
                var distance = Vector2.Distance(new(b.Source.Position.X, b.Source.Position.Z), new(b.Target.Position.X, b.Target.Position.Z));
                yield return new(id + "/tether", "光之龙吐息连线", RayRectangleTemplate.Symmetric(.06f, distance),
                    b.Source.Position, angle, Navigate: false, Unclipped: true, Color: new Vector4(1, 1, 1, 1));
            }
        }
        foreach (var aoe in WolLineAoes()) yield return aoe;
        foreach (var aoe in WolPartyAoes()) yield return aoe;
        foreach (var aoe in WolTowerAoes()) yield return aoe;
        foreach (var aoe in WolMovementAoes()) yield return aoe;
        if (wolBaits.Any(b => b.Action == 20256 && !b.Source.Dead && WolOwn(b))
            || wolMarks.Any(m => m.Action == 20251 && !m.Target.Dead && m.Target.Id == PlayerId && !m.Destroyed))
            yield return new("wol/corners", "距离衰减与陨石引导至四角", new SohrKhaiWallHintTemplate(Arena,
                new[] { new Vector3(80, 0, 80), new(120, 0, 80), new(120, 0, 120), new(80, 0, 120) }
                    .Select(p => (IGeometryShape)new CircleGeometry(p, 3)).ToArray()), Arena.Position, Display: false, Activation: _now.AddSeconds(5));
    }

    private IEnumerable<DungeonAmbientAoe> WolTowerAoes()
    {
        if (Arena == null || wolTowers.Count == 0 || _actors.GetValueOrDefault(PlayerId) is not { } player) yield break;
        var tower = wolTowers.MinBy(t => t.At);
        var shape = new CircleGeometry(tower.Position, tower.Radius);
        var inside = shape.Contains(player.Position);
        var count = _actors.Values.Count(a => a.PartySlot >= 0 && !a.Dead && shape.Contains(a.Position));
        var correct = count >= 2 && count <= tower.Max;
        var join = count < tower.Max || inside && correct;
        yield return new($"wol/tower/{tower.Key}/display", "最早双人塔", new CircleTemplate(tower.Radius), tower.Position,
            Navigate: false, Safe: join, OutlineOnly: join || inside);
        yield return join
            ? new("wol/tower/join", "按顺序进入最早双人塔", new SohrKhaiWallHintTemplate(Arena, [shape]), Arena.Position,
                Display: false, Activation: tower.At)
            : new("wol/tower/avoid", "避开满员双人塔", new CircleTemplate(tower.Radius), tower.Position, Display: false, Activation: tower.At);
    }

    private IEnumerable<DungeonAmbientAoe> WolLineAoes()
    {
        if (Arena == null) yield break;
        var lines = wolBaits.Where(b => b.Action == 20248 && !b.Source.Dead).ToArray();
        var marked = lines.Any(WolOwn);
        var joins = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var id = $"wol/line/{line.Key}";
            var angle = WolAngle(line);
            var shape = RayRectangleTemplate.Symmetric(8, 60);
            var own = WolOwn(line);
            yield return new(id + "/display", "光之亡命徒直线分摊", shape, line.Source.Position, angle,
                Navigate: false, Safe: own || !marked);
            if (!own && !marked) joins.Add(shape.Resolve(line.Source.Position, angle, null));
            else if (!own)
                yield return new(id + "/avoid", "直线点名互避", RayRectangleTemplate.Symmetric(16, 60), line.Source.Position, angle,
                    Display: false, Activation: line.At);
            else
            {
                var buddies = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && !wolBaits.Any(b => b.Action == 20248 && b.Target.Id == a.Id && !b.TargetDestroyed))
                    .Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (buddies.Length > 0)
                    yield return new(id + "/buddy", "点名靠近未点名队友", new SohrKhaiWallHintTemplate(Arena, buddies),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (joins.Count > 0)
            yield return new("wol/line/join", "进入光之亡命徒分摊", new SohrKhaiWallHintTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }
}
