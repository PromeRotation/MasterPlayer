using System.Numerics;

namespace MasterPlayer.Radar;

internal readonly record struct RadarPathDash(Vector2 Start, Vector2 End, float Progress);

// Geometry only: shared by the live and replay radar, without calling ImGui.
internal static class RadarPathDashes
{
    internal const int MaxDashIntervals = 2048;
    private const double DashLength = 6;
    private const double Period = 10.5;

    public static bool TryBuild(IReadOnlyList<Vector2> path, float animationTime, List<RadarPathDash> output)
    {
        output.Clear();
        if (path.Count == 0 || !float.IsFinite(animationTime)) return false;
        foreach (var point in path)
            if (!float.IsFinite(point.X) || !float.IsFinite(point.Y)) return false;

        double total = 0;
        for (var i = 1; i < path.Count; ++i) total += Length(path[i - 1], path[i]);
        var offset = Modulo((double)animationTime * 16, Period);
        var fadeLength = Math.Max(total, 1);
        double accumulated = 0;
        var remaining = MaxDashIntervals;
        for (var i = 1; i < path.Count && remaining > 0; ++i)
        {
            var a = path[i - 1];
            var b = path[i];
            var length = Length(a, b);
            if (length >= 0.01)
            {
                var first = -Modulo(accumulated - offset, Period);
                var count = (int)Math.Min(remaining, Math.Ceiling((length - first) / Period));
                // Advance an integer dash index, never a rounded float cursor.
                // The old cursor could add less than half an ULP and loop forever on the draw thread.
                for (var dash = 0; dash < count; ++dash)
                {
                    var start = first + dash * Period;
                    var from = Math.Max(0, start);
                    var to = Math.Min(length, start + DashLength);
                    if (to <= from) continue;
                    var p = Interpolate(a, b, from / length);
                    var q = Interpolate(a, b, to / length);
                    if (p != q) output.Add(new(p, q, (float)((accumulated + from) / fadeLength)));
                }
                remaining -= count;
            }
            accumulated += length;
        }
        return true;
    }

    private static double Length(Vector2 a, Vector2 b)
    {
        var x = (double)b.X - a.X;
        var y = (double)b.Y - a.Y;
        return Math.Sqrt(x * x + y * y);
    }
    private static double Modulo(double value, double modulus) => (value % modulus + modulus) % modulus;
    private static Vector2 Interpolate(Vector2 a, Vector2 b, double t)
        => new((float)(a.X + ((double)b.X - a.X) * t), (float)(a.Y + ((double)b.Y - a.Y) * t));
}
