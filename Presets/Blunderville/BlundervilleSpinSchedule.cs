using System.Numerics;

namespace MasterPlayer.Mechanics.Blunderville;

internal readonly record struct BlundervilleSpinImpact(DateTime At, float X, float Z);

// FirstB spin bridge blades (34775): rows z -281/-301/-321 fire 3x3m boxes at
// x 0.5/3.5/6.5, rows -288/-308/-328 at x -0.5/-3.5/-6.5. A row sweeps its
// three boxes 0.2s and 0.15s apart, alternately outward and inward, so each
// box fires twice per ~4.28s cycle. 18 ARRs (2026-10-07..13): every box and
// sweep direction repeats with one period per round (4.277–4.299s, residual
// sd 0.02s), but the row phases differ between rounds, so the clock is learned
// from the effects. A knockback needs the player's centre inside the box
// 0.05s before the effect packet (32 of MP's 33 recorded knockbacks).
internal sealed class BlundervilleSpinSchedule
{
    internal const double Lead = .05, NominalPeriod = 4.28;
    internal const float Half = 1.5f;
    private static readonly float[] Rows = [-281, -288, -301, -308, -321, -328];
    private const int Keep = 8;
    private readonly Dictionary<(int X, int Z, int Direction), List<DateTime>> _series = [];
    private double _period = NominalPeriod;

    public double Period => _period;

    public void Effect(Vector3 position, float rotation, DateTime at)
    {
        if (!Blade(position) || !float.IsFinite(rotation)) return;
        var key = ((int)MathF.Round(position.X * 2), (int)MathF.Round(position.Z), rotation > 0 ? 1 : -1);
        if (!_series.TryGetValue(key, out var times)) _series[key] = times = [];
        // One effect per sweep; repeats and late packets of an old sweep add nothing.
        if (times.Count > 0 && (at - times[^1]).TotalSeconds < 1) return;
        times.Add(at);
        if (times.Count > Keep) times.RemoveAt(0);
        _period = EstimatePeriod();
    }

    // All 36 box/direction clocks seen at least twice, the latest within two
    // cycles: a stalled clock cannot be projected across the bridge.
    public bool Ready(DateTime now)
        => _series.Count == 36 && _series.Values.All(t => t.Count >= 2 && (now - t[^1]).TotalSeconds < 2 * _period);

    // Knockback instants (effect packet minus Lead) in [from, until].
    public List<BlundervilleSpinImpact> Impacts(DateTime from, DateTime until)
    {
        var result = new List<BlundervilleSpinImpact>();
        foreach (var ((x, z, _), times) in _series)
        {
            var anchor = Anchor(times);
            var first = Math.Ceiling((from - anchor).TotalSeconds / _period);
            for (var m = Math.Max(0, first - 1); ; ++m)
            {
                var at = anchor.AddSeconds(m * _period - Lead);
                if (at > until) break;
                if (at >= from) result.Add(new(at, x / 2f, z));
            }
        }
        result.Sort((a, b) => a.At.CompareTo(b.At));
        return result;
    }

    public void Reset() { _series.Clear(); _period = NominalPeriod; }

    internal static bool Blade(Vector3 p)
        => MathF.Abs(p.X) < 8 && MathF.Abs(p.Y - 10) < .5f && Rows.Any(z => MathF.Abs(p.Z - z) < .05f);

    // The latest sweep, re-estimated as the median of the kept sweeps projected
    // onto it: one late packet does not move the clock.
    private DateTime Anchor(List<DateTime> times)
    {
        var last = times[^1];
        var projected = times.Select(t => (t - last).TotalSeconds - Math.Round((t - last).TotalSeconds / _period) * _period)
            .OrderBy(v => v).ToArray();
        return last.AddSeconds(projected[projected.Length / 2]);
    }

    private double EstimatePeriod()
    {
        var samples = new List<double>();
        foreach (var times in _series.Values)
            for (var i = 1; i < times.Count; ++i)
            {
                var delta = (times[i] - times[i - 1]).TotalSeconds;
                var cycles = Math.Round(delta / NominalPeriod);
                if (cycles is >= 1 and <= 3 && Math.Abs(delta / cycles - NominalPeriod) < .1) samples.Add(delta / cycles);
            }
        if (samples.Count < 6) return NominalPeriod;
        samples.Sort();
        return samples[samples.Count / 2];
    }
}
