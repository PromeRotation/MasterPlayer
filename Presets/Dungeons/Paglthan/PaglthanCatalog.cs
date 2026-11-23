using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class PaglthanCatalog
{
    internal const uint TERRITORY = 938;
    internal static bool Owns(uint oid) => oid is 0x3169 or 0x32FE or 0x316A;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3169, "Amhuluk", p => PaglthanArenas.Amhuluk(p.Y),
        [
            new(23630, 0x3169, "CriticalRip", null, "死刑"),
            new(23629, 0x3169, "ElectricBurst", null, "全屏伤害"),
            new(24773, 0x3169, "WideBlaster", new FanTemplate(26, 2 * MathF.PI / 3)),
        ], [870, 23633, 23627, 23628, 23632, 23634, 23635, 23631]),
        new(0x32FE, "Magitek Fortress", p => FortressArena(p.Y, false),
        [
            new(23701, 0x233C, "TwoTonzeMagitekMissile", new CircleTemplate(12)),
            new(23703, 0x233C, "GroundToGroundBallistic", null, "南北方向准备击退"),
            new(23710, 0x31AC, "DefensiveReaction", null, "全屏伤害"),
            new(23708, 0x31B0, "Aethershot", new CircleTemplate(6)),
            new(23705, 0x31AE, "Exhaust", RayRectangleTemplate.Symmetric(7, 40)),
        ], [870, 23706, 23700, 10758, 23704]),
        new(0x316A, "Lunar Bahamut", p => PaglthanArenas.LunarBahamut(p.Y),
        [
            new(24667, 0x316B, "Upburst", new CircleTemplate(2)),
            new(23368, 0x316B, "BigBurst", new CircleTemplate(9)),
            new(23385, 0x316A, "PerigeanBreath", new FanTemplate(30, MathF.PI / 2)),
            new(23381, 0x316A, "AkhMornFirst", null, "四次分摊"),
            new(23373, 0x233C, "MegaflareSpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(23374, 0x233C, "MegaflareAOE", new CircleTemplate(6)),
            new(23378, 0x316A, "MegaflareDive", RayRectangleTemplate.Symmetric(12, 41)),
            new(23370, 0x233C, "LunarFlareBig", new CircleTemplate(11)),
            new(23371, 0x233C, "LunarFlareSmall", new CircleTemplate(6)),
            new(23383, 0x316A, "Gigaflare", null, "全屏伤害"),
            new(23384, 0x316A, "Flatten", null, "延迟死刑"),
        ], [23620, 23367, 23382, 23372, 23375, 23377, 23376, 23369]),
    ];

    internal static IGeometryShape FortressArena(float y, bool core) => core
        ? new RectangleGeometry(new(-175, y, 8.5f), 0, 14, 14)
        : new RectangleGeometry(new(-175, y, 43), 0, 29, 29);
}
