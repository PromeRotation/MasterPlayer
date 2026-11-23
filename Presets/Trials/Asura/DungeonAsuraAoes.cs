using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> AsuraAoes()
    {
        if (!IsAsura || Active == null || Arena == null) yield break;
        if (_actors.GetValueOrDefault(PrimaryId) is { Dead: false, Targetable: true } boss)
            yield return new("asura/enemy", "阿修罗", new CircleTemplate(boss.HitboxRadius), boss.Position, Navigate: false, OutlineOnly: true);
        foreach (var aoe in asuraLacerations) yield return aoe;
        if (asuraFace != null) yield return asuraFace;
        if (asuraShrink != null) yield return asuraShrink;
        foreach (var cast in _casts.Values.Where(c => c.Rule.Action == 35997))
            yield return new($"asura/chakra5/{cast.Source}", "外圈脉轮", new DonutTemplate(18, 20),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        foreach (var cast in asuraMyriad.Take(6))
            yield return new($"asura/myriad/{cast.Source}", "万象", cast.Rule.Shape!, cast.Location, cast.Rotation, Activation: cast.FinishAt);
        for (var i = 0; i < Math.Min(2, asuraKhadga.Count); ++i)
        {
            var aoe = asuraKhadga[i];
            if (i == 0)
                aoe = aoe with { Navigate = true, Color = asuraKhadga.Count > 1 ? new Vector4(1, 1, 0, 1) : aoe.Color };
            else
            {
                var difference = asuraKhadga[0].Rotation - aoe.Rotation - MathF.PI;
                difference = MathF.Atan2(MathF.Sin(difference), MathF.Cos(difference));
                if (MathF.Abs(difference) <= MathF.PI / 180) aoe = aoe with { Navigate = false };
            }
            // Risky/Color 是源 AOEInstance 的持久状态；成为首项时仅恢复 Risky。
            asuraKhadga[i] = aoe;
            yield return aoe;
        }
        if (asuraKhadga.Count > 1)
            yield return new("asura/khadga/center", "六连斩靠近中心", new SohrKhaiWallHintTemplate(Arena,
                [new MasterPlayer.Spatial.Geometry.CircleGeometry(asuraKhadga[0].Position, 3)]),
                Arena.Position, Display: false, Activation: asuraKhadga[0].Activation);
        foreach (var b in asuraBaits.Where(b => b.Action == 36000 ? !b.Source.Dead : !b.Target.Dead))
        {
            var own = b.Target.Id == PlayerId && !b.TargetDestroyed;
            var radius = b.Action == 36000 ? 4 : 5;
            var pos = b.Action == 36000 ? b.Target.Position : QuantizedDungeonPosition(b.Target.Position);
            var id = $"asura/bait/{b.Action}/{b.Key}";
            yield return new(id + "/display", b.Action == 36000 ? "切割宝石" : "有序混沌", new CircleTemplate(radius),
                pos, Navigate: false, OutlineOnly: b.Action == 36003 || own);
            var navRadius = b.Action == 36003 ? 6 : radius;
            if (!own)
                yield return new(id + "/avoid", "远离点名", new CircleTemplate(navRadius), pos, Display: false, Activation: b.At);
            else
                foreach (var buddy in _actors.Values.Where(a => a.PartySlot >= 0 && !a.Dead && a.Id != PlayerId
                    && (b.Action != 36003 || !asuraBaits.Any(other => other.Action == 36003 && other.Target.Id == a.Id && !other.TargetDestroyed && !other.Target.Dead))))
                    yield return new(id + $"/buddy/{buddy.Id}", "点名远离队友", new CircleTemplate(navRadius),
                        b.Action == 36003 ? QuantizedDungeonPosition(buddy.Position) : buddy.Position,
                        Display: false, Activation: b.At);
        }
    }

    private IEnumerable<DungeonDamagePrediction> AsuraDamage()
    {
        if (!IsAsura || Active == null) yield break;
        var baits = asuraBaits.Where(b => b.Action == 36000).ToArray();
        foreach (var b in baits.Where(b => !b.Source.Dead).DistinctBy(b => (b.Target.Id, b.TargetDestroyed)))
            if (b.Target.PartySlot >= 0)
                yield return new(b.Target.Id, b.Target.PartySlot, b.Target.PartyRole, b.Target.Name, baits[0].At, "Tankbuster");
    }

    private IEnumerable<string> AsuraHints()
    {
        if (!IsAsura || Active == null) yield break;
        if (asuraKhadga.Count > 0) yield return $"六连斩：剩余 {asuraKhadga.Count} 段";
        if (asuraFace != null) yield return "脸谱：避开半场";
        if (asuraBaits.Any(b => b.Action == 36000)) yield return "范围死刑";
        if (asuraBaits.Any(b => b.Action == 36003 && !b.Target.Dead)) yield return "有序混沌：分散";
    }
}
