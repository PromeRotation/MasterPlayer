using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Radar;
using MasterPlayer.Replay;
using MasterPlayer.Settings;

namespace MasterPlayer.Windows;

/// <summary>
/// 录制回放独立窗口：顶部录制/回放工具栏 + 文件列表，下方分页签。
/// 「时间轴回放」= 上内嵌雷达（RadarView）+ 下事件时间轴（泳道/缩放/悬停/点击 Seek，播放头跟随回放时钟）；
/// 「日志对比」= 多份录制对齐后的技能汇总表 + 叠加读条泳道。
/// 由 UiBuilder.Draw（主线程）驱动；/prmp replay 或主面板按钮开关。
/// </summary>
public sealed class ReplayWindow : IDisposable
{
    private const float RadarSectionHeight = 300f;

    private static readonly Vector4 AccentColor = new(0.45f, 0.78f, 1f, 1f);
    private static readonly Vector4 OnColor = new(0.40f, 0.90f, 0.50f, 1f);
    private static readonly Vector4 WarningColor = new(1f, 0.65f, 0.30f, 1f);
    private static readonly Vector4 ErrorColor = new(1f, 0.45f, 0.45f, 1f);
    private static readonly Vector4 HintColor = new(0.62f, 0.66f, 0.74f, 1f);
    private static readonly Vector4 PlayheadColor = new(0.95f, 0.35f, 0.35f, 1f);
    private static readonly Vector4 GridColor = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 GridTextColor = new(1f, 1f, 1f, 0.35f);

    private static readonly float[] SpeedValues = [0.25f, 0.5f, 1f, 2f, 4f];
    private static readonly string[] SpeedLabels = ["0.25x", "0.5x", "1x", "2x", "4x"];

    private static readonly (ReplayLane Lane, string Label, Vector4 Color)[] LaneDefs =
    [
        (ReplayLane.Battle, "战斗", new Vector4(0.55f, 0.90f, 0.60f, 1f)),
        (ReplayLane.Cast, "读条", new Vector4(1f, 0.55f, 0.45f, 1f)),
        (ReplayLane.Effect, "生效", new Vector4(1f, 0.80f, 0.40f, 1f)),
        (ReplayLane.Tether, "连线", new Vector4(0.50f, 0.80f, 1f, 1f)),
        (ReplayLane.Status, "状态", new Vector4(0.75f, 0.60f, 1f, 1f)),
        (ReplayLane.Mark, "点名", new Vector4(1f, 0.60f, 0.90f, 1f)),
        (ReplayLane.Arena, "场地", new Vector4(0.60f, 0.90f, 0.85f, 1f)),
        (ReplayLane.Misc, "其他", new Vector4(0.70f, 0.70f, 0.75f, 1f)),
    ];

    private static readonly Vector4[] ComparePalette =
    [
        new(0.45f, 0.78f, 1f, 1f),
        new(1f, 0.65f, 0.40f, 1f),
        new(0.55f, 0.90f, 0.55f, 1f),
        new(0.95f, 0.60f, 0.95f, 1f),
    ];

    private readonly RadarView _radarView = new();
    private readonly List<string> _replayFiles = [];
    private readonly Dictionary<string, CompareEntry> _compareEntries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _actionNames = new();
    private readonly List<(Vector2 Min, Vector2 Max, string Tip)> _hoverTips = new(128);
    private readonly bool[] _laneVisible = [true, true, true, true, true, true, true, false];

    private bool _visible;
    private bool _disposed;
    private string? _timelinePath;
    private ReplayTimeline? _timeline;
    private float _pxPerSec = 30f;
    private float _cmpPxPerSec = 24f;
    private DateTime _lastErrorLogUtc;

    private sealed class CompareEntry
    {
        public ReplayRecording Recording = null!;
        public ReplayTimeline Timeline = null!;
        public double Offset;
    }

