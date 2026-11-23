using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TrialKnockbackDrawing(string id, string name, Vector3 from, Vector3 to)
    {
        foreach (var aoe in DungeonMovementPreview(id, name, from, to))
        {
            if (aoe.Id.EndsWith("/landing", StringComparison.Ordinal) && Arena is CircleGeometry circle && !circle.Contains(to))
            {
                var direction = Vector3.Normalize(to - from);
                var relative = from - circle.Position;
                relative.Y = 0;
                var dot = Vector3.Dot(relative, direction);
                var distance = -dot + MathF.Sqrt(MathF.Max(0, dot * dot + circle.Radius * circle.Radius - relative.LengthSquared()));
                yield return aoe with { Position = from + direction * distance, Unclipped = true };
            }
            else if (aoe.Id.EndsWith("/landing", StringComparison.Ordinal) && Arena != null && !Arena.Contains(to))
            {
                var direction = new Vector2(to.X - from.X, to.Z - from.Z);
                var start = new Vector2(from.X, from.Z);
                var exits = new List<float>();
                float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;
                foreach (var contour in Arena.GetContours())
                    for (var i = 0; i < contour.Count; ++i)
                    {
                        var a = new Vector2(contour[i].X, contour[i].Z);
                        var b = contour[(i + 1) % contour.Count];
                        var edge = new Vector2(b.X, b.Z) - a;
                        var determinant = Cross(direction, edge);
                        if (MathF.Abs(determinant) < .000001f) continue;
                        var delta = a - start;
                        var t = Cross(delta, edge) / determinant;
                        var u = Cross(delta, direction) / determinant;
                        if (t >= 0 && t <= 1 && u >= 0 && u <= 1
                            && !Arena.Contains(Vector3.Lerp(from, to, t + .0001f), 0)) exits.Add(t);
                    }
                yield return aoe with { Position = exits.Count > 0 ? Vector3.Lerp(from, to, exits.Min()) : to, Unclipped = true };
            }
            else yield return aoe;
        }
    }
}
