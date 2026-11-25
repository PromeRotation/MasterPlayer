namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class QuantumTimeline
{
    // -1 preparing, 0 selecting quantum level, 1..10 Q40 conditions,
    // 11 source's permanent "???", 12 source's unsupported level, 13 finished.
    internal int Stage { get; private set; } = -1;
    internal DateTime EnteredAt { get; private set; }
    internal uint SelectedLevel { get; private set; }
    internal readonly List<int> Overdue = [];
    private static readonly float[] EXPECTED = [12.1f, 8f, 13.1f, 4f, 1.6f, 1.4f, 1.2f, 5.1f, 6.9f, 7.8f];

    internal void Begin(DateTime now)
    {
        Stage = 0;
        EnteredAt = now;
    }

    internal void Advance(DateTime now, uint level, Func<int, bool> condition, Action<int> enter, Action<int> exit)
    {
        if (Stage == 0)
        {
            if (level < 15) return;
            SelectedLevel = level;
            Stage = level >= 40 ? 1 : 12;
            EnteredAt = now;
            enter(Stage);
        }
        while (Stage is >= 1 and <= 10)
        {
            if (!condition(Stage))
            {
                if ((now - EnteredAt).TotalSeconds < EXPECTED[Stage - 1] + 1f) return;
                Overdue.Add(Stage);
            }
            exit(Stage);
            ++Stage;
            EnteredAt = now;
            enter(Stage);
        }
    }

    internal void Finish(Action<int> exit)
    {
        exit(Stage);
        Stage = 13;
    }
}
