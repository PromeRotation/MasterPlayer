using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Mechanics.Dungeons;

internal static class NoviceTacticalCatalog
{
    internal static bool IsTerritory(uint territory) => territory is 1127 or 1128 or 1129;
    internal static bool NativeEffect(uint action) => action is 40678 or 40680 or 40692 or 40682 or 40695 or 40683
        or 40696 or 40685 or 40698 or 40686 or 40699 or 40689 or 40690 or 40702 or 40703 or 40694 or 40693 or 40760 or 40759;
    internal static DungeonCastRule[] Rules()
    {
        var rules = new List<DungeonCastRule>
        {
            new(40697, 0, "Fireflood", new CircleTemplate(15)),
            new(40700, 0, "Blazing Ring", new DonutTemplate(8, 25)),
            new(40710, 0, "Windage", new CircleTemplate(5)),
            new(40713, 0, "Furious Flare", new CircleTemplate(18)),
            new(40730, 0, "Furious Flare II", new CircleTemplate(20)),
            new(40723, 0, "Water Drop", new CircleTemplate(10)),
            new(40725, 0, "Upwell", new CircleTemplate(5)),
            new(40747, 0, "Glaciate", new CircleTemplate(6)),
            new(40763, 0, "Fireflood", new CircleTemplate(15)),
            new(40761, 0, "Withdraw", null, "Raidwide"),
        };
        foreach (var action in new uint[] { 40689, 40702, 40694, 40693, 40760, 40759, 40706, 40722, 40709, 40726,
            40716, 40727, 40732, 40733, 40752, 40753, 40743, 40757 })
            rules.Add(new(action, 0, $"Tactical {action}", null));
        return [.. rules];
    }
}
