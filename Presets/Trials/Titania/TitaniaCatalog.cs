using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TitaniaCatalog
{
    internal const uint TERRITORY = 845;
    internal static bool IsAdd(uint oid) => oid is 0x27F0 or 0x27F4 or 0x27EE or 0x27F2 or 0x27EF or 0x27F3 or 0x2878 or 0x27F1;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x27ED, "Titania", p => new RectangleGeometry(new(100, p.Y, 100), 0, 40, 40),
        [
            new(15708, 0, "BrightSabbath", null, "全屏伤害"),
            new(15710, 0, "PhantomRuneDonut", new DonutTemplate(5, 60)),
            new(15709, 0, "PhantomRuneCircle", new CircleTemplate(10)),
            new(15694, 0, "FrostRune", new CircleTemplate(10)),
            new(16259, 0, "GentleBreeze", RayRectangleTemplate.Symmetric(8, 60)),
            new(15701, 0, "LeafStorm", new FanTemplate(50, MathF.PI / 9)),
            new(15875, 0, "LeafStormGiant", new FanTemplate(50, MathF.PI / 9)),
            new(15704, 0, "PucksRebukeCircle", new CircleTemplate(5)),
            new(15702, 0, "PucksCaprice", null, "全屏伤害"),
            new(15697, 0, "BeingMortal", null, "全屏伤害"),
            new(15698, 0, "Pease", new CircleTemplate(6)),
            new(16927, 0, "Uplift", null),
            new(15668, 0, "Peasebomb", null),
            new(15789, 0, "WarAndPease", null),
            new(15703, 0, "PucksBreath", null),
            new(15705, 0, "PucksRebukeKnockback", null),
            new(15687, 0, "FlameRune", null),
            new(15685, 0, "MistRune", null),
        ], [872, 15707, 17267, 15664, 15662, 15696, 15658, 15665, 15672, 15700, 15677,
            17888, 18059, 18058, 15678, 15679, 15682, 15666, 15699])
    ];
}
