using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class WarriorOfLightCatalog
{
    internal const uint TERRITORY = 922;
    internal static DungeonDefinition[] Create() => [Definition(0x2DDD, "Warrior of Light"), Definition(0x2DDE, "Warrior of Light (Checkpoint)")];

    private static DungeonDefinition Definition(uint oid, string name) => new(oid, name,
        p => new RectangleGeometry(new(100, p.Y, 100), 0, 40, 40),
        [
            new(20263, 0, "TerrorUnleashed", null, "全屏伤害"),
            new(20266, 0, "SolemnConfiteor", new CircleTemplate(6)),
            new(20241, 0, "CoruscantSaberDonut", new DonutTemplate(5, 60)),
            new(20240, 0, "CoruscantSaberCircle", new CircleTemplate(10)),
            new(20243, 0, "ImbuedAbsoluteBlizzardIII", null),
            new(20299, 0, "ImbuedCoruscance", new CircleTemplate(10)),
            new(20300, 0, "ImbuedCoruscanceDonut", new DonutTemplate(5, 60)),
            new(21628, 0, "UltimateCrossover", null, "全屏伤害"),
            new(21297, 0, "Ascendance", null, "全屏伤害"),
            new(20258, 0, "MeteoricBurstTower", null),
            new(20265, 0, "ElddragonDive", null, "全屏伤害"),
            new(20261, 0, "Cauterize", RayRectangleTemplate.Symmetric(20, 40)),
            new(20264, 0, "TheBitterEnd", null),
            new(21076, 0, "RadiantBraverCleave", new FanTemplate(60, MathF.PI * 92 / 180)),
            new(20251, 0, "RadiantMeteor", null),
            new(20254, 0, "BrimstoneEarth", new CircleTemplate(6)),
            new(20255, 0, "BrimstoneEarthPulse", null),
            new(20252, 0, "SuitonSan", null),
            new(20253, 0, "KatonSan", null),
            new(20260, 0, "FlareBreath", new FanTemplate(60, MathF.PI / 2)),
            new(20259, 0, "PerfectDecimation", new FanTemplate(60, MathF.PI / 4)),
        ],
        [872, 20291, 20270, 20239, 20242, 20269, 20238, 20290, 20293, 20593, 21298, 20611, 21379,
            21627, 20279, 20285, 20284, 21278, 20257, 20289, 20282, 20283, 20267, 20256,
            20237, 20276, 20246, 20247, 20277, 20294, 20829, 20248, 20249, 20278, 20250, 20262,
            20280, 20281, 20296, 20287, 21073, 20286, 21527]);
}
