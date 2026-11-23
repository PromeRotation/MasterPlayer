using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ArrQuestCatalog
{
    internal const uint ARCHON = 1049;
    internal const uint ULTIMATE_WEAPON = 1052;
    internal const uint STEPS_OF_FAITH = 1068;
    internal static readonly uint[] STEPS_ADDS =
    [
        0x3CD5, 0x3AA2, 0x3AA4, 0x3AA5, 0x3AAC, 0x3AA7, 0x3AAF, 0x3ABF, 0x3AA8, 0x3AA9,
        0x3A94, 0x3A93, 0x3A95, 0x3A96, 0x3A97, 0x3ABB, 0x3ABC, 0x3AB0,
        0x3A9A, 0x3A9B, 0x3A9C, 0x3A9D, 0x3AA0, 0x3A9E, 0x3A9F, 0x3AC0, 0x3AB1,
        0x3AB2, 0x3AB3, 0x3AB4, 0x3AAB, 0x3AB6, 0x3AC2, 0x3AB8, 0x3AB9, 0x3AB7, 0x3ABA, 0x3ABD, 0x3AC1,
    ];
    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        ARCHON =>
        [
            new(0x38F5, "Rhitahtyn sas Arvina", p => new CircleGeometry(new Vector3(-689, p.Y, -815), 14.5f),
            [
                new(28871, 0, "TartareanShockwaveSmall", new CircleTemplate(7)),
                new(28870, 0, "GalesOfTartarus", RayRectangleTemplate.Symmetric(5, 30)),
                new(28865, 0, "MagitekMissiles", new CircleTemplate(7)),
                new(28869, 0, "TartareanTomb", new CircleTemplate(11)),
                new(28874, 0, "DrillShot", RayRectangleTemplate.Symmetric(5, 30)),
                new(28877, 0, "TartareanShockwaveBig", new CircleTemplate(14)),
                new(28876, 0, "GalesOfTartarusBig", RayRectangleTemplate.Symmetric(30, 30)),
            ], []),
        ],
        ULTIMATE_WEAPON =>
        [
            new(0x3933, "Lahabrea", p => new CircleGeometry(new Vector3(-704, p.Y, 480), 20),
            [
                new(29327, 0, "AncientFireIII", null, "全屏伤害"),
                new(29329, 0, "DarkThunder", new CircleTemplate(1)),
                new(29331, 0, "EndOfDays", RayRectangleTemplate.Symmetric(8, 60)),
                new(29762, 0, "EndOfDaysAdds", RayRectangleTemplate.Symmetric(8, 60)),
                new(29340, 0, "Nightburn", null, "单体伤害"),
                new(29332, 0, "FiresphereSummon", null),
                new(29333, 0, "Burst", null),
                new(29335, 0, "AncientEruption", new CircleTemplate(6)),
                new(29760, 0, "FluidFlare", new FanTemplate(40, MathF.PI / 3)),
                new(29756, 0, "AncientCross", null),
                new(29758, 0, "BurstFlare", null),
                new(29337, 0, "GripOfNight", new FanTemplate(40, MathF.PI * 5 / 6)),
            ], []),
        ],
        STEPS_OF_FAITH =>
        [
            new(0x3A5F, "Vishap", p => new RectangleGeometry(new(0, p.Y, 245), 0, 44, 80),
            [
                new(31262, 0, "RipperClaw", new FanTemplate(9, MathF.PI / 2)),
                new(30892, 0, "Levinshower", null),
                new(30880, 0, "EarthshakerAOE", new CircleTemplate(31)),
                new(30887, 0, "Earthshaker", null),
                new(26410, 0, "EarthrisingAOE", new CircleTemplate(31)),
                new(30888, 0, "Earthrising", null),
                new(30879, 0, "SidewiseSlice", new FanTemplate(50, MathF.PI * 2 / 3)),
                new(30875, 0, "FireballSpread", new CircleTemplate(6), PartyAoe: "分散"),
                new(30894, 0, "FireballAOE", new CircleTemplate(6)),
                new(30883, 0, "Flamisphere", new CircleTemplate(10)),
                new(26401, 0, "BodySlam", null),
                new(26408, 0, "Touchdown", null),
                new(30185, 0, "FlameBreath", null),
                new(26411, 0, "FlameBreath2", null),
                new(30885, 0, "Cauterize", null),
                new(29789, 0, "ScorchingBreath", null),
            ], []),
        ],
        _ => [],
    };
}
