using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class PraetoriumCatalog
{
    internal const uint TERRITORY = 1044;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x3872, "Mark II Magitek Colossus", p => PraetoriumArenas.Colossus(p.Y),
        [
            new(28474, 0x3872, "CeruleumVent", null, "全屏伤害"),
            new(28469, 0x233C, "IronKissAlphaInner", new CircleTemplate(6)),
            new(28470, 0x233C, "IronKissAlphaOuter", new CircleTemplate(6)),
            new(28472, 0x233C, "IronKissBeta", new CircleTemplate(5), PartyAoe: "分散"),
            new(28473, 0x3872, "GrandSword", new FanTemplate(25, MathF.PI / 2)),
        ], [28468, 28471]),
        new(0x3873, "Nero tol Scaeva", p => PraetoriumArenas.NeroStarting(p.Y),
        [
            new(28482, 0x3873, "IronUprising", new FanTemplate(7, MathF.PI * 2 / 3)),
            new(28483, 0x3873, "SpineShatter", null, "死刑"),
            new(28476, 0x3873, "AugmentedSuffering", null, "击退12码：靠近场心"),
            new(28477, 0x3873, "AugmentedShatter", new CircleTemplate(6), PartyAoe: "分摊"),
            new(28478, 0x3873, "AugmentedUprising", new FanTemplate(45, MathF.PI / 2)),
            new(28481, 0x3873, "WheelOfSuffering", new CircleTemplate(7)),
        ], [28479]),
        new(0x3875, "Gaius van Baelsar", p => new RectangleGeometry(new(-562, p.Y, 220), 0, 29, 39),
        [
            new(28492, 0x233C, "HandOfTheEmpire", new CircleTemplate(5), PartyAoe: "分散"),
            new(28493, 0x3875, "FestinaLente", new CircleTemplate(6), PartyAoe: "分摊"),
            new(28494, 0x3875, "Innocence", null, "死刑"),
            new(28495, 0x3875, "HorridaBella", null, "全屏伤害"),
            new(29052, 0x233C, "Ductus", new CircleTemplate(8)),
            new(28498, 0x3877, "Heirsbane", null, "单体伤害"),
        ], [28484, 28491, 29051, 28487, 28488, 29779]),
    ];
}
