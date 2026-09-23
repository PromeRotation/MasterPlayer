using System.Numerics;
using BossMod;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.MechanicHost;

public enum MechanicRadarExtraKind
{
    Eye,
    Text,
    Sign,
}

public sealed record MechanicRadarExtra(MechanicRadarExtraKind Kind, Vector3 Position, string? Text, int IconId, bool Danger, uint Color);

internal sealed record MechanicPreviewShape(IGeometryShape Shape, bool Safe, bool OutlineOnly, float Progress);

internal sealed record MechanicPreviewLine(Vector3 Start, Vector3 End, bool Safe);

internal static class MechanicDrawConvert
{
    // ColorConfig 默认值（ABGR）；MP 不加载 BMR 配置文件，这些值即运行库实际生效值。
    private const uint ColorAoe = 0x80008080;
    private const uint ColorDanger = 0xff00ffff;
    private const uint ColorTrap = 0x80000080;
    private const uint ColorEnemy = 0xff0000ff;
    private const uint ColorVulnerable = 0xffff00ff;
    private const uint ColorSafeFromAoe = 0x80008000;
    private const uint ColorSafe = 0xff00ff00;
    private const uint ColorBorder = 0xffffffff;

    public static bool IsSafeColor(uint color) => color is ColorSafeFromAoe or ColorSafe;

    private static bool IsDangerColor(uint color)
        => color is ColorAoe or ColorDanger or ColorTrap or ColorEnemy or ColorVulnerable;

    public static int Convert(IReadOnlyList<MechanicDrawCommand> commands, float fallbackY,
        List<MechanicPreviewShape> shapes, List<MechanicPreviewLine> lines, List<MechanicRadarExtra> extras)
    {
        var skipped = 0;
        foreach (var command in commands)
        {
            try
            {
                if (!ConvertCommand(command, fallbackY, shapes, lines, extras))
                    ++skipped;
            }
            catch
            {
                ++skipped;
            }
        }
        return skipped;
    }

