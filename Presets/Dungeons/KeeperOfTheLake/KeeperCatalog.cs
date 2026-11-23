using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class KeeperCatalog
{
    internal const uint TERRITORY = 1063;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x3927, "Einhander", p => KeeperArenas.Einhander(p.Y),
        [
            new(29273, 0x3927, "AeroBlast", null, "全屏伤害"),
            new(29271, 0x3927, "QuickFiringCannon", RayRectangleTemplate.Symmetric(4, 40)),
            new(29275, 0x3929, "CeruleumExplosion", new CircleTemplate(12)),
            new(29620, 0x3927, "HeavySwing", null, "死刑"),
            new(29272, 0x3927, "MiniCannon", new CircleTemplate(15)),
        ], [29270]),
        new(0xE68, "Magitek Gunship", p => KeeperArenas.Gunship(p.Y),
        [
            new(3389, 0xE68, "Flamethrower", new FanTemplate(18, MathF.PI * 2 / 3)),
            new(3392, 0xE7B, "CarpetBomb", new CircleTemplate(5)),
            new(1433, 0xE69, "DrillCannons", RayRectangleTemplate.Symmetric(5, 32.8f)),
            new(1435, 0xE69, "Overcharge", new FanTemplate(10.8f, MathF.PI * 2 / 3)),
        ], [3391, 3390, 3411]),
        new(0x392A, "Midgardsormr", p => KeeperArenas.Midgardsormr(p.Y),
        [
            new(29889, 0x392A, "Admonishment", RayRectangleTemplate.Symmetric(12, 40)),
            new(29888, 0x392A, "InnerTurmoil", new CircleTemplate(22)),
            new(29279, 0x392A, "PhantomOuterTurmoil", new PtDonutSectorTemplate(20.5f, 39, MathF.PI)),
            new(29278, 0x392A, "PhantomInnerTurmoil", new CircleTemplate(22)),
            new(29280, 0x392A, "PhantomAdmonishment", RayRectangleTemplate.Symmetric(12, 40)),
            new(29891, 0x392A, "Disgust", null, "全屏伤害"),
            new(29286, 0x233C, "AntipathyFirst", new CircleTemplate(6)),
            new(29287, 0x233C, "AntipathySecond", new DonutTemplate(6, 12)),
            new(29288, 0x233C, "AntipathyThird", new DonutTemplate(12, 20)),
            new(29281, 0x392A, "Animadversion", null, "全屏伤害"),
            new(29282, 0x392A, "AnimadversionEnrage", null, "全屏伤害"),
            new(29283, 0x392A, "AkhMorn", new CircleTemplate(6), PartyAoe: "分摊"),
            new(29602, 0x392A, "Condescension", null, "死刑"),
        ], [29277, 29285, 29290, 29284]),
    ];
}
