using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class BurnCatalog
{
    internal const uint TERRITORY = 1173;
    internal static bool Owns(uint oid) => oid is 0x2419 or 0x23AA or 0x2431;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2419, "Hedetet", p => BurnArenas.Hedetet(p.Y),
        [
            new(12691, 0x2419, "CrystalNeedle", null, "死刑"),
            new(12692, 0x2419, "Hailfire", null, "躲到水晶后"),
            new(12689, 0x2419, "Shardfall", null, "躲到水晶后"),
            new(12696, 0x241A, "ResonantFrequency", new CircleTemplate(6)),
            new(12695, 0x241A, "CrystallineFracture", new CircleTemplate(3)),
            new(12690, 0x2419, "Dissonance", new DonutTemplate(5, 40)),
        ], [870, 33212, 12694, 12693, 12697]),
        new(0x23AA, "Defective Drone", p => new RectangleGeometry(new(0, p.Y, -70), 0, 20, 19),
        [
            new(11635, 0x23AA, "AetherochemicalFlame", null, "全屏伤害"),
            new(11637, 0x23AA, "FullThrottle", null),
            new(11640, 0x23AC, "AditDriver", RayRectangleTemplate.Symmetric(6, 33)),
            new(11634, 0x23AA, "AetherochemicalCoil", null, "死刑"),
        ], [872, 11636, 13526, 13529, 13525, 11638, 11639]),
        new(0x2431, "Mist Dragon", p => new CircleGeometry(new(-300, p.Y, -400), 19.5f),
        [
            new(12619, 0x2431, "RimeWreath", null, "全屏伤害"),
            new(12613, 0x233C, "FogPlumeCircle", new CircleTemplate(6)),
            new(12614, 0x233C, "FogPlumeCross", null),
            new(12609, 0x2431, "ColdFogVisual", null),
            new(12617, 0x233C, "Cauterize", null),
            new(12618, 0x2431, "Touchdown", new CircleTemplate(15)),
        ], [870, 12620, 12612, 12608, 12610, 12611, 12621, 12615, 12616]),
    ];
}