    private static bool ConvertCommand(MechanicDrawCommand command, float y,
        List<MechanicPreviewShape> shapes, List<MechanicPreviewLine> lines, List<MechanicRadarExtra> extras)
    {
        switch (command)
        {
            case MechanicDrawZoneCircle c:
                if (!Valid(c.Radius) || c.Radius <= 0f) return false;
                shapes.Add(new(new CircleGeometry(ToWorld(c.Center, y), c.Radius), IsSafeColor(c.Color), c.Outline, 0f));
                return true;
            case MechanicDrawZoneDonut d:
                if (!Valid(d.OuterRadius) || !Valid(d.InnerRadius) || d.OuterRadius <= 0f || d.OuterRadius <= d.InnerRadius) return false;
                shapes.Add(new(new DonutGeometry(ToWorld(d.Center, y), d.InnerRadius, d.OuterRadius), IsSafeColor(d.Color), d.Outline, 0f));
                return true;
            case MechanicDrawZoneCone c:
                return ConvertCone(c, y, shapes);
            case MechanicDrawZoneRect r:
                return ConvertRect(r, y, shapes);
            case MechanicDrawZoneCapsule c:
                return ConvertCapsule(c, y, shapes);
            case MechanicDrawZoneArcCapsule a:
                return ConvertArcCapsule(a, y, shapes);
            case MechanicDrawZoneCross c:
            {
                if (!Valid(c.Range) || !Valid(c.HalfWidth) || c.Range <= 0f || c.HalfWidth <= 0f) return false;
                var center = ToWorld(c.Origin, y);
                var safe = IsSafeColor(c.Color);
                shapes.Add(new(new RectangleGeometry(center, c.Rotation.Rad, 2f * c.HalfWidth, 2f * c.Range), safe, c.Outline, 0f));
                shapes.Add(new(new RectangleGeometry(center, c.Rotation.Rad + MathF.PI / 2f, 2f * c.HalfWidth, 2f * c.Range), safe, c.Outline, 0f));
                return true;
            }
            case MechanicDrawZoneTriangles t:
            {
                if (t.Vertices.Length < 3) return false;
                var safe = IsSafeColor(t.Color);
                var added = false;
                for (var i = 0; i + 2 < t.Vertices.Length; i += 3)
                {
                    var polygon = TryPolygon([ToWorld(t.Vertices[i], y), ToWorld(t.Vertices[i + 1], y), ToWorld(t.Vertices[i + 2], y)]);
                    if (polygon == null) continue;
                    shapes.Add(new(polygon, safe, false, 0f));
                    added = true;
                }
                return added;
            }
            case MechanicDrawComplexPoly p:
            {
                // 场地边框（白色描边）由 MP 边界层绘制，跳过避免重复
                if (p.Color == ColorBorder) return false;
                var safe = IsSafeColor(p.Color);
                var added = false;
                foreach (var part in p.Parts)
                {
                    if (part.Exterior.Length < 3) continue;
                    var polygon = TryPolygon(part.Exterior.Select(v => ToWorld(v, y)).ToArray());
                    if (polygon == null) continue;
                    shapes.Add(new(polygon, safe, p.Outline, 0f));
                    added = true;
                }
                return added;
            }
            case MechanicDrawLine l:
                lines.Add(new(ToWorld(l.A, y), ToWorld(l.B, y), IsSafeColor(l.Color)));
                return true;
            case MechanicDrawPolyline p:
            {
                if (p.Vertices.Length < 2) return false;
                var safe = IsSafeColor(p.Color);
                for (var i = 0; i + 1 < p.Vertices.Length; ++i)
                    lines.Add(new(ToWorld(p.Vertices[i], y), ToWorld(p.Vertices[i + 1], y), safe));
                if (p.Closed)
                    lines.Add(new(ToWorld(p.Vertices[^1], y), ToWorld(p.Vertices[0], y), safe));
                return true;
            }
            case MechanicDrawTriangle t:
            {
                if (t.Filled)
                {
                    var polygon = TryPolygon([ToWorld(t.A, y), ToWorld(t.B, y), ToWorld(t.C, y)]);
                    if (polygon == null) return false;
                    shapes.Add(new(polygon, IsSafeColor(t.Color), false, 0f));
                    return true;
                }
                var safe = IsSafeColor(t.Color);
                lines.Add(new(ToWorld(t.A, y), ToWorld(t.B, y), safe));
                lines.Add(new(ToWorld(t.B, y), ToWorld(t.C, y), safe));
                lines.Add(new(ToWorld(t.C, y), ToWorld(t.A, y), safe));
                return true;
            }
            case MechanicDrawQuad q:
            {
                var safe = IsSafeColor(q.Color);
                lines.Add(new(ToWorld(q.A, y), ToWorld(q.B, y), safe));
                lines.Add(new(ToWorld(q.B, y), ToWorld(q.C, y), safe));
                lines.Add(new(ToWorld(q.C, y), ToWorld(q.D, y), safe));
                lines.Add(new(ToWorld(q.D, y), ToWorld(q.A, y), safe));
                return true;
            }
            case MechanicDrawEye e:
                extras.Add(new(MechanicRadarExtraKind.Eye, ToWorld(e.Position, y), null, 0, e.Danger, e.Color));
                return true;
            case MechanicDrawTextWorld t:
                extras.Add(new(MechanicRadarExtraKind.Text, ToWorld(t.Position, y), t.Text, 0, IsDangerColor(t.Color), t.Color));
                return true;
            case MechanicDrawSignMarker s:
                extras.Add(new(MechanicRadarExtraKind.Sign, ToWorld(s.Position, y), SignLabel(s.Sign), (int)s.IconId, false, 0));
                return true;
            default:
                return false;
        }
    }

    private static bool ConvertCone(MechanicDrawZoneCone cone, float y, List<MechanicPreviewShape> shapes)
    {
        var outer = cone.OuterRadius;
        var inner = cone.InnerRadius;
        var halfAngle = cone.HalfAngle.Rad;
        if (!Valid(outer) || outer <= 0f || !Valid(halfAngle) || halfAngle <= 0f) return false;
        var center = ToWorld(cone.Center, y);
        var safe = IsSafeColor(cone.Color);
        if (inner <= 0f)
        {
            shapes.Add(new(new FanGeometry(center, cone.Direction.Rad, outer, MathF.Min(2f * halfAngle, 2f * MathF.PI)), safe, cone.Outline, 0f));
            return true;
        }
        if (outer <= inner) return false;
        var points = new List<Vector3>();
        var segments = ArcSegments(2f * halfAngle);
        for (var i = 0; i <= segments; ++i)
            points.Add(Polar(center, outer, cone.Direction.Rad - halfAngle + 2f * halfAngle * i / segments));
        for (var i = segments; i >= 0; --i)
            points.Add(Polar(center, inner, cone.Direction.Rad - halfAngle + 2f * halfAngle * i / segments));
        var polygon = TryPolygon(points);
        if (polygon == null) return false;
        shapes.Add(new(polygon, safe, cone.Outline, 0f));
        return true;
    }

