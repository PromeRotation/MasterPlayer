using ECommons.DalamudServices;
using MasterPlayer.Scripting;

namespace MasterPlayer.Replay;

/// <summary>
/// 回放控制器：加载录制文件，按录制时间轴把合成事件经 ReplayScriptEventRouter 喂给回放会话中的脚本。
/// 回放会话 = 停掉实时脚本 → 按录制区域重启启用脚本（回放模式）→ 主线程时钟驱动派发；
/// 结束/跳转 = 停回放脚本并 ExitReplayMode（下一帧区域自动启停恢复实时脚本）。
/// 合成事件只在脚本层派发，不进 PromeEventBus，DodgeController/Follow 等不响应回放。
/// </summary>
public sealed class ReplayController : IDisposable
{
    private readonly ScriptManager _scripts;
    private readonly ReplayRecorder _recorder;
    private readonly List<(string ScriptId, ReplayScriptEventRouter Router)> _routers = [];
    private ReplayRecording? _recording;
    private int _nextIndex;
    private bool _disposed;

    internal ReplayController(ScriptManager scripts, ReplayRecorder recorder)
    {
        _scripts = scripts;
        _recorder = recorder;
    }

    public bool SessionActive { get; private set; }

    public bool Playing { get; private set; }

    /// <summary>回放时钟（相对录制开始的秒数）。</summary>
    public double Clock { get; private set; }

    public float Speed { get; private set; } = 1f;

    public string StatusMessage { get; private set; } = "未加载录制。";

    public string? LoadedFilePath { get; private set; }

    public ReplayRecording? Recording => _recording;

    public int NextEventIndex => _nextIndex;

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
            _nextIndex = 0;
            var skipped = _recording.SkippedEvents > 0 ? $"，跳过 {_recording.SkippedEvents} 条未知事件" : "";
            StatusMessage = $"已加载：{Path.GetFileName(path)}（{_recording.Events.Count} 个事件，时长 {_recording.Duration:F1} 秒{skipped}）。";
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

    public void SetSpeed(float speed) => Speed = speed;

    /// <summary>跳转到指定秒数：重启回放脚本后从开头补发事件（BMR 式重置 + 快进）。</summary>
    public void Seek(double t)
    {
        if (_recording == null || _disposed)
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

        Clock = Math.Clamp(t, 0d, _recording.Duration);
        _nextIndex = 0;
        DispatchUpTo(Clock);
        Playing = resume;
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

        SessionActive = false;
        Playing = false;
        Clock = 0;
        _nextIndex = 0;
        _scripts.ExitReplayMode();
        StatusMessage = "已停止回放。";
    }

    /// <summary>主线程驱动（插件 Update 中、脚本 Update 之前调用）：推进时钟并派发到期事件。</summary>
    public void Update(float deltaTime)
    {
        if (!SessionActive)
            return;

        // 回放脚本被外部停光（刷新脚本、关总开关等）时自动结束会话，避免门控卡死。
        // 零脚本的会话（无匹配录制区域的启用脚本）允许存活，时间轴照常推进。
        if (_routers.Count > 0 && !_routers.Any(r => _scripts.Running.TryGetValue(r.ScriptId, out var run) && run.IsReplaySession))
        {
            StopSession();
            StatusMessage = "回放脚本已全部停止，回放结束。";
            return;
        }

        if (!Playing || _recording == null)
            return;

        Clock += deltaTime * Speed;
        DispatchUpTo(Clock);

        if (_nextIndex >= _recording.Events.Count)
        {
            Playing = false;
            StatusMessage = "回放结束（已到文件末尾）。";
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
        SessionActive = true;
        StartSessionScripts();
        Clock = 0;
        _nextIndex = 0;
        Playing = false;

        StatusMessage = _routers.Count == 0
            ? "回放会话已启动，但没有匹配录制区域的已启用脚本（仅时间轴推进）。"
            : $"回放会话已启动：{_routers.Count} 个脚本。";
        return true;
    }

    private void RestartSessionScripts()
    {
        foreach (var (id, _) in _routers.ToArray())
            if (_scripts.Running.TryGetValue(id, out var run) && run.IsReplaySession)
                _scripts.StopScript(id);
        _routers.Clear();
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

            var router = new ReplayScriptEventRouter();
            if (_scripts.StartScript(id, router))
                _routers.Add((id, router));
            else
                router.Dispose();
        }
    }

    private void DispatchUpTo(double clock)
    {
        if (_recording == null)
            return;

        var events = _recording.Events;
        while (_nextIndex < events.Count && events[_nextIndex].T <= clock)
        {
            var ev = events[_nextIndex].Event;
            foreach (var (_, router) in _routers)
                router.Raise(ev);
            _nextIndex++;
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
