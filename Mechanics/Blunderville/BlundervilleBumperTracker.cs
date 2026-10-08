using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Contact and the endpoint pulse have separate clocks. Replay the last observed
// movement cycle, with a short velocity fallback until that cycle is confirmed.
internal sealed class BlundervilleBumperTracker
{
    private sealed class Track
    {
        public readonly List<(DateTime At, Vector3 Position)> Samples = [];
        public readonly List<(DateTime At, Vector3 Position)> Hits = [];
        public double Period;
        public DateTime ForecastAt;
        public DateTime CoarseUntil;
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
        public readonly Dictionary<DateTime, BlundervilleHazard> Slices = [];
    }
    private readonly Dictionary<ulong, Track> _tracks = [];

    public void Observe(BlundervilleActor actor, DateTime now)
    {
        if (actor.Oid != 16339 || actor.Dead) { Remove(actor.Id); return; }
        if (!_tracks.TryGetValue(actor.Id, out var track)) _tracks[actor.Id] = track = new();
        var samples = track.Samples;
        if (samples.Count > 0 && (now < samples[^1].At || Vector3.Distance(samples[^1].Position, actor.Position) > 10))
        { samples.Clear(); track.Period = 0; track.Hits.Clear(); track.Slices.Clear(); track.ForecastAt = track.CoarseUntil = default; }
        if (track.Period > 0 && track.Hits.Count > 0 && now - track.Hits[^1].At > TimeSpan.FromSeconds(track.Period))
        { track.Period = 0; track.Hits.Clear(); track.Slices.Clear(); track.ForecastAt = default; }
        // Distant objects in ARR/live can update at <2Hz. A stale stepped
        // trajectory is not a precise clock: keep the full observed sweep until
        // one fresh cycle has replaced those sparse samples.
        if (samples.Count > 0 && MathF.Abs(actor.Position.X - samples[^1].Position.X) > 1)
        { track.CoarseUntil = now.AddSeconds(Math.Max(10, track.Period)); track.Slices.Clear(); }
        if (samples.Count == 0 || now > samples[^1].At) samples.Add((now, actor.Position));
        samples.RemoveAll(s => s.At < now.AddSeconds(-15));
        const long slice = TimeSpan.TicksPerSecond / 4;
        var anchor = new DateTime(now.Ticks / slice * slice, now.Kind);
        var bodyRadius = MathF.Max(1.2f, float.IsFinite(actor.HitboxRadius) ? actor.HitboxRadius : 0);
        if (track.Slices.TryGetValue(anchor, out var current) && (!current.Geometry.Contains(actor.Position + Vector3.UnitX * bodyRadius)
            || !current.Geometry.Contains(actor.Position - Vector3.UnitX * bodyRadius)))
        { track.Slices.Clear(); track.ForecastAt = default; }
        if (track.ForecastAt == anchor) return;
        track.ForecastAt = anchor;
        var radius = bodyRadius + .2f;
        var forecast = new List<BlundervilleHazard>();
        foreach (var expired in track.Slices.Keys.Where(t => t < anchor.AddSeconds(-.5)).ToArray()) track.Slices.Remove(expired);
        var observedMin = actor.Position; var observedMax = actor.Position;
        foreach (var sample in samples)
        { observedMin = Vector3.Min(observedMin, sample.Position); observedMax = Vector3.Max(observedMax, sample.Position); }
        var previous = samples.LastOrDefault(s => s.At <= now.AddSeconds(-.3));
        var velocity = previous.At == default ? Vector3.Zero : (actor.Position - previous.Position) / (float)(now - previous.At).TotalSeconds;
        if (velocity.Length() > 3) velocity = Vector3.Zero;
        Vector3 Predict(double seconds)
        {
            var offset = seconds - track.Period;
            if (track.Period > 0) while (offset > 0) offset -= track.Period;
            var at = now.AddSeconds(offset);
            if (track.Period > 0 && samples[0].At <= at)
            {
                var low = 1; var high = samples.Count - 1;
                while (low < high)
                {
                    var middle = (low + high) / 2;
                    if (samples[middle].At < at) low = middle + 1; else high = middle;
                }
                if (low < samples.Count && samples[low].At >= at)
                    return Vector3.Lerp(samples[low - 1].Position, samples[low].Position,
                        (float)((at - samples[low - 1].At).TotalSeconds / (samples[low].At - samples[low - 1].At).TotalSeconds));
            }
            var p = actor.Position + velocity * (float)Math.Min(seconds, .6);
            return p with { X = Math.Clamp(p.X, observedMin.X, observedMax.X) };
        }
        var offsetSeconds = (anchor - now).TotalSeconds;
        // Before learning a cycle only local extrapolation is justified; the
        // race planner waits outside the lane until both clocks are confirmed.
        for (var i = 0; i < (track.Period > 0 ? 48 : 4); ++i)
        {
            var from = anchor.AddSeconds(i * .25);
            if (track.Period > 0 && track.Slices.TryGetValue(from, out var existing)) { forecast.Add(existing); continue; }
            // Packet sampling and cycle drift are uncertainty in animation time,
            // not a permanent larger cylinder at either stationary endpoint.
            var coarse = now < track.CoarseUntil;
            var min = coarse ? observedMin : Predict(offsetSeconds + i * .25 - .2);
            var max = coarse ? observedMax : min;
            for (var sample = 1; !coarse && sample <= 9; ++sample)
            {
                var p = Predict(offsetSeconds + i * .25 - .2 + sample * (.65 / 9));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            // The immediate sample always wins over a slightly shifted old cycle.
            if (i == 0) { min = Vector3.Min(min, actor.Position); max = Vector3.Max(max, actor.Position); }
            var hazard = new BlundervilleHazard($"bumper/{actor.Id}/{from.Ticks}", new RectangleGeometry((min + max) / 2, 0,
                max.X - min.X + radius * 2, max.Z - min.Z + radius * 2), from.AddSeconds(-.1), from.AddSeconds(.35), now);
            forecast.Add(hazard);
            if (track.Period > 0) track.Slices[from] = hazard;
        }
        track.Forecast = forecast;
    }

    public void Effect(ulong source, Vector3 position, DateTime now)
    {
        if (!_tracks.TryGetValue(source, out var track)) return;
        var hits = track.Hits;
        if (hits.Count > 0 && now - hits[^1].At < TimeSpan.FromSeconds(.2)) return;
        hits.Add((now, position));
        if (hits.Count > 4) hits.RemoveAt(0);
        if (hits.Count < 4) return;
        var a = (hits[2].At - hits[0].At).TotalSeconds; var b = (hits[3].At - hits[1].At).TotalSeconds;
        var period = a is > 8 and < 13 && Math.Abs(a - b) < .6
            && Vector3.Distance(hits[2].Position, hits[0].Position) < .15f && Vector3.Distance(hits[3].Position, hits[1].Position) < .15f ? (a + b) / 2 : 0;
        if (track.Period == 0 || period == 0 || Math.Abs(period - track.Period) > .3)
        { track.Slices.Clear(); track.ForecastAt = default; }
        track.Period = period;
    }

    public IEnumerable<BlundervilleHazard> Hazards => _tracks.Values.SelectMany(t => t.Forecast);
    public void Remove(ulong id) => _tracks.Remove(id);
    public void Reset() => _tracks.Clear();
    public bool Ready(ulong id) => _tracks.TryGetValue(id, out var track) && track.Period > 0;
}
