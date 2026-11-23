using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> IhuyAoes()
    {
        if (territory != IhuykatumuCatalog.TERRITORY || Arena == null) yield break;
        if (ihuyInhale != null) yield return ihuyInhale;
        for (var i = 0; i < ihuyBury.Count; ++i)
        {
            if (i < 2 && ihuyBury.Count > 2) ihuyBury[i] = ihuyBury[i] with { Color = new Vector4(1, 1, 0, 1) };
            yield return ihuyBury[i];
        }
        foreach (var s in ihuyShores)
        {
            GeometryTemplate shape = s.Stage == 0 ? new CircleTemplate(10) : new DonutTemplate(s.Stage * 10, (s.Stage + 1) * 10);
            yield return new($"ihuy/shore/{s.Source}/{s.Start.Ticks}", "同心震荡", shape, s.Position, Activation: s.At);
        }
        foreach (var wind in ihuyWinds.Take(4)) yield return wind;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 36349))
            yield return new($"ihuy/spark/cast/{cast.Source}", "雷场生成预告", new CircleTemplate(4),
                _actors.GetValueOrDefault(cast.TargetId)?.Position ?? cast.Location, Activation: cast.FinishAt);
        foreach (var p in ihuySparkPredictions)
            yield return new($"ihuy/spark/event/{p.Serial}", "雷场生成预告", new CircleTemplate(4), p.Position, Activation: p.At);
        foreach (var zone in _actors.Values.Where(a => a.Oid == 0x1EBA21 && a.EventState != 7))
            yield return new($"ihuy/spark/object/{zone.Id}", "持续雷场", new CircleTemplate(4), QuantizedDungeonPosition(zone.Position));
        foreach (var wind in _actors.Values.Where(a => a.Oid == 0x416C))
        {
            yield return new($"ihuy/whirlwind/{wind.Id}/display", "移动旋风", new DungeonCapsuleTemplate(4.5f, 5), wind.Position, wind.Rotation, Navigate: false);
            yield return new($"ihuy/whirlwind/{wind.Id}/obstacle", "旋风障碍", new CircleTemplate(4.5f), wind.Position, Display: false, Obstacle: true);
            foreach (var (length, seconds) in new (float, double)[] { (2.5f, 1.1), (5, 3), (10, 10), (15, double.MaxValue) })
                yield return new($"ihuy/whirlwind/{wind.Id}/{length}", "旋风前方预测", new DungeonCapsuleTemplate(4.5f, length),
                    wind.Position, wind.Rotation, Display: false, Activation: seconds == double.MaxValue ? DateTime.MaxValue : _now.AddSeconds(seconds));
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 36508 or 36513 or 36522 or 36353
            && _actors.TryGetValue(c.TargetId, out var a) && !a.Dead))
        {
            var radius = cast.Rule.Action == 36508 ? 14 : 6;
            foreach (var a in ShisuiBait($"ihuy/spread/{cast.Source}", cast.TargetId, radius, cast.FinishAt))
                yield return a with { Shape = a.Display ? a.Shape : new CircleTemplate(radius + 1), OutlineOnly = a.Display,
                    Position = QuantizedDungeonPosition(a.Position) };
        }
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 36357))
        {
            if (!_actors.TryGetValue(cast.TargetId, out var target)) continue;
            var own = cast.TargetId == PlayerId;
            yield return new($"ihuy/buster/{cast.Source}/display", "范围死刑", new CircleTemplate(6), target.Position,
                Navigate: !own, OutlineOnly: own, Activation: cast.FinishAt);
            if (own)
                foreach (var member in _actors.Values.Where(a => a.Id != PlayerId && a.PartySlot >= 0 && !a.Dead))
                    yield return new($"ihuy/buster/{cast.Source}/avoid/{member.Id}", "死刑远离队友", new CircleTemplate(6),
                        member.Position, Display: false, Activation: cast.FinishAt);
        }
        foreach (var bait in ihuyBaits.Where(b => !b.Source.Dead))
        {
            var delta = bait.Target.Position - bait.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            var own = bait.Target.Id == PlayerId;
            yield return new($"ihuy/bait/{bait.Serial}/display", "连线扇形引导", new FanTemplate(60, MathF.PI / 6),
                bait.Source.Position, angle, Navigate: !own, OutlineOnly: own, Activation: bait.At);
            yield return new($"ihuy/bait/{bait.Serial}/tether", "引导连线", new RectangleTemplate(.1f, new Vector2(delta.X, delta.Z).Length()),
                (bait.Source.Position + bait.Target.Position) * .5f, angle, Navigate: false, Color: Vector4.One);
            if (!own) continue;
            yield return new($"ihuy/bait/{bait.Serial}/edge", "连线引导至场边", new RectangleTemplate(36, 36),
                Arena.Position, Display: false, Activation: _now.AddSeconds(8.6));
            foreach (var member in _actors.Values.Where(a => a.Id != PlayerId && a.PartySlot >= 0 && !a.Dead))
                yield return new($"ihuy/bait/{bait.Serial}/party/{member.Id}", "扇形避开队友", new FanTemplate(100, MathF.PI / 6),
                    bait.Source.Position, MathF.Atan2(member.Position.X - bait.Source.Position.X, member.Position.Z - bait.Source.Position.Z),
                    Display: false, Activation: bait.At);
        }
    }

    private IEnumerable<DungeonDamagePrediction> IhuyDamage()
    {
        if (territory != IhuykatumuCatalog.TERRITORY) yield break;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 36357))
            if (_actors.TryGetValue(cast.TargetId, out var a))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, cast.FinishAt, "Tankbuster");
        foreach (var bait in ihuyBaits.Where(b => !b.Source.Dead))
        {
            var a = bait.Target;
            yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, bait.At, "Tankbuster");
        }
    }
}
