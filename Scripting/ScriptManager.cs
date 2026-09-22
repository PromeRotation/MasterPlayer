using System.Reflection;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using MasterPlayer.Scripting.Visual;
using PromeRotation.LogSystem;

namespace MasterPlayer.Scripting;

public sealed class ScriptManager : IDisposable
{
    private readonly List<ScriptDescriptor> _descriptors = new();
    private readonly Dictionary<string, RunningScript> _running = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

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
        LoadBuiltInScripts();

        SetStatus($"已刷新脚本列表，共 {_descriptors.Count} 个脚本。");
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(ScriptsDirectory);
    }

    private void LoadBuiltInScripts()
    {
        try
        {
            var assembly = typeof(ScriptManager).Assembly;
            LoadScriptTypesFromAssembly(assembly, $"{assembly.GetName().Name}.dll", ScriptSource.BuiltIn);
        }
        catch (Exception ex)
        {
            SetError($"加载内置脚本失败：{ex.Message}");
            Svc.Log.Error(ex, "[MasterPlayer] Failed to load built-in scripts");
        }
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

        foreach (var path in Directory.EnumerateFiles(directory, "*" + VisualScriptStore.FileExtension, SearchOption.AllDirectories))
        {
            try
            {
                AddDescriptor(CreateVisualDescriptor(path, VisualScriptStore.Parse(File.ReadAllText(path))));
            }
            catch (Exception ex)
            {
                SetError($"加载可视化脚本失败：{Path.GetFileName(path)} - {ex.Message}");
                Svc.Log.Error(ex, $"[MasterPlayer] Failed to load visual script: {path}");
            }
        }
    }

    private static ScriptDescriptor CreateVisualDescriptor(string path, VisualScriptDefinition definition)
    {
        var snapshot = VisualScriptStore.Clone(definition);
        var metadata = new ScriptMetadata(snapshot.Id, snapshot.Name, [(uint)snapshot.TerritoryId], "1.0", "",
            snapshot.Note ?? "", snapshot.TerritoryId.ToString(), Path.GetFullPath(path));
        return new ScriptDescriptor(metadata, ScriptSource.Visual, metadata.SourcePath, () => new VisualMechanicScript(snapshot));
    }

    internal string SaveVisualScript(string path, VisualScriptDefinition definition, string? expectedContent)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = _descriptors.FirstOrDefault(d => d.Source != ScriptSource.BuiltIn
            && (string.Equals(d.Metadata.Id, definition.Id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)));
        if (existing != null && (existing.Source != ScriptSource.Visual || expectedContent == null
            || !string.Equals(existing.Metadata.Id, definition.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("脚本 ID 或文件路径与已有脚本冲突。请复制为新脚本后保存。");

        var descriptor = CreateVisualDescriptor(fullPath, definition);
        var json = VisualScriptStore.Save(fullPath, definition, expectedContent);
        StopScript(definition.Id);
        if (existing != null) _descriptors.Remove(existing);
        AddDescriptor(descriptor);
        LoadBuiltInScripts();
        if (MasterPlayerSettings.Instance.EnableScripts && IsScriptEnabled(definition.Id)
            && IsTerritoryMatched(descriptor.Metadata, Svc.ClientState.TerritoryType) && !StartScript(definition.Id))
            return json;
        SetStatus($"已保存可视化脚本：{definition.Name}。");
        return json;
    }

    internal void DeleteVisualScript(ScriptDescriptor descriptor, string expectedContent)
    {
        if (descriptor.Source != ScriptSource.Visual) throw new InvalidOperationException("只能在编辑器中删除可视化脚本。");
        VisualScriptStore.Delete(descriptor.SourcePath, expectedContent);
        StopScript(descriptor.Metadata.Id);
        _descriptors.Remove(descriptor);
        LoadBuiltInScripts();
        var settings = MasterPlayerSettings.Instance;
        if (!_descriptors.Any(d => string.Equals(d.Metadata.Id, descriptor.Metadata.Id, StringComparison.OrdinalIgnoreCase)))
        {
            settings.ScriptEnabled.Remove(descriptor.Metadata.Id);
            settings.ScriptSettings.Remove(descriptor.Metadata.Id);
            settings.MechanismEnabled.Remove(descriptor.Metadata.Id);
        }
        settings.Save();
        SetStatus($"已删除可视化脚本：{descriptor.Metadata.Name}。");
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
            if (source == ScriptSource.BuiltIn && type.Namespace != "MasterPlayer.Scripts"
                && type.Namespace?.StartsWith("MasterPlayer.Scripts.", StringComparison.Ordinal) != true)
                continue;

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
        if (descriptor.Source == ScriptSource.BuiltIn
            && _descriptors.Any(x => MatchesScript(x, descriptor)))
            return;

        var exists = _descriptors.Any(x => x.Source != ScriptSource.BuiltIn && string.Equals(
            x.Metadata.Id,
            descriptor.Metadata.Id,
            StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            SetError($"脚本 ID 重复，已跳过：{descriptor.Metadata.Id}");
            return;
        }

        foreach (var builtIn in _descriptors.Where(x => x.Source == ScriptSource.BuiltIn && MatchesScript(x, descriptor)).ToArray())
        {
            StopScript(builtIn.Metadata.Id);
            _descriptors.Remove(builtIn);
        }

        _descriptors.Add(descriptor);
    }

    private static bool MatchesScript(ScriptDescriptor first, ScriptDescriptor second)
        => string.Equals(first.Metadata.Id, second.Metadata.Id, StringComparison.OrdinalIgnoreCase)
           || string.Equals(first.Metadata.Name, second.Metadata.Name, StringComparison.OrdinalIgnoreCase);

    // ── 运行管理（每脚本独立生命周期）────────────────

    /// <summary>回放会话进行中为 true：Update 跳过区域自动启停，脚本由 ReplayController 驱动。</summary>
    internal bool ReplayModeActive { get; private set; }

    internal void EnterReplayMode()
    {
        ReplayModeActive = true;
        MasterPlayerPlugin.Instance!.SafeZone.Clear();
    }

    internal void ExitReplayMode()
    {
        MasterPlayerPlugin.Instance!.SafeZone.Clear();
        ReplayModeActive = false;
    }

    public bool StartScript(string id) => StartScript(id, null);

    /// <summary>router 非空时脚本以回放模式启动（事件来自 ReplayController 而非 PromeEventBus）。</summary>
    internal bool StartScript(string id, IScriptEventRouter? router)
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
            var context = new MasterPlayerScriptContext(this, descriptor.Metadata, settings, router);
            script.Host = context;

            var running = new RunningScript(descriptor, script, context, settings, router != null);
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

    /// <summary>每帧驱动（主线程）：总开关 → 区域自动启停 → 逐脚本 Update。</summary>
    public void Update(float deltaTime)
    {
        if (!MasterPlayerSettings.Instance.EnableScripts)
        {
            if (_running.Count > 0)
                StopAll();
            return;
        }

        if (!ReplayModeActive)
        {
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
        }

        if (!ReplayModeActive) UpdateRunning(deltaTime, false);
    }

    internal void UpdateReplayScripts(float deltaTime)
    {
        if (!MasterPlayerSettings.Instance.EnableScripts) { StopAll(); return; }
        UpdateRunning(deltaTime, true);
    }

    private void UpdateRunning(float deltaTime, bool replay)
    {
        foreach (var (id, running) in _running.ToArray())
        {
            if (running.IsReplaySession != replay) continue;
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

    // ── 手动刷新（面板「刷新脚本」按钮）─────────────────

    /// <summary>手动重载：停止全部运行中脚本 → 重扫目录 → 按启用状态重启。脚本目录不做文件监听，只有点按钮才刷新。</summary>
    public void ReloadScripts()
    {
        try
        {
            var wasRunning = ReplayModeActive ? [] : _running.Keys.ToArray();
            if (ReplayModeActive) MasterPlayerPlugin.Instance?.Replay?.StopSession();
            foreach (var id in wasRunning)
                StopScript(id);

            RefreshCatalog();

            foreach (var id in wasRunning)
            {
                if (IsScriptEnabled(id) && _descriptors.Any(d => string.Equals(d.Metadata.Id, id, StringComparison.OrdinalIgnoreCase)))
                    StartScript(id);
            }

            SetStatus($"已重新加载脚本（{_descriptors.Count} 个）。");
        }
        catch (Exception ex)
        {
            SetError($"脚本重载失败：{ex.Message}");
            Svc.Log.Error(ex, "[MasterPlayer] Script reload failed");
        }
    }

    /// <summary>任一运行中脚本声明接管该读条技能时返回 true（通用躲避跳过，避免重复注册危险区）。</summary>
    public bool IsGenericDodgeSuppressed(uint actionId)
    {
        foreach (var (_, running) in _running)
            if (running.Context is MasterPlayerScriptContext context && context.SuppressesGenericDodge(actionId))
                return true;
        return false;
    }

    public bool IsGenericDodgeSuppressed(LogSystemCastStartEvent cast)
        => IsGenericDodgeSuppressed(cast.ActionId)
           || _running.Values.Any(r => r.Script is VisualMechanicScript script && script.SuppressesGenericDodge(cast));

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

    /// <summary>回放会话启动的脚本为 true（事件来自回放而非实时总线）。</summary>
    public bool IsReplaySession { get; }

    internal RunningScript(
        ScriptDescriptor descriptor,
        MasterPlayerScript script,
        IScriptHostContext context,
        ScriptSettingStore settings,
        bool isReplaySession = false)
    {
        Descriptor = descriptor;
        Script = script;
        Context = context;
        Settings = settings;
        IsReplaySession = isReplaySession;
    }
}
