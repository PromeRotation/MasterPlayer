using System.Numerics;
using System.Reflection;
using System.Text;
using BossMod;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.BmrBridge;

internal enum BmrNavZoneKind
{
    Direct,
    Split,
    Sampled,
    ComplementSkipped,
    Failed,
}

internal sealed record BmrNavPrimitive(GeometryTemplate Template, Vector3 Position, float Rotation, string Signature);

internal static class BmrHintsConvert
{
    private const float SampleStep = 0.5f;
    private const float MaxSampleRadius = 60f;
    private const float ComplementFillRatio = 0.65f;
    private const int MaxSampleComponents = 4;

    public static (float DelaySeconds, bool Persistent) MapActivation(DateTime activation, DateTime now)
    {
        if (activation == default || activation <= now)
            return (0f, true);
        return ((float)(activation - now).TotalSeconds, false);
    }

    public static IReadOnlyList<BmrNavPrimitive> ConvertForbiddenZone(ShapeDistance sd, float y,
        Vector3 sampleCenter, float sampleRadius, out BmrNavZoneKind kind)
    {
        List<BmrNavPrimitive>? direct;
        try
        {
            direct = TryDirect(sd, y);
        }
        catch
        {
            direct = null;
        }
        if (direct != null)
        {
            kind = direct.Count == 1 ? BmrNavZoneKind.Direct : BmrNavZoneKind.Split;
            return direct;
        }
        if (IsComplement(sd))
        {
            kind = BmrNavZoneKind.ComplementSkipped;
            return [];
        }
        var sampled = Sample(sd, y, sampleCenter, sampleRadius);
        kind = sampled.Count != 0 ? BmrNavZoneKind.Sampled : BmrNavZoneKind.Failed;
        return sampled;
    }

    public static IGeometryShape? ConvertBounds(ArenaBounds bounds, WPos center, float y)
    {
        switch (bounds)
        {
            case ArenaBoundsCircle circle:
                return new CircleGeometry(center.ToVec3(y), circle.Radius);
            case ABRect rect:
                return new RectangleGeometry(center.ToVec3(y), rect.Rotation.Rad, 2f * rect.HalfWidth, 2f * rect.HalfHeight);
            case ArenaBoundsCustom custom:
            {
                WorldPolygonGeometry? best = null;
                foreach (var part in custom.Polygon.Parts)
                {
                    var exteriorEnd = part.HoleStarts.Count > 0 ? part.HoleStarts[0] : part.Vertices.Count;
                    if (exteriorEnd < 3)
                        continue;
                    var points = new List<Vector3>(exteriorEnd);
                    for (var i = 0; i < exteriorEnd; ++i)
                        points.Add((custom.Center + part.Vertices[i]).ToVec3(y));
                    var polygon = TryPolygon(points);
                    if (polygon != null && (best == null || polygon.WorldPoints.Count > best.WorldPoints.Count))
                        best = polygon;
                }
                return best;
            }
            default:
            {
                // 未知边界类型：用 ShapeSimplified 外轮廓兜底
                WorldPolygonGeometry? best = null;
                foreach (var part in bounds.ShapeSimplified.Parts)
                {
                    var exteriorEnd = part.HoleStarts.Count > 0 ? part.HoleStarts[0] : part.Vertices.Count;
                    if (exteriorEnd < 3)
                        continue;
                    var points = new List<Vector3>(exteriorEnd);
                    for (var i = 0; i < exteriorEnd; ++i)
                        points.Add((center + part.Vertices[i]).ToVec3(y));
                    var polygon = TryPolygon(points);
                    if (polygon != null && (best == null || polygon.WorldPoints.Count > best.WorldPoints.Count))
                        best = polygon;
                }
                return best;
            }
        }
    }

