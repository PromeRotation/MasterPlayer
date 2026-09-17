using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>
/// 录制器：把 PromeEventBus 的 LogSystem 全量事件流（主线程派发）写成 .mpreplay.jsonl 录制文件。
/// 手动开停（面板或 /prmp rec），或按设置 AutoRecordOnBattleStart 随战斗自动开停。
/// 与回放互斥：回放会话进行中禁止开始录制。
/// </summary>
public sealed class ReplayRecorder : IDisposable
{
    private readonly IDisposable _battleStartedSub;
    private readonly IDisposable _battleEndedSub;
    private IDisposable? _logSub;
    private ReplayRecordingWriter? _writer;
    private DateTime _startLocal;
    private bool _autoStarted;
    private bool _disposed;

    public ReplayRecorder()
    {
        _battleStartedSub = PromeEventBus.OnBattleStarted(this, OnBattleStarted);
        _battleEndedSub = PromeEventBus.OnBattleEnded(this, OnBattleEnded);
    }

    public static string RecordingsDirectory => Path.Combine(
        Svc.PluginInterface.ConfigDirectory.FullName,
        "MasterPlayer",
        "Replays");

    public bool IsRecording => _writer != null;

    public string? CurrentFilePath { get; private set; }

    public int EventCount { get; private set; }

    public TimeSpan Elapsed => IsRecording ? DateTime.Now - _startLocal : TimeSpan.Zero;

    public string StatusMessage { get; private set; } = "未录制。";

    public bool StartRecording(bool automatic = false)
    {
        if (IsRecording)
            return false;
        if (_disposed)
            return false;
        if (MasterPlayerPlugin.Instance?.Replay?.SessionActive == true)
        {
            StatusMessage = "回放进行中，无法开始录制。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(RecordingsDirectory);
            var territory = (ushort)Svc.ClientState.TerritoryType;
            var path = Path.Combine(RecordingsDirectory,
                $"rec_{DateTime.Now:yyyyMMdd_HHmmss}_t{territory}{ReplayRecordingReader.FileExtension}");

            var version = typeof(MasterPlayerPlugin).Assembly.GetName().Version?.ToString() ?? "unknown";
            var writer = new ReplayRecordingWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            _startLocal = DateTime.Now;
            EventCount = 0;
            writer.WriteHeader(new ReplayHeader(1, territory, DateTime.UtcNow, version));

            _writer = writer;
            CurrentFilePath = path;
            _autoStarted = automatic;
            _logSub = PromeEventBus.OnLogEvent(this, OnLogEvent);

            StatusMessage = $"录制中：{Path.GetFileName(path)}";
            Svc.Log.Info($"[MasterPlayer] 开始录制：{path}");
            return true;
        }
        catch (Exception ex)
        {
            CleanupRecording();
            StatusMessage = $"开始录制失败：{ex.Message}";
            Svc.Log.Error(ex, "[MasterPlayer] 开始录制失败");
            return false;
        }
    }

    public void StopRecording()
    {
        if (!IsRecording)
            return;

        var file = CurrentFilePath;
        var count = EventCount;
        CleanupRecording();
        StatusMessage = $"已停止录制（{count} 个事件）：{Path.GetFileName(file ?? "")}";
        Svc.Log.Info($"[MasterPlayer] 停止录制：{file}（{count} 个事件）");
    }

    private void OnLogEvent(LogSystemEvent ev)
    {
        var writer = _writer;
        if (writer == null)
            return;

        var t = Math.Max(0d, (ev.Timestamp - _startLocal).TotalSeconds);
        try
        {
            writer.WriteEvent(t, ev);
            EventCount++;
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, $"[MasterPlayer] 写入录制事件失败（{ev.Kind}）");
        }
    }

    private void OnBattleStarted()
    {
        if (MasterPlayerSettings.Instance.AutoRecordOnBattleStart && !IsRecording && !_disposed)
            StartRecording(automatic: true);
    }

    private void OnBattleEnded()
    {
        if (_autoStarted && IsRecording)
            StopRecording();
    }

    private void CleanupRecording()
    {
        try { _logSub?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _logSub = null;
        _writer = null;
        CurrentFilePath = null;
        _autoStarted = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _battleStartedSub.Dispose(); } catch { }
        try { _battleEndedSub.Dispose(); } catch { }
        CleanupRecording();
    }
}
