using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HydaelynCatalog
{
    internal const uint TERRITORY = 995;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x34EA, "Hydaelyn", p => new LocalPolygonGeometry(new(100, p.Y, 100), 0,
            Enumerable.Range(0, 48).Select(i =>
            {
                var (sin, cos) = Math.SinCos(i * (Math.PI * 2 / 48));
                return new Vector2((float)sin * 20, (float)cos * 20);
            })),
        [
            new(26071, 0, "HerossRadiance", null, "全屏伤害"),
            new(26072, 0, "MagossRadiance", null, "全屏伤害"),
            new(26064, 0, "RadiantHalo", null, "全屏伤害"),
            new(26070, 0, "MousasScorn", null),
            new(26069, 0, "HerossSundering", null),
            new(27737, 0, "CrystallineStoneIII", null),
            new(27738, 0, "CrystallineBlizzardIII", null),
            new(26062, 0, "Beacon1", null),
            new(26063, 0, "Beacon2", null),
            new(26060, 0, "HydaelynsRay", RayRectangleTemplate.Symmetric(30, 45)),
            new(26061, 0, "Incandescence", null),
        ],
        [870, 27732, 27733, 26025, 28282, 26030, 27660, 26056, 26055, 26051, 26255,
            26067, 26015, 27729, 27855, 26016, 27853, 27854, 26043, 26066, 27911,
            26012, 26028, 26029, 26014, 26017, 26032, 26033, 27734, 26037, 26038,
            26039, 26065, 26058, 26260, 26261, 26259]),
    ];
}
