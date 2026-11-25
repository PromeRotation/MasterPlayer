using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static IGeometryShape NingishzidaArena(float height)
    {
        Vector2[] world =
        [
            new(-292.06f, -260.52f), new(-291.70f, -259.98f), new(-283f, -253.77f), new(-278.9f, -248.62f),
            new(-276.9f, -243.92f), new(-276.7f, -243.32f), new(-275.95f, -237.58f), new(-275.96f, -236.90f),
            new(-276.71f, -231.18f), new(-279.11f, -225.31f), new(-282.9f, -220.35f), new(-283.44f, -219.89f),
            new(-287.65f, -216.66f), new(-288.24f, -216.30f), new(-293.88f, -214f), new(-299.76f, -213.22f),
            new(-300.41f, -213.24f), new(-306.18f, -214f), new(-312.06f, -216.44f), new(-316.71f, -220.01f),
            new(-317.2f, -220.47f), new(-320.75f, -225.10f), new(-321.04f, -225.68f), new(-323.28f, -231.07f),
            new(-324.03f, -236.80f), new(-324.06f, -237.44f), new(-323.29f, -243.30f), new(-323.08f, -243.94f),
            new(-321.31f, -248.22f), new(-317.93f, -252.79f), new(-317.48f, -253.28f), new(-312.6f, -257.12f),
            new(-307.27f, -259.75f), new(-307.11f, -260.30f),
        ];
        var center = new Vector3((world.Min(p => p.X) + world.Max(p => p.X)) / 2, height,
            (world.Min(p => p.Y) + world.Max(p => p.Y)) / 2);
        var polygon = new LocalPolygonGeometry(center, 0, world.Select(p => new Vector2(center.X - p.X, center.Z - p.Y)));
        var entrance = new Vector2(-299.312f - center.X, -261.765f - center.Z);
        Vector2[] rectangle = [entrance + new Vector2(-20, -1.325f), entrance + new Vector2(20, -1.325f),
            entrance + new Vector2(20, 1.325f), entrance + new Vector2(-20, 1.325f)];
        return RecenterPalaceRegion((PolygonRegionGeometry)DungeonPolygonDifference.Build(polygon,
            [(Array.Empty<Vector2>(), rectangle)]));
    }

    private static IGeometryShape AlfardArena(float height)
    {
        Vector2[] world =
        [
            new(-290.26f, -259.65f), new(-289.73f, -259.2f), new(-288.76f, -258.22f), new(-288.25f, -257.85f),
            new(-284.66f, -255.87f), new(-284.15f, -255.44f), new(-282.3f, -252.9f), new(-280.65f, -251.85f),
            new(-280.23f, -251.36f), new(-279.47f, -250.25f), new(-278.54f, -249.28f), new(-278.25f, -248.68f),
            new(-277.55f, -245.63f), new(-276.36f, -243.41f), new(-275.84f, -241.48f), new(-275.28f, -232.23f),
            new(-275.59f, -230.28f), new(-276.77f, -227.32f), new(-277.83f, -225.62f), new(-277.95f, -224.33f),
            new(-278.1f, -223.7f), new(-279.39f, -221.36f), new(-279.83f, -220.85f), new(-280.9f, -220.21f),
            new(-281.89f, -218.63f), new(-283.28f, -217.17f), new(-288.34f, -213.95f), new(-290.24f, -213.48f),
            new(-295.71f, -211.32f), new(-296.39f, -211.21f), new(-297.68f, -211.29f), new(-298.33f, -211.27f),
            new(-300.34f, -211.04f), new(-303.69f, -211.33f), new(-305.68f, -211.77f), new(-307.7f, -212.09f),
            new(-309.55f, -212.77f), new(-310.66f, -213.4f), new(-311.62f, -214.33f), new(-316.43f, -217.31f),
            new(-317.33f, -218.08f), new(-317.98f, -219.25f), new(-318.42f, -219.61f), new(-319f, -219.93f),
            new(-319.4f, -220.49f), new(-319.62f, -221.13f), new(-319.97f, -221.66f), new(-320.42f, -222.15f),
            new(-320.8f, -222.73f), new(-321.29f, -223.91f), new(-321.64f, -224.43f), new(-322.48f, -225.44f),
            new(-324.76f, -234.26f), new(-324.34f, -240.62f), new(-324.21f, -241.3f), new(-323.76f, -242.46f),
            new(-322.69f, -246.47f), new(-320.62f, -248.97f), new(-319.69f, -250.83f), new(-319.32f, -251.38f),
            new(-317.59f, -253.47f), new(-313.43f, -256.21f), new(-312.98f, -256.61f), new(-312.12f, -257.62f),
            new(-311.61f, -258.06f), new(-308.02f, -259.6f),
        ];
        var center = new Vector3((world.Min(p => p.X) + world.Max(p => p.X)) / 2, height,
            (world.Min(p => p.Y) + world.Max(p => p.Y)) / 2);
        var polygon = new LocalPolygonGeometry(center, 0, world.Select(p => new Vector2(center.X - p.X, center.Z - p.Y)));
        return polygon;
    }
}
