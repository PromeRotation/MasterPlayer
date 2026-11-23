using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class WorqorCatalog
{
    internal const uint TERRITORY = 1193;
    internal static bool Owns(uint oid) => oid is 0x4159 or 0x415D or 0x415F;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4159, "RyoqorTerteh", p => WorqorArenas.RyoqorStarting(p.Y),
        [
            new(36280, 0, "FrostingFracas", null, "全屏伤害"),
            new(36270, 0, "IceScream", RayRectangleTemplate.Symmetric(20, 20)),
            new(36271, 0, "FrozenSwirl", new CircleTemplate(15)),
            new(36278, 0, "SnowBoulder", RayRectangleTemplate.Symmetric(6, 50)),
            new(36281, 0, "SparklingSprinkling", new CircleTemplate(5), PartyAoe: "分散"),
        ], [872, 36279, 36265, 36266, 36272, 36275, 36713]),
        new(0x415D, "Kahderyor", p => WorqorArenas.Kahderyor(p.Y),
        [
            new(36282, 0, "WindUnbound", null, "全屏伤害"),
            new(36153, 0, "CrystallineCrush", null),
            new(36296, 0, "WindShot", null),
            new(36295, 0, "EarthenShot", new CircleTemplate(6), PartyAoe: "分散"),
            new(36298, 0, "SeedCrystals", new CircleTemplate(6), PartyAoe: "分散"),
            new(36290, 0, "CrystallineStorm", RayRectangleTemplate.Symmetric(2, 50)),
            new(36297, 0, "EyeOfTheFierce", null, "背对视线"),
            new(36293, 0, "StalagmiteCircle", new CircleTemplate(15)),
            new(36294, 0, "CyclonicRing", new DonutTemplate(8, 40)),
        ], [872, 36285, 36284, 36300, 36283, 36299, 36286, 36291, 36287, 36288, 36289]),
        new(0x415F, "Gurfurlur", p => new RectangleGeometry(new(-54, p.Y, -195), 0, 49, 49),
        [
            new(36375, 0, "HeavingHaymaker", null, "全屏伤害"),
            new(36302, 0, "LithicImpact", RayRectangleTemplate.Symmetric(4, 4)),
            new(36307, 0, "GreatFlood", null, "全屏伤害"),
            new(36303, 0, "Allfire1", RayRectangleTemplate.Symmetric(10, 10)),
            new(36304, 0, "Allfire2", RayRectangleTemplate.Symmetric(10, 10)),
            new(36305, 0, "Allfire3", RayRectangleTemplate.Symmetric(10, 10)),
            new(36306, 0, "VolcanicDrop", new CircleTemplate(6), PartyAoe: "分散"),
            new(36320, 0, "EnduringGlory", null, "全屏伤害"),
            new(36310, 0, "Windswrath1", null, "全屏伤害"),
            new(39074, 0, "Windswrath2", null, "全屏伤害"),
        ], [872, 36269, 36301, 36315, 36313, 36314, 39260, 36319, 36321, 36322, 36311]),
    ];
}
