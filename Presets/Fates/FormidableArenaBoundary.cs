using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Dungeons;

// Immutable traversal geometry; only the independent display center follows the source party player.
internal sealed class FormidableArenaBoundary : ILocalViewBoundary
{
    private readonly RectangleGeometry navigation;
    internal FormidableArenaBoundary(Vector3 center, Vector3 viewCenter)
    {
        navigation = new(center, 0, 60, 60);
        ViewCenter = viewCenter;
        TraversalIndex = new(navigation.GetContours());
    }
    public Vector3? ViewCenter { get; internal set; }
    public bool ShowBoundary => false;
    public float ViewRadius => 30;
    public ArenaBoundaryIndex TraversalIndex { get; }
    public GeometryKind Kind => navigation.Kind;
    public Vector3 Position => navigation.Position;
    public float Rotation => 0;
    public bool Contains(Vector3 point, float epsilon = GeometryHelper.DefaultEpsilon) => navigation.Contains(point, epsilon);
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = GeometryHelper.DefaultEpsilon)
        => navigation.IntersectsSegment(start, end, epsilon);
    public GeometryBounds GetBounds() => navigation.GetBounds();
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => navigation.GetContours(segments);
}
