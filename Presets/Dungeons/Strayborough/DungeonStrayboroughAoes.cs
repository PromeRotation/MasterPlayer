using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> StrayAoes()
    {
        if (territory != StrayboroughCatalog.TERRITORY || Arena == null) yield break;
        if (strayShrink != null) yield return strayShrink;
        foreach (var group in straySimple.GroupBy(c => c.Rule.Action))
        {
            var count = group.Count();
            var max = group.Key switch { 36531 => 4, 37340 => 5, _ => int.MaxValue };
            var n = 0;
            foreach (var c in group.Take(max))
                yield return new($"stray/simple/{c.Source}/{c.StartedAt.Ticks}", c.Rule.Name, c.Rule.Shape!, c.Location, c.Rotation,
                    Activation: c.FinishAt, Color: group.Key == 36531 && count > 2 && n++ < 2 ? new Vector4(1, .15f, .15f, .45f) : null);
        }
        foreach (var a in strayFalling.Concat(strayPour).Concat(strayTea)) yield return a;
        for (var i = 0; i < strayCharges.Count; ++i)
        {
            var a = strayCharges[i];
            yield return new($"stray/charge/{i}", "玩偶冲锋", RayRectangleTemplate.Symmetric(2, 6), a.Position, a.Rotation,
                Activation: DateTime.MinValue);
        }
        foreach (var line in strayExas)
            foreach (var a in line.Aoes) yield return a;
        foreach (var c in strayChasers)
        {
            var p = StrayChaserPosition(c);
            var key = $"stray/chaser/{c.Serial}";
            yield return new(key + "/display", "追踪爆炸", new CircleTemplate(4), p, Navigate: false, Activation: c.At);
            var player = _actors.GetValueOrDefault(PlayerId);
            var mine = c.Target.Id == PlayerId;
            var navPosition = mine && player != null ? p - new Vector3(MathF.Sin(player.Rotation), 0, MathF.Cos(player.Rotation)) * 4 : p;
            yield return new(key + "/avoid", "躲开追踪爆炸", new CircleTemplate(mine ? 7 : 5), navPosition, Display: false, Activation: c.At);
            foreach (var a in StrayLine(key + "/line", c.Previous, c.Target.Position, false)) yield return a;
        }
        if (strayTargetMask.Contains(PlayerId))
            yield return new("stray/chase/edge", "追踪点名靠场边", new CircleTemplate(13.5f), Arena.Position, Display: false, Activation: strayChaseActivation);
        foreach (var a in StrayPartyAoes()) yield return a;
        foreach (var s in strayStretches.Where(s => !s.Source.Dead && s.Target.Id == PlayerId))
        {
            foreach (var a in StrayLine($"stray/stretch/{s.Serial}/line", s.Source.Position, s.Target.Position, s.Tether == 1)) yield return a;
            if (_actors.GetValueOrDefault(PlayerId) is { } player && HasStatus(player, 3949))
                yield return new($"stray/stretch/{s.Serial}/avoid", "拉长幽灵连线", new CircleTemplate(20), s.Source.Position,
                    Display: false, Activation: s.At);
        }
        if (!strayPlatforms) yield break;
        var ghost = _actors.GetValueOrDefault(PlayerId) is { } pc && HasStatus(pc, 3949);
        var spreads = straySpreads.Where(s => !s.Target.Dead).ToArray();
        var inverted = false;
        var activation = DateTime.MinValue;
        if (spreads.Length > 0) { inverted = ghost; activation = spreads[0].At; }
        else if (strayFleshbuster != null) { inverted = !ghost; activation = strayFleshbuster.Value; }
        else if (strayStretches.Any(s => !s.Source.Dead)) inverted = !ghost;
        var y = Arena.Position.Y;
        var platforms = new[] { StrayboroughArenas.Platform0(y), StrayboroughArenas.Platform1(y),
            StrayboroughArenas.Platform2(y), StrayboroughArenas.Platform3(y) };
        for (var i = 0; i < platforms.Length; ++i)
            yield return new($"stray/platform/{i}", inverted ? "进入变身平台" : "避开变身平台", new DungeonShapeTemplate(platforms[i]), platforms[i].Position,
                Navigate: !inverted, Safe: inverted, Activation: activation);
        if (inverted)
            yield return new("stray/platform/enter", "必须进入变身平台", new SohrKhaiWallHintTemplate(Arena, platforms),
                Arena.Position, Display: false, Activation: activation);
    }

    private static IEnumerable<DungeonAmbientAoe> StrayLine(string key, Vector3 source, Vector3 target, bool safe)
    {
        var delta = target - source;
        delta.Y = 0;
        if (delta.LengthSquared() < .0001f) yield break;
        yield return new(key, "机制连线", RayRectangleTemplate.Symmetric(.08f, delta.Length()), source, MathF.Atan2(delta.X, delta.Z),
            Navigate: false, Safe: safe, Unclipped: true);
    }

    private IEnumerable<DungeonAmbientAoe> StrayPartyAoes()
    {
        if (Arena == null) yield break;
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var player = _actors.GetValueOrDefault(PlayerId);
        var spreads = straySpreads.Where(s => !s.Target.Dead).ToArray();
        foreach (var s in spreads)
        {
            var key = $"stray/spread/{s.Serial}";
            yield return new(key + "/display", "分散", new CircleTemplate(8), QuantizedDungeonPosition(s.Target.Position),
                Navigate: false, OutlineOnly: true);
            if (player == null || HasStatus(player, 3949) || party.Length < 2) continue;
            if (s.Target.Id != PlayerId)
                yield return new(key + "/avoid", "避开分散目标", new CircleTemplate(9), QuantizedDungeonPosition(s.Target.Position),
                    Display: false, Activation: s.At);
            else
                foreach (var a in party.Where(a => a.Id != PlayerId && !spreads.Any(s => s.Target.Id == a.Id)))
                    yield return new(key + $"/party/{a.Id}", "分散远离队友", new CircleTemplate(9), a.Position, Display: false, Activation: s.At);
        }
        var lines = strayLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var joins = new List<IGeometryShape>();
        foreach (var l in lines)
        {
            var delta = l.Target.Position - l.Source.Position;
            var rotation = MathF.Atan2(delta.X, delta.Z);
            var shape = RayRectangleTemplate.Symmetric(8, 80);
            var key = $"stray/stack/{l.Serial}";
            yield return new(key + "/display", "直线分摊", shape, l.Source.Position, rotation, Navigate: false,
                Safe: l.Target.Id == PlayerId || !marked, Activation: l.At);
            if (l.Target.Id == PlayerId)
            {
                var candidates = party.Where(a => !strayLines.Any(b => b.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new(key + "/gather", "分摊目标靠近队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: l.At);
            }
            else if (!marked) joins.Add(shape.Resolve(l.Source.Position, rotation, null));
            else yield return new(key + "/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 80), l.Source.Position, rotation,
                Display: false, Activation: l.At);
        }
        if (joins.Count > 0)
            yield return new("stray/stack/join", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, joins.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> StrayDamage()
    {
        if (territory != StrayboroughCatalog.TERRITORY) yield break;
        var lines = strayLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> StrayHints()
    {
        if (territory != StrayboroughCatalog.TERRITORY) yield break;
        if (strayFleshbuster != null || strayStretches.Any(s => !s.Source.Dead)) yield return "变成幽灵";
        else if (straySpreads.Any(s => !s.Target.Dead)) yield return "变回肉身";
        var first = strayStretches.FirstOrDefault(s => s.Target.Id == PlayerId && !s.Source.Dead);
        if (first.Source != null)
            yield return first.Tether == 1 || Vector3.DistanceSquared(first.Source.Position, first.Target.Position) >= 400 ? "连线已拉长" : "继续拉长连线";
        if (strayLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
        if (strayTargetMask.Contains(PlayerId) || strayChasers.Any(c => c.Target.Id == PlayerId)) yield return "靠场边持续躲开追踪圈";
    }
}
