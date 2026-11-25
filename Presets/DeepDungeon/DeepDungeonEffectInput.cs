namespace MasterPlayer.Mechanics.DeepDungeon;

// EO persistent effects use only source/action. Legacy expanded targets retain
// their common packet timestamp; nonzero global sequences also identify the
// corresponding complete packet across the two capture paths.
internal sealed class DeepDungeonEffectInput
{
    private readonly HashSet<(ulong Source, uint Action, uint Sequence, DateTime Time)> legacy = [];
    private readonly Dictionary<(ulong Source, uint Action, uint Sequence, DateTime ZeroTime), int> unmatched = [];
    private readonly Dictionary<(ulong Source, uint Action), (DateTime Time, bool Native)> zeroStreams = [];
    internal bool AmbiguousZeroStreams { get; private set; }

    internal bool Native(ulong source, uint action, uint sequence, DateTime time)
        => Accept(source, action, sequence, time, true);

    internal bool Legacy(ulong source, uint action, uint sequence, DateTime time)
    {
        time = time.ToUniversalTime();
        return legacy.Add((source, action, sequence, time)) && Accept(source, action, sequence, time, false);
    }

    private bool Accept(ulong source, uint action, uint sequence, DateTime time, bool native)
    {
        var key = (source, action, sequence, sequence == 0 ? time.ToUniversalTime() : default);
        if (sequence == 0)
        {
            if (zeroStreams.TryGetValue((source, action), out var previous)
                && previous.Native != native && previous.Time != key.Item4)
                AmbiguousZeroStreams = true;
            zeroStreams[(source, action)] = (key.Item4, native);
        }
        var count = unmatched.GetValueOrDefault(key);
        var paired = native ? count < 0 : count > 0;
        count += native ? 1 : -1;
        if (count == 0) unmatched.Remove(key);
        else unmatched[key] = count;
        return !paired;
    }

    internal void Reset()
    {
        legacy.Clear();
        unmatched.Clear();
        zeroStreams.Clear();
        AmbiguousZeroStreams = false;
    }
}
