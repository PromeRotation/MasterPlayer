using MasterPlayer.Scripting.Visual;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Dodge;

/// <summary>
/// 危险区几何外扩：注册前按 DodgeDangerMargin 膨胀模板，让 AI 解算出的安全点与危险区边缘保持最小距离。
/// 多边形模板（Local/WorldPolygon）没有简单可靠的外扩方式，原样返回；未知子类同样原样返回。
/// </summary>
public static class DangerMarginInflator
{
    public static GeometryTemplate Inflate(GeometryTemplate template, float margin)
    {
        if (margin <= 0f)
        {
            return template;
        }

        return template switch
        {
            Mechanics.Hunts.HuntStackJoinTemplate join => join with { Radius = MathF.Max(0, join.Radius - margin) },
            Mechanics.Dungeons.DungeonCapsuleTemplate capsule => capsule with { Radius = capsule.Radius + margin },
            Mechanics.Dungeons.DungeonCrossTemplate cross => cross with { Length = cross.Length + margin, HalfWidth = cross.HalfWidth + margin },
            Mechanics.PilgrimsTraverse.Pt30BallSweepTemplate sweep => sweep with { Radius = sweep.Radius + margin },
            Mechanics.PilgrimsTraverse.PtDonutSectorTemplate sector => sector with
            {
                Inner = MathF.Max(0, sector.Inner - margin),
                Outer = sector.Outer + margin,
            },
            Mechanics.Crucible.ResolvedGeometryTemplate snapshot => snapshot.Inflate(margin),
            RotationOffsetTemplate rotated => rotated with { Inner = Inflate(rotated.Inner, margin) },
            CircleTemplate circle => new CircleTemplate(circle.Radius + margin),
            DonutTemplate donut => new DonutTemplate(MathF.Max(0f, donut.InnerRadius - margin), donut.OuterRadius + margin),
            FanTemplate fan => new FanTemplate(fan.Radius + margin, fan.AngleRadians),
            RectangleTemplate rect => new RectangleTemplate(rect.Width + margin * 2f, rect.Length + margin),
            RayRectangleTemplate ray => new RayRectangleTemplate(ray.MinLocalX - margin, ray.MaxLocalX + margin, ray.Length + margin),
            _ => template,
        };
    }
}
