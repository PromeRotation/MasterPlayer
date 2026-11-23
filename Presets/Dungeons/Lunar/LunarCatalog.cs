using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class LunarCatalog
{
    internal const uint TERRITORY = 1164;
    internal static bool Owns(uint oid) => oid is 0x3FE2 or 0x4022 or 0x4042;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3FE2, "DarkElf", p => new RectangleGeometry(new(-401, p.Y, -231), 0, 31, 31),
        [
            new(35205, 0, "RuinousConfluence", null),
            new(35254, 0, "RuinousHex1", null),
            new(34789, 0, "RuinousHex2", null),
            new(34787, 0, "Explosion", RayRectangleTemplate.Symmetric(8, 8)),
            new(34788, 0, "VoidDarkII", new CircleTemplate(6), PartyAoe: "分散"),
            new(35204, 0, "StaffSmite", null, "死刑"),
            new(34782, 0, "AbyssalOutburst", null, "全屏伤害"),
        ], [872, 34777, 34783, 34779, 34780, 34778, 34781]),
        new(0x4022, "DamcyanAntlion", p => new RectangleGeometry(new(0, p.Y, 60), 0, 39, 50),
        [
            new(34813, 0, "Sandblast", null, "全屏伤害"),
            new(34819, 0, "Landslip", null, "定向击退20码"),
            new(35871, 0, "AntlionMarchTelegraph", null),
            new(34822, 0, "EarthenGeyser", new CircleTemplate(10), PartyAoe: "分摊"),
            new(34443, 0, "PoundSand", new CircleTemplate(12)),
        ], [872, 34818, 34824, 34816, 34817, 34820, 34821]),
        new(0x4042, "Durante", p => new CircleGeometry(new(0, p.Y, -422), 22.5f),
        [
            new(35010, 0, "ArcaneEdge", null, "全屏伤害"),
            new(35011, 0, "OldMagic", null, "全屏伤害"),
            new(35905, 0, "Contrapasso", null, "全屏伤害"),
            new(36058, 0, "DuplicitousBatteryTelegraph", null),
            new(35006, 0, "Explosion1", new CircleTemplate(11)),
            new(35005, 0, "Explosion2", new CircleTemplate(9)),
            new(35882, 0, "FallenGrace", new CircleTemplate(6), PartyAoe: "分散"),
            new(35009, 0, "HardSlash", new FanTemplate(50, MathF.PI / 2)),
            new(36056, 0, "TwilightPhase", RayRectangleTemplate.Symmetric(20, 60)),
            new(35002, 0, "DarkImpact", new CircleTemplate(25)),
            new(34995, 0, "DeathsJourneyCircle", new CircleTemplate(8)),
            new(34996, 0, "DeathsJourneyCone", new FanTemplate(30, MathF.PI / 6)),
        ], [870, 34991, 36057, 34994, 35003, 35004, 14588, 35007, 35008, 36055, 34997, 34998, 35001, 35872]),
    ];
}
