using BossMod;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Scripting;

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
    public static IReadOnlyList<MechanicTimelineRow> BuildRows(ScriptTimeline snapshot, int maxNext = 3)
    {
        var rows = new List<MechanicTimelineRow>();
        var nextCount = 0;
        for (var i = 0; i < snapshot.Entries.Count; ++i)
        {
            var entry = snapshot.Entries[i];
            if (entry.Name.Length == 0) continue;
            var isCurrent = i == 0;
            if (!isCurrent && nextCount >= maxNext) break;
            var seconds = Math.Max(0f, (float)(entry.ExpectedEnd - snapshot.CapturedAt).TotalSeconds);
            rows.Add(new(entry.Name, seconds, ToneOf(entry.Hints), isCurrent, false));
            if (!isCurrent) ++nextCount;
        }
        return rows;
    }

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
        => ToneOf((ScriptTimelineHint)(int)hint);

    internal static MechanicTimelineTone ToneOf(ScriptTimelineHint hint)
    {
        if (hint.HasFlag(ScriptTimelineHint.Tankbuster))
            return MechanicTimelineTone.Tankbuster;
        if (hint.HasFlag(ScriptTimelineHint.Raidwide))
            return MechanicTimelineTone.Raidwide;
        if (hint.HasFlag(ScriptTimelineHint.Knockback))
            return MechanicTimelineTone.Knockback;
        if (hint.HasFlag(ScriptTimelineHint.BossCastStart) || hint.HasFlag(ScriptTimelineHint.BossCastEnd))
            return MechanicTimelineTone.Cast;
        if (hint.HasFlag(ScriptTimelineHint.PositioningStart) || hint.HasFlag(ScriptTimelineHint.VulnerableStart)
            || hint.HasFlag(ScriptTimelineHint.DowntimeStart))
            return MechanicTimelineTone.Attention;
        return MechanicTimelineTone.Neutral;
    }
}
