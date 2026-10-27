using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

/// <summary>
/// 设置面板：侧栏分区（总览/显示与绘制/智能走位/机制库/录制回放/诊断）。
/// 由 UiBuilder.Draw（主线程）驱动；PR 插件管理器按钮或聊天指令 /prpr 开关。
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
    private int _page, _displayPage, _scriptScope, _mechanicsPage;
    private string _scriptFilter = "";
    private string _territoryFilter = "";
    private string? _selectedScript;
    private static readonly string[] Pages = ["总览", "显示与绘制", "智能走位", "机制库", "录制回放", "诊断"];
    private readonly VisualScriptEditor _visualEditor = new();
    private readonly BlundervilleRouteEditor _routeEditor = new();

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
        _routeEditor.Draw();
        if (!_visible) return;

        using var theme = PromeUiTheme.PushWindowStyle();
        ImGui.SetNextWindowSize(new Vector2(900, 700), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(780, 520), new Vector2(1800, 1400));
        if (!ImGui.Begin("PromeRadar 设置###MasterPlayer 设置", ref _visible))
        {
            ImGui.End();
            return;
        }

        try
        {
            if (ImGui.BeginChild("##navigation", new Vector2(138, -1), false))
            {
                ImGui.TextColored(AccentColor, "PromeRadar");
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
                    case 3: DrawMechanicsTab(); break;
                    case 4: DrawReplayTab(); break;
                    case 5: DrawDiagnosticsTab(); break;
                }
            }
            ImGui.EndChild();
        }
        finally { ImGui.End(); }
    }

    private void DrawOverviewTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        DrawCurrentArea();

        SectionHeader("功能模块");
        changed |= ModuleRow("真实世界绘制", "在游戏 3D 世界中绘制机制范围与安全点提示。", settings.EnableRadar, v => settings.EnableRadar = v);
        changed |= ModuleRow("雷达悬浮窗", "俯视场地、机制和走位路线。", settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        ModuleRow("智能走位", "统一控制接近目标、躲避与机制移动；关闭后仅绘制和指路。", settings.EnableSmartMovement, v => MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(v));
        changed |= ModuleRow("机制库", "自动加载当前区域已启用的机制，按副本管理。", settings.EnableMechanics, v => settings.EnableMechanics = v);

        if (changed)
            settings.Save();

        SectionHeader("当前状态");
        DrawOverviewStatus(settings);

        SectionHeader("录制与反馈");
        DrawRecordControls();
        Hint("躲错、漏画或卡住时：用 ARR 录下那一把，把录像和出问题的时间点交给 AI，按 docs/ARR解析与模拟流程.md 修复。");
    }

    // Current area, whether its mechanics are enabled, and which scripts run here.
    private void DrawCurrentArea()
    {
        var plugin = MasterPlayerPlugin.Instance;
        var territory = Svc.ClientState.TerritoryType;
        var settings = MasterPlayerSettings.Instance;
        ImGui.TextColored(AccentColor, $"{MechanicHost.MechanicTerritoryCatalog.ResolveName(territory)}（{territory}）");
        var enabled = settings.IsTerritoryEnabled(territory);
        ImGui.SameLine();
        if (ImGui.Checkbox("启用本区域机制##overview_area", ref enabled))
        {
            settings.TerritoryEnabled[territory] = enabled;
            settings.Save();
        }
        var here = plugin?.Scripts?.Scripts.Where(d => d.Metadata.Territorys.Contains(territory)).ToArray() ?? [];
        if (here.Length == 0)
        {
            Hint(plugin?.MechanicHost?.ActiveModuleName is { } module ? $"本区域无专属脚本，使用内置机制模块：{module}" : "本区域无专属脚本，读条范围由通用躲避识别。");
            return;
        }
        foreach (var descriptor in here)
        {
            var running = plugin!.Scripts!.Running.ContainsKey(descriptor.Metadata.Id);
            ImGui.TextColored(running ? OnColor : HintColor, $"{(running ? "●" : "○")} {descriptor.Metadata.Name}");
            ImGui.SameLine();
            if (ImGui.SmallButton($"设置##goto_{descriptor.Metadata.Id}"))
            {
                _page = 3; _mechanicsPage = 0; _selectedScript = descriptor.Metadata.Id; _scriptFilter = ""; _scriptScope = 0;
            }
        }
    }

    private static void DrawRecordControls()
    {
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        if (recorder == null)
            return;
        if (recorder.IsRecording)
        {
            if (ImGui.Button("停止录制")) recorder.StopRecording();
            ImGui.SameLine();
            ImGui.TextColored(OnColor, $"● 录制中 {(int)recorder.Elapsed.TotalMinutes:D2}:{recorder.Elapsed.Seconds:D2}（{recorder.EventCount} 事件）");
        }
        else if (ImGui.Button("开始录制"))
            recorder.StartRecording();
        ImGui.SameLine();
        if (ImGui.Button("打开回放窗口"))
            MasterPlayerPlugin.Instance?.ReplayWindow?.Toggle();
    }

    private static void DrawOverviewStatus(MasterPlayerSettings settings)
    {
        try
        {
            var setting = MasterPlayerPlugin.Instance!.SafeZone.Setting;
            var status = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
            var scripts = MasterPlayerPlugin.Instance?.Scripts;
            var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow);

            HintInline($"运行中脚本 {scripts?.Running.Count ?? 0} / {scripts?.Scripts.Count ?? 0}");
            HintInline($"活动危险区 {snapshot.DangerAreas.Count}");
            Hint($"插件安全区：{status.PhaseText}");

            var needsSafeZone = settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableSmartMovement;
            if (needsSafeZone && !setting.EnableSafeZone)
                ImGui.TextColored(WarningColor, "插件解算被外部关闭，机制范围可能无法显示；重新加载插件可恢复。");
            if (settings.EnableSmartMovement &&
                (Spatial.HostMovement.AutomaticMovementEnabled || Spatial.HostMovement.HasControl))
            {
                ImGui.TextColored(WarningColor, "PR 本体安全区正在使用自动移动，插件已让位。");
                if (ImGui.Button("关闭 PR 本体安全区自动移动"))
                    Spatial.HostMovement.DisableAutomaticMovement();
            }

            var anyModuleOn = settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableSmartMovement || settings.EnableMechanics;
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
        changed |= Check("仅在有机制的区域显示", settings.RadarOnlyWithScript, v => settings.RadarOnlyWithScript = v);

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

        ModuleRow("智能走位", "接近目标、脱险和机制站位由同一个规划器协调，机制站位优先。", settings.EnableSmartMovement, v => MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(v));
        Hint("关闭后继续显示机制与路线，不控制移动、跳跃或朝向。DTR 或 /prpr ai on/off 也可切换。");
        SectionHeader("接近目标偏好");

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

        var slot = Math.Clamp(settings.FollowSlot, 0, 7);
        if (ImGui.Combo("跟随队友", ref slot, "不指定\0小队第 2 位\0小队第 3 位\0小队第 4 位\0小队第 5 位\0小队第 6 位\0小队第 7 位\0小队第 8 位\0"))
        {
            settings.FollowSlot = slot;
            changed = true;
        }
        Hint("按小队列表顺序（第 1 位是自己）。不指定时按下方选项决定是否跟随当前目标。");

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

        SectionHeader("避险偏好");
        changed |= Check("优化撤离时机", settings.OptimizeDodgeTiming, v => settings.OptimizeDodgeTiming = v);
        Hint("有安全路线时延后起步，保留输出和读条时间；按实际路程与判定时间撤离，无需选中目标。");
        var margin = settings.DodgeDangerMargin;
        if (PluginUi.Slider("危险区安全余量", ref margin, 0f, 2f, "%.2f 码"))
        {
            settings.DodgeDangerMargin = margin;
            changed = true;
        }
        Hint("注册危险区时向外扩张的安全余量，智能躲避会与危险区边缘至少保持该距离。");
        Hint("所有机制的移动控制均受智能走位总开关控制，脚本内的自动竞速等选项只在总开关开启时生效。");

        if (changed)
            settings.Save();
    }

    private void DrawMechanicsTab()
    {
        var settings = MasterPlayerSettings.Instance;
        if (ModuleRow("启用机制库", "统一控制内置机制和自定义脚本，关闭某副本会停用该区域的全部机制。", settings.EnableMechanics, v => settings.EnableMechanics = v)) settings.Save();
        _mechanicsPage = PluginUi.TabBar("##mechanics_sections", ["机制脚本", "按区域启停"], _mechanicsPage);
        if (_mechanicsPage == 0) DrawScriptsTab();
        else DrawTerritories();
    }

    private void DrawTerritories()
    {
        var plugin = MasterPlayerPlugin.Instance;
        if (plugin == null) return;
        var settings = MasterPlayerSettings.Instance;
        var current = Svc.ClientState.TerritoryType;
        var enabled = settings.IsTerritoryEnabled(current);
        if (ImGui.Checkbox($"启用当前区域（{current}）的机制", ref enabled))
        {
            settings.TerritoryEnabled[current] = enabled;
            settings.Save();
        }
        Hint("关闭区域后，该区域的机制绘制、提示和走位同时停用。未列出的区域可在进入后通过上方开关管理。");
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##territory_filter", "搜索副本名称 / 区域 ID", ref _territoryFilter, 256);
        var entries = (plugin.MechanicHost?.Territories ?? []).ToDictionary(entry => entry.TerritoryId);
        foreach (var script in plugin.Scripts?.Scripts ?? [])
        {
            foreach (var territory in script.Metadata.Territorys)
                if (!entries.ContainsKey(territory))
                    entries[territory] = new(territory, MechanicHost.MechanicTerritoryCatalog.ResolveName(territory), 0);
        }
        foreach (var territory in settings.TerritoryEnabled.Keys.Append(current))
            if (!entries.ContainsKey(territory))
                entries[territory] = new(territory, MechanicHost.MechanicTerritoryCatalog.ResolveName(territory), 0);
        if (!ImGui.BeginTable("##territories", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new Vector2(-1, -1))) return;
        ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 45);
        ImGui.TableSetupColumn("副本 / 区域", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("区域 ID", ImGuiTableColumnFlags.WidthFixed, 75);
        ImGui.TableHeadersRow();
        var filter = _territoryFilter.Trim();
        foreach (var entry in entries.Values.OrderByDescending(entry => entry.TerritoryId == current).ThenBy(entry => entry.Name))
        {
            if (!entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && !entry.TerritoryId.ToString().Contains(filter)) continue;
            ImGui.PushID(entry.TerritoryId.ToString());
            ImGui.TableNextRow(); ImGui.TableNextColumn();
            var active = settings.IsTerritoryEnabled(entry.TerritoryId);
            if (ImGui.Checkbox("##enabled", ref active)) { settings.TerritoryEnabled[entry.TerritoryId] = active; settings.Save(); }
            ImGui.TableNextColumn(); ImGui.TextWrapped(entry.Name);
            ImGui.TableNextColumn(); ImGui.TextUnformatted(entry.TerritoryId.ToString());
            ImGui.PopID();
        }
        ImGui.EndTable();
    }

    private void DrawScriptsTab()
    {
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null) { Hint("脚本管理器尚未就绪。"); return; }
        var settings = MasterPlayerSettings.Instance;
        Hint("内置预设随插件加载，自定义脚本放在配置目录；专属脚本优先于内置机制模块。");
        if (ImGui.Button("刷新脚本")) manager.ReloadScripts();
        ImGui.SameLine();
        if (ImGui.Button("打开脚本目录")) OpenScriptsDirectory(manager.ScriptsDirectory);
        ImGui.SameLine();
        if (ImGui.Button("新建可视化脚本")) _visualEditor.Open();
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
                var status = running ? "运行中" : !settings.EnableMechanics ? "机制库已关闭"
                    : descriptor.Metadata.Territorys.Length > 0 && descriptor.Metadata.Territorys.All(t => !settings.IsTerritoryEnabled(t)) ? "区域已关闭"
                    : manager.IsScriptEnabled(id) ? "已启用 · 进入区域后运行" : "已停用";
                ImGui.TextColored(running ? OnColor : HintColor, $"{SourceLabel(descriptor.Source)} · {status}");
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

        SectionHeader("插件安全区状态");
        DrawSafeZoneStatus(settings);

        SectionHeader("机制模块");
        DrawMechanicModules(settings);

        SectionHeader("机制处理器");
        DrawMechanicHandlers();
    }

    private void DrawReplayTab()
    {
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        var replay = MasterPlayerPlugin.Instance?.Replay;
        if (recorder == null || replay == null)
        {
            ImGui.TextDisabled("（录制回放未初始化）");
            return;
        }

        DrawRecordControls();
        if (!recorder.IsRecording && !string.IsNullOrEmpty(recorder.StatusMessage))
            HintInline(recorder.StatusMessage);
        if (replay.SessionActive)
            ImGui.TextColored(OnColor, $"● 回放中：{replay.ReplayScriptCount} 个脚本，{replay.Clock:F1} s");
        Hint("回放窗口可查看场地预览、拖动时间轴或对比多份日志；/prpr rec、/prpr replay 也可操作。");
        Hint("插件录制（.mpreplay.jsonl）用于在回放窗口检查绘制；修机制请用 ARR 录像，见 docs/ARR解析与模拟流程.md。");

        var autoRecord = MasterPlayerSettings.Instance.AutoRecordOnBattleStart;
        if (ImGui.Checkbox("战斗开始时自动录制（战斗结束自动停止）", ref autoRecord))
        {
            MasterPlayerSettings.Instance.AutoRecordOnBattleStart = autoRecord;
            MasterPlayerSettings.Instance.Save();
        }
    }

    private static void DrawMechanicModules(MasterPlayerSettings settings)
    {
        var bridge = MasterPlayerPlugin.Instance?.MechanicHost;
        HintInline("实时世界输入机制运行库，驱动内置机制模块");
        if (bridge == null)
        {
            ImGui.TextDisabled("（机制模块宿主未初始化）");
            return;
        }
        if (!bridge.IsAvailable)
        {
            ImGui.TextColored(ErrorColor, $"机制运行库不可用：{bridge.LastError ?? "初始化失败"}");
            return;
        }

        var maturity = Math.Clamp(settings.MechanicModuleMinMaturity, 1, 4) - 1;
        if (ImGui.Combo("模块成熟度门槛", ref maturity, "开发中 (WIP)\0第三方贡献 (Contributed)\0已验证 (Verified)\0支持 AI (AISupport)\0"))
        {
            settings.MechanicModuleMinMaturity = maturity + 1;
            settings.Save();
        }

        ImGui.Text($"状态：{(!bridge.IsEnabled ? "已停用" : bridge.IsSuppressed ? "当前区域由专属脚本接管" : "运行中")}");
        HintInline($"效果包捕获：{(bridge.CaptureActive ? "正常" : "不可用（效果数组组件降级）")}");
        ImGui.Text($"已加载模块 {bridge.LoadedModuleCount} / 待激活 {bridge.PendingModuleCount}");
        ImGui.Text($"激活模块：{bridge.ActiveModuleName ?? "无"}");
        if (bridge.ActiveZoneModuleName is { } zoneModule)
            HintInline($"区域模块：{zoneModule}");
        if (bridge.DrawModuleName is { } drawModule)
            HintInline($"绘制来源：{drawModule}");
        if (bridge.NavigationActive)
            HintInline($"导航区：危险 {bridge.NavDangerZoneCount} / 目标 {bridge.NavGoalZoneCount} / 障碍 {bridge.NavObstacleCount} / 边界 {(bridge.NavBoundaryActive ? "有" : "无")}");
        if (bridge.SpecialModeHint is { } specialMode)
            ImGui.TextColored(WarningColor, $"特殊模式：{specialMode}");
        if (bridge.PredictedDamageText is { } predicted)
            HintInline($"预计伤害：{predicted}");
        if (bridge.CurrentTimeline is { } timeline)
        {
            var rows = MechanicHost.MechanicTimelineView.BuildRows(timeline, bridge.MechanicClockUtc);
            var current = rows.FirstOrDefault(r => r.IsCurrent);
            HintInline($"阶段：{(timeline.PhaseName.Length != 0 ? timeline.PhaseName : "—")}｜当前：{(current != null ? $"{current.Name}（剩 {current.Seconds:F1}s）" : "—")}");
            var next = rows.Where(r => !r.IsCurrent).Take(3).Select(r => r.IsBranch ? "分支?" : $"{r.Name} {r.Seconds:F1}s").ToArray();
            if (next.Length != 0)
                HintInline($"后续：{string.Join(" → ", next)}");
        }
        if (bridge.CaptureErrorCount != 0)
            ImGui.TextColored(WarningColor, $"绘制捕获错误累计 {bridge.CaptureErrorCount} 起（{bridge.DrawCaptureError ?? "详见日志"}）");
        if (bridge.LastError is { } error)
            ImGui.TextColored(WarningColor, $"最近错误：{error}");

        var hints = bridge.CurrentHints;
        if (hints.Count != 0)
        {
            SectionHeader("机制提示");
            foreach (var (text, risk) in hints)
                ImGui.TextColored(risk ? ErrorColor : OnColor, text);
        }
    }

    private static void DrawSafeZoneStatus(MasterPlayerSettings settings)
    {
        try
        {
            var setting = MasterPlayerPlugin.Instance!.SafeZone.Setting;
            var status = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
            var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow);

            Hint($"解算：{(setting.EnableSafeZone ? "开启" : "关闭")}｜智能走位：{(settings.EnableSmartMovement ? "开启" : "关闭")}");
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
        Hint(descriptor.Source == ScriptSource.BuiltIn ? "来源：插件内置" : "来源：配置目录");
        var enabled = manager.IsScriptEnabled(id);
        if (PluginUi.Toggle("enabled", "启用此脚本", "进入适用区域后自动运行。", ref enabled)) manager.SetScriptEnabled(id, enabled);
        if (descriptor.Source == ScriptSource.Visual && ImGui.Button("编辑脚本")) _visualEditor.Open(descriptor.SourcePath);
        if (id == "masterplayer.goldsaucer.blunderville" && ImGui.Button("编辑糖豆人路网（一二三关）")) _routeEditor.Open();
        if (!string.IsNullOrWhiteSpace(metadata.Note)) ImGui.TextWrapped(metadata.Note);
        Hint(metadata.Territorys.Length == 0 ? "适用区域：通用"
            : "适用区域：" + string.Join("、", metadata.Territorys.Select(t => $"{MechanicHost.MechanicTerritoryCatalog.ResolveName(t)}（{t}）")));
        if (manager.Running.TryGetValue(id, out var run))
        {
            DrawScriptSettings(run);
            DrawMechanismToggles(manager, id, run.Script.GetMechanisms());
        }
        else
        {
            DrawMechanismToggles(manager, id, Mechanisms(descriptor));
            Hint("脚本设置在脚本运行后显示；机制开关现在即可调整，进入区域后生效。");
        }
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

    private readonly Dictionary<string, string[]> _mechanisms = [];
    // A script lists its mechanisms without starting; create it once to read the list.
    private string[] Mechanisms(ScriptDescriptor descriptor)
    {
        if (_mechanisms.TryGetValue(descriptor.Metadata.Id, out var cached)) return cached;
        try { cached = descriptor.CreateInstance().GetMechanisms().ToArray(); }
        catch { cached = []; }
        return _mechanisms[descriptor.Metadata.Id] = cached;
    }

    private static string SourceLabel(ScriptSource source) => source switch
    {
        ScriptSource.BuiltIn => "内置",
        ScriptSource.Visual => "可视化",
        _ => "自定义",
    };

    private static void DrawMechanismToggles(ScriptManager manager, string scriptId, IEnumerable<string> list)
    {
        var mechanisms = list.ToArray();
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
