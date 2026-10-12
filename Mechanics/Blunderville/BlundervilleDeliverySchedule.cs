using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// 2026-10-13 route research, 21 Delivery rounds: all 25 carrying hazards spawn
// at the same places and run on the course-start clock (first effect 0.06–
// 0.45s after t0, residual p95 <= 0.45s). MP waited at the hub for the charge
// and rhythm trackers to learn cycles that were known all along, then lost
// every crystal (0 deliveries in 5 automated rounds). Forecast them from
// t0 instead, with the calibrated footprints: the effective hit is 0.40s
// before the effect packet, +-0.45s, footprints grown 0.15m. Each device is
// re-anchored on its own recent effects, because families drift
// independently by up to +-0.55s after 60s.
internal sealed class BlundervilleDeliverySchedule
{
    internal const double HitLead = .40, Window = .45, Horizon = 15, Memory = 15;
    internal const float Growth = .15f, Height = 6;

    private sealed record Device(uint Action, IGeometryShape Footprint, Vector3[] Anchors, (double First, double Period)[] Beats);

    private static Device Box(uint action, float x, float z, float rotation, float along0, float along1, float halfWidth, (double, double)[] beats)
    {
        var direction = new Vector3(MathF.Sin(rotation), 0, MathF.Cos(rotation));
        var center = new Vector3(x, Height, z) + direction * ((along0 + along1) / 2);
        return new(action, new RectangleGeometry(center, rotation, 2 * halfWidth + 2 * Growth, along1 - along0 + 2 * Growth), [new(x, Height, z)], beats);
    }

    private static Device Segment(float ax, float az, float bx, float bz, float halfWidth, float extension, (double, double)[] beats)
    {
        Vector3 a = new(ax, Height, az), b = new(bx, Height, bz);
        var delta = b - a;
        return new(34774, new RectangleGeometry((a + b) / 2, MathF.Atan2(delta.X, delta.Z), 2 * halfWidth + 2 * Growth,
            delta.Length() + 2 * extension + 2 * Growth), [a, b], beats);
    }

    // Wind 34800 is deliberately absent: it cannot be avoided on the north
    // carry (118 of 119 human carries were hit) and never drops the crystal.
    private static readonly Device[] Devices =
    [
        Box(34799, -228.6f, 263.9f, 2.094f, -2.5f, 2.5f, 2f, [(0.198, 4.1015)]),
        Box(34799, -226.6f, 267.4f, 2.094f, -2.5f, 2.5f, 2f, [(2.172, 4.1004)]),
        Box(34799, -224.6f, 270.8f, 2.094f, -2.5f, 2.5f, 2f, [(0.198, 4.1016)]),
        Box(34799, -214.7f, 255.9f, 2.094f, -2.5f, 2.5f, 2f, [(2.172, 4.1004)]),
        Box(34799, -212.7f, 259.4f, 2.094f, -2.5f, 2.5f, 2f, [(0.199, 4.1016)]),
        Box(34799, -210.7f, 262.9f, 2.094f, -2.5f, 2.5f, 2f, [(2.172, 4.1004)]),
        Box(34799, -204f, 216.5f, 0f, -2.5f, 2.5f, 2f, [(0.2, 3.7001)]),
        Box(34716, -203f, 226.9f, 1.571f, -2.2f, 2.2f, 2.1f, [(3.368, 4.2168), (0.511, 4.2185)]),
        Box(34716, -200f, 226.9f, 1.57f, -2.2f, 2.2f, 2.1f, [(3.119, 4.2126), (0.953, 4.2164)]),
        Box(34716, -197f, 226.9f, 1.571f, -2.2f, 2.2f, 2.1f, [(2.65, 4.2162), (1.303, 4.2143)]),
        Box(34799, -196f, 216.5f, 0f, -2.5f, 2.5f, 2f, [(1.994, 3.6993)]),
        Box(34716, -190.1f, 261.2f, -0.524f, -2.2f, 2.2f, 2.1f, [(2.617, 4.2181), (1.266, 4.2164)]),
        Box(34716, -188.6f, 258.6f, -0.524f, -2.2f, 2.2f, 2.1f, [(3.12, 4.2126), (0.952, 4.2164)]),
        Box(34716, -187.1f, 256f, -0.524f, -2.2f, 2.2f, 2.1f, [(3.409, 4.2144), (0.536, 4.2171)]),
        Box(34716, -171f, 272.2f, -0.524f, -2.2f, 2.2f, 2.1f, [(2.616, 4.2181), (1.266, 4.2164)]),
        Box(34716, -169.5f, 269.6f, -0.524f, -2.2f, 2.2f, 2.1f, [(3.12, 4.2126), (0.951, 4.2165)]),
        Box(34716, -168f, 267f, -0.524f, -2.2f, 2.2f, 2.1f, [(3.408, 4.2144), (0.536, 4.2171)]),
        Segment(-221.06f, 269.95f, -226.12f, 261.13f, 2f, 0.6f, [(4.09, 8.0098), (0.104, 8.0096)]),
        Segment(-218.43f, 268.45f, -223.5f, 259.66f, 2f, 0.6f, [(6.079, 8.0089), (2.072, 8.0079)]),
        Segment(-207.2f, 261.92f, -212.24f, 253.16f, 2f, 0.6f, [(3.147, 5.9732), (0.199, 5.97)]),
        Segment(-188.98f, 263.57f, -173.75f, 272.36f, 2f, 0.6f, [(5.981, 8.02), (2.072, 8.008)]),
        Segment(-188.53f, 260.37f, -171.19f, 270.34f, 2f, 0.6f, [(4.199, 7.9968), (0.104, 8.0098)]),
        Segment(-176.56f, 259.66f, -181.66f, 268.48f, 2f, 0.6f, [(3.232, 5.966), (0.167, 5.9729)]),
        Segment(-169.7f, 267.75f, -187f, 257.77f, 2f, 0.6f, [(6.078, 8.009), (2.073, 8.0078)]),
        Segment(-169.27f, 264.55f, -184.53f, 255.73f, 2f, 0.6f, [(4.09, 8.0098), (0.104, 8.0097)]),
    ];

