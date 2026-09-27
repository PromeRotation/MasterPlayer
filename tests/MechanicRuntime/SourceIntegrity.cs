using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class SourceIntegrity
{
    private sealed record Adaptation(string Reason, string OriginalSha256, string ImportedSha256);

    // 不纳入导入的上游文件：原插件入口、斗兽棋，以及 2026-09-27 移除的寻路引擎与 AI/移动执行控制
    // （MP 使用自有 SafeZoneReborn 解算 + PR GreenMove 移动，这些上游文件无构造者）
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.Ordinal)
    {
        "Framework/Plugin.cs",
        "Pathfinding/Map.cs",
        "Pathfinding/ThetaStar.cs",
        "Pathfinding/NavigationDecision.cs",
        "Pathfinding/MapVisualizer.cs",
        "AI/AIBehaviour.cs",
        "AI/AIController.cs",
        "AI/AIManager.cs",
        "AI/AIManagementWindow.cs",
        "AI/AIRotationModule.cs",
        "AI/Broadcast.cs",
        "Framework/ActionManagerEx.cs",
        "Framework/MovementOverride.cs",
        "Framework/IPCProvider.cs",
        "Framework/DTRProvider.cs",
        "Framework/MultiboxManager.cs",
        "Framework/WorldStateGameSync.cs",
        "BossModule/AIHintsVisualizer.cs",
        "Autorotation/UIRotationWindow.cs",
        "Modules/Dawntrail/Ultimate/FRU/FRUAI.cs",
        "Modules/Dawntrail/Savage/M02SHoneyBLovely/AI/AIExperiment.cs",
        "Modules/Dawntrail/Savage/M04SWickedThunder/AI/AIExperiment.cs",
    };
    private static readonly string[] ExcludedPrefixes = ["Modules/Dawntrail/Foray/Crucible/", "Debug/", "Autorotation/MiscAI/"];

    public static int Validate(string repository, string originalRepository)
    {
        var source = Path.Combine(originalRepository, "BossMod");
        var imported = Path.Combine(repository, "MechanicRuntime/Upstream");
        var changes = JsonSerializer.Deserialize<Dictionary<string, Adaptation>>(
            File.ReadAllText(Path.Combine(repository, "MechanicRuntime/SourceChanges.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Missing upstream adaptation manifest.");
        var actualChanges = new HashSet<string>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(source, p).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj")
                && !ExcludedFiles.Contains(p) && !ExcludedPrefixes.Any(p.StartsWith)
                && (p.EndsWith(".cs") || p.StartsWith("Pathfinding/ObstacleMaps/")))
            .Order().ToArray();
        var importedFiles = Directory.EnumerateFiles(imported, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(imported, p).Replace('\\', '/')).Order().ToArray();
        if (!files.SequenceEqual(importedFiles))
            throw new InvalidOperationException("Imported source/resource paths differ from the complete upstream set.");
        foreach (var file in files)
        {
            var original = Path.Combine(source, file);
            var copy = Path.Combine(imported, file);
            var originalBytes = file.EndsWith(".cs")
                ? Encoding.UTF8.GetBytes(File.ReadAllText(original).Replace("\r\n", "\n"))
                : File.ReadAllBytes(original);
            var importedBytes = file.EndsWith(".cs")
                ? Encoding.UTF8.GetBytes(File.ReadAllText(copy).Replace("\r\n", "\n"))
                : File.ReadAllBytes(copy);
            var equal = originalBytes.AsSpan().SequenceEqual(importedBytes);
            if (!equal) actualChanges.Add(file);
            if (changes.TryGetValue(file, out var change)
                && (string.IsNullOrWhiteSpace(change.Reason)
                    || !string.Equals(Convert.ToHexString(SHA256.HashData(originalBytes)), change.OriginalSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Convert.ToHexString(SHA256.HashData(importedBytes)), change.ImportedSha256, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Upstream adaptation does not match its reviewed source and result: {file}");
        }
        if (!actualChanges.SetEquals(changes.Keys))
            throw new InvalidOperationException("Undocumented upstream adaptations: "
                + string.Join(", ", actualChanges.Except(changes.Keys)) + "; stale adaptation entries: "
                + string.Join(", ", changes.Keys.Except(actualChanges)));
        return files.Length;
    }
}
