using System.Reflection;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Lumina.Excel;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Drawing;
using MasterPlayer.Spatial.Geometry;
using MasterPlayer.Spatial.SafeZone;

namespace MasterPlayer.Scripting;

// Compile the real standalone script against a headless host using the plugin's geometry and registry.
[AttributeUsage(AttributeTargets.Class)]
internal sealed class MechanicScriptAttribute : Attribute
{
    public MechanicScriptAttribute(string name, uint[] territorys, string guid, string version, string author, string note)
        => _ = (name, territorys, guid, version, author, note);
}

public abstract class MasterPlayerScript
{
    private Action<LogSystemCastStartEvent>? _cast;
    private Action<LogSystemCancelCastEvent>? _cancel;
    private Action<LogSystemInterruptCastEvent>? _interrupt;
    private Action? _battleEnded;
    public ScriptSafeZoneApi SafeZone { get; } = new();
    public ScriptDrawApi Draw { get; } = new();
    public List<string> Errors { get; } = [];
    public Replay.ReplayWorldState? ReplayWorld { get; set; }
    public DateTime CurrentTime => ReplayWorld?.CurrentTime ?? DateTime.UtcNow;
    protected virtual void OnStart() { }
    protected virtual void OnUpdate(float deltaTime) { }
    protected virtual void OnStop() { }
    public virtual IEnumerable<string> GetMechanisms() => [];
    public bool IsMechanismEnabled(string mechanism) => mechanism != "文字提示";
    protected void Log(string text) => throw new NotSupportedException(text);
    protected void LogError(string text) => Errors.Add(text);
    protected void SuppressGenericDodge(params uint[] actionIds) { }
    protected void OnStartCast(Action<LogSystemCastStartEvent> callback) => _cast += callback;
    protected void OnCancelCast(Action<LogSystemCancelCastEvent> callback) => _cancel += callback;
    protected void OnInterruptCast(Action<LogSystemInterruptCastEvent> callback) => _interrupt += callback;
    protected void OnBattleEnded(Action callback) => _battleEnded += callback;
    internal void StartForTest() => OnStart();
    internal void StopForTest()
    {
        OnStop();
        _cast = null;
        _cancel = null;
        _interrupt = null;
        _battleEnded = null;
    }
    internal void SendForTest(LogSystemCastStartEvent e) => _cast?.Invoke(e);
    internal void SendForTest(LogSystemCancelCastEvent e) => _cancel?.Invoke(e);
    internal void SendForTest(LogSystemInterruptCastEvent e) => _interrupt?.Invoke(e);
    internal void EndBattleForTest() => _battleEnded?.Invoke();
    protected void OnActionEffect(Action<LogSystemActionEffectEvent> callback) => throw new NotSupportedException();
    protected void OnTether(Action<LogSystemTetherEvent> callback) => throw new NotSupportedException();
    protected void OnTetherCancel(Action<LogSystemTetherCancelEvent> callback) => throw new NotSupportedException();
    protected void OnEObjAnimation(Action<LogSystemEObjAnimationEvent> callback) => throw new NotSupportedException();
    protected void OnEObjSetState(Action<LogSystemEObjSetStateEvent> callback) => throw new NotSupportedException();
    protected void OnMapEffect(Action<LogSystemMapEffectEvent> callback) => throw new NotSupportedException();
}

