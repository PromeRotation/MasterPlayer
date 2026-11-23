using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DohnMhegCatalog
{
    internal const uint TERRITORY = 821;
    internal static bool Owns(uint oid) => oid is 0x3F2 or 0x98E or 0xF14;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x3F2, "Aenc Thon (first)", p => DohnMhegArenas.First(p.Y),
        [
            new(8857, 0x3F2, "CandyCane", null, "死刑"),
            new(8871, 0x3F2, "HydrofallVisual", null),
            new(8893, 0x233C, "Hydrofall", new CircleTemplate(6)),
            new(8852, 0x3F2, "LaughingLeap", new CircleTemplate(4)),
            new(7822, 0x3F2, "Landsblood", null, "全屏伤害"),
        ], [872, 8840, 7899, 8800]),
        new(0x98E, "Griaule", p => DohnMhegArenas.Griaule(p.Y),
        [
            new(8906, 0x98E, "Swinge", null),
            new(8897, 0x98E, "Fodder", null),
            new(8915, 0x98E, "Tiiimbeeer", null, "全屏伤害"),
            new(8901, 0x98E, "CoilingIvy", null),
        ], [870, 10355, 8899]),
        new(0xF14, "Aenc Thon (last)", p => DohnMhegArenas.Last(p.Y),
        [
            new(13732, 0xF14, "CripplingBlow", null, "死刑"),
            new(13708, 0xF14, "VirtuosicCapriccio", null, "全屏伤害"),
            new(13552, 0xF14, "ImpChoir", null),
            new(13551, 0xF14, "ToadChoir", new FanTemplate(19.5f, MathF.PI * 5 / 6)),
            new(13498, 0xF14, "FunambulistsFantasia", null),
            new(13519, 0x233C, "FunambulistsFantasiaPull", null),
            new(13521, 0xF14, "ChangelingsFantasia", null),
            new(13522, 0x233C, "ChangelingsFantasiaHelper", null),
            new(13550, 0x233C, "BileBombardment", new CircleTemplate(8)),
            new(13547, 0xF14, "CorrosiveBile", null),
            new(13952, 0xF14, "FlailingTentacles", null),
            new(15723, 0xF63, "Finale", null, "摧毁谎言之琴"),
            new(13520, 0xF14, "FinaleEnrage", null, "全屏伤害"),
        ], [870, 872, 13206, 13549, 13548, 13953]),
    ];
}