    public void Toggle()
    {
        if (!_disposed)
            _visible = !_visible;
    }

    public void Draw()
    {
        if (_disposed || !_visible)
            return;

        try
        {
            DrawInternal();
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLogUtc).TotalSeconds >= 5d)
            {
                _lastErrorLogUtc = now;
                Svc.Log.Error(ex, "[MasterPlayer] ReplayWindow.Draw 失败。");
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _visible = false;
    }

    private void DrawInternal()
    {
        ImGui.SetNextWindowSize(new Vector2(960, 720), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(720, 480), new Vector2(2000, 1400));
        if (!ImGui.Begin("MasterPlayer 录制回放", ref _visible))
        {
            ImGui.End();
            return;
        }

        DrawToolbar();
        DrawFileList();

        if (ImGui.BeginTabBar("##replay_tabs"))
        {
            if (ImGui.BeginTabItem("时间轴回放"))
            {
                DrawTimelineTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("日志对比"))
            {
                DrawCompareTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    // ── 工具栏 ────────────────────────────────────────

    private void DrawToolbar()
    {
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        var replay = MasterPlayerPlugin.Instance?.Replay;
        if (recorder == null || replay == null)
        {
            ImGui.TextDisabled("（录制回放未初始化）");
            return;
        }

        if (recorder.IsRecording)
        {
            ImGui.TextColored(OnColor, $"● 录制中 {(int)recorder.Elapsed.TotalMinutes:D2}:{recorder.Elapsed.Seconds:D2}（{recorder.EventCount} 事件）");
            ImGui.SameLine();
            if (ImGui.Button("停止录制"))
                recorder.StopRecording();
        }
        else
        {
            if (replay.SessionActive)
            {
                ImGui.TextColored(WarningColor, "回放会话进行中，无法录制");
            }
            else if (ImGui.Button("开始录制"))
            {
                recorder.StartRecording();
            }
        }

        ImGui.SameLine();
        var autoRecord = MasterPlayerSettings.Instance.AutoRecordOnBattleStart;
        if (ImGui.Checkbox("战斗自动录制", ref autoRecord))
        {
            MasterPlayerSettings.Instance.AutoRecordOnBattleStart = autoRecord;
            MasterPlayerSettings.Instance.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("打开录制目录"))
            OpenDirectory(ReplayRecorder.RecordingsDirectory);

        ImGui.SameLine(0f, 24f);

        var recording = replay.Recording;
        ImGui.BeginDisabled(recording == null || recorder.IsRecording);
        if (ImGui.Button(replay.Playing ? "暂停" : "播放"))
        {
            if (replay.Playing) replay.Pause(); else replay.Play();
        }
        ImGui.SameLine();
        if (ImGui.Button("停止"))
            replay.StopSession();
        ImGui.SameLine();
        var speedIndex = Array.IndexOf(SpeedValues, replay.Speed);
        if (speedIndex < 0) speedIndex = 2;
        ImGui.SetNextItemWidth(80f);
        if (ImGui.Combo("##speed", ref speedIndex, SpeedLabels, SpeedLabels.Length))
            replay.SetSpeed(SpeedValues[speedIndex]);
        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.TextColored(HintColor, replay.StatusMessage);
        if (replay.RouterErrorCount > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(ErrorColor, $"脚本回调异常 {replay.RouterErrorCount} 次");
        }
    }

    // ── 文件列表 ──────────────────────────────────────

    private void DrawFileList()
    {
        if (ImGui.Button("刷新列表"))
            RefreshFiles();
        if (_replayFiles.Count == 0)
            RefreshFiles();

        ImGui.SameLine();
        ImGui.TextColored(HintColor, "勾选多份进「日志对比」；「回放」加载并播放该录制。");

        if (ImGui.BeginChild("##replay_files", new Vector2(-1f, 96f), true))
        {
            foreach (var file in _replayFiles)
            {
                ImGui.PushID(file);

                var comparing = _compareEntries.ContainsKey(file);
                if (ImGui.Checkbox("##compare", ref comparing))
                    SetCompareSelected(file, comparing);

                ImGui.SameLine();
                if (ImGui.SmallButton("回放"))
                {
                    var replay = MasterPlayerPlugin.Instance?.Replay;
                    if (replay != null && replay.Load(file))
                        replay.Play();
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(Path.GetFileName(file));

                ImGui.PopID();
            }
        }
        ImGui.EndChild();
    }

    private void RefreshFiles()
    {
        try
        {
            var directory = ReplayRecorder.RecordingsDirectory;
            _replayFiles.Clear();
            if (Directory.Exists(directory))
                _replayFiles.AddRange(Directory.EnumerateFiles(directory, "*" + ReplayRecordingReader.FileExtension)
                    .OrderByDescending(File.GetLastWriteTime));

            foreach (var path in _compareEntries.Keys.ToArray())
                if (!_replayFiles.Contains(path))
                    _compareEntries.Remove(path);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "[MasterPlayer] 扫描录制目录失败");
        }
    }

    private void SetCompareSelected(string file, bool selected)
    {
        if (!selected)
        {
            _compareEntries.Remove(file);
            return;
        }

        try
        {
            var recording = ReplayRecordingReader.Load(file);
            _compareEntries[file] = new CompareEntry
            {
                Recording = recording,
                Timeline = ReplayTimeline.Build(recording),
                Offset = 0d,
            };
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[MasterPlayer] 对比加载录制失败：{file}");
        }
    }

    // ── 时间轴回放页签 ─────────────────────────────────

    private void DrawTimelineTab()
    {
        DrawRadarSection();

        var replay = MasterPlayerPlugin.Instance?.Replay;
        var recording = replay?.Recording;
        if (replay?.LoadedFilePath != _timelinePath)
        {
            _timelinePath = replay?.LoadedFilePath;
            _timeline = recording == null ? null : ReplayTimeline.Build(recording);
        }

        if (_timeline == null || replay == null || recording == null)
        {
            ImGui.TextDisabled("在上方文件列表点「回放」加载一份录制后，这里显示事件时间轴。");
            return;
        }

        for (var i = 0; i < LaneDefs.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            var visible = _laneVisible[i];
            if (ImGui.Checkbox(LaneDefs[i].Label, ref visible))
                _laneVisible[i] = visible;
        }

        ImGui.SameLine(0f, 24f);
        ImGui.SetNextItemWidth(160f);
        if (ImGui.SliderFloat("缩放", ref _pxPerSec, 1f, 240f, "%.0f px/s"))
            _pxPerSec = Math.Clamp(_pxPerSec, 1f, 240f);

        DrawTimelineCanvas(replay, _timeline);
    }

    private void DrawRadarSection()
    {
        if (ImGui.BeginChild("##replay_radar", new Vector2(-1f, RadarSectionHeight), true))
        {
            var origin = ImGui.GetCursorScreenPos();
            var size = ImGui.GetContentRegionAvail();
            var center = origin + size * 0.5f;
            var radius = MathF.Max(20f, MathF.Min(size.X, size.Y) * 0.5f - 8f);
            ImGui.Dummy(size);
            if (!_radarView.Draw(ImGui.GetWindowDrawList(), center, radius))
            {
                ImGui.GetWindowDrawList().AddText(center - new Vector2(60f, 8f),
                    ImGui.GetColorU32(HintColor), "（游戏中且玩家在场时显示雷达）");
            }
        }
        ImGui.EndChild();
    }

    private void DrawTimelineCanvas(ReplayController replay, ReplayTimeline timeline)
    {
        const float gutter = 34f; // 左侧泳道标签槽（固定，不随横向滚动）
        var duration = Math.Max(timeline.Duration, 0.1);
        if (ImGui.BeginChild("##replay_timeline", new Vector2(-1f, -1f), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var origin = ImGui.GetCursorScreenPos();
            var viewSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y);
            var contentWidth = gutter + (float)(duration * _pxPerSec) + 40f;
            ImGui.InvisibleButton("##timeline_hit", new Vector2(MathF.Max(contentWidth, viewSize.X), MathF.Max(viewSize.Y, 1f)));

            var scrollX = ImGui.GetScrollX();
            var hovered = ImGui.IsItemHovered();
            var mouse = ImGui.GetIO().MousePos;
            var trackMinX = origin.X + gutter;
            var trackMaxX = origin.X + viewSize.X;

            if (hovered && ImGui.GetIO().MouseWheel != 0f)
            {
                var mouseT = (mouse.X - trackMinX + scrollX) / _pxPerSec;
                _pxPerSec = Math.Clamp(_pxPerSec * (ImGui.GetIO().MouseWheel > 0f ? 1.25f : 0.8f), 1f, 240f);
                ImGui.SetScrollX((float)(mouseT * _pxPerSec) + gutter - (mouse.X - origin.X));
                scrollX = ImGui.GetScrollX();
            }

            if (hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Left) && mouse.X >= trackMinX)
            {
                var t = Math.Clamp((mouse.X - trackMinX + scrollX) / _pxPerSec, 0f, (float)duration);
                replay.Seek(t);
            }

            var drawList = ImGui.GetWindowDrawList();
            _hoverTips.Clear();

            var laneHeight = (viewSize.Y - 14f) / LaneDefs.Length;
            var laneTop = origin.Y + 14f;

            drawList.PushClipRect(new Vector2(trackMinX, origin.Y), new Vector2(trackMaxX, origin.Y + viewSize.Y), true);

            // 时间网格与刻度
            var gridStep = PickGridStep(_pxPerSec);
            for (var t = 0d; t <= duration + gridStep * 0.5; t += gridStep)
            {
                var x = trackMinX + (float)(t * _pxPerSec) - scrollX;
                if (x < trackMinX - 1f || x > trackMaxX + 1f)
                    continue;
                drawList.AddLine(new Vector2(x, origin.Y + 10f), new Vector2(x, origin.Y + viewSize.Y), ImGui.GetColorU32(GridColor));
                drawList.AddText(new Vector2(x + 2f, origin.Y), ImGui.GetColorU32(GridTextColor), FormatTime(t));
            }

            // 泳道内容
            for (var laneIndex = 0; laneIndex < LaneDefs.Length; laneIndex++)
            {
                if (!_laneVisible[laneIndex])
                    continue;
                var (lane, _, laneColor) = LaneDefs[laneIndex];
                var laneMinY = laneTop + laneIndex * laneHeight;
                var laneMidY = laneMinY + laneHeight * 0.5f;
                var laneColorU32 = ImGui.GetColorU32(laneColor);

                if (lane == ReplayLane.Cast)
                {
                    foreach (var span in timeline.Casts)
                    {
                        var x0 = trackMinX + (float)(span.Start * _pxPerSec) - scrollX;
                        var x1 = trackMinX + (float)(span.End * _pxPerSec) - scrollX;
                        if (x1 < trackMinX - 40f || x0 > trackMaxX + 40f)
                            continue;

                        var barColor = HashColor(span.ActionId);
                        barColor.W = span.Cancelled || span.Interrupted ? 0.30f : 0.55f;
                        var min = new Vector2(MathF.Max(x0, trackMinX), laneMidY - 7f);
                        var max = new Vector2(MathF.Max(x1, min.X + 2f), laneMidY + 7f);
                        drawList.AddRectFilled(min, max, ImGui.GetColorU32(barColor));
                        var edge = span.Interrupted ? new Vector4(1f, 0.3f, 0.3f, 1f)
                            : span.Cancelled ? new Vector4(1f, 0.7f, 0.3f, 1f)
                            : laneColor;
                        drawList.AddRect(min, max, ImGui.GetColorU32(edge));

                        var label = $"{ResolveActionName(span.ActionId)}  {NameOf(timeline, span.SourceId)}";
                        drawList.PushClipRect(min, max, true);
                        drawList.AddText(min + new Vector2(3f, -1f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), label);
                        drawList.PopClipRect();

                        var tip = $"{ResolveActionName(span.ActionId)}（#{span.ActionId}）\n施法者：{NameOf(timeline, span.SourceId)}\n{FormatTime(span.Start)} → {FormatTime(span.End)}"
                            + (span.Interrupted ? "\n已打断" : span.Cancelled ? "\n已取消" : "");
                        _hoverTips.Add((min, max, tip));
                    }
                }
                else
                {
                    foreach (var tick in timeline.Ticks)
                    {
                        if (tick.Lane != lane)
                            continue;
                        var x = trackMinX + (float)(tick.T * _pxPerSec) - scrollX;
                        if (x < trackMinX - 2f || x > trackMaxX + 2f)
                            continue;

                        if (lane == ReplayLane.Battle)
                        {
                            var battleColor = tick.Kind == "BattleStarted" ? new Vector4(0.4f, 1f, 0.5f, 0.8f)
                                : tick.Kind == "BattleEnded" ? new Vector4(1f, 0.4f, 0.4f, 0.8f)
                                : new Vector4(1f, 0.9f, 0.4f, 0.8f);
                            drawList.AddLine(new Vector2(x, laneTop), new Vector2(x, origin.Y + viewSize.Y), ImGui.GetColorU32(battleColor), 2f);
                            _hoverTips.Add((new Vector2(x - 3f, laneTop), new Vector2(x + 3f, origin.Y + viewSize.Y),
                                $"{tick.Detail} @ {FormatTime(tick.T)}"));
                        }
                        else
                        {
                            var min = new Vector2(x - 1.5f, laneMidY - 6f);
                            var max = new Vector2(x + 1.5f, laneMidY + 6f);
                            drawList.AddRectFilled(min, max, laneColorU32);
                            _hoverTips.Add((new Vector2(x - 3f, min.Y), new Vector2(x + 3f, max.Y),
                                $"{tick.Kind} @ {FormatTime(tick.T)}\n{tick.Detail}"));
                        }
                    }
                }
            }

            // 播放头
            var playheadX = trackMinX + (float)(replay.Clock * _pxPerSec) - scrollX;
            if (playheadX >= trackMinX && playheadX <= trackMaxX)
            {
                drawList.AddLine(new Vector2(playheadX, origin.Y), new Vector2(playheadX, origin.Y + viewSize.Y),
                    ImGui.GetColorU32(PlayheadColor), 2f);
            }

            drawList.PopClipRect();

            // 悬停提示
            if (hovered)
                foreach (var (min, max, tip) in _hoverTips)
                    if (mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y)
                    {
                        ImGui.SetTooltip(tip);
                        break;
                    }

            // 泳道标签槽（盖住滚动到其下的内容）
            var gutterColor = ImGui.GetColorU32(new Vector4(0.09f, 0.10f, 0.14f, 1f));
            drawList.AddRectFilled(origin, new Vector2(trackMinX, origin.Y + viewSize.Y), gutterColor);
            var labelColor = ImGui.GetColorU32(GridTextColor);
            for (var laneIndex = 0; laneIndex < LaneDefs.Length; laneIndex++)
            {
                if (!_laneVisible[laneIndex])
                    continue;
                var y = laneTop + laneIndex * laneHeight + laneHeight * 0.5f - 7f;
                drawList.AddText(new Vector2(origin.X + 3f, y), labelColor, LaneDefs[laneIndex].Label);
            }
        }
        ImGui.EndChild();
    }

    // ── 日志对比页签 ──────────────────────────────────

    private void DrawCompareTab()
    {
        if (_compareEntries.Count == 0)
        {
            ImGui.TextDisabled("在上方文件列表勾选两份以上同副本录制进行对比。");
            return;
        }

        if (ImGui.Button("按战斗开始对齐"))
            AlignOffsets(ReplayComparison.FirstBattleTime);
        ImGui.SameLine();
        if (ImGui.Button("按首次读条对齐"))
            AlignOffsets(ReplayComparison.FirstCastTime);
        ImGui.SameLine();
        if (ImGui.Button("偏移清零"))
            foreach (var entry in _compareEntries.Values)
                entry.Offset = 0d;

        var territories = _compareEntries.Values.Select(e => e.Recording.Header.Territory).Distinct().ToArray();
        if (territories.Length > 1)
            ImGui.TextColored(WarningColor, "所选录制的区域不一致，对比仅供参考。");

        var logs = _compareEntries.Select((pair, index) =>
                (Log: new ComparisonLog($"L{index + 1}", pair.Value.Timeline, pair.Value.Offset), Entry: pair.Value, Path: pair.Key, Index: index))
            .ToArray();

        // 每份日志的偏移调节
        foreach (var (log, entry, path, index) in logs)
        {
            ImGui.PushID(path);
            var color = ComparePalette[index % ComparePalette.Length];
            ImGui.ColorButton("##color", color, ImGuiColorEditFlags.NoTooltip, new Vector2(14f, 14f));
            ImGui.SameLine();
            var offset = (float)entry.Offset;
            ImGui.SetNextItemWidth(140f);
            if (ImGui.DragFloat("##offset", ref offset, 0.05f, 0f, 0f, $"{log.Label} 偏移 %.2f s"))
                entry.Offset = offset;
            ImGui.SameLine();
            ImGui.TextUnformatted(Path.GetFileName(path));
            ImGui.PopID();
        }

        // 汇总表
        var rows = ReplayComparison.Compare(logs.Select(l => l.Log).ToArray());
        if (ImGui.BeginChild("##compare_table", new Vector2(-1f, 220f), true))
        {
            var columns = 3 + logs.Length;
            if (ImGui.BeginTable("##compare_rows", columns, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("技能", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("出现", ImGuiTableColumnFlags.WidthFixed, 52f);
                ImGui.TableSetupColumn("首用最大差", ImGuiTableColumnFlags.WidthFixed, 76f);
                foreach (var (log, _, _, _) in logs)
                    ImGui.TableSetupColumn($"{log.Label} 次数/首用", ImGuiTableColumnFlags.WidthFixed, 92f);
                ImGui.TableHeadersRow();

                foreach (var row in rows)
                {
                    ImGui.PushID((int)row.ActionId);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var inAll = row.PresentInAll(logs.Length);
                    ImGui.TextColored(inAll ? OnColor : WarningColor, $"{ResolveActionName(row.ActionId)} #{row.ActionId}");
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(inAll ? "所有日志都出现" : "仅部分日志出现");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{row.LogsWithAction}/{logs.Length}");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.FirstTimeSpread < 0.05 ? "≈0" : $"±{row.FirstTimeSpread:F1}s");
                    for (var i = 0; i < logs.Length; i++)
                    {
                        ImGui.TableNextColumn();
                        ImGui.TextUnformatted(row.FirstTimesAligned[i] is { } first
                            ? $"{row.Counts[i]} / {first:F1}s"
                            : "—");
                    }
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        DrawCompareCanvas(logs.Select(l => (l.Log, l.Entry, l.Index)).ToArray());
    }

    private void AlignOffsets(Func<ReplayTimeline, double> anchor)
    {
        foreach (var entry in _compareEntries.Values)
            entry.Offset = -anchor(entry.Timeline);
    }

    private void DrawCompareCanvas((ComparisonLog Log, CompareEntry Entry, int Index)[] logs)
    {
        const float gutter = 30f;
        var maxEnd = logs.SelectMany(l => l.Log.Timeline.Casts.Select(c => c.End + l.Log.Offset)).DefaultIfEmpty(10d).Max();
        var minStart = logs.SelectMany(l => l.Log.Timeline.Casts.Select(c => c.Start + l.Log.Offset)).DefaultIfEmpty(0d).Min();
        var origin0 = Math.Min(minStart, 0d);
        var span = Math.Max(maxEnd - origin0, 1d);

        if (ImGui.BeginChild("##compare_lanes", new Vector2(-1f, -1f), true, ImGuiWindowFlags.HorizontalScrollbar))
        {
            var origin = ImGui.GetCursorScreenPos();
            var viewSize = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y);
            var contentWidth = gutter + (float)(span * _cmpPxPerSec) + 40f;
            ImGui.InvisibleButton("##compare_hit", new Vector2(MathF.Max(contentWidth, viewSize.X), MathF.Max(viewSize.Y, 1f)));

            var scrollX = ImGui.GetScrollX();
            var trackMinX = origin.X + gutter;
            var trackMaxX = origin.X + viewSize.X;
            if (ImGui.IsItemHovered() && ImGui.GetIO().MouseWheel != 0f)
            {
                var mouse = ImGui.GetIO().MousePos;
                var mouseT = (mouse.X - trackMinX + scrollX) / _cmpPxPerSec + origin0;
                _cmpPxPerSec = Math.Clamp(_cmpPxPerSec * (ImGui.GetIO().MouseWheel > 0f ? 1.25f : 0.8f), 1f, 240f);
                ImGui.SetScrollX((float)((mouseT - origin0) * _cmpPxPerSec) + gutter - (mouse.X - origin.X));
                scrollX = ImGui.GetScrollX();
            }

            var drawList = ImGui.GetWindowDrawList();
            _hoverTips.Clear();

            var laneHeight = (viewSize.Y - 14f) / logs.Length;
            var laneTop = origin.Y + 14f;

            drawList.PushClipRect(new Vector2(trackMinX, origin.Y), new Vector2(trackMaxX, origin.Y + viewSize.Y), true);

            var gridStep = PickGridStep(_cmpPxPerSec);
            for (var t = Math.Floor(origin0 / gridStep) * gridStep; t <= maxEnd + gridStep * 0.5; t += gridStep)
            {
                var x = trackMinX + (float)((t - origin0) * _cmpPxPerSec) - scrollX;
                if (x < trackMinX - 1f || x > trackMaxX + 1f)
                    continue;
                drawList.AddLine(new Vector2(x, origin.Y + 10f), new Vector2(x, origin.Y + viewSize.Y), ImGui.GetColorU32(GridColor));
                drawList.AddText(new Vector2(x + 2f, origin.Y), ImGui.GetColorU32(GridTextColor), FormatTime(t));
            }

            foreach (var (log, _, index) in logs)
            {
                var laneMidY = laneTop + index * laneHeight + laneHeight * 0.5f;
                var labelColor = ComparePalette[index % ComparePalette.Length];

                foreach (var battle in log.Timeline.Ticks.Where(t => t.Lane == ReplayLane.Battle))
                {
                    var bx = trackMinX + (float)((battle.T + log.Offset - origin0) * _cmpPxPerSec) - scrollX;
                    if (bx >= trackMinX && bx <= trackMaxX)
                        drawList.AddLine(new Vector2(bx, laneMidY - laneHeight * 0.4f), new Vector2(bx, laneMidY + laneHeight * 0.4f),
                            ImGui.GetColorU32(labelColor), 2f);
                }

                foreach (var cast in log.Timeline.Casts)
                {
                    var x0 = trackMinX + (float)((cast.Start + log.Offset - origin0) * _cmpPxPerSec) - scrollX;
                    var x1 = trackMinX + (float)((cast.End + log.Offset - origin0) * _cmpPxPerSec) - scrollX;
                    if (x1 < trackMinX - 40f || x0 > trackMaxX + 40f)
                        continue;

                    var barColor = HashColor(cast.ActionId);
                    barColor.W = 0.6f;
                    var min = new Vector2(MathF.Max(x0, trackMinX), laneMidY - 6f);
                    var max = new Vector2(MathF.Max(x1, min.X + 2f), laneMidY + 6f);
                    drawList.AddRectFilled(min, max, ImGui.GetColorU32(barColor));

                    var label = ResolveActionName(cast.ActionId);
                    drawList.PushClipRect(min, max, true);
                    drawList.AddText(min + new Vector2(3f, -1f), ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.9f)), label);
                    drawList.PopClipRect();

                    _hoverTips.Add((min, max, $"{log.Label}：{label}（#{cast.ActionId}）\n{FormatTime(cast.Start + log.Offset)}（原始 {FormatTime(cast.Start)}）"));
                }
            }

            drawList.PopClipRect();

            var mousePos = ImGui.GetIO().MousePos;
            if (ImGui.IsItemHovered())
                foreach (var (min, max, tip) in _hoverTips)
                    if (mousePos.X >= min.X && mousePos.X <= max.X && mousePos.Y >= min.Y && mousePos.Y <= max.Y)
                    {
                        ImGui.SetTooltip(tip);
                        break;
                    }

            var gutterColor = ImGui.GetColorU32(new Vector4(0.09f, 0.10f, 0.14f, 1f));
            drawList.AddRectFilled(origin, new Vector2(trackMinX, origin.Y + viewSize.Y), gutterColor);
            foreach (var (log, _, index) in logs)
            {
                var laneMidY = laneTop + index * laneHeight + laneHeight * 0.5f;
                drawList.AddText(new Vector2(origin.X + 3f, laneMidY - 7f), ImGui.GetColorU32(ComparePalette[index % ComparePalette.Length]), log.Label);
            }
        }
        ImGui.EndChild();
    }

    // ── 助手 ─────────────────────────────────────────

    private string ResolveActionName(uint actionId)
    {
        if (_actionNames.TryGetValue(actionId, out var cached))
            return cached;

        var name = $"#{actionId}";
        try
        {
            var row = Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRowOrDefault(actionId);
            if (row is { } action)
            {
                var text = action.Name.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    name = text;
            }
        }
        catch
        {
        }

        _actionNames[actionId] = name;
        return name;
    }

    private static string? NameOf(ReplayTimeline timeline, ulong id)
        => timeline.EntityNames.TryGetValue(id, out var name) ? name : $"0x{id:X}";

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(seconds, 0d));
        return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";
    }

    private static float PickGridStep(float pxPerSec)
    {
        ReadOnlySpan<float> candidates = [0.5f, 1f, 2f, 5f, 10f, 15f, 30f, 60f];
        foreach (var candidate in candidates)
            if (candidate * pxPerSec >= 60f)
                return candidate;
        return 120f;
    }

    private static Vector4 HashColor(uint id)
    {
        var hue = (id * 2654435761u % 360u) / 360f;
        return Hsv(hue, 0.55f, 0.85f);
    }

    private static Vector4 Hsv(float h, float s, float v)
    {
        var i = (int)(h * 6f);
        var f = h * 6f - i;
        var p = v * (1f - s);
        var q = v * (1f - f * s);
        var t = v * (1f - (1f - f) * s);
        return (i % 6) switch
        {
            0 => new Vector4(v, t, p, 1f),
            1 => new Vector4(q, v, p, 1f),
            2 => new Vector4(p, v, t, 1f),
            3 => new Vector4(p, q, v, 1f),
            4 => new Vector4(t, p, v, 1f),
            _ => new Vector4(v, p, q, 1f),
        };
    }

    private static void OpenDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start("explorer.exe", directory);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "[MasterPlayer] 打开录制目录失败");
        }
    }
}
