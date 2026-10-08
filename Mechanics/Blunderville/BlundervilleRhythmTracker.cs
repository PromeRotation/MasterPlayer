using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

// No-cast devices: learn two complete, matching animation cycles from effects.
// Each forecast retains its own pose and absolute impact time, never a moving "now" window.
internal sealed class BlundervilleRhythmTracker
{
    private sealed record Beat(DateTime At, Vector3 Position, float Rotation);
    private sealed class Track
    {
        public readonly List<Beat> Beats = [];
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
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
            if (elapsed < 0 || elapsed > 8) { beats.Clear(); track.Forecast = []; }
        }
        beats.Add(new(now, position, rotation));
        if (beats.Count > 2 * length + 1) beats.RemoveAt(0);
        var forecast = new List<BlundervilleHazard>();
        // These repeated effects are contact windows, not single cast completions.
        forecast.Add(new($"rhythm/{actor.Id}/{action}/{now.Ticks}", rule.Shape.Resolve(position, rotation, null), now, now.AddSeconds(.3), now));
        if (beats.Count == 2 * length + 1)
        {
            var valid = true;
            for (var i = 1; i <= length; ++i)
            {
                var a = (beats[i].At - beats[i - 1].At).TotalSeconds;
                var b = (beats[i + length].At - beats[i + length - 1].At).TotalSeconds;
                valid &= a > .2 && a < (action == 34717 ? 6.5 : 4.2) && Math.Abs(a - b) < .22
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
                    // Retain the entire 10.7s traversal horizon even just before
                    // the next endpoint effect (up to 6.5s after this sample).
                    if (impact > now.AddSeconds(action == 34717 ? 18 : 6)) break;
                    var beat = beats[index];
                    forecast.Add(new($"rhythm/{actor.Id}/{action}/{impact.Ticks}", rule.Shape.Resolve(beat.Position, beat.Rotation, null),
                        impact.AddSeconds(-.18), impact.AddSeconds(.3), now));
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
            34775 when MathF.Abs(p.X) <= 12 && p.Z is > -335 and < -275 => 2,
            34799 when p.X is > -245 and < -155 && p.Z is > 210 and < 280 => 1,
            34716 when MathF.Abs(p.X + 200) > 6 && p.X is > -245 and < -155 && p.Z is > 245 and < 275 => 2,
            34801 when MathF.Abs(p.X) <= 15 && p.Z is > 215 and < 255 => 8,
            34801 when MathF.Abs(p.X) <= 15 && p.Z is > 140 and < 205 => 4,
            34802 when MathF.Abs(p.X) <= 15 && p.Z is > 215 and < 275 => 1,
            _ => 0
        };
    }
}
