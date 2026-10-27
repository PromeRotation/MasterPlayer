using System.Numerics;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Crucible;

internal readonly record struct WDir(float X, float Z)
{
    public float LengthSq() => X * X + Z * Z;
    public float Length() => MathF.Sqrt(LengthSq());
    public WDir Normalized() => Length() is > 0.0001f and var n ? this / n : default;
    public WDir OrthoL() => new(Z, -X);
    public WDir OrthoR() => new(-Z, X);
    public float Dot(WDir v) => X * v.X + Z * v.Z;
    public float Cross(WDir v) => X * v.Z - Z * v.X;
    public static WDir operator +(WDir a, WDir b) => new(a.X + b.X, a.Z + b.Z);
    public static WDir operator -(WDir a, WDir b) => new(a.X - b.X, a.Z - b.Z);
    public static WDir operator -(WDir a) => new(-a.X, -a.Z);
    public static WDir operator *(WDir a, float f) => new(a.X * f, a.Z * f);
    public static WDir operator *(float f, WDir a) => a * f;
    public static WDir operator /(WDir a, float f) => new(a.X / f, a.Z / f);
}

internal readonly record struct WPos(float X, float Z)
{
    public static WPos operator +(WPos a, WDir b) => new(a.X + b.X, a.Z + b.Z);
    public static WPos operator -(WPos a, WDir b) => new(a.X - b.X, a.Z - b.Z);
    public static WDir operator -(WPos a, WPos b) => new(a.X - b.X, a.Z - b.Z);
    public bool AlmostEqual(WPos p, float epsilon) => (this - p).LengthSq() <= epsilon * epsilon;
    public bool InCircle(WPos p, float r) => AlmostEqual(p, r);
    public bool InRect(WPos p, float x, float z) => MathF.Abs(X - p.X) <= x && MathF.Abs(Z - p.Z) <= z;
    public bool InRect(WPos p, Angle r, float front, float back, float halfWidth) => InRect(p, r.ToDirection(), front, back, halfWidth);
    public bool InRect(WPos p, WDir dir, float front, float back, float halfWidth)
    {
        var d = this - p;
        return d.Dot(dir) <= front && d.Dot(dir) >= -back && MathF.Abs(d.Cross(dir)) <= halfWidth;
    }
    public Vector3 ToVec3(float y = 0) => new(X, y, Z);
    public static WPos FromVec3(Vector3 p) => new(p.X, p.Z);
}

internal readonly record struct Angle(float Rad)
{
    public float Deg => Rad * 180 / MathF.PI;
    public Angle Normalized() => new(MathF.IEEERemainder(Rad, MathF.Tau));
    public Angle Abs() => new(Math.Abs(Rad));
    public WDir ToDirection() => new(MathF.Sin(Rad), MathF.Cos(Rad));
    public static Angle FromDirection(WDir d) => new(MathF.Atan2(d.X, d.Z));
    public static Angle operator +(Angle a, Angle b) => new(a.Rad + b.Rad);
    public static Angle operator -(Angle a, Angle b) => new(a.Rad - b.Rad);
    public static Angle operator -(Angle a) => new(-a.Rad);
    public static Angle operator *(Angle a, float f) => new(a.Rad * f);
    public static Angle operator *(float f, Angle a) => a * f;
}

internal static class AngleExtensions
{
    public static Angle Degrees(this float f) => new(f * MathF.PI / 180);
    public static Angle Degrees(this int f) => ((float)f).Degrees();
}

internal abstract class ShapeDistance
{
    public abstract float Distance(in WPos p);
    public virtual IGeometryShape? Geometry(float y) => null;
    // 走 FrozenConstraintGeometry 兜底的内容键（参数量化）；直接几何或不可缓存返回 null。
    public virtual string? ContentKey() => null;
}

