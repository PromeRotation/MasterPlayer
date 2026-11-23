using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ArboretumCatalog
{
    internal const uint TERRITORY = 511;
    internal static readonly IGeometryShape QueenArena = ArboretumArenas.QueenHawk(0);
    internal static readonly uint[] ANTS = [0x1423, 0x1426, 0x146E, 0x1425, 0x1424, 0x1422, 0x1427, 0x1428, 0x1429];
    internal static readonly uint[] NARBROOI = [0x142A, 0x142C, 0x142B, 0x142E, 0x142D];
    internal static readonly uint[] MALBORO = [0x141F, 0x1421, 0x141E, 0x1420];
    internal static bool IsTrash(uint oid) => oid is 0x141F or 0x1423 or 0x142A;
    internal static bool Owns(uint oid) => IsTrash(oid) || oid is 0x142F or 0x1432 or 0x1434;

    internal static DungeonDefinition[] Create() =>
    [
        new(0x141F, "Malboro Terra", p => MalboroArenas.First(p.Y),
        [
            new(4532, 0, "OffalBreath", new CircleTemplate(6)),
            new(5355, 0, "Spiritus", new FanTemplate(6, MathF.PI / 3)),
        ], [871, 872, 923, 5354], MALBORO),
        new(0x142F, "Rose Garden", p => ArboretumArenas.RoseGarden(p.Y),
        [
            new(5333, 0x142F, "ExtremelyBadBreath", new FanTemplate(24.775f, MathF.PI / 2)),
            new(5230, 0x14C5, "ExtremelyBadBreathFirst", new FanTemplate(24.775f, MathF.PI / 2)),
            new(5316, 0x14C6, "EarthyBreath", new FanTemplate(7.5f, MathF.PI / 6)),
            new(5229, 0x142F, "BurrFester", null, "全屏伤害"),
        ], [872, 5228, 5231, 5232, 5224, 5225, 5226, 5234]),
        new(0x1432, "Queen Hawk", p => ArboretumArenas.QueenHawk(p.Y),
        [
            new(5237, 0x1432, "Apitoxin", new CircleTemplate(6)),
            new(5240, 0x1433, "StraightSpindle", RayRectangleTemplate.Symmetric(3, 51.08f)),
            new(5243, 0x1B2, "Crossfire", RayRectangleTemplate.Symmetric(14, 50.5f)),
        ], [871, 5236, 5378, 5241, 5235, 5238, 5233, 5239, 5242]),
        new(0x1434, "Belladonna", p => ArboretumArenas.Belladonna(p.Y),
        [
            new(5215, 0x1434, "AtropineSpore", new DonutTemplate(9, 40)),
            new(5221, 0x1434, "SoulVacuum", null, "全屏伤害"),
            new(5216, 0x1434, "FrondFatale", null, "背对视线"),
        ], [872, 5219, 5379, 5222, 5220, 5217, 5218, 5223]),
        new(0x1423, "Velveteen Ant", p => ArboretumArenas.AntFirst(p.Y),
        [
            new(417, 0x1422, "TortoiseStomp", new CircleTemplate(11)),
            new(4526, 0x146E, "UnfinalSting", RayRectangleTemplate.Symmetric(3, 9.08f)),
            new(2482, 0x1427, "FinalSting", null, "单体伤害"),
        ], [870, 871, 872, 523, 5369, 5370, 4525], ANTS),
        new(0x142A, "Tangled Narbrooi", p => ArboretumArenas.NarbrooiFirst(p.Y),
        [
            new(5353, 0x142C, "TheWoodRemembers", new FanTemplate(7.85f, MathF.PI * 2 / 3)),
            new(5350, 0x142B, "MindOfItsOwn", new FanTemplate(9, MathF.PI * 2 / 3)),
            new(4701, 0x142D, "Canopy", new FanTemplate(7.7f, MathF.PI * 2 / 3)),
            new(5352, 0x142C, "Hurl", new CircleTemplate(6)),
        ], [872, 870, 4502, 5351], NARBROOI),
    ];
}
