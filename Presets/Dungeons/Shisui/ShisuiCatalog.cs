using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ShisuiCatalog
{
    internal const uint TERRITORY = 1367;
    internal static bool Owns(uint oid) => oid is 0x1B0C or 0x1B0E or 0x1B0F;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1B0C, "Amikiri", p => ShisuiArenas.Amikiri(p.Y), [], [872, 8050, 8051, 8052, 8053, 8056, 8057, 8055]),
        new(0x1B0E, "Ruby Princess", p => ShisuiArenas.RubyPrincess(p.Y),
        [
            new(8058, 0x1B0E, "Seduce", null, "进入关闭的宝箱取得变老"),
            new(8059, 0x1B0E, "CoriolisKick", null, "远离踢击"),
            new(8060, 0x1B0E, "AbyssalVolcano", new CircleTemplate(7)),
            new(9431, 0x18D6, "GeothermalFlatulence", null, "连续十次追踪爆炸"),
        ], [872, 8063, 8062, 8061]),
        new(0x1B0F, "Shisui Yohi", p => ShisuiArenas.Yohi(p.Y),
        [
            new(8064, 0x1B0F, "ThickFog", null, "全屏伤害"),
            new(8066, 0x1B0F, "MadStare", null, "背对视线"),
            new(8069, 0x1B10, "BiteAndRun", null, "死刑"),
            new(8070, 0x1C87, "BiteAndRun", null, "死刑"),
        ], [870, 8071, 8067, 8065, 8068]),
    ];
}
