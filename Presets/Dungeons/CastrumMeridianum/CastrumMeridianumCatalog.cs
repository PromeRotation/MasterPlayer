using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CastrumMeridianumCatalog
{
    internal const uint TERRITORY = 1043;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x38CA, "Black Eft", p => CastrumMeridianumArenas.BlackEft(p.Y),
        [
            new(28773, 0x38CA, "HighPoweredMagitekRay", RayRectangleTemplate.Symmetric(4, 50)),
            new(28775, 0x38CA, "MagitekCannon", new CircleTemplate(6)),
            new(29268, 0x38CA, "IncendiarySupport", null, "全屏三连"),
        ], [28774]),
        new(0x38CD, "Magitek Vanguard F-1", p => CastrumMeridianumArenas.Vanguard(p.Y),
        [
            new(28779, 0x233C, "ThermobaricCharge", new CircleTemplate(20)),
            new(28781, 0x233C, "HyperchargeInner", new CircleTemplate(10)),
            new(28782, 0x233C, "HyperchargeOuter", new DonutTemplate(12, 30)),
            new(28784, 0x233C, "TargetedSupport", new CircleTemplate(5)),
            new(28785, 0x38CD, "CermetDrill", null, "死刑"),
            new(29146, 0x38CD, "Overcharge", new FanTemplate(11, MathF.PI * 2 / 3)),
        ], [28778, 28780, 28783]),
        new(0x38CE, "Livia sas Junius", p => CastrumMeridianumArenas.Livia(p.Y),
        [
            new(28798, 0x38CE, "AglaeaClimb", null, "死刑"),
            new(28033, 0x38CE, "ArtificialPlasma", null, "全屏伤害"),
            new(29163, 0x233C, "Roundhouse", new CircleTemplate(10)),
            new(28787, 0x233C, "RoundhouseDischarge", new CircleTemplate(8)),
            new(28794, 0x233C, "InfiniteReachDischarge", new CircleTemplate(8)),
            new(28793, 0x233C, "InfiniteReach", RayRectangleTemplate.Symmetric(4, 40)),
            new(28790, 0x233C, "StunningSweepDischarge", new CircleTemplate(8)),
            new(29164, 0x233C, "StunningSweep", new CircleTemplate(8)),
            new(29356, 0x233C, "ThermobaricCharge", new CircleTemplate(13)),
            new(28797, 0x233C, "AngrySalamander", new DungeonCrossTemplate(20, 2)),
        ], [28786, 28791, 28792, 28795, 28796, 29357, 28789, 29354]),
    ];
}
