using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CaduceusCatalog
{
    internal static DungeonDefinition Create() => new(0x7D7, "Caduceus",
        p => new RectangleGeometry(new Vector3(-26, p.Y, -407), 0, 70, 86),
        [
            new(1209, 0, "WhipBack", new FanTemplate(9, MathF.PI * 2 / 3)),
            new(1210, 0, "Regorge", null),
            new(1214, 0, "Syrup", null),
        ], [1207, 1208, 1211, 674, 872, 1213, 1454]);
}
