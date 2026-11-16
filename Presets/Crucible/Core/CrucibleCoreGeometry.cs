using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Crucible;

// 斗兽共用几何类型：雷达、跟随安全检查和躲避膨胀也会用到。
internal sealed record ResolvedGeometryTemplate(IGeometryShape Shape) : GeometryTemplate
{
    public override GeometryKind Kind => Shape.Kind;
    public override IGeometryShape Resolve(Vector3 position, float rotation, float? lengthOverride) => Shape;
    public ResolvedGeometryTemplate Inflate(float margin) => new(Shape switch
    {
        CircleGeometry c => new CircleGeometry(c.Position, c.Radius + margin),
        DonutGeometry d => new DonutGeometry(d.Position, Math.Max(0, d.InnerRadius - margin), d.OuterRadius + margin),
        RectangleGeometry r => new RectangleGeometry(r.Position, r.Rotation, r.Width + margin * 2, r.Length + margin * 2),
        FanGeometry f => new FanGeometry(f.Position, f.Rotation, f.Radius + margin, f.AngleRadians),
        _ => Shape
    });
}

// Keep exact positioning holes: freezing a 0.6y pocket on the ordinary 0.5y grid fills it in.
// This is a navigation constraint, so ResolvedGeometryTemplate does not inflate it as damage.
internal sealed class CircleComplementGeometry(Vector3 center, float safeRadius, GeometryBounds bounds) : IGeometryShape
{
    public float SafeRadius { get; } = safeRadius;
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position { get; } = center;
    public float Rotation => 0;
    public bool Contains(Vector3 point, float epsilon = 0.0001f)
        => bounds.Contains(point, epsilon) && OutsidePocket(point, epsilon);
    private bool OutsidePocket(Vector3 point, float epsilon)
    {
        var dx = point.X - Position.X;
        var dz = point.Z - Position.Z;
        var radius = Math.Max(0, SafeRadius - epsilon);
        return dx * dx + dz * dz >= radius * radius;
    }
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = 0.0001f)
    {
        var lo = 0f;
        var hi = 1f;
        var delta = end - start;
        bool Clip(float origin, float direction, float min, float max)
        {
            if (Math.Abs(direction) < 1e-8f) return origin >= min && origin <= max;
            var a = (min - origin) / direction;
            var b = (max - origin) / direction;
            lo = Math.Max(lo, Math.Min(a, b));
            hi = Math.Min(hi, Math.Max(a, b));
            return lo <= hi;
        }
        if (!Clip(start.X, delta.X, bounds.MinX - epsilon, bounds.MaxX + epsilon)
            || !Clip(start.Z, delta.Z, bounds.MinZ - epsilon, bounds.MaxZ + epsilon)) return false;
        // A circle is convex: the clipped segment stays in its hole iff both endpoints do.
        return OutsidePocket(start + delta * lo, epsilon) || OutsidePocket(start + delta * hi, epsilon);
    }
    public GeometryBounds GetBounds() => bounds;
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}

// Exact B08 positioning pocket. It is not inflated as a damage shape.
internal sealed class RectangleComplementGeometry(Vector3 center, float halfWidth, float halfHeight, GeometryBounds bounds) : IGeometryShape
{
    public float SafeHalfWidth { get; } = halfWidth;
    public float SafeHalfHeight { get; } = halfHeight;
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position { get; } = center;
    public float Rotation => 0;
    public bool Contains(Vector3 point, float epsilon = 0.0001f)
        => bounds.Contains(point, epsilon) && OutsidePocket(point, epsilon);
    private bool OutsidePocket(Vector3 point, float epsilon)
    {
        return Math.Abs(point.X - Position.X) >= Math.Max(0, SafeHalfWidth - epsilon)
            || Math.Abs(point.Z - Position.Z) >= Math.Max(0, SafeHalfHeight - epsilon);
    }
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = 0.0001f)
    {
        var lo = 0f;
        var hi = 1f;
        var delta = end - start;
        bool Clip(float origin, float direction, float min, float max)
        {
            if (Math.Abs(direction) < 1e-8f) return origin >= min && origin <= max;
            var a = (min - origin) / direction;
            var b = (max - origin) / direction;
            lo = Math.Max(lo, Math.Min(a, b));
            hi = Math.Min(hi, Math.Max(a, b));
            return lo <= hi;
        }
        if (!Clip(start.X, delta.X, bounds.MinX - epsilon, bounds.MaxX + epsilon)
            || !Clip(start.Z, delta.Z, bounds.MinZ - epsilon, bounds.MaxZ + epsilon)) return false;
        // The rectangular pocket is convex: the clipped segment stays in its hole iff both endpoints do.
        return OutsidePocket(start + delta * lo, epsilon) || OutsidePocket(start + delta * hi, epsilon);
    }
    public GeometryBounds GetBounds() => bounds;
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64) => [];
}

