using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class KtisisCatalog
{
    internal const uint TERRITORY = 974;
    internal static bool Owns(uint oid) => oid is 0x3323 or 0x3425 or 0x348A;
    internal static uint InterruptAction(uint job) => job switch
    {
        1 or 3 or 19 or 21 or 32 or 37 => 7538,
        5 or 23 or 31 or 38 => 7551,
        _ => 0,
    };
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3323, "Lyssa", p => KtisisArenas.Lyssa(p.Y),
        [
            new(25181, 0x3323, "FrigidStomp", null, "全屏伤害"),
            new(25180, 0x3323, "HeavySmash", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25179, 0x3324, "IcePillar", new CircleTemplate(4)),
            new(25375, 0x3324, "PillarPierce", RayRectangleTemplate.Symmetric(4, 40)),
            new(25177, 0x233C, "PunishingSlice", RayRectangleTemplate.Symmetric(50, 50)),
            new(25182, 0x3323, "SkullDasher", null, "死刑"),
        ], [6497, 28304, 25175, 25178, 25176]),
        new(0x3425, "LadonLord", p => new RectangleGeometry(new(0, p.Y, 48), 0, 39, 39),
        [
            new(25741, 0x3425, "Intimidation", null, "全屏伤害"),
            new(25742, 0x3425, "PyricBlast", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25734, 0x3425, "PyricBreathFront", new FanTemplate(40, MathF.Tau / 3)),
            new(25735, 0x3425, "PyricBreathLeft", new FanTemplate(40, MathF.Tau / 3)),
            new(25736, 0x3425, "PyricBreathRight", new FanTemplate(40, MathF.Tau / 3)),
            new(25745, 0x233C, "PyricSphere", new DungeonCrossTemplate(50, 2)),
            new(25743, 0x3425, "Scratch", null, "死刑"),
        ], [872, 25733, 25732, 25915, 25737, 25738, 25739, 25744, 25740]),
        new(0x348A, "Hermes", p => KtisisArenas.HermesStarting(p.Y),
        [
            new(25891, 0x348C, "CosmicKiss", new CircleTemplate(10)),
            new(25886, 0x348A, "Trismegistos", null, "全屏伤害"),
            new(25901, 0x233C, "TrueAeroRepeat", RayRectangleTemplate.Symmetric(6, 40)),
            new(25897, 0x233C, "TrueAeroIISpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(25898, 0x233C, "TrueAeroIICircle", new CircleTemplate(6)),
            new(25889, 0x348B, "TrueAeroIV", RayRectangleTemplate.Symmetric(10, 50)),
            new(27836, 0x348B, "TrueAeroIVLOS", RayRectangleTemplate.Symmetric(10, 50), "躲到完好陨石背后"),
            new(27837, 0x348B, "TrueAeroIVFour", RayRectangleTemplate.Symmetric(10, 50)),
            new(25907, 0x348A, "TrueBravery", null, "打断首领勇气"),
            new(25906, 0x233C, "TrueTornado", new CircleTemplate(4)),
        ], [872, 25892, 25888, 25893, 25895, 25890, 25894, 25899, 25887, 25900, 25896, 25902, 25903, 25904, 25905]),
    ];
}
