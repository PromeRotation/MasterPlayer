using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.MechanicRuntime;
using MasterPlayer.Replay;
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
    private MechanicSession? _replaySession;
    private MechanicWorldFeed? _replayFeed;
    private ReplayFeedAdapter? _replayAdapter;
    private ushort _replayTerritory;
    private bool _disposed;
    private string? _lastLoggedError;

    private MechanicSession? ActiveSession => _replaySession ?? _session;

    public bool IsAvailable { get; private set; }
    public bool IsSuppressed { get; private set; }
    public bool CaptureActive => _capture?.IsActive == true;
    public string? LastError { get; private set; }
    public int LoadedModuleCount => ActiveSession?.BossModules.LoadedModules.Count ?? 0;
    public int PendingModuleCount => ActiveSession?.BossModules.PendingModules.Count ?? 0;
    public string? ActiveModuleName => ActiveSession?.BossModules.ActiveModule?.GetType().Name;
    public string? ActiveZoneModuleName => ActiveSession?.ZoneModules.ActiveModule?.GetType().Name;
    public IReadOnlyList<(string Text, bool IsRisk)> CurrentHints => _drawBridge?.CurrentHints ?? [];
    public IReadOnlyList<MechanicRadarExtra> DrawExtras => _drawBridge?.Extras ?? [];
    public string? DrawExtrasKey => _drawBridge?.ExtrasKey;
    public string? DrawModuleName => _drawBridge?.ActiveDrawModuleName;
    public int CaptureErrorCount => _drawBridge?.CaptureErrorCount ?? 0;
    public string? DrawCaptureError => _drawBridge?.LastCaptureError;
    public bool HasActiveModule => ActiveSession?.BossModules.ActiveModule?.StateMachine.ActivePhase != null;
    public bool NavigationActive => _navBridge?.NavigationActive == true;
    public int NavDangerZoneCount => _navBridge?.DangerZoneCount ?? 0;
    public int NavGoalZoneCount => _navBridge?.GoalZoneCount ?? 0;
    public int NavObstacleCount => _navBridge?.ObstacleZoneCount ?? 0;
    public bool NavBoundaryActive => _navBridge?.BoundaryActive == true;
    public string? SpecialModeHint => _navBridge?.SpecialModeHint;
    public string? PredictedDamageText => _navBridge?.PredictedDamageText;
    public MechanicTimelineSnapshot? CurrentTimeline { get; private set; }
    public DateTime MechanicClockUtc
    {
        get
        {
            var time = ActiveSession?.World.CurrentTime ?? default;
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
        _subscriptions.Add(PromeEventBus.OnTether(this, e => Enqueue(MechanicEventFactory.FromTether(e))));
        _subscriptions.Add(PromeEventBus.OnTetherCancel(this, e => Enqueue(MechanicEventFactory.FromTetherCancel(e))));
        _subscriptions.Add(PromeEventBus.OnTargetIcon(this, e => Enqueue(MechanicEventFactory.FromTargetIcon(e))));
        _subscriptions.Add(PromeEventBus.OnTargetVfx(this, e => Enqueue(MechanicEventFactory.FromTargetVfx(e))));
        _subscriptions.Add(PromeEventBus.OnMarker(this, e =>
        {
            if (MechanicEventFactory.FromMarker(e) is { } marker)
                Enqueue(marker);
        }));
        _subscriptions.Add(PromeEventBus.OnMapEffect(this, e => Enqueue(MechanicEventFactory.FromMapEffect(e))));
        _subscriptions.Add(PromeEventBus.OnEObjAnimation(this, e => Enqueue(MechanicEventFactory.FromEObjAnimation(e))));
        _subscriptions.Add(PromeEventBus.OnEObjSetState(this, e => Enqueue(MechanicEventFactory.FromEObjSetState(e))));
        _subscriptions.Add(PromeEventBus.OnDirectorUpdate(this, e => Enqueue(MechanicEventFactory.FromDirectorUpdate(e))));
        _subscriptions.Add(PromeEventBus.OnNpcYell(this, e => Enqueue(MechanicEventFactory.FromNpcYell(e))));
        _subscriptions.Add(PromeEventBus.OnSystemLog(this, id => Enqueue(MechanicEventFactory.FromSystemLog(id))));
        _subscriptions.Add(PromeEventBus.OnPlayActionTimeline(this, e =>
        {
            if (MechanicEventFactory.FromPlayActionTimeline(e) is { } timeline)
                Enqueue(timeline);
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
        if (!IsAvailable || _session == null || _feed == null || !settings.EnableMechanicModules || IsSuppressed)
        {
            if (_replaySession == null)
            {
                _capture?.Drain(null);
                _navBridge?.Sleep();
                _drawBridge?.Sleep();
                CurrentTimeline = null;
            }
            return;
        }
        if (MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true)
            return;

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

    internal void EnterReplaySession(ushort territory)
    {
        if (!IsAvailable || _session == null || _disposed)
            return;
        ExitReplaySession();
        try
        {
            _replayTerritory = territory;
            _replaySession = new MechanicSession(ResolveGameVersion());
            _replayFeed = new MechanicWorldFeed(_replaySession);
            _replayAdapter = new ReplayFeedAdapter(territory, LiveWorldSampler.ResolveCfc);
            _navBridge?.SwitchSession(_replaySession);
            _drawBridge?.SwitchSession(_replaySession);
        }
        catch (Exception ex)
        {
            ExitReplaySession();
            LastError = ex.Message;
            Svc.Log.Error(ex, "[MasterPlayer] 机制模块回放会话创建失败");
        }
    }

    internal void RestartReplaySession()
    {
        if (_replaySession == null)
            return;
        DisposeReplayCore();
        _replaySession = new MechanicSession(ResolveGameVersion());
        _replayFeed = new MechanicWorldFeed(_replaySession);
        _replayAdapter = new ReplayFeedAdapter(_replayTerritory, LiveWorldSampler.ResolveCfc);
        _navBridge?.SwitchSession(_replaySession);
        _drawBridge?.SwitchSession(_replaySession);
    }

    internal void ExitReplaySession()
    {
        if (_replaySession == null)
            return;
        DisposeReplayCore();
        _replaySession = null;
        _replayFeed = null;
        _replayAdapter = null;
        if (_session != null)
        {
            _navBridge?.SwitchSession(_session);
            _drawBridge?.SwitchSession(_session);
        }
        CurrentTimeline = null;
    }

    private void DisposeReplayCore()
    {
        try { _replaySession?.Dispose(); }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] 机制模块回放会话释放失败：{ex.Message}"); }
            catch { }
        }
    }

    internal void OnReplayEvent(LogSystemEvent ev)
    {
        if (_replayFeed == null || _replayAdapter == null || IsReplaySuppressed())
            return;
        try
        {
            if (_replayAdapter.TryConvertEvent(ev) is { } feedEvent)
                _replayFeed.Enqueue(feedEvent);
        }
        catch (Exception ex)
        {
            LogReplayError("event", ex);
        }
    }

    internal void OnReplayTick(ReplayWorldState world)
    {
        if (_replaySession == null || _replayFeed == null || _replayAdapter == null)
            return;
        if (IsReplaySuppressed())
        {
            _navBridge?.Sleep();
            _drawBridge?.Sleep();
            CurrentTimeline = null;
            return;
        }
        try
        {
            _replaySession.BeginFrame(world.CurrentTime);
            _replayFeed.ApplyFrame(_replayAdapter.ConvertFrame(world));
            foreach (var frameEvent in _replayAdapter.DrainFrameEvents())
                _replayFeed.Enqueue(frameEvent);
            if (_replayAdapter.FlushPendingEffect() is { } effect)
                _replayFeed.Enqueue(effect);
            _replayFeed.Flush();
            _replaySession.Update();
            _navBridge?.Update();
            _drawBridge?.Update();
            UpdateTimeline();
        }
        catch (Exception ex)
        {
            LogReplayError("frame", ex);
        }
    }

    private bool IsReplaySuppressed()
    {
        var scripts = MasterPlayerPlugin.Instance?.Scripts;
        if (scripts == null)
            return false;
        foreach (var running in scripts.Running.Values)
        {
            if (running.Descriptor.Metadata.Territorys.Length != 0)
                return true;
        }
        return false;
    }

    private void LogReplayError(string stage, Exception ex)
    {
        LastError = ex.Message;
        var message = $"replay-{stage}:{ex.Message}";
        if (_lastLoggedError != message)
        {
            _lastLoggedError = message;
            Svc.Log.Warning($"[MasterPlayer] 机制模块回放驱动失败（{stage}）：{ex.Message}");
        }
    }

    private void UpdateTimeline()
    {
        var module = ActiveSession?.BossModules.ActiveModule;
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
        ExitReplaySession();
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