public sealed class ScriptSafeZoneApi : IDisposable
{
    public SafeZoneManager Manager { get; private set; } = new(new SafeZoneSetting { EnableSafeZone = true });
    private bool _ownsManager = true;
    internal void ShareManagerForTest(SafeZoneManager manager)
    {
        Manager.Dispose();
        Manager = manager;
        _ownsManager = false;
    }
    private SafeZoneRegistry Registry => new(Manager, () => UtcNowForTest ?? DateTime.UtcNow);
    public IDisposable BeginDrawingGroup(string name) => new Scope();
    public SafeZoneAreaHandle AddDelayedHit(GeometryTemplate template, TransformBinding binding, float delaySeconds, float hitDurationSeconds = 0.6f)
        => Registry.AddDelayedHit(template, binding, delaySeconds, hitDurationSeconds);
    public SafeZoneAreaHandle AddPersistentDanger(GeometryTemplate template, TransformBinding binding)
        => Registry.AddPersistentDanger(template, binding);
    public float DangerMarginForTest { get; set; }
    public DateTime? UtcNowForTest { get; set; }
    public SafeZoneAreaHandle AddDangerSnapshot(GeometryTemplate template, TransformBinding binding, IReadOnlyList<DangerWindow> windows)
        => Manager.AddDangerArea(MasterPlayer.Dodge.DangerMarginInflator.Inflate(template, DangerMarginForTest), binding, windows);
    public SafeZoneAreaHandle AddExactDangerSnapshot(GeometryTemplate template, TransformBinding binding, IReadOnlyList<DangerWindow> windows)
        => Manager.AddDangerArea(template, binding, windows);
    public SafeZoneAreaHandle AddPersistentSafeArea(GeometryTemplate template, TransformBinding binding, int priority = 0, PositionPreference preference = PositionPreference.Nearest)
        => Manager.AddGoalArea(template, binding, [GoalWindow.Persistent(UtcNowForTest ?? DateTime.UtcNow, priority, preference)]);
    public SafeZoneAreaHandle AddTimedDanger(GeometryTemplate template, TransformBinding binding, float durationSeconds)
        => Registry.AddTimedDanger(template, binding, durationSeconds);
    public SafeZoneAreaHandle AddTimedSafeArea(GeometryTemplate template, TransformBinding binding, float durationSeconds,
        int priority = 0, PositionPreference preference = PositionPreference.Nearest)
        => Registry.AddTimedSafeArea(template, binding, durationSeconds, priority, preference);
    public SafeZoneAreaHandle AddObstacle(GeometryTemplate template, TransformBinding binding) => Registry.AddObstacle(template, binding);
    internal readonly HashSet<SafeZoneAreaHandle> NavigationOnly = [];
    internal void SetNavigationOnly(SafeZoneAreaHandle handle, bool value) { if (value) NavigationOnly.Add(handle); else NavigationOnly.Remove(handle); }
    public bool Remove(SafeZoneAreaHandle handle) { NavigationOnly.Remove(handle); return Registry.Remove(handle); }
    public void Dispose() { if (_ownsManager) Manager.Dispose(); }
    private sealed class Scope : IDisposable { public void Dispose() { } }
}

public sealed class ScriptDrawApi
{
    internal readonly Dictionary<string, MasterPlayer.Radar.AoePreview> Previews = [];
    internal readonly Dictionary<string, MasterPlayer.Radar.AoeLinePreview> Lines = [];
    public void Add(string id, IGeometryShape geometry, DrawStyle? style = null, float durationMs = 0f) { }
    public void AddAoe(string id, IGeometryShape geometry, bool safe = false, float durationMs = 0f) { }
    internal void AddAoePreview(string id, IGeometryShape geometry, bool safe, bool outlineOnly, float progress)
        => Previews[id] = new(id, geometry, safe, new(), DateTime.MaxValue, outlineOnly, progress);
    internal void AddLinePreview(string id, System.Numerics.Vector3 start, System.Numerics.Vector3 end, bool safe) => Lines[id] = new(id, start, end, safe, new());
    public void AddTether(string id, object start, object end, DrawStyle? style = null, float durationMs = 0f) { }
    public bool Remove(string id) => Previews.Remove(id) | Lines.Remove(id);
}

public class ScriptServiceProxy : DispatchProxy
{
    private Func<string, object?[], object?> _invoke = null!;

    internal static T Create<T>(Func<string, object?[], object?> invoke) where T : class
    {
        var proxy = Create<T, ScriptServiceProxy>();
        ((ScriptServiceProxy)(object)proxy)._invoke = invoke;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args) => _invoke(method!.Name, args ?? []);
}

internal sealed class ScriptDataManager : IDataManager
{
    public ClientLanguage Language => throw new NotSupportedException();
    public Lumina.GameData GameData => throw new NotSupportedException();
    public ExcelModule Excel => throw new NotSupportedException();
    public bool HasModifiedGameDataFiles => throw new NotSupportedException();
    public ExcelSheet<T> GetExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelRow<T> => null!;
    public SubrowExcelSheet<T> GetSubrowExcelSheet<T>(ClientLanguage? language = null, string? name = null) where T : struct, IExcelSubrow<T> => throw new NotSupportedException();
    public FileResource? GetFile(string path) => throw new NotSupportedException();
    public T? GetFile<T>(string path) where T : FileResource => throw new NotSupportedException();
    public Task<T> GetFileAsync<T>(string path, CancellationToken cancellationToken) where T : FileResource => throw new NotSupportedException();
    public bool FileExists(string path) => throw new NotSupportedException();
}
