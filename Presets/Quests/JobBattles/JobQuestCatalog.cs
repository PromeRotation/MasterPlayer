using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Mechanics.PilgrimsTraverse;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class JobQuestCatalog
{
    internal const uint BLOOD_ON_THE_DECK = 708;
    internal const uint MIND_OVER_MANOR = 1233;
    internal const uint SOMEWHERE_ONLY_SHE_KNOWS = 1234;
    internal const uint FANGS_OF_THE_VIPER = 1235;
    internal const uint FACE_OF_TRUE_EVIL = 709;
    internal const uint SAGES_FOCUS = 1022;
    internal const uint ORPHANS_AND_BROKEN_BLADE = 715;
    internal const uint BATTLE_ON_BEKKO = 711;
    internal const uint KILLING_ART = 1021;
    internal const uint OUR_UNSUNG_HEROES = 722;
    internal const uint LIFE_EPHEMERAL = 1023;
    internal const uint POWER_TO_PROTECT = 648;
    internal const uint ART_FOR_THE_LIVING = 702;
    internal const uint STEEL_AGAINST_STEEL = 866;
    internal const uint RAISING_THE_SWORD = 706;
    internal const uint GAMBOLING_FOR_GIL = 867;
    internal const uint HEROES_AND_PRETENDERS = 1218;
    internal const uint TO_HAVE_LOVED_AND_LOST = 869;
    internal const uint BAR_THE_PASSAGE = 1246;

    internal static bool IsTerritory(uint territory) => territory is BLOOD_ON_THE_DECK or MIND_OVER_MANOR or SOMEWHERE_ONLY_SHE_KNOWS or FANGS_OF_THE_VIPER or FACE_OF_TRUE_EVIL or SAGES_FOCUS or ORPHANS_AND_BROKEN_BLADE or BATTLE_ON_BEKKO or KILLING_ART or OUR_UNSUNG_HEROES or LIFE_EPHEMERAL or POWER_TO_PROTECT or ART_FOR_THE_LIVING or STEEL_AGAINST_STEEL or RAISING_THE_SWORD or GAMBOLING_FOR_GIL or HEROES_AND_PRETENDERS or TO_HAVE_LOVED_AND_LOST or BAR_THE_PASSAGE;
    internal static bool Owns(uint territory, uint oid) => territory switch
    {
        BLOOD_ON_THE_DECK => oid == 0x1BED,
        MIND_OVER_MANOR => oid == 0x4292,
        SOMEWHERE_ONLY_SHE_KNOWS => oid is 0x4293 or 0x4296 or 0x4298,
        FANGS_OF_THE_VIPER => oid == 0x429F,
        FACE_OF_TRUE_EVIL => oid == 0x1BEE,
        SAGES_FOCUS => oid == 0x3587,
        ORPHANS_AND_BROKEN_BLADE => oid == 0x1C5E,
        BATTLE_ON_BEKKO => oid == 0x1BF8,
        KILLING_ART => oid == 0x3664,
        OUR_UNSUNG_HEROES => oid == 0x1CAF,
        LIFE_EPHEMERAL => oid is 0x35C5 or 0x35C6,
        POWER_TO_PROTECT => oid == 0x1BCB,
        ART_FOR_THE_LIVING => oid == 0x1CBA,
        STEEL_AGAINST_STEEL => oid == 0x2A45,
        RAISING_THE_SWORD => oid == 0x1B51,
        GAMBOLING_FOR_GIL => oid == 0x29D2,
        HEROES_AND_PRETENDERS => oid == 0x428A,
        TO_HAVE_LOVED_AND_LOST => oid == 0x2927,
        BAR_THE_PASSAGE => oid is 0x465D or 0x4660 or 0x466A,
        _ => false,
    };

    internal static IGeometryShape BarPassageArena(float y, bool small)
    {
        var z = small ? 379.241f : 379.27f;
        var radius = small ? 20 : 24.5f;
        var sides = small ? 64 : 20;
        return new WorldPolygonGeometry(Enumerable.Range(0, sides).Select(i =>
            new Vector3(radius * MathF.Cos(i * MathF.Tau / sides), y, z + radius * MathF.Sin(i * MathF.Tau / sides))),
            new(0, y, z));
    }

    internal static bool InGroup(uint primary, uint oid) => primary switch
    {
        0x4293 => oid is 0x4293 or 0x4294 or 0x4295,
        0x4296 => oid is 0x4296 or 0x4297,
        _ => false,
    };

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        BAR_THE_PASSAGE => [new(0x465D, "Bar the Passage P1", p => new RectangleGeometry(new(0, p.Y, -336.3f), 0, 69.2f, 87.4f),
        [
            new(40896, 0, "Nopal Fan", RayRectangleTemplate.Symmetric(4, 15)),
            new(40898, 0, "Pulverizing Pound", new CircleTemplate(5)),
            new(40901, 0, "Body Slam", new CircleTemplate(6)),
            new(41787, 0, "Predatory Swoop", new CircleTemplate(6)),
            new(40900, 0, "Zillion Needles", new CircleTemplate(4)),
            new(41960, 0, "Flock Frenzy", null, "全屏伤害"),
            new(41125, 0, "Toxic Spit", null),
        ], []), new(0x4660, "Bar the Passage P2", p => new RectangleGeometry(new(0, p.Y, 28.5f), 0, 69.2f, 47),
        [
            new(40904, 0, "Savage Swipe", new FanTemplate(9, MathF.PI * 2 / 3)),
            new(40905, 0, "111-tonze Swing", new CircleTemplate(10)),
            new(40906, 0, "11-tonze Swipe", new FanTemplate(10, MathF.PI * 2 / 3)),
        ], []), new(0x466A, "Apyaahi the Architect", p => BarPassageArena(p.Y, false),
        [
            new(40914, 0, "Flames of the Falling Order", new CircleTemplate(12)),
            new(40922, 0, "Rotting Roar", null),
            new(40916, 0, "Mycocyclone", null),
            new(40917, 0, "Burst", null),
            new(40918, 0, "Burst", null),
            new(40909, 0, "Pathogenic Power", null),
            new(40910, 0, "Pathogenic Power", null),
            new(40912, 0, "Spore Burst", new CircleTemplate(6)),
            new(40920, 0, "Flesh-boiling Punishment", new DonutTemplate(5, 40)),
            new(40930, 0, "Mycelium Stomp", null),
            new(40927, 0, "Spoiling Smash", null),
            new(40932, 0, "Blazing Blaze of Glory", null),
        ], [])],
        TO_HAVE_LOVED_AND_LOST => [new(0x2927, "Dikaiosyne", p => new CircleGeometry(new(-798.6f, p.Y, -40.49f), 20),
        [
            new(4747, 0, "Bloodstain", new CircleTemplate(5)),
            new(16132, 0, "Brand of Sin", null),
            new(16134, 0, "Blade of Justice", null),
            new(17431, 0, "Sanctified Holy", null),
            new(17427, 0, "Sanctified Holy II", new CircleTemplate(5)),
            new(17430, 0, "Sanctified Holy III", new CircleTemplate(6)),
            new(17552, 0, "Heretic's Fork", new DungeonCrossTemplate(40, 3)),
            new(17470, 0, "Heretic's Quoit", new DonutTemplate(5, 15)),
            new(4746, 0, "Spirits Without", RayRectangleTemplate.Symmetric(3, 3.5f)),
            new(16131, 0, "Seraph Blade", new FanTemplate(40, MathF.PI)),
            new(15576, 0, "Fracture", null), new(13207, 0, "Fracture", null),
            new(13208, 0, "Fracture", null), new(13209, 0, "Fracture", null),
            new(15374, 0, "Fracture", null), new(16612, 0, "Fracture", null),
        ], [])],
        HEROES_AND_PRETENDERS => [new(0x428A, "Heroes and Pretenders", p => new WorldPolygonGeometry(
            Enumerable.Range(0, 20).Select(i => new Vector3(676 + 14.5f * MathF.Cos(i * MathF.Tau / 20),
                p.Y, 41 + 14.5f * MathF.Sin(i * MathF.Tau / 20))), new(676, p.Y, 41)),
        [
            new(37466, 0, "Promised Fall", new CircleTemplate(13)),
            new(37464, 0, "Gold Dust", null),
            new(37463, 0, "Acid Rain", null),
            new(37459, 0, "Unbound Arrow", null),
            new(37458, 0, "Fledgling Fury", new CircleTemplate(4)),
            new(37467, 0, "Foreseen Flurry", null),
        ], [])],
        RAISING_THE_SWORD => [new(0x1B51, "Aldis Sword of Nald", p => new CircleGeometry(new(-89.3f, p.Y, 0), 20.5f),
        [
            new(8134, 0, "Victory Slash", new FanTemplate(6.5f, MathF.PI * 2 / 3)),
            new(8137, 0, "Shuddering Swipe", new FanTemplate(60, MathF.PI / 6)),
            new(8141, 0, "Nald's Whisper", new CircleTemplate(20)),
            new(8136, 0, "Shuddering Swipe", null),
        ], [])],
        GAMBOLING_FOR_GIL => [new(0x29D2, "Ranaa Mihgo", p => new WorldPolygonGeometry(
            Enumerable.Range(0, 50).Select(i => new Vector3(520.47f + 17.5f * MathF.Cos(i * MathF.Tau / 50),
                p.Y, 124.99f + 16 * MathF.Sin(i * MathF.Tau / 50))), new(520.47f, p.Y, 124.99f)),
        [
            new(17197, 0, "War Dance", new CircleTemplate(5)),
            new(17198, 0, "Charming Chasse", null),
            new(17204, 0, "Hannish Fire", new CircleTemplate(6)),
            new(17214, 0, "Hannish Waters", new FanTemplate(40, MathF.PI / 6)),
            new(15646, 0, "Ranaa's Finish", new CircleTemplate(15)),
            new(17289, 0, "Foxshot", null),
        ], [])],
        POWER_TO_PROTECT => [new(0x1BCB, "Io", p => new WorldPolygonGeometry(
            [new(101.93f, p.Y, -666.63f), new(94.49f, p.Y, -639.63f),
             new(50.64f, p.Y, -652.38f), new(57.58f, p.Y, -679.32f)], new(76.285f, p.Y, -659.475f)),
        [
            new(1003, 0, "Iron Tempest", new CircleTemplate(5.5f)),
            new(2175, 0, "Fire II", null),
            new(720, 0, "Overpower", new FanTemplate(6.5f, MathF.PI / 2)),
            new(1135, 0, "Rive", RayRectangleTemplate.Symmetric(2, 30.5f)),
            new(8348, 0, "Diffractive Laser", new CircleTemplate(5)),
        ], [])],
        ART_FOR_THE_LIVING => [new(0x1CBA, "Summoning Node", p => new WorldPolygonGeometry(
        [
            new(-115.5f, p.Y, -272.34f), new(-106.5f, p.Y, -272.34f), new(-93f, p.Y, -280.25f),
            new(-88.8f, p.Y, -287.6f), new(-88.3f, p.Y, -287.6f), new(-88.3f, p.Y, -302.4f),
            new(-88.8f, p.Y, -302.4f), new(-92.85f, p.Y, -310.77f), new(-106.5f, p.Y, -318.68f),
            new(-115.5f, p.Y, -318.68f), new(-129.15f, p.Y, -310.77f), new(-133.2f, p.Y, -302.4f),
            new(-133.7f, p.Y, -302.4f), new(-133.7f, p.Y, -288.6f), new(-133.2f, p.Y, -288.6f),
            new(-129f, p.Y, -280.25f),
        ], new(-111, p.Y, -295.51f)),
        [
            new(8683, 0, "Piercing Laser", RayRectangleTemplate.Symmetric(6, 33.68f)),
            new(8707, 0, "Nerve Gas", new FanTemplate(35, MathF.PI * 2 / 3)),
            new(8708, 0, "Nerve Gas Left", new FanTemplate(35, MathF.PI)),
            new(8709, 0, "Nerve Gas Right", new FanTemplate(35, MathF.PI)),
            new(8697, 0, "111-tonze Swing", new CircleTemplate(12)),
            new(8699, 0, "11-tonze Swipe", new FanTemplate(9, MathF.PI * 2 / 3)),
        ], [])],
        STEEL_AGAINST_STEEL => [new(0x2A45, "Vitus quo Messalla", p => new CircleGeometry(new(-266, p.Y, -507), 19.5f),
        [
            new(17553, 0, "Culling Blade", new FanTemplate(60, MathF.PI / 6)),
            new(17568, 0, "The Order", null),
            new(17561, 0, "Captive Bolt", null),
            new(17575, 0, "Aetherochemical Grenado", new CircleTemplate(8)),
            new(17563, 0, "Exsanguination", new PtDonutSectorTemplate(2, 7, MathF.PI)),
            new(17564, 0, "Exsanguination", new PtDonutSectorTemplate(7, 12, MathF.PI)),
            new(17565, 0, "Exsanguination", new PtDonutSectorTemplate(12, 17, MathF.PI)),
            new(17574, 0, "Diffractive Laser", RayRectangleTemplate.Symmetric(4, 45)),
            new(17569, 0, "Snake Shot", new FanTemplate(20, MathF.PI * 4 / 3)),
            new(17559, 0, "To the Slaughter", new FanTemplate(40, MathF.PI)),
            new(17558, 0, "Scalding Tank", null),
        ], [])],
        LIFE_EPHEMERAL =>
        [
            new(0x35C5, "Ancel and Mahaud", p => LifeArena(p.Y, 19.5f),
            [
                new(26842, 0, "Demifire II", null, "单体伤害"),
                new(26839, 0, "Chi Blast", null, "全屏伤害"),
                new(26837, 0, "Explosion", new CircleTemplate(6)),
                new(26836, 0, "Arm of the Scholar", new CircleTemplate(5)),
                new(26832, 0, "Raw Rockbreaker", null),
                new(26833, 0, "Raw Rockbreaker", null),
                new(26834, 0, "Raw Rockbreaker", null),
                new(26844, 0, "Electrogenetic Force", null),
                new(26847, 0, "Classical Fire", null),
                new(26848, 0, "Classical Thunder", null),
                new(26849, 0, "Classical Blizzard", new CircleTemplate(6)),
                new(26850, 0, "Classical Stone", new CircleTemplate(15)),
            ], []),
            new(0x35C6, "Guildivain", p => LifeArena(p.Y, 19.5f),
            [
                new(26854, 0, "Tracheostomy", new DonutTemplate(10, 20)),
                new(26855, 0, "Right Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26856, 0, "Left Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26862, 0, "Right Left Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26863, 0, "Right Left Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26864, 0, "Left Right Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26865, 0, "Left Right Scalpel", new FanTemplate(15, MathF.PI * 7 / 6)),
                new(26857, 0, "Laparotomy", new FanTemplate(15, MathF.PI * 2 / 3)),
                new(26858, 0, "Amputation", new FanTemplate(20, MathF.PI * 2 / 3)),
                new(26861, 0, "Hypothermia", null, "全屏伤害"),
                new(28386, 0, "Craniotomy", null, "全屏伤害"),
                new(26860, 0, "Cryonics", null),
                new(26867, 0, "Frigotherapy", null),
            ], []),
        ],
        OUR_UNSUNG_HEROES => [new(0x1CAF, "Fallen Kuribu", p => new CircleGeometry(new(232.3f, p.Y, 407.7f), 20),
        [
            new(5604, 0, "Glory", new FanTemplate(42.7f, MathF.PI / 2)),
            new(8635, 0, "Cure IV", new CircleTemplate(12)),
            new(4366, 0, "Dark II", new FanTemplate(50.8f, MathF.PI / 3)),
        ], [])],
        KILLING_ART => [new(0x3664, "Orcus", p => OrcusArena(p.Y),
        [
            new(27591, 0, "Meaty Slice", RayRectangleTemplate.Symmetric(12, 50)),
            new(27595, 0, "Cleaver", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(27597, 0, "Flank Cleaver", new FanTemplate(40, MathF.PI * 2 / 3)),
            new(27599, 0, "Carnem Levare Cross", new DungeonCrossTemplate(40, 4)),
            new(27602, 0, "Carnem Levare Donut", null),
            new(27600, 0, "Carnem Levare Donut", null),
            new(27603, 0, "Carnem Levare Donut", null),
            new(27601, 0, "Carnem Levare Donut", null),
            new(27605, 0, "Void Mortar", new CircleTemplate(13)),
            new(27593, 0, "Focus Inferi", null),
        ], [])],
        BATTLE_ON_BEKKO => [new(0x1BF8, "Ugetsu Slayer of a Thousand Souls", p => new RectangleGeometry(new(808.8f, p.Y, 69.5f), 0, 28, 28),
        [
            new(8433, 0, "Hissatsu Kyuten", new CircleTemplate(5.5f)),
            new(9145, 0, "Tenka Goken", new FanTemplate(8.5f, MathF.PI * 2 / 3)),
            new(8437, 0, "Shin Getsubaku", new CircleTemplate(6)),
            new(8435, 0, "Mijin Giri", RayRectangleTemplate.Symmetric(10, 80.5f)),
            new(8439, 0, "Ugetsuzan", null),
            new(8440, 0, "Ugetsuzan", null),
            new(8441, 0, "Ugetsuzan", null),
            new(8442, 0, "Ugetsuzan", null),
            new(8446, 0, "Kurui Yukikaze", RayRectangleTemplate.Symmetric(4, 44)),
            new(8447, 0, "Kurui Gekko", null, "全屏伤害"),
            new(8448, 0, "Kurui Kasha", new PtDonutSectorTemplate(4.5f, 8.5f, MathF.PI / 2)),
        ], [])],
        SAGES_FOCUS => [new(0x3587, "Ancel Rockfist", p => SageArena(p.Y),
        [
            new(26535, 0, "Triple Threat", null, "死刑"),
            new(26537, 0, "Explosion", new CircleTemplate(6)),
            new(26543, 0, "Arm of the Scholar", new CircleTemplate(5)),
            new(26542, 0, "Noubelea", RayRectangleTemplate.Symmetric(4, 50)),
            new(26546, 0, "Demiblizzard III", new DonutTemplate(10, 40)),
            new(26539, 0, "Demigravity", new CircleTemplate(6)),
            new(26550, 0, "Demigravity", new CircleTemplate(6)),
            new(26548, 0, "Demifire III", null, "全屏伤害"),
            new(26554, 0, "Demifire II", new CircleTemplate(14)),
            new(26553, 0, "Demifire II Spread", null),
        ], [])],
        ORPHANS_AND_BROKEN_BLADE => [new(0x1C5E, "Ompagne Deepblack", p => new CircleGeometry(new(-166.8f, p.Y, 290), 20),
        [
            new(8459, 0, "Shadow of Death", new CircleTemplate(5)),
            new(8453, 0, "Spiral Hell", RayRectangleTemplate.Symmetric(4, 40)),
            new(8457, 0, "Headsmans Delight", null),
        ], [])],
        FANGS_OF_THE_VIPER => [new(0x429F, "Fangs of the Viper", p => ViperArena(p.Y),
        [
            new(37703, 0, "Burning Cyclone", new FanTemplate(6, MathF.PI * 2 / 3)),
            new(39263, 0, "Foul Breath", new FanTemplate(7, MathF.PI / 2)),
            new(37704, 0, "Brow Horn", RayRectangleTemplate.Symmetric(4, 6)),
            new(37700, 0, "Firebreathe", new FanTemplate(60, MathF.PI / 2)),
            new(37701, 0, "Right-Sided Shockwave", new FanTemplate(20, MathF.PI)),
            new(37702, 0, "Left-Sided Shockwave", new FanTemplate(20, MathF.PI)),
        ], [])],
        FACE_OF_TRUE_EVIL => [new(0x1BEE, "Musosai", p => new RectangleGeometry(new(-217.27f, p.Y, -158.31f), 0, 30, 30),
        [
            new(8415, 0, "Hissatsu To", RayRectangleTemplate.Symmetric(4, 44.5f)),
            new(8412, 0, "Hissatsu Kyuten", new CircleTemplate(5.5f)),
            new(8417, 0, "Hissatsu Kiku", RayRectangleTemplate.Symmetric(4, 44.5f)),
            new(8421, 0, "Maiogi", new FanTemplate(80, MathF.PI * 5 / 18)),
            new(8422, 0, "Musojin", null, "全屏伤害"),
            new(8419, 0, "Arashi", null),
            new(8643, 0, "Arashi no Kiku", null),
            new(8642, 0, "Arashi no Maiogi", null),
        ], [])],
        BLOOD_ON_THE_DECK => [new(0x1BED, "Gurumi Borlumi", p => new RectangleGeometry(new(0, p.Y, 15.8f), 0, 16, 15),
        [
            new(8407, 0, "Scythe Tail", new CircleTemplate(7)),
            new(8405, 0, "Butcher", new FanTemplate(9, MathF.PI / 2)),
            new(8408, 0, "Tenka Goken", new FanTemplate(8.5f, MathF.PI * 2 / 3)),
            new(8411, 0, "Bombslinger", new CircleTemplate(6)),
        ], [])],
        MIND_OVER_MANOR => [new(0x4292, "Mind Over Manor", p => JobQuestArenas.MindOverManor(p.Y),
        [
            new(37516, 0, "Jittering Glare", new FanTemplate(40, MathF.PI / 6)),
            new(37511, 0, "Gyrating Glare", null, "全屏伤害"),
            new(37512, 0, "Rubble Rouse", new CircleTemplate(10)),
            new(37513, 0, "Rock and Refrain", new CircleTemplate(10)),
            new(37515, 0, "Rock and Refrain", new CircleTemplate(10)),
        ], [])],
        SOMEWHERE_ONLY_SHE_KNOWS =>
        [
            new(0x4293, "The Winged Steed", p => JobQuestArenas.TheWingedSteed(p.Y),
            [
                new(37517, 0, "Burning Bright", RayRectangleTemplate.Symmetric(6, 47)),
                new(37518, 0, "Nicker", new CircleTemplate(12)),
            ], []),
            new(0x4296, "Flight of the Griffin", p => JobQuestArenas.FlightOfTheGriffin(p.Y),
            [
                new(37519, 0, "Swooping Frenzy", new CircleTemplate(12)),
                new(37522, 0, "Feathercut", RayRectangleTemplate.Symmetric(5, 10)),
                new(37520, 0, "Frigid Pulse", new DonutTemplate(12, 60)),
                new(37521, 0, "Fervid Pulse", new DungeonCrossTemplate(50, 7)),
                new(37523, 0, "Eye of the Fierce", null),
            ], []),
            new(0x4298, "Janquetilaques Portrait", p => new RectangleGeometry(new(0, p.Y, -340), 0, 49, 49),
            [
                new(37540, 0, "Freeze in Cyan", new FanTemplate(40, MathF.PI / 4)),
                new(37529, 0, "Nine Ivies", new FanTemplate(50, MathF.PI / 9)),
                new(37538, 0, "Tornado in Green", new DonutTemplate(10, 40)),
                new(37537, 0, "Sculpture Cast", null),
                new(37539, 0, "Blaze in Red", null, "全屏伤害"),
                new(37531, 0, "Earthquake", null),
                new(37532, 0, "Earthquake", null),
                new(37533, 0, "Earthquake", null),
                new(37535, 0, "Flood in Blue", RayRectangleTemplate.Symmetric(10, 50)),
            ], []),
        ],
        _ => [],
    };

    private static WorldPolygonGeometry ViperArena(float y)
    {
        var angleIncrement = MathF.Tau / 20;
        var points = Enumerable.Range(0, 20).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * angleIncrement);
            return new Vector3(264 + 19.5f * (float)sin, y, 480 + 19.5f * (float)cos);
        });
        return new(points, new(264, y, 480));
    }

    internal static WorldPolygonGeometry LifeArena(float y, float radius)
    {
        var increment = MathF.Tau / 20;
        return new(Enumerable.Range(0, 20).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * increment);
            return new Vector3(224.8f + radius * (float)sin, y, -855.8f + radius * (float)cos);
        }), new(224.8f, y, -855.8f));
    }

    private static WorldPolygonGeometry SageArena(float y)
    {
        var angleIncrement = MathF.Tau / 20;
        var points = Enumerable.Range(0, 20).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * angleIncrement);
            return new Vector3(18.5f * (float)sin, y, -82.146f + 18.5f * (float)cos);
        });
        return new(points, new(0, y, -82.146f));
    }

    private static PolygonRegionGeometry OrcusArena(float y)
    {
        var center = new Vector3(-69.569f, y, -388);
        var increment = MathF.Tau / 64;
        var points = Enumerable.Range(0, 64).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * increment);
            return new Vector2(19.5f * (float)sin, -19.5f * (float)cos);
        });
        var polygon = new LocalPolygonGeometry(center, 0, points);
        var entrance = new Vector2(-69 - center.X, -368 - center.Z);
        Vector2[] rectangle =
        [
            entrance + new Vector2(-20, -.94f), entrance + new Vector2(20, -.94f),
            entrance + new Vector2(20, .94f), entrance + new Vector2(-20, .94f),
        ];
        var region = (PolygonRegionGeometry)DungeonPolygonDifference.Build(polygon, [(Array.Empty<Vector2>(), rectangle)]);
        var bounds = region.GetBounds();
        var sourceCenter = new Vector3((bounds.MinX + bounds.MaxX) / 2, y, (bounds.MinZ + bounds.MaxZ) / 2);
        var triangles = region.FillPolygons.Select(p => p.Select(v =>
            new Vector2(v.X - sourceCenter.X, v.Z - sourceCenter.Z)).ToArray()).ToArray();
        return new(sourceCenter, triangles, [], triangles, region.GetContours());
    }
}
