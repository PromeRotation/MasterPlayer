using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;
using MasterPlayer.Mechanics.PilgrimsTraverse;

namespace MasterPlayer.Mechanics.Dungeons;

internal static partial class HeavenswardIntroCatalog
{
    internal const uint REGULA = 560;
    internal const uint DEATHGAZE = 561;
    internal const uint ALICANTO = 598;
    internal const uint EDDA = 565;
    internal const uint IXTAB = 564;
    internal const uint AH_PUCH = 601;
    internal const uint ALDYNN = 533;
    internal const uint REGULA_VITH = 459;
    internal const uint TISIPHONE = 602;
    internal const uint NINGISHZIDA = 563;
    internal const uint ALFARD = 600;
    internal const uint DIVINE_INTERVENTION = 456;
    internal const uint GRAOULLY = 477;
    internal const uint SPURGE = 562;
    internal const uint KIRTIMUKHA = 599;
    internal const uint NYBETH = 597;
    internal const uint GODMOTHER = 596;
    internal const uint GUDANNA = 595;
    internal const uint BLACK_RIDER = 593;
    internal const uint YAQUARU = 594;

    internal static bool IsTerritory(uint territory) => territory is REGULA or DEATHGAZE or ALICANTO or EDDA or IXTAB or AH_PUCH
        or ALDYNN or REGULA_VITH or TISIPHONE or NINGISHZIDA or ALFARD or DIVINE_INTERVENTION or GRAOULLY
        or SPURGE or KIRTIMUKHA or NYBETH or GODMOTHER or GUDANNA or BLACK_RIDER or YAQUARU;

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        NYBETH => [NybethDefinition()],
        GODMOTHER => [GodmotherDefinition()],
        GUDANNA => [GudannaDefinition()],
        BLACK_RIDER => [BlackRiderDefinition()],
        YAQUARU => [YaquaruDefinition()],
        SPURGE or KIRTIMUKHA => [PalacePlantDefinition(territory)],
        GRAOULLY => [new(0x10B9, "Graoully", p => new WorldPolygonGeometry(
        [
            new(-483.91f, p.Y, -299.22f), new(-519.70f, p.Y, -272.85f),
            new(-546.66f, p.Y, -309.50f), new(-510.38f, p.Y, -336.53f),
        ]),
        [
            new(3095, 0, "Pillar Impact", new CircleTemplate(6.5f)),
            new(4259, 0, "Pillar Pierce", RayRectangleTemplate.Symmetric(4, 82.5f)),
            new(4260, 0, "Cauterize", RayRectangleTemplate.Symmetric(20, 55)),
            new(4261, 0, "Sheet of Ice", new CircleTemplate(5)),
        ], [])],
        DIVINE_INTERVENTION => [new(0x1010, "Ser Grinnaux and Ser Paulecrain", p => HeavenswardQuestArenas.DivineIntervention(p.Y),
        [
            new(3993, 0, "Lightning Bolt", null),
            new(1003, 0, "Iron Tempest", new CircleTemplate(5.5f)),
            new(720, 0, "Overpower", new FanTemplate(6.5f, MathF.PI / 2)),
            new(1316, 0, "Ring of Frost", new CircleTemplate(6.5f)),
            new(1135, 0, "Rive", RayRectangleTemplate.Symmetric(2, 30.5f)),
            new(866, 0, "Heartstopper", RayRectangleTemplate.Symmetric(3, 3.5f)),
            new(3992, 0, "Thunder Thrust", null, "全屏伤害"),
        ], [])],
        NINGISHZIDA => [new(0x16AC, "Ningishzida", p => NingishzidaArena(p.Y),
        [
            new(6427, 0, "Ball of Fire", null),
            new(6428, 0, "Ball of Ice", null),
            new(6429, 0, "Fear Itself", new DonutTemplate(5, 50)),
        ], [])],
        ALFARD => [new(0x181A, "Alfard", p => AlfardArena(p.Y),
        [
            new(7139, 0, "Ball of Fire", null),
            new(7140, 0, "Ball of Ice", null),
            new(7141, 0, "Fear Itself", new DonutTemplate(5, 50)),
        ], [])],
        TISIPHONE => [new(0x181C, "Tisiphone", p => AhPuchArena(p.Y),
        [
            new(7153, 0, "Blood Rain", null, "Heavy Raidwide damage! Also killing any add that is currently up"),
            new(7108, 0, "Dark Mist", new CircleTemplate(10)),
            new(7112, 0, "Desolation", RayRectangleTemplate.Symmetric(6, 57.3f)),
            new(7110, 0, "Fatal Allure", null, "Boss is life stealing from the succubus"),
            new(7148, 0, "Sweet Steel", new FanTemplate(7, MathF.PI / 2)),
            new(7113, 0, "Terror Eye", new CircleTemplate(6)),
            new(7177, 0, "Void Aero", RayRectangleTemplate.Symmetric(8, 42)),
            new(7150, 0, "Void Fire II", new CircleTemplate(5)),
            new(7109, 0, "Void Fire IV", new CircleTemplate(10)),
        ], [])],
        ALDYNN => [new(0x154E, "Flame General Aldynn", p => new CircleGeometry(new(-35.75f, p.Y, -205.5f), 15),
        [
            new(5763, 0, "Flaming Tizona", new CircleTemplate(6)),
            new(5765, 0, "The Curse", new PtDonutSectorTemplate(2, 7, MathF.PI)),
        ], [])],
        REGULA_VITH => [new(0xF1C, "Regula van Hydrus (VIth)", p => new CircleGeometry(new(252.75f, p.Y, 553), 19.5f),
        [
            new(4000, 0, "Hand of the Empire", new CircleTemplate(2)),
            new(4005, 0, "Terminus Est", null),
        ], [])],
        IXTAB => [new(0x16B9, "Ixtab", p => PalaceArena(new(-300, p.Y, -226), 24.18f, 32, new(-300, -250.954f), 1.25f),
        [
            new(6434, 0, "Accursed Pox", new CircleTemplate(8)),
            new(6430, 0, "Ancient Eruption", new CircleTemplate(4)),
            new(6431, 0, "Entropic Flame", RayRectangleTemplate.Symmetric(8, 53.8f)),
            new(6433, 0, "Scream", null, "Raidwide + Fear, Adds need to be dead by now"),
            new(6432, 0, "Shadow Flare", null, "全屏伤害"),
        ], [])],
        AH_PUCH => [new(0x181B, "Ah Puch", p => AhPuchArena(p.Y),
        [
            new(7146, 0, "Accursed Pox", new CircleTemplate(8)),
            new(7142, 0, "Ancient Eruption", new CircleTemplate(4)),
            new(7143, 0, "Entropic Flame", RayRectangleTemplate.Symmetric(8, 53.8f)),
            new(7145, 0, "Scream", null, "Raidwide + Fear, Adds need to be dead by now"),
            new(7144, 0, "Shadow Flare", null, "全屏伤害"),
        ], [])],
        DEATHGAZE => [new(0x1692, "Palace Deathgaze", p => PalaceArena(new(-300, p.Y, -220), 24.65f, 48, new(-300, -245), 1.25f),
        [
            new(6420, 0x1692, "Aero Blast", null, "全屏伤害"),
            new(6418, 0x1692, "Bombination", new CircleTemplate(12)),
            new(6419, 0x1692, "Lumisphere", new CircleTemplate(6)),
            new(6417, 0x1692, "Stormwind", new FanTemplate(18, MathF.PI / 2)),
        ], [])],
        ALICANTO => [new(0x1818, "Alicanto", p => PalaceArena(new(-300, p.Y, -236), 24.5f, 24, new(-298.763f, -261.119f), 1.55f),
        [
            new(7132, 0x1818, "Aero Blast", null, "全屏伤害"),
            new(7130, 0x1818, "Bombination", new CircleTemplate(12)),
            new(7131, 0x1818, "Lumisphere", new CircleTemplate(6)),
            new(7129, 0x1818, "Stormwind", new FanTemplate(18, MathF.PI / 2)),
        ], [])],
        EDDA => [new(0x16C6, "Edda Blackbosom", p => PalaceArena(new(300, p.Y, 374), 24.18f, 32, new(300, 349.299f), 1.25f),
        [
            new(6402, 0, "Black Honeymoon", null, "全屏伤害"),
            new(6403, 0, "Cold Feet", null),
            new(6400, 0, "Dark Harvest", null, "死刑"),
            new(6404, 0, "Desolation", RayRectangleTemplate.Symmetric(6, 57.3f)),
            new(6398, 0, "In Health", new CircleTemplate(16)),
            new(6399, 0, "In Health", new DonutTemplate(4.5f, 50)),
            new(6405, 0, "Terror Eye", new CircleTemplate(6)),
        ], [])],
        REGULA => [new(0x161E, "Regula van Hydrus", p => Polygon(new(230, p.Y, 79), 20.256f, 24),
        [
            new(6026, 0x161E, "Magitek Slug", RayRectangleTemplate.Symmetric(4, 60)),
            new(6031, 0x1620, "Aetherochemical Grenado", new CircleTemplate(8)),
            new(6032, 0x161F, "Self Detonate", null),
            new(6032, 0x1620, "Self Detonate", null),
            new(6027, 0x161E, "Magitek Spread", new FanTemplate(20.55f, 4 * MathF.PI / 3)),
        ], [])],
        _ => [],
    };

    private static LocalPolygonGeometry Polygon(Vector3 center, float radius, int edges)
    {
        var points = Enumerable.Range(0, edges).Select(i =>
        {
            var angle = i * (2 * Math.PI / edges);
            return new Vector2(radius * (float)Math.Sin(angle), -radius * (float)Math.Cos(angle));
        });
        return new(center, 0, points);
    }

    private static IGeometryShape PalaceArena(Vector3 center, float radius, int edges, Vector2 entranceCenter, float halfHeight)
    {
        var polygon = Polygon(center, radius, edges);
        var entrance = entranceCenter - new Vector2(center.X, center.Z);
        Vector2[] rectangle =
        [
            entrance + new Vector2(-20, -halfHeight), entrance + new Vector2(20, -halfHeight),
            entrance + new Vector2(20, halfHeight), entrance + new Vector2(-20, halfHeight),
        ];
        var region = (PolygonRegionGeometry)DungeonPolygonDifference.Build(polygon, [(Array.Empty<Vector2>(), rectangle)]);
        return RecenterPalaceRegion(region);
    }

    private static PolygonRegionGeometry RecenterPalaceRegion(PolygonRegionGeometry region)
    {
        var bounds = region.GetBounds();
        var arenaCenter = new Vector3((bounds.MinX + bounds.MaxX) / 2, region.Position.Y, (bounds.MinZ + bounds.MaxZ) / 2);
        var triangles = region.FillPolygons.Select(p => p.Select(v =>
            new Vector2(v.X - arenaCenter.X, v.Z - arenaCenter.Z)).ToArray()).ToArray();
        return new PolygonRegionGeometry(arenaCenter, triangles, [], triangles, region.GetContours());
    }
}
