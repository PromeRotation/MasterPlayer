using System;
using System.Collections.Generic;
using ECommons.DalamudServices;

namespace MasterPlayer.Dodge;

public enum AoeShapeKind
{
    Circle,
    Donut,
    Fan,
    Rectangle,
}

// Param1/Param2 随 Kind 变化：Circle=半径/-；Donut=内半径/外半径；Fan=半径/总开角(弧度)；Rectangle=宽/长。
// ExtraDelaySeconds 在读条时长之外追加的判定延迟（部分技能判定晚于读条结束）。
public sealed record AoeShapeDescriptor(AoeShapeKind Kind, float Param1, float Param2, bool FollowCaster, float ExtraDelaySeconds);

public static class AoeShapeDatabase
{
    // 占位示例条目：以下 ActionId 与参数均未核对真实副本数据，需后续按副本逐个扩充。
    private static readonly Dictionary<uint, AoeShapeDescriptor> KnownShapes = new()
    {
        [20001u] = new AoeShapeDescriptor(AoeShapeKind.Circle, 8f, 0f, false, 0f),
        [20002u] = new AoeShapeDescriptor(AoeShapeKind.Fan, 20f, MathF.PI * 2f / 3f, true, 0f),
        [20003u] = new AoeShapeDescriptor(AoeShapeKind.Rectangle, 6f, 40f, true, 0.3f),
    };

    public static bool TryGetShape(uint actionId, out AoeShapeDescriptor? descriptor)
    {
        if (KnownShapes.TryGetValue(actionId, out descriptor))
        {
            return true;
        }

        descriptor = InferFromActionSheet(actionId);
        return descriptor is not null;
    }

    // Lumina Action 表兜底推断：CastType 2=圆形 3=扇形 4=直线矩形 5=月环；
    // EffectRange=半径/边长，XAxisModifier=扇形总开角(度)/矩形宽/月环内半径；TargetArea=false 表示以施法者为中心。
    private static AoeShapeDescriptor? InferFromActionSheet(uint actionId)
    {
        try
        {
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
            if (!row.HasValue)
            {
                return null;
            }

            var action = row.Value;
            float effectRange = action.EffectRange;
            float xAxisModifier = action.XAxisModifier;
            if (effectRange <= 0f)
            {
                return null;
            }

            switch (action.CastType)
            {
                case 2:
                    return new AoeShapeDescriptor(AoeShapeKind.Circle, effectRange, 0f, !action.TargetArea, 0f);
                case 3:
                {
                    var angleRadians = (xAxisModifier > 0f ? xAxisModifier : 120f) * MathF.PI / 180f;
                    return new AoeShapeDescriptor(AoeShapeKind.Fan, effectRange, MathF.Min(angleRadians, MathF.PI * 2f), true, 0f);
                }
                case 4:
                    return new AoeShapeDescriptor(AoeShapeKind.Rectangle, xAxisModifier > 0f ? xAxisModifier : 4f, effectRange, true, 0f);
                case 5 when xAxisModifier > 0f && xAxisModifier < effectRange:
                    return new AoeShapeDescriptor(AoeShapeKind.Donut, xAxisModifier, effectRange, !action.TargetArea, 0f);
                case 5:
                    return new AoeShapeDescriptor(AoeShapeKind.Circle, effectRange, 0f, !action.TargetArea, 0f);
                default:
                    return null;
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] 从 Lumina 推断 AOE 形状失败 (ActionId={actionId})");
            return null;
        }
    }
}
