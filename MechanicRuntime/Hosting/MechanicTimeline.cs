using BossMod;

namespace MasterPlayer.MechanicRuntime;

public sealed record MechanicTimelineEntry(uint StateId, string Name, DateTime ExpectedEnd, StateMachine.StateHint Hints);
public sealed record MechanicTimelineSnapshot(int PhaseIndex, string PhaseName, float ElapsedInState,
    IReadOnlyList<MechanicTimelineEntry> Entries, IReadOnlyList<uint> Branches);

public static class MechanicTimeline
{
    public static MechanicTimelineSnapshot Capture(BossModule module, int maxStates = 16)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStates);
        var machine = module.StateMachine;
        if (machine.ActiveState == null)
            return new(machine.ActivePhaseIndex, machine.ActivePhase?.Name ?? "", 0, [], []);
        var entries = new List<MechanicTimelineEntry>();
        var visited = new HashSet<uint>();
        var state = machine.ActiveState;
        var expectedEnd = module.WorldState.CurrentTime.AddSeconds(-machine.TimeSinceTransitionClamped);
        uint[] branches = [];
        while (state != null && entries.Count < maxStates && visited.Add(state.ID))
        {
            expectedEnd = expectedEnd.AddSeconds(state.Duration);
            entries.Add(new(state.ID, state.Name, expectedEnd, state.EndHint));
            if (state.NextStates is { Length: > 1 } next)
            {
                branches = Array.ConvertAll(next, s => s.ID);
                break;
            }
            state = state.NextStates is { Length: 1 } single ? single[0] : null;
        }
        return new(machine.ActivePhaseIndex, machine.ActivePhase?.Name ?? "", machine.TimeSinceTransition, entries, branches);
    }
}
