using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AmaurotCatalog
{
    internal const uint TERRITORY = 838;
    internal static bool Owns(uint oid) => oid is 0x27B6 or 0x27B8 or 0x27C1;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x27B6, "The First Beast", p => new RectangleGeometry(new(-80, p.Y, 82), 0, 39, 39),
        [
            new(15566, 0x27B6, "VenomousBreath", new FanTemplate(9, MathF.PI * 2 / 3)),
            new(15558, 0x233C, "MeteorRain", new CircleTemplate(6)),
            new(15562, 0x233C, "TheFallingSky", new CircleTemplate(10)),
            new(15563, 0x27B6, "TheFinalSky", null, "躲到陨石后"),
            new(17108, 0x29DC, "CosmicKiss", new CircleTemplate(10)),
            new(15564, 0x18D6, "Towerfall", RayRectangleTemplate.Symmetric(40, 35)),
            new(15565, 0x27B6, "Earthquake", new CircleTemplate(10)),
            new(13642, 0x18D6, "TheBurningSky", new CircleTemplate(6)),
            new(15560, 0x233C, "TheBurningSkySpread", new CircleTemplate(6), PartyAoe: "分散"),
        ], [870, 15556, 15561, 17110, 15559]),
        new(0x27B8, "Bellwether", p => AmaurotArenas.Bellwether(p.Y),
        [
            new(15567, 0x27B8, "ShrillShriek", null, "全屏伤害"),
            new(15571, 0x2879, "Aetherspike", RayRectangleTemplate.Symmetric(8, 40)),
            new(15572, 0x18D6, "Comet", new CircleTemplate(4)),
            new(16765, 0x27BC, "SicklyInferno", new CircleTemplate(5)),
            new(15568, 0x27B8, "BurstEnrage", null, "狂暴"),
        ], [870, 6499, 18013, 15570, 15573, 15588, 15569, 15919]),
        new(0x27C1, "Therion", p => AmaurotArenas.Therion(0, p.Y),
        [
            new(15587, 0x27C1, "ShadowWreck", null, "全屏伤害"),
            new(15575, 0x27C1, "Apokalypsis", null),
            new(15578, 0x27C1, "TherionCharge", new CircleTemplate(20)),
            new(17107, 0x233C, "DeathlyRayTherion", null),
            new(15586, 0x233C, "Misfortune", new CircleTemplate(6)),
        ], [15574, 15577, 15579, 16786, 15582, 16785, 15580, 15581, 15583, 15585]),
    ];
}
