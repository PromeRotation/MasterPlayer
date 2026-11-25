namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private TreasurePlayerGaze? treasurePlayerGaze;
    internal TreasurePlayerGaze? TreasurePlayerGaze => treasurePlayerGaze;
    private static bool TreasurePlayerGazeAction(uint action) => action is 21760 or 21762;

    private void TreasurePlayerGazeStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsNativeTreasure || Active?.BossOid != NativeTreasureCatalog.DAEN_ECHIDNA
            || !TreasurePlayerGazeAction(cast.Rule.Action)) return;
        var previous = treasurePlayerGaze;
        if (correction && (previous == null || previous.Cast.Source != cast.Source
            || previous.Cast.Rule.Action != cast.Rule.Action || previous.Cast.StartedAt != cast.StartedAt)) return;
        var sameTarget = correction && previous!.Cast.TargetId == cast.TargetId;
        var target = sameTarget ? previous!.Target : _actors.GetValueOrDefault(cast.TargetId);
        treasurePlayerGaze = new(cast, target, sameTarget && previous!.Removed);
    }

    private void TreasurePlayerGazeObserve()
    {
        if (treasurePlayerGaze is not { Target: { } target, Removed: false } gaze) return;
        if (_actors.GetValueOrDefault(target.Id) is { } current && current.Oid == target.Oid)
            treasurePlayerGaze = gaze with { Target = current };
        else
            treasurePlayerGaze = gaze with { Removed = true };
    }

    private void TreasurePlayerGazeRemoved(ulong id)
    {
        if (treasurePlayerGaze?.Target?.Id == id)
            treasurePlayerGaze = treasurePlayerGaze with { Removed = true };
    }

    private void TreasurePlayerGazeFinish(DungeonPendingCast cast)
    {
        // Either action's Finish clears the component-wide slot, regardless of the latest source.
        if (TreasurePlayerGazeAction(cast.Rule.Action)) treasurePlayerGaze = null;
    }

    private IEnumerable<DungeonPendingCast> TreasurePlayerGazes()
    {
        if (treasurePlayerGaze is { Target: { } target } gaze)
            yield return gaze.Cast with { Location = target.Position };
    }
}
