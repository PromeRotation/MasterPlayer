using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TroiaCatalog
{
    internal const uint TERRITORY = 1070;
    internal static bool IsTrash(uint oid) => oid is 0x397D or 0x399E or 0x3983 or 0x3986;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x3988 or 0x396D or 0x39C5;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x397D, "TroianScavanger", p => p.Z > 170 ? TroiaArenas.ScavangerFirst(p.Y) : TroiaArenas.ScavangerSecond(p.Y),
        [
            new(30212, 0x397E, "Condemnation", new FanTemplate(6, MathF.PI / 2)),
            new(30214, 0x397C, "EvilPhlegm", new CircleTemplate(5)),
            new(30213, 0x397D, "DarkArrivisme", new CircleTemplate(5)),
        ], [6497, 28630], [0x397C, 0x397E]),
        new(0x399E, "TroianFootman", p => TroiaArenas.Footman(p.Y),
        [
            new(29713, 0x399E, "GrimFate", new FanTemplate(12, MathF.PI * 2 / 3)),
            new(30218, 0x397F, "VoidTrap", new CircleTemplate(6)),
            new(29712, 0x399E, "GrimHalo", new CircleTemplate(12)),
            new(30215, 0x3981, "Geirrothr", new FanTemplate(6, MathF.PI * 2 / 3)),
        ], [6497, 870], [0x397F, 0x3980, 0x3981]),
        new(0x3983, "TroianBishop", p => TroiaArenas.Bishop(p.Y),
        [
            new(30216, 0x3982, "HallOfSorrow", new CircleTemplate(8)),
            new(30217, 0x3983, "JestersReap", new FanTemplate(12, MathF.PI * 2 / 3)),
        ], [6497], [0x3982, 0x3980]),
        new(0x3986, "TroianButler", p => p.Z > -138 ? TroiaArenas.ButlerFirst(p.Y) : TroiaArenas.ButlerSecond(p.Y),
        [
            new(30223, 0x3984, "ArachneWeb", new CircleTemplate(6)),
            new(30221, 0x3985, "Swoop", new CircleTemplate(6)),
            new(30220, 0x3987, "UnholyDarkness", new CircleTemplate(8)),
            new(30219, 0x3987, "Karma", new FanTemplate(30, MathF.PI / 2)),
            new(30222, 0x3986, "Dark", new FanTemplate(6, MathF.PI * 2 / 3)),
        ], [6497, 6499], [0x3984, 0x3987, 0x3985]),
        new(0x3988, "EvilDreamers", p => TroiaArenas.Dreamers(p.Y),
        [
            new(29621, 0x3966, "UniteMare1", new CircleTemplate(6)),
            new(29622, 0x3966, "UniteMare2", new CircleTemplate(18)),
            new(29628, 0x3968, "UniteMare3", new CircleTemplate(12)),
            new(29624, 0x3966, "DarkVision1", RayRectangleTemplate.Symmetric(5, 40)),
            new(29627, 0x3966, "DarkVision2", RayRectangleTemplate.Symmetric(5, 41)),
            new(29626, 0x3966, "VoidGravity", new CircleTemplate(6), PartyAoe: "分散"),
            new(29630, 0x3988, "EndlessNightmare", null, "狂暴读条"),
        ], [30246, 29629, 29623, 29728], [0x3966, 0x3967, 0x3968]),
        new(0x396D, "Beatrice", p => TroiaArenas.Beatrice(p.Y),
        [
            new(29817, 0x233C, "BeatificScorn", new CircleTemplate(9)),
            new(29821, 0x233C, "DeathForeseen1", null, "背对视线"),
            new(29828, 0x233C, "DeathForeseen2", null, "背对视线"),
            new(29818, 0x396D, "EyeOfTroia", null, "全屏伤害"),
            new(29824, 0x396D, "Hush", null, "死刑"),
            new(29823, 0x233C, "VoidNail", new CircleTemplate(6), PartyAoe: "分散"),
            new(29822, 0x396D, "Voidshaker", new FanTemplate(20, MathF.PI * 2 / 3)),
            new(31207, 0x233C, "ToricVoid", new DonutTemplate(10, 20)),
            new(31208, 0x233C, "Antipressure", new CircleTemplate(6), PartyAoe: "分摊"),
        ], [872, 29820, 29811, 29812, 29813, 29814, 29815, 29816, 29819, 29829, 31206]),
        new(0x39C5, "Scarmiglione", p => TroiaArenas.ScarmiglioneStarting(p.Y),
        [
            new(30235, 0x39C5, "BlightedBedevilment", new CircleTemplate(9)),
            new(30260, 0x233C, "BlightedBladework", new CircleTemplate(25)),
            new(30261, 0x39C5, "BlightedSweep", new FanTemplate(40, MathF.PI)),
            new(30257, 0x39C5, "CursedEcho", null, "全屏伤害"),
            new(30241, 0x233C, "Nox", new CircleTemplate(10)),
            new(30232, 0x233C, "RottenRampage", new CircleTemplate(6)),
            new(30233, 0x233C, "RottenRampageSpread", new CircleTemplate(6), PartyAoe: "分散"),
            new(30236, 0x233C, "VacuumWave", null, "利用安全墙抵挡击退"),
            new(30243, 0x233C, "VoidVortex1", new CircleTemplate(6), PartyAoe: "分摊"),
            new(30254, 0x233C, "VoidVortex2", new CircleTemplate(6), PartyAoe: "分摊"),
            new(30242, 0x233C, "VoidGravity", new CircleTemplate(6), PartyAoe: "分散"),
            new(30263, 0x233C, "Firedamp", new CircleTemplate(5), "死刑", "分散"),
        ], [30258, 872, 30237, 30259, 30245, 30247, 30248, 30249, 30250, 30240, 30231, 30234, 30244, 30253, 30262]),
    ];
}
