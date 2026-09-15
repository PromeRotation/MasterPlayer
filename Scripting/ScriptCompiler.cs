using System.Collections.Immutable;
using System.Numerics;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using PromeRotation.Scripting;

namespace MasterPlayer.Scripting;

/// <summary>
/// 外部 .cs 动态编译（Roslyn）。
/// 注入 global using 语法树，脚本作者无需写 using；引用按类型定位 DLL（缓存）。
/// 编译结果按源码内容缓存，重复加载免编译。
/// </summary>
internal static class ScriptCompiler
{
    private static volatile IReadOnlyList<MetadataReference>? _references;
    private static readonly object LockObj = new();

    // 按源码哈希缓存编译产物（进程级）
    private static readonly Dictionary<string, byte[]> CompileCache = new(StringComparer.Ordinal);
    private static readonly object CacheLock = new();

    private static readonly SyntaxTree GlobalUsingsSyntaxTree = CSharpSyntaxTree.ParseText(
        string.Join("\n",
        [
            "global using System;",
            "global using System.Linq;",
            "global using System.Collections.Generic;",
            "global using System.Numerics;",
            "global using System.Threading;",
            "global using System.Threading.Tasks;",
            "global using System.Text.RegularExpressions;",
            "global using Dalamud.Game.ClientState.Objects.Types;",
            "global using PromeRotation.Scripting;",
            "global using PromeRotation.Spatial.Drawing;",
            "global using PromeRotation.Spatial.Geometry;",
            "global using PromeRotation.Spatial.SafeZone;",
            "global using PromeRotation.GreenMoveSystem;",
            "global using MasterPlayer.Scripting;",
        ]),
        new CSharpParseOptions(LanguageVersion.Latest));

    /// <summary>
    /// 编译源码为程序集字节。失败时 errors 携带诊断信息。
    /// </summary>
    public static byte[]? Compile(string source, string sourcePath, out string errors)
    {
        var hash = HashSource(source);
        lock (CacheLock)
        {
            if (CompileCache.TryGetValue(hash, out var cached))
            {
                errors = "";
                return cached;
            }
        }

        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            path: sourcePath);

        var compilation = CSharpCompilation.Create(
            assemblyName: $"MasterPlayerScript_{Guid.NewGuid():N}",
            syntaxTrees: [GlobalUsingsSyntaxTree, tree],
            references: GetReferences(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                allowUnsafe: true));

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        errors = FormatDiagnostics(result.Diagnostics);

        if (!result.Success)
            return null;

        stream.Position = 0;
        var bytes = stream.ToArray();

        lock (CacheLock)
        {
            CompileCache[hash] = bytes;
        }

