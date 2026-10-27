using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Delivery's dash effect names the complete ray before the visible body
// reaches its other endpoint. Opposite rays share one observed cycle.
internal sealed class BlundervilleChargeTracker
{
    private sealed record Beat(Vector3 From, Vector3 To, DateTime At);
    private sealed class Track
    {
        public readonly List<Beat> Beats = [];
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
        public bool Trusted;
    }
    private readonly Dictionary<ulong, Track> _tracks = [];

    public void Effect(ulong source, Vector3 from, Vector3? destination, DateTime now, float? rotation = null)
    {
        _tracks.TryGetValue(source, out var track);
        if (track?.Beats.LastOrDefault() is { } known && (Distance(from, known.To) < .25f || Distance(from, known.From) < .25f))
        {
            if (rotation is { } angle && float.IsFinite(angle))
            {
                var ray = Vector2.Normalize(new(known.To.X - known.From.X, known.To.Z - known.From.Z));
                var alignment = Vector2.Dot(ray, new(MathF.Sin(angle), MathF.Cos(angle)));
                if (MathF.Abs(alignment) > .98f)
                { from = alignment > 0 ? known.From : known.To; destination = alignment > 0 ? known.To : known.From; }
            }
            destination ??= Distance(from, known.To) < .25f ? known.From : known.To;
        }
        if (destination is not { } to) return;
        if (!Valid(from, to)) return;
        if (track == null) _tracks[source] = track = new();
        var beats = track.Beats;
        if (beats.Count > 0)
        {
            var previous = beats[^1]; var elapsed = (now - previous.At).TotalSeconds;
            if (elapsed is >= 0 and < .2) return;
            var sameRay = Distance(from, previous.To) < .25f && Distance(to, previous.From) < .25f
                || Distance(from, previous.From) < .25f && Distance(to, previous.To) < .25f;
            if (elapsed < 0 || elapsed > 6 || !sameRay)
                beats.Clear();
        }
        beats.Add(new(from, to, now));
        if (beats.Count > 7) beats.RemoveAt(0);
        var forecast = new List<BlundervilleHazard>();
        AddSweep(forecast, source, from, to, now, now, 0, 0);
        track.Trusted = false;
        if (beats.Count >= 3)
        {
            var intervals = beats.Zip(beats.Skip(1), (a, b) => (b.At - a.At).TotalSeconds).ToArray();
            var sorted = intervals.Order().ToArray();
            var period = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
            var phases = beats.Select((b, i) => (b.At - beats[0].At).TotalSeconds - i * period).ToArray();
            var phase = phases.Min();
            // Hit packets can trail their animation by up to 1.12s in the
            // supplied ARR. Preserve its stable phase across the resulting
            // long/short interval pair, while bounding drift and stale input.
            if (period is > 2.7 and < 4.5 && intervals.All(t => t is > 1.5 and < 6)
                && phases.Max() - phase < 1.3)
            {
                track.Trusted = true;
                for (var cycle = 0; cycle < 3; ++cycle)
                {
                    var at = beats[0].At.AddSeconds(phase + (beats.Count + cycle) * period);
                    var drift = .35 + cycle * .1;
                    var late = Math.Max(.35, phases.Max() - phase) + cycle * .1;
                    AddSweep(forecast, source, cycle % 2 == 0 ? to : from, cycle % 2 == 0 ? from : to,
                        at, now, drift, late);
                }
            }
        }
        track.Forecast = forecast;
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        foreach (var track in _tracks.Values)
            foreach (var hazard in track.Forecast)
                if (hazard.Until >= now) yield return hazard;
    }

    public bool Ready(ulong source, DateTime now) => _tracks.TryGetValue(source, out var track)
        && track.Trusted && now >= track.Beats[^1].At && now - track.Beats[^1].At < TimeSpan.FromSeconds(6);
    public void Remove(ulong source) => _tracks.Remove(source);
    public void Reset() => _tracks.Clear();
    private static bool Valid(Vector3 from, Vector3 to) => BlundervilleEncounter.Finite(from) && BlundervilleEncounter.Finite(to)
        && from.X is > -245 and < -155 && from.Z is > 245 and < 280
        && to.X is > -245 and < -155 && to.Z is > 245 and < 280 && Distance(from, to) is > 8 and < 22;
    private static float Distance(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Z), new(b.X, b.Z));
    private static IGeometryShape Shape(Vector3 a, Vector3 b)
        => new RectangleGeometry((a + b) / 2, MathF.Atan2(b.X - a.X, b.Z - a.Z), 3, Distance(a, b) + 3);

    private static void AddSweep(List<BlundervilleHazard> result, ulong source, Vector3 from, Vector3 to,
        DateTime at, DateTime observed, double early, double late)
    {
        var id = $"charge/{source}/{at.Ticks}";
        result.Add(new(id + "/hit", Shape(from, to), at.AddSeconds(-early), at.AddSeconds(.15 + late), observed));
        // The recorded body reaches its far endpoint in 0.379–1.192s. Sweep
        // all speeds in that range through bounded time slices, instead of
        // treating every point on the whole dash as occupied for 1.25s.
        for (var i = 0; i < 7; ++i)
        {
            var start = i * .2; var end = (i + 1) * .2;
            var a = Vector3.Lerp(from, to, (float)Math.Clamp((start - .15) / 1.25, 0, 1));
            var b = Vector3.Lerp(from, to, (float)Math.Clamp(end / .35, 0, 1));
            result.Add(new(id + $"/body/{i}", Shape(a, b), at.AddSeconds(start - early), at.AddSeconds(end + late), observed));
        }
    }
}
