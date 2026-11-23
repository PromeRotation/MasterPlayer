using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HardTrialCatalog
{
    internal const uint IFRIT = 292;
    internal const uint TITAN = 293;
    internal const uint GARUDA = 294;

    internal static bool IsTerritory(uint territory) => territory is IFRIT or TITAN or GARUDA;
    internal static bool Owns(uint oid, uint territory) => territory switch
    {
        IFRIT => oid == 0xD1, TITAN => oid == 0xF7, GARUDA => oid == 0xF2, _ => false,
    };

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        IFRIT => [new(0xD1, "Ifrit Hard", p => ArrTrialCatalog.IfritArena(p.Y),
        [
            new(1358, 0, "Eruption", new CircleTemplate(8)),
            new(1359, 0, "RadiantPlume", new CircleTemplate(8)),
            new(457, 0, "CrimsonCyclone", null),
        ], [451, 1353, 1354, 1355, 452, 1356, 1357])],
        GARUDA => [new(0xF2, "Garuda Hard", p => new CircleGeometry(new(0, p.Y, 0), 22),
        [
            new(1382, 0, "Slipstream", new FanTemplate(11.7f, MathF.PI / 2)),
            new(1384, 0, "MistralShriek", null),
            new(1390, 0, "MistralSong", null),
            new(1385, 0, "AerialBlast", null, "全屏伤害"),
            new(1386, 0, "GreatWhirlwind", new CircleTemplate(8)),
            new(1387, 0, "EyeOfTheStorm", new DonutTemplate(12, 25)),
        ], [870, 1379, 1380, 1381, 1388, 1389])],
        TITAN => [new(0xF7, "Titan Hard", p => new CircleGeometry(new(0, p.Y, 0), 25),
        [
            new(1363, 0, "WeightOfTheLand", new CircleTemplate(6)),
            new(1364, 0, "Landslide", RayRectangleTemplate.Symmetric(6, 40.25f)),
            new(1365, 0, "Geocrush", null),
            new(1052, 0, "Burst", null),
        ], [872, 1360, 1361, 1362, 1051, 645, 1477, 1366, 643])],
        _ => [],
    };
}
