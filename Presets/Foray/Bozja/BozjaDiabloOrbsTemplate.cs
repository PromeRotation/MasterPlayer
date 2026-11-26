using System.Numerics;
using MasterPlayer.Mechanics.Dungeons;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Bozja;

internal sealed record BozjaDiabloOrbsTemplate(Vector3[] Centers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    internal float Distance(float x, float z)
    {
        var result = float.NegativeInfinity;
        foreach (var p in Centers)
            result = MathF.Max(result, -MathF.Max(MathF.Abs(x - p.X) - .5f, MathF.Abs(z - p.Z) - .5f));
        return result;
    }

    internal IEnumerable<IGeometryShape> Convert(Vector3 center, float radius)
        => GeometryGridSampler.Sample(center, radius, Distance);

    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => throw new InvalidOperationException("Orb navigation requires intersection sampling.");
}
