using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CosmosCatalog
{
    internal const uint TERRITORY = 884;
    internal const float BLACK_FLAME_ROTATION = -.003f * MathF.PI / 180;
    internal static bool IsSeed(uint oid) => oid is >= 0x1EAE9E and <= 0x1EAEA1;
    internal static bool Owns(uint oid) => oid is 0x2C1A or 0x2C04 or 0x2C13;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2C1A, "Seeker of Solitude", p => new RectangleGeometry(new(0, p.Y, 187), 0, 41, 29),
        [
            new(18281, 0x2C1A, "Shadowbolt", null, "死刑"),
            new(18851, 0x2C1A, "ImmortalAnathema", null, "全屏伤害"),
            new(18852, 0x233C, "Tribulation", null),
            new(18287, 0x233C, "DarkShock", new CircleTemplate(6)),
            new(18282, 0x2C1A, "DarkPulse", null, "四人分摊"),
            new(18285, 0x233C, "DarkWell", new CircleTemplate(5), PartyAoe: "分散"),
        ], [18280, 18283, 18288, 18286, 18289, 18284, 18713]),
        new(0x2C04, "Leannan Sith", p => new RectangleGeometry(new(0, p.Y, -60), 0, 39, 39),
        [
            new(18203, 0x2C04, "StormOfColor", null, "死刑"),
            new(18204, 0x2C04, "OdeToLostLove", null, "全屏伤害"),
            new(18205, 0x2C04, "DirectSeeding", null, "搬运种子离开绿地"),
            new(18206, 0x2C04, "GardenersHymn", null),
            new(18211, 0x233C, "FarWind", new CircleTemplate(8)),
            new(18212, 0x233C, "FarWindSpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(18768, 0x2C04, "OdeToFallenPetals", new DonutTemplate(5, 60)),
            new(18209, 0x2C06, "IrefulWind", null, "顺风击退10码"),
        ], [870, 872, 18207, 18208, 18210]),
        new(0x2C13, "Lugus", p => new RectangleGeometry(new(0, p.Y, -340), 0, 49, 49),
        [
            new(18275, 0x2C13, "ScorchingLeft", new FanTemplate(40, MathF.PI)),
            new(18274, 0x2C13, "ScorchingRight", new FanTemplate(40, MathF.PI)),
            new(18268, 0x233C, "OtherworldlyHeat", new DungeonCrossTemplate(10, 2)),
            new(18276, 0x2C13, "CaptiveBolt", null, "死刑"),
            new(18273, 0x2C13, "FiresIre", new FanTemplate(20, MathF.PI / 2)),
            new(18277, 0x2C13, "CullingBlade", null, "全屏伤害"),
            new(18279, 0x233C, "Plummet", new CircleTemplate(3)),
        ], [870, 18269, 18267, 18265, 18266, 18270, 18272, 18271, 18278]),
    ];
}
