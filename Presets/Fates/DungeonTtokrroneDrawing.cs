using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private IEnumerable<DungeonAmbientAoe> TtokrroneArenaAoes(DungeonActor primary)
    {
        var boundary = Arena ?? TtokrroneArenas.Ttokrrone(primary.Position.Y);
        if (Active == null)
            yield return new("ttokrrone/precombat/arena", "场地", new LocalPolygonTemplate(
                boundary.GetContours()[0].Select(p => GeometryHelper.WorldToLocal(p, boundary.Position, 0))),
                boundary.Position, Navigate: false, OutlineOnly: true);
        if (primary.Dead || !primary.Targetable) yield break;
        var outside = !boundary.Contains(primary.Position, 0);
        var position = outside ? TtokrroneMarkerPosition(boundary, primary.Position) : primary.Position;
        yield return new($"ttokrrone/enemy/{primary.Id}", "敌人及朝向",
            new LocalPolygonTemplate([new(0, -.7f), new(.433f, .35f), new(-.433f, .35f)]),
            position, primary.Rotation, Navigate: false, OutlineOnly: outside, Unclipped: true, Color: new(1, 0, 0, 1));
    }

    internal static Vector3 TtokrroneMarkerPosition(IGeometryShape boundary, Vector3 position)
    {
        var offset = position - boundary.Position;
        // ArenaBoundsCustom deliberately leaves the almost-centred X-axis special case unclamped.
        if (MathF.Abs(offset.X) < .1f || MathF.Abs(offset.X) < 1 && MathF.Abs(offset.Z) < 1) return position;
        var best = position;
        var distance = float.PositiveInfinity;
        var point = new Vector2(position.X, position.Z);
        foreach (var contour in boundary.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var a = new Vector2(contour[i].X, contour[i].Z);
                var b = new Vector2(contour[(i + 1) % contour.Count].X, contour[(i + 1) % contour.Count].Z);
                var edge = b - a;
                var projection = Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0, 1);
                var candidate = a + edge * projection;
                var next = Vector2.DistanceSquared(point, candidate);
                if (next >= distance) continue;
                distance = next;
                best = new(candidate.X, position.Y, candidate.Y);
            }
        return best;
    }
}
