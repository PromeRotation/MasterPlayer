using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

/// <summary>
/// 设置面板：侧栏分区（总览/显示与绘制/自动走位/机制脚本/录制回放/诊断）。
/// 由 UiBuilder.Draw（主线程）驱动；PR 插件管理器按钮或聊天指令 /prmp 开关。
/// </summary>
public sealed class MainWindow : IDisposable
{
    private static Vector4 AccentColor => PromeUiTheme.Accent;
    private static Vector4 OnColor => PromeUiTheme.Success;
    private static Vector4 WarningColor => PromeUiTheme.Warning;
    private static Vector4 HintColor => PromeUiTheme.TextMuted;
    private static Vector4 ErrorColor => PromeUiTheme.Danger;

    private bool _visible;
    private bool _disposed;
    private string _aoeFilter = "";
    private int _page, _displayPage, _scriptScope;
    private string _scriptFilter = "";
    private string? _selectedScript;
    private static readonly string[] Pages = ["总览", "显示与绘制", "自动走位", "机制脚本", "录制回放", "诊断"];
    private readonly VisualScriptEditor _visualEditor = new();

    public void Toggle()
    {
        if (!_disposed)
            _visible = !_visible;
    }

    public void Draw()
    {
        if (_disposed)
            return;
        _visualEditor.Draw();
        if (!_visible) return;

        using var theme = PromeUiTheme.PushWindowStyle();
        ImGui.SetNextWindowSize(new Vector2(900, 700), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(780, 520), new Vector2(1800, 1400));
        if (!ImGui.Begin("MasterPlayer 设置", ref _visible))
        {
            ImGui.End();
            return;
        }

        try
        {
            if (ImGui.BeginChild("##navigation", new Vector2(138, -1), false))
            {
                ImGui.TextColored(AccentColor, "MasterPlayer");
                ImGui.Spacing();
                for (var i = 0; i < Pages.Length; ++i)
                    if (ImGui.Selectable(Pages[i], _page == i, ImGuiSelectableFlags.None, new Vector2(0, 36))) _page = i;
                ImGui.Spacing();
                Hint("外观跟随 PR 主题");
                Hint("设置即时生效");
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("##page_" + _page, new Vector2(-1, -1), false))
            {
                PluginUi.PageHeader(Pages[_page]);
                switch (_page)
                {
                    case 0: DrawOverviewTab(); break;
                    case 1: DrawAppearanceTab(); break;
                    case 2: DrawAutomationTab(); break;
                    case 3: DrawScriptsTab(); break;
                    case 4: DrawReplayTab(); break;
                    case 5: DrawDiagnosticsTab(); break;
                }
            }
            ImGui.EndChild();
        }
        finally { ImGui.End(); }
    }

    private static void DrawOverviewTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        SectionHeader("功能模块");
        changed |= ModuleRow("真实世界绘制", "在游戏 3D 世界中绘制机制范围与安全点提示。", settings.EnableRadar, v => settings.EnableRadar = v);
        changed |= ModuleRow("雷达悬浮窗", "俯视场地、机制和走位路线。", settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        changed |= ModuleRow("智能躲避", "根据机制判定时间自动前往安全位置。", settings.EnableAutoDodge, v => settings.EnableAutoDodge = v);
        changed |= ModuleRow("接近目标", "跟随队友或当前目标，避险优先。", settings.EnableFollow, v => settings.EnableFollow = v);
        changed |= ModuleRow("机制脚本", "进入对应区域自动运行已启用的脚本。", settings.EnableScripts, v => settings.EnableScripts = v);

        if (changed)
            settings.Save();

        SectionHeader("当前状态");
        DrawOverviewStatus(settings);
    }

