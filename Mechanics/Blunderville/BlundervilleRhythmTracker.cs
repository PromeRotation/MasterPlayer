using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// No-cast devices: learn two complete, matching animation cycles from effects.
// Each forecast retains its own pose and absolute impact time, never a moving "now" window.
internal sealed class BlundervilleRhythmTracker
{
    internal sealed record Beat(DateTime At, Vector3 Position, float Rotation);
    private sealed class Track
    {
        public readonly List<Beat> Beats = [];
        public readonly List<DateTime> BurstStarts = [];
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
        public readonly Dictionary<(int X, int Z), Beat> Spots = [];
        public bool RecurrenceBroken;
    }

    // Final 34801/34802 helpers revisit fixed spots with one period that four
    // recordings agree on within 0.05s (19:51, 22:18, 23:23 and 10-12 15:10);
    // only the starting phase differs. Each spot is predictable from its own
    // latest hit, so the course need not wait 9–17 jittered beats to learn.
    // Anchored to the spot's own latest packet the forecast is usually within
    // ~0.05s; 0.1s missed jittered packets in the 19:51 recording. Execution
    // lag is added by the consumer (Final planner / solver), not here.
    // The 4-step 34801 rows (one 1.86s step per cycle) revisit spots ±0.2s.
    internal const double RecurrenceLead = .25, RecurrenceTail = .3, RecurrenceHorizon = 20, RecurrenceTolerance = .4;
    internal static double? FinalRecurrence(uint action, Vector3 p)
    {
        if (MathF.Abs(p.X) > 15) return null;
        return action switch
        {
            34801 when p.Z is > 140 and < 205 => 6.03,
            34801 when p.Z is > 215 and < 255 => 11.07,
            34802 when p.Z is > 260 and < 275 => 2.54,
            34802 when p.Z is > 215 and < 235 => 3.26,
            // End-of-course alternating circles: every endpoint repeats each 5.08s.
            34796 or 29795 when p.Z is > 130 and < 180 => 5.08,
            _ => null
        };
    }
    private readonly Dictionary<(ulong Source, uint Action), Track> _tracks = [];

