using System.Numerics;
using ECommons.DalamudServices;
using MasterPlayer.BmrRuntime;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.BmrBridge;

public sealed class BmrBridgeController : IDisposable
{
    private const int WaymarkSlotCount = 8;

    private readonly List<IDisposable> _subscriptions = [];
    private readonly (byte Id, Vector3? Pos)[] _waymarks = new (byte, Vector3?)[WaymarkSlotCount];
    private BmrSession? _session;
    private BmrWorldFeed? _feed;
    private BmrActionEffectCapture? _capture;
    private BmrDrawBridge? _drawBridge;
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
    public IReadOnlyList<BmrRadarExtra> DrawExtras => _drawBridge?.Extras ?? [];
    public string? DrawExtrasKey => _drawBridge?.ExtrasKey;
    public string? DrawModuleName => _drawBridge?.ActiveDrawModuleName;
    public int CaptureErrorCount => _drawBridge?.CaptureErrorCount ?? 0;
    public string? DrawCaptureError => _drawBridge?.LastCaptureError;

    public BmrBridgeController()
    {
        try
        {
            BmrRuntimeEnvironment.Initialize(Svc.Data.GameData, Svc.Log);
            ApplyMaturity(MasterPlayerSettings.Instance.BmrMinMaturity);
            _session = new BmrSession(ResolveGameVersion());
            _feed = new BmrWorldFeed(_session);
            _capture = new BmrActionEffectCapture();
            _drawBridge = new BmrDrawBridge(_session);
            SubscribeEvents();
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            Svc.Log.Error(ex, "[MasterPlayer] BMR 运行库初始化失败，BMR 机制桥已停用");
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
        _subscriptions.Add(PromeEventBus.OnTether(this, e => Enqueue(new BmrEventTether(e.SourceId, e.TetherId, e.TargetId))));
        _subscriptions.Add(PromeEventBus.OnTetherCancel(this, e => Enqueue(new BmrEventTetherCancel(e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnTargetIcon(this, e => Enqueue(new BmrEventIcon(e.MarkerId, e.TargetId, e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnTargetVfx(this, e => Enqueue(new BmrEventVfx(e.VfxId, 0, e.SourceId))));
        _subscriptions.Add(PromeEventBus.OnMarker(this, e =>
        {
            if (e.MarkerId is >= 1 and <= (uint)BossMod.Sign.Count)
                Enqueue(new BmrEventHeadMarker((byte)(e.MarkerId - 1), e.TargetId));
        }));
        _subscriptions.Add(PromeEventBus.OnMapEffect(this, e =>
            Enqueue(new BmrEventMapEffect((byte)e.Position, e.Param1 | ((uint)e.Param2 << 16)))));
        _subscriptions.Add(PromeEventBus.OnEObjAnimation(this, e => Enqueue(new BmrEventEObjAnimation(e.SourceId, e.Param1, e.Param2))));
        _subscriptions.Add(PromeEventBus.OnEObjSetState(this, e => Enqueue(new BmrEventEObjSetState(e.SourceId, e.State))));
        _subscriptions.Add(PromeEventBus.OnDirectorUpdate(this, e =>
            Enqueue(new BmrEventDirectorUpdate(e.Instance, e.Command, e.Param1, e.Param2, e.Param3, e.Param4))));
        _subscriptions.Add(PromeEventBus.OnNpcYell(this, e => Enqueue(new BmrEventNpcYell(e.SourceId, e.NpcYellId))));
        _subscriptions.Add(PromeEventBus.OnSystemLog(this, id => Enqueue(new BmrEventSystemLog(id, []))));
        _subscriptions.Add(PromeEventBus.OnPlayActionTimeline(this, e =>
        {
            if (e.TimelineId <= ushort.MaxValue)
                Enqueue(new BmrEventPlayActionTimeline(e.SourceId, (ushort)e.TimelineId));
        }));
        _subscriptions.Add(PromeEventBus.OnLogEvent<LogSystemWaymarkEvent>(this, e =>
        {
            if (e.MarkerId < WaymarkSlotCount)
                _waymarks[e.MarkerId] = (e.MarkerId, e.Active ? e.Position : null);
        }));
    }

    private void Enqueue(BmrFeedEvent feedEvent)
    {
        if (CanAcceptEvents)
            _feed?.Enqueue(feedEvent);
    }

    private bool CanAcceptEvents
        => !_disposed && IsAvailable && _feed != null && MasterPlayerSettings.Instance.EnableBmrBridge
            && !IsSuppressed && MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive != true;

    public void Update()
    {
        if (_disposed)
            return;
        var settings = MasterPlayerSettings.Instance;
        IsSuppressed = ComputeSuppressed();
        if (!IsAvailable || _session == null || _feed == null || !settings.EnableBmrBridge || IsSuppressed
            || MasterPlayerPlugin.Instance?.Scripts?.ReplayModeActive == true)
        {
            _capture?.Drain(null);
            _drawBridge?.Sleep();
            return;
        }

        try
        {
            ApplyMaturity(settings.BmrMinMaturity);
            var now = DateTime.UtcNow;
            var frame = BmrLiveSampler.Capture(_waymarks);
            _capture?.Drain(_feed);
            _session.BeginFrame(now);
            _feed.ApplyFrame(frame);
            _feed.Flush();
            _session.Update();
            _drawBridge?.Update();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            if (ex.Message != _lastLoggedError)
            {
                _lastLoggedError = ex.Message;
                Svc.Log.Error(ex, "[MasterPlayer] BMR 机制桥帧更新失败");
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
                try { Svc.Log.Warning($"[MasterPlayer] BMR 机制桥退订失败：{ex.Message}"); }
                catch { }
            }
        }
        _subscriptions.Clear();
        try
        {
            _capture?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] BMR 效果包捕获释放失败：{ex.Message}"); }
            catch { }
        }
        _capture = null;
        try
        {
            _drawBridge?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] BMR 绘制桥释放失败：{ex.Message}"); }
            catch { }
        }
        _drawBridge = null;
        try
        {
            _session?.Dispose();
        }
        catch (Exception ex)
        {
            try { Svc.Log.Warning($"[MasterPlayer] BMR 会话释放失败：{ex.Message}"); }
            catch { }
        }
        _session = null;
        _feed = null;
        IsAvailable = false;
    }
}
