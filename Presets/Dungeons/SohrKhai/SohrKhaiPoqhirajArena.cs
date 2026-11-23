using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class SohrKhaiPoqhirajArena
{
    internal static IGeometryShape Create(int removed, float y)
    {
        // Union the base and broken-wall strips before tracing the exterior: internal
        // tile edges would incorrectly stop navigation and world-outline clipping.
        var rects = new List<(float X0, float X1, float Z0, float Z1)>
        {
            (395.5f, 404.5f, 104.166f - 19.75f, 104.166f + 19.75f),
        };
        for (var i = 0; i < 8; ++i)
        {
            if ((removed & 1 << i) == 0) continue;
            var j = i % 4;
            var x = i < 4 ? 395.25f : 404.75f;
            var z = 89.161f + 10 * j + (j == 0 ? .125f : j == 3 ? -.125f : 0);
            var halfLength = j is 0 or 3 ? 4.875f : 5;
            rects.Add((x - .25f, x + .25f, z - halfLength, z + halfLength));
        }
        var xs = rects.SelectMany(r => new[] { r.X0, r.X1 }).Distinct().Order().ToArray();
        var zs = rects.SelectMany(r => new[] { r.Z0, r.Z1 }).Distinct().Order().ToArray();
        var cells = new HashSet<(int X, int Z)>();
        for (var x = 0; x < xs.Length - 1; ++x)
            for (var z = 0; z < zs.Length - 1; ++z)
            {
                var px = (xs[x] + xs[x + 1]) / 2;
                var pz = (zs[z] + zs[z + 1]) / 2;
                if (rects.Any(r => px > r.X0 && px < r.X1 && pz > r.Z0 && pz < r.Z1)) cells.Add((x, z));
            }
        var edges = new Dictionary<(int X, int Z), (int X, int Z)>();
        foreach (var (x, z) in cells)
        {
            if (!cells.Contains((x, z - 1))) edges[(x, z)] = (x + 1, z);
            if (!cells.Contains((x + 1, z))) edges[(x + 1, z)] = (x + 1, z + 1);
            if (!cells.Contains((x, z + 1))) edges[(x + 1, z + 1)] = (x, z + 1);
            if (!cells.Contains((x - 1, z))) edges[(x, z + 1)] = (x, z);
        }
        var start = edges.Keys.First();
        var current = start;
        var vertices = new List<Vector3>();
        do
        {
            vertices.Add(new(xs[current.X], y, zs[current.Z]));
            current = edges[current];
        } while (current != start);
        return new WorldPolygonGeometry(vertices);
    }

    internal static Vector3 Landing(Vector3 point, float rotation, int removed)
    {
        var displacement = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation)) * 30;
        var stop = 1f;
        if (MathF.Abs(displacement.X) > .00001f)
            for (var i = 0; i < 8; ++i)
            {
                if ((removed & 1 << i) != 0) continue;
                var x = i < 4 ? 395.5f : 404.5f;
                var t = (x - point.X) / displacement.X;
                var z = point.Z + t * displacement.Z;
                var z0 = 89.161f + 10 * (i % 4) - 5;
                if (t >= 0 && t < stop && z >= z0 && z <= z0 + 10) stop = t;
            }
        return point + displacement * (stop < 1 ? Math.Max(0, stop - .00005f) : 1);
    }
}
