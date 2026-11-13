namespace MasterPlayer.Combat.Sequences;

internal sealed class SkillSequenceRunner
{
    private SkillSequenceProfile? profile;
    private DateTime stepStarted;
    private DateTime? completionDue;
    private bool confirmed;

    public int StepIndex { get; private set; }
    public bool Running => profile != null;
    public SkillSequenceEntry? Current => profile != null && StepIndex < profile.Entries.Count ? profile.Entries[StepIndex] : null;
    public string? CompletionCommand { get; private set; }
    public string Status { get; private set; } = "待机";

    public void Start(SkillSequenceProfile selected, DateTime now)
    {
        Stop();
        profile = SkillSequenceExchange.Clone(selected);
        stepStarted = now;
        Status = "等待技能";
    }

    public void Stop()
    {
        profile = null;
        StepIndex = 0;
        completionDue = null;
        CompletionCommand = null;
        confirmed = false;
        Status = "待机";
    }

    public void Confirm() => confirmed = true;

    public SkillSequenceEntry? Tick(DateTime now, IReadOnlySet<uint> statuses)
    {
        if (profile == null) return null;
        if (completionDue is { } due)
        {
            if (now < due) return null;
            CompletionCommand = profile.CompletionCommand;
            completionDue = null;
            if (!profile.LoopExecution)
            {
                profile = null;
                Status = "已完成";
                return null;
            }
            StepIndex = 0;
            stepStarted = now;
            Status = "循环执行";
            // Let the host send the completion command before starting the next round.
            return null;
        }
        if (confirmed)
        {
            confirmed = false;
            ++StepIndex;
            stepStarted = now;
        }
        while (StepIndex < profile.Entries.Count)
        {
            var entry = profile.Entries[StepIndex];
            if (!entry.Enabled || entry.SkillId == 0
                || entry.SkipIfHasStatusId != 0 && statuses.Contains(entry.SkipIfHasStatusId)
                || entry.RequireStatusId != 0 && !statuses.Contains(entry.RequireStatusId))
            {
                ++StepIndex;
                stepStarted = now;
                continue;
            }
            Status = $"第 {StepIndex + 1}/{profile.Entries.Count} 步：{entry.SkillId}";
            return entry;
        }
        completionDue = now.AddMilliseconds(Math.Clamp(profile.CompletionCommandDelayMs, 0, 3000));
        Status = "等待完成命令";
        return null;
    }

    public void Skip(DateTime now, string reason)
    {
        ++StepIndex;
        stepStarted = now;
        confirmed = false;
        Status = reason;
    }

    public double StepSeconds(DateTime now) => (now - stepStarted).TotalSeconds;

    public string? TakeCompletionCommand()
    {
        var command = CompletionCommand;
        CompletionCommand = null;
        return command;
    }
}
