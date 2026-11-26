using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

internal static class DelubrumDahuGeometry
{
    internal static float WallDistance(IGeometryShape arena, Vector3 from, Vector3 direction)
    {
        // Work in the source polygon's local plane to avoid adding the world
        // center before subtracting it again at every edge intersection.
        var polygon = (LocalPolygonGeometry)arena;
        var origin = new Vector2(from.X - arena.Position.X, from.Z - arena.Position.Z);
        var dir = new Vector2(direction.X, direction.Z);
        var points = polygon.LocalPoints;
        var best = float.MaxValue;
        for (var i = 0; i < points.Count; ++i)
        {
            var a = -points[i];
            var b = -points[(i + 1) % points.Count];
            var edge = b - a;
            var denominator = dir.X * edge.Y - dir.Y * edge.X;
            var delta = a - origin;
            if (MathF.Abs(denominator) <= 1e-9f)
            {
                // Source polygon raycasts include the first collinear overlap,
                // including zero when the player is already on the wall.
                if (MathF.Abs(delta.X * dir.Y - delta.Y * dir.X) <= 1e-7f)
                {
                    var inverseLength = 1f / (dir.LengthSquared() + 1e-20f);
                    var first = Vector2.Dot(delta, dir) * inverseLength;
                    var last = Vector2.Dot(b - origin, dir) * inverseLength;
                    var overlap = MathF.Max(0, MathF.Min(first, last));
                    if (overlap <= MathF.Max(first, last)) best = MathF.Min(best, overlap);
                }
                continue;
            }
            var t = (delta.X * edge.Y - delta.Y * edge.X) / denominator;
            var u = (delta.X * dir.Y - delta.Y * dir.X) / denominator;
            if (t >= 0 && u >= 0 && u <= 1) best = MathF.Min(best, t);
        }
        return best;
    }
}
