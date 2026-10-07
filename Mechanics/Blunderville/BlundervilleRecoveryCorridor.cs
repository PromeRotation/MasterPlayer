using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleRecoveryCorridor : IGeometryShape
{
    private readonly IReadOnlyList<IGeometryShape> _segments;
    public BlundervilleRecoveryCorridor(IReadOnlyList<Vector3> nodes, float halfWidth)
    {
        var segments = new List<IGeometryShape>();
        if (nodes.Count > 0) segments.Add(new CircleGeometry(nodes[0], halfWidth));
        for (var i = 1; i < nodes.Count; i++)
        {
            var delta = nodes[i] - nodes[i - 1]; delta.Y = 0;
            if (delta.LengthSquared() > 0.01f)
                segments.Add(new RayRectangleGeometry(nodes[i - 1], MathF.Atan2(delta.X, delta.Z), halfWidth * 2, delta.Length()));
            segments.Add(new CircleGeometry(nodes[i], halfWidth));
        }
        _segments = segments.AsReadOnly();
        Position = nodes.Count > 0 ? nodes[0] : Vector3.Zero;
    }
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position { get; }
    public float Rotation => 0;
    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => _segments.Any(s => s.Contains(point, epsilon));
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon) => _segments.Any(s => s.IntersectsSegment(start, end, epsilon));
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => _segments.SelectMany(s => s.GetContours(segments)).ToArray();
    public GeometryBounds GetBounds() => GeometryBounds.FromPoints(GetContours().SelectMany(c => c).ToArray());
}