internal sealed class SDCircle(WPos origin, float radius) : ShapeDistance
{
    public WPos Origin => origin;
    public float Radius => radius;
    public override float Distance(in WPos p) => (p - origin).Length() - radius;
    public override IGeometryShape Geometry(float y) => new CircleGeometry(origin.ToVec3(y), radius);
    public override string? ContentKey() => $"C|{origin.X:F2},{origin.Z:F2}|{radius:F2}";
}
internal sealed class SDInvertedCircle(WPos origin, float radius) : ShapeDistance
{
    public WPos Origin => origin;
    public float Radius => radius;
    public override float Distance(in WPos p) => radius - (p - origin).Length();
    public override string? ContentKey() => $"IC|{origin.X:F2},{origin.Z:F2}|{radius:F2}";
}
internal class SDRect(WPos origin, WDir dir, float front, float back, float halfWidth) : ShapeDistance
{
    public override float Distance(in WPos p)
    {
        var d = p - origin;
        var z = d.Dot(dir);
        var qx = MathF.Abs(d.Cross(dir)) - halfWidth;
        var qz = MathF.Abs(z - (front - back) / 2) - (front + back) / 2;
        return MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qz, 0) * MathF.Max(qz, 0)) + MathF.Min(MathF.Max(qx, qz), 0);
    }
    public override IGeometryShape Geometry(float y) => new RectangleGeometry((origin + dir * ((front - back) / 2)).ToVec3(y), Angle.FromDirection(dir).Rad, halfWidth * 2, front + back);
    public override string? ContentKey()
        => $"R|{origin.X:F2},{origin.Z:F2}|{dir.X:F3},{dir.Z:F3}|{front:F2},{back:F2},{halfWidth:F2}";
}
internal sealed class SDInvertedRect(WPos origin, WDir dir, float front, float back, float halfWidth) : ShapeDistance
{
    public SDInvertedRect(WPos origin, Angle rotation, float front, float back, float halfWidth) : this(origin, rotation.ToDirection(), front, back, halfWidth) { }
    private readonly SDRect _rect = new(origin, dir, front, back, halfWidth);
    public override float Distance(in WPos p) => -_rect.Distance(p);
    public override string? ContentKey()
        => $"IR|{origin.X:F2},{origin.Z:F2}|{dir.X:F3},{dir.Z:F3}|{front:F2},{back:F2},{halfWidth:F2}";
}
internal sealed class SDCapsule(WPos start, WDir direction, float length, float radius) : ShapeDistance
{
    public SDCapsule(WPos start, Angle rotation, float length, float radius) : this(start, rotation.ToDirection(), length, radius) { }
    public override float Distance(in WPos p)
    {
        var d = p - start;
        return (d - direction * Math.Clamp(d.Dot(direction), 0, length)).Length() - radius;
    }
    public override IGeometryShape? Geometry(float y)
    {
        // 两端半圆 + 切线的解析多边形，避免走 168×168 约束网格兜底（移动区每帧重建的主线程热点）
        if (radius <= 0f || length < 0f || direction.LengthSq() <= 1e-12f)
            return null;
        var startV = start.ToVec3(y);
        var endV = (start + direction * length).ToVec3(y);
        var normal = direction.OrthoL();
        var segments = Math.Clamp((int)MathF.Ceiling(radius * 4f), 4, 12);
        var points = new List<Vector3>(2 * segments + 2);
        for (var i = 0; i <= segments; ++i)
        {
            var t = (float)i / segments * MathF.PI;
            var off = normal * MathF.Cos(t) + direction * MathF.Sin(t);
            points.Add(new Vector3(endV.X + off.X * radius, y, endV.Z + off.Z * radius));
        }
        for (var i = 0; i <= segments; ++i)
        {
            var t = (float)i / segments * MathF.PI;
            var off = -normal * MathF.Cos(t) - direction * MathF.Sin(t);
            points.Add(new Vector3(startV.X + off.X * radius, y, startV.Z + off.Z * radius));
        }
        return new WorldPolygonGeometry(points);
    }
}
internal sealed class SDIntersection(params ShapeDistance[] shapes) : ShapeDistance
{
    public override float Distance(in WPos p)
    {
        var result = float.MinValue;
        foreach (var shape in shapes) result = Math.Max(result, shape.Distance(p));
        return result;
    }
    public override string? ContentKey()
    {
        var sb = new System.Text.StringBuilder("IX");
        foreach (var shape in shapes)
        {
            var key = shape.ContentKey();
            if (key == null)
                return null;
            sb.Append('|').Append(key);
        }
        return sb.ToString();
    }
}

internal sealed class SDKnockbackInAABBRectAwayFromOrigin(WPos center, WPos origin, float distance, float halfWidth, float halfHeight) : ShapeDistance
{
    public override float Distance(in WPos p)
    {
        var landing = p + (p - origin).Normalized() * distance;
        return Math.Min(halfWidth - Math.Abs(landing.X - center.X), halfHeight - Math.Abs(landing.Z - center.Z));
    }
    public override string? ContentKey()
        => $"KB|{center.X:F2},{center.Z:F2}|{origin.X:F2},{origin.Z:F2}|{distance:F2},{halfWidth:F2},{halfHeight:F2}";
}

