using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AlexandriaCatalog
{
    internal const uint TERRITORY = 1199;
    internal static bool Owns(uint oid) => oid is 0x4173 or 0x416A or 0x41CE;
    internal static bool Partition(uint action) => action is 39007 or 39238 or 39249;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4173, "Antivirus X", p => new RectangleGeometry(new(852, p.Y, 823), 0, 45, 35),
        [
            new(36378, 0, "ImmuneResponseVisualSmall", null),
            new(36379, 0, "ImmuneResponseSmall", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(36381, 0, "ImmuneResponseBig", new FanTemplate(40, MathF.PI * 4 / 3)),
            new(36382, 0, "PathocircuitPurge", null),
            new(36383, 0, "PathocrossPurge", null),
            new(36387, 0, "Cytolysis", null, "全屏伤害"),
        ], [36388, 36380, 36384, 36385, 36386]),
        new(0x416A, "Amalgam", p => new RectangleGeometry(new(-533, p.Y, -373), 0, 46, 46),
        [
            new(36338, 0, "Electrowave", null, "全屏伤害"),
            new(36323, 0, "Disassembly", null, "全屏伤害"),
            new(36339, 0, "Amalgamight", null, "单体伤害"),
            new(36327, 0, "CentralizedCurrent", RayRectangleTemplate.Symmetric(15, 90)),
            new(36331, 0, "SplitCurrent", RayRectangleTemplate.Symmetric(25, 90)),
            new(36330, 0, "SplitCurrent", RayRectangleTemplate.Symmetric(25, 90)),
            new(39136, 0, "SupercellMatrixTriangle", new LocalPolygonTemplate(
                [Vector2.Zero, new(28.284271f, -28.284271f), new(-28.284271f, -28.284271f)])),
            new(39138, 0, "SupercellMatrixPortal", RayRectangleTemplate.Symmetric(8, 55)),
            new(36336, 0, "Voltburst", new CircleTemplate(6)),
            new(36334, 0, "StaticSpark", new CircleTemplate(6), PartyAoe: "分散"),
            new(36333, 0, "Superbolt", new CircleTemplate(6), PartyAoe: "分摊"),
            new(39254, 0, "TernaryCharge1", null),
            new(39255, 0, "TernaryCharge2", null),
            new(39256, 0, "TernaryCharge3", null),
        ], [872, 36337, 36329, 36325, 36332, 39253]),
        new(0x41CE, "Eliminator", p => new RectangleGeometry(new(-759, p.Y, -648), 0, 31, 31),
        [
            new(36765, 0, "Disruption", null, "全屏伤害"),
            new(39007, 0, "Partition", new FanTemplate(40, MathF.PI)),
            new(39238, 0, "Partition", new FanTemplate(40, MathF.PI)),
            new(39249, 0, "Partition", new FanTemplate(40, MathF.PI)),
            new(39615, 0, "Terminate", RayRectangleTemplate.Symmetric(10, 40)),
            new(39616, 0, "HaloOfDestruction", new DonutTemplate(6, 40)),
            new(39239, 0, "Explosion", RayRectangleTemplate.Symmetric(8, 50)),
            new(36793, 0, "Compression", new CircleTemplate(6)),
            new(39243, 0, "Electray", new CircleTemplate(6), PartyAoe: "分散"),
            new(36794, 0, "Impact", null),
            new(36783, 0, "LightOfSalvationMarker", null),
        ], [36764, 36763, 39599, 39600, 36768, 36766, 36767, 36781, 36775, 36772, 36774, 36777,
            36788, 39248, 39247, 36773, 36776, 36778, 36779, 36780, 36789, 36790, 36791, 36792,
            36782, 36784, 36785, 36786, 36787, 36795, 36796]),
    ];
}