        return bytes;
    }

    private static string HashSource(string source)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash);
    }

    private static IReadOnlyList<MetadataReference> GetReferences()
    {
        if (_references != null)
            return _references;

        lock (LockObj)
        {
            if (_references != null)
                return _references;

            var pluginDirectory = GetPluginDirectory();
            var probeDirectories = GetReferenceProbeDirectories(pluginDirectory);
            var refs = new List<MetadataReference>();

            AddAssembly(refs, probeDirectories, typeof(object).Assembly, "System.Private.CoreLib");
            AddAssembly(refs, probeDirectories, typeof(Enumerable).Assembly, "System.Linq");
            AddAssembly(refs, probeDirectories, typeof(Vector3).Assembly, "System.Numerics");
            AddAssembly(refs, probeDirectories, typeof(IBattleChara).Assembly, "Dalamud");
            AddAssembly(refs, probeDirectories, typeof(Svc).Assembly, "ECommons");
            AddAssembly(refs, probeDirectories, typeof(IScriptContext).Assembly, "PromeRotation.Scripting");
            AddAssembly(refs, probeDirectories, typeof(PromeRotation.Plugin).Assembly, "PromeRotation");
            AddAssembly(refs, probeDirectories, typeof(MasterPlayerScript).Assembly, "MasterPlayer");
            AddRuntimeFacades(refs);
            AddLoadedOptionalAssemblies(refs, probeDirectories);

            _references = refs
                .GroupBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToArray();

            try
            {
                var skipped = GetSkippedAssemblies();
                var message = $"[MasterPlayer] 脚本编译引用共 {_references.Count} 项，探测目录：{string.Join(" | ", probeDirectories)}";
                if (skipped.Count > 0)
                    message += $"；未能解析的程序集：{string.Join(", ", skipped)}";
                Svc.Log.Info(message);
            }
            catch
            {
            }

            return _references;
        }
    }

    private static void AddLoadedOptionalAssemblies(List<MetadataReference> refs, IReadOnlyList<string> probeDirectories)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is "FFXIVClientStructs"
                or "InteropGenerator.Runtime"
                or "Lumina"
                or "Lumina.Excel"
                or "Dalamud.Bindings.ImGui"
                or "PromeRotation"
                or "PromeRotation.Scripting")
            {
                AddAssembly(refs, probeDirectories, assembly, name!);
            }
        }
    }

    private static void AddRuntimeFacades(List<MetadataReference> refs)
    {
        var coreLibLocation = typeof(object).Assembly.Location;
        if (string.IsNullOrWhiteSpace(coreLibLocation))
            return;

        var runtimeDirectory = Path.GetDirectoryName(coreLibLocation);
        if (string.IsNullOrWhiteSpace(runtimeDirectory))
            return;

        foreach (var name in new[]
        {
            "System.Runtime",
            "System.Runtime.InteropServices",
            "System.Numerics.Vectors",
            "System.Collections",
            "System.Collections.Concurrent",
            "System.Linq",
            "System.Linq.Expressions",
            "System.ObjectModel",
            "System.Memory",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Text.RegularExpressions",
        })
        {
            var path = Path.Combine(runtimeDirectory, $"{name}.dll");
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }
    }

    private static void AddAssembly(
        List<MetadataReference> refs,
        IReadOnlyList<string> probeDirectories,
        Assembly assembly,
        string simpleName)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(assembly.Location) && File.Exists(assembly.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(assembly.Location));
                return;
            }
        }
        catch
        {
            // Dynamic assemblies do not expose a stable Location; skip them.
        }

        // 流式加载的程序集（Location 为空）：按候选目录逐一探测同名 DLL。
        // 插件包目录有 MasterPlayer.dll；宿主 PR 安装目录有 PromeRotation/ECommons/CodeAnalysis 等。
        foreach (var directory in probeDirectories)
        {
            var probePath = Path.Combine(directory, $"{simpleName}.dll");
            if (File.Exists(probePath))
            {
                refs.Add(MetadataReference.CreateFromFile(probePath));
                return;
            }
        }

        RecordSkippedAssembly(simpleName);
    }

    private static readonly List<string> SkippedAssemblies = new();

    private static void RecordSkippedAssembly(string simpleName)
    {
        lock (CacheLock)
        {
            if (!SkippedAssemblies.Contains(simpleName, StringComparer.OrdinalIgnoreCase))
                SkippedAssemblies.Add(simpleName);
        }
    }

    private static IReadOnlyList<string> GetSkippedAssemblies()
    {
        lock (CacheLock)
        {
            return SkippedAssemblies.ToArray();
        }
    }

    private static IReadOnlyList<string> GetReferenceProbeDirectories(string pluginDirectory)
    {
        var directories = new List<string>();
        AddIfExists(directories, pluginDirectory);

        // 宿主 PR 安装/构建目录（Svc.PluginInterface 是宿主的接口，AssemblyLocation 指向其 PromeRotation.dll，
        // 即使程序集本体被流式加载，该 FileInfo 仍来自插件安装记录，通常有效）。
        try
        {
            var hostDirectory = Svc.PluginInterface.AssemblyLocation?.Directory?.FullName;
            AddIfExists(directories, hostDirectory);
        }
        catch
        {
        }

        return directories;
    }

    private static void AddIfExists(List<string> directories, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return;

        if (!directories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            directories.Add(directory);
    }

    private static string GetPluginDirectory()
    {
        // 本插件程序集被 PR 加载器流式加载（Assembly.Location 为空），而 Svc.PluginInterface.AssemblyLocation
        // 指向宿主 PR 目录（没有 MasterPlayer.dll）。唯一可靠位置是 PR 卫星插件包目录：
        // pluginConfigs\PromeRotation\Plugins\<程序集名>\，构建产物即部署于此。
        try
        {
            var configDirectory = Svc.PluginInterface.ConfigDirectory?.FullName;
            if (!string.IsNullOrWhiteSpace(configDirectory))
            {
                var assemblyName = typeof(MasterPlayerScript).Assembly.GetName().Name ?? "MasterPlayer";
                var packageDirectory = Path.Combine(configDirectory, "Plugins", assemblyName);
                if (File.Exists(Path.Combine(packageDirectory, $"{assemblyName}.dll")))
                    return packageDirectory;
            }
        }
        catch
        {
        }

        try
        {
            var location = typeof(MasterPlayerScript).Assembly.Location;
            var directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrWhiteSpace(directory))
                return directory;
        }
        catch
        {
        }

        try
        {
            var directory = Svc.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }
        catch
        {
        }

        return ".";
    }

    private static string FormatDiagnostics(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(x => x.Severity == DiagnosticSeverity.Error)
            .Select(x => x.ToString())
            .ToArray();

        return errors.Length == 0 ? "" : string.Join(Environment.NewLine, errors);
    }
}
