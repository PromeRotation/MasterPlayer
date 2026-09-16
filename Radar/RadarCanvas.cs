using System.Numerics;
using Dalamud.Bindings.ImGui;
using PromeRotation.Spatial.Geometry;

namespace MasterPlayer.Radar;

/// <summary>
/// 雷达画布：以屏幕圆盘为裁剪区域封装 ImDrawList 绘制。
/// 填充多边形经 Sutherland–Hodgman 凸域裁剪（64 边形逼近圆盘），
/// 线段经凸域参数裁剪，所有内容都被限制在圆盘内。
/// </summary>
internal sealed class RadarCanvas
{
    private const int ClipSegments = 64;

    private readonly ImDrawListPtr _drawList;
    private readonly Vector2 _center;
    private readonly float _radius;
    private readonly Vector2[] _clipPolygon;
    private readonly float _clipWinding;
    private readonly List<Vector2> _clipA = new(24);
    private readonly List<Vector2> _clipB = new(24);
    private readonly List<Vector3> _planePoints = new(24);

    public RadarCanvas(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        _drawList = drawList;
        _center = center;
        _radius = radius;
        _clipPolygon = new Vector2[ClipSegments];
        for (var i = 0; i < ClipSegments; i++)
        {
            var angle = i * (MathF.PI * 2f / ClipSegments);
            _clipPolygon[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }

        _clipWinding = SignedArea(_clipPolygon) >= 0f ? 1f : -1f;
    }

    public Vector2 Center => _center;
    public float Radius => _radius;

    public bool Contains(Vector2 point)
        => Vector2.DistanceSquared(point, _center) <= _radius * _radius;

    /// <summary>填充任意多边形（允许凹多边形），裁剪到圆盘后三角化填充。</summary>
    public void FillPolygon(IReadOnlyList<Vector2> polygon, uint color)
    {
        if (polygon.Count < 3)
        {
            return;
        }

        var clipped = ClipPolygon(polygon);
        if (clipped.Count < 3)
        {
            return;
        }

        _planePoints.Clear();
        foreach (var point in clipped)
        {
            _planePoints.Add(new Vector3(point.X, 0f, point.Y));
        }

        if (PolygonTriangulator.TryTriangulate(_planePoints, out var indices))
        {
            var flags = _drawList.Flags;
            _drawList.Flags &= ~ImDrawListFlags.AntiAliasedFill;
            for (var i = 0; i + 2 < indices.Length; i += 3)
            {
                _drawList.AddTriangleFilled(
                    clipped[indices[i]],
                    clipped[indices[i + 1]],
                    clipped[indices[i + 2]],
                    color);
            }

            _drawList.Flags = flags;
            return;
        }

        _drawList.PathClear();
        foreach (var point in clipped)
        {
            _drawList.PathLineTo(point);
        }

        _drawList.PathFillConvex(color);
        _drawList.PathClear();
    }

    /// <summary>描边一条线段，裁剪到圆盘。</summary>
    public void StrokeSegment(Vector2 a, Vector2 b, uint color, float thickness)
    {
        if (ClipSegment(a, b, out var p0, out var p1))
        {
            _drawList.AddLine(p0, p1, color, thickness);
        }
    }

    /// <summary>描边闭合轮廓，逐段裁剪到圆盘。</summary>
    public void StrokeContour(IReadOnlyList<Vector2> contour, uint color, float thickness)
    {
        if (contour.Count < 2)
        {
            return;
        }

        for (var i = 0; i < contour.Count; i++)
        {
            StrokeSegment(contour[i], contour[(i + 1) % contour.Count], color, thickness);
        }
    }

    /// <summary>带辉光的轮廓描边：先粗而淡的一层，再细而实的一层。</summary>
    public void StrokeContourGlow(IReadOnlyList<Vector2> contour, Vector4 color, float thickness)
    {
        var glow = color;
        glow.W *= 0.22f;
        StrokeContour(contour, ImGui.GetColorU32(glow), thickness * 3f);
        StrokeContour(contour, ImGui.GetColorU32(color), thickness);
    }

    public void FillCircle(Vector2 center, float radius, uint color, int segments = 0)
        => _drawList.AddCircleFilled(center, radius, color, segments);

    public void StrokeCircle(Vector2 center, float radius, uint color, float thickness, int segments = 0)
        => _drawList.AddCircle(center, radius, color, segments, thickness);

    private List<Vector2> ClipPolygon(IReadOnlyList<Vector2> input)
    {
        _clipA.Clear();
        _clipA.AddRange(input);
        var source = _clipA;
        var target = _clipB;

        for (var edge = 0; edge < _clipPolygon.Length && source.Count > 0; edge++)
        {
            var e0 = _clipPolygon[edge];
            var e1 = _clipPolygon[(edge + 1) % _clipPolygon.Length];
            var edgeDir = e1 - e0;

            target.Clear();
            for (var i = 0; i < source.Count; i++)
            {
                var current = source[i];
                var next = source[(i + 1) % source.Count];
                var currentValue = _clipWinding * Cross(edgeDir, current - e0);
                var nextValue = _clipWinding * Cross(edgeDir, next - e0);
                var currentInside = currentValue >= 0f;
                var nextInside = nextValue >= 0f;

                if (nextInside)
                {
                    if (!currentInside)
                    {
                        var t = currentValue / (currentValue - nextValue);
                        target.Add(current + (next - current) * t);
                    }

                    target.Add(next);
                }
                else if (currentInside)
                {
                    var t = currentValue / (currentValue - nextValue);
                    target.Add(current + (next - current) * t);
                }
            }

            (source, target) = (target, source);
        }

        return source;
    }

    private bool ClipSegment(Vector2 a, Vector2 b, out Vector2 p0, out Vector2 p1)
    {
        var t0 = 0f;
        var t1 = 1f;
        var delta = b - a;

        for (var edge = 0; edge < _clipPolygon.Length; edge++)
        {
            var e0 = _clipPolygon[edge];
            var e1 = _clipPolygon[(edge + 1) % _clipPolygon.Length];
            var edgeDir = e1 - e0;
            var valueA = _clipWinding * Cross(edgeDir, a - e0);
            var valueB = _clipWinding * Cross(edgeDir, b - e0);

            if (valueA < 0f && valueB < 0f)
            {
                p0 = p1 = default;
                return false;
            }

            if (valueA < 0f || valueB < 0f)
            {
                var t = valueA / (valueA - valueB);
                if (valueA < 0f)
                {
                    t0 = MathF.Max(t0, t);
                }
                else
                {
                    t1 = MathF.Min(t1, t);
                }

                if (t0 > t1)
                {
                    p0 = p1 = default;
                    return false;
                }
            }
        }

        p0 = a + delta * t0;
        p1 = a + delta * t1;
        return true;
    }

    private static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

    private static float SignedArea(IReadOnlyList<Vector2> polygon)
    {
        var area = 0f;
        for (var i = 0; i < polygon.Count; i++)
        {
            area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        }

        return area * 0.5f;
    }
}
