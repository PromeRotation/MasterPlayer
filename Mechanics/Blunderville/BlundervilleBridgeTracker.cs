namespace MasterPlayer.Mechanics.Blunderville;

internal sealed class BlundervilleBridgeTracker
{
    private const uint InitialPresent = 0x00010001;
    private const uint Present = 0x00020001;
    private const uint Absent = 0x00080004;
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
            if (bridge.PendingPresentDuration == null) bridge.Cycles.Clear();
        }
        else if (state == Present && bridge.State == Absent && bridge.PendingPresentDuration is { } duration)
        {
            var absentDuration = (now - bridge.ChangedAt).TotalSeconds;
            if (absentDuration is >= 0.2 and <= 5)
            {
                bridge.Cycles.Enqueue((duration, absentDuration));
                while (bridge.Cycles.Count > 5) bridge.Cycles.Dequeue();
            }
            else bridge.Cycles.Clear();
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
        if (bridge.State == InitialPresent || !Timing(bridge, out var present, out _)) return true;
        return AddSeconds(bridge.ChangedAt, present) is { } deadline && now <= deadline;
    }

    public DateTime? BridgeNextDisappearAt(byte index, DateTime now)
    {
        if (!_bridges.TryGetValue(index, out var bridge) || !Fresh(bridge, now)
            || bridge.State != Present || !Timing(bridge, out var present, out _)) return null;
        var deadline = AddSeconds(bridge.ChangedAt, present);
        return deadline is { } value && (now - value).TotalSeconds <= 0.3 ? deadline : null;
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
        var periods = bridge.Cycles.Select(c => c.Present + c.Absent).Order().ToArray();
        var presentTimes = bridge.Cycles.Select(c => c.Present).Order().ToArray();
        var absentTimes = bridge.Cycles.Select(c => c.Absent).Order().ToArray();
        // ARR periods are about six seconds. Fit measured cycles, and reject missing transitions or jittered samples.
        if (periods[^1] - periods[0] > 0.4 || presentTimes[^1] - presentTimes[0] > 0.4 || absentTimes[^1] - absentTimes[0] > 0.4) return false;
        present = Median(presentTimes);
        absent = Median(absentTimes);
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

        public void Clear()
        {
            State = 0;
            ChangedAt = default;
            LastPacketAt = default;
            PendingPresentDuration = null;
            Cycles.Clear();
        }
    }
}
