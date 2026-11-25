using System.Numerics;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> treasureCarpet = [];
    private readonly List<DungeonPendingCast> treasureQuietWorld = [];
    private readonly List<(DungeonPendingCast Cast, bool Risky, bool Danger)> treasureSystematic = [];
    private bool treasureCarpetExpanded;
    internal IReadOnlyList<DungeonPendingCast> TreasureCarpet => treasureCarpet;
    internal IReadOnlyList<DungeonPendingCast> TreasureQuietWorld => treasureQuietWorld;
    internal IReadOnlyList<(DungeonPendingCast Cast, bool Risky, bool Danger)> TreasureSystematic => treasureSystematic;

    private void TreasureFortressStart(DungeonPendingCast cast, bool correction)
    {
        if (Active?.BossOid != NativeTreasureCatalog.MOBILE_FORTRESS) return;
        if (cast.Rule.Action is >= 43739 and <= 43741)
        {
            var index = correction ? treasureSystematic.FindIndex(a => a.Cast.Source == cast.Source
                && a.Cast.StartedAt == cast.StartedAt && a.Cast.Rule.Action == cast.Rule.Action) : -1;
            if (correction && index < 0) return;
            if (index >= 0) treasureSystematic[index] = (cast, treasureSystematic[index].Risky, treasureSystematic[index].Danger);
            else
            {
                treasureSystematic.Add((cast, false, false));
                if (treasureSystematic.Count == 16)
                {
                    var order = treasureSystematic.Select(a => a.Cast).ToList();
                    DungeonAoeOrder.Sort(order, c => c.FinishAt);
                    var states = treasureSystematic.ToDictionary(a => a.Cast);
                    treasureSystematic.Clear();
                    treasureSystematic.AddRange(order.Select(c => states[c]));
                }
            }
            return;
        }
        if (cast.Rule.Action != 43732 && cast.Rule.Action is not (>= 43734 and <= 43737)) return;
        var group = cast.Rule.Action == 43732 ? treasureQuietWorld : treasureCarpet;
        var old = correction ? group.FindIndex(c => c.Source == cast.Source && c.StartedAt == cast.StartedAt
            && c.Rule.Action == cast.Rule.Action) : -1;
        if (correction && old < 0) return;
        if (old >= 0) group[old] = cast;
        else
        {
            group.Add(cast);
            if (cast.Rule.Action != 43732 && group.Count >= 4)
                DungeonAoeOrder.Sort(group, c => c.FinishAt);
        }
    }

    private void TreasureFortressFinish(DungeonPendingCast cast)
    {
        if (Active?.BossOid != NativeTreasureCatalog.MOBILE_FORTRESS) return;
        // SimpleAOEs removes by source before checking the watched action.
        var carpetIndex = treasureCarpet.FindIndex(c => c.Source == cast.Source);
        if (carpetIndex >= 0) treasureCarpet.RemoveAt(carpetIndex);
        var quietIndex = treasureQuietWorld.FindIndex(c => c.Source == cast.Source);
        if (quietIndex >= 0) treasureQuietWorld.RemoveAt(quietIndex);
        if (cast.Rule.Action == 43737) treasureCarpetExpanded = false;
        if (cast.Rule.Action is >= 43739 and <= 43741 && treasureSystematic.Count != 0)
            treasureSystematic.RemoveAt(0);
    }

    private void TreasureFortressUpdate()
    {
        if (treasureCarpet.Count == 8) treasureCarpetExpanded = true;
    }

    private IEnumerable<DungeonAmbientAoe> TreasureFortressAoes()
    {
        var max = treasureCarpetExpanded ? 4 : 2;
        var riskyCount = treasureCarpetExpanded ? 2 : 1;
        for (var i = 0; i < Math.Min(max, treasureCarpet.Count); ++i)
        {
            var cast = treasureCarpet[i];
            yield return new($"treasure/carpet/{cast.Source}", cast.Rule.Name, RayRectangleTemplate.Symmetric(40, 10),
                cast.Location, cast.Rotation, Navigate: i < riskyCount, Activation: cast.FinishAt,
                Color: treasureCarpet.Count > riskyCount && i < riskyCount ? new Vector4(1, 1, 0, 1) : null);
        }
        for (var i = 0; i < Math.Min(4, treasureQuietWorld.Count); ++i)
        {
            var cast = treasureQuietWorld[i];
            if (cast.FinishAt >= treasureQuietWorld[0].FinishAt.AddSeconds(3.5)
                || i > 1 && cast.Rotation == treasureQuietWorld[0].Rotation && cast.Rotation == treasureQuietWorld[1].Rotation)
                break;
            // A partial recording with only one caster still has its known first rectangle.
            yield return new($"treasure/quiet/{cast.Source}", cast.Rule.Name, RayRectangleTemplate.Symmetric(40, 10),
                cast.Location, cast.Rotation, Activation: cast.FinishAt);
        }
        var count = treasureSystematic.Count;
        var visible = count == 16 ? 9 : count;
        for (var i = 0; i < visible; ++i)
        {
            var state = treasureSystematic[i];
            if (state.Cast.FinishAt < treasureSystematic[0].Cast.FinishAt.AddSeconds(1))
            {
                state.Risky = true;
                state.Danger |= count > 7;
                treasureSystematic[i] = state;
            }
            var cast = state.Cast;
            yield return new($"treasure/systematic/{cast.Source}/{cast.StartedAt.Ticks}", cast.Rule.Name,
                RayRectangleTemplate.Symmetric(10, 10), cast.Location, cast.Rotation, Navigate: state.Risky,
                Activation: cast.FinishAt, Color: state.Danger ? new Vector4(1, 1, 0, 1) : null);
        }
    }

    private void ResetTreasureFortress()
    {
        treasureCarpet.Clear();
        treasureQuietWorld.Clear();
        treasureSystematic.Clear();
        treasureCarpetExpanded = false;
    }
}
