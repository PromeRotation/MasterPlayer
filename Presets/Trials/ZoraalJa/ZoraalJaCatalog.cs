using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class ZoraalJaCatalog
{
    internal const uint TERRITORY = 1200;
    internal static IGeometryShape Arena(float y, bool small = false)
        => new RectangleGeometry(new(100, y, 100), MathF.PI / 4, small ? 20 : 40, small ? 20 : 40);

    internal static GeometryTemplate Transition(float y)
    {
        var center = new Vector3(100, y, 100);
        var original = new LocalPolygonGeometry(center, MathF.PI / 4,
            [new(-20, -20), new(20, -20), new(20, 20), new(-20, 20)]);
        var inner = Arena(y, true).GetContours()[0].Select(p => new Vector2(p.X - 100, p.Z - 100)).ToArray();
        return new DungeonShapeTemplate(DungeonPolygonDifference.Build(original, [([], inner)]));
    }

    internal static DungeonDefinition[] Create() =>
    [
        new(0x42A9, "ZoraalJa", p => Arena(p.Y),
        [
            new(37707, 0, "SoulOverflow", null, "全屏伤害"),
            new(37708, 0, "CalamitysEdge", null, "全屏伤害"),
            new(37714, 0, "DoubleEdgedSwords", new FanTemplate(30, MathF.PI)),
            new(37715, 0, "PatricidalPique", null, "死刑"),
            new(37709, 0, "Burst", new CircleTemplate(8)),
            new(38184, 0, "VorpalTrailFirst", null),
            new(37712, 0, "VorpalTrailSecond", null),
        ], [6497, 39137, 37744, 37713, 37710, 38183, 37711]),
        new(0x42B4, "ZoraalJaP2", p => Arena(p.Y),
        [
            new(37734, 0, "SmitingCircuitDonut", new DonutTemplate(10, 30)),
            new(37742, 0, "HalfCircuitDonut", new DonutTemplate(10, 30)),
            new(37735, 0, "SmitingCircuitCircle", new CircleTemplate(10)),
            new(37743, 0, "HalfCircuitCircle", new CircleTemplate(10)),
            new(37716, 0, "DawnOfAnAge", null, "全屏伤害"),
            new(37718, 0, "Actualize", null, "全屏伤害"),
            new(37754, 0, "BitterReaping", null, "死刑"),
            new(37720, 0, "ChasmOfVollok", RayRectangleTemplate.Symmetric(5, 5)),
            new(37722, 0, "ChasmOfVollokResolve", null),
            new(37738, 0, "HalfFull", RayRectangleTemplate.Symmetric(60, 60)),
            new(37741, 0, "HalfCircuitRect", RayRectangleTemplate.Symmetric(60, 60)),
        ], [37798, 37717, 37731, 37732, 37733, 37719, 37753, 37721, 37723, 37726, 37728, 37727, 37729,
            37730, 37736, 37737, 37739, 37740, 37748, 37749, 35567, 37750, 37752]),
    ];
}
