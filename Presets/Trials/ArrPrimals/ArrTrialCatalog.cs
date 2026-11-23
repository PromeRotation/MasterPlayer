using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ArrTrialCatalog
{
    internal const uint IFRIT = 1045;
    internal const uint TITAN = 1046;
    internal const uint GARUDA = 1047;

    internal static bool IsTerritory(uint territory) => territory is IFRIT or TITAN or GARUDA;
    internal static bool Owns(uint oid, uint territory) => territory switch
    {
        IFRIT => oid == 0xCF, TITAN => oid == 0xF6, GARUDA => oid == 0xEF, _ => false,
    };

    internal static IGeometryShape IfritArena(float y)
        => new LocalPolygonGeometry(new(0, y, 0), 0, Enumerable.Range(0, 36).Select(i =>
            new Vector2(MathF.Sin(MathF.Tau * i / 36), MathF.Cos(MathF.Tau * i / 36)) * 19.5f));

    internal static DungeonDefinition[] Create(uint territory) => territory switch
    {
        IFRIT => [new(0xCF, "Ifrit", p => IfritArena(p.Y),
        [
            new(733, 0, "Eruption", new CircleTemplate(8)),
            new(734, 0, "RadiantPlume", new CircleTemplate(8)),
        ], [872, 453, 454, 455, 456, 458])],
        TITAN => [new(0xF6, "Titan", p => new CircleGeometry(new(0, p.Y, 0), 20),
        [
            new(651, 0, "Geocrush", new CircleTemplate(18)),
            new(650, 0, "Landslide", RayRectangleTemplate.Symmetric(6, 40)),
            new(973, 0, "WeightOfTheLand", new CircleTemplate(6)),
        ], [872, 642, 641, 645, 28799, 652, 644])],
        GARUDA => [new(0xEF, "Garuda", p => new CircleGeometry(new(0, p.Y, 0), 21),
        [
            new(659, 0, "Slipstream", new FanTemplate(11.7f, MathF.PI / 2)),
            new(667, 0, "MistralSongP1", null),
            new(660, 0, "MistralSongP2", new FanTemplate(31.7f, MathF.PI * 2 / 3)),
            new(661, 0, "MistralShriek", new CircleTemplate(24.7f)),
        ], [870, 656, 657, 658, 662, 664, 665])],
        _ => [],
    };
}
