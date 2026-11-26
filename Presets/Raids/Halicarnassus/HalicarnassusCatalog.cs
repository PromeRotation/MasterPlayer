using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HalicarnassusCatalog
{
    internal const uint TERRITORY = 693;
    internal const uint BOSS = 0x1E14;
    internal static readonly IGeometryShape ARENA = new RectangleGeometry(default, 0, 40, 40);
    // Source component activation order, not action-number order.
    internal static readonly uint[] ACTIONS = [9318, 9314, 9313, 9312, 9323, 9327, 9329, 9325];

    internal static GeometryTemplate? Shape(uint action) => action switch
    {
        9318 => new FanTemplate(19.5f, 75f * (MathF.PI / 180f) * 2),
        9314 => RayRectangleTemplate.Symmetric(6, 60),
        9313 => new CircleTemplate(9),
        9312 => new DonutTemplate(4, 15),
        9323 => new CircleTemplate(10),
        9327 => new FanTemplate(34, 10f * (MathF.PI / 180f) * 2),
        _ => null,
    };

    internal static DungeonDefinition[] Create()
        => [new(BOSS, "欧米茄时空狭缝 德尔塔幻境3", _ => ARENA,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];
}
