using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AurumValeCatalog
{
    internal const uint TERRITORY = 1331;
    internal static bool IsFruit(uint oid) => oid is 0x1E8ED9 or 0x1E8ED7 or 0x1E8ED8 or 0x1E878A
        or 0x1E8EE3 or 0x1E8EE1 or 0x1E8EDE or 0x1E8EDF or 0x1E8EE0 or 0x1E8EE2;
    internal static bool FruitFor(uint boss, uint oid) => boss switch
    {
        0x4B0C => oid is 0x1E8ED9 or 0x1E8ED7 or 0x1E8ED8 or 0x1E878A,
        0x4B0E => oid is 0x1E8EE3 or 0x1E8EE1 or 0x1E8EDE or 0x1E8EDF or 0x1E8EE0 or 0x1E8EE2,
        _ => false,
    };

    internal static DungeonDefinition[] Create() =>
    [
        new(0x4B0C, "Locksmith", p => AurumValeArenas.Locksmith(p.Y),
        [
            new(45742, 0x4B0C, "HundredLashings", null, "死刑"),
            new(45741, 0x4B0C, "GoldDust", new CircleTemplate(8)),
        ], [45214]),
        new(0x4B0D, "Coincounter", p => AurumValeArenas.Coincounter(p.Y),
        [
            new(45745, 0x4B0D, "TenTonzeSwipe", new FanTemplate(10, MathF.PI * 2 / 3)),
            new(45744, 0x4B0D, "HundredTonzeSwipe", new FanTemplate(10, MathF.PI * 2 / 3)),
            new(45743, 0x4B0D, "Glower", RayRectangleTemplate.Symmetric(7, 21)),
            new(45747, 0x4B0D, "EyeOfTheBeholder", new PtDonutSectorTemplate(8, 19, MathF.PI * 1.5f)),
            new(45746, 0x4B0D, "HundredTonzeSwing", new CircleTemplate(12)),
        ], []),
        new(0x4B0E, "Miser's Mistress", p => AurumValeArenas.Mistress(p.Y),
        [
            new(45997, 0x4B0E, "VineProbe", null, "死刑"),
            new(45996, 0x4B0E, "BadBreath", new FanTemplate(15.85f, MathF.PI * 2 / 3)),
            new(45998, 0x4B0E, "BurrBurrow", null, "全屏伤害"),
        ], [45999, 46000, 46001]),
    ];
}
