using System.Reflection;
using ECommons.DalamudServices;
using MasterPlayer.Settings;

namespace MasterPlayer.Scripting;

public sealed class ScriptManager : IDisposable
{
    private readonly List<ScriptDescriptor> _descriptors = new();
    private readonly Dictionary<string, RunningScript> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Threading.Timer? _reloadTimer;
    private volatile bool _pendingRefresh;
    private bool _disposed;

    public ScriptManager()
    {
        _reloadTimer = new System.Threading.Timer(_ => _pendingRefresh = true, null, Timeout.Infinite, Timeout.Infinite);
        _watcher = CreateWatcher();
    }

    public IReadOnlyList<ScriptDescriptor> Scripts => _descriptors;

    public IReadOnlyDictionary<string, RunningScript> Running => _running;

    public string StatusMessage { get; private set; } = "未加载脚本。";

    public string ErrorMessage { get; private set; } = "";

    public string ScriptsDirectory => Path.Combine(
        Svc.PluginInterface.ConfigDirectory.FullName,
        "MasterPlayer",
        "Scripts");

    // ── 目录扫描 ─────────────────────────────────────

    public void RefreshCatalog()
    {
        _descriptors.Clear();
        ErrorMessage = "";

        EnsureDirectory();
        LoadExternalScripts();

        SetStatus($"已刷新脚本列表，共 {_descriptors.Count} 个脚本。");
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ScriptsDirectory);
    }

    private void LoadExternalScripts()
    {
        var directory = ScriptsDirectory;
        if (!Directory.Exists(directory))
            return;

        foreach (var csPath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            TryLoadSourceScript(csPath);
        }

        foreach (var dllPath in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
        {
            TryLoadExternalAssembly(dllPath);
        }
    }

    private void TryLoadSourceScript(string csPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(csPath);
            var source = File.ReadAllText(fullPath);
            var bytes = ScriptCompiler.Compile(source, fullPath, out var errors);
            if (bytes == null)
            {
                SetError($"源码脚本编译失败：{Path.GetFileName(fullPath)}{Environment.NewLine}{errors}");
                return;
            }

            var assembly = ScriptAssemblyResolver.LoadFromBytes(bytes);
            LoadScriptTypesFromAssembly(assembly, fullPath, ScriptSource.LocalSource);
        }
        catch (Exception ex)
        {
            SetError($"加载源码脚本失败：{Path.GetFileName(csPath)} - {ex.Message}");
            Svc.Log.Error(ex, $"[MasterPlayer] Failed to load source script: {csPath}");
        }
    }

    private void TryLoadExternalAssembly(string dllPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(dllPath);
            var assembly = ScriptAssemblyResolver.LoadFromPath(fullPath);
            LoadScriptTypesFromAssembly(assembly, fullPath, ScriptSource.LocalDll);
        }
        catch (Exception ex)
        {
            SetError($"加载脚本程序集失败：{Path.GetFileName(dllPath)} - {ex.Message}");
            Svc.Log.Error(ex, $"[MasterPlayer] Failed to load script assembly: {dllPath}");
        }
    }

    private void LoadScriptTypesFromAssembly(Assembly assembly, string sourcePath, ScriptSource source)
    {
        foreach (var type in GetAssemblyTypes(assembly))
        {
            if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) == null)
                continue;

            if (!typeof(MasterPlayerScript).IsAssignableFrom(type))
                continue;

            var metadata = BuildMetadata(type, sourcePath);
            var descriptor = new ScriptDescriptor(
                metadata,
                source,
                sourcePath,
                () => (MasterPlayerScript)Activator.CreateInstance(type)!);

            AddDescriptor(descriptor);
        }
    }

    private static ScriptMetadata BuildMetadata(Type type, string sourcePath)
    {
        var attr = type.GetCustomAttribute<MechanicScriptAttribute>();

        var id = attr?.Guid ?? type.FullName ?? type.Name;
        if (string.IsNullOrWhiteSpace(id))
            id = type.Name;

        return new ScriptMetadata(
            Id: id,
            Name: attr?.Name ?? type.Name,
            Territorys: attr?.Territorys ?? [],
            Version: attr?.Version ?? "0.0.0",
            Author: attr?.Author ?? "未知",
            Note: attr?.Note ?? "",
            Region: RegionName(attr?.Territorys),
            SourcePath: sourcePath);
    }

    private static string RegionName(uint[]? territorys)
    {
        if (territorys == null || territorys.Length == 0)
            return "通用";
        return string.Join(",", territorys);
    }

    private void AddDescriptor(ScriptDescriptor descriptor)
    {
        var exists = _descriptors.Any(x => string.Equals(
            x.Metadata.Id,
            descriptor.Metadata.Id,
            StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            SetError($"脚本 ID 重复，已跳过：{descriptor.Metadata.Id}");
            return;
        }

        _descriptors.Add(descriptor);
    }

    // ── 运行管理（每脚本独立生命周期）────────────────

    public bool StartScript(string id)
    {
        if (_running.ContainsKey(id))
            return true;

        var descriptor = _descriptors.FirstOrDefault(x => string.Equals(
            x.Metadata.Id, id, StringComparison.OrdinalIgnoreCase));
        if (descriptor == null)
        {
            SetError($"脚本不存在：{id}");
            return false;
        }

        MasterPlayerScript? script = null;
        try
        {
            script = descriptor.CreateInstance();
            var settings = LoadScriptSettings(id, script);
            var context = new MasterPlayerScriptContext(this, descriptor.Metadata, settings);
            script.Host = context;

            var running = new RunningScript(descriptor, script, context, settings);
            _running[id] = running;

            script.StartInternal();

            SetStatus($"已启动脚本：{descriptor.Metadata.Name}");
            return true;
        }
        catch (Exception ex)
        {
            _running.Remove(id);

            // StartInternal 在 OnStart 前置 _started = true，故 OnStart 中途抛异常时
            // StopInternal 仍会执行 OnStop 并 Dispose 上下文（退订事件、清理三门面）。
            if (script != null)
            {
                try
                {
                    script.StopInternal();
                }
                catch
                {
                }
            }

            SetError($"脚本启动失败：{descriptor.Metadata.Name} - {ex.Message}");
            Svc.Log.Error(ex, $"[MasterPlayer] Script start failed: {descriptor.Metadata.Name}");
            return false;
        }
    }

    public void StopScript(string id)
    {
        if (!_running.TryGetValue(id, out var running))
            return;

        _running.Remove(id);

        try
        {
            running.Script.StopInternal();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] Script stop failed: {running.Descriptor.Metadata.Name}");
        }

        SetStatus($"已停止脚本：{running.Descriptor.Metadata.Name}");
    }

    public void StopAll()
    {
        foreach (var id in _running.Keys.ToArray())
            StopScript(id);
    }

    /// <summary>每帧驱动（主线程）：热重载 → 总开关 → 区域自动启停 → 逐脚本 Update。</summary>
    public void Update(float deltaTime)
    {
        if (_pendingRefresh)
        {
            _pendingRefresh = false;
            ReloadAfterExternalChange();
        }

        if (!MasterPlayerSettings.Instance.EnableScripts)
        {
            if (_running.Count > 0)
                StopAll();
            return;
        }

        uint territory = Svc.ClientState.TerritoryType;
        foreach (var descriptor in _descriptors)
        {
            var id = descriptor.Metadata.Id;
            var enabled = IsScriptEnabled(id);
            var isRunning = _running.ContainsKey(id);
            var matched = IsTerritoryMatched(descriptor.Metadata, territory);

            if (enabled && matched && !isRunning)
                StartScript(id);
            else if (isRunning && (!enabled || !matched))
                StopScript(id);
        }

        foreach (var (id, running) in _running.ToArray())
        {
            try
            {
                running.Script.UpdateInternal(deltaTime);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, $"[MasterPlayer] Script update failed: {running.Descriptor.Metadata.Name}");
                StopScript(id);
                SetError($"脚本更新异常，已停止：{running.Descriptor.Metadata.Name} - {ex.Message}");
            }
        }
    }

    /// <summary>当前区域是否匹配脚本（空区域列表 = 通用，全区域匹配）。</summary>
    public static bool IsTerritoryMatched(ScriptMetadata metadata, uint currentTerritory)
        => metadata.Territorys.Length == 0
           || metadata.Territorys.Contains(currentTerritory);

    // ── 启停 / 机制开关持久化 ─────────────────────────

    public bool IsScriptEnabled(string id)
        => MasterPlayerSettings.Instance.ScriptEnabled.TryGetValue(id, out var enabled) && enabled;

    public void SetScriptEnabled(string id, bool enabled)
    {
        MasterPlayerSettings.Instance.ScriptEnabled[id] = enabled;
        MasterPlayerSettings.Instance.Save();

        if (!enabled)
            StopScript(id);
    }

    /// <summary>机制开关：MechanismEnabled 存的是「已禁用」机制名集合，默认启用。</summary>
    public bool IsMechanismEnabled(string scriptId, string mechanism)
    {
        var map = MasterPlayerSettings.Instance.MechanismEnabled;
        return !map.TryGetValue(scriptId, out var disabled)
               || disabled == null
               || !disabled.Contains(mechanism);
    }

    public void SetMechanismEnabled(string scriptId, string mechanism, bool enabled)
    {
        var map = MasterPlayerSettings.Instance.MechanismEnabled;
        if (!map.TryGetValue(scriptId, out var disabled) || disabled == null)
        {
            disabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            map[scriptId] = disabled;
        }

        if (enabled)
            disabled.Remove(mechanism);
        else
            disabled.Add(mechanism);

        MasterPlayerSettings.Instance.Save();
    }

    // ── 每脚本设置 ───────────────────────────────────

    private ScriptSettingStore LoadScriptSettings(string id, MasterPlayerScript script)
    {
        var store = new ScriptSettingStore(() =>
        {
            SaveScriptSettings(id, script);
            MasterPlayerSettings.Instance.Save();
        });

        store.RegisterProperties(script.GetType(), script);
        store.SetDisplayNames(script.GetType());

        if (MasterPlayerSettings.Instance.ScriptSettings.TryGetValue(id, out var obj) && obj != null)
            store.Deserialize(obj);

        return store;
    }

    private void SaveScriptSettings(string id, MasterPlayerScript script)
    {
        if (_running.TryGetValue(id, out var running) && !ReferenceEquals(running.Script, script))
            return;

        var store = _running.TryGetValue(id, out var run) ? run.Settings : null;
        if (store == null)
            return;

        MasterPlayerSettings.Instance.ScriptSettings[id] = store.Serialize();
    }

    // ── 热重载（脚本目录文件变更）─────────────────────

    private FileSystemWatcher? CreateWatcher()
    {
        try
        {
            Directory.CreateDirectory(ScriptsDirectory);
            var watcher = new FileSystemWatcher(ScriptsDirectory)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            watcher.Changed += OnScriptFileChanged;
            watcher.Created += OnScriptFileChanged;
            watcher.Renamed += OnScriptFileChanged;
            watcher.Deleted += OnScriptFileChanged;
            return watcher;
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 脚本目录监视初始化失败");
            return null;
        }
    }

    private void OnScriptFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var extension = Path.GetExtension(e.FullPath);
            if (!string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                return;

            // 后台线程只做防抖计时，严禁触碰游戏 API；实际刷新在 Update()（主线程）执行
            _reloadTimer?.Change(500, Timeout.Infinite);
        }
        catch
        {
        }
    }

    private void ReloadAfterExternalChange()
    {
        try
        {
            var wasRunning = _running.Keys.ToArray();
            foreach (var id in wasRunning)
                StopScript(id);

            RefreshCatalog();

            foreach (var id in wasRunning)
            {
                if (IsScriptEnabled(id))
                    StartScript(id);
            }

            SetStatus($"脚本目录已变更，已重新加载（{_descriptors.Count} 个脚本）。");
        }
        catch (Exception ex)
        {
            SetError($"脚本热重载失败：{ex.Message}");
            Svc.Log.Error(ex, "[MasterPlayer] Script hot reload failed");
        }
    }

    private static Type[] GetAssemblyTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            var details = ex.LoaderExceptions?
                .OfType<Exception>()
                .Select(x => x.Message)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];
            var suffix = details.Length == 0
                ? ex.Message
                : string.Join(" | ", details);
            throw new InvalidOperationException($"程序集类型加载失败：{suffix}", ex);
        }
    }

    public void SetStatus(string message)
    {
        StatusMessage = message;
    }

    public void SetError(string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _watcher?.Dispose();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 脚本目录监视释放失败");
        }

        try
        {
            _reloadTimer?.Dispose();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 脚本重载计时器释放失败");
        }

        try
        {
            StopAll();
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[MasterPlayer] 停止全部脚本失败");
        }
    }
}

public sealed class RunningScript
{
    public ScriptDescriptor Descriptor { get; }
    public MasterPlayerScript Script { get; }
    public IScriptHostContext Context { get; }
    public ScriptSettingStore Settings { get; }

    internal RunningScript(
        ScriptDescriptor descriptor,
        MasterPlayerScript script,
        IScriptHostContext context,
        ScriptSettingStore settings)
    {
        Descriptor = descriptor;
        Script = script;
        Context = context;
        Settings = settings;
    }
}
