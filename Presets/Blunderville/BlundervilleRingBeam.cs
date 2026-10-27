using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// 2026-10-13 route research, 21 Ring rounds: the 34808 beam turns on the
// course-start clock from -2.319 rad, at -0.191 rad/s until 30.07s, -0.244
// rad/s until 75.06s and -0.2793 rad/s after (tick residual <= 0.05 rad). MP
// saw only the current tick and waited where the beam arrived (17:44: 6s at
// the NE corner with it 1.6 rad behind). Forecast the sweep instead,
// re-anchored on the live tick rotations.
internal sealed class BlundervilleRingBeam
{
    internal static readonly Vector3 Center = new(200, 10, -125);
    // A longer forecast blocks the next node long before the beam arrives:
    // Ring replays, beam ticks hit 20 -> 11 (6s) / 10 (2.5s), cast hits 21 -> 26 / 20.
    internal const double Slot = .25, Horizon = 2.5, Lead = .1;
    // The ring's outer edge is 25m from the centre; a metre of margin covers
    // the re-anchored residual there (0.04 rad in the first seconds of 19:48).
    internal const float Theta0 = -2.319f, Width = 10, Length = 27;
    private static readonly (double From, double Omega)[] Segments = [(0, -.191), (30.07, -.244), (75.06, -.2793)];
    private readonly Queue<double> _residuals = new();
    private DateTime? _start;

    public bool Active => _start != null;

    // Set from the Director start every scan; a recovered start has no clock.
    public void SetStart(DateTime? start)
    {
        if (start == _start) return;
        _start = start;
        _residuals.Clear();
    }

    internal static double Model(double elapsed)
    {
        double angle = Theta0, from = 0;
        for (var i = 0; i < Segments.Length; ++i)
        {
            var until = i + 1 < Segments.Length ? Segments[i + 1].From : double.PositiveInfinity;
            angle += Segments[i].Omega * (Math.Min(elapsed, until) - from);
            if (elapsed <= until) break;
            from = until;
        }
        return angle;
    }

    public void Observe(float rotation, DateTime at)
    {
        if (_start is not { } start || !float.IsFinite(rotation)) return;
        var residual = Wrap(rotation - Model((at - start).TotalSeconds));
        if (Math.Abs(residual) > .5) return;
        _residuals.Enqueue(residual);
        while (_residuals.Count > 5) _residuals.Dequeue();
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        if (_start is not { } start) yield break;
        var ordered = _residuals.Order().ToArray();
        // Quantized so an unchanged forecast stays equal across scans.
        var offset = ordered.Length > 0 ? Math.Round(ordered[ordered.Length / 2], 2) : 0;
        var elapsed = (now - start).TotalSeconds;
        for (var k = (int)Math.Floor(elapsed / Slot); k * Slot <= elapsed + Horizon; ++k)
        {
            double from = k * Slot, until = from + Slot;
            var a = Model(from) + offset; var b = Model(until) + offset;
            var geometry = new RayRectangleGeometry(Center, (float)Wrap((a + b) / 2), Width + Length * (float)Math.Abs(b - a), Length);
            // Each slice already encloses the angular sweep, observation error
            // and its time margin; cast buffering would count them twice.
            yield return new($"beam/forecast/{k}", geometry, start.AddSeconds(from - Lead), start.AddSeconds(until + Lead), start)
                { HasMotionEnvelope = true };
        }
    }

    private static double Wrap(double angle) => Math.IEEERemainder(angle, 2 * Math.PI);
}