    private static void DrawOverviewStatus(MasterPlayerSettings settings)
    {
        try
        {
            var plugin = PromeRotation.Plugin.Instance;
            var hostSetting = plugin.SafeZoneRebornSetting;
            var status = plugin.SafeZoneRebornMovementStatus;
            var scripts = MasterPlayerPlugin.Instance?.Scripts;
            var snapshot = plugin.SafeZoneRebornManager.BuildSnapshot(DateTime.UtcNow);

            ImGui.Text($"当前区域 ID：{Svc.ClientState.TerritoryType}");
            HintInline($"运行中脚本 {scripts?.Running.Count ?? 0} / {scripts?.Scripts.Count ?? 0}");
            HintInline($"活动危险区 {snapshot.DangerAreas.Count}");
            Hint($"安全区 Reborn：{status.PhaseText}");

            var needsSafeZone = settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableAutoDodge;
            if (needsSafeZone && !hostSetting.EnableSafeZone)
                ImGui.TextColored(WarningColor, "安全区 Reborn 已关闭，机制范围无法显示；请在诊断页开启 Reborn 解算。");
            if (settings.EnableAutoDodge && !hostSetting.EnableAutoMovement)
                ImGui.TextColored(WarningColor, "Reborn 自动移动已关闭，智能躲避不会走位；请在诊断页开启 Reborn 自动移动。");
            if (settings.EnableAutoDodge &&
                ((plugin.SafeZoneSetting.EnableSafeZone && plugin.SafeZoneSetting.EnableAutoMovement) || plugin.SafeZoneMovementStatus.HasControl))
            {
                ImGui.TextColored(WarningColor, "旧版安全区自动移动优先，Reborn 已让位。");
                if (ImGui.Button("关闭旧版自动移动"))
                    plugin.SafeZoneSetting.EnableAutoMovement = false;
            }

            var anyModuleOn = settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableAutoDodge ||
                              settings.EnableFollow || settings.EnableScripts;
            if (!anyModuleOn)
                Hint("所有模块均已关闭，打开上方开关即可开始使用。");
        }
        catch (Exception ex)
        {
            ImGui.TextDisabled($"状态读取失败：{ex.Message}");
        }
    }

    private static void DrawRadarTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        SectionHeader("显示");
        changed |= Check("启用雷达悬浮窗", settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        changed |= Check("仅在有机制脚本的区域显示", settings.RadarOnlyWithScript, v => settings.RadarOnlyWithScript = v);

        var centerMode = settings.RadarCenterMode;
        if (ImGui.Combo("中心模式", ref centerMode, "场地基准（完整显示脚本注册的场地）\0玩家摄像机基准（跟随自身 25 码视野）\0"))
        {
            settings.RadarCenterMode = centerMode;
            changed = true;
        }

        changed |= Check("画面随摄像机旋转", settings.RadarRotateWithCamera, v => settings.RadarRotateWithCamera = v);

        SectionHeader("外观");
        var scale = settings.RadarScale;
        if (PluginUi.Slider("雷达大小", ref scale, 0.3f, 3f, "%.2f 倍"))
        {
            settings.RadarScale = scale;
            changed = true;
        }

        changed |= PluginUi.Opacity("背景不透明度", ref settings.RadarBackgroundOpacity);

        changed |= Check("显示罗盘", settings.RadarShowCompass, v => settings.RadarShowCompass = v);
        changed |= Check("显示距离环", settings.RadarShowRangeRings, v => settings.RadarShowRangeRings = v);
        changed |= Check("危险区临期脉冲", settings.RadarPulseDanger, v => settings.RadarPulseDanger = v);

        SectionHeader("交互");
        changed |= Check("锁定位置（鼠标穿透）", settings.RadarOverlayLock, v => settings.RadarOverlayLock = v);
        Hint("解锁后可拖动雷达，滚轮调整大小，右键打开快捷菜单。");

        if (changed)
            settings.Save();
    }

