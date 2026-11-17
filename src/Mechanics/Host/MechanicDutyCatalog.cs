using System.Reflection;
using BossMod;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace MasterPlayer.MechanicHost;

public enum DutySupport { None, Partial, Full }

public sealed record DutyModuleEntry(string Name, BossModuleInfo.Maturity Maturity);

public sealed record DutyEntry(
    uint CfcId, uint TerritoryId, string Name,
    uint TypeId, string TypeName, uint Icon,
    uint Expansion, int Level, bool HighEnd, uint SortKey,
    IReadOnlyList<DutyModuleEntry> Modules, bool HasZoneModule)
{
    public int VerifiedCount { get; } = Modules.Count(m => m.Maturity >= BossModuleInfo.Maturity.Verified);
}

/// <summary>
/// Every duty in the duty finder, with the mechanic modules that cover it.
/// Support level is derived, never hand-written: see <see cref="Evaluate"/>.
/// </summary>
internal static class MechanicDutyCatalog
{
    private const uint DungeonType = 2;
    private const int DungeonBossCount = 3;

    public static IReadOnlyList<DutyEntry> Build()
    {
        var cfc = Svc.Data.GetExcelSheet<ContentFinderCondition>();
        if (cfc == null) return [];
        var names = Svc.Data.GetExcelSheet<BNpcName>();

        var modules = new Dictionary<uint, List<(int Order, DutyModuleEntry Entry)>>();
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
        {
            if (info.GroupType is not (BossModuleInfo.GroupType.CFC or BossModuleInfo.GroupType.MaskedCarnivale
                or BossModuleInfo.GroupType.BaldesionArsenal or BossModuleInfo.GroupType.CastrumLacusLitore
                or BossModuleInfo.GroupType.TheDalriada or BossModuleInfo.GroupType.TheForkedTowerBlood
                or BossModuleInfo.GroupType.TheForkedTowerMagic or BossModuleInfo.GroupType.CriticalEngagement
                or BossModuleInfo.GroupType.BozjaDuel or BossModuleInfo.GroupType.EurekaNM) || info.GroupID == 0)
                continue;
            // CE / duel / NM modules use NameID for another sheet; fall back to the module type name there.
            var name = info.GroupType is BossModuleInfo.GroupType.CriticalEngagement or BossModuleInfo.GroupType.BozjaDuel or BossModuleInfo.GroupType.EurekaNM
                ? "" : names?.GetRowOrDefault(info.NameID)?.Singular.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) name = info.ModuleType.Name;
            if (!modules.TryGetValue(info.GroupID, out var list)) modules[info.GroupID] = list = [];
            list.Add((info.SortOrder, new(name, info.Maturity)));
        }

        var zoneCfc = new HashSet<uint>();
        var zoneTerritory = new HashSet<uint>();
        foreach (var type in typeof(ZoneModule).Assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(ZoneModule))) continue;
            var info = type.GetCustomAttribute<ZoneModuleInfoAttribute>();
            if (info == null) continue;
            if (info.TerritoryID != 0) zoneTerritory.Add(info.TerritoryID);
            if (info.CFCID != 0) zoneCfc.Add(info.CFCID);
        }

        var result = new List<DutyEntry>();
        foreach (var row in cfc)
        {
            var name = row.Name.ToString();
            var territory = row.TerritoryType.RowId;
            var type = row.ContentType.ValueNullable;
            if (string.IsNullOrWhiteSpace(name) || territory == 0 || type == null || row.ContentType.RowId == 0) continue;
            var list = modules.TryGetValue(row.RowId, out var found)
                ? found.OrderBy(m => m.Order).Select(m => m.Entry).ToArray() : [];
            result.Add(new DutyEntry(row.RowId, territory, name,
                row.ContentType.RowId, type.Value.Name.ToString(), type.Value.Icon,
                row.TerritoryType.ValueNullable?.ExVersion.RowId ?? 0, row.ClassJobLevelRequired, row.HighEndDuty, row.SortKey,
                list, zoneCfc.Contains(row.RowId) || zoneTerritory.Contains(territory)));
        }
        return result.OrderBy(e => e.TypeId).ThenBy(e => e.SortKey).ThenBy(e => e.CfcId).ToArray();
    }

    /// <summary>
    /// Full: a dedicated script, or every boss has a verified module.
    /// Partial: some coverage (unverified modules, missing bosses, or zone module only).
    /// </summary>
    public static (DutySupport Level, string Reason) Evaluate(DutyEntry entry, string? scriptName)
    {
        if (scriptName != null) return (DutySupport.Full, $"专属脚本「{scriptName}」");
        var total = entry.Modules.Count;
        if (total == 0)
            return entry.HasZoneModule ? (DutySupport.Partial, "只有区域模块，没有首领模块") : (DutySupport.None, "暂无机制模块");
        if (entry.VerifiedCount < total)
            return (DutySupport.Partial, $"{total} 个首领模块中 {total - entry.VerifiedCount} 个未验证");
        if (entry.TypeId == DungeonType && total < DungeonBossCount)
            return (DutySupport.Partial, $"只覆盖 {total} 个首领");
        return (DutySupport.Full, $"{total} 个首领模块均已验证");
    }
}