    private readonly List<(DateTime At, double Residual)>[] _residuals = Devices.Select(_ => new List<(DateTime, double)>()).ToArray();
    private DateTime? _start;

    public bool Active => _start != null;

    // Set from the Director start every scan; a recovered start has no clock.
    public void SetStart(DateTime? start)
    {
        if (start == _start) return;
        _start = start;
        foreach (var list in _residuals) list.Clear();
    }

    public void Observe(uint action, Vector3 position, DateTime at)
    {
        if (_start is not { } start || action is not (34716 or 34774 or 34799)) return;
        for (var i = 0; i < Devices.Length; ++i)
        {
            var device = Devices[i];
            if (device.Action != action || !device.Anchors.Any(a => DistanceXZ(a, position) < 1.5f)) continue;
            var elapsed = (at - start).TotalSeconds;
            var residual = device.Beats.Select(b => elapsed - (b.First + Math.Round((elapsed - b.First) / b.Period) * b.Period))
                .MinBy(Math.Abs);
            if (Math.Abs(residual) > 1) return;
            var list = _residuals[i];
            list.Add((at, residual));
            list.RemoveAll(r => (at - r.At).TotalSeconds > Memory);
            return;
        }
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        if (_start is not { } start) yield break;
        var elapsed = (now - start).TotalSeconds;
        for (var i = 0; i < Devices.Length; ++i)
        {
            var device = Devices[i];
            var recent = _residuals[i].Select(r => r.Residual).Order().ToArray();
            var offset = recent.Length > 0 ? recent[recent.Length / 2] : 0;
            foreach (var (first, period) in device.Beats)
                for (var k = Math.Max(0, Math.Floor((elapsed - first - 1) / period)); first + k * period - HitLead + offset <= elapsed + Horizon; ++k)
                {
                    var hit = start.AddSeconds(first + k * period + offset - HitLead);
                    if (hit.AddSeconds(Window) < now) continue;
                    // Telegraphed at the course start: an unchanged forecast must stay
                    // equal across scans, or navigation re-registers it and every
                    // pending solve goes stale (the player then stands still).
                    yield return new($"delivery/{i}/{k}/{first}", device.Footprint, hit.AddSeconds(-Window), hit.AddSeconds(Window), start);
                }
        }
    }

    // The same calibrated footprint, for replays and tests.
    internal static bool Hits(uint action, Vector3 source, Vector3 player)
        => Devices.Where(d => d.Action == action && d.Anchors.Any(a => DistanceXZ(a, source) < 1.5f))
            .Any(d => d.Footprint.Contains(player with { Y = Height }, -Growth));

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
