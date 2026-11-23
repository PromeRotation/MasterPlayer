using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SastashaHardCatalog
{
    internal const uint TERRITORY = 387;

    internal static DungeonDefinition[] Create() =>
    [
        new(0xCBA, "Karlabos", p => new CircleGeometry(new(166, p.Y, -85), 21),
        [
            new(3060, 0xCBA, "TailScrew", null, "螺旋尾：使用眩晕阻止，不能用打断"),
        ], []),
        new(0xCBB, "Captain Madison", p => new RectangleGeometry(new(-103, p.Y, 136), 0, 36, 40),
        [
            new(1099, 0xCBC, "Bloodstain", new CircleTemplate(7.5f)),
            new(3062, 0xCBB, "Hornswaggle", new FanTemplate(5, MathF.PI / 2)),
            new(1299, 0xCBD, "Rive", RayRectangleTemplate.Symmetric(3, 18)),
        ], []),
    ];
}
