using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class PtDonutSectorGeometry(Vector3 position, float rotation, float inner, float outer, float angle) : GeometryShape(position, rotation)
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        var delta = point - Position;
        var radius = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        return radius >= inner - epsilon && radius <= outer + epsilon
            && MathF.Abs(MathF.IEEERemainder(MathF.Atan2(delta.X, delta.Z) - Rotation, MathF.Tau)) <= angle * .5f + epsilon;
    }
    public override IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        var count = Math.Max(8, segments);
        var points = new List<Vector3>(2 * (count + 1));
        for (var i = 0; i <= count; ++i) Add(outer, i);
        for (var i = count; i >= 0; --i) Add(inner, i);
        return [points];
        void Add(float r, int i)
        {
            var a = Rotation - angle * .5f + angle * i / count;
            points.Add(Position + new Vector3(MathF.Sin(a) * r, 0, MathF.Cos(a) * r));
        }
    }
}