    private static List<BmrNavPrimitive>? TryDirect(ShapeDistance sd, float y)
    {
        switch (sd)
        {
            case SDCircle circle:
            {
                var origin = new Vector3(F(circle, "originX"), y, F(circle, "originZ"));
                var radius = F(circle, "radius");
                return radius > 0f ? [Prim(new CircleTemplate(radius), origin, 0f)] : null;
            }
            case SDDonut donut:
            {
                var origin = new Vector3(F(donut, "originX"), y, F(donut, "originZ"));
                var inner = F(donut, "innerRadius");
                var outer = F(donut, "outerRadius");
                return outer > 0f && outer > inner ? [Prim(new DonutTemplate(inner, outer), origin, 0f)] : null;
            }
            case SDCone cone:
            {
                var origin = new Vector3(F(cone, "originX"), y, F(cone, "originZ"));
                var radius = F(cone, "radius");
                if (!RecoverWedge(F(cone, "coneFactor"), F(cone, "nlX"), F(cone, "nlZ"), F(cone, "nrX"), F(cone, "nrZ"),
                        out var centerDir, out var halfAngle) || radius <= 0f || halfAngle <= 0f)
                    return null;
                return [Prim(new FanTemplate(radius, MathF.Min(2f * halfAngle, 2f * MathF.PI)), origin, centerDir)];
            }
            case SDDonutSector sector:
            {
                var origin = new Vector3(F(sector, "originX"), y, F(sector, "originZ"));
                var inner = F(sector, "innerRadius");
                var outer = F(sector, "outerRadius");
                if (!RecoverWedge(F(sector, "coneFactor"), F(sector, "nlX"), F(sector, "nlZ"), F(sector, "nrX"), F(sector, "nrZ"),
                        out var centerDir, out var halfAngle) || outer <= 0f || outer <= inner || halfAngle <= 0f)
                    return null;
                var polygon = TryPolygon(RingFanPoints(origin, inner, outer, centerDir, halfAngle));
                return polygon != null ? [Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f)] : null;
            }
            case SDRect rect:
            {
                var dirX = F(rect, "dirX");
                var dirZ = F(rect, "dirZ");
                var dirLength = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
                var lenFront = F(rect, "lenFront");
                var lenBack = F(rect, "lenBack");
                var halfWidth = F(rect, "halfWidth");
                if (dirLength <= 1e-6f || lenFront + lenBack <= 0f || halfWidth <= 0f)
                    return null;
                dirX /= dirLength;
                dirZ /= dirLength;
                var rotation = MathF.Atan2(dirX, dirZ);
                var origin = new Vector3(F(rect, "originX") + dirX * (lenFront - lenBack) * 0.5f, y,
                    F(rect, "originZ") + dirZ * (lenFront - lenBack) * 0.5f);
                return [Prim(new RectangleTemplate(2f * halfWidth, lenFront + lenBack), origin, rotation)];
            }
            case SDCross cross:
            {
                var originV = Field<WPos>(cross, "origin");
                var direction = Field<WDir>(cross, "direction");
                var length = F(cross, "length");
                var halfWidth = F(cross, "halfWidth");
                if (length <= 0f || halfWidth <= 0f || direction.LengthSq() <= 1e-12f)
                    return null;
                var origin = new Vector3(originV.X, y, originV.Z);
                var rotation = direction.ToAngle().Rad;
                return
                [
                    Prim(new RectangleTemplate(2f * halfWidth, 2f * length), origin, rotation),
                    Prim(new RectangleTemplate(2f * halfWidth, 2f * length), origin, rotation + MathF.PI / 2f),
                ];
            }
            case SDTri tri:
            {
                var polygon = TryPolygon([
                    new Vector3(F(tri, "aX"), y, F(tri, "aZ")),
                    new Vector3(F(tri, "bX"), y, F(tri, "bZ")),
                    new Vector3(F(tri, "cX"), y, F(tri, "cZ")),
                ]);
                return polygon != null ? [Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f)] : null;
            }
            case SDConvexPolygon convex:
            {
                var edges = FieldRef<Array>(convex, "edges");
                if (edges == null || edges.Length < 3)
                    return null;
                var points = new List<Vector3>(edges.Length);
                foreach (var edge in edges)
                {
                    var (a, _) = (ValueTuple<WPos, WPos>)edge!;
                    points.Add(new Vector3(a.X, y, a.Z));
                }
                var polygon = TryPolygon(points);
                return polygon != null ? [Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f)] : null;
            }
            case SDCapsule capsule:
            {
                var originV = Field<WPos>(capsule, "origin");
                var direction = Field<WDir>(capsule, "direction");
                var length = F(capsule, "length");
                var radius = F(capsule, "radius");
                if (radius <= 0f || length < 0f || direction.LengthSq() <= 1e-12f)
                    return null;
                var polygon = TryPolygon(CapsulePoints(new Vector3(originV.X, y, originV.Z), direction.Normalized(), radius, length));
                return polygon != null ? [Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f)] : null;
            }
            case SDArcCapsule arc:
            {
                var orbit = new Vector3(F(arc, "ox"), y, F(arc, "oz"));
                var start = new Vector2(F(arc, "p0x") - orbit.X, F(arc, "p0z") - orbit.Z);
                var end = new Vector2(F(arc, "p1x") - orbit.X, F(arc, "p1z") - orbit.Z);
                var tube = F(arc, "tube");
                var radius = F(arc, "R");
                if (tube <= 0f || radius <= 1e-6f)
                    return null;
                var startAngle = MathF.Atan2(start.X, start.Y);
                var sweep = SignedSweep(start, end);
                if (sweep == 0f)
                    return null;
                var polygon = TryPolygon(ArcCapsulePoints(orbit, radius, startAngle, sweep, tube));
                return polygon != null ? [Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f)] : null;
            }
            case SDPolygonWithHoles withHoles:
            {
                var core = FieldRef<object>(withHoles, "_core");
                if (core == null)
                    return null;
                var polygon = FieldRef<RelSimplifiedComplexPolygon>(core, "_polygon");
                if (polygon == null)
                    return null;
                var origin = new WPos(F(core, "_originX"), F(core, "_originZ"));
                return ComplexPolygonPrims(polygon, origin, y);
            }
            case SDComplexPolygonInvertedContains complex:
            {
                // 名称含 Inverted 但语义为「多边形内禁止」——直接取外轮廓
                var polygon = FieldRef<RelSimplifiedComplexPolygon>(complex, "polygon");
                var center = Field<WPos>(complex, "center");
                return polygon != null ? ComplexPolygonPrims(polygon, center, y) : null;
            }
            case SDUnion union:
            {
                var zones = FieldRef<ShapeDistance[]>(union, "zones");
                if (zones == null || zones.Length == 0)
                    return null;
                var result = new List<BmrNavPrimitive>();
                foreach (var zone in zones)
                {
                    var inner = TryDirect(zone, y);
                    if (inner == null)
                        return null;
                    result.AddRange(inner);
                }
                return result;
            }
            default:
                return null;
        }
    }

    private static List<BmrNavPrimitive>? ComplexPolygonPrims(RelSimplifiedComplexPolygon polygon, WPos origin, float y)
    {
        var result = new List<BmrNavPrimitive>();
        foreach (var part in polygon.Parts)
        {
            var exteriorEnd = part.HoleStarts.Count > 0 ? part.HoleStarts[0] : part.Vertices.Count;
            if (exteriorEnd < 3)
                continue;
            var points = new List<Vector3>(exteriorEnd);
            for (var i = 0; i < exteriorEnd; ++i)
                points.Add((origin + part.Vertices[i]).ToVec3(y));
            var shape = TryPolygon(points);
            if (shape != null)
                result.Add(Prim(new WorldPolygonTemplate(shape.WorldPoints), shape.Position, 0f));
        }
        return result.Count != 0 ? result : null;
    }

    // SDCone/SDDonutSector/SDArcCapsule 的边法线均为 nl=coneFactor·OrthoL(dir+half)、nr=coneFactor·OrthoR(dir−half)；撤销即还原。
    private static bool RecoverWedge(float coneFactor, float nlX, float nlZ, float nrX, float nrZ,
        out float centerDir, out float halfAngle)
    {
        centerDir = 0f;
        halfAngle = 0f;
        if (coneFactor == 0f)
            return false;
        // u1 = cf·OrthoR(nl)（OrthoR 撤销 OrthoL）；u2 = cf·OrthoL(nr)
        var u1x = -coneFactor * nlZ;
        var u1z = coneFactor * nlX;
        var u2x = coneFactor * nrZ;
        var u2z = -coneFactor * nrX;
        var bisectX = u1x + u2x;
        var bisectZ = u1z + u2z;
        if (bisectX * bisectX + bisectZ * bisectZ <= 1e-12f)
            return false;
        if (coneFactor < 0f)
        {
            // 半角超过 90° 时分角线反向
            bisectX = -bisectX;
            bisectZ = -bisectZ;
        }
        centerDir = MathF.Atan2(bisectX, bisectZ);
        halfAngle = MathF.Abs(NormalizeAngle(MathF.Atan2(u1x, u1z) - centerDir));
        return halfAngle > 1e-4f && halfAngle <= MathF.PI;
    }

    private static bool IsComplement(ShapeDistance sd)
        => sd is SDInvertedCircle or SDInvertedDonut or SDInvertedCone or SDInvertedDonutSector or SDInvertedTri
            or SDInvertedRect or SDInvertedCapsule or SDInvertedCross or SDInvertedArcCapsule
            or SDInvertedUnion or SDInvertedUnionOffset or SDOutsideOfUnion or SDPrecisePosition;

    private static List<BmrNavPrimitive> Sample(ShapeDistance sd, float y, Vector3 center, float radius)
    {
        radius = Math.Clamp(radius, 5f, MaxSampleRadius);
        var half = (int)MathF.Ceiling(radius / SampleStep);
        var total = (2 * half + 1) * (2 * half + 1);
        if (total > 200 * 200)
        {
            half = 99;
            radius = half * SampleStep;
        }
        var width = 2 * half + 1;
        var filled = new bool[width * width];
        var filledCount = 0;
        var radiusSq = radius * radius;
        for (var gz = 0; gz < width; ++gz)
        {
            for (var gx = 0; gx < width; ++gx)
            {
                var dx = (gx - half) * SampleStep;
                var dz = (gz - half) * SampleStep;
                if (dx * dx + dz * dz > radiusSq)
                    continue;
                if (sd.Distance(new WPos(center.X + dx, center.Z + dz)) <= 0f)
                {
                    filled[gz * width + gx] = true;
                    ++filledCount;
                }
            }
        }
        if (filledCount == 0 || filledCount > ComplementFillRatio * MathF.PI * radiusSq / (SampleStep * SampleStep))
            return [];

        var visited = new bool[width * width];
        var components = new List<List<int>>();
        for (var i = 0; i < filled.Length; ++i)
        {
            if (!filled[i] || visited[i])
                continue;
            var component = new List<int>();
            var stack = new Stack<int>();
            stack.Push(i);
            visited[i] = true;
            while (stack.Count > 0)
            {
                var cell = stack.Pop();
                component.Add(cell);
                var cx = cell % width;
                var cz = cell / width;
                foreach (var next in new[] { cx > 0 ? cell - 1 : -1, cx + 1 < width ? cell + 1 : -1, cz > 0 ? cell - width : -1, cz + 1 < width ? cell + width : -1 })
                {
                    if (next >= 0 && filled[next] && !visited[next])
                    {
                        visited[next] = true;
                        stack.Push(next);
                    }
                }
            }
            components.Add(component);
        }
        components.Sort(static (a, b) => b.Count.CompareTo(a.Count));

        var result = new List<BmrNavPrimitive>();
        foreach (var component in components.Take(MaxSampleComponents))
        {
            if (component.Count < 2)
                continue;
            var outline = TraceOutline(filled, width, component);
            if (outline.Count < 3)
                continue;
            var points = outline.Select(corner => new Vector3(
                center.X + (corner.X - half - 0.5f) * SampleStep, y, center.Z + (corner.Z - half - 0.5f) * SampleStep)).ToList();
            var polygon = TryPolygon(points);
            if (polygon != null)
                result.Add(Prim(new WorldPolygonTemplate(polygon.WorldPoints), polygon.Position, 0f));
        }
        return result;
    }

    // 角点网格（0..width）上的边界边链接：每个填充格对空邻居产出有向边，行进方向使填充格保持在同一侧；
    // 对角捏合点按 cross=+1 优先拆环，一个组件可能有多个环（孔洞），取边数最多的环作为外轮廓。
    private static List<(int X, int Z)> TraceOutline(bool[] filled, int width, List<int> component)
    {
        var edgeFrom = new Dictionary<int, List<int>>();
        foreach (var cell in component)
        {
            var cx = cell % width;
            var cz = cell / width;
            if (cz == 0 || !filled[cell - width])
                AddEdge(edgeFrom, cx, cz, cx + 1, cz);
            if (cx + 1 == width || !filled[cell + 1])
                AddEdge(edgeFrom, cx + 1, cz, cx + 1, cz + 1);
            if (cz + 1 == width || !filled[cell + width])
                AddEdge(edgeFrom, cx + 1, cz + 1, cx, cz + 1);
            if (cx == 0 || !filled[cell - 1])
                AddEdge(edgeFrom, cx, cz + 1, cx, cz);
        }
        if (edgeFrom.Count == 0)
            return [];

        var loops = new List<List<(int X, int Z)>>();
        var visitedEdges = new HashSet<long>();
        foreach (var (from, tos) in edgeFrom)
        {
            foreach (var to in tos)
            {
                if (!visitedEdges.Add(PackEdge(from, to)))
                    continue;
                var loop = new List<(int X, int Z)> { Unpack(from) };
                var prev = from;
                var current = to;
                var guard = edgeFrom.Count + 16;
                while (current != from && guard-- > 0)
                {
                    loop.Add(Unpack(current));
                    var (prevX, prevZ) = Unpack(prev);
                    var (curX, curZ) = Unpack(current);
                    var dirX = Math.Sign(curX - prevX);
                    var dirZ = Math.Sign(curZ - prevZ);
                    var next = -1;
                    var nextCross = int.MinValue;
                    foreach (var candidate in edgeFrom[current])
                    {
                        if (visitedEdges.Contains(PackEdge(current, candidate)))
                            continue;
                        var (candX, candZ) = Unpack(candidate);
                        var candDirX = Math.Sign(candX - curX);
                        var candDirZ = Math.Sign(candZ - curZ);
                        var cross = dirX * candDirZ - dirZ * candDirX;
                        if (cross > nextCross)
                        {
                            nextCross = cross;
                            next = candidate;
                        }
                    }
                    if (next < 0)
                        break;
                    visitedEdges.Add(PackEdge(current, next));
                    prev = current;
                    current = next;
                }
                if (current == from && loop.Count >= 3)
                    loops.Add(loop);
            }
        }
        return loops.Count == 0 ? [] : loops.OrderByDescending(l => l.Count).First();
    }

    private static void AddEdge(Dictionary<int, List<int>> edges, int fromX, int fromZ, int toX, int toZ)
    {
        var from = fromX * 1000003 + fromZ;
        if (!edges.TryGetValue(from, out var list))
            edges[from] = list = [];
        list.Add(toX * 1000003 + toZ);
    }

    private static long PackEdge(int from, int to) => ((long)from << 32) | (uint)to;

    private static (int X, int Z) Unpack(int vertex) => (vertex / 1000003, vertex % 1000003);

    public static string SignatureOf(ShapeDistance sd)
    {
        var builder = new StringBuilder(256);
        BuildSignature(sd, builder, 0);
        return builder.ToString();
    }

    private static readonly Dictionary<Type, FieldInfo[]> FieldCache = [];
    private static readonly object FieldCacheGate = new();

    private static FieldInfo[] FieldsOf(Type type)
    {
        lock (FieldCacheGate)
        {
            if (!FieldCache.TryGetValue(type, out var fields))
            {
                fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                FieldCache[type] = fields;
            }
            return fields;
        }
    }

    private static void BuildSignature(object? value, StringBuilder sb, int depth)
    {
        if (value == null || depth > 4)
            return;
        var type = value.GetType();
        sb.Append(type.Name).Append('{');
        foreach (var field in FieldsOf(type))
        {
            var fieldValue = field.GetValue(value);
            switch (fieldValue)
            {
                case null:
                    break;
                case float f:
                    sb.Append(f.ToString("F2")).Append(';');
                    break;
                case double d:
                    sb.Append(d.ToString("F2")).Append(';');
                    break;
                case bool or byte or short or ushort or int or uint or long or ulong:
                    sb.Append(fieldValue).Append(';');
                    break;
                case WPos pos:
                    sb.Append(pos.X.ToString("F2")).Append(',').Append(pos.Z.ToString("F2")).Append(';');
                    break;
                case WDir dir:
                    sb.Append(dir.X.ToString("F2")).Append(',').Append(dir.Z.ToString("F2")).Append(';');
                    break;
                case Angle angle:
                    sb.Append(angle.Rad.ToString("F3")).Append(';');
                    break;
                case string text:
                    sb.Append(text).Append(';');
                    break;
                case Array array:
                    sb.Append('#').Append(array.Length).Append(';');
                    foreach (var element in array)
                        BuildSignature(element, sb, depth + 1);
                    break;
                case ShapeDistance nested:
                    BuildSignature(nested, sb, depth + 1);
                    break;
                case RelSimplifiedComplexPolygon polygon:
                    sb.Append('#').Append(polygon.Parts.Count).Append(';');
                    foreach (var part in polygon.Parts)
                        foreach (var vertex in part.Vertices)
                            sb.Append(vertex.X.ToString("F2")).Append(',').Append(vertex.Z.ToString("F2")).Append(';');
                    break;
                default:
                    if (!field.FieldType.IsPrimitive && fieldValue is not decimal)
                        BuildSignature(fieldValue, sb, depth + 1);
                    break;
            }
        }
        sb.Append('}');
    }

    private static float F(object obj, string name)
        => FieldsOf(obj.GetType()).First(f => f.Name == name).GetValue(obj) is float v ? v : 0f;

    private static T Field<T>(object obj, string name) where T : struct
        => FieldsOf(obj.GetType()).First(f => f.Name == name).GetValue(obj) is T v ? v : default;

    private static T? FieldRef<T>(object obj, string name) where T : class
        => FieldsOf(obj.GetType()).FirstOrDefault(f => f.Name == name)?.GetValue(obj) as T;

    private static BmrNavPrimitive Prim(GeometryTemplate template, Vector3 position, float rotation)
    {
        var sb = new StringBuilder(64);
        sb.Append(template.Kind).Append('|')
            .Append(position.X.ToString("F2")).Append(',').Append(position.Z.ToString("F2")).Append('|')
            .Append(rotation.ToString("F3")).Append('|');
        switch (template)
        {
            case CircleTemplate c:
                sb.Append(c.Radius.ToString("F2"));
                break;
            case DonutTemplate d:
                sb.Append(d.InnerRadius.ToString("F2")).Append(',').Append(d.OuterRadius.ToString("F2"));
                break;
            case FanTemplate f:
                sb.Append(f.Radius.ToString("F2")).Append(',').Append(f.AngleRadians.ToString("F3"));
                break;
            case RectangleTemplate r:
                sb.Append(r.Width.ToString("F2")).Append(',').Append(r.Length.ToString("F2"));
                break;
            case WorldPolygonTemplate p:
                sb.Append('#').Append(p.WorldPoints.Count).Append(';');
                foreach (var point in p.WorldPoints)
                    sb.Append(point.X.ToString("F2")).Append(',').Append(point.Z.ToString("F2")).Append(';');
                break;
        }
        return new(template, position, rotation, sb.ToString());
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 2f * MathF.PI;
        if (angle > MathF.PI) angle -= 2f * MathF.PI;
        if (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }

    private static float SignedSweep(Vector2 start, Vector2 end)
    {
        // Atan2(X,Z) 约定下 atan2(cross, dot) = -Δθ，取负还原 BMR 的角度增量方向
        var cross = start.X * end.Y - start.Y * end.X;
        var dot = start.X * end.X + start.Y * end.Y;
        return -MathF.Atan2(cross, dot);
    }

    private static List<Vector3> RingFanPoints(Vector3 center, float inner, float outer, float centerDir, float halfAngle)
    {
        var points = new List<Vector3>();
        var segments = ArcSegments(2f * halfAngle);
        for (var i = 0; i <= segments; ++i)
            points.Add(Polar(center, outer, centerDir - halfAngle + 2f * halfAngle * i / segments));
        for (var i = segments; i >= 0; --i)
            points.Add(Polar(center, inner, centerDir - halfAngle + 2f * halfAngle * i / segments));
        return points;
    }

    private static List<Vector3> CapsulePoints(Vector3 start, WDir direction, float radius, float length)
    {
        var points = new List<Vector3>();
        var startAngle = direction.ToAngle().Rad;
        var end = new Vector3(start.X + direction.X * length, start.Y, start.Z + direction.Z * length);
        AppendArc(points, end, radius, startAngle - MathF.PI / 2f, MathF.PI);
        AppendArc(points, start, radius, startAngle + MathF.PI / 2f, MathF.PI);
        return points;
    }

    private static List<Vector3> ArcCapsulePoints(Vector3 orbit, float radius, float startAngle, float sweep, float tube)
    {
        var segments = ArcSegments(MathF.Abs(sweep));
        var outer = new List<Vector3>(segments + 1);
        var inner = new List<Vector3>(segments + 1);
        for (var i = 0; i <= segments; ++i)
        {
            var angle = startAngle + sweep * i / segments;
            outer.Add(Polar(orbit, radius + tube, angle));
            inner.Add(Polar(orbit, MathF.Max(0f, radius - tube), angle));
        }
        var points = new List<Vector3>(outer);
        points.AddRange(CapBetween(outer[^1], inner[^1]));
        for (var i = segments; i >= 0; --i)
            points.Add(inner[i]);
        points.AddRange(CapBetween(inner[0], outer[0]));
        return points;
    }

    private static IEnumerable<Vector3> CapBetween(Vector3 from, Vector3 to)
    {
        var center = (from + to) * 0.5f;
        var outward = from - center;
        var radius = outward.Length();
        if (radius <= 1e-6f)
            yield break;
        var tangent = new Vector3(-outward.Z, 0f, outward.X) / radius;
        for (var i = 1; i < 4; ++i)
        {
            var t = i / 4f * MathF.PI;
            yield return center + outward * MathF.Cos(t) + tangent * (radius * MathF.Sin(t));
        }
    }

    private static void AppendArc(List<Vector3> points, Vector3 center, float radius, float startAngle, float sweep)
    {
        var segments = ArcSegments(MathF.Abs(sweep));
        for (var i = 0; i <= segments; ++i)
            points.Add(Polar(center, radius, startAngle + sweep * i / segments));
    }

    private static int ArcSegments(float radians)
        => Math.Clamp((int)MathF.Ceiling(radians * 24f / MathF.PI), 2, 64);

    private static Vector3 Polar(Vector3 center, float radius, float angleRad)
        => GeometryHelper.PointFromRotationDistanceXZ(center, angleRad, radius);

    private static WorldPolygonGeometry? TryPolygon(IReadOnlyList<Vector3> points)
    {
        if (points.Count < 3)
            return null;
        try
        {
            return new WorldPolygonGeometry(points);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record BmrNavZoneRequest(string Key, IReadOnlyList<BmrNavPrimitive> Primitives,
    float DelaySeconds, bool Persistent, string Signature);

internal sealed class BmrNavAreaDiffer
{
    private sealed class Entry
    {
        public string Signature = "";
        public List<SafeZoneAreaHandle> Handles = [];
    }

    private readonly Dictionary<string, Entry> _zones = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (SafeZoneAreaHandle Handle, string Signature)> _obstacles = new(StringComparer.Ordinal);

    public int ZoneCount => _zones.Count;
    public int ObstacleCount => _obstacles.Count;

    public void Sync(SafeZoneRegistry registry, SafeZoneManager manager,
        IReadOnlyList<BmrNavZoneRequest> dangers,
        IReadOnlyList<(string Key, BmrNavPrimitive Prim)> obstacles,
        float dangerMargin,
        Action<SafeZoneAreaHandle>? track)
    {
        var marginSuffix = dangerMargin.ToString("F2");
        var seenZones = new HashSet<string>(StringComparer.Ordinal);
        foreach (var request in dangers)
        {
            if (request.Primitives.Count == 0)
                continue;
            var signature = request.Signature + "|m" + marginSuffix;
            seenZones.Add(request.Key);
            if (_zones.TryGetValue(request.Key, out var entry))
            {
                if (entry.Signature == signature)
                    continue;
                if (entry.Handles.Count == request.Primitives.Count)
                {
                    var updated = true;
                    for (var i = 0; i < request.Primitives.Count; ++i)
                    {
                        var prim = request.Primitives[i];
                        updated &= manager.UpdateArea(entry.Handles[i],
                            Dodge.DangerMarginInflator.Inflate(prim.Template, dangerMargin),
                            new StaticTransform(prim.Position, prim.Rotation), incrementVersion: false);
                    }
                    if (updated)
                    {
                        entry.Signature = signature;
                        continue;
                    }
                }
                foreach (var handle in entry.Handles)
                    registry.Remove(handle);
                entry.Handles.Clear();
                RegisterZone(registry, request, dangerMargin, marginSuffix, entry, track);
                continue;
            }
            var created = new Entry();
            RegisterZone(registry, request, dangerMargin, marginSuffix, created, track);
            if (created.Handles.Count != 0)
                _zones[request.Key] = created;
        }
        foreach (var stale in _zones.Keys.Where(k => !seenZones.Contains(k)).ToArray())
        {
            foreach (var handle in _zones[stale].Handles)
                registry.Remove(handle);
            _zones.Remove(stale);
        }

        var seenObstacles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, prim) in obstacles)
        {
            seenObstacles.Add(key);
            if (_obstacles.TryGetValue(key, out var existing))
            {
                if (existing.Signature == prim.Signature)
                    continue;
                registry.Remove(existing.Handle);
                _obstacles.Remove(key);
            }
            var handle = registry.AddObstacle(prim.Template, new StaticTransform(prim.Position, prim.Rotation));
            if (handle.IsValid)
            {
                _obstacles[key] = (handle, prim.Signature);
                track?.Invoke(handle);
            }
        }
        foreach (var stale in _obstacles.Keys.Where(k => !seenObstacles.Contains(k)).ToArray())
        {
            registry.Remove(_obstacles[stale].Handle);
            _obstacles.Remove(stale);
        }
    }

    public void Clear(SafeZoneRegistry registry)
    {
        foreach (var entry in _zones.Values)
            foreach (var handle in entry.Handles)
                registry.Remove(handle);
        _zones.Clear();
        foreach (var obstacle in _obstacles.Values)
            registry.Remove(obstacle.Handle);
        _obstacles.Clear();
    }

    private static void RegisterZone(SafeZoneRegistry registry, BmrNavZoneRequest request,
        float dangerMargin, string marginSuffix, Entry entry, Action<SafeZoneAreaHandle>? track)
    {
        foreach (var prim in request.Primitives)
        {
            var template = Dodge.DangerMarginInflator.Inflate(prim.Template, dangerMargin);
            var binding = new StaticTransform(prim.Position, prim.Rotation);
            var handle = request.Persistent
                ? registry.AddPersistentDanger(template, binding)
                : registry.AddDelayedHit(template, binding, request.DelaySeconds, SafeZoneRegistry.DefaultHitDurationSeconds);
            if (handle.IsValid)
            {
                entry.Handles.Add(handle);
                track?.Invoke(handle);
            }
        }
        entry.Signature = request.Signature + "|m" + marginSuffix;
    }
}
