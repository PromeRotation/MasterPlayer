using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SeiryuCatalog
{
    internal const uint TERRITORY = 824;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x25F4, "Seiryu", p => FourLordsArenas.SeiryuPhase1(p.Y),
        [
            new(15390, 0, "HundredTonzeSwing", new CircleTemplate(16)),
            new(14336, 0, "DragonsWake", null, "全屏伤害"),
            new(14334, 0, "FifthElement", null, "全屏伤害"),
            new(14342, 0, "FortuneBladeSigil", RayRectangleTemplate.Symmetric(4, 100)),
            new(14333, 0, "InfirmSoul", null),
            new(15391, 0, "Kanabo", new FanTemplate(45, MathF.PI / 3)),
            new(14355, 0, "YamaKagura", RayRectangleTemplate.Symmetric(6, 60)),
            new(14344, 0, "Handprint", new FanTemplate(40, MathF.PI)),
            new(14345, 0, "ForceOfNature2", new CircleTemplate(5)),
            new(14349, 0, "CoursingRiver", null),
            new(14346, 0, "ForceOfNature1", null),
            new(14281, 0, "StrengthOfSpirit", null),
            new(14855, 0, "OnmyoSigil", null),
            new(14856, 0, "SerpentEyeSigil", null),
        ], [870, 872, 14276, 14293, 14319, 15388, 15389, 14325, 14350, 14626, 14627, 14282,
            14347, 14348, 14277, 15490, 15474, 14352, 14353, 14354, 14316, 15392, 14305, 14300,
            14340, 15397, 14988, 14989, 14338, 14339, 14285, 14286, 14287, 14288, 14309, 14310, 14343, 14313])
    ];
}
