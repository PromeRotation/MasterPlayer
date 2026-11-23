using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CoilGauntletCatalog
{
    internal const uint TERRITORY = 244;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x1E86F9, "T04 Gauntlet", p => new CircleGeometry(new(0, p.Y, 0), 25),
        [
            new(1236, 0x7E3, "GravityThrust", null),
            new(1237, 0x7E3, "Pox", null),
        ], [872, 1230, 1231, 1233, 673, 1239, 1238, 1258]),
    ];
}
