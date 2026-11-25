using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    private static LocalPolygonGeometry AhPuchArena(float height)
    {
        // Exact SharedBounds140150 outline; preserve its concave edge details.
        Vector2[] world =
        [
            new(-289.68f, -259.66f), new(-289.06f, -259.36f), new(-285.88f, -257.19f), new(-283.86f, -255.6f),
            new(-281.04f, -252.7f), new(-280.77f, -252.07f), new(-278.87f, -250.28f), new(-278.04f, -249.24f),
            new(-278.14f, -247.41f), new(-277.98f, -246.78f), new(-277.25f, -244.87f), new(-276.94f, -244.36f),
            new(-276.19f, -243.32f), new(-275.96f, -242.68f), new(-275.92f, -241.34f), new(-275.78f, -240.75f),
            new(-275.33f, -239.55f), new(-275.29f, -238.86f), new(-275.6f, -237.62f), new(-275.65f, -237.01f),
            new(-275.62f, -235.77f), new(-275.7f, -235.09f), new(-276.09f, -233.81f), new(-276.21f, -233.2f),
            new(-276.13f, -231.95f), new(-276.17f, -231.28f), new(-279.76f, -223.87f), new(-280.65f, -222.97f),
            new(-281.01f, -222.51f), new(-281.31f, -220.71f), new(-282.75f, -219.31f), new(-284.52f, -218.47f),
            new(-285.02f, -218.13f), new(-286.34f, -216.8f), new(-286.89f, -216.4f), new(-288.05f, -215.96f),
            new(-288.69f, -215.81f), new(-290.06f, -215.67f), new(-290.6f, -215.46f), new(-293.81f, -213.31f),
            new(-294.4f, -213.05f), new(-296.26f, -212.86f), new(-298.16f, -212.46f), new(-300.13f, -212.43f),
            new(-302.04f, -212.64f), new(-305.96f, -213.71f), new(-307.29f, -213.74f), new(-307.94f, -213.82f),
            new(-309.72f, -214.46f), new(-310.38f, -214.57f), new(-311.02f, -214.87f), new(-311.59f, -215.27f),
            new(-312.45f, -216.22f), new(-315.35f, -218.04f), new(-317.22f, -220.02f), new(-319.38f, -221.39f),
            new(-320.88f, -223.48f), new(-321.21f, -224.04f), new(-321.43f, -225.45f), new(-321.65f, -226f),
            new(-322.85f, -228.25f), new(-323.03f, -228.9f), new(-323.01f, -229.49f), new(-323.71f, -231.17f),
            new(-323.89f, -231.8f), new(-324.12f, -233.77f), new(-324.28f, -234.4f), new(-324.38f, -235.08f),
            new(-324.25f, -236.32f), new(-324.67f, -237.5f), new(-324.59f, -238.9f), new(-324.06f, -240.96f),
            new(-324.03f, -241.58f), new(-324.1f, -242.86f), new(-323.94f, -243.51f), new(-323.24f, -245.28f),
            new(-323.12f, -245.93f), new(-321.47f, -248.78f), new(-321.06f, -249.98f), new(-320.8f, -250.57f),
            new(-319.71f, -252.23f), new(-316.45f, -254.46f), new(-314.86f, -256.49f), new(-313.24f, -257.64f),
            new(-312.55f, -258.64f), new(-312.09f, -259.12f), new(-311.56f, -259.5f), new(-310.91f, -259.67f),
        ];
        var center = new Vector3((world.Min(p => p.X) + world.Max(p => p.X)) / 2, height,
            (world.Min(p => p.Y) + world.Max(p => p.Y)) / 2);
        return new(center, 0, world.Select(p => new Vector2(center.X - p.X, center.Z - p.Y)));
    }
}
