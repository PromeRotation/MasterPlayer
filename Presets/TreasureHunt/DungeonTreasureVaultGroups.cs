using System.Numerics;

namespace MasterPlayer.Mechanics.Dungeons;

internal sealed partial class DungeonEncounter
{
    private readonly List<DungeonPendingCast> treasureVaultGroups = [];
    private readonly List<DungeonPendingCast> treasureHighVoltage = [];
    internal IReadOnlyList<DungeonPendingCast> TreasureVaultGroups => treasureVaultGroups;
    internal IReadOnlyList<DungeonPendingCast> TreasureHighVoltage => treasureHighVoltage;

    private void TreasureVaultGroupStart(DungeonPendingCast cast, bool correction)
    {
        if (!IsNativeTreasure || !NativeTreasureCatalog.VaultGroupAction(cast.Rule.Action)) return;
        var group = cast.Rule.Action is >= 43778 and <= 43781 ? treasureHighVoltage : treasureVaultGroups;
        var index = correction ? group.FindIndex(c => c.Source == cast.Source
            && c.Rule.Action == cast.Rule.Action && c.StartedAt == cast.StartedAt) : -1;
        if (correction && index < 0) return;
        if (index >= 0) group[index] = cast;
        else
        {
            group.Add(cast);
            var expected = cast.Rule.Action switch
            {
                43687 or 43688 or 43706 or 43707 or 43773 or 43774 => 8,
                >= 43778 and <= 43781 => 4,
                >= 22797 and <= 22800 => 99,
                _ => 12,
            };
            if (cast.Rule.Action != 43645 && group.Count >= expected)
                DungeonAoeOrder.Sort(group, c => c.FinishAt);
        }
    }

    private void TreasureVaultGroupFinish(DungeonPendingCast cast)
    {
        var index = treasureVaultGroups.FindIndex(c => c.Source == cast.Source);
        if (index >= 0) treasureVaultGroups.RemoveAt(index);
        var highIndex = treasureHighVoltage.FindIndex(c => c.Source == cast.Source);
        if (highIndex >= 0) treasureHighVoltage.RemoveAt(highIndex);
    }

    private IEnumerable<DungeonAmbientAoe> TreasureVaultGroupAoes()
    {
        var fastitocalon = IsNativeTreasure && Active?.BossOid == NativeTreasureCatalog.FASTITOCALON;
        var count = fastitocalon ? treasureVaultGroups.Count : Math.Min(4, treasureVaultGroups.Count);
        for (var i = 0; i < count; ++i)
        {
            var cast = treasureVaultGroups[i];
            var risky = !fastitocalon || cast.FinishAt.AddSeconds(-3) <= _now;
            yield return new($"treasure/vault-group/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                cast.Rotation, Navigate: risky, Activation: cast.FinishAt,
                Color: fastitocalon && treasureVaultGroups.Count > 4 && i < 4 ? new Vector4(1, 1, 0, 1) : null);
        }
        for (var i = 0; i < Math.Min(3, treasureHighVoltage.Count); ++i)
        {
            var cast = treasureHighVoltage[i];
            yield return new($"treasure/high-voltage/{cast.Source}", cast.Rule.Name, cast.Rule.Shape!, cast.Location,
                cast.Rotation, Activation: cast.FinishAt,
                Color: treasureHighVoltage.Count > 1 && i == 0 ? new Vector4(1, 1, 0, 1) : null);
        }
    }
}
