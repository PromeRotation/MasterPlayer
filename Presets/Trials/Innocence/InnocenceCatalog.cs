using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class InnocenceCatalog
{
    internal const uint TERRITORY = 846;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x28FB, "Innocence", p => new CircleGeometry(new(100, p.Y, 100), 20),
        [
            new(16027, 0, "RealmRazer", null, "全屏伤害"),
            new(16030, 0, "ScoldsBridle", null, "全屏伤害"),
            new(16114, 0, "ExaltedPlumes", null, "全屏伤害"),
            new(16019, 0, "ExaltedWing", null, "全屏伤害"),
            new(16106, 0, "Shadowreaver", null, "全屏伤害"),
            new(16035, 0, "RighteousBolt", null),
            new(16029, 0, "Daybreak", new CircleTemplate(6)),
            new(16025, 0, "Enthrall", null, "背对视线"),
            new(16051, 0, "HolyTrinity", RayRectangleTemplate.Symmetric(4, 40)),
            new(16049, 0, "SoulAndBody", new PtDonutSectorTemplate(15, 20, MathF.PI * 2 / 9)),
            new(16050, 0, "SoulAndBody1", new PtDonutSectorTemplate(15, 20, MathF.PI * 2 / 9)),
            new(16056, 0, "Reprobation", RayRectangleTemplate.Symmetric(4, 21)),
            new(16075, 0, "ReprobationLong", RayRectangleTemplate.Symmetric(4, 42)),
            new(18064, 0, "Manacle", new CircleTemplate(6)),
            // The source selects the (action, radius) overload, not RaidwideCasts.
            new(18066, 0, "PhaseTwoRaidwides", new CircleTemplate(16065)),
            new(16062, 0, "GodRayCone", new FanTemplate(5, MathF.PI * 5 / 9)),
            new(16063, 0, "GodRayMedium", new PtDonutSectorTemplate(5, 10, MathF.PI * 5 / 9)),
            new(16064, 0, "GodRayOuter", new PtDonutSectorTemplate(10, 20, MathF.PI * 5 / 9)),
            new(16071, 0, "BeatificVision", RayRectangleTemplate.Symmetric(30, 45)),
        ], [16016, 16026, 16021, 870, 16031, 17175, 16028, 16023, 16017, 16022, 16197, 16018,
            16708, 16033, 16020, 16032, 16572, 16041, 16121, 16122, 16034, 16054, 16055,
            16053, 17072, 17073, 18065, 16065, 18184, 16060, 16061, 14588, 16190, 16070, 16068, 16069])
    ];
}
