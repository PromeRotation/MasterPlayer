using MasterPlayer.Replay;
using PromeRotation.LogSystem;

internal static class ReplayTimelineRegression
{
    public static void Run(Action<string, Action> run)
    {
        run("replay timeline builds spans, truncation and entity names", TimelineBuild);
        run("replay comparison aligns logs and summarizes shared/different actions", Comparison);
    }

    private static void TimelineBuild()
    {
        var recording = MakeRecording(
            (0.0, new LogSystemObjectCreateEvent { SourceId = 100, Name = "Boss", DataId = 1234 }),
            (0.5, new LogSystemBattleStartedEvent()),
            (1.0, new LogSystemCastStartEvent { ActionId = 1001, CastTime = 5f, SourceId = 100 }),
            (3.0, new LogSystemCancelCastEvent { SourceId = 100, ActionId = 1001 }),
            (4.0, new LogSystemCastStartEvent { ActionId = 1002, CastTime = 2f, SourceId = 100 }),
            (5.0, new LogSystemInterruptCastEvent { SourceId = 100, ActionId = 1002 }),
            (6.0, new LogSystemStatusAddEvent { SourceId = 100, TargetId = 200, StatusId = 7, Duration = 30f }),
            (7.0, new LogSystemTetherEvent { SourceId = 100, TargetId = 200, TetherId = 0x55 }));

        var timeline = ReplayTimeline.Build(recording);

        Check(timeline.Duration == 7.0, "Duration must equal the last event time.");
        Check(timeline.EntityNames.TryGetValue(100, out var name) && name == "Boss", "ObjectCreate must name the caster.");

        Check(timeline.Casts.Count == 2, "Two casts must produce two spans.");
        var first = timeline.Casts[0];
        Check(first.Start == 1.0 && first.End == 3.0 && first.Cancelled && !first.Interrupted,
            "A cancel within the cast window must truncate the span.");
        Check(first.SourceName == "Boss", "Cast spans must carry the resolved source name.");
        var second = timeline.Casts[1];
        Check(second.Start == 4.0 && second.End == 5.0 && second.Interrupted && !second.Cancelled,
            "An interrupt within the cast window must truncate the span and flag interruption.");

        Check(timeline.Ticks.Count(t => t.Lane == ReplayLane.Battle) == 1, "Battle lane must hold the battle-start tick.");
        Check(timeline.Ticks.Count(t => t.Lane == ReplayLane.Tether) == 1, "Tether lane must hold the tether tick.");
        Check(timeline.Ticks.Count(t => t.Lane == ReplayLane.Status) == 1, "Status lane must hold the status tick.");
        Check(timeline.Ticks.Count(t => t.Lane == ReplayLane.Cast) == 4, "Cast lane must hold start/cancel/interrupt ticks.");
        Check(timeline.Ticks.Count(t => t.Lane == ReplayLane.Misc) == 1, "ObjectCreate must land in the misc lane.");
        Check(ReplayComparison.FirstBattleTime(timeline) == 0.5, "FirstBattleTime must find the battle-start tick.");
        Check(ReplayComparison.FirstCastTime(timeline) == 1.0, "FirstCastTime must find the first cast start.");
    }

    private static void Comparison()
    {
        var logA = ReplayTimeline.Build(MakeRecording(
            (2.0, new LogSystemCastStartEvent { ActionId = 1001, CastTime = 3f, SourceId = 100 }),
            (10.0, new LogSystemCastStartEvent { ActionId = 1002, CastTime = 2f, SourceId = 100 }),
            (20.0, new LogSystemCastStartEvent { ActionId = 1003, CastTime = 4f, SourceId = 100 })));
        var logB = ReplayTimeline.Build(MakeRecording(
            (2.5, new LogSystemCastStartEvent { ActionId = 1001, CastTime = 3f, SourceId = 100 }),
            (19.0, new LogSystemCastStartEvent { ActionId = 1003, CastTime = 4f, SourceId = 100 }),
            (19.5, new LogSystemCastStartEvent { ActionId = 1003, CastTime = 4f, SourceId = 100 }),
            (30.0, new LogSystemCastStartEvent { ActionId = 1004, CastTime = 5f, SourceId = 100 })));

        var rows = ReplayComparison.Compare([new ComparisonLog("A", logA, 0d), new ComparisonLog("B", logB, 0d)]);

        Check(rows.Count == 4, "The union of action ids must produce four rows.");
        Check(rows.Select(r => r.ActionId).SequenceEqual(new uint[] { 1001, 1002, 1003, 1004 }),
            "Rows must sort by earliest aligned first use.");

        var shared = rows[0];
        Check(shared.ActionId == 1001 && shared.PresentInAll(2) && shared.LogsWithAction == 2,
            "The opener must be present in both logs.");
        Check(shared.Counts.SequenceEqual(new[] { 1, 1 }) &&
              shared.FirstTimesAligned[0] == 2.0 && shared.FirstTimesAligned[1] == 2.5,
            "Counts and first-use times must be tracked per log.");
        Check(Math.Abs(shared.FirstTimeSpread - 0.5) < 1e-9, "Spread must be the first-use difference across logs.");

        Check(rows[1].ActionId == 1002 && !rows[1].PresentInAll(2) && rows[1].FirstTimesAligned[1] == null &&
              rows[1].FirstTimeSpread == 0d, "A log-specific action must be flagged partial with a null first-use and no spread.");

        var twice = rows[2];
        Check(twice.ActionId == 1003 && twice.Counts[1] == 2 && twice.FirstTimesAligned[1] == 19.0,
            "Repeated casts in one log must count separately and keep the earliest first use.");

        Check(rows[3].ActionId == 1004 && !rows[3].PresentInAll(2) && rows[3].FirstTimesAligned[0] == null,
            "The B-only finisher must be flagged partial with a null first-use in A.");

        var aligned = ReplayComparison.Compare([new ComparisonLog("A", logA, 0d), new ComparisonLog("B", logB, -0.5)]);
        Check(Math.Abs(aligned[0].FirstTimeSpread) < 1e-9 && aligned[0].FirstTimesAligned[1] == 2.0,
            "Applying a log offset must re-align first-use times.");
    }

    private static ReplayRecording MakeRecording(params (double T, LogSystemEvent Event)[] events)
        => new(new ReplayHeader(1, 100, DateTime.UnixEpoch, "test"),
            events.Select(e => new ReplayEventEntry(e.T, e.Event)).ToArray(), 0);

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
