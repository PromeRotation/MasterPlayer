using System.Reflection;
using BossMod;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace MasterPlayer.MechanicHost;

internal static class MechanicTerritoryCatalog
{
    public static IReadOnlyList<MechanicTerritoryEntry> Build()
    {
        var counts = new Dictionary<uint, int>();
        var cfc = Svc.Data.GetExcelSheet<ContentFinderCondition>();
        void Add(uint territory)
        {
            if (territory != 0) counts[territory] = counts.GetValueOrDefault(territory) + 1;
        }
        foreach (var info in BossModuleRegistry.RegisteredModules.Values)
            if (info.GroupType is BossModuleInfo.GroupType.CFC or BossModuleInfo.GroupType.MaskedCarnivale
                or BossModuleInfo.GroupType.RemovedUnreal or BossModuleInfo.GroupType.BaldesionArsenal
                or BossModuleInfo.GroupType.CastrumLacusLitore or BossModuleInfo.GroupType.TheDalriada
                or BossModuleInfo.GroupType.TheForkedTowerBlood or BossModuleInfo.GroupType.TheForkedTowerMagic
                or BossModuleInfo.GroupType.ForayFATE or BossModuleInfo.GroupType.CriticalEngagement
                or BossModuleInfo.GroupType.BozjaDuel or BossModuleInfo.GroupType.EurekaNM)
                Add(cfc?.GetRowOrDefault(info.GroupID)?.TerritoryType.RowId ?? 0);
        foreach (var type in typeof(ZoneModule).Assembly.GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(ZoneModule))) continue;
            var info = type.GetCustomAttribute<ZoneModuleInfoAttribute>();
            if (info != null) Add(info.TerritoryID != 0 ? info.TerritoryID : cfc?.GetRowOrDefault(info.CFCID)?.TerritoryType.RowId ?? 0);
        }
        return counts.Select(pair => new MechanicTerritoryEntry(pair.Key, ResolveName(pair.Key), pair.Value))
            .OrderBy(entry => entry.Name).ToArray();
    }

    public static string ResolveName(uint territory)
    {
        var row = Svc.Data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory);
        var name = row?.ContentFinderCondition.ValueNullable?.Name.ToString();
        if (string.IsNullOrWhiteSpace(name)) name = row?.PlaceName.ValueNullable?.Name.ToString();
        return string.IsNullOrWhiteSpace(name) ? $"区域 {territory}" : name;
    }
}
