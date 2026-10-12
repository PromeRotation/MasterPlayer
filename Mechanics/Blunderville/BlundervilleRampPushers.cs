using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// ARR 2026-10-12 15:10:49 FirstB: the three 34774 pushers on the final ramp
// (rows z≈-364/-356/-348) were only drawn for the step just announced, so the
// solver walked into the next step and one push cost ~18s. In seven FirstB
// ARRs each pusher steps ~3.5m every 1.606s (median; packet jitter ±0.1s)
// between five stops x≈-6.9/-3.5/0/3.5/6.9, reversing at the ends.
internal sealed class BlundervilleRampPushers
{
    // The push is the effect packet itself: six ARRs show players inside a
    // step's sweep 0.4–1.4s after its packet without a push. Late packets
    // (up to ~0.5s past the phase) stay covered by the late margin.
    internal const double StepSeconds = 1.606, Occupied = .3, Horizon = 6;
    internal const float EndX = 6.9f, StepLength = 3.45f;
    private sealed class Track
    {
        public readonly List<DateTime> Times = [];
        public Vector3? LastFrom;
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
    }
    private readonly Dictionary<ulong, Track> _tracks = [];

    public static bool OnRamp(Vector3 from, Vector3 to)
        => BlundervilleEncounter.Finite(from) && BlundervilleEncounter.Finite(to) && from.Z is > -370 and < -340
            && MathF.Abs(to.Z - from.Z) < .5f && MathF.Abs(from.X) < EndX + .5f && MathF.Abs(to.X) < EndX + .5f
            && MathF.Abs(to.X - from.X) is > 2 and < 5;

    public void Effect(ulong source, Vector3 from, Vector3? destination, DateTime now)
    {
        if (!BlundervilleEncounter.Finite(from) || from.Z is <= -370 or >= -340 || MathF.Abs(from.X) > EndX + .5f) return;
        if (!_tracks.TryGetValue(source, out var track)) _tracks[source] = track = new();
        var previous = track.LastFrom; track.LastFrom = from;
        // 01:20 ARR packets carry no destination (-1000); the step then
        // continues the move from the previous packet's start.
        if (destination is not { } to || !OnRamp(from, to))
        {
            if (previous is not { } last || !OnRamp(last, from)) return;
            to = Next(last, from);
        }
        var times = track.Times;
        if (times.Count > 0)
        {
            var elapsed = (now - times[^1]).TotalSeconds;
            if (elapsed is >= 0 and < .5) return;
            if (elapsed < 0 || elapsed > 4 * StepSeconds) times.Clear();
        }
        times.Add(now);
        if (times.Count > 8) times.RemoveAt(0);
        // Packets only arrive late (00:14 ARR: up to ~0.5s, then a short
        // interval); the motion keeps its phase. Anchor on the earliest
        // projection of the recent steps, and keep late packets covered.
        var anchor = now;
        foreach (var time in times)
        {
            var projected = time.AddSeconds(Math.Round((now - time).TotalSeconds / StepSeconds) * StepSeconds);
            if (projected < anchor && (now - projected).TotalSeconds < .6) anchor = projected;
        }
        var lateness = (now - anchor).TotalSeconds;
        var forecast = new List<BlundervilleHazard>();
        var a = from; var b = to;
        for (var k = 1; k <= Horizon; ++k)
        {
            (a, b) = (b, Next(a, b));
            var at = anchor.AddSeconds(k * StepSeconds);
            var jitter = .15 + .05 * k;
            forecast.Add(new($"ramp/{source}/{k}", Shape(a, b), at.AddSeconds(-jitter), at.AddSeconds(Occupied + Math.Max(lateness, .5) + jitter), now));
        }
        track.Forecast = forecast;
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        foreach (var track in _tracks.Values)
            foreach (var hazard in track.Forecast)
                if (hazard.Until >= now) yield return hazard;
    }

    public void Remove(ulong source) => _tracks.Remove(source);
    public void Reset() => _tracks.Clear();

    private static Vector3 Next(Vector3 a, Vector3 b)
    {
        var direction = MathF.Sign(b.X - a.X);
        var x = b.X + direction * StepLength;
        if (MathF.Abs(x) > EndX + .4f) x = b.X - direction * StepLength;
        return b with { X = Math.Clamp(x, -EndX, EndX) };
    }

    // Same body footprint as the announced charge (BlundervilleEncounter).
    private static IGeometryShape Shape(Vector3 a, Vector3 b)
    {
        var delta = b - a; delta.Y = 0;
        return new RectangleGeometry((a + b) / 2, MathF.Atan2(delta.X, delta.Z), 3, delta.Length() + 3);
    }
}
