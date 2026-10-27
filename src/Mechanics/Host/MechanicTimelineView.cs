using BossMod;
using MasterPlayer.MechanicRuntime;

namespace MasterPlayer.MechanicHost;

internal enum MechanicTimelineTone
{
    Neutral,
    Cast,
    Tankbuster,
    Raidwide,
    Knockback,
    Attention,
}

internal sealed record MechanicTimelineRow(string Name, float Seconds, MechanicTimelineTone Tone, bool IsCurrent, bool IsBranch);

internal static class MechanicTimelineView
{
    public static IReadOnlyList<MechanicTimelineRow> BuildRows(MechanicTimelineSnapshot snapshot, DateTime now, int maxNext = 3)
    {
        var rows = new List<MechanicTimelineRow>();
        var entries = snapshot.Entries;
        var nextCount = 0;
        for (var i = 0; i < entries.Count; ++i)
        {
            var entry = entries[i];
            if (entry.Name.Length == 0)
                continue;
            var isCurrent = i == 0;
            if (!isCurrent && nextCount >= maxNext)
                break;
            var seconds = Math.Max(0f, (float)(entry.ExpectedEnd - now).TotalSeconds);
            rows.Add(new(entry.Name, seconds, ToneOf(entry.Hints), isCurrent, false));
            if (!isCurrent)
                ++nextCount;
        }
        if (snapshot.Branches.Count != 0)
            rows.Add(new("分支?", 0f, MechanicTimelineTone.Attention, false, true));
        return rows;
    }

    internal static MechanicTimelineTone ToneOf(StateMachine.StateHint hint)
    {
        if (hint.HasFlag(StateMachine.StateHint.Tankbuster))
            return MechanicTimelineTone.Tankbuster;
        if (hint.HasFlag(StateMachine.StateHint.Raidwide))
            return MechanicTimelineTone.Raidwide;
        if (hint.HasFlag(StateMachine.StateHint.Knockback))
            return MechanicTimelineTone.Knockback;
        if (hint.HasFlag(StateMachine.StateHint.BossCastStart) || hint.HasFlag(StateMachine.StateHint.BossCastEnd))
            return MechanicTimelineTone.Cast;
        if (hint.HasFlag(StateMachine.StateHint.PositioningStart) || hint.HasFlag(StateMachine.StateHint.VulnerableStart)
            || hint.HasFlag(StateMachine.StateHint.DowntimeStart))
            return MechanicTimelineTone.Attention;
        return MechanicTimelineTone.Neutral;
    }
}
