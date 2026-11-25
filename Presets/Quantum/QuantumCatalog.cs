namespace MasterPlayer.Mechanics.Dungeons;

internal static class QuantumCatalog
{
    internal const uint TERRITORY = 1311;
    internal static readonly uint[] ACTIONS = [45117, 44098, 45200, 44118, 44122, 44129, 44134, 44132, 44130];
    internal static DungeonDefinition[] Create()
        => [new(0x48EE, "卓异的悲寂深想战", _ => QuantumGeometry.Floor,
            ACTIONS.Select(a => new DungeonCastRule(a, 0, $"Action {a}", null)).ToArray(), [])];
}
