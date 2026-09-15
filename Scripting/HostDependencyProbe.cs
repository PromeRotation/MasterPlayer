using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using ECommons.DalamudServices;

namespace MasterPlayer.Scripting;

/// <summary>
/// 宿主依赖兜底探针：白名单（§5.5）之外的依赖（如 Microsoft.CodeAnalysis）不进插件包（csproj Private=false），
/// PR 加载器（PromeExAssemblyLoader）只把白名单解析到宿主，其余依赖 miss 后触发卫星 ALC 的 Resolving ——
/// 在此按「插件自身目录 → 宿主 PR 安装目录」顺序探测同名 DLL，读字节流加载（不锁文件、不再进 Load 重写）。
/// 程序集加载即挂钩（ModuleInitializer）；全部异常吞掉返回 null，让上层报原始异常。
/// </summary>
internal static class HostDependencyProbe
{
    private static AssemblyLoadContext? _hookedContext;

    // CA2255：库中使用 ModuleInitializer 是有意为之——卫星插件需要在程序集加载时（而非首次调用时）挂 ALC Resolving。
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Init()
#pragma warning restore CA2255
    {
        try
        {
            var alc = AssemblyLoadContext.GetLoadContext(typeof(HostDependencyProbe).Assembly);
            if (alc == null || ReferenceEquals(_hookedContext, alc))
                return;

            alc.Resolving += OnResolving;
            _hookedContext = alc;
        }
        catch
        {
        }
    }

    private static Assembly? OnResolving(AssemblyLoadContext alc, AssemblyName name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name.Name))
                return null;

            var fileName = name.Name + ".dll";
            foreach (var directory in CandidateDirectories())
            {
                var candidate = Path.Combine(directory, fileName);
                if (!File.Exists(candidate))
                    continue;

                var bytes = File.ReadAllBytes(candidate);
                using var stream = new MemoryStream(bytes, writable: false);
                var assembly = alc.LoadFromStream(stream);
                SafeLogInfo($"[MasterPlayer] 依赖兜底加载：{name.Name} ← {directory}");
                return assembly;
            }
        }
        catch
        {
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        // 1. 插件自身目录（若用户/构建把依赖 DLL 放包里则优先）。插件本体被 PR 流加载，Location 为空时
        //    回退到已知的包目录（pluginConfigs\PromeRotation\Plugins\MasterPlayer）。
        string? own = null;
        try
        {
            own = Path.GetDirectoryName(typeof(HostDependencyProbe).Assembly.Location);
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(own))
        {
            try
            {
                own = Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "Plugins", "MasterPlayer");
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(own))
            yield return own;

        // 2. 宿主本体目录（正在运行的 PR 安装目录，依赖版本天然与本体一致）。PromeRotation 是白名单
        //    程序集，typeof 解析走加载器共享路径，其 Location 指向真实安装目录。
        string? host = null;
        try
        {
            host = Path.GetDirectoryName(typeof(PromeRotation.Plugin).Assembly.Location);
        }
        catch
        {
        }

        if (!string.IsNullOrWhiteSpace(host) && !string.Equals(host, own, StringComparison.OrdinalIgnoreCase))
            yield return host;
    }

    private static void SafeLogInfo(string message)
    {
        try
        {
            Svc.Log.Info(message);
        }
        catch
        {
        }
    }
}
