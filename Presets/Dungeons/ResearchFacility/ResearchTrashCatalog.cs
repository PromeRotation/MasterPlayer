using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ResearchTrashCatalog
{
    internal static readonly uint[] FIRST = [0xFA5, 0xF48, 0xF49, 0xF59, 0xF5A, 0xFA4, 0xFA3];
    internal static readonly uint[] VANGUARD = [0x1350, 0xF4B, 0xF4A, 0xF4C];
    internal static readonly uint[] DREADNAUGHT = [0xF54, 0xF55];
    internal static readonly uint[] CONJURER = [0xF56, 0xF57];
    internal static readonly uint[] SHABTI_CONJURER = [0xF58, 0xF56, 0xF57];
    internal static readonly uint[] BIO_FIRST = [0xF4D, 0xF4E, 0x10B1, 0x10B0, 0x10AF, 0xF50, 0xF4F];
    internal static readonly uint[] BIO_SECOND = [0xF4D, 0x10B3, 0xF51, 0x10B2, 0xF53];

    internal static bool IsPrimary(uint oid) => oid is 0xFA5 or 0xFA3 or 0x1350 or 0xF54 or 0xF58 or 0xF56 or 0xF4D;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xFA5, "Enforcement Droid 210", p => ResearchTrashArenas.Enforcement(p.Y), FirstCasts(), [4549, 4550, 4665, 4664, 608, 607], FIRST),
        new(0xFA3, "Scrambled Iron Giant", p => ResearchTrashArenas.Enforcement(p.Y), FirstCasts(), [4549, 4550, 4665, 4664, 608, 607], FIRST),
        new(0x1350, "Sixth Legion Magitek Vanguard H-1", p => ResearchTrashArenas.Vanguard(p.Y),
        [
            new(526, 0x1350, "CermetDrill", RayRectangleTemplate.Symmetric(5, 8.8f)),
            new(866, 0xF4B, "Heartstopper", RayRectangleTemplate.Symmetric(3, 3.5f)),
            new(316, 0xF4C, "Stoneskin", null, "石肤：打断或眩晕"),
        ], [969, 404, 403, 722, 717, 10, 76, 718, 120, 20], VANGUARD),
        new(0xF54, "Facility Dreadnaught", p => ResearchTrashArenas.Dreadnaught(p.Y),
        [
            new(4556, 0xF54, "Rotoswipe", new FanTemplate(11, MathF.PI * 2 / 3)),
            new(4825, 0xF55, "AutoCannons", RayRectangleTemplate.Symmetric(5, 42.4f)),
            new(4557, 0xF54, "WreckingBall", new CircleTemplate(8)),
        ], [], DREADNAUGHT),
        new(0xF58, "Cultured Shabti", p => p.Z > 200 ? ResearchTrashArenas.Shabti(p.Y) : ResearchTrashArenas.ShabtiConjurer(p.Y),
            ShabtiConjurerCasts(), [966, 969], SHABTI_CONJURER),
        new(0xF56, "Cloned Conjurer", p => p.Z > 200 ? ResearchTrashArenas.Conjurer(p.Y) : ResearchTrashArenas.ShabtiConjurer(p.Y),
            ShabtiConjurerCasts(), [966, 969], SHABTI_CONJURER),
        new(0xF4D, "Bioculture Node", p => p.Z < 250 ? ResearchTrashArenas.BiocultureFirst(p.Y) : ResearchTrashArenas.BiocultureSecond(p.Y),
        [
            new(4465, 0xF50, "Puncture", RayRectangleTemplate.Symmetric(3, 5.5f)),
            new(4703, 0xF4F, "TailSlap", new FanTemplate(7, MathF.PI * 2 / 3)),
            new(4666, 0x10B0, "CalcifyingMist", new FanTemplate(6.9f, MathF.PI / 2)),
            new(4464, 0xF50, "EerieSoundwave", new CircleTemplate(7.5f)),
            new(3342, 0x10B3, "MarrowDrain", new FanTemplate(9.7f, MathF.PI * 2 / 3)),
            new(3341, 0x10B3, "MarrowDrain", new FanTemplate(9.7f, MathF.PI * 2 / 3)),
            new(3340, 0x10B3, "MarrowDrain", new FanTemplate(9.7f, MathF.PI * 2 / 3)),
            new(3343, 0x10B3, "TheRamsVoice", new CircleTemplate(9.7f)),
            new(4519, 0xF51, "Sideswipe", new FanTemplate(9, MathF.PI / 2)),
            new(917, 0x10B2, "Gust", new CircleTemplate(3)),
        ], [4824, 4704, 939, 4518, 915], [.. BIO_FIRST, .. BIO_SECOND]),
    ];

    private static DungeonCastRule[] FirstCasts() =>
    [
        new(33030, 0xFA3, "GrandSword", new FanTemplate(16, MathF.PI * 2 / 3)),
        new(609, 0xFA4, "TheHand", new FanTemplate(7.5f, MathF.PI * 2 / 3)),
    ];

    private static DungeonCastRule[] ShabtiConjurerCasts() =>
    [
        new(1259, 0xF58, "Spellsword", new FanTemplate(8.2f, MathF.PI * 2 / 3)),
        new(1260, 0xF58, "DeathsDoor", RayRectangleTemplate.Symmetric(2, 22.2f)),
        new(900, 0xF56, "Tornado", new CircleTemplate(6), PartyAoe: "分散"),
    ];
}
