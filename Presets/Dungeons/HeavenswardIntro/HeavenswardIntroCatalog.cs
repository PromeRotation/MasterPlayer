using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class HeavenswardIntroCatalog
{
    internal const uint REGULA = 560;
    internal const uint DEATHGAZE = 561;
    internal const uint ALICANTO = 598;

    internal static bool IsTerritory(uint territory) => territory is REGULA or DEATHGAZE or ALICANTO;

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        DEATHGAZE => [new(0x1692, "Palace Deathgaze", p => PalaceArena(p.Y, false),
        [
            new(6420, 0x1692, "Aero Blast", null, "全屏伤害"),
            new(6418, 0x1692, "Bombination", new CircleTemplate(12)),
            new(6419, 0x1692, "Lumisphere", new CircleTemplate(6)),
            new(6417, 0x1692, "Stormwind", new FanTemplate(18, MathF.PI / 2)),
        ], [])],
        ALICANTO => [new(0x1818, "Alicanto", p => PalaceArena(p.Y, true),
        [
            new(7132, 0x1818, "Aero Blast", null, "全屏伤害"),
            new(7130, 0x1818, "Bombination", new CircleTemplate(12)),
            new(7131, 0x1818, "Lumisphere", new CircleTemplate(6)),
            new(7129, 0x1818, "Stormwind", new FanTemplate(18, MathF.PI / 2)),
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

    private static IGeometryShape PalaceArena(float y, bool alicanto)
    {
        var center = new Vector3(-300, y, alicanto ? -236 : -220);
        var polygon = Polygon(center, alicanto ? 24.5f : 24.65f, alicanto ? 24 : 48);
        var entrance = new Vector2(alicanto ? 1.237f : 0, alicanto ? -25.119f : -25);
        var halfHeight = alicanto ? 1.55f : 1.25f;
        Vector2[] rectangle =
        [
            entrance + new Vector2(-20, -halfHeight), entrance + new Vector2(20, -halfHeight),
            entrance + new Vector2(20, halfHeight), entrance + new Vector2(-20, halfHeight),
        ];
        var region = (PolygonRegionGeometry)DungeonPolygonDifference.Build(polygon, [(Array.Empty<Vector2>(), rectangle)]);
        var bounds = region.GetBounds();
        var arenaCenter = new Vector3((bounds.MinX + bounds.MaxX) / 2, y, (bounds.MinZ + bounds.MaxZ) / 2);
        var triangles = region.FillPolygons.Select(p => p.Select(v =>
            new Vector2(v.X - arenaCenter.X, v.Z - arenaCenter.Z)).ToArray()).ToArray();
        return new PolygonRegionGeometry(arenaCenter, triangles, [], triangles, region.GetContours());
    }
}
