using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class AlteRoiteCatalog
{
    internal const uint TERRITORY = 691;
    internal const uint BOSS = 0x1A6F;
    internal const uint ORB = 0x1A71;
    internal static readonly IGeometryShape ARENA = new CircleGeometry(default, 20);
    internal static readonly uint[] ACTIONS = [9180, 9179, 9175, 9182, 9186, 7896, 9173, 9188, 9176, 9185, 9177];

    internal static DungeonDefinition[] Create()
        => [new(BOSS, "欧米茄时空狭缝 德尔塔幻境1", _ => ARENA,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];
}
