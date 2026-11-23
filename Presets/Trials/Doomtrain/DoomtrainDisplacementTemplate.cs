using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record DoomtrainDisplacementTemplate(IGeometryShape Arena, Vector3 Displacement, int SafeWallCar = 0) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new DoomtrainDisplacementGeometry(this);

    internal bool Unsafe(Vector3 point)
    {
        if (SafeWallCar == 0) return !Arena.Contains(point + Displacement, 0);
        // One shared draw-in, independent of party slot. The ray travels -Z;
        // the safe rectangles extend from each wall towards +Z, excluding t=20.
        var walls = SafeWallCar == 2
            ? new[] { (Min: 100.1f, Max: 104.9f, Z: 150.1f), (Min: 95.1f, Max: 99.9f, Z: 160.1f) }
            : [(Min: 105.1f, Max: 109.9f, Z: 305.1f)];
        return !walls.Any(w => point.X >= w.Min && point.X <= w.Max && point.Z >= w.Z && point.Z < w.Z + 20);
    }
}