    private void DrawAppearanceTab()
    {
        var settings = MasterPlayerSettings.Instance;
        _displayPage = PluginUi.TabBar("##display_sections", ["雷达", "世界绘制", "按机制显示"], _displayPage);
        if (_displayPage == 0)
        {
            DrawRadarTab();
            if (DrawAoeAppearance("范围颜色", settings.RadarAoe)) settings.Save();
            return;
        }
        if (_displayPage == 1)
        {
            var worldChanged = ModuleRow("启用世界绘制", "在游戏场景中显示机制范围。", settings.EnableRadar, v => settings.EnableRadar = v);
            worldChanged |= DrawAoeAppearance("范围颜色", settings.WorldAoe);
            if (worldChanged) settings.Save();
            return;
        }
        var changed = false;

        SectionHeader("按技能／机制显示");
        Hint("隐藏只是不绘制，危险区仍参与躲避解算；识别过的技能会自动加入列表。");
        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint("##aoe_filter", "筛选名称…", ref _aoeFilter, 256);

        if (ImGui.BeginTable("##aoe_sources", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(-1f, 260f)))
        {
            ImGui.TableSetupColumn("技能／机制", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("真实世界", ImGuiTableColumnFlags.WidthFixed, 70f);
            ImGui.TableSetupColumn("雷达", ImGuiTableColumnFlags.WidthFixed, 55f);
            ImGui.TableHeadersRow();

            foreach (var (key, visibility) in settings.AoeVisibility.OrderBy(pair => pair.Value.Name))
            {
                if (!visibility.Name.Contains(_aoeFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                ImGui.PushID(key);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextWrapped(visibility.Name);
                ImGui.TableNextColumn();
                changed |= ImGui.Checkbox("##world", ref visibility.World);
                ImGui.TableNextColumn();
                changed |= ImGui.Checkbox("##radar", ref visibility.Radar);
                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (changed)
            settings.Save();
    }

    private static bool DrawAoeAppearance(string label, AoeViewSettings settings)
    {
        SectionHeader(label);
        Hint("点击色块改颜色，拖动滑条改不透明度。0% 为透明，100% 为不透明。");
        var changed = PluginUi.ColorOpacity("危险填充", ref settings.DangerFill);
        changed |= PluginUi.ColorOpacity("危险边框", ref settings.DangerOutline);
        changed |= PluginUi.ColorOpacity("安全填充", ref settings.SafeFill);
        changed |= PluginUi.ColorOpacity("安全边框", ref settings.SafeOutline);
        changed |= ImGui.Checkbox("危险预告逐渐加深", ref settings.FadeDanger);
        if (settings.FadeDanger) Hint("危险越接近判定，填充越明显；滑条控制最大不透明度。");
        changed |= ImGui.Checkbox("显示危险区", ref settings.ShowDanger);
        changed |= ImGui.Checkbox("显示安全区", ref settings.ShowSafe);
        if (ImGui.TreeNode("按形状筛选"))
        {
            changed |= ImGui.Checkbox("圆形", ref settings.ShowCircles);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("月环", ref settings.ShowDonuts);
            changed |= ImGui.Checkbox("扇形", ref settings.ShowFans);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("矩形／直线", ref settings.ShowRectangles);
            changed |= ImGui.Checkbox("多边形", ref settings.ShowPolygons);
            ImGui.SameLine();
            changed |= ImGui.Checkbox("其他形状", ref settings.ShowOtherShapes);
            ImGui.TreePop();
        }
        return changed;
    }

    private static void DrawAutomationTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        SectionHeader("接近目标");
        Hint("开启智能躲避后，机制站位和脱险优先于接近目标。");
        changed |= Check("启用跟随", settings.EnableFollow, v => settings.EnableFollow = v);

        var combatDistance = settings.CombatDistance == CombatDistanceMode.Ranged ? 1 : 0;
        ImGui.TextUnformatted("战斗距离");
        ImGui.SetNextItemWidth(-1);
        if (ImGui.Combo("##战斗距离", ref combatDistance, "近战\0远程\0"))
        {
            settings.CombatDistance = (CombatDistanceMode)combatDistance;
            changed = true;
        }
        if (settings.CombatDistance == CombatDistanceMode.Ranged)
        {
            var range = settings.RangedAttackRange;
            if (PluginUi.Slider("远程射程", ref range, 5f, 25f, "%.1f 码"))
            {
                settings.RangedAttackRange = range;
                changed = true;
            }
            Hint("射程内优先保持安全站位，超出射程才接近；按常用技能设置射程。");
        }
        else
            Hint("优先选择 3 码攻击范围内的安全位置；必须远离时先躲避，危险结束后回到近战范围。");
        Hint("对怪物计入双方目标圈，并预留停步余量。跟随队友使用下方距离。");

        var slot = settings.FollowSlot;
        if (ImGui.SliderInt("跟随槽位", ref slot, 0, 7))
        {
            settings.FollowSlot = slot;
            changed = true;
        }
        Hint("0 = 不指定小队槽位；1~7 = 小队 UI 序。");

        changed |= Check("无有效槽位时跟随当前目标", settings.FollowTargetWhenNoSlot, v => settings.FollowTargetWhenNoSlot = v);
        Hint("支持玩家和怪物；不跟随已死亡或不可选中的目标。");

        var distance = settings.FollowDistance;
        if (PluginUi.Slider("队友跟随距离", ref distance, 0.5f, 10f, "%.1f 码"))
        {
            settings.FollowDistance = distance;
            changed = true;
        }
        Hint("仅用于玩家，按双方中心距离计算。");

        changed |= Check("战斗中生效", settings.FollowDuringCombat, v => settings.FollowDuringCombat = v);
        changed |= Check("非战斗中也生效", settings.FollowOutOfCombat, v => settings.FollowOutOfCombat = v);

        SectionHeader("智能躲避");
        changed |= Check("启用智能躲避", settings.EnableAutoDodge, v => settings.EnableAutoDodge = v);
        changed |= Check("优化撤离时机", settings.OptimizeDodgeTiming, v => settings.OptimizeDodgeTiming = v);
        Hint("有安全路线时延后起步，保留输出和读条时间；按实际路程与判定时间撤离，无需选中目标或开启跟随。");
        var margin = settings.DodgeDangerMargin;
        if (PluginUi.Slider("危险区安全余量", ref margin, 0f, 2f, "%.2f 码"))
        {
            settings.DodgeDangerMargin = margin;
            changed = true;
        }
        Hint("注册危险区时向外扩张的安全余量，智能躲避会与危险区边缘至少保持该距离。");
        Hint("开启雷达或真实世界绘制会启用 PR 安全区系统；开启智能躲避还会启用 PR 自动移动。只开启绘制不会启用自动移动。");

        if (changed)
            settings.Save();
    }

    private void DrawScriptsTab()
    {
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null) { Hint("脚本管理器尚未就绪。"); return; }
        var settings = MasterPlayerSettings.Instance;
        if (ModuleRow("启用机制脚本", "进入对应区域自动运行，离开后停止。", settings.EnableScripts, v => settings.EnableScripts = v)) settings.Save();
        if (ImGui.Button("刷新脚本")) manager.ReloadScripts();
        ImGui.SameLine();
        if (ImGui.Button("打开目录")) OpenScriptsDirectory(manager.ScriptsDirectory);
        ImGui.SameLine();
        if (ImGui.Button("可视化编辑器")) _visualEditor.Open();
        if (!string.IsNullOrEmpty(manager.StatusMessage)) Hint(manager.StatusMessage);
        if (!string.IsNullOrEmpty(manager.ErrorMessage))
        {
            if (ImGui.BeginChild("##script_errors", new Vector2(-1, 80), true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ErrorColor);
                ImGui.TextWrapped(manager.ErrorMessage);
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
        }
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##script_filter", "搜索脚本名称 / 区域 ID", ref _scriptFilter, 256);
        _scriptScope = PluginUi.TabBar("##script_scope", ["全部", "当前区域", "运行中"], _scriptScope);
        var territory = Svc.ClientState.TerritoryType;
        var filter = _scriptFilter.Trim();
        var entries = manager.Scripts.Where(d =>
            (_scriptScope != 1 || d.Metadata.Territorys.Length == 0 || d.Metadata.Territorys.Contains(territory)) &&
            (_scriptScope != 2 || manager.Running.ContainsKey(d.Metadata.Id)) &&
            (d.Metadata.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
             d.Metadata.Region.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
             d.Metadata.Territorys.Any(t => t.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(d => manager.Running.ContainsKey(d.Metadata.Id)).ThenBy(d => d.Metadata.Name).ToArray();
        if (entries.Length == 0)
        {
            Hint(manager.Scripts.Count == 0 ? "暂无脚本。可在编辑器中新建，或将脚本放入目录后刷新。" : "没有匹配的脚本，可修改搜索或筛选条件。");
            return;
        }
        if (!entries.Any(d => d.Metadata.Id == _selectedScript)) _selectedScript = entries[0].Metadata.Id;
        var listWidth = Math.Clamp(ImGui.GetContentRegionAvail().X * 0.38f, 190, 290);
        if (ImGui.BeginChild("##script_list", new Vector2(listWidth, -1), true))
        {
            foreach (var descriptor in entries)
            {
                var id = descriptor.Metadata.Id;
                ImGui.PushID(id);
                if (ImGui.Selectable(descriptor.Metadata.Name + "###select", _selectedScript == id)) _selectedScript = id;
                var running = manager.Running.ContainsKey(id);
                var status = running ? "运行中" : !settings.EnableScripts ? "总开关已关闭" : manager.IsScriptEnabled(id) ? "已启用 · 等待区域" : "已停用";
                ImGui.TextColored(running ? OnColor : HintColor, status);
                ImGui.Separator();
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
        ImGui.SameLine();
        if (ImGui.BeginChild("##script_detail", new Vector2(-1, -1), false))
            DrawScriptDetails(manager, entries.First(d => d.Metadata.Id == _selectedScript));
        ImGui.EndChild();
    }

    private static void DrawDiagnosticsTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var debugMode = settings.DebugMode;
        if (ImGui.Checkbox("调试模式（在下方显示解算计数等诊断信息）", ref debugMode))
        {
            settings.DebugMode = debugMode;
            settings.Save();
        }

        SectionHeader("本体状态");
        DrawHostStatus(settings);

        SectionHeader("机制处理器");
        DrawMechanicHandlers();
    }

    private static void DrawReplayTab()
    {
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        var replay = MasterPlayerPlugin.Instance?.Replay;
        if (recorder == null || replay == null)
        {
            ImGui.TextDisabled("（录制回放未初始化）");
            return;
        }

        if (recorder.IsRecording)
            ImGui.TextColored(OnColor, $"● 录制中 {(int)recorder.Elapsed.TotalMinutes:D2}:{recorder.Elapsed.Seconds:D2}（{recorder.EventCount} 事件）");
        else
            HintInline(recorder.StatusMessage);

        if (replay.SessionActive)
            ImGui.TextColored(OnColor, $"● 回放中：{replay.ReplayScriptCount} 个脚本，{replay.Clock:F1} s");

        if (ImGui.Button("打开录制回放窗口"))
            MasterPlayerPlugin.Instance?.ReplayWindow?.Toggle();
        Hint("查看场地预览、拖动时间轴或对比多份日志。指令 /prmp replay 也可打开。");

        var autoRecord = MasterPlayerSettings.Instance.AutoRecordOnBattleStart;
        if (ImGui.Checkbox("战斗开始时自动录制（战斗结束自动停止）", ref autoRecord))
        {
            MasterPlayerSettings.Instance.AutoRecordOnBattleStart = autoRecord;
            MasterPlayerSettings.Instance.Save();
        }
    }

    private static void DrawHostStatus(MasterPlayerSettings settings)
    {
        try
        {
            var plugin = PromeRotation.Plugin.Instance;
            var setting = plugin.SafeZoneRebornSetting;
            var status = plugin.SafeZoneRebornMovementStatus;
            var snapshot = plugin.SafeZoneRebornManager.BuildSnapshot(DateTime.UtcNow);

            var enableSolve = setting.EnableSafeZone;
            if (ImGui.Checkbox("启用 Reborn 解算", ref enableSolve))
                setting.EnableSafeZone = enableSolve;
            var enableMovement = setting.EnableAutoMovement;
            if (ImGui.Checkbox("启用 Reborn 自动移动", ref enableMovement))
                setting.EnableAutoMovement = enableMovement;
            ImGui.Text($"移动控制权：{(status.HasControl ? "持有" : "无")}    阶段：{status.PhaseText}");
            ImGui.Text($"最新解算方案：{(status.LatestSolution != null ? "有" : "无")}");
            ImGui.Text($"当前危险区：{snapshot.DangerAreas.Count}    运行中脚本：{MasterPlayerPlugin.Instance?.Scripts?.Running.Count ?? 0}");

            if (settings.DebugMode)
            {
                ImGui.Text($"派发次数：{status.DispatchCount}    最近派发：{status.LastDispatchUtc:HH:mm:ss.fff} UTC");
                ImGui.TextWrapped($"异步解算：运行中={status.IsAsyncSolveRunning} 发起={status.AsyncSolveStartedCount} 采纳={status.AsyncSolveAcceptedCount} 取消={status.AsyncSolveCancelledCount} 丢弃={status.AsyncSolveDiscardedCount}");
                ImGui.TextWrapped($"区域快照 v{snapshot.Version}：危险区 {snapshot.DangerAreas.Count} / 目标区 {snapshot.GoalAreas.Count} / 障碍 {snapshot.HardObstacles.Count} / 边界 {(snapshot.ArenaBoundary != null ? "有" : "无")}");
            }
        }
        catch (Exception ex)
        {
            ImGui.TextDisabled($"状态读取失败：{ex.Message}");
        }
    }

    private static void OpenScriptsDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start("explorer.exe", directory);
        }
        catch (Exception ex)
        {
            Svc.Log.Warning(ex, "[MasterPlayer] 打开脚本目录失败");
        }
    }

    private void DrawScriptDetails(ScriptManager manager, ScriptDescriptor descriptor)
    {
        var metadata = descriptor.Metadata;
        var id = metadata.Id;
        ImGui.PushID(id);
        ImGui.TextWrapped(metadata.Name);
        Hint($"v{metadata.Version} · {metadata.Author}");
        var enabled = manager.IsScriptEnabled(id);
        if (PluginUi.Toggle("enabled", "启用此脚本", "在适用区域自动运行。", ref enabled)) manager.SetScriptEnabled(id, enabled);
        if (descriptor.Source == ScriptSource.Visual && ImGui.Button("编辑脚本")) _visualEditor.Open(descriptor.SourcePath);
        if (!string.IsNullOrWhiteSpace(metadata.Note)) ImGui.TextWrapped(metadata.Note);
        Hint(metadata.Territorys.Length == 0 ? "适用区域：通用" : $"适用区域：{string.Join(", ", metadata.Territorys)}");
        if (manager.Running.TryGetValue(id, out var run))
        {
            DrawScriptSettings(run);
            DrawMechanismToggles(manager, id, run);
        }
        else Hint("脚本运行后可调整设置与机制开关。");
        if (ImGui.TreeNode("文件信息"))
        {
            ImGui.TextWrapped(metadata.SourcePath);
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    private static void DrawScriptSettings(RunningScript run)
    {
        var store = run.Settings;
        if (store.RegisteredSettings.Count == 0)
            return;

        SectionHeader("脚本设置");
        foreach (var (propName, displayName) in store.RegisteredSettings)
            DrawScriptSetting(store, propName, displayName);
    }

    private static void DrawScriptSetting(ScriptSettingStore store, string propName, string displayName)
    {
        var type = store.GetPropertyType(propName);
        var value = store.GetPropertyValue(propName);

        ImGui.PushID(propName);

        if (type == typeof(bool))
        {
            var b = value is bool bv && bv;
            if (ImGui.Checkbox(displayName, ref b))
                store.SetPropertyValue(propName, b);
        }
        else if (type == typeof(int))
        {
            var i = value is int iv ? iv : 0;
            if (ImGui.InputInt(displayName, ref i))
                store.SetPropertyValue(propName, i);
        }
        else if (type == typeof(float))
        {
            var f = value is float fv ? fv : 0f;
            if (ImGui.DragFloat(displayName, ref f, 0.1f))
                store.SetPropertyValue(propName, f);
        }
        else if (type == typeof(string))
        {
            var s = value as string ?? "";
            if (ImGui.InputText(displayName, ref s, 256))
                store.SetPropertyValue(propName, s);
        }
        else if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var current = value != null ? Array.IndexOf(names, value.ToString()) : 0;
            if (ImGui.Combo(displayName, ref current, names, names.Length) && current >= 0)
                store.SetPropertyValue(propName, Enum.Parse(type, names[current]));
        }
        else if (type == typeof(ScriptColor))
        {
            var color = value as ScriptColor ?? new ScriptColor();
            var v4 = color.V4;
            if (ImGui.ColorEdit4(displayName, ref v4, ImGuiColorEditFlags.NoInputs))
            {
                color.V4 = v4;
                store.SetPropertyValue(propName, color);
            }
        }
        else
        {
            ImGui.TextDisabled($"{displayName}：不支持的类型 {type.Name}");
        }

        ImGui.PopID();
    }

    private static void DrawMechanismToggles(ScriptManager manager, string scriptId, RunningScript run)
    {
        var mechanisms = run.Script.GetMechanisms().ToArray();
        if (mechanisms.Length == 0)
            return;

        SectionHeader("机制开关");
        foreach (var mechanism in mechanisms)
        {
            var enabled = manager.IsMechanismEnabled(scriptId, mechanism);
            if (ImGui.Checkbox(mechanism, ref enabled))
                manager.SetMechanismEnabled(scriptId, mechanism, enabled);
        }
    }

    private static void DrawMechanicHandlers()
    {
        var dispatcher = MasterPlayerPlugin.Instance?.Mechanics;
        if (dispatcher == null)
        {
            ImGui.TextDisabled("（MechanicDispatcher 未初始化）");
            return;
        }

        var handlers = dispatcher.Handlers;
        if (handlers.Count == 0)
        {
            ImGui.TextDisabled("（无已注册处理器）");
            return;
        }

        foreach (var handler in handlers)
        {
            ImGui.PushID(handler.Id);

            var enabled = handler.Enabled;
            if (ImGui.Checkbox("##enabled", ref enabled))
                handler.Enabled = enabled;

            ImGui.SameLine();
            ImGui.Text(handler.DisplayName);

            ImGui.PopID();
        }
    }

    private static void SectionHeader(string text)
    {
        ImGui.Spacing();
        ImGui.TextColored(AccentColor, text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, HintColor);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private static void HintInline(string text)
        => ImGui.TextColored(HintColor, text);

    private static bool Check(string label, bool value, Action<bool> apply)
    {
        var newValue = value;
        if (!ImGui.Checkbox(label, ref newValue))
            return false;
        apply(newValue);
        return true;
    }

    private static bool ModuleRow(string label, string description, bool value, Action<bool> apply)
    {
        var newValue = value;
        var changed = PluginUi.Toggle(label, label, description, ref newValue);
        if (changed)
            apply(newValue);
        return changed;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _visible = false;
        }
        catch (Exception ex)
        {
            try
            {
                Svc.Log.Warning(ex, "[MasterPlayer] MainWindow Dispose 异常");
            }
            catch
            {
            }
        }
    }
}
