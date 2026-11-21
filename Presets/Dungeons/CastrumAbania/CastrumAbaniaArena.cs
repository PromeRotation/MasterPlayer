using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class CastrumAbaniaArena
{
    internal static IGeometryShape For(uint bossOid, float y)
    {
        var center = bossOid switch
        {
            CastrumAbaniaCatalog.MagnaRoader => new Vector3(-212.99f, y, 186.99f),
            CastrumAbaniaCatalog.SubjectNumberXXIV => new Vector3(10.5f, y, 186.5f),
            CastrumAbaniaCatalog.Inferno => new Vector3(282.5f, y, -27.25f),
            _ => Vector3.Zero,
        };
        var radius = bossOid == CastrumAbaniaCatalog.Inferno ? 19.51f : 19.55f;
        var points = new CircleGeometry(center, radius).GetContours(256)[0].ToList();
        // All BMR exclusions cut across an entrance at the edge of the circle.
        // Clip against the inside face of each rectangle, retaining the closed arena.
        if (bossOid == CastrumAbaniaCatalog.MagnaRoader)
        {
            points = Clip(points, new(0, 0, -1), -168.25f);
            points = Clip(points, Vector3.UnitZ, 205.9f);
        }
        else if (bossOid == CastrumAbaniaCatalog.SubjectNumberXXIV)
        {
            points = Clip(points, Vector3.UnitZ, 205.5f);
            points = Clip(points, Vector3.UnitX, 28.9f);
        }
        else if (bossOid == CastrumAbaniaCatalog.Inferno)
        {
            var rotation = -17.532f * MathF.PI / 180;
            var normal = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
            var entrance = new Vector3(277.157f, y, -7.933f);
            points = Clip(points, normal, Vector3.Dot(entrance, normal) - 1.25f);
        }
        return new WorldPolygonGeometry(points);
    }

    private static List<Vector3> Clip(List<Vector3> points, Vector3 normal, float limit)
    {
        var result = new List<Vector3>();
        var previous = points[^1];
        var previousDistance = Vector3.Dot(previous, normal) - limit;
        foreach (var current in points)
        {
            var distance = Vector3.Dot(current, normal) - limit;
            if ((previousDistance <= 0) != (distance <= 0))
                result.Add(Vector3.Lerp(previous, current, previousDistance / (previousDistance - distance)));
            if (distance <= 0) result.Add(current);
            previous = current;
            previousDistance = distance;
        }
        return result;
    }
}
