namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private bool GarudaExNeedsProvoke(DungeonActor plume)
    {
        if (!IsGarudaExtreme || !GarudaExAi || plume.Oid != 0x600 || plume.TargetId == PlayerId
            || _actors.GetValueOrDefault(PlayerId) is not { } player || !ThornTank(player)
            || _actors.GetValueOrDefault(plume.TargetId) is not { } tank || !HasStatus(tank, 379)) return false;
        var stacks = statusChanges.TryGetValue((tank.Id, 379), out var change) && _now < change.ChangedAt.AddSeconds(.5)
            ? change.Param : tank.StatusParams?.GetValueOrDefault(379u) ?? 0;
        return stacks >= 2;
    }

    private int GarudaExPriority(DungeonActor actor)
    {
        if (!GarudaExAi) return 0;
        return actor.Oid switch
        {
            0xF5 => 1,
            0x620 => 2,
            0x621 => CurrentAssignment == "MT" ? 0 : 3,
            0xF4 => CurrentAssignment == "MT" ? 0 : 4,
            0x601 => CurrentAssignment == "MT" ? 0 : 5,
            // Source PriorityPointless is sticky: its later priority=6 during a
            // tank swap does not override -1 while Garuda is targetable.
            0x600 => _actors.GetValueOrDefault(PrimaryId) is { Targetable: true } ? -1 : 6,
            _ => 0,
        };
    }

}
