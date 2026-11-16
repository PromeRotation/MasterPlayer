using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace MasterPlayer;

internal static class DutyReplayState
{
    public static bool Active => Svc.Condition?[ConditionFlag.DutyRecorderPlayback] == true;
}
