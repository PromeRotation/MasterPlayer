using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class QitanaCatalog
{
    internal const uint TERRITORY = 823;
    internal static bool Owns(uint oid) => oid is 0x2A40 or 0x27AF or 0x27B0 or 0x27B1;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2A40, "Ronkan Dreamer", p => p.Z > 550 ? QitanaArenas.DreamerFirst(p.Y) : QitanaArenas.DreamerSecond(p.Y),
        [
            new(17223, 0x2A40, "WrathOfTheRonka", null),
            new(17433, 0x2A40, "RonkanFire", null, "单体伤害"),
            new(17387, 0x2A40, "RonkanAbyss", new CircleTemplate(6)),
            new(15923, 0x28E3, "BurningBeam", RayRectangleTemplate.Symmetric(4, 15)),
        ], [872, 17949, 15918, 15916, 15917]),
        new(0x27AF, "Lozatl", p => QitanaArenas.Lozatl(p.Y),
        [
            new(15497, 0x27AF, "Stonefist", null, "死刑"),
            new(15498, 0x27AF, "SunToss", new CircleTemplate(5)),
            new(15499, 0x27AF, "LozatlsScorn", null, "全屏伤害"),
            new(15501, 0x27AF, "HeatUpLeft", null),
            new(15502, 0x27AF, "HeatUpRight", null),
            new(15504, 0x27AF, "LozatlsFuryRight", RayRectangleTemplate.Symmetric(20, 60)),
            new(15503, 0x27AF, "LozatlsFuryLeft", RayRectangleTemplate.Symmetric(20, 60)),
        ], [872, 15500, 15725]),
        new(0x27B0, "Batsquatch", p => QitanaArenas.Batsquatch(p.Y),
        [
            new(15505, 0x27B0, "RipperFang", null, "死刑"),
            new(15506, 0x27B0, "Soundwave", null, "全屏伤害"),
            new(15507, 0x27B0, "Subsonics", null, "全屏伤害"),
            new(15510, 0x233C, "FallingRock", new CircleTemplate(3)),
            new(15509, 0x233C, "FallingRockSmall", new CircleTemplate(2)),
            new(15511, 0x233C, "FallingBoulder", new CircleTemplate(4)),
            new(15512, 0x233C, "Towerfall", null),
        ], [870, 15508]),
        new(0x27B1, "Eros", p => new RectangleGeometry(new(17, p.Y, -538), 0, 29, 39),
        [
            new(15513, 0x27B1, "Rend", null, "死刑"),
            new(15514, 0x27B1, "HoundOutOfHeaven", null),
            new(15515, 0x27B1, "Glossolalia", null, "全屏伤害"),
            new(15516, 0x27B1, "ViperPoison", null),
            new(15518, 0x233C, "ViperPoisonPatterns", new CircleTemplate(6)),
            new(15517, 0x233C, "ViperPoisonBait", new CircleTemplate(6), PartyAoe: "分散"),
            new(17168, 0x27B1, "Inhale", null),
            new(15520, 0x27B1, "HeavingBreath", null),
            new(16923, 0x233C, "HeavingBreathVisual", null),
            new(15524, 0x27B1, "ConfessionOfFaithSides", null),
            new(15521, 0x27B1, "ConfessionOfFaithCenterVisual", null),
            new(15526, 0x233C, "ConfessionOfFaithLeft", new FanTemplate(60, MathF.PI / 3)),
            new(15527, 0x233C, "ConfessionOfFaithRight", new FanTemplate(60, MathF.PI / 3)),
            new(15522, 0x233C, "ConfessionOfFaithCenter", new FanTemplate(60, MathF.PI / 3)),
            new(15525, 0x233C, "ConfessionOfFaithStack", null, "全屏伤害"),
            new(15523, 0x233C, "ConfessionOfFaithSpread", new CircleTemplate(5), PartyAoe: "分散"),
        ], [872, 15519, 17079, 17080]),
    ];
}
