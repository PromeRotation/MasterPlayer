using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

// Navigation-only chord capsule; the renderer separately shows the real 4m ball.
internal sealed record Pt30BallSweepTemplate(Vector3 Start, Vector3 End, float Radius) : GeometryTemplate
{
    public override GeometryKind Kind => GeometryKind.WorldPolygon;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride)
        => new Pt30BallSweepGeometry(Start, End, Radius);
}