// Snapshot arbitrary mechanic constraints on the main thread. PR workers never capture actors or components.
internal sealed class FrozenConstraintGeometry : IGeometryShape
{
    private readonly bool[] _inside;
    private readonly int _size;
    private readonly float _step;
    private readonly Vector3 _min;
    private readonly bool _visible;
    private IReadOnlyList<IReadOnlyList<Vector3>>? _contours;
    private IReadOnlyList<(Vector3 Start, Vector3 End)>? _boundarySegments;
    public int ContentHash { get; }
    public GeometryKind Kind => GeometryKind.WorldPolygon;
    public Vector3 Position { get; }
    public float Rotation => 0;
    private FrozenConstraintGeometry(bool[] inside, int size, float step, Vector3 position, bool visible)
    {
        _inside = inside; _size = size; _step = step; _visible = visible;
        var hash = new HashCode();
        foreach (var cell in inside) hash.Add(cell);
        ContentHash = hash.ToHashCode();
        Position = position;
        _min = Position - new Vector3(size * step / 2, 0, size * step / 2);
    }
    /// <summary>斗兽 FrozenConstraintFactory 算好格子后调用。</summary>
    internal static FrozenConstraintGeometry FromCells(bool[] cells, int size, float step, Vector3 position, bool visible)
        => new(cells, size, step, position, visible);
    public bool Contains(Vector3 point, float epsilon = 0.0001f)
    {
        var x = (int)MathF.Floor((point.X - _min.X) / _step);
        var z = (int)MathF.Floor((point.Z - _min.Z) / _step);
        return x >= 0 && z >= 0 && x < _size && z < _size && _inside[z * _size + x];
    }
    public bool IntersectsSegment(Vector3 start, Vector3 end, float epsilon = 0.0001f)
    {
        var count = Math.Max(1, (int)Math.Ceiling(Vector3.Distance(start, end) / (_step / 3)));
        for (var i = 0; i <= count; ++i) if (Contains(Vector3.Lerp(start, end, (float)i / count))) return true;
        return false;
    }
    public GeometryBounds GetBounds() => new(_min.X, _min.Z, _min.X + _size * _step, _min.Z + _size * _step);
    public IReadOnlyList<(Vector3 Start, Vector3 End)> GetBoundarySegments() => _boundarySegments ??= BuildBoundarySegments().ToArray();
    private IEnumerable<(Vector3 Start, Vector3 End)> BuildBoundarySegments()
    {
        bool Filled(int x, int z) => x >= 0 && z >= 0 && x < _size && z < _size && _inside[z * _size + x];
        for (var z = 0; z < _size; ++z)
            for (var x = 0; x < _size; ++x)
            {
                if (!Filled(x, z)) continue;
                var a = _min + new Vector3(x * _step, 0, z * _step);
                var b = a + new Vector3(_step, 0, 0);
                var c = a + new Vector3(_step, 0, _step);
                var d = a + new Vector3(0, 0, _step);
                if (!Filled(x, z - 1)) yield return (a, b);
                if (!Filled(x + 1, z)) yield return (b, c);
                if (!Filled(x, z + 1)) yield return (c, d);
                if (!Filled(x - 1, z)) yield return (d, a);
            }
    }
    public IReadOnlyList<IReadOnlyList<Vector3>> GetContours(int segments = 64)
    {
        if (!_visible) return [];
        if (_contours != null) return _contours;
        // Merge each horizontal run for display; Contains retains the full non-convex footprint.
        var rows = new List<IReadOnlyList<Vector3>>();
        for (var z = 0; z < _size; ++z)
            for (var x = 0; x < _size; ++x)
                if (_inside[z * _size + x])
                {
                    var begin = x;
                    while (x + 1 < _size && _inside[z * _size + x + 1]) ++x;
                    var a = _min + new Vector3(begin * _step, 0, z * _step);
                    var b = _min + new Vector3((x + 1) * _step, 0, (z + 1) * _step);
                    rows.Add([a, new(b.X, a.Y, a.Z), b, new(a.X, a.Y, b.Z)]);
                }
        return _contours = rows;
    }
}