internal abstract record AOEShape
{
    public abstract ShapeDistance Distance(WPos origin, Angle rotation);
    public bool Check(WPos point, WPos origin, Angle rotation = default) => Distance(origin, rotation).Distance(point) <= 0;
    public abstract IGeometryShape Geometry(WPos origin, Angle rotation, float y);
    public void Outline(MiniArena arena, WPos origin, Angle rotation, uint color) => arena.Shape(this, origin, rotation, color);
    public void Draw(MiniArena arena, WPos origin, Angle rotation = default, uint color = Colors.AOE) => arena.Shape(this, origin, rotation, color);
}
internal sealed record AOEShapeCircle(float Radius) : AOEShape
{
    public override ShapeDistance Distance(WPos origin, Angle rotation) => new SDCircle(origin, Radius);
    public override IGeometryShape Geometry(WPos origin, Angle rotation, float y) => new CircleGeometry(origin.ToVec3(y), Radius);
}
internal sealed record AOEShapeDonut(float InnerRadius, float OuterRadius) : AOEShape
{
    private sealed class DistanceImpl(WPos origin, float inner, float outer) : ShapeDistance
    {
        public override float Distance(in WPos p) => MathF.Abs((p - origin).Length() - (inner + outer) / 2) - (outer - inner) / 2;
        public override IGeometryShape Geometry(float y) => new DonutGeometry(origin.ToVec3(y), inner, outer);
    }
    public override ShapeDistance Distance(WPos origin, Angle rotation) => new DistanceImpl(origin, InnerRadius, OuterRadius);
    public override IGeometryShape Geometry(WPos origin, Angle rotation, float y) => new DonutGeometry(origin.ToVec3(y), InnerRadius, OuterRadius);
}
internal sealed record AOEShapeRect(float LengthFront, float HalfWidth, float LengthBack = 0) : AOEShape
{
    public override ShapeDistance Distance(WPos origin, Angle rotation) => new SDRect(origin, rotation.ToDirection(), LengthFront, LengthBack, HalfWidth);
    public override IGeometryShape Geometry(WPos origin, Angle rotation, float y) => Distance(origin, rotation).Geometry(y)!;
}
internal sealed record AOEShapeCone(float Radius, Angle HalfAngle) : AOEShape
{
    private sealed class DistanceImpl(WPos origin, Angle rotation, float radius, Angle half) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = p - origin;
            var a = MathF.Abs((Angle.FromDirection(offset) - rotation).Normalized().Rad);
            return MathF.Max(offset.Length() - radius, MathF.Sin(Math.Clamp(a - half.Rad, -MathF.PI / 2, MathF.PI / 2)) * offset.Length());
        }
        public override IGeometryShape Geometry(float y) => new FanGeometry(origin.ToVec3(y), rotation.Rad, radius, half.Rad * 2);
    }
    public override ShapeDistance Distance(WPos origin, Angle rotation) => new DistanceImpl(origin, rotation, Radius, HalfAngle);
    public override IGeometryShape Geometry(WPos origin, Angle rotation, float y) => new FanGeometry(origin.ToVec3(y), rotation.Rad, Radius, HalfAngle.Rad * 2);
}
internal sealed record AOEShapeCross(float Length, float HalfWidth) : AOEShape
{
    private sealed class DistanceImpl(AOEShapeCross shape, WPos origin, Angle rotation) : ShapeDistance
    {
        private readonly SDRect _a = new(origin, rotation.ToDirection(), shape.Length, shape.Length, shape.HalfWidth);
        private readonly SDRect _b = new(origin, rotation.ToDirection().OrthoL(), shape.Length, shape.Length, shape.HalfWidth);
        public override float Distance(in WPos p) => Math.Min(_a.Distance(p), _b.Distance(p));
        public override IGeometryShape Geometry(float y) => shape.Geometry(origin, rotation, y);
    }
    public override ShapeDistance Distance(WPos origin, Angle rotation) => new DistanceImpl(this, origin, rotation);
    public override IGeometryShape Geometry(WPos origin, Angle rotation, float y)
    {
        var l = Length; var w = HalfWidth;
        Vector2[] points = [new(-w,-l),new(w,-l),new(w,-w),new(l,-w),new(l,w),new(w,w),new(w,l),new(-w,l),new(-w,w),new(-l,w),new(-l,-w),new(-w,-w)];
        return new LocalPolygonGeometry(origin.ToVec3(y), rotation.Rad, points);
    }
}

