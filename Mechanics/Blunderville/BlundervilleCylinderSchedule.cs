using System.Numerics;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Blunderville;

// 2026-10-13 route research and the 17:44/01:01/04:45 obstacle fixtures: every
// FirstA cylinder (16339) shuttles 4m between its lane's two endpoints on one
// fixed half cycle — 4.772–4.785s on the bridge rows (z -166..-178), 5.791–
// 5.803s on the ice rows (z -188..-203) — casting 34717 on each arrival (the
// R3 pulse lands ~0.98s later), leaving 2.06s/3.09s after the cast and
// crossing at 2.0m/s (fitted over 766 half cycles). The trackers learn two full cycles (~19s) first;
// MP waited 8–13s at the entrance for them. With the half cycle known, one
// cast anchors a cylinder.
internal sealed class BlundervilleCylinderSchedule
{
    private sealed record Kind(double Half, double Depart, double Travel);
    private static readonly Kind Bridge = new(4.778, 2.06, 2.01), Ice = new(5.797, 3.09, 2.02);
    private static readonly float[] BridgeEnds = [-213, -209, -202, -198, -191, -187], IceEnds = [-208, -204, -196, -192];
    internal const double PulseDelay = .98, Horizon = 22, Slice = .25;
    internal const float Radius = 1.4f;

    private sealed class Track
    {
        public readonly List<(DateTime At, Vector3 Position)> Casts = [];
        public Kind? Kind;
        public Vector3 First, Second;
        public DateTime Anchor;
        public IReadOnlyList<BlundervilleHazard> Forecast = [];
        public DateTime ForecastAt;
        // A slice keeps its shape until the next cast: the bumper planner
        // discards a search whenever its inputs change.
        public readonly Dictionary<DateTime, BlundervilleHazard> Slices = [];
    }
    private readonly Dictionary<ulong, Track> _tracks = [];

    private static Kind? KindOf(Vector3 p) => p.Z is > -180 and < -164 ? Bridge : p.Z is > -205 and < -186 ? Ice : null;

    public void Cast(ulong source, Vector3 position, DateTime at)
    {
        if (KindOf(position) is not { } kind || !BlundervilleEncounter.Finite(position)) return;
        var ends = kind == Bridge ? BridgeEnds : IceEnds;
        var index = Array.FindIndex(ends, x => MathF.Abs(x - position.X) < .6f);
        if (index < 0) return;
        if (!_tracks.TryGetValue(source, out var track)) _tracks[source] = track = new();
        if (track.Casts.Count > 0 && (at - track.Casts[^1].At).TotalSeconds is < 1 and > -1) return;
        if (track.Casts.Count > 0 && (at < track.Casts[^1].At || MathF.Abs(track.Casts[^1].Position.Z - position.Z) > .5f)) track.Casts.Clear();
        track.Casts.Add((at, position));
        if (track.Casts.Count > 5) track.Casts.RemoveAt(0);
        Vector3 first = position with { X = ends[index] }, second = position with { X = ends[index ^ 1] };
        // Packets only arrive late (01:01: a cast 1.14s late, together with its
        // pulse), so the earliest of the recent casts projected onto the latest
        // slot on the known half cycle is the clock.
        var latest = track.Casts[^1].At;
        var anchor = latest.AddSeconds(track.Casts.Min(c =>
            (c.At - latest).TotalSeconds + Math.Round((latest - c.At).TotalSeconds / kind.Half) * kind.Half));
        // Like the learned sweeps, each endpoint refreshes the slices: near
        // slices then shed the growth they were published with far ahead.
        track.Slices.Clear();
        track.Kind = kind; track.First = first; track.Second = second;
        track.Anchor = anchor;
        track.ForecastAt = default;
    }

    // A clock without a cast for 2.5 half cycles has lost its packets (or the
    // device left the scan): fall back to the learned trackers.
    public bool Ready(ulong id, DateTime now) => _tracks.TryGetValue(id, out var track) && Fresh(track, now);
    private static bool Fresh(Track track, DateTime now) => track.Kind is { } kind && track.Casts.Count > 0
        && now >= track.Casts[^1].At.AddSeconds(-1) && (now - track.Casts[^1].At).TotalSeconds <= 2.5 * kind.Half;

    public void Remove(ulong id) => _tracks.Remove(id);
    public void Reset() => _tracks.Clear();

    // Position at a time, from the anchored cast at the first endpoint.
    private static Vector3 Position(Kind kind, DateTime anchor, Vector3 first, Vector3 second, DateTime at)
    {
        var elapsed = (at - anchor).TotalSeconds;
        var n = Math.Floor(elapsed / kind.Half);
        var phase = elapsed - n * kind.Half;
        var from = (long)n % 2 == 0 ? first : second;
        var to = (long)n % 2 == 0 ? second : first;
        if (phase < kind.Depart) return from;
        if (phase >= kind.Depart + kind.Travel) return to;
        return Vector3.Lerp(from, to, (float)((phase - kind.Depart) / kind.Travel));
    }

    public IEnumerable<BlundervilleHazard> Hazards(DateTime now)
    {
        const long slice = TimeSpan.TicksPerSecond / 4;
        var start = new DateTime(now.Ticks / slice * slice, now.Kind);
        foreach (var (id, track) in _tracks)
        {
            if (track.Kind is not { } kind || !Fresh(track, now)) continue;
            if (track.ForecastAt != start)
            {
                track.ForecastAt = start;
                var forecast = new List<BlundervilleHazard>();
                foreach (var expired in track.Slices.Keys.Where(t => t < start).ToArray()) track.Slices.Remove(expired);
                for (var i = 0; i * Slice < Horizon; ++i)
                {
                    var from = start.AddSeconds(i * Slice);
                    if (track.Slices.TryGetValue(from, out var existing)) { forecast.Add(existing); continue; }
                    // The learned sweeps' growth: plans executed further ahead
                    // (ice slides) carry more timing error than the clock.
                    var uncertainty = Math.Min(.7, .2 + Math.Max(0, (from - now).TotalSeconds) * .025);
                    var min = Position(kind, track.Anchor, track.First, track.Second, from.AddSeconds(-uncertainty)); var max = min;
                    for (var sample = 1; sample <= 9; ++sample)
                    {
                        var p = Position(kind, track.Anchor, track.First, track.Second,
                            from.AddSeconds(-uncertainty + sample * (Slice + 2 * uncertainty) / 9));
                        min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                    }
                    // Same identity and window as the learned cylinder sweeps.
                    var body = new BlundervilleHazard($"bumper/{id}/{from.Ticks}", new BlundervilleBumperSweep(min, max, Radius),
                        from.AddSeconds(-.1), from.AddSeconds(.35), track.Anchor);
                    track.Slices[from] = body; forecast.Add(body);
                }
                var cycles = Math.Floor((start - track.Anchor).TotalSeconds / kind.Half) - 1;
                for (var k = cycles; (track.Anchor.AddSeconds(k * kind.Half) - start).TotalSeconds < Horizon; ++k)
                {
                    var impact = track.Anchor.AddSeconds(k * kind.Half + PulseDelay);
                    if (impact.AddSeconds(1) < now) continue;
                    var endpoint = (long)k % 2 == 0 ? track.First : track.Second;
                    forecast.Add(new($"rhythm/{id}/34717/{impact.Ticks}", new CircleGeometry(endpoint, 3),
                        impact.AddSeconds(-.7), impact.AddSeconds(1), track.Anchor));
                }
                track.Forecast = forecast;
            }
            foreach (var hazard in track.Forecast)
                if (hazard.Until >= now) yield return hazard;
        }
    }
}
