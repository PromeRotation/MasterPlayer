using ECommons.DalamudServices;
using MasterPlayer.Scripting;

namespace MasterPlayer.Replay;

/// <summary>
/// 回放控制器：按录制时间轴恢复对象快照，再将事件经 ReplayScriptEventRouter 派发给脚本。
/// 回放会话 = 停掉实时脚本 → 按录制区域重启启用脚本（回放模式）→ 主线程时钟驱动派发；
/// 跳转重建对象与脚本状态；停止才 ExitReplayMode（下一帧恢复实时脚本）。
/// 合成事件只在脚本层派发，不进 PromeEventBus，DodgeController/Follow 等不响应回放。
/// </summary>
public sealed class ReplayController : IDisposable
{
    private readonly ScriptManager _scripts;
    private readonly ReplayRecorder _recorder;
    private readonly MechanicHost.MechanicHostController? _mechanicHost;
    private readonly List<(string ScriptId, ReplayScriptEventRouter Router)> _routers = [];
    private ReplayRecording? _recording;
    private ReplayPlayback? _playback;
    private bool _disposed;

    internal ReplayController(ScriptManager scripts, ReplayRecorder recorder, MechanicHost.MechanicHostController? mechanicHost = null)
    {
        _scripts = scripts;
        _recorder = recorder;
        _mechanicHost = mechanicHost;
    }

    public bool SessionActive { get; private set; }

    public bool Playing { get; private set; }

    /// <summary>回放时钟（相对录制开始的秒数）。</summary>
    public double Clock { get; private set; }

    public float Speed { get; private set; } = 1f;

    public string StatusMessage { get; private set; } = "未加载录制。";

    public string? LoadedFilePath { get; private set; }

    public ReplayRecording? Recording => _recording;
    private string WorldWarning => _recording?.WorldFrames.Count == 0 ? " 旧录制缺少对象状态，场景不能完整还原。" : "";

    public int NextEventIndex => _playback?.NextEventIndex ?? 0;
    public ReplayWorldState? World => SessionActive ? _playback?.World : null;

    public int ReplayScriptCount => _routers.Count;

    public int RouterErrorCount
    {
        get
        {
            var count = 0;
            foreach (var (_, router) in _routers)
                count += router.ErrorCount;
            return count;
        }
    }

    public bool Load(string path)
    {
        StopSession();
        try
        {
            _recording = ReplayRecordingReader.Load(path);
            LoadedFilePath = path;
            Clock = 0;
            _playback = null;
            var skipped = _recording.SkippedEvents > 0 ? $"，跳过 {_recording.SkippedEvents} 条未知事件" : "";
            StatusMessage = $"已加载：{Path.GetFileName(path)}（{_recording.Events.Count} 个事件，时长 {_recording.Duration:F1} 秒{skipped}）。" + WorldWarning;
            return true;
        }
        catch (Exception ex)
        {
            _recording = null;
            LoadedFilePath = null;
            StatusMessage = $"加载录制失败：{ex.Message}";
            Svc.Log.Error(ex, $"[MasterPlayer] 加载录制失败：{path}");
            return false;
        }
    }

    public void Play()
    {
        if (_recording == null || _disposed)
            return;
        if (!SessionActive && !StartSession())
            return;
        Playing = true;
    }

    public void Pause() => Playing = false;

