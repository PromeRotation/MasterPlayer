using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal sealed class Pt30BallSweepGeometry(Vector3 start, Vector3 end, float radius) : IGeometryShape
{
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position => (start + end) * .5f;
    public float Rotation => 0;

    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon)
        => GeometryHelper.DistanceSquaredPointToSegmentXZ(point, start, end) <= (radius + epsilon) * (radius + epsilon);

    public bool IntersectsSegment(Vector3 from, Vector3 to, float epsilon = GeometryHelper.DefaultEpsilon)
    {
        if (GeometryHelper.SegmentsIntersectXZ(start, end, from, to, epsilon)) return true;
        var distance = MathF.Min(
            MathF.Min(GeometryHelper.DistanceSquaredPointToSegmentXZ(from, start, end), GeometryHelper.DistanceSquaredPointToSegmentXZ(to, start, end)),
            MathF.Min(GeometryHelper.DistanceSquaredPointToSegmentXZ(start, from, to), GeometryHelper.DistanceSquaredPointToSegmentXZ(end, from, to)));
        return distance <= (radius + epsilon) * (radius + epsilon);
    }

    public GeometryBounds GetBounds() => new(MathF.Min(start.X, end.X) - radius, MathF.Min(start.Z, end.Z) - radius,
        MathF.Max(start.X, end.X) + radius, MathF.Max(start.Z, end.Z) + radius);

    // Never rendered or used as a goal. Analytic distance checks avoid rebuilding
    // and triangulating dozens of moving polygons each frame.
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}
