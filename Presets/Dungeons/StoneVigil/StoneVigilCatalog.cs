using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class StoneVigilCatalog
{
    internal const uint TERRITORY = 1042;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x5B5, "Chudo-Yudo", p => StoneVigilArenas.ChudoYudo(p.Y),
        [
            new(902, 0x5B5, "LionsBreath", new FanTemplate(10.24f, MathF.PI * 2 / 3)),
            new(903, 0x5B5, "Swinge", new FanTemplate(44.24f, MathF.PI / 3)),
        ], [901]),
        new(0x38C7, "Koshchei", p => StoneVigilArenas.Koshchei(p.Y),
        [
            new(28732, 0x38C7, "SpikedTail", null, "死刑"),
            new(29053, 0x38C7, "SonicStorm", new CircleTemplate(6)),
        ], [28730, 28731]),
        new(0x5AF, "Isgebind", p => StoneVigilArenas.Isgebind(p.Y),
        [
            new(1025, 0x5AF, "RimeWreath", null, "全屏伤害"),
            new(1022, 0x5AF, "FrostBreath", new FanTemplate(27, MathF.PI * 2 / 3)),
            new(1023, 0x5AF, "SheetOfIce", new CircleTemplate(5)),
            new(1024, 0x233C, "SheetOfIce2", new CircleTemplate(5)),
            new(1026, 0x5AF, "Cauterize", RayRectangleTemplate.Symmetric(20, 48)),
        ], [1027]),
    ];
}
