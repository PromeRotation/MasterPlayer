using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// UndyingHatred constructs SDRect from two world points. World rounding and
// the host's second direction normalization are observable at the boundary.
internal sealed record BozjaPhantomRectTemplate(Vector3 From, Vector3 To) : GeometryTemplate
{
    private readonly Vector3 delta = To - From;
    public override GeometryKind Kind => GeometryKind.Rectangle;

    internal float Distance(float x, float z)
    {
        var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        var inverse = 1f / length;
        var dx = delta.X * inverse;
        var dz = delta.Z * inverse;
        var px = x - From.X;
        var pz = z - From.Z;
        var along = px * dx + pz * dz;
        var side = px * dz - pz * dx;
        return MathF.Max(MathF.Max(along - length, -along), MathF.Abs(side) - 24);
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
    {
        var length = MathF.Sqrt(delta.X * delta.X + delta.Z * delta.Z);
        var inverse = 1f / length;
        var dx = delta.X * inverse;
        var dz = delta.Z * inverse;
        var dirLength = MathF.Sqrt(dx * dx + dz * dz);
        dx /= dirLength;
        dz /= dirLength;
        var center = new Vector3(From.X + dx * length * .5f, position.Y, From.Z + dz * length * .5f);
        return new RectangleGeometry(center, MathF.Atan2(dx, dz), 48, length);
    }
}
