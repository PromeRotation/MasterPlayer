using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using Host = PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Radar;

internal static class HostDrawingGeometry
{
    public static Host.IGeometryShape Convert(IGeometryShape shape) => shape switch
    {
        CircleGeometry s => new Host.CircleGeometry(s.Position, s.Radius),
        DonutGeometry s => new Host.DonutGeometry(s.Position, s.InnerRadius, s.OuterRadius),
        FanGeometry s => new Host.FanGeometry(s.Position, s.Rotation, s.Radius, s.AngleRadians),
        RectangleGeometry s => new Host.RectangleGeometry(s.Position, s.Rotation, s.Width, s.Length),
        RayRectangleGeometry s => new Host.RayRectangleGeometry(s.Position, s.Rotation, s.MinLocalX, s.MaxLocalX, s.Length),
        LocalPolygonGeometry s => new Host.LocalPolygonGeometry(s.Position, s.Rotation, s.LocalPoints),
        WorldPolygonGeometry s => new Host.WorldPolygonGeometry(s.WorldPoints),
        _ => new CustomGeometry(shape),
    };

    private sealed class CustomGeometry(IGeometryShape shape) : Host.IGeometryShape
    {
        public Host.GeometryKind Kind => (Host.GeometryKind)shape.Kind;
        public Vector3 Position => shape.Position;
        public float Rotation => shape.Rotation;
        public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => shape.Contains(point, epsilon);
        public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => shape.IntersectsSegment(start, end, epsilon);
        public Host.GeometryBounds GetBounds()
        {
            var bounds = shape.GetBounds();
            return new(bounds.MinX, bounds.MinZ, bounds.MaxX, bounds.MaxZ);
        }
        public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => shape.GetContours(segments);
    }
}
