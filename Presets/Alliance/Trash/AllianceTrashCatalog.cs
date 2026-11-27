using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal static class AllianceTrashCatalog
{
    internal static bool IsScript(string id) => id is
        "masterplayer.alliance.cuchulainn" or
        "masterplayer.alliance.irminsulsawtooth" or
        "masterplayer.alliance.arachneeve" or
        "masterplayer.alliance.deathgazehollow" or
        "masterplayer.alliance.ozma" or
        "masterplayer.alliance.cetus" or
        "masterplayer.alliance.echidna" or
        "masterplayer.alliance.calofisteri" or
        "masterplayer.alliance.forgall" or
        "masterplayer.alliance.headstone" or
        "masterplayer.alliance.ferdiadhollow" or
        "masterplayer.alliance.protoultima" or
        "masterplayer.alliance.diabolos" or
        "masterplayer.alliance.paradigmtrash" or
        "masterplayer.alliance.vanguard" or "masterplayer.alliance.aquarius" or "masterplayer.alliance.groundskeeper"
        or "masterplayer.alliance.despot" or "masterplayer.alliance.serpent"
        or "masterplayer.alliance.angelosfirst" or "masterplayer.alliance.angelossecond"
        or "masterplayer.alliance.detector" or "masterplayer.alliance.jack" or "masterplayer.alliance.kraken"
        or "masterplayer.alliance.lions" or "masterplayer.alliance.rhalgr" or "masterplayer.alliance.heavyartillery" or "masterplayer.alliance.lightartillery";

    internal static readonly BozjaDefinition[] Definitions =
    [
        Create("A10VanguardPathfinder", "先锋寻路人", 0x4689, 1248, AllianceTrashArenas.Vanguard(0), AllianceTrashArenas.VanguardRadius,
        [
            new("Seismostomp", [41652], BozjaRuleKind.Aoe, new CircleTemplate(5)),
            new("BombToss", [41655], BozjaRuleKind.Aoe, new CircleTemplate(3)),
        ]),
        Create("A10Aquarius", "宝瓶蟹", 0x468F, 1248, AllianceTrashArenas.Aquarius(0), AllianceTrashArenas.AquariusRadius,
        [
            new("WaterIII", [41666], BozjaRuleKind.Aoe, new CircleTemplate(7)),
            new("CursedSphere", [41656], BozjaRuleKind.Aoe, new CircleTemplate(3)),
            new("BubbleShower", [41664], BozjaRuleKind.Aoe, new FanTemplate(6, 60 * (MathF.PI / 180))),
            new("Scoop", [41663], BozjaRuleKind.Aoe, new FanTemplate(15, 120 * (MathF.PI / 180))),
            new("Beatdown", [41662], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(3, 9)),
            new("SpiderWeb", [41659], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("HundredFists", [40648], BozjaRuleKind.Interrupt),
            new("Agaricus", [41661], BozjaRuleKind.Aoe, new CircleTemplate(5)),
        ]),
        Create("A10Groundskeeper", "坐镇巨像", 0x4691, 1248, AllianceTrashArenas.Groundskeeper(0), AllianceTrashArenas.GroundskeeperRadius,
        [
            new("IsleDrop", [41670], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("MysteriousLight", [41667], BozjaRuleKind.CastGaze),
        ]),
        Create("A10Despot", "专制者", 0x4692, 1248, AllianceTrashArenas.Despot(0), AllianceTrashArenas.DespotRadius,
        [
            new("IsleDrop", [41699], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("WingCutter", [41672], BozjaRuleKind.Aoe, new FanTemplate(6, 120 * (MathF.PI / 180))),
            new("ScraplineTyphoon", [40650, 41393, 41902], BozjaRuleKind.Aoe, MaxCasts: 1),
            new("ScraplineStorm", [40650], BozjaRuleKind.AlliancePull),
            new("Panzerfaust", [41698], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("PanzerfaustHint", [41698], BozjaRuleKind.Interrupt),
        ]),
        Create("A30Trash1", "塔利亚海蛇", 0x4010, 1178, new CircleGeometry(new(-800, 0, -800), 20), 20,
        [
            new("WaterIII", [35438], BozjaRuleKind.Aoe, new CircleTemplate(8)),
            new("PelagicCleaver", [35439, 35852], BozjaRuleKind.Aoe, new FanTemplate(40, 60 * (MathF.PI / 180)), GroupedFinish: true),
            new("PelagicCleaver1Hint", [35439], BozjaRuleKind.Interrupt, Hint: "Interrupt!"),
            new("PelagicCleaver2Hint", [35852], BozjaRuleKind.Interrupt, Hint: "Interrupt!"),
            new("WaterDivineFlood", [35442, 35440], BozjaRuleKind.Aoe, new CircleTemplate(6), GroupedFinish: true),
        ]),
        Create("A30Trash2Pack1", "神的使者·第一组", 0x4013, 1178, AllianceTrashArenas.AngelosFirst(0), AllianceTrashArenas.AngelosFirstRadius,
        [
            new("RingOfSkylight", [35444], BozjaRuleKind.Aoe, new DonutTemplate(8, 30)),
            new("RingOfSkylightInterruptHint", [35444], BozjaRuleKind.Interrupt, Hint: "Interrupt!"),
            new("SkylightCross", [35445], BozjaRuleKind.Aoe, new DungeonCrossTemplate(60, 4)),
            new("SkylightCrossInterruptHint", [35445], BozjaRuleKind.Interrupt, Hint: "Interrupt!"),
            new("Skylight", [35446], BozjaRuleKind.Aoe, new CircleTemplate(6)),
        ]),
        Create("A30Trash2Pack2", "神的使者·第二组", 0x40E1, 1178, new RectangleGeometry(new(800, 0, 909.75f), 0, 39, 39), 19.5f,
        [
            new("RingOfSkylight", [35444], BozjaRuleKind.Aoe, new DonutTemplate(8, 30)),
            new("SkylightCross", [35445], BozjaRuleKind.Aoe, new DungeonCrossTemplate(60, 4)),
        ]),
        Create("A20Detector", "探测器", 0x4880, 1304, AllianceNextArenas.Detector(0), AllianceNextArenas.DetectorRadius,
        [
            new("Electray", [43576], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(5, 40)),
            new("Electroswipe", [43559], BozjaRuleKind.Aoe, new FanTemplate(50, 120 * (MathF.PI / 180))),
            new("ElectroswipeHint", [43559], BozjaRuleKind.Interrupt),
        ]),
        Create("A20JaggedyEaredJack", "锯耳 杰克", 0x4875, 1304, AllianceNextArenas.Jack(0), AllianceNextArenas.JackRadius,
        [
            new("DustCloud", [43554], BozjaRuleKind.Aoe, new FanTemplate(10, 120 * (MathF.PI / 180))),
        ]),
        Create("A20Kraken", "克拉肯", 0x4885, 1304, AllianceNextArenas.Kraken(0), AllianceNextArenas.KrakenRadius,
        [
            new("MightyStrikes", [43575], BozjaRuleKind.Interrupt),
            new("PowerAttack", [43569], BozjaRuleKind.Aoe, new FanTemplate(20, 120 * (MathF.PI / 180))),
            new("CatapultParalyzeIIIBanishga", [43567, 43568, 43562, 43570], BozjaRuleKind.Aoe, new CircleTemplate(6), GroupedFinish: true),
            new("ImpactRoar", [43566], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("Flood", [43791, 43792], BozjaRuleKind.Aoe, new CircleTemplate(8)),
            new("Sucker", [43791], BozjaRuleKind.AlliancePull),
        ]),
    ];

    internal static readonly BozjaDefinition Lions = Create("A10Lions", "阿格莱亚双狮", 0x38DA, 1054,
        AllianceNextArenas.Lions(0), AllianceNextArenas.LionsRadius,
        [
            new("DoubleImmolation", [29143], BozjaRuleKind.Raidwide, GlobalHint: true),
            new("SlashAndBurn", [29136, 29138, 29141, 29142, 29376, 29377],
                BozjaRuleKind.Aoe, new CircleTemplate(14), MaxCasts: 1),
            new("RoaringBlaze", [29134, 29135, 29375], BozjaRuleKind.Aoe,
                new FanTemplate(50, 180 * (MathF.PI / 180)), MaxCasts: 1),
        ]);

    internal static readonly BozjaDefinition Rhalgr = Create("A10RhalgrEmissary", "拉尔戈神使", 0x38FA, 1054,
        AllianceNextArenas.Rhalgr(0), AllianceNextArenas.RhalgrRadius,
        [
            new("LightningBolt", [28898], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("Boltloop", [28893, 28894, 28895], BozjaRuleKind.Earthbreaker),
            new("BoltsFromTheBlue", [28900], BozjaRuleKind.CastCounter),
            new("DestructiveStrike", [28889], BozjaRuleKind.DahuBait, new FanTemplate(13, 120 * (MathF.PI / 180))),
            new("DestructiveStatic", [28896], BozjaRuleKind.Aoe, new FanTemplate(50, 180 * (MathF.PI / 180))),
            new("DestructiveCharge", [28891], BozjaRuleKind.Aoe, new FanTemplate(25, 90 * (MathF.PI / 180))),
        ]);

    internal static readonly BozjaDefinition HeavyArtillery = Create("A20HeavyArtilleryUnit", "重型陆战装置", 0x2E83, 917,
        new RectangleGeometry(new(200, 0, -216.25f), 0, 23, 88.5f), 44.25f,
        [
            new("ArenaChanges", [], BozjaRuleKind.Artillery),
            new("Energy", [20658], BozjaRuleKind.Artillery, new BozjaCapsuleTemplate(1, 3)),
            new("ManeuverHighPoweredLaser", [20657], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8, 90), GroupedFinish: true),
        ]);

    internal static readonly BozjaDefinition LightArtillery = Create("A20LightArtilleryUnit", "轻型陆战装置", 0x2EE4, 917,
        new RectangleGeometry(new(-230, 0, -15), 0, 49, 49), 24.5f,
        [
            new("FireVoidzone", [], BozjaRuleKind.Artillery, new CircleTemplate(6)),
            new("SurfaceMissileImpact", [21007], BozjaRuleKind.Aoe, new CircleTemplate(6)),
            new("HomingMissileImpact", [21008], BozjaRuleKind.CircleBait, new CircleTemplate(6)),
            new("LongBarreledLaser", [21010], BozjaRuleKind.Aoe, RayRectangleTemplate.Symmetric(8, 70)),
            new("VoltArray", [21009], BozjaRuleKind.Interrupt),
            new("ManeuverMartialArm", [21011], BozjaRuleKind.Tankbuster, GlobalHint: true),
            new("InitiateSelfDestruct", [21013], BozjaRuleKind.CastCounter),
        ]);

    private static BozjaDefinition Create(string key, string name, uint oid, uint territory, IGeometryShape arena,
        float radius, BozjaRule[] rules)
        => new(key, name, oid, 0, arena.Position, arena, rules, PullRadius: float.PositiveInfinity,
            SampleRadius: radius, Territory: territory);

    internal static uint[] Trash(string key) => key switch
    {
        "A10Lions" => [0x38DB],
        "A20Detector" => [0x4880, 0x4881],
        "A20JaggedyEaredJack" => [0x4875, 0x487A, 0x4879, 0x4877, 0x4876, 0x4874, 0x4878],
        "A20Kraken" => [0x4885, 0x4888, 0x488A, 0x4887, 0x4886, 0x4889, 0x488B, 0x488D, 0x488C],
        "A10VanguardPathfinder" => [0x4689, 0x470E, 0x4688, 0x4687],
        "A10Aquarius" => [0x468F, 0x468D, 0x468E, 0x4711, 0x468C, 0x468B, 0x4710, 0x468A, 0x470F],
        "A10Groundskeeper" => [0x4691, 0x4712, 0x4690],
        "A10Despot" => [0x4692, 0x4713, 0x4693],
        "A30Trash1" => [0x4011, 0x4012, 0x4085],
        "A30Trash2Pack1" => [0x4014],
        "A30Trash2Pack2" => [0x40E1],
        _ => [],
    };
}
