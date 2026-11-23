using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ClyteumCatalog
{
    internal const uint TERRITORY = 1345;
    internal static bool Owns(uint oid) => oid is 0x4C2C or 0x4C3F or 0x4C28;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x4C2C, "Eye of the Scorpion", p => new RectangleGeometry(new(-615, p.Y, 575), 0, 40, 40),
        [
            new(48896, 0, "EyesOnMe", null, "全屏伤害"),
            new(50177, 0, "PetrifyingBeam", new FanTemplate(70, MathF.PI * 100 / 180)),
            new(50178, 0, "PetrifyingBeam", new FanTemplate(70, MathF.PI * 100 / 180)),
            new(48901, 0, "PenetratorMissile", new CircleTemplate(6), PartyAoe: "分摊"),
            new(48898, 0, "SurfaceMissile", new CircleTemplate(5)),
            new(48899, 0, "AntiPersonnelMissile", new CircleTemplate(5), PartyAoe: "分散"),
        ], [50170, 45128, 50110, 50428, 50175, 50176, 48893, 48897, 48895]),
        new(0x4C3F, "Chort", p => new CircleGeometry(new(660, p.Y, -141), 15),
        [
            new(48871, 0, "MortifyingFlesh", RayRectangleTemplate.Symmetric(8, 40)),
            new(50400, 0, "MortifyingFleshAOEs", RayRectangleTemplate.Symmetric(16, 40)),
            new(48876, 0, "MortifyingFleshAOEs", RayRectangleTemplate.Symmetric(16, 40)),
            new(50361, 0, "BasicVomit", new FanTemplate(50, MathF.PI * 2 / 3)),
            new(48878, 0, "BodyweightExorcism", null),
            new(48882, 0, "BodyweightTower", null),
            new(48887, 0, "ProfanePressure", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [45308, 48888, 48884, 50408, 48869, 48872, 48877, 48879, 48880, 48881, 50417, 48885, 48886]),
        new(0x4C28, "Malphas", p => new CircleGeometry(new(760, p.Y, -803), 20),
        [
            new(48920, 0, "RubbishDisposal", null, "全屏伤害"),
            new(50313, 0, "VoidDark", new FanTemplate(30, MathF.PI)),
            new(48933, 0, "Goekinesis", RayRectangleTemplate.Symmetric(4, 80)),
            new(48937, 0, "MetallicMiasma", new FanTemplate(60, MathF.PI / 6)),
            new(48943, 0, "MetallicMiasma", new FanTemplate(60, MathF.PI / 6)),
            new(48935, 0, "CastOffHalo", new CircleTemplate(7)),
            new(48940, 0, "CastOffHalo", new CircleTemplate(7)),
            new(50314, 0, "ShadowPlay", new CircleTemplate(6), PartyAoe: "分散"),
            new(48930, 0, "GluttonousWire", new CircleTemplate(6), PartyAoe: "分摊"),
            new(48928, 0, "WrathfulWire", new CircleTemplate(5), PartyAoe: "分散"),
            new(48931, 0x4C28, "StringUp", null),
        ], [50220, 48932, 48921, 48922, 48936, 48934, 48929, 48923, 50315, 48938, 48941, 48925, 48939, 48942]),
    ];
}