    public void SetSpeed(float speed)
    {
        if (!float.IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
        Speed = speed;
    }

    /// <summary>跳转到指定秒数：重启回放脚本后从开头补发事件（BMR 式重置 + 快进）。</summary>
    public void Seek(double t)
    {
        if (_recording == null || _disposed || !double.IsFinite(t))
            return;

        var resume = Playing;
        if (!SessionActive)
        {
            if (!StartSession())
                return;
        }
        else
        {
            RestartSessionScripts();
        }

        if (DispatchUpTo(Math.Clamp(t, 0d, _recording.Duration))) Playing = resume;
    }

    public void StopSession()
    {
        if (!SessionActive)
            return;

        // 只停仍是回放会话的脚本；被「刷新脚本」等外部重启为实时模式的同 id 脚本不动。
        foreach (var (id, _) in _routers.ToArray())
            if (_scripts.Running.TryGetValue(id, out var run) && run.IsReplaySession)
                _scripts.StopScript(id);
        _routers.Clear();

        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        runtime.ReplayClock = null;
        runtime.Manager.EntityPoseOverride = null;
        _mechanicHost?.ExitReplaySession();
        SessionActive = false;
        Playing = false;
        Clock = 0;
        _playback = null;
        _scripts.ExitReplayMode();
        StatusMessage = "已停止回放。";
    }

    /// <summary>主线程驱动（插件 Update 中、脚本 Update 之前调用）：推进时钟并派发到期事件。</summary>
    public void Update(float deltaTime)
    {
        if (!SessionActive)
            return;

        _scripts.EnforceReplayActivation();
        // 回放脚本被外部停光（刷新脚本、关总开关等）时自动结束会话，避免门控卡死。
        // 零脚本的会话（无匹配录制区域的启用脚本）允许存活，时间轴照常推进。
        if (_routers.Count > 0 && !_routers.Any(r => _scripts.Running.TryGetValue(r.ScriptId, out var run) && run.IsReplaySession))
        {
            StopSession();
            StatusMessage = "回放脚本已全部停止，回放结束。";
            return;
        }

        if (!Playing || _recording == null || !float.IsFinite(deltaTime))
            return;

        if (!DispatchUpTo(Clock + Math.Max(0d, deltaTime) * Speed)) return;

        if (Clock >= _recording.Duration)
        {
            Playing = false;
            StatusMessage = "回放结束（已到文件末尾）。" + WorldWarning;
        }
    }

    private bool StartSession()
    {
        if (_recording == null || SessionActive)
            return false;
        if (_recorder.IsRecording)
        {
            StatusMessage = "录制进行中，无法开始回放。";
            return false;
        }

        _scripts.StopAll();
        _scripts.EnterReplayMode();
        _playback = new(_recording);
        SessionActive = true;
        var runtime = MasterPlayerPlugin.Instance!.SafeZone;
        runtime.ReplayClock = () => World!.CurrentTime;
        runtime.Manager.EntityPoseOverride = id => World!.Actors.TryGetValue(id, out var actor) ? (actor.Position, actor.Rotation) : null;
        Clock = 0;
        _mechanicHost?.EnterReplaySession(_recording.Header.Territory);
        StartSessionScripts();
        Playing = false;
        if (!DispatchUpTo(0)) return false;

        StatusMessage = (_routers.Count == 0
            ? "回放会话已启动，但没有匹配录制区域的已启用脚本（仅时间轴推进）。"
            : $"回放会话已启动：{_routers.Count} 个脚本。") + WorldWarning;
        return true;
    }

    private void RestartSessionScripts()
    {
        foreach (var (id, _) in _routers.ToArray())
            if (_scripts.Running.TryGetValue(id, out var run) && run.IsReplaySession)
                _scripts.StopScript(id);
        _routers.Clear();
        _scripts.EnterReplayMode();
        _playback = new(_recording!);
        _mechanicHost?.RestartReplaySession();
        Clock = 0;
        StartSessionScripts();
    }

    private void StartSessionScripts()
    {
        var territory = _recording!.Header.Territory;
        foreach (var descriptor in _scripts.Scripts)
        {
            var id = descriptor.Metadata.Id;
            if (!_scripts.IsScriptEnabled(id))
                continue;
            if (!ScriptManager.IsTerritoryMatched(descriptor.Metadata, territory))
                continue;

            var router = new ReplayScriptEventRouter(_playback!.World);
            if (_scripts.StartScript(id, router))
                _routers.Add((id, router));
            else
                router.Dispose();
        }
    }

    private bool DispatchUpTo(double clock)
    {
        if (_recording == null)
            return false;

        try
        {
            _playback!.AdvanceTo(clock, ev =>
            {
                _mechanicHost?.OnReplayEvent(ev);
                foreach (var (_, router) in _routers)
                    router.Raise(ev);
            }, dt =>
            {
                if (_playback != null)
                    _mechanicHost?.OnReplayTick(_playback.World);
                _scripts.UpdateReplayScripts(dt);
            });
            Clock = _playback.Clock;
            return true;
        }
        catch (Exception ex)
        {
            StopSession();
            StatusMessage = $"回放失败，已停止：{ex.Message}";
            Svc.Log.Error(ex, "[MasterPlayer] 回放对象或事件失败");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopSession();
    }
}
