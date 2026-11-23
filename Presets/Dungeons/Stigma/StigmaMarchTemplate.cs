using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record StigmaMarchTemplate(IGeometryShape Arena, Vector3[] Offsets, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => new StigmaMarchGeometry(this);
    internal bool Unsafe(Vector3 point)
    {
        var from = point;
        foreach (var offset in Offsets)
        {
            var to = point + offset;
            if (!Arena.Contains(to, 0) || Dangers.Any(d => d.Contains(to, 0) || !d.Contains(from, 0) && d.IntersectsSegment(from, to, 0)))
                return true;
            from = to;
        }
        return false;
    }
}
