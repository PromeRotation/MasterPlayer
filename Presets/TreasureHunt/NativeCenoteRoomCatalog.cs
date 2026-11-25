using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NativeCenoteRoomCatalog
{
    internal const uint ROOM_1 = 0x1EBAA4;
    internal const uint ROOM_2 = 0x1EBAA5;
    internal const uint ROOM_3 = 0x1EBAA6;
    internal const uint ROOM_4 = 0x1EBAA7;
    internal static bool IsRoom(uint oid) => oid is ROOM_1 or ROOM_2 or ROOM_3 or ROOM_4;
    internal static uint[] Trash(uint room) => room switch
    {
        ROOM_1 => [0x4312, 0x4311, 0x4310, 0x4307, 0x4308, 0x4309, 0x430C, 0x430B, 0x430A, 0x430E, 0x430D, 0x430F],
        ROOM_2 => [0x4310, 0x4313, 0x4316, 0x4318, 0x4314, 0x4311, 0x4315, 0x4317],
        ROOM_3 => [0x431D, 0x431E, 0x4324, 0x4323, 0x4319, 0x431A, 0x4320, 0x431F, 0x431B, 0x431C, 0x4321, 0x4322],
        ROOM_4 => [0x4325, 0x430C, 0x4327, 0x4328, 0x4326, 0x430D, 0x430F],
        _ => [],
    };
    internal static uint[] Enemies(uint room) => [.. Trash(room), 0x4304, 0x4303, 0x4300, 0x4301, 0x4302, 0x42FF, 0x42FE];
    internal static int Priority(uint oid) => oid switch
    {
        >= 0x4300 and <= 0x4304 => 6 - (int)(oid - 0x4300),
        0x42FE => 2,
        0x42FF => 1,
        _ => 0,
    };
    internal static DungeonDefinition[] Create() =>
    [
        new(ROOM_1, "Cenote Ja Ja Gural Room 1", p => NativeCenoteRoomArenas.Room1(p.Y),
        [
            new(38291, 0, "Nepenthic Plunge", new FanTemplate(10, MathF.PI / 2)),
            new(38292, 0, "Creeping Hush", new FanTemplate(12, 2 * MathF.PI / 3)),
            new(38290, 0, "Ovation", RayRectangleTemplate.Symmetric(4, 14)),
            new(38283, 0, "Bestial Fire", new CircleTemplate(5)),
            new(38284, 0, "Head Butt", new FanTemplate(6, 2 * MathF.PI / 3)),
            new(38287, 0, "Aetherial Blast", RayRectangleTemplate.Symmetric(4, 20)),
            new(38285, 0, "Isle Drop", new CircleTemplate(6)),
            new(38281, 0, "Envenomate", null),
            new(38288, 0, "Syrup Spout", new FanTemplate(10, 2 * MathF.PI / 3)),
            new(38289, 0, "Spinning Attack", RayRectangleTemplate.Symmetric(4, 10)),
            .. CommonCasts(),
        ], []),
        new(ROOM_2, "Cenote Ja Ja Gural Room 2", p => NativeCenoteRoomArenas.Room2(p.Y),
        [
            new(38296, 0, "Water III", new CircleTemplate(8)),
            new(38298, 0, "Pollen Corona", new CircleTemplate(8)),
            new(38293, 0, "Swiftwind Serenade", RayRectangleTemplate.Symmetric(8, 40)),
            new(38290, 0, "Ovation", RayRectangleTemplate.Symmetric(4, 14)),
            new(38300, 0, "Gravel Shower", RayRectangleTemplate.Symmetric(4, 10)),
            new(38299, 0, "Flatten", new FanTemplate(8, MathF.PI / 2)),
            new(38295, 0, "Dissever", new FanTemplate(10, MathF.PI / 2)),
            new(38291, 0, "Nepenthic Plunge", new FanTemplate(10, MathF.PI / 2)),
            new(38297, 0, "Double Smash", new FanTemplate(10, 2 * MathF.PI / 3)),
            new(38302, 0, "Critical Bite", new FanTemplate(10, 2 * MathF.PI / 3)),
            new(38301, 0, "Tornado", new CircleTemplate(6)),
            new(38294, 0, "Tornadic Serenade", new CircleTemplate(6)),
            .. CommonCasts(),
        ], []),
        new(ROOM_3, "Cenote Ja Ja Gural Room 3", p => NativeCenoteRoomArenas.Room3(p.Y),
        [
            new(38308, 0, "Feathercut", RayRectangleTemplate.Symmetric(8, 40)),
            new(38307, 0, "Wingbeat", new FanTemplate(18, MathF.PI / 3)),
            new(38303, 0, "Gold Dust", new CircleTemplate(8)),
            new(38304, 0, "Bloody Caress", new FanTemplate(12, 2 * MathF.PI / 3)),
            new(38310, 0, "Swift Sough", new FanTemplate(13, MathF.PI / 3)),
            new(38313, 0, "Tornado", new CircleTemplate(6)),
            new(38311, 0, "Incubus", new CircleTemplate(6)),
            new(38306, 0, "Fire Break", new FanTemplate(8, MathF.PI / 2)),
            new(38312, 0, "Fire II", new CircleTemplate(5)),
            new(38309, 0, "Bitter Nectar", new CircleTemplate(5)),
            .. CommonCasts(),
        ], []),
        new(ROOM_4, "Cenote Ja Ja Gural Room 4", p => NativeCenoteRoomArenas.Room4(p.Y),
        [
            new(38286, 0, "Lumisphere", new CircleTemplate(6)),
            new(38316, 0, "Tornado", new CircleTemplate(6)),
            new(38314, 0, "Roots of Atopy", new CircleTemplate(6)),
            new(38287, 0, "Aetherial Blast", RayRectangleTemplate.Symmetric(4, 20)),
            new(38317, 0, "Serrated Spin", new CircleTemplate(8)),
            new(38319, 0, "Left Wingblade", new FanTemplate(8, MathF.PI)),
            new(38318, 0, "Right Wingblade", new FanTemplate(8, MathF.PI)),
            new(38288, 0, "Syrup Spout", new FanTemplate(10, 2 * MathF.PI / 3)),
            new(38289, 0, "Spinning Attack", RayRectangleTemplate.Symmetric(4, 10)),
            new(38315, 0, "Odious Air", new FanTemplate(12, 2 * MathF.PI / 3)),
            .. CommonCasts(),
        ], []),
    ];

    private static DungeonCastRule[] CommonCasts() =>
    [
        new(38277, 0, "Rotten Spores", new CircleTemplate(6)),
        new(38279, 0, "Spin", new CircleTemplate(11)),
        new(38278, 0, "Scoop", new FanTemplate(15, 2 * MathF.PI / 3)),
        .. new uint[] { 32302, 32301, 32304, 32303, 32305 }
            .Select(id => new DungeonCastRule(id, 0, "Mandragora", new CircleTemplate(7))),
    ];
}