    public void Effect(BlundervilleActor actor, uint action, Vector3 position, float rotation, DateTime now)
    {
        var length = PatternLength(actor, action, position);
        if (length == 0 || !float.IsFinite(rotation) || !BlundervilleCatalog.TryGet(action, out var rule) || rule.Shape == null) return;
        var key = (actor.Id, action);
        if (!_tracks.TryGetValue(key, out var track)) _tracks[key] = track = new();
        var beats = track.Beats;
        if (beats.Count > 0)
        {
            var elapsed = (now - beats[^1].At).TotalSeconds;
            if (elapsed is >= 0 and < .2) return;
            if (elapsed < 0 || elapsed > 8 || action == 34773 && Vector3.DistanceSquared(position, beats[^1].Position) > .04f)
            { beats.Clear(); track.BurstStarts.Clear(); track.Forecast = []; }
            if (elapsed < 0) { track.Spots.Clear(); track.RecurrenceBroken = false; }
        }
        beats.Add(new(now, position, rotation));
        if (action == 34773)
        {
            // Eight contact packets belong to one continuous spinning animation.
            // Keep its whole window stable instead of flickering between packets.
            if (beats.Count == 1 || now - beats[^2].At > TimeSpan.FromSeconds(1.5))
            {
                track.BurstStarts.Add(now);
                if (track.BurstStarts.Count > 5) track.BurstStarts.RemoveAt(0);
                track.Forecast = RotatingBurst(actor.Id, track.BurstStarts, rule.Shape, position, rotation, now);
            }
            if (beats.Count > 2) beats.RemoveAt(0);
            return;
        }
        if (action == 34799)
        {
            if (beats.Count > 7) beats.RemoveAt(0);
            track.Forecast = StationarySweep(actor.Id, beats, rule.Shape, now);
            return;
        }
        if (beats.Count > (action == 34717 ? AlternatingHistory : 2 * length + 1)) beats.RemoveAt(0);
        if (FinalRecurrence(action, position) is { } period && !track.RecurrenceBroken)
        {
            var spot = ((int)MathF.Round(position.X), (int)MathF.Round(position.Z * 2));
            if (track.Spots.TryGetValue(spot, out var previous))
            {
                var gap = (now - previous.At).TotalSeconds;
                var cycles = Math.Round(gap / period);
                // A revisit off the verified period means this is not the
                // recorded layout; fall back to the generic pattern learner.
                if (cycles < 1 || Math.Abs(gap - cycles * period) > RecurrenceTolerance) track.RecurrenceBroken = true;
            }
            if (!track.RecurrenceBroken)
            {
                track.Spots[spot] = new(now, position, rotation);
                var recurring = new List<BlundervilleHazard>
                {
                    new($"rhythm/{actor.Id}/{action}/{now.Ticks}", rule.Shape.Resolve(position, rotation, null), now, now.AddSeconds(.3), now)
                };
                foreach (var known in track.Spots.Values)
                {
                    if ((now - known.At).TotalSeconds > 2.5 * period) continue;
                    for (var impact = known.At.AddSeconds(period); impact <= now.AddSeconds(RecurrenceHorizon); impact = impact.AddSeconds(period))
                        if (impact > now)
                            recurring.Add(new($"rhythm/{actor.Id}/{action}/{impact.Ticks}", rule.Shape.Resolve(known.Position, known.Rotation, null),
                                impact.AddSeconds(-RecurrenceLead), impact.AddSeconds(RecurrenceTail), now));
                }
                track.Forecast = recurring;
                return;
            }
        }
        var forecast = new List<BlundervilleHazard>();
        // These repeated effects are contact windows, not single cast completions.
        forecast.Add(new($"rhythm/{actor.Id}/{action}/{now.Ticks}", rule.Shape.Resolve(position, rotation, null), now, now.AddSeconds(.3), now));
        if (action == 34717)
        {
            if (FitAlternating(beats, length) is { } fit)
                foreach (var (impact, beat) in fit.Impacts(now, now.AddSeconds(28)))
                    forecast.Add(new($"rhythm/{actor.Id}/{action}/{impact.Ticks}", rule.Shape.Resolve(beat.Position, beat.Rotation, null),
                        impact.AddSeconds(-.7 - fit.Extra), impact.AddSeconds(1 + fit.Extra), now));
        }
        else if (beats.Count == 2 * length + 1)
        {
            var valid = true;
            for (var i = 1; i <= length; ++i)
            {
                var a = (beats[i].At - beats[i - 1].At).TotalSeconds;
                var b = (beats[i + length].At - beats[i + length - 1].At).TotalSeconds;
                valid &= a > .2 && a < (action == 34717 ? 6.5 : 4.2) && Math.Abs(a - b) < (action == 34717 ? .4 : .22)
                    && Vector3.DistanceSquared(beats[i].Position, beats[i + length].Position) < .04f
                    && (rule.Shape is Spatial.SafeZone.CircleTemplate || MathF.Cos(beats[i].Rotation - beats[i + length].Rotation) > .99f);
            }
            if (valid)
            {
                var impact = now;
                for (var step = 0; step < 20; ++step)
                {
                    var index = length + 1 + step % length;
                    impact = impact.AddSeconds((beats[index].At - beats[index - 1].At).TotalSeconds);
                    // Retain the entire 20.5s traversal horizon even just before
                    // the next endpoint effect (up to 6.5s after this sample).
                    if (impact > now.AddSeconds(action == 34717 ? 28 : 6)) break;
                    var beat = beats[index];
                    // Endpoint packets in the recorded lane drift by up to .65s
                    // while adjacent cycles still pass the matching check.
                    forecast.Add(new($"rhythm/{actor.Id}/{action}/{impact.Ticks}", rule.Shape.Resolve(beat.Position, beat.Rotation, null),
                        impact.AddSeconds(action == 34717 ? -.7 : -.18), impact.AddSeconds(action == 34717 ? 1 : .3), now));
                }
            }
        }
        track.Forecast = forecast;
    }

