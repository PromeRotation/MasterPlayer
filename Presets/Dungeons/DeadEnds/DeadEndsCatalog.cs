using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DeadEndsCatalog
{
    internal const uint TERRITORY = 973;
    internal static bool Owns(uint oid) => oid is 0x34C4 or 0x34C6 or 0x34C7;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x34C4, "CausticGrebuloff", p => DeadEndsArenas.Grebuloff(p.Y),
        [
            new(25924, 0x233C, "Befoulment", new CircleTemplate(6), PartyAoe: "分散"),
            new(25922, 0x233C, "BlightedWater", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25918, 0x233C, "CoughUp", new CircleTemplate(6)),
            new(25916, 0x34C4, "Miasmata", null, "全屏伤害"),
            new(25919, 0x34C5, "NecroticFluid", new CircleTemplate(6)),
            new(28348, 0x233C, "NecroticMist", new CircleTemplate(6)),
            new(25920, 0x34C4, "PoxFlail", null, "死刑"),
            new(28347, 0x34C4, "WaveOfNausea", new DonutTemplate(6, 40)),
        ], [872, 25923, 25921, 28349, 25917]),
        new(0x34C6, "Peacekeeper", p => DeadEndsArenas.PeacekeeperStarting(p.Y),
        [
            new(25936, 0x34C6, "Decimation", null, "全屏伤害"),
            new(25931, 0x34C6, "EclipsingExhaust", null, "全屏伤害"),
            new(28360, 0x34C6, "ElectromagneticRepellant", new CircleTemplate(9)),
            new(25935, 0x34C6, "Elimination", RayRectangleTemplate.Symmetric(10, 46), "死刑"),
            new(28359, 0x233C, "InfantryDeterrent", new CircleTemplate(6), PartyAoe: "分散"),
            new(25927, 0x233C, "NoFuture", new CircleTemplate(6)),
            new(25928, 0x233C, "NoFutureSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(25934, 0x233C, "Peacefire", new CircleTemplate(10)),
            new(28352, 0x384B, "SmallBoreLaser", RayRectangleTemplate.Symmetric(4, 20)),
        ], [25977, 28356, 28358, 25925, 28351, 25933, 28350, 28357, 25926]),
        new(0x34C7, "Rala", p => DeadEndsArenas.Rala(p.Y),
        [
            new(25946, 0x233C, "Benevolence", new CircleTemplate(6), PartyAoe: "分摊"),
            new(25939, 0x233C, "LamellarLightCircle", new CircleTemplate(15)),
            new(25951, 0x233C, "LamellarLightLine", RayRectangleTemplate.Symmetric(4, 40)),
            new(25940, 0x34C7, "Lifesbreath", RayRectangleTemplate.Symmetric(10, 50)),
            new(25943, 0x34C7, "LovingEmbraceLeft", new FanTemplate(45, MathF.PI)),
            new(25944, 0x34C7, "LovingEmbraceRight", new FanTemplate(45, MathF.PI)),
            new(25949, 0x34C7, "Pity", null, "死刑"),
            new(25948, 0x233C, "StillEmbrace", new CircleTemplate(6), PartyAoe: "分散"),
            new(25950, 0x34C7, "WarmGlow", null, "全屏伤害"),
        ], [870, 25945, 25942, 25937, 25938, 25947, 25941]),
    ];
}
