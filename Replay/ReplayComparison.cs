namespace MasterPlayer.Replay;

/// <summary>参与对比的一份日志：标签 + 时间轴 + 对齐偏移（秒，正值 = 整体后移）。</summary>
public sealed record ComparisonLog(string Label, ReplayTimeline Timeline, double Offset);

/// <summary>一个技能（读条 ActionId）在多份日志间的对比行。</summary>
public sealed record ActionComparisonRow(
    uint ActionId,
    int LogsWithAction,
    int[] Counts,
    double?[] FirstTimesAligned,
    double FirstTimeSpread)
{
    /// <summary>是否所有日志都出现过该技能。</summary>
    public bool PresentInAll(int logCount) => LogsWithAction == logCount;
}

/// <summary>
/// 多日志对比：按读条 ActionId 聚合各日志的出现次数与首次时间（应用对齐偏移），
/// 供回放窗口归纳「共同技能 / 差异技能 / 时间轴偏移」。纯数据，不依赖游戏服务。
/// </summary>
public static class ReplayComparison
{
    /// <summary>按 ActionId 聚合；行按最早首用（对齐后）升序。</summary>
    public static IReadOnlyList<ActionComparisonRow> Compare(IReadOnlyList<ComparisonLog> logs)
    {
        var rows = new Dictionary<uint, (int[] Counts, double?[] FirstTimes)>();
        for (var i = 0; i < logs.Count; i++)
        {
            var log = logs[i];
            foreach (var cast in log.Timeline.Casts)
            {
                if (!rows.TryGetValue(cast.ActionId, out var acc))
                {
                    acc = (new int[logs.Count], new double?[logs.Count]);
                    rows[cast.ActionId] = acc;
                }

                acc.Counts[i]++;
                var aligned = cast.Start + log.Offset;
                if (acc.FirstTimes[i] == null || aligned < acc.FirstTimes[i]!.Value)
                    acc.FirstTimes[i] = aligned;
            }
        }

        return rows
            .Select(pair =>
            {
                var present = pair.Value.FirstTimes.Where(t => t.HasValue).Select(t => t!.Value).ToArray();
                var spread = present.Length >= 2 ? present.Max() - present.Min() : 0d;
                return new ActionComparisonRow(
                    pair.Key,
                    pair.Value.Counts.Count(c => c > 0),
                    pair.Value.Counts,
                    pair.Value.FirstTimes,
                    spread);
            })
            .OrderBy(row => row.FirstTimesAligned.Where(t => t.HasValue).Select(t => t!.Value).DefaultIfEmpty(double.MaxValue).Min())
            .ToArray();
    }

    /// <summary>首个读条时刻；无读条返回 0。</summary>
    public static double FirstCastTime(ReplayTimeline timeline)
        => timeline.Casts.Count == 0 ? 0d : timeline.Casts.Min(c => c.Start);

    /// <summary>首个 BattleStarted 时刻；无则 0。</summary>
    public static double FirstBattleTime(ReplayTimeline timeline)
    {
        var mark = timeline.Ticks.FirstOrDefault(t => t.Lane == ReplayLane.Battle && t.Kind == "BattleStarted");
        return mark?.T ?? 0d;
    }
}
