using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

// Energy's future union is sampled as one source SDF, separately from the
// immediate R1 circle union and the R1/L3 visual capsules.
internal sealed record BozjaEnergyTemplate((Vector3 Position, float Rotation)[] Sources) : GeometryTemplate
{
    private static readonly BozjaCapsuleTemplate capsule = new(1.5f, 3);
    public override GeometryKind Kind => GeometryKind.WorldPolygon;

    internal float Distance(float x, float z)
    {
        var minimum = float.MaxValue;
        foreach (var (position, rotation) in Sources)
        {
            var value = capsule.Distance(position, rotation, new(x, position.Y, z));
            if (value < minimum) minimum = value;
        }
        return minimum;
    }

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Energy union requires the finite navigation sampler.");
}
