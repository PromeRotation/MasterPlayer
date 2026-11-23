using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class MtGulgCatalog
{
    internal const uint TERRITORY = 822;
    internal static bool IsPrejudice(uint oid) => oid is 0x28F2 or 0x28EE or 0x28EF or 0x28F1;
    internal static bool Owns(uint oid) => oid is 0x28F2 or 0x27CA or 0x28F0 or 0x27CC or 0x28F3 or 0x27CE;
    internal static DungeonDefinition[] Create() =>
    [
        new(0x28F2, "Forgiven Prejudice", p => new CircleGeometry(p, 30),
        [
            new(16813, 0x28F1, "SanctifiedAero", RayRectangleTemplate.Symmetric(8, 40)),
            new(16815, 0x28F2, "PunitiveLight", null, "打断或眩晕全屏伤害"),
            new(16814, 0x28F2, "Sanctification", new FanTemplate(12, MathF.PI / 2)),
        ], [870, 872, 16812], [0x28EE, 0x28EF, 0x28F1]),
        new(0x27CA, "Forgiven Cruelty", p => MtGulgArenas.Cruelty(p.Y),
        [
            new(15611, 0x27CA, "Rake", null, "死刑"),
            new(16818, 0x27CA, "LumenInfinitum", RayRectangleTemplate.Symmetric(5, 40)),
            new(15616, 0x233C, "TyphoonWing", new FanTemplate(25, MathF.PI / 3)),
            new(17153, 0x233C, "TyphoonWingLong", new FanTemplate(25, MathF.PI / 3)),
            new(15613, 0x233C, "CycloneWing", null, "全屏伤害"),
            new(15619, 0x233C, "HurricaneWing", new CircleTemplate(10)),
        ], [872, 15615, 15614, 15617, 15618, 15612]),
        new(0x28F0, "Forgiven Apathy", p => p.Z > -106 ? MtGulgArenas.ApathySecond(p.Y) : MtGulgArenas.ApathyFirst(p.Y),
        [
            new(16245, 0x233C, "EarthShaker", new FanTemplate(60, MathF.PI / 3)),
            new(16815, 0x28F2, "PunitiveLight", null, "打断或眩晕全屏伤害"),
            new(16814, 0x28F2, "Sanctification", new FanTemplate(12, MathF.PI / 2)),
        ], [870, 872, 16812, 16242, 16243, 16244]),
        new(0x27CC, "Forgiven Whimsy", p => new RectangleGeometry(new(-240, p.Y, -50), 0, 29, 29),
        [
            new(15627, 0x27CC, "SacramentOfPenance", null),
            new(16989, 0x27CC, "ExegesisDiagonal", null),
            new(16987, 0x27CC, "ExegesisEastWest", null),
            new(15622, 0x27CC, "ExegesisNorthSouth", null),
            new(16988, 0x27CC, "ExegesisCross", null),
            new(15625, 0x27CC, "Catechism", null),
            new(15631, 0x27CC, "JudgmentDay", null, "进入一人塔"),
            new(15630, 0x27CD, "PerfectContrition", null),
        ], [15624, 15628, 15620, 15623, 15626, 15633, 15632, 15629]),
        new(0x28F3, "Forgiven Revelry", p => new RectangleGeometry(new(-240, p.Y, 176.3f), 0, 29.3f, 28.8f),
        [
            new(16250, 0x233C, "LeftPalm", RayRectangleTemplate.Symmetric(15, 30)),
            new(16248, 0x233C, "RightPalm", RayRectangleTemplate.Symmetric(15, 30)),
            new(16251, 0x2947, "LightShot", RayRectangleTemplate.Symmetric(4, 40)),
        ], [16246, 16249, 16247]),
        new(0x27CE, "Forgiven Obscenity", p => new RectangleGeometry(new(-240, p.Y, 237), 0, 29, 39),
        [
            new(15636, 0x27CE, "OrisonFortissimo", null),
            new(15638, 0x27CE, "DivineDiminuendo", new CircleTemplate(8)),
            new(15640, 0x27CE, "DivineDiminuendoBlessed", new CircleTemplate(8)),
            new(15649, 0x27CF, "DivineDiminuendoClone", new CircleTemplate(8)),
            new(15641, 0x233C, "DivineDiminuendoDonut", new DonutTemplate(10, 16)),
            new(18025, 0x233C, "DivineDiminuendoOuter", new DonutTemplate(18, 32)),
            new(15642, 0x27CE, "ConvictionMarcato", RayRectangleTemplate.Symmetric(5, 40)),
            new(15643, 0x233C, "ConvictionMarcatoHelper", RayRectangleTemplate.Symmetric(5, 40)),
            new(15648, 0x27CF, "ConvictionMarcatoClone", RayRectangleTemplate.Symmetric(5, 40)),
            new(15644, 0x27CE, "PenancePianissimo", null),
            new(15645, 0x27CE, "FeatherMarionette", null),
            new(17066, 0x27CE, "SolitaireRing", null),
            new(15653, 0x27CE, "GoldChaser", null),
            new(15634, 0x27CE, "SacramentSforzando", null),
        ], [872, 15639, 15637, 16846, 15652, 15655, 15635, 15654]),
    ];
}
