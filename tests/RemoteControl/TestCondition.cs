using System.Reflection;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;

namespace MasterPlayer.RemoteControlTests;

public class TestCondition : DispatchProxy
{
    public static ICondition DutyReplay() => Create<ICondition, TestCondition>();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => method!.Name == "get_Item" && (ConditionFlag)args![0]! == ConditionFlag.DutyRecorderPlayback;
}
