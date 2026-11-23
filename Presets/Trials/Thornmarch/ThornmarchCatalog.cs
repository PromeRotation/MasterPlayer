using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ThornmarchCatalog
{
    internal const uint TERRITORY = 1067;
    internal static bool IsEnemy(uint oid) => oid is >= 0x391C and <= 0x3923;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x391C, "Thornmarch Hard", p => new CircleGeometry(new(0, p.Y, 0), 21),
        [
            new(29216, 0, "SpinningMogshield", new CircleTemplate(6)),
            new(29215, 0, "ThousandKuponzeSwipe", new FanTemplate(20, MathF.PI / 2)),
            new(29192, 0, "MograinOfDeath", null),
            new(29211, 0, "PomHoly", null, "全屏伤害"),
            new(29213, 0, "MoggledayNightFever", new FanTemplate(30, MathF.Tau / 3)),
            new(29214, 0, "MoogleThrust", null, "死刑"),
            new(29217, 0, "MementoMoogle", null, "全屏伤害"),
            new(29210, 0, "PomHolyBoss", null, "全屏伤害"),
            new(29209, 0, "ThousandKuponzeCharge", null, "死刑"),
            new(29207, 0, "PomBog", null),
            new(29204, 0, "MogStone", null),
            new(29196, 0, "MoogleGoRoundBoss", null),
            new(29197, 0, "MoogleGoRoundAdd", null),
            new(29206, 0, "TwinPomMeteor", null),
            new(29190, 0, "GoodKingsDecree3", null),
            new(29199, 0, "MogComet", new CircleTemplate(6)),
            new(29201, 0, "PomStoneIn", null),
            new(29202, 0, "PomStoneMid", null),
            new(29619, 0, "PomStoneOut", null),
            new(29208, 0, "MogCreation", RayRectangleTemplate.Symmetric(10, 50)),
        ], [870, 872, 29187, 29191, 29212, 29193, 29194, 29195, 29188, 29203, 29189, 29205, 29198, 29200])
    ];
}