internal abstract class ArenaBounds
{
    public abstract bool Contains(WDir offset);
    public abstract IGeometryShape Geometry(WPos center, float y);
}
internal sealed class ArenaBoundsCircle(float radius) : ArenaBounds
{
    public float Radius => radius;
    public override bool Contains(WDir offset) => offset.LengthSq() <= radius * radius;
    public override IGeometryShape Geometry(WPos center, float y) => new CircleGeometry(center.ToVec3(y), radius);
}
internal class ArenaBoundsRect(float halfWidth, float halfHeight) : ArenaBounds
{
    public override bool Contains(WDir offset) => Math.Abs(offset.X) <= halfWidth && Math.Abs(offset.Z) <= halfHeight;
    public override IGeometryShape Geometry(WPos center, float y) => new RectangleGeometry(center.ToVec3(y), 0, halfWidth * 2, halfHeight * 2);
}
internal sealed class ArenaBoundsSquare(float halfWidth, float mapResolution = 0.5f) : ArenaBoundsRect(halfWidth, halfWidth)
{
    public float MapResolution => mapResolution;
}
internal abstract record Shape;
internal sealed record Rectangle(WPos Center, float HalfWidth, float HalfHeight) : Shape;
internal sealed class ArenaBoundsCustom(Shape[] shapes, bool AdjustForHitboxInwards = false, WPos CenterOverride = default) : ArenaBounds
{
    private readonly WPos[] _outline = Outline(shapes, AdjustForHitboxInwards ? 0.5f : 0);
    private readonly IGeometryShape _geometry = new WorldPolygonGeometry(Outline(shapes, AdjustForHitboxInwards ? 0.5f : 0).Select(p => p.ToVec3()));
    public override bool Contains(WDir offset) => _geometry.Contains((CenterOverride + offset).ToVec3());
    public override IGeometryShape Geometry(WPos center, float y) => new WorldPolygonGeometry(_outline.Select(p => (p + (center - CenterOverride)).ToVec3(y)));
    private static WPos[] Outline(Shape[] shapes, float margin)
    {
        var rectangles = shapes.Cast<Rectangle>().ToArray();
        var xs = rectangles.SelectMany(r => new[] { r.Center.X - r.HalfWidth, r.Center.X + r.HalfWidth }).Distinct().Order().ToArray();
        var zs = rectangles.SelectMany(r => new[] { r.Center.Z - r.HalfHeight, r.Center.Z + r.HalfHeight }).Distinct().Order().ToArray();
        var edges = new Dictionary<WPos, WPos>();
        bool Filled(int x, int z) => x >= 0 && z >= 0 && x < xs.Length - 1 && z < zs.Length - 1
            && rectangles.Any(r => new WPos((xs[x] + xs[x + 1]) / 2, (zs[z] + zs[z + 1]) / 2).InRect(r.Center, r.HalfWidth, r.HalfHeight));
        for (var z = 0; z < zs.Length - 1; ++z)
            for (var x = 0; x < xs.Length - 1; ++x)
            {
                if (!Filled(x, z)) continue;
                var a = new WPos(xs[x], zs[z]); var b = new WPos(xs[x + 1], zs[z]);
                var c = new WPos(xs[x + 1], zs[z + 1]); var d = new WPos(xs[x], zs[z + 1]);
                if (!Filled(x, z - 1)) edges.Add(a, b);
                if (!Filled(x + 1, z)) edges.Add(b, c);
                if (!Filled(x, z + 1)) edges.Add(c, d);
                if (!Filled(x - 1, z)) edges.Add(d, a);
            }
        var points = new List<WPos>();
        var first = edges.Keys.First(); var next = first;
        do { points.Add(next); next = edges[next]; } while (next != first && points.Count <= edges.Count);
        if (points.Count != edges.Count) throw new InvalidOperationException("Crucible floor must be one connected polygon without holes.");
        var corners = points.Where((p, i) => Math.Abs((p - points[(i + points.Count - 1) % points.Count]).Cross(points[(i + 1) % points.Count] - p)) > 0.001f).ToArray();
        return corners.Select((p, i) => p + margin * ((p - corners[(i + corners.Length - 1) % corners.Length]).Normalized().OrthoR()
            + (corners[(i + 1) % corners.Length] - p).Normalized().OrthoR())).ToArray();
    }
}

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
    private FrozenConstraintGeometry(bool[] inside, int size, float step, WPos center, float y, bool visible)
    {
        _inside = inside; _size = size; _step = step; _visible = visible;
        var hash = new HashCode();
        foreach (var cell in inside) hash.Add(cell);
        ContentHash = hash.ToHashCode();
        Position = center.ToVec3(y);
        _min = Position - new Vector3(size * step / 2, 0, size * step / 2);
    }
    public static FrozenConstraintGeometry Create(Func<WPos, float> distance, WPos center, float y, float radius = 42, float step = 0.5f, bool visible = false, bool safeRegion = false)
    {
        var size = (int)Math.Ceiling(radius * 2 / step);
        var cells = new bool[size * size];
        for (var z = 0; z < size; ++z)
            for (var x = 0; x < size; ++x)
            {
                var p = new WPos(center.X + (x + 0.5f - size / 2f) * step, center.Z + (z + 0.5f - size / 2f) * step);
                var inside = distance(p) <= 0;
                for (var dz = -1; dz <= 1; dz += 2)
                    for (var dx = -1; dx <= 1; dx += 2)
                        if (safeRegion) inside &= distance(p + new WDir(dx, dz) * (step / 2)) <= 0;
                        else inside |= distance(p + new WDir(dx, dz) * (step / 2)) <= 0;
                cells[z * size + x] = inside;
            }
        return new(cells, size, step, center, y, visible);
    }
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
