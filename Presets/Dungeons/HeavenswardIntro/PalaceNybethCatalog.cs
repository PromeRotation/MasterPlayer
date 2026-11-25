using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    internal const string NYBETH_GUIDE = "There is 3 sets of adds that spawn at HP %'s -> (90%, 65%, 40%) \nA resolution can make the adds permanently disappear once they are at 0% HP/the corpse are just laying on the floor.\nResolution is also does high damage to the adds + 0.3% to the Boss\nSolo tip: Either pop a resolution on all add packs, or pop lust -> resolution on 2nd ad pack. Make sure to keep regen up!";

    private static DungeonDefinition NybethDefinition() => new(0x1808, "Nybeth Obdilord", p => NybethArena(p.Y),
    [
        new(6872, 0, "Abyss", null),
        new(6878, 0, "Catapult", new CircleTemplate(6)),
        new(6875, 0, "Doom", new FanTemplate(47.4f, 2 * MathF.PI / 3)),
        new(6874, 0, "Shackle", RayRectangleTemplate.Symmetric(8, 52.4f)),
        new(6876, 0, "Summon Darkness", null, "Summoning the corse, incoming Adds! \nRemember to use a resolution to make them permanently disappear"),
    ], []);

    private static LocalPolygonGeometry NybethArena(float height)
    {
        // DD100NybethObdilord.vertices, including the source closing vertex.
        Vector2[] world =
        [
            new(302.11f, 276.34f), new(307.39f, 276.36f), new(308.04f, 276.58f), new(309.51f, 278.03f),
            new(310.04f, 278.17f), new(311.94f, 278.24f), new(312.52f, 278.61f), new(321.65f, 287.79f),
            new(321.81f, 288.44f), new(321.9f, 290.34f), new(324.01f, 292.58f), new(324.28f, 293.17f),
            new(324.29f, 306.71f), new(324.08f, 307.35f), new(321.86f, 309.64f), new(321.77f, 312.15f),
            new(312.3f, 321.62f), new(311.60f, 321.80f), new(309.56f, 321.92f), new(307.32f, 324.12f),
            new(306.66f, 324.29f), new(300.1f, 324.22f), new(293.11f, 324.29f), new(292.52f, 323.96f),
            new(290.57f, 322.02f), new(290.01f, 321.88f), new(288.08f, 321.81f), new(287.52f, 321.42f),
            new(278.22f, 312.13f), new(278.14f, 310.01f), new(278f, 309.45f), new(275.71f, 307.13f),
            new(275.73f, 293.36f), new(275.86f, 292.68f), new(277.75f, 290.77f), new(278.12f, 290.27f),
            new(278.20f, 288.37f), new(278.33f, 287.73f), new(287.51f, 278.59f), new(288.05f, 278.21f),
            new(290.12f, 278.13f), new(290.61f, 277.91f), new(291.97f, 276.56f), new(302.11f, 276.34f),
        ];
        var center = new Vector3((world.Min(p => p.X) + world.Max(p => p.X)) / 2, height,
            (world.Min(p => p.Y) + world.Max(p => p.Y)) / 2);
        return new(center, 0, world.Select(p => new Vector2(center.X - p.X, center.Z - p.Y)));
    }
}
