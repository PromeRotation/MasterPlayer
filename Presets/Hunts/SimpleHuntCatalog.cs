using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Hunts;

internal static class SimpleHuntCatalog
{
    internal const string SCRIPT_ID = "masterplayer.hunts.simple";
    internal static readonly HuntDefinition[] Definitions =
    [
        new(0x3672, "KerShroud", [
            new(27725, "AccursedPox", new CircleTemplate(8)),
            new(27724, "EntropicFlame", RayRectangleTemplate.Symmetric(8, 60))]),
        new(0x4571, "CrystalIncarnation", [
            new(39623, "FireII", new CircleTemplate(5)),
            new(39624, "BlizzardII", new FanTemplate(40, MathF.PI / 4))]),
        new(0x1ABF, "Gajasura", [
            new(8188, "Spin", new FanTemplate(8.23f, 2 * MathF.PI / 3)),
            new(8187, "Hurl", new CircleTemplate(6)),
            new(8189, "Buffet", null, "Tankbuster", "Heavy damage on random target (except tank)")]),
        new(0x1AB3, "Orghana", [
            new(7997, "Epigraph", RayRectangleTemplate.Symmetric(8, 55.04f)),
            new(8000, "TremblingEpigraph", null, "Raidwide", "Raidwide x4"),
            new(8002, "FlaringEpigraph", null, "Raidwide", "Raidwide")]),
        new(0x2820, "OPoorestPauldia", [
            new(16830, "RustingClaw", new FanTemplate(12.025f, 2 * MathF.PI / 3)),
            new(16831, "TailDrive", new FanTemplate(34.025f, 2 * MathF.PI / 3)),
            new(16832, "WordsOfWoe", RayRectangleTemplate.Symmetric(6, 49.025f)),
            new(16833, "TheSpin", null, "Raidwide", "Raidwide")]),
        new(0x35C0, "ArchEta", [
            new(27269, "EnergyWave", RayRectangleTemplate.Symmetric(14, 40)),
            new(27270, "TailSwipe", new FanTemplate(25, MathF.PI / 2)),
            new(27271, "HeavyStomp", new CircleTemplate(17)),
            new(27272, "SonicHowl", null, "Raidwide", "Raidwide"),
            new(27273, "SteelFang", null, "Tankbuster", "Tankbuster")]),
        new(0x35DD, "Hulder", [
            new(27073, "LayOfMislaidMemory", new FanTemplate(30, 2 * MathF.PI / 3)),
            new(27075, "TempestuousWrath", null, ChargeHalfWidth: 4),
            new(27076, "RottingElegy", new DonutTemplate(5, 50)),
            new(27077, "OdeToLostLove", null, "Raidwide", "Raidwide"),
            new(27078, "StormOfColor", null, "Tankbuster", "Tankbuster")]),
        new(0x2A03, "ForgivenGossip", [
            new(17043, "Icefall", new CircleTemplate(5)),
            new(18041, "PetrifyingEye", null, GazeRange: 10000)]),
        new(0x1AB5, "BoneCrawler", [
            new(7924, "HeatBreath", new FanTemplate(14.2f, MathF.PI / 2)),
            new(7920, "RipperClaw", new FanTemplate(11.2f, MathF.PI / 2)),
            new(7921, "WildCharge", null, ChargeHalfWidth: 4),
            new(7922, "HotCharge", null, ChargeHalfWidth: 6),
            new(7919, "TailSwing", new CircleTemplate(10), KnockbackDistance: 20),
            new(7918, "TailSmash", new FanTemplate(18.2f, MathF.PI / 2))]),
        new(0x1AB1, "Okina", [
            new(7990, "Hydrocannon", RayRectangleTemplate.Symmetric(6, 30)),
            new(7991, "ElectricSwipe", new FanTemplate(25, MathF.PI / 3)),
            new(7996, "ElectricWhorl", new DonutTemplate(8, 60)),
            new(7995, "Expulsion", new CircleTemplate(14), KnockbackDistance: 30),
            new(7993, "BodySlam", new CircleTemplate(10), KnockbackDistance: 20),
            new(7994, "Immersion", null, "Raidwide", "Raidwide"),
            new(7992, "RubyTide", null, Hint: "Applies damage buff to self")]),
        new(0x1AC0, "Angada", [
            new(8190, "ScytheTail", new CircleTemplate(9.4f)),
            new(8191, "Butcher", new FanTemplate(11.4f, 2 * MathF.PI / 3)),
            new(8193, "RockThrow", new CircleTemplate(6))]),
        new(0x1AB2, "Gamma", [
            new(7914, "DiffractiveLaser", new CircleTemplate(5)),
            new(7913, "MagitekFlamehook", null, "Raidwide", "Raidwide + Pyretic"),
            new(7915, "Launcher", null, "Raidwide", "Raidwide (%HP based)")]),
        new(0x35DF, "LunatenderQueen", [
            new(27363, "AvertYourEyes", null, GazeRange: 10000),
            new(27364, "YouMayApproach", new DonutTemplate(6, 40)),
            new(27365, "AwayWithYou", new CircleTemplate(15)),
            new(27366, "Needles", new CircleTemplate(6)),
            new(27367, "WickedWhim", null, Hint: "Invert next cast"),
            new(27369, "AvertYourEyesInverted", null, GazeRange: 10000, InvertedGaze: true),
            new(27370, "YouMayApproachInverted", new CircleTemplate(15)),
            new(27371, "AwayWithYouInverted", new DonutTemplate(6, 40))]),
        new(0x2854, "Baal", [
            new(17956, "SewerWater1", new FanTemplate(12, MathF.PI)),
            new(17957, "SewerWater2", new FanTemplate(12, MathF.PI)),
            new(17423, "SewageWaveFirst1", null),
            new(17424, "SewageWaveFirst2", null)]),
        new(0x28B5, "Huracan", [
            new(17496, "SpringBreeze", RayRectangleTemplate.Symmetric(10, 80)),
            new(17499, "SummerHeat", null, "Raidwide", "Raidwide"),
            new(17495, "DawnsEdge", null),
            new(17497, "WinterRain", null),
            new(17498, "AutumnWreath", null)]),
        new(0x283A, "Grassman", [
            new(17859, "ChestThump", null),
            new(17861, "StoolPelt", new CircleTemplate(5)),
            new(17860, "Browbeat", null, "Tankbuster", "Tankbuster"),
            new(17862, "Streak", null, ChargeHalfWidth: 3)]),
        new(0x2874, "Maliktender", [
            new(18019, "Sabotendance", new CircleTemplate(8)),
            new(18022, "TwentyKNeedles", RayRectangleTemplate.Symmetric(8, 20)),
            new(18024, "NineNineNineKNeedles", RayRectangleTemplate.Symmetric(8, 20)),
            new(18020, "Haste", null)]),
        new(0x35C1, "FanAil", [
            new(27373, "Divebomb", RayRectangleTemplate.Symmetric(11, 30), FollowCasterRotation: true),
            new(27376, "LiquidHell", new CircleTemplate(6)),
            new(27378, "Plummet", new FanTemplate(8, MathF.PI / 2)),
            new(27379, "DeathSentence", null, "Tankbuster", "Tankbuster"),
            new(27380, "CycloneWing", null, "Raidwide", "Raidwide")]),
        new(0x41FC, "Starcrier", [
            new(37038, "WingsbreadthWinds", new CircleTemplate(8)),
            new(37039, "StormwallWinds", new DonutTemplate(8, 25)),
            new(37040, "DirgeOfTheLost", null, Hint: "Applies temporary misdirection"),
            new(37163, "AeroIV", null, "Raidwide", "Raidwide"),
            new(37305, "SwiftwindSerenade", RayRectangleTemplate.Symmetric(8, 40))]),
        new(0x35DE, "Storsie", [
            new(27354, "AspectEarth", null), new(27355, "AspectWind", null), new(27356, "AspectLightning", null),
            new(27358, "Whorlstorm", null), new(27359, "Defibrillate", null), new(27360, "EarthenAugur", null),
            new(27361, "FangsEnd", null, "Tankbuster", "Tankbuster")]),
        new(0x360B, "MoussePrincess", [
            new(27318, "PrincessThrenodyPrepare", null), new(27319, "PrincessThrenodyResolve", null),
            new(27320, "WhimsyAlaMode", null, Hint: "Select direction"),
            new(27321, "AmorphicFlail", new CircleTemplate(9)),
            new(27322, "PrincessCacophony", new CircleTemplate(12)),
            new(27323, "Banish", null, "Tankbuster", "Tankbuster")]),
        new(0x35DC, "Ophioneus", [
            new(27350, "RightMaw", new FanTemplate(30, MathF.PI)), new(27351, "LeftMaw", new FanTemplate(30, MathF.PI)),
            new(27352, "PyricCircle", null), new(27346, "LeapingPyricCircleAOE", null), new(27341, "LeapingPyricCircle", null),
            new(27353, "PyricBurst", null), new(27347, "LeapingPyricBurstAOE", null), new(27342, "LeapingPyricBurst", null),
            new(27348, "Scratch", null, "Tankbuster", "Tankbuster")]),
        new(0x35BD, "Ruminator", [
            new(26874, "ChitinousTraceCircleFirst", null), new(26875, "ChitinousTraceDonutFirst", null),
            new(26876, "ChitinousTraceCircle", null), new(26877, "ChitinousTraceDonut", null),
            new(26878, "AdvanceCircle", null), new(26879, "AdvanceDonut", null),
            new(26915, "ReversalCircle", null), new(26916, "ReversalDonut", null),
            new(26882, "StygianVapor", null, "Raidwide", "Raidwide")]),
        new(0x3670, "Sphatika", [
            new(27617, "Gnaw", null, "Tankbuster", "Tankbuster"),
            new(27619, "Caterwaul", null, "Raidwide", "Raidwide"),
            new(27620, "Brace1", null, Hint: "Select directions"), new(27621, "Brace2", null, Hint: "Select directions"),
            new(27622, "Brace3", null, Hint: "Select directions"), new(27623, "Brace4", null, Hint: "Select directions"),
            new(27624, "LickwhipStance", null, Hint: "Cleave sides literal"),
            new(27625, "WhiplickStance", null, Hint: "Cleave sides inverted"),
            .. Enumerable.Range(27626, 8).Select(action => new HuntCastRule((uint)action, "BearingCleave", null)),
            new(27714, "BearingCleave9", null), new(27715, "BearingCleave10", null)]),
        new(0x361B, "Gurangatch", [
            .. new uint[] { 27493, 27494, 27495, 27496, 27497, 27498, 27499, 27500, 27521, 27522 }
                .Select(action => new HuntCastRule(action, "Slammer", null)),
            new(27512, "BoneShaker", null, "Raidwide", "Raidwide")]),
        new(0x360A, "Burfurlur", [
            .. new uint[] { 27307, 27308, 27692, 27309, 27310, 27693 }
                .Select(action => new HuntCastRule(action, "QuintupleSneeze", null)),
            new(27314, "Uppercut", new FanTemplate(15, 2 * MathF.PI / 3)),
            new(27313, "RottenSpores", new CircleTemplate(6))]),
        new(0x3609, "Minerva", [
            new(27297, "AntiPersonnelBuild", null, Hint: "Select next AOE type"),
            new(27298, "RingBuild", null, Hint: "Select next AOE type"),
            new(27517, "BallisticMissileCircleWarning", null, Hint: "Select next AOE target"),
            new(27518, "BallisticMissileDonutWarning", null, Hint: "Select next AOE target"),
            new(27299, "BallisticMissileCircle", null), new(27300, "BallisticMissileDonut", null),
            new(27301, "Hyperflame", new FanTemplate(60, MathF.PI / 3)),
            new(27302, "SonicAmplifier", null, "Raidwide", "Raidwide"),
            new(27304, "HammerKnuckles", null, "Tankbuster", "Tankbuster")]),
        new(0x35BE, "Armstrong", [
            new(27472, "MagitekCompressorFirst", null),
            new(27473, "MagitekCompressorReverse", null), new(27474, "MagitekCompressorNext", null),
            new(27475, "AcceleratedLanding", new CircleTemplate(6)),
            new(27476, "CalculatedCombustion", null, "Raidwide", "Raidwide"),
            new(27477, "Pummel", null, "Tankbuster", "Tankbuster"),
            new(27478, "SoporificGas", new CircleTemplate(12))]),
        new(0x2839, "Supay", [
            new(17858, "BlasphemousHowl", null, BaitRadius: 8),
            new(17856, "PetroEyes", null, GazeRange: 10000)]),
        new(0x281F, "TheMudman", [
            new(16827, "BogBequest", new DonutTemplate(5, 20)),
            new(16828, "FeculentFlood", new FanTemplate(40, MathF.PI / 3)),
            new(16826, "RoyalFlush", new CircleTemplate(8)),
            new(16829, "GravityForce", null, Hint: "Interrupt!", BaitRadius: 6)]),
        new(0x35FB, "Petalodus", [
            new(27063, "MarineMayhem", null, Hint: "Interrupt! (Raidwide x3)"),
            new(27067, "Waterga", null, SpreadRadius: 6),
            new(27068, "TidalGuillotine", new CircleTemplate(13)),
            new(27069, "AncientBlizzard", new FanTemplate(40, MathF.PI / 4))]),
        new(0x28B4, "LilMurderer", [
            new(17493, "GobthunderIII", null, Hint: "Interrupt!", SpreadRadius: 20),
            new(17488, "GoblinPunch", null, "Tankbuster", "Tankbuster"),
            new(17492, "GobthunderII", new CircleTemplate(8)),
            new(17491, "Gobhaste", null, Hint: "Attack speed buff")]),
        new(0x288F, "Nuckelavee", [
            new(16964, "Torpedo", null),
            new(16965, "BogBody", null, SpreadRadius: 5),
            new(16967, "Gallop", null)]),
        new(0x2853, "Rusalka", [
            new(17363, "Hydrocannon", new CircleTemplate(8)),
            new(17368, "AetherialSpark", RayRectangleTemplate.Symmetric(4, 12)),
            new(17366, "AetherialPull", null, PullDistance: 30)]),
        new(0x2875, "Sugaar", [
            new(18018, "BodySlam", new CircleTemplate(11)),
            new(18015, "NumbingNoise", new FanTemplate(13, 2 * MathF.PI / 3)),
            new(18016, "TailSnap", new FanTemplate(18, 2 * MathF.PI / 3)),
            new(18100, "NumbingNoiseRotation", null, PullDistance: 25, PullFromCaster: true),
            new(18101, "TailSnapRotation", null, PullDistance: 25, PullFromCaster: true),
            new(18098, "NumbingNoiseRotationAOE", null), new(18099, "TailSnapRotationAOE", null)]),
        new(0x3671, "Aegeiros", [
            new(27708, "Leafstorm", null), new(27709, "Rimestorm", null),
            new(27710, "Snowball", new CircleTemplate(8)),
            new(27712, "BackhandBlow", new FanTemplate(12, 2 * MathF.PI / 3))]),
        new(0x2890, "Nariphon", [
            new(16970, "OdiousMiasma", new FanTemplate(12, 2 * MathF.PI / 3)),
            new(16972, "AllergenInjection", null, BaitRadius: 6),
            new(16971, "RootsOfAtopy", null)]),
        new(0x35BF, "Yilan", [
            new(27230, "Soundstorm", null, Hint: "Apply march debuffs"),
            new(27231, "MiniLight", null),
            new(27232, "Devour", null, Hint: "Harmless unless you got minimized by the previous mechanic"),
            new(27233, "BogBomb", new CircleTemplate(6)),
            new(27234, "BrackishRain", new FanTemplate(10, MathF.PI / 2))]),
        new(0x35DB, "NarrowRift", [
            .. new uint[] { 27487, 27488, 27331, 27332, 27335, 27337 }
                .Select(action => new HuntCastRule(action, "EmptyPromise", null)),
            .. new uint[] { 27327, 27328, 27329, 27330 }
                .Select(action => new HuntCastRule(action, "ContinualMeddling", null, Hint: "Apply march debuffs")),
            new(27325, "ContinualMeddlingFA", null)]),
        new(0x4395, "SallyTheSweeper", [
            .. new uint[] { 38454, 38455, 38456, 38457, 38458, 38459, 38460 }
                .Select(action => new HuntCastRule(action, "ExecutionModel", null)),
            new(38466, "TargetedAdvance", new CircleTemplate(18))]),
    ];

    internal static HuntDefinition? Find(uint oid) => Definitions.FirstOrDefault(d => d.Oid == oid);
    internal static bool Owns(uint oid) => Find(oid) != null;
}
