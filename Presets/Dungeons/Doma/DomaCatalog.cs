using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DomaCatalog
{
    internal const uint TERRITORY = 1144;
    internal static bool Owns(uint oid) => oid is 0x1BCC or 0x1BD0 or 0x1BD4;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1BCC, "Magitek Rearguard", p => new RectangleGeometry(new(124.64f, p.Y, 17.54f), 76.5f * MathF.PI / 180, 39, 39),
        [
            new(8350, 0x1BCC, "GarleanFireVisual", null),
            new(8351, 0x1BCD, "GarleanFire", new CircleTemplate(6)),
            new(8353, 0x1BCF, "MagitekRay", RayRectangleTemplate.Symmetric(2, 45.9f)),
            new(8352, 0x1BCE, "SelfDetonate", new CircleTemplate(6)),
        ], [872, 8349]),
        new(0x1BD0, "Magitek Hexadrone", p => new RectangleGeometry(new(-240, p.Y, 130.5f), 0, 39, 39),
        [
            new(8354, 0x1BD0, "CircleOfDeath", new CircleTemplate(8.24f)),
            new(8356, 0x1BD0, "MagitekMissilesVisual", null),
            new(8357, 0x1BD1, "MagitekMissiles", null, "每塔一人"),
        ], [8501, 8355, 8358, 9287, 8359]),
        new(0x1BD4, "Hypertuned Grynewaht", p => new RectangleGeometry(new(-240, p.Y, -198), 0, 39, 39),
        [
            new(8369, 0x1BD8, "CleanCut", null),
            new(8360, 0x1BD4, "Chainsaw", RayRectangleTemplate.Symmetric(2, 4.6f)),
            new(8367, 0x1BD5, "ThermobaricCharge", new CircleTemplate(30)),
        ], [870, 8361, 8364, 8365, 8362, 8363, 8366, 8368, 9287, 9144, 8359]),
    ];
}
