using MasterPlayer.Scripting;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed class ThordanTimelineStep(uint id, float expected, Func<bool> ready, bool timeout)
{
    internal uint Id { get; } = id;
    internal float Expected { get; } = expected;
    internal Func<bool> Ready { get; } = ready;
    internal bool Timeout { get; } = timeout;
    internal string Name { get; set; } = "";
    internal string Comment { get; set; } = "";
    internal ScriptTimelineHint Hints { get; set; }
    internal uint ExpectedAction { get; set; }
    internal ThordanComponent? ConditionComponent { get; set; }
    internal float Overdue { get; set; } = 1;
    internal List<(ThordanComponent Component, bool Activate)> Enter { get; } = [];
    internal List<(ThordanComponent Component, bool Activate)> Exit { get; } = [];

    internal ThordanTimelineStep AddEnter(params ThordanComponent[] components)
    {
        foreach (var component in components) Enter.Add((component, true));
        return this;
    }

    internal ThordanTimelineStep AddExit(params ThordanComponent[] components)
    {
        foreach (var component in components) Exit.Add((component, false));
        return this;
    }

    internal ThordanTimelineStep ActivateOnExit(ThordanComponent component)
    {
        Exit.Add((component, true));
        return this;
    }
}
