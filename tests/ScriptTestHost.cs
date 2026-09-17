using System.Reflection;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using Lumina.Data;
using Lumina.Excel;
using PromeRotation.LogSystem;
using PromeRotation.Spatial.Drawing;
using PromeRotation.Spatial.Geometry;
using PromeRotation.Spatial.SafeZone;

namespace MasterPlayer.Scripting;

// Compile the real standalone script against a headless host; geometry and area lifetimes use PR itself.
[AttributeUsage(AttributeTargets.Class)]
internal sealed class MechanicScriptAttribute : Attribute
{
    public MechanicScriptAttribute(string name, uint[] territorys, string guid, string version, string author, string note)
        => _ = (name, territorys, guid, version, author, note);
}

public abstract class MasterPlayerScript
{
    public ScriptSafeZoneApi SafeZone { get; } = new();
    public ScriptDrawApi Draw { get; } = new();
    public List<string> Errors { get; } = [];
    protected virtual void OnStart() { }
    protected virtual void OnUpdate(float deltaTime) { }
    protected virtual void OnStop() { }
    public virtual IEnumerable<string> GetMechanisms() => [];
    public bool IsMechanismEnabled(string mechanism) => mechanism != "文字提示";
    protected void Log(string text) => throw new NotSupportedException(text);
    protected void LogError(string text) => Errors.Add(text);
    protected void SuppressGenericDodge(params uint[] actionIds) { }
    protected void OnStartCast(Action<LogSystemCastStartEvent> callback) => throw new NotSupportedException();
    protected void OnCancelCast(Action<LogSystemCancelCastEvent> callback) => throw new NotSupportedException();
    protected void OnInterruptCast(Action<LogSystemInterruptCastEvent> callback) => throw new NotSupportedException();
    protected void OnActionEffect(Action<LogSystemActionEffectEvent> callback) => throw new NotSupportedException();
    protected void OnTether(Action<LogSystemTetherEvent> callback) => throw new NotSupportedException();
    protected void OnTetherCancel(Action<LogSystemTetherCancelEvent> callback) => throw new NotSupportedException();
    protected void OnEObjAnimation(Action<LogSystemEObjAnimationEvent> callback) => throw new NotSupportedException();
    protected void OnEObjSetState(Action<LogSystemEObjSetStateEvent> callback) => throw new NotSupportedException();
    protected void OnMapEffect(Action<LogSystemMapEffectEvent> callback) => throw new NotSupportedException();
}

public sealed class ScriptSafeZoneApi : IDisposable
{
    public SafeZoneManager Manager { get; } = new(new SafeZoneSetting { EnableSafeZone = true });
    private SafeZoneRegistry Registry => new(Manager);
    public IDisposable BeginDrawingGroup(string name) => new Scope();
    public SafeZoneAreaHandle AddDelayedHit(GeometryTemplate template, TransformBinding binding, float delaySeconds, float hitDurationSeconds = 0.6f)
        => Registry.AddDelayedHit(template, binding, delaySeconds, hitDurationSeconds);
    public SafeZoneAreaHandle AddPersistentDanger(GeometryTemplate template, TransformBinding binding)
        => Registry.AddPersistentDanger(template, binding);
    public SafeZoneAreaHandle AddTimedSafeArea(GeometryTemplate template, TransformBinding binding, float durationSeconds,
        int priority = 0, PositionPreference preference = PositionPreference.Nearest)
        => Registry.AddTimedSafeArea(template, binding, durationSeconds, priority, preference);
    public SafeZoneAreaHandle AddObstacle(GeometryTemplate template, TransformBinding binding) => Registry.AddObstacle(template, binding);
    public bool Remove(SafeZoneAreaHandle handle) => Registry.Remove(handle);
    public void Dispose() => Manager.Dispose();
    private sealed class Scope : IDisposable { public void Dispose() { } }
}

public sealed class ScriptDrawApi
{
    public void Add(string id, IGeometryShape geometry, DrawStyle? style = null, float durationMs = 0f) { }
    public void AddAoe(string id, IGeometryShape geometry, bool safe = false, float durationMs = 0f) { }
    public void AddTether(string id, object start, object end, DrawStyle? style = null, float durationMs = 0f) { }
    public bool Remove(string id) => true;
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
