using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class WickedThunderCatalog
{
    internal const uint TERRITORY = 1231;
    internal const uint BOSS = 0x4263;
    internal static readonly Vector3 CENTER = new(100, 0, 100);
    internal static readonly IGeometryShape ARENA = new RectangleGeometry(CENTER, 0, 40, 40);
    internal static readonly uint[] ACTIONS = [37560, 37561, 37564, 37565, 37566, 37567, 39429, 39439,
        37574, 37573, 37576, 37575, 37549, 37552, 39759, 39765, 39766, 39767, 37553, 37554, 37557];

    internal static DungeonDefinition[] Create()
        => [new(BOSS, "阿卡狄亚轻量级4（普通）", _ => ARENA,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];
}