    // ARR 2026-10-12 01:01: cylinder endpoint pulses arrived up to ~.5s late,
    // so adjacent half cycles differed by .8s while the full cycle stayed ~9.6s.
    // The strict two-cycle comparison rejected nearly every window and the
    // ordinary bridge entrance never opened. Fit the median full cycle over a
    // longer history, anchor each endpoint separately, and widen the window
    // by the measured residual instead of discarding the clock.
    internal const int AlternatingHistory = 9;
    internal sealed record AlternatingFit(double Period, DateTime[] Anchors, Beat[] Poses, double Extra)
    {
        public IEnumerable<(DateTime Impact, Beat Pose)> Impacts(DateTime now, DateTime horizon)
        {
            for (var parity = 0; parity < Anchors.Length; ++parity)
                for (var impact = Anchors[parity].AddSeconds(Period); impact <= horizon; impact = impact.AddSeconds(Period))
                    if (impact > now) yield return (impact, Poses[parity]);
        }
    }

    internal static AlternatingFit? FitAlternating(IReadOnlyList<Beat> beats, int length)
    {
        // Use the longest recent run whose endpoints still alternate; a lost
        // packet breaks the parity and older beats cannot be matched to it.
        var first = Math.Max(0, beats.Count - length);
        while (first > 0 && Vector3.DistanceSquared(beats[first - 1].Position, beats[first - 1 + length].Position) < .04f) --first;
        // Two beats per endpoint already measure one complete cycle each; the
        // cylinder body tracker uses the same two matching cycles.
        if (beats.Count - first < 2 * length) return null;
        var cycles = new List<double>();
        for (var i = first; i + length < beats.Count; ++i)
            cycles.Add((beats[i + length].At - beats[i].At).TotalSeconds);
        for (var i = first + 1; i < beats.Count; ++i)
            if ((beats[i].At - beats[i - 1].At).TotalSeconds is <= .2 or >= 7) return null;
        var period = Median(cycles);
        if (period <= .4 || period >= 13) return null;
        var inliers = cycles.Count(c => Math.Abs(c - period) < .6);
        if (inliers < 2 || inliers * 2 < cycles.Count + 1) return null;
        var anchors = new DateTime[length]; var poses = new Beat[length]; var residual = 0.0;
        for (var parity = 0; parity < length; ++parity)
        {
            var latest = beats.Count - 1 - parity;
            var projected = new List<double>();
            for (var i = latest; i >= first; i -= length)
                projected.Add((beats[i].At - beats[latest].At).TotalSeconds + (latest - i) / length * period);
            var offset = Median(projected);
            residual = Math.Max(residual, projected.Max(p => Math.Abs(p - offset)));
            anchors[parity] = beats[latest].At.AddSeconds(offset);
            poses[parity] = beats[latest];
        }
        if (residual > 1.1) return null;
        return new(period, anchors, poses, Math.Clamp(residual - .65, 0, .5));
    }

    private static IReadOnlyList<BlundervilleHazard> RotatingBurst(ulong source, List<DateTime> starts,
        Spatial.SafeZone.GeometryTemplate shape, Vector3 position, float rotation, DateTime now)
    {
        var geometry = shape.Resolve(position, rotation, null);
        var period = starts.Count < 2 ? 0 : Median(starts.Zip(starts.Skip(1), (a, b) => (b - a).TotalSeconds));
        var count = period is >= 5.7 and <= 6.5 ? 3 : 1;
        var result = new List<BlundervilleHazard>(count);
        var phase = count == 1 ? now : starts.Select((s, i) => s.AddSeconds((starts.Count - 1 - i) * period)).Min();
        for (var i = 0; i < count; ++i)
        {
            var at = phase.AddSeconds(i * period);
            result.Add(new($"rhythm/{source}/34773/{at.Ticks}", geometry, at.AddSeconds(-.35), at.AddSeconds(3.45), now));
        }
        return result;
    }

