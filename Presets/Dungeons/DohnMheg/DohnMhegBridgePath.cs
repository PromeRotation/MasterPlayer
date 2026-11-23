using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class DohnMhegBridgePath
{
    // Midpoints between the two edges of the upstream plank polygon. Sharp
    // turns need explicit points: raster smoothing would cut across the chasm.
    private static readonly Vector2[] CENTERS =
    [
        new(-142.72f, -233.5f),
        new(-140.8555f, -246.2535f),
        new(-129.965f, -242.3645f),
        new(-114.7f, -244.4f),
        new(-125.85f, -249.5f),
        new(-124.245f, -254.5f),
    ];

    internal static Vector3[] Build(Vector3 player, Vector3 target, IGeometryShape arena)
    {
        if (!arena.Contains(player) || !arena.Contains(target)) return [];
        var route = CENTERS.Select(p => new Vector3(p.X, player.Y, p.Y)).Append(target with { Y = player.Y }).ToArray();
        var start = route.Length - 1;
        while (start >= 0 && !SafeSegment(arena, player, route[start])) --start;
        if (start < 0) return [];
        var result = new List<Vector3>();
        var previous = player;
        for (var i = start; i < route.Length; ++i)
        {
            // The fixed suffix is verified against the exported arena in the
            // regression; the runtime validates the complete timed route again.
            if (Vector3.DistanceSquared(previous, route[i]) > .0009f) result.Add(route[i]);
            previous = route[i];
        }
        return result.ToArray();
    }

    internal static bool SafeSegment(IGeometryShape arena, Vector3 start, Vector3 end)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(start, end) / .04f));
        for (var i = 0; i <= steps; ++i)
        {
            var point = Vector3.Lerp(start, end, (float)i / steps);
            if (!arena.Contains(point)) return false;
            for (var direction = 0; direction < 8; ++direction)
            {
                var angle = direction * MathF.PI / 4;
                if (!arena.Contains(point + new Vector3(MathF.Sin(angle), 0, MathF.Cos(angle)) * .08f)) return false;
            }
        }
        return true;
    }
}
