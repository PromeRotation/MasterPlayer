using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ByakkoCatalog
{
    internal const uint TERRITORY = 746;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x20F7, "Byakko", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(10799, 0, "StormPulse", null, "全屏伤害"),
            new(10827, 0, "TheRoarOfThunder", null, "全屏伤害"),
            new(10797, 0, "HeavenlyStrike", null, "死刑"),
            new(10798, 0, "SweepTheLeg1", new FanTemplate(28.5f, MathF.PI * 1.5f)),
            new(10822, 0, "SweepTheLeg3", new DonutTemplate(5, 30)),
            new(10819, 0, "ImperialGuard", RayRectangleTemplate.Symmetric(5, 44.75f)),
            new(10796, 0, "FireAndLightning1", RayRectangleTemplate.Symmetric(20, 50)),
            new(10801, 0, "FireAndLightning2", RayRectangleTemplate.Symmetric(20, 50)),
            // 源 DistantClap(5,3) 无可命中区域；不把非法月环改成另一种机制。
            new(10800, 0, "DistantClap", null),
        ], [872, 870, 9681, 10212, 10793, 10818, 10824, 10215, 10811, 10209, 10829, 10210, 10806,
            10808, 10809, 10070, 10208, 10802, 10821, 10825, 10071, 9679, 9822, 10794, 10222, 10817, 10221, 10828])
    ];
}
