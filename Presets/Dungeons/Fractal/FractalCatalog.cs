using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class FractalCatalog
{
    internal const uint TERRITORY = 430;
    internal static readonly uint[] CLOCKWORK = [0x1020, 0x101E, 0x101F];
    internal static readonly uint[] MANIKIN = [0x101D, 0x101E, 0x101F, 0x101B, 0x101C];
    internal static readonly uint[] NAGA = [0x1023, 0x1025, 0x1021, 0x1022, 0x1024];
    internal static readonly uint[] EMERALD = [0x1028, 0x1026, 0x102B, 0x1027];
    internal static readonly uint[] CRIMSON = [0x102A, 0x1029, 0x102B, 0x1027, 0x1026];

    internal static bool IsControl(uint oid) => oid is 0x1E990E or 0x1E990F or 0x1E9910;
    internal static bool IsTrash(uint oid) => IsControl(oid) || oid is 0x1020 or 0x101D or 0x1023;
    internal static bool IsInteraction(uint oid) => IsControl(oid) || oid is 0x1E99EA or 0x1E99E8 or 0x1E99EB or 0x1E99E9;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x1012 or 0x1013 or 0x1018;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x1012, "Phantom Ray", p => FractalArenas.PhantomRay(p.Y),
        [
            new(3965, 0xD25, "DoubleSever", new FanTemplate(30.5f, MathF.PI / 2)),
            new(3968, 0xD25, "AtmosphericCompression", new CircleTemplate(5)),
            new(3962, 0x1012, "RapidSever", null, "死刑"),
        ], [871, 3967, 3963, 3964, 3966]),
        new(0x1013, "Minotaur", p => FractalArenas.Minotaur(p.Y),
        [
            new(3969, 0x1013, "OneOneTonzeSwipe", new FanTemplate(9, MathF.PI * 2 / 3)),
            new(3970, 0x1013, "OneOneOneTonzeSwing", new CircleTemplate(12)),
            new(3971, 0x1013, "TenTonzeSlash", new FanTemplate(44, MathF.PI / 3)),
            new(3975, 0x1013, "OneOneOneOneTonzeSwing", null, "交互孵化器眩晕首领"),
            new(3972, 0x1013, "DisorientingGroan", null, "击退"),
        ], [870, 871, 872, 2150, 3976, 3974]),
        new(0x1018, "The Curator", p => FractalFloorBoundary.Create(p.Y, 0),
        [
            new(3985, 0xD25, "SeedOfTheRivers", new CircleTemplate(5)),
            new(3981, 0x1018, "TheEducator", null),
            new(3986, 0x1019, "TheEducatorBootSequence", new RectangleTemplate(10, 10)),
            new(3979, 0x1018, "AetherochemicalExplosive", null, "炸弹减益：驱散并分散"),
        ], [872, 3977, 3978, 3980, 3982, 3983, 3984]),
        new(0x1020, "Clockwork Dreadnaught", p => FractalArenas.Clockwork(p.Y),
            [new(4556, 0x1020, "Rotoswipe", new FanTemplate(11, MathF.PI * 2 / 3))], [872], CLOCKWORK),
        new(0x101D, "Manikin Conjurer", p => FractalArenas.Manikin(p.Y),
        [
            new(720, 0x101B, "Overpower", new FanTemplate(6.5f, MathF.PI / 2)),
            new(1135, 0x101B, "Rive", RayRectangleTemplate.Symmetric(2, 30.5f)),
        ], [970, 872, 870], MANIKIN),
        new(0x1023, "Flawless Naga", p => FractalArenas.Naga(p.Y),
        [
            new(3027, 0x1023, "CalcifyingMist", new FanTemplate(7.35f, MathF.PI / 2)),
            new(2144, 0x1024, "TheRamsVoice", new CircleTemplate(9.7f), "打断或眩晕"),
            new(2145, 0x1024, "TheDragonsVoice", new DonutTemplate(7.7f, 30), "打断或眩晕"),
            new(4555, 0x1025, "Furore", null, "打断或眩晕"),
            new(3028, 0x1023, "BalefulRoar", null, "打断或眩晕"),
        ], [870, 871, 872, 2143], NAGA),
        new(0x1E990F, "Violet Control Unit", p => FractalArenas.Violet(p.Y), [], [870, 872]),
        new(0x1E990E, "Emerald Control Unit", p => FractalArenas.Emerald(p.Y),
        [
            new(1427, 0x1028, "Exhaust", RayRectangleTemplate.Symmetric(12, 33)),
            new(3988, 0x1026, "AutoCannons", RayRectangleTemplate.Symmetric(5, 42)),
            new(3987, 0x1B2, "SpawnReservoir", new CircleTemplate(3)),
        ], [870, 1425, 2842, 2843, 3078]),
        new(0x1E9910, "Crimson Control Unit", p => FractalArenas.Crimson(p.Y),
        [
            new(2706, 0x102A, "TheHand", new FanTemplate(7.5f, MathF.PI * 2 / 3)),
            new(2705, 0x102A, "Shred", RayRectangleTemplate.Symmetric(4, 5.5f)),
            new(3988, 0x1026, "AutoCannons", RayRectangleTemplate.Symmetric(5, 42)),
            new(3987, 0x1B2, "SpawnReservoir", new CircleTemplate(3)),
        ], [870, 872, 4549, 4550, 3078]),
    ];
}
