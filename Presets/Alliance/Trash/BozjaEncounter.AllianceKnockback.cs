using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed partial class BozjaEncounter
{
    // The source SimpleKnockbacks only draws sequential movements and warns
    // about the arena edge. It has no AddAIHints override.
    internal IEnumerable<(BozjaCast Cast, Vector3 From, Vector3 To)> AllianceSimpleMovements(BozjaActor viewer, BozjaRule rule)
    {
        if (!Active || rule.Kind != BozjaRuleKind.AllianceKnockback) yield break;
        var from = viewer.Actor.Position with { Y = 0 };
        foreach (var cast in Casts.Where(c => c.Rule == rule))
        {
            if (WindImmune(viewer, cast.Activation)) continue;
            var delta = rule.KnockbackPull ? cast.Origin - from : from - cast.Origin;
            var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
            Vector3 direction;
            if (rule.KnockbackSide != 0)
            {
                var sin = (float)Math.Sin(cast.Rotation);
                var cos = (float)Math.Cos(cast.Rotation);
                direction = rule.KnockbackSide < 0 ? new(cos, 0, -sin) : new(-cos, 0, sin);
            }
            else
            {
                if (length == 0) continue;
                var inverse = 1f / length;
                direction = new(delta.X * inverse, 0, delta.Z * inverse);
            }
            var distance = rule.KnockbackPull ? Math.Min(rule.KnockbackDistance, length) : rule.KnockbackDistance;
            if (distance <= 0) continue;
            if (rule.KnockbackStopAtWall)
            {
                float hit;
                if (Arena is AllianceArenaGeometry polygon) hit = polygon.Raycast(from, direction);
                else if (Arena is RectangleGeometry rectangle) hit = AllianceRectangleRay(rectangle, from, direction);
                else
                {
                    var circle = (CircleGeometry)Arena;
                    var offset = from - circle.Position;
                    var halfB = offset.X * direction.X + offset.Z * direction.Z;
                    var determinant = halfB * halfB - (offset.X * offset.X + offset.Z * offset.Z) + circle.Radius * circle.Radius;
                    hit = determinant < 0 ? float.MaxValue : -halfB + MathF.Sqrt(determinant);
                    if (hit < 0) hit = float.MaxValue;
                }
                distance = Math.Min(distance, hit - Math.Clamp(viewer.Actor.HitboxRadius - .499f,
                    .5f - .499f, viewer.Actor.HitboxRadius - .499f));
            }
            var to = from + distance * direction;
            yield return (cast, from, to);
            from = to;
        }
    }

    internal static float AllianceRectangleRay(RectangleGeometry rectangle, Vector3 from, Vector3 direction)
    {
        var sin = (float)Math.Sin(rectangle.Rotation);
        var cos = (float)Math.Cos(rectangle.Rotation);
        var offset = from - rectangle.Position;
        var ox = offset.X * cos + offset.Z * -sin;
        var oz = offset.X * sin + offset.Z * cos;
        var dx = direction.X * cos + direction.Z * -sin;
        var dz = direction.X * sin + direction.Z * cos;
        var inverseX = 1f / dx;
        var inverseZ = 1f / dz;
        var minimum = -float.Epsilon;
        var maximum = float.MaxValue;
        var x1 = (-rectangle.Width * .5f - ox) * inverseX;
        var x2 = (rectangle.Width * .5f - ox) * inverseX;
        var z1 = (-rectangle.Length * .5f - oz) * inverseZ;
        var z2 = (rectangle.Length * .5f - oz) * inverseZ;
        // These ordered comparisons intentionally retain source NaN handling
        // when a ray starts exactly on a wall and is parallel to it.
        static float Min(float a, float b) => a < b ? a : b;
        static float Max(float a, float b) => a > b ? a : b;
        minimum = Min(Max(x1, minimum), Max(x2, minimum));
        maximum = Max(Min(x1, maximum), Min(x2, maximum));
        minimum = Min(Max(z1, minimum), Max(z2, minimum));
        maximum = Max(Min(z1, maximum), Min(z2, maximum));
        if (minimum > maximum) return float.MaxValue;
        if (minimum >= 0) return minimum;
        return maximum >= 0 ? maximum : float.MaxValue;
    }
}
