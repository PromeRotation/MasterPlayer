using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Blunderville;

// Swept circular contact body. A rectangle adds nonphysical square corners
// that close the diagonal gaps between adjacent cylinders.
internal sealed class BlundervilleBumperSweep : IGeometryShape
{
    internal sealed record SnapshotTemplate(BlundervilleBumperSweep Shape) : GeometryTemplate
    {
        public override GeometryKind Kind => Shape.Kind;
        public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Shape;
    }
    private readonly Vector3 _from, _to;
    private readonly float _radius;
    public BlundervilleBumperSweep(Vector3 minimum, Vector3 maximum, float radius)
    {
        Position = (minimum+maximum)/2;
        _from = Position with { X = minimum.X };
        _to = Position with { X = maximum.X };
        // The recorded lane is horizontal; retain all small Z displacement
        // as additional radial uncertainty rather than discarding it.
        _radius = radius + (maximum.Z-minimum.Z)/2;
    }
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position { get; }
    public float Rotation => 0;
    public bool Contains(Vector3 point,float epsilon=GeometryHelper.DefaultEpsilon)
        => GeometryHelper.DistanceSquaredPointToSegmentXZ(point,_from,_to) <= (_radius+epsilon)*(_radius+epsilon);
    public bool IntersectsSegment(Vector3 start,Vector3 end,float epsilon=GeometryHelper.DefaultEpsilon)
    {
        var squared = (_radius+epsilon)*(_radius+epsilon);
        return GeometryHelper.SegmentsIntersectXZ(start,end,_from,_to,0)
            || MathF.Min(MathF.Min(GeometryHelper.DistanceSquaredPointToSegmentXZ(start,_from,_to),
                    GeometryHelper.DistanceSquaredPointToSegmentXZ(end,_from,_to)),
                MathF.Min(GeometryHelper.DistanceSquaredPointToSegmentXZ(_from,start,end),
                    GeometryHelper.DistanceSquaredPointToSegmentXZ(_to,start,end))) <= squared;
    }
    public GeometryBounds GetBounds() => new(_from.X-_radius,Position.Z-_radius,_to.X+_radius,Position.Z+_radius);
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments=64)
    {
        var half = Math.Max(4,segments/2); var points = new Vector3[(half+1)*2];
        // Circumscribe the circular caps so polygon consumers retain the
        // analytic body's clearance between contour samples.
        var radius = _radius/MathF.Cos(MathF.PI/(2*half));
        for(var i=0;i<=half;++i)
        {
            var angle = -MathF.PI/2+i*MathF.PI/half;
            var delta = new Vector3(MathF.Cos(angle)*radius,0,MathF.Sin(angle)*radius);
            points[i] = _to+delta;
            points[half+1+i] = _from-delta;
        }
        return [points];
    }
}
