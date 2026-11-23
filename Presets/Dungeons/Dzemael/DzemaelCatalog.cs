using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DzemaelCatalog
{
    internal const uint TERRITORY = 1330;
    internal const uint INVINCIBILITY = 4410;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x4A8A, "All-seeing Eye", p => DzemaelArenas.AllSeeingEye(p.Y),
        [
            new(45569, 0x4A8A, "EyesOnMe", null, "全屏伤害"),
            new(45570, 0x4A8A, "BlusteringBlink", RayRectangleTemplate.Symmetric(10, 55)),
            new(45568, 0x233C, "VoidMatter", new CircleTemplate(10)),
        ], [45567]),
        new(0x4A8B, "Taulurd", p => DzemaelArenas.Taulurd(p.Y),
        [
            new(45571, 0x4A8B, "DoubleSmash", new FanTemplate(8, MathF.PI * 2 / 3)),
            new(45575, 0x4A8C, "Firewater", new CircleTemplate(5)),
        ], [45574, 45573, 45576]),
        new(0x4A8D, "Batraal", p => DzemaelArenas.Batraal(p.Y),
        [
            new(45579, 0x4A8D, "GrimHalo", new CircleTemplate(12)),
            new(45582, 0x4A8D, "Desolation", RayRectangleTemplate.Symmetric(6, 60)),
            new(45583, 0x4A8E, "AetherialSurge", new CircleTemplate(6)),
        ], [45580, 45581, 45577, 45578]),
    ];
}
