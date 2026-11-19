namespace MasterPlayer.Mechanics.PilgrimsTraverse;

internal static class PtBossCatalog
{
    private static readonly (int Floor, uint Oid, uint Territory)[] Bosses =
    [
        (10, 0x460D, 1281), (20, 0x485C, 1282), (30, 0x4609, 1283), (40, 0x460B, 1284), (50, 0x4872, 1285),
        (60, 0x49DE, 1286), (70, 0x484C, 1287), (80, 0x485D, 1288), (90, 0x48F5, 1289), (99, 0x48EA, 1290)
    ];
    internal static bool Owns(uint oid, uint territory) => Bosses.Any(b => b.Oid == oid && b.Territory == territory);
    internal static bool IsScript(string id) => Bosses.Any(b => string.Equals(id, $"promeradar.pt{b.Floor}", StringComparison.OrdinalIgnoreCase));
}
