using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

internal readonly record struct BlundervilleRampStep(DateTime At, float Z, float FromX, float ToX);

// ARR 2026-10-12 15:10:49 FirstB: the three 34774 pushers on the final ramp
// (rows z≈-364/-356/-348) were only drawn for the step just announced, so the
// solver walked into the next step and one push cost ~18s. In seven FirstB
// ARRs each pusher steps ~3.5m every 1.606s (median; packet jitter ±0.1s)
// between five stops x≈-6.9/-3.5/0/3.5/6.9, reversing at the ends.
internal sealed class BlundervilleRampPushers
{
    // The swept action hits near its effect packet. Its whole sweep does not
    // remain dangerous, but the moving/resting body can separately stun on
    // contact (20:57 ARR); RampAscent models that body throughout the step.
    // Late action packets stay covered by the late margin.
    internal const double StepSeconds = 1.606, Occupied = .3, Horizon = 6;
    internal const float EndX = 6.9f, StepLength = 3.45f, Half = 1.5f;
    private sealed class Track
    {
        public readonly List<DateTime> Times = [];
        public Vector3? LastFrom;
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
        public Vector3 From, To;
        public DateTime Anchor;
    }
    private readonly Dictionary<ulong, Track> _tracks = [];
    // 19 FirstB ARRs: all three rows step together, 1.604–1.612s per round.
    public double Period { get; private set; } = StepSeconds;

    public static bool OnRamp(Vector3 from, Vector3 to)
        => BlundervilleEncounter.Finite(from) && BlundervilleEncounter.Finite(to) && from.Z is > -370 and < -340
            && MathF.Abs(to.Z - from.Z) < .5f && MathF.Abs(from.X) < EndX + .5f && MathF.Abs(to.X) < EndX + .5f
            && MathF.Abs(to.X - from.X) is > 2 and < 5;

    public void Effect(ulong source, Vector3 from, Vector3? destination, DateTime now)
    {
        if (!BlundervilleEncounter.Finite(from) || from.Z is <= -370 or >= -340 || MathF.Abs(from.X) > EndX + .5f) return;
        if (!_tracks.TryGetValue(source, out var track)) _tracks[source] = track = new();
        // A packet's start can be a stale actor position (01:02 ARR: -6.88 for a
        // -3.52 -> 0 step); the previous step's destination is where this one starts.
        if (destination is { } announced && track.Anchor != default && (now - track.Anchor).TotalSeconds < 2.4
            && MathF.Abs(track.To.X - from.X) > .3f && OnRamp(track.To, announced))
            from = track.To;
        var previous = track.LastFrom; track.LastFrom = from;
        // 01:20 ARR packets carry no destination (-1000); the step then
        // continues the move from the previous packet's start.
        if (destination is not { } to || !OnRamp(from, to))
        {
            if (previous is { } last && OnRamp(last, from)) to = Next(last, from);
            // A repeated stale start: the step after the last known one, so the
            // clock stays fresh (01:20 ARR has neither destinations nor clean starts).
            else if (track.Anchor != default && (now - track.Anchor).TotalSeconds is > .8 and < 2.4)
                (from, to) = (track.To, Next(track.From, track.To));
            else return;
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
        Period = EstimatePeriod();
        // Packets only arrive late (00:14 ARR: up to ~0.5s, then a short
        // interval); the motion keeps its phase. Anchor on the earliest
        // projection of the recent steps, and keep late packets covered.
        var anchor = now;
        foreach (var time in times)
        {
            var projected = time.AddSeconds(Math.Round((now - time).TotalSeconds / Period) * Period);
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
        track.From = from; track.To = to; track.Anchor = anchor;
    }

    // All three pushers known within four steps: the step order is fixed, so a
    // missed packet or two leaves the forecast in place.
    public bool Ready(DateTime now)
        => _tracks.Values.Count(t => t.Anchor != default && (now - t.Anchor).TotalSeconds < 4 * Period) >= 3;

    // The current and coming steps at their scheduled packet times (the
    // earliest projection; packets only arrive late). A push catches a player
    // inside the step's x range and 1.5m of its row 0.1–0.2s before the packet
    // (all 10 of MP's recorded pushes, none of ~5000 other steps).
    public List<BlundervilleRampStep> Steps(DateTime until)
    {
        var result = new List<BlundervilleRampStep>();
        foreach (var track in _tracks.Values)
        {
            if (track.Anchor == default) continue;
            var a = track.From; var b = track.To;
            // Include the current step: its body stays at the destination after
            // the action effect and can still apply environmental stun.
            for (var k = 0; k < 64; ++k)
            {
                if (k > 0) (a, b) = (b, Next(a, b));
                var at = track.Anchor.AddSeconds(k * Period);
                if (at > until) break;
                result.Add(new(at, a.Z, a.X, b.X));
            }
        }
        result.Sort((x, y) => x.At.CompareTo(y.At));
        return result;
    }

    private double EstimatePeriod()
    {
        var samples = new List<double>();
        foreach (var track in _tracks.Values)
            for (var i = 1; i < track.Times.Count; ++i)
            {
                var delta = (track.Times[i] - track.Times[i - 1]).TotalSeconds;
                var steps = Math.Round(delta / StepSeconds);
                if (steps is >= 1 and <= 3 && Math.Abs(delta / steps - StepSeconds) < .05) samples.Add(delta / steps);
            }
        if (samples.Count < 6) return StepSeconds;
        samples.Sort();
        return samples[samples.Count / 2];
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        foreach (var track in _tracks.Values)
            foreach (var hazard in track.Forecast)
                if (hazard.Until >= now) yield return hazard;
    }

    public void Remove(ulong source) => _tracks.Remove(source);
    public void Reset() => _tracks.Clear();

    // Stops per row (19 ARRs): the low row turns at ±3.77, the others at ±3.5.
    private static float[] Stops(float z)
        => MathF.Abs(z + 347.83f) < .5f ? [-6.88f, -3.77f, 0, 3.77f, 6.88f]
            : MathF.Abs(z + 355.82f) < .5f ? [-6.85f, -3.49f, 0, 3.49f, 6.85f]
            : MathF.Abs(z + 363.82f) < .5f ? [-6.88f, -3.52f, 0, 3.52f, 6.88f] : [];

    private static Vector3 Next(Vector3 a, Vector3 b)
    {
        var direction = MathF.Sign(b.X - a.X);
        var stops = Stops(b.Z);
        if (stops.Length > 0)
        {
            var at = Array.IndexOf(stops, stops.MinBy(s => MathF.Abs(s - b.X)));
            var next = at + (int)direction;
            if (next < 0 || next >= stops.Length) next = at - (int)direction;
            return b with { X = stops[next] };
        }
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
