using System;
using System.Collections.Generic;
using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using PromeRotation.LogSystem;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Dodge;

public enum AoeShapeKind
{
    Circle,
    Donut,
    Fan,
    Rectangle,
    RayRectangle,
}

// Param1/Param2 随 Kind 变化：Circle=半径/-；Donut=内半径/外半径；Fan=半径/总开角(弧度)；Rectangle/RayRectangle=宽/长。
// ExtraDelaySeconds 在读条时长之外追加的判定延迟（部分技能判定晚于读条结束）。
public sealed record AoeShapeDescriptor(AoeShapeKind Kind, float Param1, float Param2, bool FollowCaster, float ExtraDelaySeconds)
{
    public GeometryTemplate? ExactTemplate { get; init; }
    public bool UseCastRotation { get; init; }
    public bool UseSourcePosition { get; init; }

    // CastType 8 冲锋：长度改用读条事件里 施法者→落点 的距离（Param2 仅作兜底），锚定施法者位置、朝向落点。
    public bool IsCharge { get; init; }
}

public static class AoeShapeDatabase
{
    // CastType 2/5 且 EffectRange ≥ 30 视为全屏 AOE，不画危险区。
    private const float RaidwideSize = 30f;

    private static readonly Dictionary<uint, AoeShapeDescriptor> KnownShapes = new()
    {
        // 占位示例条目，尚未核对真实副本数据。
        [20001u] = new AoeShapeDescriptor(AoeShapeKind.Circle, 8f, 0f, false, 0f),
        [20002u] = new AoeShapeDescriptor(AoeShapeKind.Fan, 20f, MathF.PI * 2f / 3f, true, 0f),
        [20003u] = new AoeShapeDescriptor(AoeShapeKind.Rectangle, 6f, 40f, true, 0.3f),
        // 斗兽 CrucibleSpells + CrucibleTreantAOEs：R60，总角 90/150/150，锁定读条落点与 CastRotation。
        [0xBE8Au] = new(AoeShapeKind.Fan, 60f, MathF.PI / 2f, false, 0.3f) { UseCastRotation = true },
        [0xBE8Bu] = new(AoeShapeKind.Fan, 60f, MathF.PI * 5f / 6f, false, 0.3f) { UseCastRotation = true },
        [0xBE8Cu] = new(AoeShapeKind.Fan, 60f, MathF.PI * 5f / 6f, false, 0.3f) { UseCastRotation = true },
        // 朝圣31–40：包内施法朝向已包含左右侧偏转，不能跟随普通面朝向或再次加90度。
        [44690u] = new(AoeShapeKind.Fan, 60f, MathF.PI, false, 0.3f) { UseCastRotation = true },
        [44691u] = new(AoeShapeKind.Fan, 60f, MathF.PI, false, 0.3f) { UseCastRotation = true },
        // 61–70层震荡波：R30半场，首击约在包内读条结束后0.3秒生效；另一侧追击由效果事件推进。
        [42214u] = new(AoeShapeKind.Fan, 30f, MathF.PI, false, 0.3f) { UseCastRotation = true },
        [42216u] = new(AoeShapeKind.Fan, 30f, MathF.PI, false, 0.3f) { UseCastRotation = true },
        // 执行贯穿为落点R6圆，首击效果后接同落点R5–10月环，不能跟随跳跃前的来源位置。
        [42516u] = new(AoeShapeKind.Circle, 6f, 0f, false, 0.3f),
        // 71–80层双水流：首击锁定自身落点，第二击无读条，由首击效果推进。
        [42578u] = new(AoeShapeKind.Circle, 10f, 0f, false, 0.3f),
        [42748u] = new(AoeShapeKind.Donut, 10f, 40f, false, 0.3f),
        // 道中月环的内圈依据 PTFloorModule；Omen 缺失/缩放不能直接用于推断。
        [44732u] = new(AoeShapeKind.Donut, 5f, 30f, true, 0f),
        [44758u] = new(AoeShapeKind.Donut, 5f, 60f, true, 0f),
        // 可离开范围的道中大圆，不能按通用 R>=30 规则当作全屏伤害丢弃。
        [42523u] = new(AoeShapeKind.Circle, 45f, 0f, true, 0f),
        [43133u] = new(AoeShapeKind.Circle, 46f, 0f, true, 0f),
        [44759u] = new(AoeShapeKind.Fan, 8f, MathF.PI, false, 0.3f) { UseCastRotation = true },
        // 大地钻击：BMR PTFloorModule 的 R3–30、270°环扇形，不能退化为完整月环。
        [42091u] = new(AoeShapeKind.Fan, 30f, MathF.PI * 1.5f, false, 0f)
        {
            UseCastRotation = true,
            ExactTemplate = new Mechanics.PilgrimsTraverse.PtDonutSectorTemplate(3, 30, MathF.PI * 1.5f),
        },
    };

