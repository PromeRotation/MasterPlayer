using System.Numerics;
using MasterPlayer.Radar;

internal static class RadarPathDashesRegression
{
    public static void Run(Action<string, Action> run)
    {
        foreach (var (time, start, length) in new[] { (8.449701f, 350.0569f, 295.78702f),
            (0.5985971f, 396.99228f, 241.70349f), (11.641619f, 396.74463f, 266.3812f),
            (8.211881f, 221.02449f, 45.629986f), (2.816045f, 18.212461f, 257.7823f) })
            run($"radar dash rendering terminates at former float stall {time:R}/{start:R}", () =>
            {
                var result = new List<RadarPathDash>();
                Vector2[] path = [Vector2.Zero, new(start, 0), new(start + length, 0)];
                Check(RadarPathDashes.TryBuild(path, time, result), "valid path rejected");
                Validate(result, start + length);
                Check(result.Count > 0 && result[^1].End.X > start + length - 11, "path stopped at the former freeze point");
                Check(result.Count < 150, "unbounded duplicate dash output");
            });

        run("radar dashes preserve phase across short, duplicate and bent path segments", () =>
        {
            var result = new List<RadarPathDash>();
            // A right angle at 20px and duplicate waypoints; compare by distance along the polyline.
            Vector2[] path = [new(0, 0), new(3, 0), new(3, 0), new(20, 0), new(20, 80)];
            for (var frame = 0; frame < 60 * 144; ++frame)
            {
                var time = frame / 144f;
                Check(RadarPathDashes.TryBuild(path, time, result), "valid path rejected");
                Check(result.Count <= 15 && result.All(d => d.Start != d.End && Vector2.Distance(d.Start, d.End) <= 6.001f), "duplicate or overlong dash");
                for (var distance = 0.25; distance < 100; distance += 0.5)
                {
                    var point = distance < 20 ? new Vector2((float)distance, 0) : new Vector2(20, (float)distance - 20);
                    var phase = ((distance - (double)time * 16) % 10.5 + 10.5) % 10.5;
                    if (Math.Min(Math.Abs(phase), Math.Min(Math.Abs(phase - 6), Math.Abs(phase - 10.5))) < 0.0001) continue;
                    var drawn = result.Any(d => OnSegment(point, d.Start, d.End));
                    Check(drawn == (phase < 6), $"dash phase discontinuity at time {time}, distance {distance}");
                }
            }
        });

        run("radar invalid positions and animation times clear stale geometry", () =>
        {
            var result = new List<RadarPathDash>();
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                RadarPathDashes.TryBuild([Vector2.Zero, new(100, 0)], 0, result);
                Check(!RadarPathDashes.TryBuild([Vector2.Zero, new(invalid, 0)], 0, result) && result.Count == 0, "invalid position left old route visible");
                Check(!RadarPathDashes.TryBuild([Vector2.Zero, new(100, 0)], invalid, result) && result.Count == 0, "invalid animation accepted");
            }
        });

        run("radar huge finite paths have a frame-wide drawing budget", () =>
        {
            var result = new List<RadarPathDash>();
            Check(RadarPathDashes.TryBuild([Vector2.Zero, new(1e20f, 0), new(-1e20f, 0)], 2.816045f, result), "finite path rejected");
            Check(result.Count <= RadarPathDashes.MaxDashIntervals && result.All(d =>
                float.IsFinite(d.Start.X) && float.IsFinite(d.End.X) && float.IsFinite(d.Progress)), "drawing budget or numeric bounds exceeded");
            Check(RadarPathDashes.TryBuild([new(float.MaxValue, 0), new(-float.MaxValue, 0)], 0, result), "large finite coordinates overflowed");
            Check(result.Count <= RadarPathDashes.MaxDashIntervals, "degenerate projections escaped the drawing budget");
        });

        run("radar stationary and empty paths produce no dash work", () =>
        {
            var result = new List<RadarPathDash>();
            Check(RadarPathDashes.TryBuild([Vector2.One, Vector2.One], 0, result) && result.Count == 0, "stationary path produced dashes");
            Check(!RadarPathDashes.TryBuild([], 0, result) && result.Count == 0, "empty path accepted");
        });
    }

    private static bool OnSegment(Vector2 p, Vector2 a, Vector2 b)
        => Math.Abs(Vector2.Distance(a, p) + Vector2.Distance(p, b) - Vector2.Distance(a, b)) < 0.00001f;
    private static void Validate(List<RadarPathDash> dashes, float length)
    {
        float last = -1;
        foreach (var d in dashes)
        {
            Check(d.Start.X >= last && d.End.X > d.Start.X && d.End.X <= length + 0.001f, "dash does not advance monotonically inside the path");
            Check(d.End.X - d.Start.X <= 6.001f && d.Progress is >= 0 and <= 1, "dash length or fade is invalid");
            last = d.End.X;
        }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
