using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class SourceIntegrity
{
    private sealed record Adaptation(string Reason, string OriginalSha256, string ImportedSha256);

    public static int Validate(string repository, string originalRepository)
    {
        var source = Path.Combine(originalRepository, "BossMod");
        var imported = Path.Combine(repository, "BmrRuntime/Upstream");
        var changes = JsonSerializer.Deserialize<Dictionary<string, Adaptation>>(
            File.ReadAllText(Path.Combine(repository, "BmrRuntime/SourceChanges.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Missing upstream adaptation manifest.");
        var actualChanges = new HashSet<string>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(source, p).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj")
                && p != "Framework/Plugin.cs" && !p.StartsWith("Modules/Dawntrail/Foray/Crucible/")
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
