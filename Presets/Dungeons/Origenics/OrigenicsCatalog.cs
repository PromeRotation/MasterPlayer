using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class OrigenicsCatalog
{
    internal const uint TERRITORY = 1208;
    internal static bool Owns(uint oid) => oid is 0x41E2 or 0x42BA or 0x43D6 or 0x4189 or 0x43D7 or 0x4185 or 0x4170 or 0x417D;
    internal static bool Riot(uint action) => action is 36465 or 36466 or 36467;
    internal static bool Expulsion(uint action) => action is 36433 or 36434;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x41E2, "Origenics Aerostat", p => OrigenicsArenas.Aerostat(p.Y),
        [
            new(38328, 0, "IncendiaryCircle", new DonutTemplate(3, 12)),
            new(35428, 0, "GrenadoShot", new CircleTemplate(5)),
        ], [871, 873, 870, 35425], [0x42BA, 0x43D6, 0x4189, 0x43D7]),
        new(0x4185, "Herpekaris", p => new RectangleGeometry(new(-88, p.Y, -180), 0, 35, 35),
        [
            new(36519, 0, "StridentShriek", null, "全屏伤害"),
            new(36518, 0, "ConvulsiveCrush", null, "单体伤害"),
            new(37921, 0, "PoisonHeartSpread", new CircleTemplate(5), PartyAoe: "分散"),
            new(36460, 0, "PoisonHeartVoidzone", new CircleTemplate(2)),
            new(38518, 0, "PodBurst", new CircleTemplate(6)),
            new(38519, 0, "PodBurst", new CircleTemplate(6)),
            new(36465, 0, "RightSweepTelegraph", new FanTemplate(25, MathF.PI * 7 / 6)),
            new(36466, 0, "LeftSweepTelegraph", new FanTemplate(25, MathF.PI * 7 / 6)),
            new(36467, 0, "RearSweepTelegraph", new FanTemplate(25, MathF.PI / 2)),
            new(36473, 0, "CollectiveAgony", null),
        ], [872, 36520, 36459, 37451, 36452, 36453, 36454, 36463, 36469, 36470, 36471, 36474]),
        new(0x4170, "Deceiver", p => new RectangleGeometry(new(-172, p.Y, -142), 0, 49, 49),
        [
            new(36371, 0, "Electrowave", null, "全屏伤害"),
            new(36370, 0, "BionicThrash", new FanTemplate(30, MathF.PI / 2)),
            new(36372, 0, "Synchroshot", RayRectangleTemplate.Symmetric(4, 40)),
            new(36365, 0, "InitializeTurrets", RayRectangleTemplate.Symmetric(10, 4)),
            new(36366, 0, "LaserLash", RayRectangleTemplate.Symmetric(10, 40)),
            new(38320, 0, "Electray", new CircleTemplate(5), PartyAoe: "分散"),
            new(36367, 0, "Surge", null),
        ], [870, 873, 36362, 36369, 36368, 36363, 36373, 36364, 36426, 38807, 39736]),
        new(0x417D, "Ambrose", p => new RectangleGeometry(new(190, p.Y, 0), 0, 65, 48),
        [
            new(36436, 0, "PsychicWave", null, "全屏伤害"),
            new(36428, 0, "Psychokinesis", RayRectangleTemplate.Symmetric(13, 70)),
            new(36437, 0, "VoltaicSlash", null, "单体伤害"),
            new(36429, 0, "Electrolance", new CircleTemplate(22)),
            new(36438, 0, "WhorlOfTheMind", new CircleTemplate(5), PartyAoe: "分散"),
            new(36433, 0, "ExtrasensoryExpulsionNorthSouth", RayRectangleTemplate.Symmetric(15, 20)),
            new(36434, 0, "ExtrasensoryExpulsionWestEast", RayRectangleTemplate.Symmetric(20, 15)),
            new(39233, 0, "OverwhelmingCharge", new FanTemplate(26, MathF.PI)),
            new(39072, 0, "OverwhelmingCharge", new FanTemplate(26, MathF.PI)),
            new(38953, 0, "RushTelegraph", null),
        ], [870, 872, 36439, 36435, 36427, 38929, 36432, 39055, 38954, 36430, 36431]),
    ];
}
