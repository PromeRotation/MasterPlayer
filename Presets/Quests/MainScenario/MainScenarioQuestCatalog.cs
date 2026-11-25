using System.Numerics;
using MasterPlayer.Mechanics.PilgrimsTraverse;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MainScenarioQuestCatalog
{
    internal const uint MEASURE_OF_HIS_REACH = 687;
    internal const uint FADED_MEMORIES = 932;
    internal const uint DEATH_UNTO_DAWN = 977;
    internal const uint ORACLE_OF_LIGHT = 861;
    internal const uint FEAST_OF_LIES = 863;
    internal const uint RHALGRS_BEACON = 671;
    internal const uint WHERE_EVERYTHING_BEGINS = 1091;
    internal const uint FROSTY_RECEPTION = 1010;
    internal const uint GAME_IS_AFOOT = 1177;
    internal const uint SLEEP_DISTURBED = 914;
    internal const uint OTIS_OATHBROKEN = 1213;
    internal const uint HIS_FORGOTTEN_HOME = 634;
    internal const uint MAX_QUEST = 141;
    internal const uint HOPE_ON_THE_WAVES = 757;
    internal const uint TIME_BETWEEN = 685;
    internal const uint ISEULTALON = 839;
    internal const uint WARMTH_OF_THE_FAMILY = 1244;

    internal static bool IsTerritory(uint territory) => territory is MEASURE_OF_HIS_REACH or FADED_MEMORIES or DEATH_UNTO_DAWN or ORACLE_OF_LIGHT or FEAST_OF_LIES or RHALGRS_BEACON or WHERE_EVERYTHING_BEGINS or FROSTY_RECEPTION or GAME_IS_AFOOT or SLEEP_DISTURBED or OTIS_OATHBROKEN or HIS_FORGOTTEN_HOME or MAX_QUEST or HOPE_ON_THE_WAVES or TIME_BETWEEN or ISEULTALON or WARMTH_OF_THE_FAMILY;
    internal static bool Owns(uint territory, uint oid) => territory switch
    {
        MEASURE_OF_HIS_REACH => oid == 0x1C48,
        FADED_MEMORIES => oid is 0x2F1D or 0x2F1E or 0x2F21 or 0x2F28,
        DEATH_UNTO_DAWN => oid == 0x3202,
        ORACLE_OF_LIGHT => oid == 0x299D,
        FEAST_OF_LIES => oid == 0x295A,
        RHALGRS_BEACON => oid == 0x1A88,
        WHERE_EVERYTHING_BEGINS => oid == 0x39B3,
        FROSTY_RECEPTION => oid == 0x3646,
        GAME_IS_AFOOT => oid == 0x4037,
        SLEEP_DISTURBED => oid == 0x2D3C,
        OTIS_OATHBROKEN => oid == 0x4342,
        HIS_FORGOTTEN_HOME => oid == 0x213A,
        MAX_QUEST => oid == 0x252B,
        HOPE_ON_THE_WAVES => oid == 0x21B1,
        TIME_BETWEEN => oid == 0x1A36,
        ISEULTALON => oid == 0x252C,
        WARMTH_OF_THE_FAMILY => oid == 0x4632,
        _ => false,
    };

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        WARMTH_OF_THE_FAMILY => [new(0x4632, "Alpha Alligator", p => new WorldPolygonGeometry(
        [
            new(423.82f, p.Y, -140.6f), new(424.36f, p.Y, -140.47f), new(424.85f, p.Y, -140.23f), new(425.30f, p.Y, -139.93f),
            new(425.72f, p.Y, -139.59f), new(426.11f, p.Y, -139.22f), new(428.76f, p.Y, -135.75f), new(431.02f, p.Y, -134.76f),
            new(431.56f, p.Y, -134.39f), new(432.07f, p.Y, -134.19f), new(432.65f, p.Y, -134.45f), new(433.39f, p.Y, -134.54f),
            new(433.85f, p.Y, -134.25f), new(434.04f, p.Y, -133.76f), new(433.22f, p.Y, -132.93f), new(433.21f, p.Y, -132.34f),
            new(434.86f, p.Y, -128.51f), new(436.71f, p.Y, -120.31f), new(436.79f, p.Y, -119.61f), new(436.78f, p.Y, -118.91f),
            new(433.47f, p.Y, -109.77f), new(432.97f, p.Y, -109.24f), new(427.74f, p.Y, -105.89f), new(427.25f, p.Y, -105.74f),
            new(421.41f, p.Y, -104.22f), new(420.90f, p.Y, -103.86f), new(420.39f, p.Y, -102.69f), new(419.25f, p.Y, -102.22f),
            new(418.97f, p.Y, -101.65f), new(406.66f, p.Y, -81.87f), new(401.18f, p.Y, -81.39f), new(400.67f, p.Y, -81.47f),
            new(400.29f, p.Y, -81.88f), new(397.38f, p.Y, -88.17f), new(395.23f, p.Y, -98.38f), new(395.06f, p.Y, -98.95f),
            new(389.16f, p.Y, -103.32f), new(389.03f, p.Y, -103.81f), new(388.7f, p.Y, -106.85f), new(388.77f, p.Y, -107.49f),
            new(388.92f, p.Y, -108.11f), new(389.11f, p.Y, -108.73f), new(389.38f, p.Y, -109.32f), new(390.8f, p.Y, -111.52f),
            new(404.33f, p.Y, -119.98f), new(405.71f, p.Y, -121.94f), new(405.65f, p.Y, -122.46f), new(405.73f, p.Y, -123.2f),
            new(406.51f, p.Y, -124.25f), new(406.71f, p.Y, -124.75f), new(406.61f, p.Y, -125.40f), new(407.32f, p.Y, -126.66f),
            new(407.47f, p.Y, -127.25f), new(407.42f, p.Y, -127.82f), new(406.67f, p.Y, -128.79f), new(406.44f, p.Y, -129.39f),
            new(407.78f, p.Y, -134.79f), new(408.15f, p.Y, -135.35f), new(420.82f, p.Y, -140.11f), new(423.33f, p.Y, -140.59f),
        ], new Vector3((388.7f + 436.79f) * .5f, p.Y, (-140.6f - 81.39f) * .5f)),
        [
            new(40562, 0, "Toxic Spit clock", null),
            new(40563, 0, "Critical Bite", new FanTemplate(10, MathF.PI * 2 / 3)),
        ], [])],
        ISEULTALON => [new(0x252C, "Iseultalon", p => new CircleGeometry(new(-289, p.Y, -30), 25),
        [
            new(14605, 0, "Stomp", null, "全屏伤害"),
            new(14602, 0, "Death Ray", null),
            new(14603, 0, "Death Ray", RayRectangleTemplate.Symmetric(2, 40)),
            new(14606, 0, "Needle Shot", null),
            new(14607, 0, "Tail Whip", new FanTemplate(12, MathF.PI * 1.5f)),
            new(14609, 0, "Electrocution", null),
            new(14610, 0, "Fatal Current observation", null),
            new(14981, 0, "Electrocution", null),
        ], [])],
        MEASURE_OF_HIS_REACH => [new(0x1C48, "Hakuro Whitefang", p => new CircleGeometry(new(504, p.Y, -133), 20),
        [
            new(8397, 0, "Howling Icewind", RayRectangleTemplate.Symmetric(4, 44)),
            new(8398, 0, "Howling Moonlight", new CircleTemplate(10)),
            new(8450, 0, "Dragonspirit", new CircleTemplate(7.5f)),
            new(8399, 0, "Howling Bloomshower", new PtDonutSectorTemplate(4, 8, MathF.PI / 2)),
        ], [])],
        FADED_MEMORIES =>
        [
            new(0x2F1D, "King Thordan", p => new CircleGeometry(new(-247, p.Y, 321), 20),
            [
                new(21090, 0, "The Dragon's Gaze", null),
                new(21086, 0, "Hyperdimensional Slash", RayRectangleTemplate.Symmetric(8, 45)),
            ], []),
            new(0x2F28, "Zenos yae Galvus", p => new CircleGeometry(new(-321.03f, p.Y, 617.73f), 20),
            [
                new(21117, 0, "Entropic Flame", RayRectangleTemplate.Symmetric(8, 50)),
                new(21118, 0, "Vein Splitter", new CircleTemplate(10)),
                new(21121, 0, "The Final Art", null, "全屏伤害"),
            ], []),
            new(0x2F1E, "Flame General Aldynn", p => new CircleGeometry(new(-143, p.Y, 357), 20),
            [
                new(21094, 0, "Flaming Tizona", new CircleTemplate(6)),
                new(21096, 0, "Holy Bladedance", RayRectangleTemplate.Symmetric(3, 5)),
            ], []),
            new(0x2F21, "Nidhogg", p => new CircleGeometry(new(-242, p.Y, 436.5f), 20),
            [
                new(21299, 0, "High Jump", new CircleTemplate(8)),
                new(21098, 0, "Geirskogul", RayRectangleTemplate.Symmetric(8, 62)),
            ], []),
        ],
        DEATH_UNTO_DAWN => [new(0x3202, "Lunar Ifrit", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(24057, 0, "Radiant Plume", new CircleTemplate(8)),
            new(24054, 0, "Crimson Cyclone", RayRectangleTemplate.Symmetric(18, 49)),
            new(24046, 0, "Explosion", new DungeonCrossTemplate(80, 10)),
            new(24058, 0, "Hellfire", null, "全屏伤害"),
            new(24059, 0, "Hellfire", null, "全屏伤害"),
            new(24062, 0, "Agony of the Damned", null, "全屏伤害"),
        ], [])],
        ORACLE_OF_LIGHT => [new(0x299D, "Ran'jit", p => new CircleGeometry(new(126.75f, p.Y, -311.25f), 20),
        [
            new(17622, 0, "Hot Pursuit", new CircleTemplate(5)),
            new(17621, 0, "Nexus of Thunder", RayRectangleTemplate.Symmetric(5, 60.5f)),
            new(17823, 0, "Nexus of Thunder", RayRectangleTemplate.Symmetric(5, 60.5f)),
            new(18035, 0, "Burn", new CircleTemplate(8)),
            new(18036, 0, "Unbridled Wrath", null),
        ], [])],
        FEAST_OF_LIES => [new(0x295A, "Ran'jit", p => new CircleGeometry(new(0, p.Y, 18), 15),
        [
            new(16274, 0, "Unceremonious Beheading", new CircleTemplate(10)),
            new(16275, 0, "Katun Cycle", new DonutTemplate(5, 40)),
            new(16283, 0, "Merciless Right", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(16284, 0, "Merciless Right", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(16298, 0, "Merciless Left", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(16297, 0, "Merciless Left", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(16277, 0, "Evisceration", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(16285, 0, "Hot Pursuit", new CircleTemplate(5)),
            new(16276, 0, "Nexus of Thunder", RayRectangleTemplate.Symmetric(5, 45)),
            new(16296, 0, "Nexus of Thunder", RayRectangleTemplate.Symmetric(5, 45)),
            new(16290, 0, "Burn", new CircleTemplate(8)),
            new(16286, 0, "Electrocution", new CircleTemplate(6)),
            new(16292, 0, "Spiritcall", null),
        ], [])],
        RHALGRS_BEACON => [new(0x1A88, "Fordola rem Lupis", p => new CircleGeometry(new(-195.25f, p.Y, 147.5f), 20),
        [
            new(8340, 0, "Diffractive Laser", new FanTemplate(18.6f, MathF.PI / 3)),
            new(8346, 0, "Chopping Block", null),
            new(8310, 0, "Gunblade", null),
            new(8370, 0, "The Order", null),
        ], [])],
        WHERE_EVERYTHING_BEGINS => [new(0x39B3, "Scarmiglione", p => EndwalkerQuestArenas.Scarmiglione(p.Y),
        [
            new(30021, 0, "Nox", new CircleTemplate(10)),
            new(30023, 0, "Void Gravity", null),
            new(30025, 0, "Void Vortex", null),
        ], [])],
        FROSTY_RECEPTION => [new(0x3646, "Vergilia van Corculum", p => EndwalkerQuestArenas.Vergilia(p.Y),
        [
            new(27440, 0, "Giga Tempest", null, "全屏伤害"),
            new(27443, 0, "Ruination", new DungeonCrossTemplate(40, 8)),
            new(27444, 0, "Ruination", RayRectangleTemplate.Symmetric(8, 30)),
            new(27449, 0, "Resin Bomb", null),
            new(27457, 0, "Magitek Cannon", null),
            new(27459, 0, "Bombardment", new CircleTemplate(6)),
            new(27461, 0, "Lock On", null),
            new(27463, 0, "Lock On", null),
        ], [])],
        GAME_IS_AFOOT => [new(0x4037, "Giant Colibri", p => EndwalkerQuestArenas.Colibri(p.Y),
        [
            new(34886, 0, "Pecking Flurry", null, "全屏伤害"),
            new(34883, 0, "Wind Unbound", null, "全屏伤害"),
            new(34884, 0, "Snatch Morsel", null, "死刑"),
            new(34888, 0, "Falling Rock", new CircleTemplate(6)),
            new(34890, 0, "Sticky Spit", null),
            new(34893, 0, "Furling Flapping", null),
            new(35717, 0, "Swoop", null),
            new(35888, 0, "Deadly Swoop", null),
        ], [])],
        SLEEP_DISTURBED => [new(0x2D3C, "Huaca", p => new RectangleGeometry(new(100, p.Y, 100), 0, 39, 39),
        [
            new(19617, 0, "The Touch of Shadow", null, "全屏伤害"),
            new(19613, 0, "The Marrow of Flame", null),
            new(19615, 0, "The Grace of Calamity", null),
            new(19618, 0, "The Sound of Heat", new FanTemplate(60, MathF.PI / 3)),
            new(19629, 0, "The Deceit of Pain", new CircleTemplate(14)),
            new(19627, 0, "The Balm of Disgrace", new CircleTemplate(12)),
        ], [])],
        OTIS_OATHBROKEN => [new(0x4342, "Otis Oathbroken", p => OtisArena(p.Y),
        [
            new(38202, 0, "Stormlit Shockwave", null, "全屏伤害"),
            new(38203, 0, "Valorous Ascension", null, "全屏伤害"),
            new(38200, 0, "Rend Power", new FanTemplate(40, MathF.PI / 6)),
            new(38197, 0, "Searing Slash", new CircleTemplate(8)),
            new(38205, 0, "Thrown Flames", new CircleTemplate(8)),
            new(38198, 0, "Bastion Breaker", null),
            new(38199, 0, "Holy Blade", null),
            new(38207, 0, "Electrobeam", RayRectangleTemplate.Symmetric(4, 40)),
            new(38201, 0, "Steadfast Will", null, "单体伤害"),
            new(38209, 0, "Rush", null),
        ], [])],
        HIS_FORGOTTEN_HOME => [new(0x213A, "Slickshell Captain", p => new WorldPolygonGeometry(
        [
            new(464.25f, p.Y, 320.19f), new(455.65f, p.Y, 313.35f), new(457.72f, p.Y, 308.20f),
            new(445, p.Y, 292.92f), new(468.13f, p.Y, 283.56f), new(495.55f, p.Y, 299.63f),
            new(487.19f, p.Y, 313.73f),
        ], new Vector3((445f + 495.55f) * .5f, p.Y, (283.56f + 320.19f) * .5f)),
        [
            new(5831, 0, "Water III", new CircleTemplate(8)),
            new(8585, 0, "Kasaya", new FanTemplate(7.6f, MathF.PI * 2 / 3)),
            new(1087, 0, "Blizzard III", null),
        ], [])],
        MAX_QUEST => [new(0x252B, "MA-X", p => new CircleGeometry(new(295, p.Y, -22), 25),
        [
            new(14601, 0, "Chainsaw", new FanTemplate(10, MathF.PI / 2)),
            new(14600, 0, "Shock", new CircleTemplate(10)),
            new(14599, 0, "Magitek Missile", new CircleTemplate(5)),
        ], [])],
        HOPE_ON_THE_WAVES => [new(0x21B1, "Imperial Centurion", p => HopeArena(p.Y),
        [
            new(10823, 0, "Assault Cannon", RayRectangleTemplate.Symmetric(2, 75)),
            new(9428, 0, "Circle of Death", new CircleTemplate(10.24f)),
            new(10929, 0, "2-tonze Magitek Missile", new CircleTemplate(6)),
            new(10893, 0, "Magitek Missile", new CircleTemplate(11.75f)),
            new(9425, 0, "Cermet Pile", RayRectangleTemplate.Symmetric(6, 42)),
            new(10928, 0, "Self-detonate", null),
            new(10930, 0, "Self-detonate", new CircleTemplate(6)),
        ], [])],
        TIME_BETWEEN => [new(0x1A36, "Zenos yae Galvus", p => new WorldPolygonGeometry(
        [
            new(-226.91f, p.Y, 523.65f), new(-254.46f, p.Y, 524.46f), new(-254.66f, p.Y, 541.06f),
            new(-269.99f, p.Y, 544.12f), new(-269.58f, p.Y, 565.97f), new(-254.58f, p.Y, 565.89f),
            new(-249.05f, p.Y, 554.06f), new(-229.18f, p.Y, 562.35f),
        ], new Vector3((-269.99f - 226.91f) * .5f, p.Y, (523.65f + 565.97f) * .5f)),
        [
            new(8987, 0, "Vein Splitter", new CircleTemplate(10)),
            new(8986, 0, "Concentrativity", null, "全屏伤害"),
            new(8988, 0, "Lightless Flame", null),
            new(8985, 0, "Lightless Spark", new FanTemplate(40.92f, MathF.PI / 2)),
            new(8993, 0, "Art of the Sword", RayRectangleTemplate.Symmetric(6, 41)),
        ], [])],
        _ => [],
    };

    internal static IGeometryShape HopeArena(float height)
    {
        // Same float operations as source Ellipse. The bounds use the contour AABB center.
        var increment = MathF.Tau / 50;
        var (sinRotation, cosRotation) = Math.SinCos(140 * (MathF.PI / 180));
        var points = Enumerable.Range(0, 50).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * increment);
            var x = 34 * (float)cos;
            var z = 21 * (float)sin;
            return new Vector3(x * (float)cosRotation - z * (float)sinRotation + 473.25f, height,
                x * (float)sinRotation + z * (float)cosRotation + 751.75f);
        }).ToArray();
        var center = new Vector3((points.Min(p => p.X) + points.Max(p => p.X)) * .5f, height,
            (points.Min(p => p.Z) + points.Max(p => p.Z)) * .5f);
        return new WorldPolygonGeometry(points, center);
    }

    internal static IGeometryShape OtisArena(float height)
    {
        var points = Enumerable.Range(0, 20).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * (Math.PI * 2 / 20));
            return new Vector3(349 + 19.5f * (float)sin, height, -14 + 19.5f * (float)cos);
        });
        return new WorldPolygonGeometry(points, new Vector3(349, height, -14));
    }
}
