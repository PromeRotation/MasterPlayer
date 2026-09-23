using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.MechanicHost;

public sealed class MechanicHostController : IDisposable
{
    private const int WaymarkSlotCount = 8;

    private readonly List<IDisposable> _subscriptions = [];
    private readonly (byte Id, Vector3? Pos)[] _waymarks = new (byte, Vector3?)[WaymarkSlotCount];
    private MechanicSession? _session;
    private MechanicWorldFeed? _feed;
    private ActionEffectCapture? _capture;
    private MechanicDrawHost? _drawBridge;
    private MechanicNavigationHost? _navBridge;
    private bool _disposed;
    private string? _lastLoggedError;

    public bool IsAvailable { get; private set; }
    public bool IsSuppressed { get; private set; }
    public bool CaptureActive => _capture?.IsActive == true;
    public string? LastError { get; private set; }
    public int LoadedModuleCount => _session?.BossModules.LoadedModules.Count ?? 0;
    public int PendingModuleCount => _session?.BossModules.PendingModules.Count ?? 0;
    public string? ActiveModuleName => _session?.BossModules.ActiveModule?.GetType().Name;
    public string? ActiveZoneModuleName => _session?.ZoneModules.ActiveModule?.GetType().Name;
    public IReadOnlyList<(string Text, bool IsRisk)> CurrentHints => _drawBridge?.CurrentHints ?? [];
    public IReadOnlyList<MechanicRadarExtra> DrawExtras => _drawBridge?.Extras ?? [];
    public string? DrawExtrasKey => _drawBridge?.ExtrasKey;
    public string? DrawModuleName => _drawBridge?.ActiveDrawModuleName;
    public int CaptureErrorCount => _drawBridge?.CaptureErrorCount ?? 0;
    public string? DrawCaptureError => _drawBridge?.LastCaptureError;
    public bool HasActiveModule => _session?.BossModules.ActiveModule?.StateMachine.ActivePhase != null;
    public bool NavigationActive => _navBridge?.NavigationActive == true;
    public int NavDangerZoneCount => _navBridge?.DangerZoneCount ?? 0;
    public int NavObstacleCount => _navBridge?.ObstacleZoneCount ?? 0;
    public bool NavBoundaryActive => _navBridge?.BoundaryActive == true;
    public string? SpecialModeHint => _navBridge?.SpecialModeHint;
    public string? PredictedDamageText => _navBridge?.PredictedDamageText;
    public MechanicTimelineSnapshot? CurrentTimeline { get; private set; }
    public DateTime MechanicClockUtc
    {
        get
        {
            var time = _session?.World.CurrentTime ?? default;
            return time == default ? DateTime.UtcNow : time;
        }
    }

    public MechanicHostController()
    {
        try
        {
            MechanicRuntimeEnvironment.Initialize(Svc.Data.GameData, Svc.Log);
            ApplyMaturity(MasterPlayerSettings.Instance.MechanicModuleMinMaturity);
            _session = new MechanicSession(ResolveGameVersion());
            _feed = new MechanicWorldFeed(_session);
            _capture = new ActionEffectCapture();
            _navBridge = new MechanicNavigationHost(_session);
            _drawBridge = new MechanicDrawHost(_session);
            SubscribeEvents();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Svc.Log.Error(ex, "[MasterPlayer] 机制运行库初始化失败，机制模块宿主已停用");
        }
    }

    private static void ApplyMaturity(int value)
    {
        var maturity = (BossMod.BossModuleInfo.Maturity)Math.Clamp(value,
            (int)BossMod.BossModuleInfo.Maturity.Dummy, (int)BossMod.BossModuleInfo.Maturity.AISupport);
        if (BossMod.BossModuleManager.Config.MinMaturity != maturity)
            BossMod.BossModuleManager.Config.MinMaturity = maturity;
    }

    private static string ResolveGameVersion()
    {
        try
        {
            if (Svc.Data.GameData.Repositories.TryGetValue("ffxiv", out var repo) && !string.IsNullOrEmpty(repo.Version))
                return repo.Version;
        }
        catch
        {
        }
        return "live";
    }