    private static IReadOnlyList<BlundervilleHazard> StationarySweep(ulong source, List<Beat> beats,
        Spatial.SafeZone.GeometryTemplate template, DateTime now)
    {
        var last = beats[^1]; var geometry = template.Resolve(last.Position, last.Rotation, null);
        var result = new List<BlundervilleHazard> { new($"rhythm/{source}/34799/{now.Ticks}", geometry, now, now.AddSeconds(.3), now) };
        if (beats.Count < 3 || beats.Any(b => Vector3.DistanceSquared(b.Position, last.Position) > .04f
            || MathF.Cos(b.Rotation - last.Rotation) < .99f)) return result;
        var intervals = beats.Zip(beats.Skip(1), (a, b) => (b.At - a.At).TotalSeconds).ToArray();
        if (intervals.Any(t => t is < 2.5 or > 5)) return result;
        var period = Median(intervals);
        if (period is < 3 or > 4.5) return result;
        var phases = beats.Select((b, i) => (b.At - beats[0].At).TotalSeconds - i * period).ToArray();
        var phase = phases.Min();
        // The latest ARR includes a delayed packet followed by a short
        // interval; their combined animation period is unchanged. Fit the
        // phase instead of shifting the next animation to that delayed hit.
        if (phases.Any(p => p - phase > .85)) return result;
        for (var step = 0; step < 2; ++step)
        {
            var at = beats[0].At.AddSeconds(phase + (beats.Count + step) * period);
            result.Add(new($"rhythm/{source}/34799/{at.Ticks}", geometry,
                at.AddSeconds(-.8 - step * .1), at.AddSeconds(1 + step * .1), now));
        }
        return result;
    }

    private static double Median(IEnumerable<double> source)
    {
        var sorted = source.Order().ToArray();
        return (sorted[(sorted.Length - 1) / 2] + sorted[sorted.Length / 2]) / 2;
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        foreach (var track in _tracks.Values)
            foreach (var hazard in track.Forecast)
                if (hazard.Until >= now) yield return hazard;
    }
    public void Remove(ulong source)
    {
        foreach (var key in _tracks.Keys.Where(k => k.Source == source).ToArray()) _tracks.Remove(key);
    }
    public void Reset() => _tracks.Clear();
    public bool Ready(ulong source, uint action, DateTime now) => _tracks.TryGetValue((source, action), out var track)
        && track.Forecast.Count > 1 && track.Beats.Count > 0 && now >= track.Beats[^1].At
        && now - track.Beats[^1].At < TimeSpan.FromSeconds(6.5);

    private static int PatternLength(BlundervilleActor actor, uint action, Vector3 p)
    {
        if (actor.Oid == 16339 && action == 34717 && !actor.Dead && BlundervilleEncounter.Finite(p)) return 2;
        if (actor.Oid != BlundervilleCatalog.Helper || actor.Dead || !BlundervilleEncounter.Finite(p)) return 0;
        return action switch
        {
            34773 when MathF.Abs(p.X) <= 15 && p.Z is > -250 and < -190 => 8,
            34799 when p.X is > -245 and < -155 && p.Z is > 210 and < 280 => 1,
            34716 when MathF.Abs(p.X + 200) > 6 && p.X is > -245 and < -155 && p.Z is > 245 and < 275 => 2,
            34801 when MathF.Abs(p.X) <= 15 && p.Z is > 215 and < 255 => 8,
            34801 when MathF.Abs(p.X) <= 15 && p.Z is > 140 and < 205 => 4,
            34802 when MathF.Abs(p.X) <= 15 && p.Z is > 215 and < 275 => 1,
            34796 or 29795 when MathF.Abs(p.X) <= 15 && p.Z is > 130 and < 180 => 1,
            _ => 0
        };
    }
}
