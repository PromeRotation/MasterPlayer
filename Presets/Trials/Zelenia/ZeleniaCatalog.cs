using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ZeleniaCatalog
{
    internal const uint TERRITORY = 1270;
    internal static IGeometryShape Arena(float y, bool donut = false)
    {
        Vector2[] Ring(float radius) => Enumerable.Range(0, 64).Select(i =>
        {
            var (sin, cos) = Math.SinCos(i * (Math.PI * 2 / 64));
            return new Vector2((float)sin * radius, (float)cos * radius);
        }).ToArray();
        var center = new Vector3(100, y, 100);
        var outer = Ring(16);
        if (!donut) return new LocalPolygonGeometry(center, 0, outer);
        var inner = Ring(4);
        var triangles = new List<Vector2[]>();
        for (var i = 0; i < 64; ++i)
        {
            var j = (i + 1) % 64;
            triangles.Add([outer[i], outer[j], inner[i]]);
            triangles.Add([outer[j], inner[j], inner[i]]);
        }
        return new PolygonRegionGeometry(center, [outer], [inner], triangles.ToArray());
    }

    internal static DungeonDefinition[] Create() =>
    [
        new(0x47C3, "Zelenia", p => Arena(p.Y),
        [
            new(43112, 0, "PowerBreak1", RayRectangleTemplate.Symmetric(64, 24)),
            new(43113, 0, "PowerBreak2", RayRectangleTemplate.Symmetric(64, 24)),
            new(43126, 0, "HolyHazard", new FanTemplate(24, MathF.PI * 2 / 3)),
            new(43083, 0, "ThunderSlash", new FanTemplate(24, MathF.PI / 3)),
            new(43129, 0, "SpecterOfTheLost", null),
            new(43479, 0, "RosebloodBloom", null, "全屏伤害"),
            new(43127, 0, "ThornedCatharsis", null, "全屏伤害"),
            new(43071, 0, "ValorousAscension1", null, "全屏伤害"),
            new(43074, 0, "ValorousAscensionRect", null),
            new(43092, 0, "QueensCrusade", null),
            new(43084, 0, "AlexandrianThunderIVCircle1", null),
            new(43085, 0, "AlexandrianThunderIVDonut1", null),
            new(43446, 0, "AlexandrianThunderIVCircle2", null),
            new(43447, 0, "AlexandrianThunderIVDonut2", null),
            new(43102, 0, "AlexandrianThunderIII1", null),
            new(43439, 0, "AlexandrianThunderIII2", null),
        ],
        [43055, 43054, 43480, 43056, 43057, 43058, 43059, 43060, 43061, 43062, 43063,
            43124, 43122, 43120, 43125, 43121, 43123, 43065, 43128, 43078, 43079, 43093,
            43091, 43094, 43095, 43100, 43086, 43087, 43088, 43089, 43090, 43442, 43443,
            43070, 43072, 43073]),
    ];
}
