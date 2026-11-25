using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private DungeonPendingCast? treasureBitterPrediction;
    private bool treasureBitterDonut;
    private readonly Dictionary<(ulong Source, DateTime StartedAt), float> treasureBitterLeapTimes = [];
    internal DungeonPendingCast? TreasureBitterPrediction => treasureBitterPrediction;

    private void TreasureBitterStart(DungeonPendingCast cast, bool correction)
    {
        if (Active?.BossOid != NativeTreasureCatalog.OLD_BITTER_EYE) return;
        if (cast.Rule.Action == 43683)
        {
            var observed = _actors.GetValueOrDefault(cast.Source)?.Cast;
            var total = observed?.TotalTime ?? (float)((cast.FinishAt - cast.StartedAt).TotalSeconds - .3d);
            treasureBitterLeapTimes[(cast.Source, cast.StartedAt)] = Math.Max(0, total);
            return;
        }
        if (cast.Rule.Action is not (43684 or 43680)) return;
        if (correction && (treasureBitterPrediction?.Source != cast.Source
            || treasureBitterPrediction.StartedAt != cast.StartedAt)) return;
        treasureBitterPrediction = cast with { FinishAt = cast.FinishAt.AddSeconds(cast.Rule.Action == 43684 ? 4.6 : 0) };
        treasureBitterDonut = cast.Rule.Action == 43680;
    }

    private void TreasureBitterFinish(DungeonPendingCast cast)
    {
        if (Active?.BossOid != NativeTreasureCatalog.OLD_BITTER_EYE) return;
        if (cast.Rule.Action is 43676 or 43680)
            treasureBitterPrediction = null;
        else if (cast.Rule.Action == 43683)
        {
            var total = treasureBitterLeapTimes.GetValueOrDefault((cast.Source, cast.StartedAt));
            treasureBitterLeapTimes.Remove((cast.Source, cast.StartedAt));
            if (cast.Location.X == 99.99231f && cast.Location.Z == 99.99231f) return;
            // Action 43683 has ExtraCastTime100ms=0. BMR caps elapsed at TotalTime,
            // then CastFinishAt adds the old NPCRemainingTime, including float arithmetic.
            var elapsed = Math.Min((float)(_now - cast.StartedAt).TotalSeconds, total);
            var remaining = total + .3f - elapsed;
            treasureBitterPrediction = cast with { FinishAt = _now.AddSeconds(remaining + 7.1d) };
            treasureBitterDonut = true;
        }
    }

    private IEnumerable<DungeonAmbientAoe> TreasureBitterAoes()
    {
        if (treasureBitterPrediction is not { } cast) yield break;
        yield return new("treasure/bitter/prediction", "Hundred-stone Swing / Eye of the Thunderstorm",
            treasureBitterDonut ? new DonutTemplate(5, 40) : new CircleTemplate(13),
            cast.Location, Activation: cast.FinishAt);
    }
}