    private static bool ConvertRect(MechanicDrawZoneRect rect, float y, List<MechanicPreviewShape> shapes)
    {
        var length = rect.LenFront + rect.LenBack;
        if (!Valid(length) || !Valid(rect.HalfWidth) || length <= 0f || rect.HalfWidth <= 0f) return false;
        var dir = rect.Direction;
        var dirLength = dir.Length();
        if (!Valid(dirLength) || dirLength <= 1e-6f) return false;
        dir /= dirLength;
        var center = ToWorld(rect.Origin + dir * ((rect.LenFront - rect.LenBack) * 0.5f), y);
        shapes.Add(new(new RectangleGeometry(center, dir.ToAngle().Rad, 2f * rect.HalfWidth, length), IsSafeColor(rect.Color), rect.Outline, 0f));
        return true;
    }

    private static bool ConvertCapsule(MechanicDrawZoneCapsule capsule, float y, List<MechanicPreviewShape> shapes)
    {
        if (!Valid(capsule.Radius) || capsule.Radius <= 0f || !Valid(capsule.Length) || capsule.Length < 0f) return false;
        var dir = capsule.Direction;
        var dirLength = dir.Length();
        if (!Valid(dirLength) || dirLength <= 1e-6f) return false;
        dir /= dirLength;
        var startAngle = dir.ToAngle().Rad;
        var points = new List<Vector3>();
        AppendArc(points, ToWorld(capsule.Start + dir * capsule.Length, y), capsule.Radius, startAngle - MathF.PI / 2f, MathF.PI);
        AppendArc(points, ToWorld(capsule.Start, y), capsule.Radius, startAngle + MathF.PI / 2f, MathF.PI);
        var polygon = TryPolygon(points);
        if (polygon == null) return false;
        shapes.Add(new(polygon, IsSafeColor(capsule.Color), capsule.Outline, 0f));
        return true;
    }

    private static bool ConvertArcCapsule(MechanicDrawZoneArcCapsule arc, float y, List<MechanicPreviewShape> shapes)
    {
        if (!Valid(arc.Radius) || arc.Radius <= 0f) return false;
        var sweep = arc.AngularLength.Rad;
        if (!Valid(sweep) || sweep == 0f) return false;
        var startOffset = arc.Start - arc.OrbitCenter;
        var orbitRadius = startOffset.Length();
        if (!Valid(orbitRadius) || orbitRadius <= 1e-6f) return false;
        var baseAngle = startOffset.ToAngle().Rad;
        var segments = ArcSegments(MathF.Abs(sweep));
        var orbit = ToWorld(arc.OrbitCenter, y);
        var outer = new List<Vector3>(segments + 1);
        var inner = new List<Vector3>(segments + 1);
        for (var i = 0; i <= segments; ++i)
        {
            var angle = baseAngle + sweep * i / segments;
            outer.Add(Polar(orbit, orbitRadius + arc.Radius, angle));
            inner.Add(Polar(orbit, MathF.Max(0f, orbitRadius - arc.Radius), angle));
        }
        var points = new List<Vector3>(outer);
        points.AddRange(CapBetween(outer[^1], inner[^1]));
        for (var i = segments; i >= 0; --i)
            points.Add(inner[i]);
        points.AddRange(CapBetween(inner[0], outer[0]));
        var polygon = TryPolygon(points);
        if (polygon == null) return false;
        shapes.Add(new(polygon, IsSafeColor(arc.Color), arc.Outline, 0f));
        return true;
    }

    private static IEnumerable<Vector3> CapBetween(Vector3 from, Vector3 to)
    {
        var center = (from + to) * 0.5f;
        var outward = from - center;
        var radius = outward.Length();
        if (radius <= 1e-6f) yield break;
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

    private static Vector3 ToWorld(WPos position, float y) => position.ToVec3(y);

    private static WorldPolygonGeometry? TryPolygon(IReadOnlyList<Vector3> points)
    {
        if (points.Count < 3) return null;
        try
        {
            return new WorldPolygonGeometry(points);
        }
        catch
        {
            return null;
        }
    }

    private static bool Valid(float value) => float.IsFinite(value);

    public static string SignLabel(Sign sign) => sign switch
    {
        Sign.Attack1 or Sign.Attack2 or Sign.Attack3 or Sign.Attack4 or Sign.Attack5
            or Sign.Attack6 or Sign.Attack7 or Sign.Attack8
            => ((int)sign + 1 - (sign >= Sign.Attack6 ? (int)Sign.Attack6 - 5 : 0)).ToString(),
        Sign.Bind1 or Sign.Bind2 or Sign.Bind3 => $"B{(int)sign - (int)Sign.Bind1 + 1}",
        Sign.Ignore1 or Sign.Ignore2 => $"I{(int)sign - (int)Sign.Ignore1 + 1}",
        Sign.Square => "□",
        Sign.Circle => "○",
        Sign.Cross => "×",
        Sign.Triangle => "△",
        _ => sign.ToString(),
    };
}
