using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record SirensongDisplacementTemplate(IGeometryShape Arena, Vector3 Origin, Vector3? Direction,
    float Distance, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new SirensongDisplacementGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        if (Direction is { } direction) return point + direction * Distance;
        var delta = Origin - point;
        delta.Y = 0;
        var length = delta.Length();
        return length < .001f ? point : point + delta / length * MathF.Min(length, Distance);
    }

    internal bool Unsafe(Vector3 point)
    {
        var landing = Landing(point);
        return !Arena.Contains(landing, 0) || Dangers.Any(d => d.Contains(landing)
            || Direction != null && !d.Contains(point) && d.IntersectsSegment(point, landing));
    }
}
