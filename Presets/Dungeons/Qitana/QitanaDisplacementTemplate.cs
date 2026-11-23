using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record QitanaDisplacementTemplate(IGeometryShape Arena, Vector3 Origin, float Rotation,
    bool Pull, IGeometryShape[] Dangers) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new QitanaDisplacementGeometry(this);

    internal Vector3 Landing(Vector3 point)
    {
        if (Pull)
        {
            var delta = Origin - point;
            delta.Y = 0;
            var length = delta.Length();
            return length < .001f ? point : point + delta / length * MathF.Min(length, 50);
        }
        var destination = point + new Vector3(MathF.Sin(Rotation), 0, MathF.Cos(Rotation)) * 35;
        if (Arena.Contains(destination, 0)) return destination;
        // Eros has a convex rectangle and this displacement stops at its wall.
        var near = point;
        var far = destination;
        for (var i = 0; i < 16; ++i)
        {
            var middle = (near + far) * .5f;
            if (Arena.Contains(middle, 0)) near = middle;
            else far = middle;
        }
        return near;
    }
}
