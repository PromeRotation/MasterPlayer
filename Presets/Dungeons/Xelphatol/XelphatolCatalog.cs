using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class XelphatolCatalog
{
    internal const uint TERRITORY = 1113;
    internal static bool IsTrash(uint oid) => oid is 0x17AF or 0x17B5 or 0x17BA or 0x17B8;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x179B or 0x179F or 0x17A2;
    internal static bool IsKey(uint oid) => oid is 0x1EA165 or 0x1EA166 or 0x1EA167;
    internal static bool InGroup(uint primary, uint oid) => primary switch
    {
        0x17AF => oid is 0x17AF or 0x17AC or 0x17AD or 0x17AB or 0x17AE,
        0x17B5 => oid is 0x17B5 or 0x17B1 or 0x17B3 or 0x17B2,
        0x17B8 => oid is 0x17B8 or 0x17B7,
        0x17BA => oid is 0x17BA or 0x17B9 or 0x17B1 or 0x17B6 or 0x17BB or 0x17B2 or 0x17B4 or 0x17B3 or 0x17B7,
        _ => false,
    };

    internal static DungeonDefinition[] Create() =>
    [
        new(0x17AF, "Chasm Harpeia", p => p.X < -130 ? XelphatolArenas.HarpeiaFirst(p.Y) : XelphatolArenas.HarpeiaSecond(p.Y),
        [
            new(1656, 0x17AE, "FlashFlood", new FanTemplate(8.16f, MathF.PI * 2 / 3)),
            new(1006, 0x17AD, "LaboredLeap", new CircleTemplate(10.32f)),
            new(6852, 0xD25, "FallingRock", new CircleTemplate(4)),
            new(2478, 0x17AF, "WingsOfWoe", new CircleTemplate(6)),
        ], [872, 870, 871, 976, 409, 408]),
        new(0x179B, "Nuzal Hueloc", p => XelphatolArenas.Nuzal(p.Y),
        [
            new(6599, 0x179B, "WindBlast", RayRectangleTemplate.Symmetric(8, 61.5f)),
            new(6604, 0x179E, "HotBlast", null, "进入首领脚下"),
        ], [872, 6605, 870, 6598, 6603, 6601, 6602, 6600]),
        new(0x17B5, "Siege Gobbue", p => p.Z < -10 ? XelphatolArenas.SiegeFirst(p.Y) : XelphatolArenas.SiegeSecond(p.Y),
        [
            new(720, 0x17B2, "Overpower", new FanTemplate(7.08f, MathF.PI / 2)),
            new(6625, 0x17B5, "Sneeze", new FanTemplate(20.85f, MathF.PI / 2), "打断或眩晕 Sneeze"),
        ], [870, 871, 717, 423, 722]),
        new(0x17BA, "Xelphatol Swiftbeak", p =>
        {
            if (p.X < 200 && p.Z > -200) return XelphatolArenas.SwiftFirst(p.Y);
            if (p.Z < -200) return XelphatolArenas.SwiftSecond(p.Y);
            return XelphatolArenas.SwiftThird(p.Y);
        },
        [
            new(720, 0x17B2, "Overpower", new FanTemplate(7.08f, MathF.PI / 2)),
            new(6627, 0x17B7, "Gust", new CircleTemplate(5)),
        ], [871, 870, 423, 969, 722, 510, 717, 6626]),
        new(0x17B8, "Xelphatol Skycaller", p => XelphatolArenas.Skycaller(p.Y),
        [
            new(6413, 0x17B8, "Tornado", new CircleTemplate(6), "打断或眩晕 Tornado", "分散"),
            new(6629, 0x17B8, "IxaliAeroIII", null, "全屏伤害"),
            new(6628, 0x17B8, "IxaliAeroII", RayRectangleTemplate.Symmetric(8, 41.8f)),
            new(6627, 0x17B7, "Gust", new CircleTemplate(5)),
        ], [871, 969]),
        new(0x179F, "Dotoli Ciloc", p => XelphatolArenas.DotoliStarting(p.Y),
        [
            new(6606, 0x179F, "OnLow", new FanTemplate(10.98f, MathF.PI * 2 / 3)),
            new(6607, 0x179F, "OnHigh", null, "利用墙段挡击退，避开旋风"),
            new(6609, 0x179F, "Swiftfeather", null),
            new(32557, 0x179F, "Stormcoming", new CircleTemplate(6)),
        ], [872, 32556, 6610]),
        new(0x17A2, "Tozol Huatotl", p => XelphatolArenas.Tozol(p.Y),
        [
            new(6612, 0x17A2, "IxaliAeroII", RayRectangleTemplate.Symmetric(6, 43)),
            new(6613, 0x17A2, "IxaliAeroIII", null, "全屏伤害"),
            new(6618, 0x17A3, "Bill", new CircleTemplate(5), PartyAoe: "分散"),
            new(6619, 0xD25, "EyeOfTheStorm", new DonutTemplate(10, 20)),
            new(6620, 0x17A4, "MistralSong", new FanTemplate(32.89f, MathF.PI * 2 / 3)),
            new(6621, 0x17A4, "WickedWheel", new CircleTemplate(7)),
            new(6622, 0x17A4, "AerialBlast", null, "全屏伤害"),
        ], [872, 6611, 6614, 6616, 6617, 6615]),
    ];
}
