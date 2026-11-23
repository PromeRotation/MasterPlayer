using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> VanguardAoes()
    {
        if (territory != VanguardCatalog.TERRITORY || Arena == null) yield break;
        SynchronizeVanguard();
        if (vanguardShrink != null) yield return vanguardShrink;
        foreach (var aoe in vanguardEnhanced) yield return aoe;
        for (var i = 0; i < vanguardRotary.Count; ++i)
        {
            if (vanguardRotary.Count > 2 && i < 2) vanguardRotary[i] = vanguardRotary[i] with { Color = new Vector4(1, 1, 0, 1) };
            yield return vanguardRotary[i];
        }
        if (vanguardCombo.Count > 0)
        {
            vanguardCombo[0] = vanguardCombo[0] with { Navigate = true,
                Color = vanguardCombo.Count > 1 ? new Vector4(1, 1, 0, 1) : vanguardCombo[0].Color };
            if (vanguardCombo.Count > 1)
            {
                var delta = vanguardCombo[0].Rotation - vanguardCombo[1].Rotation - MathF.PI;
                if (MathF.Abs(MathF.Atan2(MathF.Sin(delta), MathF.Cos(delta))) <= MathF.PI / 180)
                    vanguardCombo[1] = vanguardCombo[1] with { Navigate = false };
            }
            foreach (var aoe in vanguardCombo.Take(2)) yield return aoe;
        }
        foreach (var group in vanguardSimple.GroupBy(c => VanguardCatalog.SimpleGroup(c.Rule.Action)))
        {
            var all = group.ToArray();
            var max = group.Key is 36570 or 37151 ? 4 : int.MaxValue;
            var danger = group.Key == 37151 ? 2 : group.Key == 37155 ? 4 : int.MaxValue;
            for (var i = 0; i < Math.Min(max, all.Length); ++i)
            {
                var cast = all[i];
                yield return new($"vanguard/simple/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name, cast.Rule.Shape!,
                    cast.Location, cast.Rotation, Navigate: group.Key != 37151 || i < 2, Activation: cast.FinishAt,
                    Color: all.Length > danger && i < danger ? new Vector4(1, 1, 0, 1) : null);
            }
        }
        if (vanguardBatteryDirty)
        {
            vanguardBatteryCache.Clear();
            foreach (var s in vanguardBattery)
            {
                var start = s.At > _now ? s.At : _now;
                for (var i = 1; i < Math.Min(9, s.Remaining); ++i)
                    vanguardBatteryCache.Add(new($"vanguard/battery/{s.Serial}/{i}", "连续旋转炮击",
                        new FanTemplate(30, MathF.PI / 6), s.Origin, s.Rotation - i * 11 * MathF.PI / 180,
                        Activation: start.AddSeconds(i * .5)));
                vanguardBatteryCache.Add(new($"vanguard/battery/{s.Serial}/0", "当前旋转炮击",
                    new FanTemplate(30, MathF.PI / 6), s.Origin, s.Rotation, Activation: s.At,
                    Color: s.Remaining > 1 ? new Vector4(1, 1, 0, 1) : null));
            }
            vanguardBatteryDirty = false;
        }
        foreach (var aoe in vanguardBatteryCache) yield return aoe;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action is 36573 or 37349 or 37922
            && _actors.TryGetValue(c.TargetId, out var target) && !target.Dead))
        {
            var radius = cast.Rule.Action == 37349 ? 8 : 5;
            foreach (var aoe in ShisuiBait($"vanguard/spread/{cast.Source}", cast.TargetId, radius, cast.FinishAt))
                yield return aoe with { Shape = aoe.Display ? aoe.Shape : new CircleTemplate(radius + 1), OutlineOnly = aoe.Display,
                    Position = QuantizedDungeonPosition(aoe.Position) };
        }
        foreach (var aoe in VanguardLineAoes()) yield return aoe;
    }

    private IEnumerable<DungeonAmbientAoe> VanguardLineAoes()
    {
        if (Arena == null) yield break;
        var lines = vanguardLines.Where(l => !l.Source.Dead).ToArray();
        var marked = lines.Any(l => l.Target.Id == PlayerId);
        var party = _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead).ToArray();
        var join = new List<IGeometryShape>();
        foreach (var line in lines)
        {
            var delta = line.Target.Position - line.Source.Position;
            var angle = MathF.Atan2(delta.X, delta.Z);
            yield return new($"vanguard/line/{line.Serial}/display", "直线分摊", RayRectangleTemplate.Symmetric(8, 36),
                line.Source.Position, angle, Navigate: false, Safe: line.Target.Id == PlayerId || !marked);
            if (line.Target.Id != PlayerId)
            {
                if (!marked) join.Add(RayRectangleTemplate.Symmetric(8, 36).Resolve(line.Source.Position, angle, null));
                else
                    yield return new($"vanguard/line/{line.Serial}/avoid", "避免重叠直线分摊", RayRectangleTemplate.Symmetric(16, 36),
                        line.Source.Position, angle, Display: false, Activation: line.At);
            }
            else
            {
                var candidates = party.Where(a => !lines.Any(l => l.Target.Id == a.Id)).Select(a => (IGeometryShape)new CircleGeometry(a.Position, 2)).ToArray();
                if (candidates.Length > 0)
                    yield return new($"vanguard/line/{line.Serial}/gather", "直线分摊点名靠拢队友", new SohrKhaiWallHintTemplate(Arena, candidates),
                        Arena.Position, Display: false, Activation: line.At);
            }
        }
        if (join.Count > 0)
            yield return new("vanguard/line/join", "进入直线分摊", new DungeonExclusiveJoinTemplate(Arena, join.ToArray()),
                Arena.Position, Display: false, Activation: lines[0].At);
    }

    private IEnumerable<DungeonDamagePrediction> VanguardDamage()
    {
        if (territory != VanguardCatalog.TERRITORY) yield break;
        foreach (var buster in vanguardBusters)
        {
            var a = _actors.GetValueOrDefault(buster.Target.Id) ?? buster.Target;
            if (a.PartySlot >= 0)
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, buster.At, "Tankbuster");
        }
        var lines = vanguardLines.Where(l => !l.Source.Dead).ToArray();
        if (lines.Length > 0)
            foreach (var a in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead))
                yield return new(a.Id, a.PartySlot, a.PartyRole, a.Name, lines.Min(l => l.At), "Shared");
    }

    private IEnumerable<string> VanguardHints()
    {
        if (territory != VanguardCatalog.TERRITORY) yield break;
        if (vanguardBombs.TryGetValue(PlayerId, out var at) && at <= _now.AddSeconds(3))
            yield return VanguardRequireAllStop ? "加速度炸弹：停手、停读条、停止自动攻击并保持不动" : "加速度炸弹即将判定：准备停止一切动作";
        if (vanguardLines.Any(l => !l.Source.Dead)) yield return "直线分摊";
    }
}
