using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// Contact and the endpoint pulse have separate clocks. Replay the last observed
// movement cycle, with a conservative short body sweep until it is confirmed.
internal sealed class BlundervilleBumperTracker
{
    private sealed class Track
    {
        public readonly List<(DateTime At, Vector3 Position)> Samples = [];
        public readonly List<(DateTime At, Vector3 Position)> Hits = [];
        public double Period;
        public double Jitter;
        public DateTime ForecastAt;
        public DateTime CoarseUntil;
        public bool Coarse;
        public DateTime ChangedAt;
        public DateTime BatchUntil;
        public double BatchUncertainty;
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
        { samples.Clear(); track.Period = track.Jitter = 0; track.Hits.Clear(); track.Slices.Clear(); track.ForecastAt = track.CoarseUntil = track.ChangedAt = track.BatchUntil = default; track.BatchUncertainty = 0; }
        if (track.Period > 0 && track.Hits.Count > 0 && now - track.Hits[^1].At > TimeSpan.FromSeconds(track.Period))
        { track.Period = track.Jitter = 0; track.Hits.Clear(); track.Slices.Clear(); track.ForecastAt = default; }
        if (track.BatchUncertainty > 0 && now >= track.BatchUntil)
        { track.BatchUncertainty = 0; track.Slices.Clear(); track.ForecastAt = default; }
        if (samples.Count > 0)
        {
            var distance = MathF.Abs(actor.Position.X - samples[^1].Position.X);
            if (distance > 1)
            {
                // A scan can consume two movement packets together. Held scans
                // are not new position observations; use the actual change gap.
                var gap = track.ChangedAt == default ? 0 : (now - track.ChangedAt).TotalSeconds;
                if (track.Period > 0 && now - samples[0].At >= TimeSpan.FromSeconds(track.Period)
                    && distance <= 1.5 && gap is >= .2 and <= 1 && distance / gap <= 3
                    && ConsistentBatch(track, actor.Position, now, gap / 2 + .05))
                {
                    track.BatchUntil = now.AddSeconds(track.Period);
                    track.BatchUncertainty = Math.Max(track.BatchUncertainty, gap / 2 + .05);
                }
                else track.CoarseUntil = now.AddSeconds(Math.Max(10, track.Period));
                track.Slices.Clear(); track.ForecastAt = default;
            }
            if (distance > .03) track.ChangedAt = now;
        }
        else track.ChangedAt = now;
        if (samples.Count == 0 || now > samples[^1].At) samples.Add((now, actor.Position));
        samples.RemoveAll(s => s.At < now.AddSeconds(-15));
        var coarse = now < track.CoarseUntil;
        if (track.Coarse != coarse)
        { track.Coarse = coarse; track.Slices.Clear(); track.ForecastAt = default; }
        const long slice = TimeSpan.TicksPerSecond / 4;
        var anchor = new DateTime(now.Ticks / slice * slice, now.Kind);
        var bodyRadius = MathF.Max(1.2f, float.IsFinite(actor.HitboxRadius) ? actor.HitboxRadius : 0);
        if (track.Slices.TryGetValue(anchor, out var current) && (!current.Geometry.Contains(actor.Position + Vector3.UnitX * bodyRadius)
            || !current.Geometry.Contains(actor.Position - Vector3.UnitX * bodyRadius)))
        { track.Slices.Clear(); track.ForecastAt = default; }
        if (track.ForecastAt == anchor && track.Period > 0) return;
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
            if (TryCyclePosition(track, now, seconds, out var position)) return position;
            var p = actor.Position + velocity * (float)Math.Min(seconds, .6);
            return p with { X = Math.Clamp(p.X, observedMin.X, observedMax.X) };
        }
        var offsetSeconds = (anchor - now).TotalSeconds;
        // Before learning a cycle only a short observed sweep is justified; the
        // race planner waits outside the lane until both clocks are confirmed.
        for (var i = 0; i < (track.Period > 0 ? 88 : 5); ++i)
        {
            var from = anchor.AddSeconds(i * .25);
            if (track.Period > 0 && track.Slices.TryGetValue(from, out var existing)) { forecast.Add(existing); continue; }
            // Packet sampling and cycle drift are uncertainty in animation time,
            // not a permanent larger cylinder at either stationary endpoint.
            var conservative = coarse || track.Period == 0;
            // Freshly corrected phases need less padding than a prediction
            // spanning another full cycle. Retain the larger long-route bound.
            var uncertainty = Math.Min(.7 + track.Jitter, Math.Max(.2, Math.Max(track.BatchUncertainty, track.Jitter))
                + Math.Max(0, offsetSeconds + i * .25) * .025);
            var min = conservative ? observedMin : Predict(offsetSeconds + i * .25 - uncertainty);
            var max = conservative ? observedMax : min;
            for (var sample = 1; !conservative && sample <= 9; ++sample)
            {
                var p = Predict(offsetSeconds + i * .25 - uncertainty + sample * ((.25 + uncertainty * 2) / 9));
                min = Vector3.Min(min, p); max = Vector3.Max(max, p);
            }
            // The immediate sample always wins over a slightly shifted old cycle.
            if (i == 0) { min = Vector3.Min(min, actor.Position); max = Vector3.Max(max, actor.Position); }
            var until = from.AddSeconds(.35);
            if (track.Period == 0 && until > now.AddSeconds(1.1)) until = now.AddSeconds(1.1);
            IGeometryShape geometry = max.Z-min.Z < .1f ? new BlundervilleBumperSweep(min,max,radius)
                : new RectangleGeometry((min+max)/2,0,max.X-min.X+radius*2,max.Z-min.Z+radius*2);
            var hazard = new BlundervilleHazard($"bumper/{actor.Id}/{from.Ticks}", geometry, from.AddSeconds(-.1), until, now);
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
        if (hits.Count > MaximumHits) hits.RemoveAt(0);
        // Each endpoint effect corrects the animation phase. Retaining a far
        // future slice across several small period changes preserves old drift.
        track.Slices.Clear(); track.ForecastAt = default;
        (track.Period, track.Jitter) = FitPeriod(hits);
    }

