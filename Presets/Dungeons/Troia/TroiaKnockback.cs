using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class TroiaKnockback
{
    internal static readonly (Vector2 A, Vector2 B)[] Walls =
    [
        (new(-36.947f, -278.523f), new(-45.457f, -281.453f)),
        (new(-45.288f, -281.348f), new(-51.652f, -287.712f)),
        (new(-51.547f, -287.543f), new(-54.477f, -296.053f)),
        (new(-54.477f, -299.947f), new(-51.547f, -308.457f)),
        (new(-51.652f, -308.288f), new(-45.288f, -314.652f)),
        (new(-45.457f, -314.547f), new(-36.947f, -317.477f)),
        (new(-33.053f, -317.478f), new(-24.543f, -314.548f)),
        (new(-24.712f, -314.652f), new(-18.348f, -308.288f)),
        (new(-18.453f, -308.457f), new(-15.523f, -299.947f)),
        (new(-15.523f, -296.053f), new(-18.453f, -287.543f)),
        (new(-18.348f, -287.712f), new(-24.712f, -281.348f)),
        (new(-24.543f, -281.453f), new(-33.053f, -278.523f)),
    ];

    internal static Vector3 Landing(Vector3 from, Vector3 origin, float hitbox, IGeometryShape arena, (Vector2 A, Vector2 B)[] walls)
    {
        var p = new Vector2(from.X, from.Z);
        var delta = p - new Vector2(origin.X, origin.Z);
        if (delta.LengthSquared() < .000001f) return from;
        var direction = Vector2.Normalize(delta);
        var distance = 30f;
        if (walls.Length != 0)
        {
            var intersection = walls.Select(w => RaySegment(p, direction, w.A, w.B)).Where(t => t <= 30).DefaultIfEmpty(float.MaxValue).Min();
            if (intersection < float.MaxValue)
                distance = Math.Min(distance, intersection - Math.Max((hitbox < .499f ? .5f : hitbox) - .499f, .001f));
            else
            {
                var boundary = float.MaxValue;
                foreach (var contour in arena.GetContours())
                    for (var i = 0; i < contour.Count; ++i)
                        boundary = Math.Min(boundary, RaySegment(p, direction, new(contour[i].X, contour[i].Z),
                            new(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z)));
                distance = Math.Min(distance, boundary + .001f);
            }
        }
        return from + new Vector3(direction.X, 0, direction.Y) * distance;
    }

    private static float RaySegment(Vector2 p, Vector2 direction, Vector2 a, Vector2 b)
    {
        static float Cross(Vector2 x, Vector2 y) => x.X * y.Y - x.Y * y.X;
        var edge = b - a;
        var determinant = Cross(direction, edge);
        if (MathF.Abs(determinant) < .000001f) return float.MaxValue;
        var t = Cross(a - p, edge) / determinant;
        var u = Cross(a - p, direction) / determinant;
        return t >= 0 && u is >= 0 and <= 1 ? t : float.MaxValue;
    }
}
