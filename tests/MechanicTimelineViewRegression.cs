using BossMod;
using MasterPlayer.MechanicHost;
using MasterPlayer.MechanicRuntime;

internal static class MechanicTimelineViewRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("mechanic timeline rows clamp remaining time and mark the current state", () =>
        {
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var snapshot = new MechanicTimelineSnapshot(0, "P1", 2f,
                [new(1, "当前读条", t0.AddSeconds(5), StateMachine.StateHint.BossCastEnd),
                 new(2, "下一机制", t0.AddSeconds(15), StateMachine.StateHint.None)], []);
            var rows = MechanicTimelineView.BuildRows(snapshot, t0);
            Check(rows.Count == 2 && rows[0].IsCurrent && !rows[1].IsCurrent, "current state not marked");
            Check(Math.Abs(rows[0].Seconds - 5f) < 0.01f && Math.Abs(rows[1].Seconds - 15f) < 0.01f, "relative seconds wrong");
            var late = MechanicTimelineView.BuildRows(snapshot, t0.AddSeconds(30));
            Check(late[0].Seconds == 0f, "overdue state must clamp to zero, not go negative");
        });

        run("mechanic timeline tones prioritize damage hints over cast markers", () =>
        {
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var snapshot = new MechanicTimelineSnapshot(0, "", 0f,
                [new(1, "坦克死刑", t0.AddSeconds(5), StateMachine.StateHint.Tankbuster | StateMachine.StateHint.BossCastEnd),
                 new(2, "全屏", t0.AddSeconds(10), StateMachine.StateHint.Raidwide),
                 new(3, "击退", t0.AddSeconds(15), StateMachine.StateHint.Knockback),
                 new(4, "读条", t0.AddSeconds(20), StateMachine.StateHint.BossCastStart)], []);
            var rows = MechanicTimelineView.BuildRows(snapshot, t0, 4);
            Check(rows[0].Tone == MechanicTimelineTone.Tankbuster, "tankbuster must win over cast markers");
            Check(rows[1].Tone == MechanicTimelineTone.Raidwide && rows[2].Tone == MechanicTimelineTone.Knockback, "damage tones wrong");
            Check(rows[3].Tone == MechanicTimelineTone.Cast, "plain cast must be its own tone");
        });

        run("mechanic timeline hides unnamed states, caps upcoming rows and keeps branches", () =>
        {
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var entries = Enumerable.Range(0, 8)
                .Select(i => new MechanicTimelineEntry((uint)i, i == 1 ? "" : $"状态{i}", t0.AddSeconds(i * 10), StateMachine.StateHint.None))
                .ToArray();
            var snapshot = new MechanicTimelineSnapshot(0, "P2", 0f, entries, [100, 101]);
            var rows = MechanicTimelineView.BuildRows(snapshot, t0, 3);
            Check(rows.Count(r => !r.IsBranch) == 4, "hidden state leaked or upcoming cap wrong");
            Check(rows[^1].IsBranch && rows[^1].Name == "分支?", "unresolved branch must be shown without guessing");
            Check(rows.Count(r => r.IsBranch) == 1, "branch row must appear exactly once");
            var noBranch = MechanicTimelineView.BuildRows(snapshot with { Branches = [] }, t0);
            Check(!noBranch.Any(r => r.IsBranch), "branch row appeared without branches");
            var empty = MechanicTimelineView.BuildRows(snapshot with { Entries = [], Branches = [] }, t0);
            Check(empty.Count == 0, "empty entries produced rows");
        });
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
