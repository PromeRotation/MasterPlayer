using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SuzakuCatalog
{
    internal const uint TERRITORY = 810;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2459, "Suzaku", p => new CircleGeometry(new(100, p.Y, 100), 20),
        [
            new(12833, 0, "ScreamsOfTheDamned", null, "全屏伤害"),
            new(12852, 0, "SouthronStar", null, "全屏伤害"),
            new(12831, 0, "AshesToAshes", null, "全屏伤害"),
            new(12844, 0, "ScarletFever", null, "全屏伤害"),
            new(12848, 0, "RuthlessRefrain", null),
            new(12832, 0, "Cremate", null, "死刑"),
            new(12850, 0, "PhantomFlurryTankbuster", null, "死刑"),
            new(12851, 0, "PhantomFlurryAOE", new FanTemplate(41, MathF.PI)),
            new(12835, 0, "FleetingSummer", new FanTemplate(40, MathF.PI / 2)),
            new(12856, 0, "Hotspot", new FanTemplate(21, MathF.PI * 2 / 3)),
            new(12859, 0, "Swoop", RayRectangleTemplate.Symmetric(6, 55)),
            new(12854, 0, "WellOfFlame", RayRectangleTemplate.Symmetric(20, 41)),
        ], [871, 14065, 12863, 12849, 12860, 12853, 12834, 12836, 12861, 12840, 12855, 12846,
            12838, 12841, 12857, 12858, 13445, 13446, 12837])
    ];
}
