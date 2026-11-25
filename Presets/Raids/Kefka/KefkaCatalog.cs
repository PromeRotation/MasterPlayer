using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class KefkaCatalog
{
    internal const uint TERRITORY = 751;
    internal static readonly CircleGeometry ARENA = new(default, 20);
    internal static readonly uint[] ACTIONS =
        [10542, 10516, 10521, 10518, 10520, 10527, 10528, 10524, 10525, 10541, 10530, 10532, 10535, 10536, 10537, 10538, 10539, 10540];

    internal static DungeonDefinition[] Create()
        => [new(0x2153, "凯夫卡（普通）", _ => ARENA,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];
}
