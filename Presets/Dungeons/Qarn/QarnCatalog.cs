using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class QarnCatalog
{
    internal const uint TERRITORY = 1267;
    internal const uint DOOM = 5187;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x477A, "Teratotaur", p => QarnArenas.Teratotaur(p.Y),
        [
            new(42231, 0x477A, "Triclip", null, "死刑"),
            new(42232, 0x477A, "Mow", new FanTemplate(8.25f, MathF.PI * 2 / 3)),
            new(42233, 0x477A, "FrightfulRoar", new CircleTemplate(6)),
            new(42229, 0x477A, "MortalRay", null, "可眩晕打断死宣；中死宣后前往发光平台"),
            new(42230, 0x477B, "FinalSting", null, "优先击杀蜜蜂，阻止终极针"),
        ], [870, 871]),
        new(0x477C, "Temple Guardian", p => QarnArenas.TempleGuardian(p.Y),
        [
            new(42234, 0x477C, "BoulderClap", new FanTemplate(14.2f, MathF.PI * 2 / 3)),
            new(42235, 0x477C, "TrueGrit", new FanTemplate(14.2f, MathF.PI * 2 / 3)),
            new(42236, 0x477C, "Rockslide", RayRectangleTemplate.Symmetric(8, 16.2f)),
            new(42238, 0x477C, "Obliterate", null, "全屏伤害"),
        ], [42237]),
        new(0x477E, "Adjudicator", p => new CircleGeometry(new(236, p.Y, 0), 20),
        [
            new(42245, 0x477E, "LoomingJudgement", null, "死刑"),
            new(42247, 0x477E, "CreepingDarkness", null, "全屏伤害"),
            new(42248, 0x477E, "DarkII", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(42246, 0x477E, "Dark", new CircleTemplate(5)),
            new(42244, 0x477F, "VergeLine", RayRectangleTemplate.Symmetric(4, 60.6f)),
            new(42241, 0x4780, "VergePulse", RayRectangleTemplate.Symmetric(4, 60.6f)),
            new(42242, 0x477F, "SelfDestruct", null, "全屏伤害：尽快击杀秘银柱"),
            new(42242, 0x4780, "SelfDestruct", null, "全屏伤害：尽快击杀秘银柱"),
        ], [42243, 42239, 30506, 42240]),
    ];
}
