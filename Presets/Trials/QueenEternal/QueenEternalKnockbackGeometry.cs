using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class QueenEternalKnockbackGeometry(QueenEternalKnockbackTemplate template) : GeometryShape(template.Center)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => template.Unsafe(point);
    public override GeometryBounds GetBounds() => new RectangleGeometry(template.Center, 0, 40, 40).GetBounds();
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];

    internal static float FirstBoundaryDistance(IGeometryShape arena, Vector3 from, Vector3 to)
    {
        var a = new Vector2(from.X, from.Z); var d = new Vector2(to.X - from.X, to.Z - from.Z);
        var length = d.Length();
        if (length == 0) return float.PositiveInfinity;
        var fraction = float.PositiveInfinity;
        foreach (var contour in arena.GetContours())
            for (var i = 0; i < contour.Count; ++i)
            {
                var b = new Vector2(contour[i].X, contour[i].Z);
                var next = contour[(i + 1) % contour.Count];
                var edge = new Vector2(next.X, next.Z) - b;
                var determinant = Cross(d, edge);
                if (MathF.Abs(determinant) < .0000001f) continue;
                var delta = b - a;
                var t = Cross(delta, edge) / determinant;
                var u = Cross(delta, d) / determinant;
                if (t >= 0 && u >= 0 && u <= 1) fraction = MathF.Min(fraction, t);
            }
        return fraction * length;
        static float Cross(Vector2 x, Vector2 y) => x.X * y.Y - x.Y * y.X;
    }
}
