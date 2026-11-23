using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TsukuyomiCatalog
{
    internal const uint TERRITORY = 778;
    internal static bool IsAdd(uint oid) => oid is 0x2242 or 0x2244 or 0x2245 or 0x224B or 0x224A or 0x2246 or 0x2247;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x2210, "Tsukuyomi", p => new CircleGeometry(new(100, p.Y, 100), 20),
        [
            new(11235, 0, "TormentUntoDeath", null),
            new(11245, 0, "TsukiNoMaiogi", null),
            new(11239, 0, "SteelOfTheUnderworld", new FanTemplate(70, MathF.PI / 2)),
            new(11234, 0, "Reprimand", null, "全屏伤害"),
            new(11246, 0, "Nightbloom", null, "全屏伤害"),
            new(11440, 0, "Nightbloom2", null, "全屏伤害"),
            new(11436, 0, "UnmovingTroika1", new FanTemplate(39, MathF.PI * 75 / 180)),
            new(11437, 0, "UnmovingTroika2", new FanTemplate(39, MathF.PI * 75 / 180)),
            new(11379, 0, "LunarHalo", new DonutTemplate(6, 14)),
            new(11256, 0, "Antitwilight", null, "全屏伤害"),
            new(11259, 0, "Lunacy", null),
            new(11257, 0, "DarkBlade", new FanTemplate(70, MathF.PI * 210 / 180)),
            new(11258, 0, "BrightBlade", new FanTemplate(70, MathF.PI * 210 / 180)),
            new(11897, 0, "DanceOfTheDead", null, "全屏伤害"),
            new(11243, 0, "ToAshes", null, "全屏伤害"),
        ], [11523, 11955, 11244, 11237, 11200, 11240, 11241, 11236, 11441, 11238, 11471, 11542, 11543,
            11858, 11247, 11435, 11210, 11211, 11248, 11478, 11438, 11249, 11261, 11551, 11260, 870])
    ];
}