    public static bool TryGetShape(uint actionId, out AoeShapeDescriptor? descriptor)
        => TryGetShape(actionId, 0f, out descriptor);

    // casterHitboxRadius 为施法者 hitbox 半径：计入 CastType 3/4/5 的尺寸。
    public static bool TryGetShape(uint actionId, float casterHitboxRadius, out AoeShapeDescriptor? descriptor)
    {
        if (Mechanics.Blunderville.BlundervilleCatalog.TryGet(actionId, out _))
            return Mechanics.Blunderville.BlundervilleCatalog.TryGetShape(actionId, out descriptor);
        if (Mechanics.Dungeons.ArrDungeonCatalog.TryGetShape(actionId, out descriptor))
            return true;
        if (Mechanics.Crucible.CrucibleActionCatalog.IsClaimed(actionId))
            return Mechanics.Crucible.CrucibleActionCatalog.TryGetShape(actionId, out descriptor);
        if (KnownShapes.TryGetValue(actionId, out descriptor))
        {
            return true;
        }

        descriptor = InferFromActionSheet(actionId, casterHitboxRadius);
        return descriptor is not null;
    }

    internal static GeometryTemplate? CreateTemplate(AoeShapeDescriptor descriptor) => descriptor.ExactTemplate ?? (descriptor.Kind switch
    {
        AoeShapeKind.Circle when descriptor.Param1 > 0f => new CircleTemplate(descriptor.Param1),
        AoeShapeKind.Donut when descriptor.Param1 >= 0f && descriptor.Param2 > descriptor.Param1
            => new DonutTemplate(descriptor.Param1, descriptor.Param2),
        AoeShapeKind.Fan when descriptor.Param1 > 0f && descriptor.Param2 > 0f
            => new FanTemplate(descriptor.Param1, MathF.Min(descriptor.Param2, MathF.PI * 2f)),
        AoeShapeKind.Rectangle when descriptor.Param1 > 0f && descriptor.Param2 > 0f
            => new RectangleTemplate(descriptor.Param1, descriptor.Param2),
        AoeShapeKind.RayRectangle when descriptor.Param1 > 0f && descriptor.Param2 > 0f
            => RayRectangleTemplate.Symmetric(descriptor.Param1, descriptor.Param2),
        _ => null,
    });

    // 冲锋（CastType 8）长度 = 施法者→落点距离，需要读条事件；其余形状与单参重载一致。
    internal static GeometryTemplate? CreateTemplate(AoeShapeDescriptor descriptor, LogSystemCastStartEvent e)
    {
        if (!descriptor.IsCharge)
        {
            return CreateTemplate(descriptor);
        }

        if (descriptor.Param1 <= 0f || e.SourcePosition is not { } source)
        {
            return null;
        }

        var dx = e.EffectPosition.X - source.X;
        var dz = e.EffectPosition.Z - source.Z;
        var length = MathF.Sqrt(dx * dx + dz * dz);
        return length > 0.5f ? RayRectangleTemplate.Symmetric(descriptor.Param1, length) : null;
    }

    internal static bool TryCreateBinding(LogSystemCastStartEvent e, AoeShapeDescriptor descriptor, out TransformBinding binding, uint territory = 0)
    {
        binding = null!;
        // 朝圣读条的方向由施法包锁定；物体面朝向可能相差90°或在读条期间改变。
        // 冲锋保留来源→落点方向，读条结束后的动态持续区不经过此绑定。
        var lockDirection = descriptor.UseCastRotation || (territory is >= 1281 and <= 1290)
            && (descriptor.Kind is AoeShapeKind.Fan or AoeShapeKind.Rectangle or AoeShapeKind.RayRectangle) && !descriptor.IsCharge;
        var rotation = e.SourceRotation;
        if (lockDirection && !CastRotation.TryGet(e, out rotation))
            return false;

        if (descriptor.IsCharge)
        {
            // 冲锋矩形锚定施法者当前位置、朝向读条落点（FFXIV 弧度约定：0=+Z，π/2=+X）。
            // 缺施法者位置时 CreateTemplate 已失败，不会走到兜底。
            binding = e.SourcePosition is { } source
                ? new StaticTransform(source, MathF.Atan2(e.EffectPosition.X - source.X, e.EffectPosition.Z - source.Z))
                : new StaticTransform(e.EffectPosition, rotation);
            return true;
        }

        var sourceId = (uint)e.SourceId;
        binding = descriptor.FollowCaster && sourceId != 0u && !lockDirection
            ? new FollowEntityPositionAndRotation(sourceId, Vector2.Zero, MissingEntityPolicy.KeepLastResolved)
            : new StaticTransform(descriptor.UseSourcePosition && e.SourcePosition is { } origin ? origin : e.EffectPosition, rotation);
        return true;
    }

