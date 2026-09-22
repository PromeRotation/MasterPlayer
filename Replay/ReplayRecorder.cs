using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.Hosting;
using PromeRotation.LogSystem;

namespace MasterPlayer.Replay;

/// <summary>
/// 录制器：把 PromeEventBus 的 LogSystem 全量事件流（主线程派发）写成 .mpreplay.jsonl 录制文件。
/// 手动开停（面板或 /prmp rec），或按设置 AutoRecordOnBattleStart 随战斗自动开停。
/// 与回放互斥：回放会话进行中禁止开始录制。
/// 来源是玩家或其召唤物的动作/状态/读条事件经 ReplayRecordFilter 过滤，不写入录制。
/// </summary>
public sealed class ReplayRecorder : IDisposable
{
    private readonly IDisposable _battleStartedSub;
    private readonly IDisposable _battleEndedSub;
    private readonly HashSet<uint> _playerSourceIds = [];
    private readonly IDisposable _logSub;
    private ReplayRecordingWriter? _writer;
    private DateTime _startLocal;
    private DateTime _playerSourceCacheAt;
    private bool _autoStarted;
    private bool _disposed;
    private readonly ReplayWorldCapture _worldCapture = new();
    private ReplayWorldFrame? _lastWorld;
    private DateTime _nextWorldCapture;
    private double _lastTime;

    public ReplayRecorder()
    {
        _battleStartedSub = PromeEventBus.OnBattleStarted(this, OnBattleStarted);
        _battleEndedSub = PromeEventBus.OnBattleEnded(this, OnBattleEnded);
        _logSub = PromeEventBus.OnLogEvent(this, OnLogEvent);
    }

    public static string RecordingsDirectory => Path.Combine(
        Svc.PluginInterface.ConfigDirectory.FullName,
        "MasterPlayer",
        "Replays");

    public bool IsRecording => _writer != null;

    public string? CurrentFilePath { get; private set; }

    public int EventCount { get; private set; }

    /// <summary>被过滤掉的玩家/召唤物来源事件数（动作/状态/读条）。</summary>
    public int FilteredCount { get; private set; }

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
            var territoryName = ResolveTerritoryName(territory);
            var nameSuffix = string.IsNullOrWhiteSpace(territoryName) ? "" : $"_{SanitizeFileName(territoryName)}";
            var path = Path.Combine(RecordingsDirectory,
                $"rec_{DateTime.Now:yyyyMMdd_HHmmss}_t{territory}{nameSuffix}{ReplayRecordingReader.FileExtension}");

            var version = typeof(MasterPlayerPlugin).Assembly.GetName().Version?.ToString() ?? "unknown";
            var writer = new ReplayRecordingWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
            _startLocal = DateTime.Now;
            EventCount = 0;
            _writer = writer;
            writer.WriteHeader(new ReplayHeader(2, territory, _startLocal.ToUniversalTime(), version) { TerritoryName = territoryName });
            _lastWorld = null;
            _lastTime = 0;
            CaptureWorld(_startLocal.ToUniversalTime());
            CurrentFilePath = path;
            _autoStarted = automatic;
            FilteredCount = 0;
            _playerSourceCacheAt = DateTime.MinValue;
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
        try { CaptureWorld(DateTime.UtcNow); }
        catch (Exception ex) { FailRecording(ex); return; }
        var count = EventCount;
        var filtered = FilteredCount;
        CleanupRecording();
        StatusMessage = $"已停止录制（{count} 个事件，过滤玩家来源 {filtered} 条）：{Path.GetFileName(file ?? "")}";
        Svc.Log.Info($"[MasterPlayer] 停止录制：{file}（{count} 个事件）");
    }

    private void OnLogEvent(LogSystemEvent ev)
    {
        _worldCapture.Observe(ev);
        var writer = _writer;
        if (writer == null)
            return;

        if (!ReplayRecordFilter.ShouldRecord(ev, IsPlayerOwnedSource))
        {
            FilteredCount++;
            return;
        }

        try
        {
            var t = CaptureWorld(DateTime.UtcNow);
            writer.WriteEvent(t, ev);
            EventCount++;
        }
        catch (Exception ex)
        {
            FailRecording(ex);
        }
    }

    public void Update()
    {
        if (_writer == null || DateTime.UtcNow < _nextWorldCapture) return;
        try { CaptureWorld(DateTime.UtcNow); }
        catch (Exception ex) { FailRecording(ex); }
    }

    private double CaptureWorld(DateTime now)
    {
        var world = _worldCapture.Capture(now);
        var t = Math.Max(_lastTime, (now - _startLocal.ToUniversalTime()).TotalSeconds);
        _writer!.WriteWorldFrame(t, ReplayWorldFrame.Delta(_lastWorld, world));
        _lastWorld = world;
        _lastTime = t;
        _nextWorldCapture = now.AddSeconds(0.1);
        return t;
    }

    private void FailRecording(Exception ex)
    {
        var path = CurrentFilePath;
        CleanupRecording();
        StatusMessage = $"录制已中止，文件可能不完整：{Path.GetFileName(path)}；{ex.Message}";
        Svc.Log.Error(ex, "[MasterPlayer] 录制对象状态或事件失败");
    }

    /// <summary>来源实体是否为玩家（PC）或玩家召唤物（OwnerId 属于 PC）；0.5s 缓存，无法判定视为 false（放行）。</summary>
    private bool IsPlayerOwnedSource(ulong sourceId)
    {
        if (sourceId == 0 || sourceId > uint.MaxValue)
            return false;

        var now = DateTime.UtcNow;
        if ((now - _playerSourceCacheAt).TotalSeconds > 0.5)
        {
            _playerSourceCacheAt = now;
            _playerSourceIds.Clear();
            try
            {
                foreach (var obj in Svc.Objects)
                    if (obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Pc)
                        _playerSourceIds.Add(obj.EntityId);
                foreach (var obj in Svc.Objects)
                    if (obj.OwnerId != 0 && _playerSourceIds.Contains(obj.OwnerId))
                        _playerSourceIds.Add(obj.EntityId);
            }
            catch
            {
            }
        }

        return _playerSourceIds.Contains((uint)sourceId);
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

    /// <summary>区域名：优先副本名（ContentFinderCondition），回退地名（PlaceName）；取不到返回空串。</summary>
    private static string ResolveTerritoryName(ushort territoryType)
    {
        try
        {
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>()?.GetRowOrDefault(territoryType);
            if (row == null)
                return "";
            var cfc = row.Value.ContentFinderCondition.ValueNullable?.Name.ToString();
            if (!string.IsNullOrEmpty(cfc))
                return cfc;
            return row.Value.PlaceName.ValueNullable?.Name.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    private void CleanupRecording()
    {
        try { _writer?.Dispose(); } catch { }
        _writer = null;
        _lastWorld = null;
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
        try { _logSub.Dispose(); } catch { }
        CleanupRecording();
    }
}