    // ARR 2026-10-12 01:01 delivered single endpoint packets up to ~.5s late
    // (half cycles 5.11/4.51/5.33/4.23s around a stable 9.6s period). Comparing
    // only the last two full cycles discarded the clock after every late packet,
    // so the entry gate never opened. Fit the median of all matching full cycles
    // and keep the measured spread as additional timing uncertainty.
    internal const int MaximumHits = 8;
    internal static (double Period, double Jitter) FitPeriod(IReadOnlyList<(DateTime At, Vector3 Position)> hits)
    {
        if (hits.Count < 4) return (0, 0);
        // The latest two cycles must still alternate between the same endpoints.
        if (Vector3.Distance(hits[^1].Position, hits[^3].Position) >= .15f
            || Vector3.Distance(hits[^2].Position, hits[^4].Position) >= .15f) return (0, 0);
        var cycles = new List<double>();
        for (var i = 0; i + 2 < hits.Count; ++i)
            if (Vector3.Distance(hits[i + 2].Position, hits[i].Position) < .15f)
                cycles.Add((hits[i + 2].At - hits[i].At).TotalSeconds);
        var sorted = cycles.Order().ToArray();
        var period = (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
        if (period is <= 8 or >= 13) return (0, 0);
        var inliers = cycles.Where(c => Math.Abs(c - period) < .6).ToArray();
        // Two agreeing cycles remain the minimum evidence; a single deviating
        // cycle is a late packet, not a new animation clock.
        if (inliers.Length < 2 || inliers.Length * 2 < cycles.Count + 1) return (0, 0);
        var mean = inliers.Average();
        var jitter = Math.Min(.5, cycles.Max(c => Math.Abs(c - period)) / 2);
        return (mean, jitter);
    }

    private static bool ConsistentBatch(Track track, Vector3 position, DateTime now, double uncertainty)
    {
        if (track.Hits.Count < 4 || MathF.Abs(position.Y - track.Hits[^1].Position.Y) > .1f
            || MathF.Abs(position.Z - track.Hits[^1].Position.Z) > .1f) return false;
        var endpointMin = track.Hits.Min(h => h.Position.X);
        var endpointMax = track.Hits.Max(h => h.Position.X);
        if (position.X < endpointMin - .2f || position.X > endpointMax + .2f) return false;
        var min = float.PositiveInfinity; var max = float.NegativeInfinity;
        for (var sample = 0; sample <= 10; ++sample)
        {
            if (!TryCyclePosition(track, now, -uncertainty + sample * (uncertainty * 2 / 10), out var predicted)) return false;
            min = MathF.Min(min, predicted.X); max = MathF.Max(max, predicted.X);
        }
        return position.X >= min - .2f && position.X <= max + .2f;
    }

    private static bool TryCyclePosition(Track track, DateTime now, double seconds, out Vector3 position)
    {
        position = default;
        var samples = track.Samples;
        if (track.Period <= 0 || samples.Count < 2) return false;
        var offset = seconds - track.Period;
        while (offset > 0) offset -= track.Period;
        var at = now.AddSeconds(offset);
        if (samples[0].At > at || samples[^1].At < at) return false;
        var low = 1; var high = samples.Count - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (samples[middle].At < at) low = middle + 1; else high = middle;
        }
        position = Vector3.Lerp(samples[low - 1].Position, samples[low].Position,
            (float)((at - samples[low - 1].At).TotalSeconds / (samples[low].At - samples[low - 1].At).TotalSeconds));
        return true;
    }

    public IEnumerable<BlundervilleHazard> Hazards => _tracks.Values.SelectMany(t => t.Forecast);
    public BlundervilleHazard UnknownTraversalHazard(BlundervilleActor actor, DateTime now)
    {
        var min = actor.Position; var max = actor.Position;
        var complete = false;
        if (_tracks.TryGetValue(actor.Id, out var track))
        {
            foreach (var sample in track.Samples)
            { min = Vector3.Min(min, sample.Position); max = Vector3.Max(max, sample.Position); }
            complete = track.Samples.Count > 1 && now - track.Samples[0].At >= TimeSpan.FromSeconds(13) && max.X - min.X >= 3.8f;
        }
        if (!complete)
        {
            // A partial sample may be either endpoint. The observed devices
            // travel up to 4.028m horizontally between their endpoint pulses.
            min = min with { X = MathF.Min(min.X, actor.Position.X - 4.1f) };
            max = max with { X = MathF.Max(max.X, actor.Position.X + 4.1f) };
        }
        var radius = MathF.Max(3, float.IsFinite(actor.HitboxRadius) ? actor.HitboxRadius : 0);
        return new($"bumper-unknown/{actor.Id}", new RectangleGeometry((min + max) / 2, 0,
            max.X - min.X + radius * 2, max.Z - min.Z + radius * 2), now, now.AddSeconds(22), now);
    }
    public void Remove(ulong id) => _tracks.Remove(id);
    public void Reset() => _tracks.Clear();
    public bool Ready(ulong id) => _tracks.TryGetValue(id, out var track) && track.Period > 0;
}
