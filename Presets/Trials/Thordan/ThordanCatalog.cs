using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ThordanCatalog
{
    internal const uint TERRITORY = 448;
    internal static readonly IGeometryShape STARTING_ARENA = ThordanArenas.Starting(0);
    internal static readonly IGeometryShape REDUCED_ARENA = ThordanArenas.Reduced(0);
    internal static readonly uint[] ACTIONS =
    [
        5246, 5247, 5248, 5249, 5250, 5251, 5252, 5253, 5254, 5255, 5256, 5257, 5258, 5259,
        5260, 5261, 5262, 5263, 5264, 5265, 5266, 5267, 5268, 5269, 5270, 5271, 5272,
        5273, 5274, 5275, 5276, 5277, 5278, 5279, 5280, 5281, 5282, 5283, 5284, 5285,
        5286, 5287, 5289, 5290, 5291, 5292, 5294, 5295, 5296, 5297, 5298, 5299, 5300, 5301,
    ];

    internal static DungeonDefinition[] Create()
        => [new(0x1453, "圆桌骑士（极）", _ => STARTING_ARENA,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];

}
