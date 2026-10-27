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
    internal const double HitLead = .40, Window = .45, Horizon = 30, Memory = 15;
    internal const float Growth = .15f, Height = 6;
    // 25 grounded north-lane contacts: stun arrives 0.186–1.593s after
    // 34799, including 22:38's +0.435s. The early sweep reaches z=219.84;
    // later contacts remain in the core footprint. Keep the animation hard,
    // unlike the optional charger-body preference, and retain an early band
    // for packet jitter (execution additionally allows 0.35s of lateness).
    // 239 human north carries (all ARRs, 0 stuns): the band z 219→214 is
    // entered from 1.8s after a packet and left up to 0.3s after the next one
    // (3.7s period). The stun is only ever after the packet, so the lead before
    // it can be short: 0.65 left a 1.3s gap and MP waited 8s at z=224 (02:32).
    // 16 replayed rounds: 0.35 brings the north carry median from 21.4s to
    // 16.2s (humans 15.9s) with no north loss; the west rows keep 0.65 (0.35
    // lost a west crystal).
    // West at 0.35 lost a crystal in 16 replays; keep 0.65 there.
    internal const double NorthHammerLead = .35, WestHammerLead = .65;
    internal const double NorthHammerTail = 1.75, NorthHammerTipTail = .65;
    private static readonly RectangleGeometry[] NorthHammerTips =
        [new(new(-204, Height, 217.15f), 0, 4.3f, 6), new(new(-196, Height, 217.15f), 0, 4.3f, 6)];

    internal sealed record Device(uint Action, IGeometryShape Footprint, Vector3[] Anchors, (double First, double Period)[] Beats);

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

    // Wind 34800 has its own displacement window and barrier pockets below.
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
    private readonly Queue<double> _windOffsets = new();

    public bool Active => _start != null;

    // Set from the Director start every scan; a recovered start has no clock.
    public void SetStart(DateTime? start)
    {
        if (start == _start) return;
        _start = start;
        _windOffsets.Clear();
        foreach (var list in _residuals) list.Clear();
    }

    public void Observe(uint action, Vector3 position, DateTime at)
    {
        if (_start is { } windStart && action == 34800 && DistanceXZ(position, new(-200, 6, 212)) < 1.5f)
        {
            var elapsed = (at - windStart).TotalSeconds;
            var residual = elapsed - (BlundervilleDeliveryWind.First + Math.Round((elapsed - BlundervilleDeliveryWind.First) / BlundervilleDeliveryWind.Period) * BlundervilleDeliveryWind.Period);
            if (Math.Abs(residual) <= 1)
            {
                _windOffsets.Enqueue(residual);
                while (_windOffsets.Count > 3) _windOffsets.Dequeue();
            }
            return;
        }
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

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now, bool northHammersOnly = false)
    {
        if (_start is not { } start) yield break;
        var elapsed = (now - start).TotalSeconds;
        var windOffset = _windOffsets.Count > 0 ? _windOffsets.Order().ElementAt(_windOffsets.Count / 2) : 0;
        for (var k = Math.Max(0, (int)Math.Floor((elapsed - BlundervilleDeliveryWind.First - 2) / BlundervilleDeliveryWind.Period)); !northHammersOnly; ++k)
        {
            var packet = start.AddSeconds(BlundervilleDeliveryWind.First + k * BlundervilleDeliveryWind.Period + windOffset);
            if (packet > now.AddSeconds(Horizon)) break;
            if (packet.AddSeconds(BlundervilleDeliveryWind.LaunchUntil) < now) continue;
            for (var part = 0; part < BlundervilleDeliveryWind.Exposed.Length; ++part)
                yield return new($"delivery/wind/{k}/{part}", BlundervilleDeliveryWind.Exposed[part],
                    packet.AddSeconds(BlundervilleDeliveryWind.LaunchFrom), packet.AddSeconds(BlundervilleDeliveryWind.LaunchUntil), start);
        }
        for (var i = 0; i < Devices.Length; ++i)
        {
            var device = Devices[i];
            if (northHammersOnly && (device.Action != 34799 || device.Anchors[0].Z >= 220)) continue;
            var recent = _residuals[i].Select(r => r.Residual).Order().ToArray();
            var offset = recent.Length > 0 ? recent[recent.Length / 2] : 0;
            foreach (var (first, period) in device.Beats)
                for (var k = Math.Max(0, Math.Floor((elapsed - first - 2) / period)); first + k * period - HitLead + offset <= elapsed + Horizon; ++k)
                {
                    var hit = start.AddSeconds(first + k * period + offset - HitLead);
                    // Telegraphed at the course start: an unchanged forecast must stay
                    // equal across scans, or navigation re-registers it and every
                    // pending solve goes stale (the player then stands still).
                    // 2026-10-15 01:12 ARR: the west 34799 rows stunned 0.37–1.37s
                    // after their packets (8 stuns, 4 crystals lost), outside the
                    // packet window, exactly like the north row. Use the measured
                    // animation window for every hammer; tips stay north-only.
                    var northHammer = device.Action == 34799 && device.Anchors[0].Z < 220;
                    if (device.Action == 34799)
                    {
                        var packet = hit.AddSeconds(HitLead);
                        if (packet.AddSeconds(NorthHammerTail) >= now)
                            yield return new($"delivery/hammer/{i}/{k}", device.Footprint,
                                packet.AddSeconds(-(northHammer ? NorthHammerLead : WestHammerLead)), packet.AddSeconds(NorthHammerTail), start);
                        if (northHammer && packet.AddSeconds(NorthHammerTipTail) >= now)
                            yield return new($"delivery/hammer-tip/{i}/{k}",
                                NorthHammerTips[device.Anchors[0].X < -200 ? 0 : 1],
                                packet, packet.AddSeconds(NorthHammerTipTail), start);
                    }
                    else if (hit.AddSeconds(Window) >= now)
                        yield return new($"delivery/{i}/{k}/{first}", device.Footprint, hit.AddSeconds(-Window), hit.AddSeconds(Window), start);
                    if (device.Anchors.Length == 2 && RestingBodies)
                    {
                        // 2026-10-15 02:32 ARR: the east charger finished its 20m charge
                        // at (-188.53,260.37) 0.65s after the packet and stunned MP on
                        // arrival there 1.5s after it (1.0m from the resting body); the
                        // crystal was then lost. The resting body is hard until the
                        // charger leaves on its return beat.
                        var beat = Array.IndexOf(device.Beats, (first, period));
                        var packet = hit.AddSeconds(HitLead);
                        var other = device.Beats[1 - beat];
                        var after = (packet - start).TotalSeconds + offset;
                        var leave = start.AddSeconds(other.First + Math.Ceiling((after - other.First) / other.Period) * other.Period + offset);
                        if (leave >= now && leave > packet)
                            yield return new($"delivery/rest/{i}/{k}/{first}", new CircleGeometry(beat == 0 ? device.Anchors[0] : device.Anchors[1], RestRadius),
                                packet.AddSeconds(RestFrom), leave, start);
                    }
                    if (device.Anchors.Length == 2 && Bodies)
                        foreach (var (zone, from, until, part) in Body(device, Array.IndexOf(device.Beats, (first, period))))
                        {
                            var packet = hit.AddSeconds(HitLead);
                            if (packet.AddSeconds(until) >= now)
                                yield return new($"{BodyPrefix}{i}/{k}/{first}/{part}", zone, packet.AddSeconds(from), packet.AddSeconds(until), start);
                        }
                }
        }
    }

    // 2026-10-14 02:08 ARR: the west charger stunned MP 0.9s after its packet,
    // 4.3m along its 10m segment, outside the packet window; stunned, it then
    // lost the crystal to 34799. 24 Delivery rounds, every player position
    // near a charger (stunned within 0.5s or not): a 20m charge stuns only
    // 16m on to its end, 0.4–1.3s after the packet (up to ~25% of samples
    // there) and less to 2.0s; a 10m charge 3.5–6.5m along at 0.3–1.0s, then
    // 3.5m to its end at 1.3–2.0s. Elsewhere along the segments, never.
    // Beat 0 runs from the second anchor to the first, beat 1 back (all
    // recorded packets, all 8 segments).
    internal const bool Bodies = true;
    internal const bool RestingBodies = true;
    internal const float RestRadius = 1f;
    internal const double RestFrom = .5;
    // Planner-only: navigation does not register these with the runtime.
    internal const string BodyPrefix = "delivery/body/";
    internal static IEnumerable<(IGeometryShape Zone, double From, double Until, int Part)> Body(Device device, int beat)
    {
        var (from, to) = beat == 0 ? (device.Anchors[1], device.Anchors[0]) : (device.Anchors[0], device.Anchors[1]);
        foreach (var (near, far, start, end, part) in BodyZones(DistanceXZ(from, to)))
            yield return (Zone(from, to, near, far, ((RectangleGeometry)device.Footprint).Width), start, end, part);
    }

    // Planned around only (BlundervilleFinalPlanner falls back without them):
    // the high-rate core of each zone. Avoiding the whole stun map, or making
    // it a hard limit, left the corridors without any way (0–3 of 15 replays).
    internal static IEnumerable<(float Near, float Far, double From, double Until, int Part)> BodyZones(float length)
        => length > 12 ? [(length - 2.5f, length + .5f, .5, 1.1, 0)] : [(3.5f, 6, .4, .9, 0)];
    // The full recorded stun map, for the replays.
    internal static IEnumerable<(float Near, float Far, double From, double Until, int Part)> TruthZones(float length)
        => length > 12
            ? [(length - 4, length + 1, .4, 1.3, 0), (length - 4, length + 1, 1.3, 2, 1)]
            : [(3.5f, 6.5f, .3, 1, 0), (3.5f, length + .5f, 1.3, 2, 1)];

    internal static RectangleGeometry Zone(Vector3 from, Vector3 to, float near, float far, float width)
    {
        var direction = Vector3.Normalize((to - from) with { Y = 0 });
        return new(from + direction * ((near + far) / 2), MathF.Atan2(direction.X, direction.Z), width, far - near);
    }

    // The same calibrated footprint, for replays and tests.
    internal static bool Hits(uint action, Vector3 source, Vector3 player)
        => Devices.Where(d => d.Action == action && d.Anchors.Any(a => DistanceXZ(a, source) < 1.5f))
            .Any(d => d.Footprint.Contains(player with { Y = Height }, -Growth));

    private static float DistanceXZ(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
}
