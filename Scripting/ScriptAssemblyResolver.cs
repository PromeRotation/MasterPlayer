using System.Reflection;
using System.Runtime.Loader;
using ECommons.DalamudServices;

namespace MasterPlayer.Scripting;

/// <summary>
/// 脚本程序集加载器：加载进插件自身 ALC，挂 Resolving 兜底依赖解析。
/// 脚本直接引用插件类型（MasterPlayerScript 等）无需编译期拼接。
/// 代价：脚本 DLL 与插件同生命周期，无法单独卸载——"卸载脚本"= 停止调度 + 清理事件订阅/绘制。
/// </summary>
internal static class ScriptAssemblyResolver
{
    private static readonly object LockObj = new();
    private static readonly Dictionary<string, Assembly> LoadedByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AssemblyDependencyResolver> ResolversByMainPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ProbeDirectories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<AssemblyLoadContext> RegisteredContexts = new();

    public static Assembly LoadFromPath(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        RegisterProbeDirectory(Path.GetDirectoryName(fullPath));
        RegisterDependencyResolver(fullPath);

        var loadContext = GetScriptLoadContext();
        EnsureRegistered(loadContext);

        lock (LockObj)
        {
            if (LoadedByPath.TryGetValue(fullPath, out var assembly))
                return assembly;

            assembly = LoadAssemblyViaStream(loadContext, fullPath);
            LoadedByPath[fullPath] = assembly;
            return assembly;
        }
    }

    public static Assembly LoadFromBytes(byte[] assemblyBytes)
    {
        var loadContext = GetScriptLoadContext();
        EnsureRegistered(loadContext);

        using var stream = new MemoryStream(assemblyBytes, writable: false);
        return loadContext.LoadFromStream(stream);
    }

    private static AssemblyLoadContext GetScriptLoadContext()
    {
        return AssemblyLoadContext.GetLoadContext(typeof(MasterPlayerScript).Assembly)
            ?? AssemblyLoadContext.Default;
    }

    private static void RegisterProbeDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        lock (LockObj)
        {
            ProbeDirectories.Add(directory);
        }
    }

    private static void RegisterDependencyResolver(string mainAssemblyPath)
    {
        lock (LockObj)
        {
            if (ResolversByMainPath.ContainsKey(mainAssemblyPath))
                return;

            ResolversByMainPath[mainAssemblyPath] = new AssemblyDependencyResolver(mainAssemblyPath);
        }
    }

    private static void EnsureRegistered(AssemblyLoadContext loadContext)
    {
        lock (LockObj)
        {
            if (RegisteredContexts.Any(x => ReferenceEquals(x, loadContext)))
                return;

            loadContext.Resolving += ResolveAssembly;
            RegisteredContexts.Add(loadContext);
        }
    }

    private static Assembly? ResolveAssembly(AssemblyLoadContext loadContext, AssemblyName assemblyName)
    {
        var hostAssembly = typeof(MasterPlayerScript).Assembly;
        if (AssemblyName.ReferenceMatchesDefinition(assemblyName, hostAssembly.GetName())
            || string.Equals(assemblyName.Name, hostAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
        {
            return hostAssembly;
        }

        var loaded = loadContext.Assemblies.FirstOrDefault(x => IsSameAssemblyName(x, assemblyName))
            ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => IsSameAssemblyName(x, assemblyName));
        if (loaded != null)
            return loaded;

        var resolvedPath = ResolveWithDependencyResolvers(assemblyName);
        if (!string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath))
            return LoadFromPath(resolvedPath);

        var fileName = $"{assemblyName.Name}.dll";
        foreach (var directory in GetProbeDirectories())
        {
            var candidate = Path.Combine(directory, fileName);
            if (!File.Exists(candidate))
                continue;

            return LoadFromPath(candidate);
        }

        return null;
    }

    private static string? ResolveWithDependencyResolvers(AssemblyName assemblyName)
    {
        AssemblyDependencyResolver[] resolvers;
        lock (LockObj)
        {
            resolvers = ResolversByMainPath.Values.ToArray();
        }

        foreach (var resolver in resolvers)
        {
            var path = resolver.ResolveAssemblyToPath(assemblyName);
            if (!string.IsNullOrWhiteSpace(path))
                return path;
        }

        return null;
    }

    private static string[] GetProbeDirectories()
    {
        var directories = new List<string>();
        lock (LockObj)
        {
            directories.AddRange(ProbeDirectories);
        }

        AddDirectory(directories, GetPluginDirectory());
        AddDirectory(directories, GetAssemblyDirectory(typeof(MasterPlayerScript).Assembly));

        return directories
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? GetPluginDirectory()
    {
        try
        {
            var directory = Svc.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                return directory;
        }
        catch
        {
        }

        return GetAssemblyDirectory(typeof(MasterPlayerScript).Assembly);
    }

    private static string? GetAssemblyDirectory(Assembly assembly)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(assembly.Location))
                return null;

            return Path.GetDirectoryName(assembly.Location);
        }
        catch
        {
            return null;
        }
    }

    private static void AddDirectory(List<string> directories, string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
            directories.Add(directory);
    }

    private static Assembly LoadAssemblyViaStream(AssemblyLoadContext loadContext, string assemblyPath)
    {
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        using var assemblyStream = new MemoryStream(assemblyBytes, writable: false);

        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        if (File.Exists(pdbPath))
        {
            var pdbBytes = File.ReadAllBytes(pdbPath);
            using var pdbStream = new MemoryStream(pdbBytes, writable: false);
            return loadContext.LoadFromStream(assemblyStream, pdbStream);
        }

        return loadContext.LoadFromStream(assemblyStream);
    }

    private static bool IsSameAssemblyName(Assembly assembly, AssemblyName requestedName)
    {
        var assemblyName = assembly.GetName();
        return string.Equals(assemblyName.Name, requestedName.Name, StringComparison.OrdinalIgnoreCase);
    }
}
