using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed record NoviceTank04Plan(IReadOnlyList<DungeonActor> Dummies, Vector3 Center, uint Action,
    bool RequestsAction)
{
    internal float GoalRadius => 1;
    internal float GoalWeight => 1;
    internal ulong? ActionTarget => null;
    internal float ActionPriority => 1;
    internal bool ClearsActionIntents => RequestsAction;
}
