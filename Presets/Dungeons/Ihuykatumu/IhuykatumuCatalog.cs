using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class IhuykatumuCatalog
{
    // Territory is supplied by the client CFC mapping, rather than the source CFC identifier.
    internal const uint TERRITORY = 1167;
    internal static bool Owns(uint oid) => oid is 0x4190 or 0x4195 or 0x4165;
    internal static bool IsBury(uint action) => action is >= 36497 and <= 36505;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4190, "PrimePunutiy", p => new RectangleGeometry(new(35, p.Y, -95), 0, 39, 39),
        [
            new(36492, 0, "PunutiyPress", null, "全屏伤害"),
            new(36493, 0, "Hydrowave", new FanTemplate(60, MathF.PI / 6)),
            new(36494, 0, "Resurface", new FanTemplate(100, MathF.PI / 3)),
            new(36497, 0, "Bury1", new CircleTemplate(12)),
            new(36500, 0, "Bury2", RayRectangleTemplate.Symmetric(10, 35)),
            new(36498, 0, "Bury3", new CircleTemplate(8)),
            new(36501, 0, "Bury4", new CircleTemplate(4)),
            new(36499, 0, "Bury5", RayRectangleTemplate.Symmetric(6, 25)),
            new(36502, 0, "Bury6", new CircleTemplate(6)),
            new(36503, 0, "Bury7", RayRectangleTemplate.Symmetric(6, 25)),
            new(36504, 0, "Bury8", RayRectangleTemplate.Symmetric(10, 35)),
            new(36505, 0, "Decay", new DonutTemplate(6, 40)),
            new(36508, 0, "PunutiyFlop1", new CircleTemplate(14), PartyAoe: "分散"),
            new(36513, 0, "PunutiyFlop2", new CircleTemplate(6), PartyAoe: "分散"),
            new(36515, 0, "ShoreShaker1", null),
            new(36516, 0, "ShoreShaker2", null),
            new(36517, 0, "ShoreShaker3", null),
        ], [872, 36496, 36495, 36506, 36509, 36510, 36511, 36512, 36514]),
        new(0x4195, "Drowsie", p => IhuykatumuArenas.Drowsie(p.Y),
        [
            new(39132, 0, "Uppercut", null, "死刑"),
            new(36478, 0, "Arise", new CircleTemplate(8)),
            new(36479, 0, "Wallop1", RayRectangleTemplate.Symmetric(10, 40)),
            new(36482, 0, "Wallop2", RayRectangleTemplate.Symmetric(16, 40)),
            new(36475, 0, "Sneeze", new FanTemplate(60, 150 * MathF.PI / 180)),
            new(36522, 0, "FlagrantSpread1", new CircleTemplate(6), PartyAoe: "分散"),
            new(36485, 0, "FlagrantSpread2", new CircleTemplate(6)),
        ], [872, 36476, 36477, 36480, 36481, 36484, 36762, 36483, 36524, 36523, 36525]),
        new(0x4165, "Apollyon", p => IhuykatumuArenas.Apollyon(p.Y),
        [
            new(36340, 0, "RazorZephyr", RayRectangleTemplate.Symmetric(12, 50)),
            new(36346, 0, "BladesOfFamine", RayRectangleTemplate.Symmetric(12, 50)),
            new(36347, 0, "Blade", null, "死刑"),
            new(36341, 0, "HighWind", null, "全屏伤害"),
            new(36350, 0, "Levinsickle", new CircleTemplate(4)),
            new(36349, 0, "LevinsickleSpark", null),
            new(36351, 0, "WingOfLightning", new FanTemplate(40, MathF.PI / 4)),
            new(36353, 0, "ThunderIII", new CircleTemplate(6), PartyAoe: "分散"),
            new(36357, 0, "BladeTB", null),
            new(36358, 0, "WindSickle", new DonutTemplate(5, 60)),
            new(36355, 0, "RazorStorm", RayRectangleTemplate.Symmetric(40, 40)),
        ], [870, 872, 36344, 36342, 36343, 36345, 36348, 36352, 36354, 36356, 36359, 36360, 36761]),
    ];
}
