using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MalikahCatalog
{
    internal const uint TERRITORY = 836;
    internal static bool Owns(uint oid) => oid is 0x2679 or 0x267A or 0x267B;
    internal static bool IsStreak(uint action) => action is 15610 or 15887 or 15609 or 15886;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2679, "Greater Armadillo", p => MalikahArenas.Armadillo(p.Y),
        [
            new(15589, 0x2679, "StoneFlail", null, "死刑"),
            new(15594, 0x233C, "FallingRock", new CircleTemplate(4)),
            new(15590, 0x2679, "HeadToss", null, "全屏伤害"),
            new(15591, 0x2679, "RightRoundVisual", null),
            new(15593, 0x2679, "FlailSmash", new CircleTemplate(10)),
            new(15929, 0x233C, "Earthshake", new DonutTemplate(10, 20)),
            new(16776, 0x2683, "Rehydration", null, "打断或眩晕"),
        ], [872, 870, 15592]),
        new(0x267A, "Amphibious Talos", p => MalikahArenas.Talos(p.Y),
        [
            new(15595, 0x267A, "Efface", null, "死刑"),
            new(15597, 0x267A, "Wellbore", new CircleTemplate(15)),
            new(15598, 0x233C, "GeyserEruption", new CircleTemplate(8)),
            new(15596, 0x267A, "HighPressure", null, "全屏伤害"),
            new(15599, 0x267A, "SwiftSpillFirst", null),
        ], [872, 15600]),
        new(0x267B, "Storge", p => MalikahArenas.Storge(p.Y),
        [
            new(15601, 0x267B, "IntestinalCrank", null, "全屏伤害"),
            new(17914, 0x2BD6, "BreakingWheelWait", null),
            new(17913, 0x2BD6, "HereticForkWait", null),
            new(15605, 0x267B, "BreakingWheel", new DonutTemplate(5, 60)),
            new(15610, 0x267C, "BreakingWheelNail", new DonutTemplate(5, 60)),
            new(15887, 0x267B, "BreakingWheelLong", new DonutTemplate(5, 60)),
            new(15606, 0x267B, "CrystalNailVisual", null),
            new(15607, 0x267C, "CrystalNail", new CircleTemplate(5)),
            new(15927, 0x267B, "CensureWheel", null),
            new(15608, 0x267B, "CensureFork", null),
            new(15602, 0x267B, "HereticsFork", new DungeonCrossTemplate(60, 5)),
            new(15609, 0x267C, "HereticsForkNail", new DungeonCrossTemplate(60, 5)),
            new(15886, 0x267B, "HereticsForkLong", new DungeonCrossTemplate(60, 5)),
        ], [870, 15528, 16808]),
    ];
}