    private void SubscribeEvents()
    {
        _subscriptions.Add(PromeEventBus.OnTether(this, e => Enqueue(new MechanicEventTether(e.SourceId, e.TetherId, e.TargetId))));
        _subscriptions.Add(PromeEventBus.OnTetherCancel(this, e => Enqueue(new MechanicEventTetherCancel(e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnTargetIcon(this, e => Enqueue(new MechanicEventIcon(e.MarkerId, e.TargetId, e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnTargetVfx(this, e => Enqueue(new MechanicEventVfx(e.VfxId, 0, e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnMarker(this, e =>
        {
            if (e.MarkerId is >= 1 and <= (uint)BossMod.Sign.Count)
                Enqueue(new MechanicEventHeadMarker((byte)(e.MarkerId - 1), e.TargetId));
        }));
        _subscriptions.Add(PromeEventBus.OnMapEffect(this, e =>
            Enqueue(new MechanicEventMapEffect((byte)e.Position, e.Param1 | ((uint)e.Param2 << 16)))));
        _subscriptions.Add(PromeEventBus.OnEObjAnimation(this, e => Enqueue(new MechanicEventEObjAnimation(e.SourceId, e.Param1, e.Param2))));
        _subscriptions.Add(PromeEventBus.OnEObjSetState(this, e => Enqueue(new MechanicEventEObjSetState(e.SourceId, e.State))));
        _subscriptions.Add(PromeEventBus.OnDirectorUpdate(this, e =>
            Enqueue(new MechanicEventDirectorUpdate(e.Instance, e.Command, e.Param1, e.Param2, e.Param3, e.Param4))));
        _subscriptions.Add(PromeEventBus.OnNpcYell(this, e => Enqueue(new MechanicEventNpcYell(e.SourceId, e.NpcYellId))));
        _subscriptions.Add(PromeEventBus.OnSystemLog(this, id => Enqueue(new MechanicEventSystemLog(id, []))));
        _subscriptions.Add(PromeEventBus.OnPlayActionTimeline(this, e =>
        {
            if (e.TimelineId <= ushort.MaxValue)
                Enqueue(new MechanicEventPlayActionTimeline(e.SourceId, (ushort)e.TimelineId));
        }));
        _subscriptions.Add(PromeEventBus.OnLogEvent<LogSystemWaymarkEvent>(this, e =>
        {
            if (e.MarkerId < WaymarkSlotCount)
                _waymarks[e.MarkerId] = (e.MarkerId, e.Active ? e.Position : null);
        }));
    }

    private void Enqueue(MechanicFeedEvent feedEvent)
    {
        if (CanAcceptEvents)
            _feed?.Enqueue(feedEvent);
    }

    private bool CanAcceptEvents
        => !_disposed && IsAvailable && _feed != null && MasterPlayerSettings.Instance.EnableMechanicModules
            && !IsSuppressed && MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive != true;

    public void Update()
    {
        if (_disposed)
            return;
        var settings = MasterPlayerSettings.Instance;
        IsSuppressed = ComputeSuppressed();
        if (!IsAvailable || _session == null || _feed == null || !settings.EnableMechanicModules || IsSuppressed
            || MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true)
        {
            _capture?.Drain(null);
            _navBridge?.Sleep();
            _drawBridge?.Sleep();
            CurrentTimeline = null;
            return;
        }

        try
        {
            ApplyMaturity(settings.MechanicModuleMinMaturity);
            var now = DateTime.UtcNow;
            var frame = LiveWorldSampler.Capture(_waymarks);
            _capture?.Drain(_feed);
            _session.BeginFrame(now);
            _feed.ApplyFrame(frame);
            _feed.Flush();
            _session.Update();
            _navBridge?.Update();
            _drawBridge?.Update();
            UpdateTimeline();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (ex.Message != _lastLoggedError)
            {
                _lastLoggedError = ex.Message;
                Svc.Log.Error(ex, "[MasterPlayer] 机制模块宿主帧更新失败");
            }
        }
    }

    private void UpdateTimeline()
    {
        var module = _session?.BossModules.ActiveModule;
        if (module?.StateMachine.ActivePhase == null)
        {
            CurrentTimeline = null;
            return;
        }
        try
        {
            CurrentTimeline = MechanicTimeline.Capture(module);
        }
        catch (Exception ex)
        {
            CurrentTimeline = null;
            var message = $"timeline:{ex.Message}";
            if (_lastLoggedError != message)
            {
                _lastLoggedError = message;
                Svc.Log.Warning($"[MasterPlayer] 机制模块 {module.GetType().Name} 时间轴捕获失败：{ex.Message}");
            }
        }
    }

    private static bool ComputeSuppressed()
    {
        var scripts = MasterPlayerPlugin.Instance?.Scripts;
        if (scripts == null)
            return false;
        var territory = (ushort)Svc.ClientState.TerritoryType;
        foreach (var running in scripts.Running.Values)
        {
            var territorys = running.Descriptor.Metadata.Territorys;
            if (territorys.Length != 0 && territorys.Contains(territory))
                return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var subscription in _subscriptions)
        {
            try
            {
                subscription.Dispose();
            }
            catch (Exception ex)
            {
                try { Svc.Log.Warning($"[MasterPlayer] 机制模块宿主退订失败：{ex.Message}"); }
                catch { }
            }
        }
        _subscriptions.Clear();
        CurrentTimeline = null;
        try
        {
            _capture?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] 机制效果包捕获释放失败：{ex.Message}"); }
            catch { }
        }
        _capture = null;
        try
        {
            _navBridge?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] 机制导航宿主释放失败：{ex.Message}"); }
            catch { }
        }
        _navBridge = null;
        try
        {
            _drawBridge?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] 机制绘制宿主释放失败：{ex.Message}"); }
            catch { }
        }
        _drawBridge = null;
        try
        {
            _session?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] 机制会话释放失败：{ex.Message}"); }
            catch { }
        }
        _session = null;
        _feed = null;
        IsAvailable = false;
    }
}
