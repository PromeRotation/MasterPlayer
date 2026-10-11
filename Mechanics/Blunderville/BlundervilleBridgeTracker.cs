namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleBridgeTracker
{
    private const uint InitialPresent = 0x00010001;
    private const uint Present = 0x00020001;
    private const uint Absent = 0x00080004;
    internal const int MaximumCycles = 7;
    private readonly Dictionary<byte, Bridge> _bridges = [];
    private DateTime _lastTime;

    public void Map(byte index, uint state, DateTime now)
    {
        if (index is < 14 or > 17) return;
        if (_lastTime != default && now < _lastTime) Reset();
        _lastTime = now;
        if (!_bridges.TryGetValue(index, out var bridge)) _bridges[index] = bridge = new();
        if (state == InitialPresent)
        {
            bridge.Clear();
            bridge.State = state;
            bridge.ChangedAt = now;
            bridge.LastPacketAt = now;
            return;
        }
        if (state is not (Present or Absent))
        {
            bridge.Clear();
            bridge.LastPacketAt = now;
            return;
        }
        if (state == bridge.State)
        {
            bridge.LastPacketAt = now;
            return;
        }
        if (state == Absent && bridge.State == Present)
        {
            var duration = (now - bridge.ChangedAt).TotalSeconds;
            bridge.PendingPresentDuration = duration is >= 0.5 and <= 10 ? duration : null;
            if (bridge.PendingPresentDuration == null) { bridge.Cycles.Clear(); bridge.Disappears.Clear(); }
            bridge.Disappears.Add(now);
            if (bridge.Disappears.Count > MaximumCycles + 1) bridge.Disappears.RemoveAt(0);
        }
        else if (state == Present && bridge.State == Absent && bridge.PendingPresentDuration is { } duration)
        {
            var absentDuration = (now - bridge.ChangedAt).TotalSeconds;
            if (absentDuration is >= 0.2 and <= 5)
            {
                bridge.Cycles.Enqueue((duration, absentDuration));
                while (bridge.Cycles.Count > MaximumCycles) bridge.Cycles.Dequeue();
            }
            else { bridge.Cycles.Clear(); bridge.Disappears.Clear(); }
            bridge.PendingPresentDuration = null;
        }
        else if (bridge.State != InitialPresent) bridge.PendingPresentDuration = null;
        bridge.State = state;
        bridge.ChangedAt = now;
        bridge.LastPacketAt = now;
    }

    public bool BridgeEnabled(byte index, DateTime now)
    {
        if (!_bridges.TryGetValue(index, out var bridge) || !Fresh(bridge, now)
            || bridge.State is not (InitialPresent or Present)) return false;
        if (bridge.State == InitialPresent || !Disappear(bridge, out var deadline)) return true;
        return deadline is { } value && now <= value;
    }

    public DateTime? BridgeNextDisappearAt(byte index, DateTime now)
    {
        if (!_bridges.TryGetValue(index, out var bridge) || !Fresh(bridge, now)
            || bridge.State != Present || !Disappear(bridge, out var deadline)) return null;
        return deadline is { } value && (now - value).TotalSeconds <= 0.3 ? deadline : null;
    }

    // MapEffect packets are only ever delayed. A late appearance shortens the
    // next present interval (01:01 ARR: absent 2.03s then present 4.19s), and a
    // late collapse makes the next one look early. The collapses keep a fixed
    // cycle, so anchor that cycle to the least delayed recent collapse and use
    // the earlier of both estimates. Either estimate alone needs two complete
    // measured cycles; 01:01 had four consecutive late appearances whose
    // present/absent pairs never agreed while the collapses stayed periodic.
    // Across 242 recorded collapses (01:01, 00:17, 04:45 ARRs) the earlier
    // estimate minus this margin was never later than the actual packet.
    internal const double CollapseMargin = .25;
    // Returns whether any measured clock applies; an overflowed deadline is
    // known but unusable, so the bridge is not treated as indefinitely safe.
    private static bool Disappear(Bridge bridge, out DateTime? deadline)
    {
        var known = Timing(bridge, out var present, out _);
        deadline = known ? AddSeconds(bridge.ChangedAt, present - CollapseMargin) : null;
        var collapses = bridge.Disappears;
        if (collapses.Count < 3 || collapses[^1] >= bridge.ChangedAt) return known;
        var all = collapses.Zip(collapses.Skip(1), (a, b) => (b - a).TotalSeconds).ToArray();
        var intervals = all.Where(i => i is >= 5.2 and <= 6.9).Order().ToArray();
        if (intervals.Length < 2 || intervals.Length * 2 < all.Length + 1) return known;
        var period = Median(intervals);
        // The latest collapse must belong to the current cycle; a 3.12s late
        // appearance (01:01 ARR) still does.
        var latest = collapses[^1];
        if ((bridge.ChangedAt - latest).TotalSeconds >= period) return known;
        var anchor = collapses.Min(t => t.AddSeconds(Math.Round((latest - t).TotalSeconds / period) * period));
        var cycle = AddSeconds(anchor, period - CollapseMargin);
        deadline = cycle == null || known && deadline == null ? null : deadline is { } appear && appear < cycle ? appear : cycle;
        return true;
    }

    public DateTime? BridgeNextAppearAt(byte index, DateTime now)
    {
        if (!_bridges.TryGetValue(index, out var bridge) || !Fresh(bridge, now)
            || bridge.State != Absent || !Timing(bridge, out _, out var absent)) return null;
        var deadline = AddSeconds(bridge.ChangedAt, absent);
        return deadline is { } value && (now - value).TotalSeconds <= 0.3 ? deadline : null;
    }

    public void Reset()
    {
        _bridges.Clear();
        _lastTime = default;
    }

    private bool Fresh(Bridge bridge, DateTime now)
        => now >= bridge.LastPacketAt && now >= _lastTime && (now - bridge.LastPacketAt).TotalSeconds <= 12;

    private static bool Timing(Bridge bridge, out double present, out double absent)
    {
        present = 0;
        absent = 0;
        if (bridge.Cycles.Count < 2) return false;
        // ARR periods are about six seconds. A single late MapEffect packet
        // (01:01 ARR: absent 2.03–3.12s) must not discard the whole window;
        // fit the agreeing majority and reject only inconsistent evidence.
        var medianPresent = Median(bridge.Cycles.Select(c => c.Present).Order().ToArray());
        var medianAbsent = Median(bridge.Cycles.Select(c => c.Absent).Order().ToArray());
        var inliers = bridge.Cycles.Where(c => Math.Abs(c.Present - medianPresent) <= 0.3 && Math.Abs(c.Absent - medianAbsent) <= 0.3).ToArray();
        if (inliers.Length < 2 || inliers.Length * 2 < bridge.Cycles.Count + 1) return false;
        present = Median(inliers.Select(c => c.Present).Order().ToArray());
        absent = Median(inliers.Select(c => c.Absent).Order().ToArray());
        return true;
    }

    private static double Median(double[] values)
        => values.Length % 2 == 0 ? (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2 : values[values.Length / 2];

    private static DateTime? AddSeconds(DateTime time, double seconds)
    {
        if (time.Ticks > DateTime.MaxValue.Ticks - seconds * TimeSpan.TicksPerSecond) return null;
        return time.AddSeconds(seconds);
    }

    private sealed class Bridge
    {
        public uint State;
        public DateTime ChangedAt;
        public DateTime LastPacketAt;
        public double? PendingPresentDuration;
        public readonly Queue<(double Present, double Absent)> Cycles = [];
        public readonly List<DateTime> Disappears = [];

        public void Clear()
        {
            State = 0;
            ChangedAt = default;
            LastPacketAt = default;
            PendingPresentDuration = null;
            Cycles.Clear();
            Disappears.Clear();
        }
    }
}
