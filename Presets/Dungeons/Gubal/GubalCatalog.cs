using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class GubalCatalog
{
    internal const uint TERRITORY = 1109;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xE82, "Demon Tome", p => GubalArenas.DemonTome(p.Y),
        [
            new(3519, 0xE82, "Repel", null, "向前击退20码"),
            new(3520, 0xED6, "LiquefyCenter", RayRectangleTemplate.Symmetric(8, 57.84f)),
            new(3521, 0xED6, "LiquefySides", RayRectangleTemplate.Symmetric(7, 57.84f)),
            new(4818, 0xED6, "Disclosure", RayRectangleTemplate.Symmetric(22, 20.5f)),
        ], [3522, 3517, 3961, 3518, 3989, 3926]),
        new(0xE83, "Byblos", p => GubalArenas.Byblos(p.Y),
        [
            new(4163, 0xE83, "HeadDown", null, "冲锋点名：避开队友"),
            new(5058, 0x10B7, "DeathRay", RayRectangleTemplate.Symmetric(3, 24.2f)),
            new(4165, 0xE83, "TailSmash", new FanTemplate(12, MathF.PI / 2)),
        ], [4159, 4164, 4167, 4158, 4168]),
        new(0xE84, "The Everliving Bibliotaph", p => GubalArenas.Bibliotaph(p.Y),
        [
            new(3780, 0xEC5, "VoidSpark", null),
            new(3524, 0xE84, "VoidCall", null, "按亮灯人数踩塔"),
            new(3528, 0xE84, "DeepDarkness", new DonutTemplate(12, 25)),
            new(3529, 0xE84, "MagicBurst", new CircleTemplate(15)),
            new(3535, 0xE86, "VoidBlizzardIII", new CircleTemplate(5)),
            new(3531, 0xEA5, "AbyssalCharge", RayRectangleTemplate.Symmetric(4, 41)),
        ], [3527, 3526, 3532, 3530, 3533, 3537, 3824, 3534, 3536]),
    ];
}
