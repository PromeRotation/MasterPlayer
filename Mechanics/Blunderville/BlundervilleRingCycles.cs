using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// 2026-10-13 route research, 21 Ring rounds: the W/E fans (34797 inner half,
// 34798 outer half, R8 180deg) fire together every 4.951s and alternate,
// and each corner fires one circle (34805, R6) every 2.566s alternating its
// outer and inner spot (residuals <= 0.26/0.38s). A cast is visible only
// 1.2s/2.7s ahead; the inner fan covers the whole inner line of its side,
// which takes ~2.7s to cross. Project the next cycles from the latest casts.
internal sealed class BlundervilleRingCycles
{
    internal const double FanPeriod = 4.9511, CirclePeriod = 2.5656, FanCast = 1.2, CircleCast = 2.7;
    internal const double Early = .2, Late = .5;
    // 2026-10-15 13:18 ARR: two stuns at (179.6,-124.4) and (178.9,-125.3),
    // 0.4–1.1m on the safe side of the West fan's dividing line. All ARRs:
    // fan stuns reach 1.1m across that line (both fans), so the seam strip
    // either side of it is dangerous with every fan.
    internal const float SeamWidth = 1.5f;
    internal const bool FanSeam = true;
    private static readonly Vector3 West = new(180, 10, -125), East = new(220, 10, -125);
    private static readonly Vector3[][] Corners =
    [
        [new(183.03f, 10, -108.03f), new(188.69f, 10, -113.69f)],
        [new(216.97f, 10, -108.03f), new(211.31f, 10, -113.69f)],
    ];
    private (DateTime At, bool Inner)? _fan;
    private readonly (DateTime At, int Spot)?[] _circles = new (DateTime, int)?[2];

    public void Reset() { _fan = null; _circles[0] = _circles[1] = null; }

    public void Cast(uint action, Vector3 source, DateTime at)
    {
        if (action is 34797 or 34798 && (Near(source, West) || Near(source, East)))
        {
            if (_fan is not { } last || (at - last.At).TotalSeconds > 1) _fan = (at, action == 34797);
            return;
        }
        if (action != 34805) return;
        for (var corner = 0; corner < 2; ++corner)
            for (var spot = 0; spot < 2; ++spot)
                if (Near(source, Corners[corner][spot])) _circles[corner] = (at, spot);
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        if (_fan is { } fan && (now - fan.At).TotalSeconds < 3 * FanPeriod)
            for (var k = 1; k <= 2; ++k)
            {
                var cast = fan.At.AddSeconds(k * FanPeriod);
                if (cast.AddSeconds(FanCast + Late) < now) continue;
                var inner = fan.Inner ^ k % 2 == 1;
                foreach (var (source, sign) in new[] { (West, 1f), (East, -1f) })
                {
                    yield return Window($"ring/fan/{cast.Ticks}/{sign}", new FanGeometry(source, sign * (inner ? 1 : -1) * MathF.PI / 2, 8, MathF.PI),
                        cast, FanCast, fan.At);
                    if (FanSeam)
                        yield return Window($"ring/fanseam/{cast.Ticks}/{sign}", new RectangleGeometry(source, 0, 2 * SeamWidth, 16), cast, FanCast, fan.At);
                }
            }
        for (var corner = 0; corner < 2; ++corner)
        {
            if (_circles[corner] is not { } circle || (now - circle.At).TotalSeconds >= 3 * CirclePeriod) continue;
            for (var k = 1; k <= 3; ++k)
            {
                var cast = circle.At.AddSeconds(k * CirclePeriod);
                if (cast.AddSeconds(CircleCast + Late) < now) continue;
                var spot = circle.Spot ^ k % 2;
                yield return Window($"ring/circle/{corner}/{cast.Ticks}", new CircleGeometry(Corners[corner][spot], 6), cast, CircleCast, circle.At,
                    BlundervilleEncounter.RingLead(34805, Corners[corner][spot]));
            }
        }
    }

    // The live cast window is [end + 0.3, end + 0.5]; a projected cycle starts
    // a little earlier for its timing spread, or with the live cast's lead.
    private static BlundervilleHazard Window(string id, IGeometryShape shape, DateTime cast, double duration, DateTime anchor, double lead = 0)
        => new(id, shape, cast.AddSeconds(duration - Math.Max(Early, lead - .3)), cast.AddSeconds(duration + Late), anchor);

    // The inner spot of each corner (NWi/NEi, 16m from the centre).
    internal static bool Inner(Vector3 source) => Near(source, Corners[0][1]) || Near(source, Corners[1][1]);

    private static bool Near(Vector3 a, Vector3 b) => Vector2.DistanceSquared(new(a.X, a.Z), new(b.X, b.Z)) < .25f;
}