    // Lumina Action 表兜底推断，CastType → 形状映射：
    //   2=落点/自身圆（不加 hitbox，TargetArea=落点型静态锚定）；3=扇形（半径+hitbox，总开角取 Omen 路径 fanXXX 度数，缺省 180°）；
    //   4=直线矩形（长+hitbox，XAxisModifier=全宽）；5=自身圆（半径+hitbox）；8=冲锋矩形（宽=XAxisModifier，长按施法者→落点距离）；
    //   10=月环（外径=EffectRange，内径取 Omen 路径 sircle_/sicle_/circle 后两位数字，缺省 3）；12=矩形（不加 hitbox）；13=扇形（不加 hitbox）。
    //   跳过：1=单体、6=自定义、7=玩家地面圈、11=十字（无对应模板）、2/5 且 EffectRange≥30（全屏）、VFX 25（视线类）。
    private static AoeShapeDescriptor? InferFromActionSheet(uint actionId, float casterHitboxRadius)
    {
        try
        {
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
            if (!row.HasValue)
            {
                return null;
            }

            var action = row.Value;
            if (action.VFX.RowId == 25)
            {
                return null;
            }

            float effectRange = action.EffectRange;
            float fullWidth = action.XAxisModifier;
            return action.CastType switch
            {
                2 when effectRange is > 0f and < RaidwideSize
                    => new AoeShapeDescriptor(AoeShapeKind.Circle, effectRange, 0f, !action.TargetArea, 0f),
                3 when effectRange > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.Fan, effectRange + casterHitboxRadius, ConeAngleRadians(action), true, 0f),
                4 when effectRange > 0f && fullWidth > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.RayRectangle, fullWidth, effectRange + casterHitboxRadius, true, 0f),
                5 when effectRange is > 0f and < RaidwideSize
                    => new AoeShapeDescriptor(AoeShapeKind.Circle, effectRange + casterHitboxRadius, 0f, true, 0f),
                8 when fullWidth > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.RayRectangle, fullWidth, effectRange, false, 0f) { IsCharge = true },
                10 when effectRange > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.Donut, DonutInnerRadius(action), effectRange, !action.TargetArea, 0f),
                12 when effectRange > 0f && fullWidth > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.RayRectangle, fullWidth, effectRange, true, 0f),
                13 when effectRange > 0f
                    => new AoeShapeDescriptor(AoeShapeKind.Fan, effectRange, ConeAngleRadians(action), true, 0f),
                _ => null,
            };
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] 从 Lumina 推断 AOE 形状失败 (ActionId={actionId})");
            return null;
        }
    }

    // Omen 贴图路径中 fanXXX 的三位数即总开角度数（如 gl_fan120_1 → 120°），解析失败回退 180°。
    private static float ConeAngleRadians(Lumina.Excel.Sheets.Action action)
    {
        const float fallback = MathF.PI;
        if (action.Omen.ValueNullable is not { } omen)
        {
            return fallback;
        }

        var path = omen.Path.ToString();
        var pos = path.IndexOf("fan", StringComparison.Ordinal);
        if (pos < 0 || pos + 6 > path.Length || !int.TryParse(path.AsSpan(pos + 3, 3), out var degrees))
        {
            return fallback;
        }

        return MathF.Min(degrees, 360) * MathF.PI / 180f;
    }

    // Omen 路径关键词后两位数字为内径；解析不到回退 3。
    private static float DonutInnerRadius(Lumina.Excel.Sheets.Action action)
    {
        if (action.Omen.ValueNullable is { } omen)
        {
            var path = omen.Path.ToString();
            if (ExtractDonutInner(path, "sircle_", 9) is { } a) return a;
            if (ExtractDonutInner(path, "sicle_", 8) is { } b) return b;
            if (ExtractDonutInner(path, "circle", 8) is { } c) return c;
        }

        return 3f;
    }

    private static float? ExtractDonutInner(string path, string keyword, int offset)
    {
        var pos = path.IndexOf(keyword, StringComparison.Ordinal);
        return pos >= 0 && pos + offset + 2 <= path.Length && int.TryParse(path.AsSpan(pos + offset, 2), out var inner) && inner > 0
            ? inner
            : null;
    }
}
