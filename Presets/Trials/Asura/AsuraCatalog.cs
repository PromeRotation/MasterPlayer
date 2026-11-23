using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AsuraCatalog
{
    internal const uint TERRITORY = 1136;
    internal static IGeometryShape Arena(float y, bool reduced = false) =>
        new LocalPolygonGeometry(new(100, y, 100), 0, Enumerable.Range(0, 32).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * (Math.PI * 2 / 32));
            var radius = reduced ? 19.165f : 19.5f * 1.004839f;
            return new Vector2((float)sin * radius, (float)cos * radius);
        }));

    internal static DungeonDefinition[] Create() =>
    [
        new(0x409B, "Asura", p => Arena(p.Y),
        [
            new(36001, 0, "LowerRealm", null, "全屏伤害"),
            new(35990, 0, "Ephemerality", null, "全屏伤害"),
            new(35994, 0, "AsuriChakra", new CircleTemplate(5)),
            new(35995, 0, "Chakra1", new DonutTemplate(6, 8)),
            new(35996, 0, "Chakra2", new DonutTemplate(9, 11)),
            new(35998, 0, "Chakra3", new DonutTemplate(12, 14)),
            new(35997, 0, "Chakra4", new DonutTemplate(15, 17)),
            new(36000, 0, "CuttingJewel", null),
            new(35991, 0, "Laceration", null),
            new(35969, 0, "IconographyPedestalPurge", new CircleTemplate(10)),
            new(35970, 0, "PedestalPurge", new CircleTemplate(60)),
            new(35971, 0, "IconographyWheelOfDeincarnation", new DonutTemplate(8, 40)),
            new(35972, 0, "WheelOfDeincarnation", new DonutTemplate(48, 96)),
            new(35973, 0, "IconographyBladewise", RayRectangleTemplate.Symmetric(6, 50)),
            new(35974, 0, "Bladewise", RayRectangleTemplate.Symmetric(28, 100)),
            new(35993, 0, "Scattering", RayRectangleTemplate.Symmetric(6, 20)),
            new(36003, 0, "OrderedChaos", null),
            new(36020, 0, "MyriadAspects1", new FanTemplate(40, MathF.PI / 6)),
            new(36021, 0, "MyriadAspects2", new FanTemplate(40, MathF.PI / 6)),
            new(36011, 0, "KhadgaTelegraph1", null),
            new(36012, 0, "KhadgaTelegraph2", null),
            new(36013, 0, "KhadgaTelegraph3", null),
            new(35989, 0, "TheFaceOfDelightA", null),
            new(35986, 0, "TheFaceOfWrathB", null),
            new(35987, 0, "TheFaceOfDelightB", null),
            new(35988, 0, "TheFaceOfWrathC", null),
            new(35985, 0, "TheFaceOfDelightC", null),
            new(35984, 0, "TheFaceOfWrathA", null),
        ],
        [870, 35966, 35999, 36008, 36009, 35967, 35968, 36017, 36018, 36010, 35976,
            35977, 35978, 35979, 35980, 35981, 35982, 35983, 36014, 36022, 36015,
            36016, 36023, 36007, 36006, 36019, 35992, 36002]),
    ];
}
