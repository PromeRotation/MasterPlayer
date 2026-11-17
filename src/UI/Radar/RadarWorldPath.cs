using System.Numerics;

namespace MasterPlayer.Radar;

internal enum RadarWorldPathPart
{
    Core,
    Edge,
}

internal readonly record struct RadarWorldPathQuad(RadarWorldPathPart Part, Vector3 A, Vector3 B, Vector3 C, Vector3 D);

/// <summary>Arrow head pointing along the route: Tip, left wing (front/back), notch, right wing (back/front).</summary>
internal readonly record struct RadarWorldPathChevron(Vector3 Tip, Vector3 LeftFront, Vector3 LeftBack, Vector3 Notch, Vector3 RightBack, Vector3 RightFront);

/// <summary>
/// World route geometry: a mitered ribbon (bright core + dark edges) with arrow heads flowing towards the destination.
/// Geometry only, no host or ImGui calls.
/// </summary>
internal static class RadarWorldPath
{
    internal const int MaxPoints = 192;
    internal const int MaxChevrons = 48;

    private const float MinStep = 0.1f;
    private const float CollinearDot = 0.9995f;
    private const float MiterMinDot = 0.3f;
    private const float CoreHalfWidth = 0.075f;
    private const float EdgeHalfWidth = 0.125f;
    private const float ChevronSpacing = 2.4f;
    private const float ChevronSpeed = 2.2f;
    private const float ChevronHalfSpan = 0.34f;
    private const float ChevronLead = 0.20f;
    private const float ChevronSweep = 0.30f;
    private const float ChevronThickness = 0.17f;
    private const float ChevronStartClearance = 0.6f;
    private const float ChevronEndClearance = 0.9f;

    public static bool TryBuild(IReadOnlyList<Vector3> path, double timeSeconds, List<Vector3> points,
        List<RadarWorldPathQuad> quads, List<RadarWorldPathChevron> chevrons, out Vector3 destination)
    {
        points.Clear();
        quads.Clear();
        chevrons.Clear();
        destination = default;
        if (path.Count == 0 || !double.IsFinite(timeSeconds)) return false;

        foreach (var point in path)
        {
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)) return false;
            if (points.Count > 0 && Distance(points[^1], point) < MinStep) continue;
            // Smoothed routes arrive densely sampled; straight runs collapse to one segment.
            if (points.Count >= 2 && Vector2.Dot(Direction(points[^2], points[^1]), Direction(points[^1], point)) > CollinearDot)
                points[^1] = point;
            else if (points.Count < MaxPoints)
                points.Add(point);
            else
                break;
        }

        destination = path[^1];
        if (points.Count < 2) return true;
        destination = points[^1];

        var segments = points.Count - 1;
        double total = 0;
        for (var i = 0; i < segments; ++i)
        {
            var a = points[i];
            var b = points[i + 1];
            var direction = Direction(a, b);
            var normal = new Vector2(-direction.Y, direction.X);
            var startOffset = i > 0 ? Miter(Direction(points[i - 1], a), direction, normal) : normal;
            var endOffset = i + 1 < segments ? Miter(direction, Direction(b, points[i + 2]), normal) : normal;

            quads.Add(new(RadarWorldPathPart.Core,
                Offset(a, startOffset, CoreHalfWidth), Offset(b, endOffset, CoreHalfWidth),
                Offset(b, endOffset, -CoreHalfWidth), Offset(a, startOffset, -CoreHalfWidth)));
            quads.Add(new(RadarWorldPathPart.Edge,
                Offset(a, startOffset, EdgeHalfWidth), Offset(b, endOffset, EdgeHalfWidth),
                Offset(b, endOffset, CoreHalfWidth), Offset(a, startOffset, CoreHalfWidth)));
            quads.Add(new(RadarWorldPathPart.Edge,
                Offset(a, startOffset, -CoreHalfWidth), Offset(b, endOffset, -CoreHalfWidth),
                Offset(b, endOffset, -EdgeHalfWidth), Offset(a, startOffset, -EdgeHalfWidth)));
            total += Distance(a, b);
        }

        BuildChevrons(points, total, timeSeconds, chevrons);
        return true;
    }

    private static void BuildChevrons(List<Vector3> points, double total, double timeSeconds, List<RadarWorldPathChevron> chevrons)
    {
        var usable = total - ChevronEndClearance;
        if (usable <= ChevronStartClearance) return;
        var spacing = Math.Max(ChevronSpacing, total / MaxChevrons);
        var phase = timeSeconds * ChevronSpeed % spacing;
        if (phase < 0) phase += spacing;

        var segment = 0;
        double segmentStart = 0;
        var segmentLength = (double)Distance(points[0], points[1]);
        // Integer index, never an accumulated float cursor.
        for (var index = 0; index < MaxChevrons; ++index)
        {
            var at = phase + index * spacing;
            if (at > usable) break;
            if (at < ChevronStartClearance) continue;
            while (segment + 2 < points.Count && at > segmentStart + segmentLength)
            {
                segmentStart += segmentLength;
                ++segment;
                segmentLength = Distance(points[segment], points[segment + 1]);
            }

            var a = points[segment];
            var b = points[segment + 1];
            var t = (float)Math.Clamp((at - segmentStart) / Math.Max(segmentLength, MinStep), 0, 1);
            var center = Vector3.Lerp(a, b, t);
            var forward = Direction(a, b);
            var side = new Vector2(-forward.Y, forward.X);

            var tip = Move(center, forward, ChevronLead);
            var notch = Move(tip, forward, -ChevronThickness);
            var leftFront = Move(Move(center, forward, ChevronLead - ChevronSweep), side, ChevronHalfSpan);
            var rightFront = Move(Move(center, forward, ChevronLead - ChevronSweep), side, -ChevronHalfSpan);
            chevrons.Add(new(tip, leftFront, Move(leftFront, forward, -ChevronThickness), notch,
                Move(rightFront, forward, -ChevronThickness), rightFront));
        }
    }

    // Offsets are in units of half width, so neighbouring segments share their corner vertices.
    private static Vector2 Miter(Vector2 incoming, Vector2 outgoing, Vector2 normal)
    {
        if (Vector2.Dot(incoming, outgoing) < MiterMinDot) return normal;
        var sum = new Vector2(-incoming.Y, incoming.X) + new Vector2(-outgoing.Y, outgoing.X);
        var length = sum.Length();
        if (length < 1e-4f) return normal;
        var miter = sum / length;
        var scale = Vector2.Dot(miter, normal);
        return scale < 0.5f ? normal : miter / scale;
    }

    private static Vector2 Direction(Vector3 from, Vector3 to)
    {
        var delta = new Vector2(to.X - from.X, to.Z - from.Z);
        var length = delta.Length();
        return length < 1e-5f ? Vector2.UnitX : delta / length;
    }

    private static float Distance(Vector3 a, Vector3 b)
        => new Vector2(b.X - a.X, b.Z - a.Z).Length();

    private static Vector3 Offset(Vector3 point, Vector2 offset, float distance)
        => new(point.X + offset.X * distance, point.Y, point.Z + offset.Y * distance);

    private static Vector3 Move(Vector3 point, Vector2 direction, float distance)
        => new(point.X + direction.X * distance, point.Y, point.Z + direction.Y * distance);
}
