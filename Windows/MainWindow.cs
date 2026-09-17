using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;

namespace MasterPlayer.Windows;

/// <summary>
/// 设置面板：分页签组织（总览/雷达悬浮窗/绘制外观/自动化/脚本/诊断）。
/// 由 UiBuilder.Draw（主线程）驱动；PR 插件管理器按钮或聊天指令 /prmp 开关。
/// </summary>
public sealed class MainWindow : IDisposable
{
    private static readonly Vector4 AccentColor = new(0.45f, 0.78f, 1f, 1f);
    private static readonly Vector4 OnColor = new(0.40f, 0.90f, 0.50f, 1f);
    private static readonly Vector4 OffColor = new(0.55f, 0.58f, 0.65f, 1f);
    private static readonly Vector4 WarningColor = new(1f, 0.65f, 0.30f, 1f);
    private static readonly Vector4 HintColor = new(0.62f, 0.66f, 0.74f, 1f);
    private static readonly Vector4 ErrorColor = new(1f, 0.45f, 0.45f, 1f);

    private bool _visible;
    private bool _disposed;
    private string _aoeFilter = "";

    public void Toggle()
    {
        if (!_disposed)
            _visible = !_visible;
    }

    public void Draw()
    {
        if (_disposed || !_visible)
            return;

        ImGui.SetNextWindowSize(new Vector2(680, 620), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(560, 440), new Vector2(1600, 1200));
        if (!ImGui.Begin("MasterPlayer 设置", ref _visible))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##main_tabs"))
        {
            if (ImGui.BeginTabItem("总览"))
            {
                DrawOverviewTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("雷达悬浮窗"))
            {
                DrawRadarTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("绘制外观"))
            {
                DrawAppearanceTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("自动化"))
            {
                DrawAutomationTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("脚本"))
            {
                DrawScriptsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("诊断"))
            {
                DrawDiagnosticsTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawOverviewTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        SectionHeader("功能模块");
        changed |= ModuleRow("真实世界绘制", "在游戏 3D 世界中绘制机制范围与安全点提示。", settings.EnableRadar, v => settings.EnableRadar = v);
        changed |= ModuleRow("雷达悬浮窗", "屏幕上的圆形 2D 俯视雷达；详见「雷达悬浮窗」页。", settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        changed |= ModuleRow("智能躲避", "按 AOE 判定时间自动规划走位，使用安全区 Reborn。", settings.EnableAutoDodge, v => settings.EnableAutoDodge = v);
        changed |= ModuleRow("战斗中跟随", "自动跟随队友或当前目标；详见「自动化」页。", settings.EnableFollow, v => settings.EnableFollow = v);
        changed |= ModuleRow("机制脚本框架", "按区域自动运行机制脚本；关闭后运行中的脚本全部停止。", settings.EnableScripts, v => settings.EnableScripts = v);

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
            ImGui.SameLine(0f, 18f);
            HintInline($"运行中脚本 {scripts?.Running.Count ?? 0} / {scripts?.Scripts.Count ?? 0}");
            ImGui.SameLine(0f, 18f);
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
        if (ImGui.SliderFloat("缩放", ref scale, 0.3f, 3f))
        {
            settings.RadarScale = scale;
            changed = true;
        }

        var opacity = settings.RadarBackgroundOpacity;
        if (ImGui.SliderFloat("背景不透明度", ref opacity, 0f, 1f))
        {
            settings.RadarBackgroundOpacity = opacity;
            changed = true;
        }

        changed |= Check("显示罗盘", settings.RadarShowCompass, v => settings.RadarShowCompass = v);
        changed |= Check("显示距离环", settings.RadarShowRangeRings, v => settings.RadarShowRangeRings = v);
        changed |= Check("危险区临期脉冲", settings.RadarPulseDanger, v => settings.RadarPulseDanger = v);

        SectionHeader("交互");
        changed |= Check("锁定位置（鼠标穿透）", settings.RadarOverlayLock, v => settings.RadarOverlayLock = v);
        Hint("解锁状态下：拖拽圆盘移动位置，滚轮缩放，右键打开快捷菜单。");

        if (changed)
            settings.Save();
    }

    private void DrawAppearanceTab()
    {
        var settings = MasterPlayerSettings.Instance;
        Hint("「真实世界」是游戏内的 3D 范围提示，「雷达」是悬浮窗里的 2D 图示，两者独立设置。颜色的 A 值控制不透明度。");
        ImGui.Spacing();

        var changed = DrawAoeAppearance("真实世界", settings.WorldAoe);
        changed |= DrawAoeAppearance("雷达悬浮窗", settings.RadarAoe);

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
        if (!ImGui.TreeNode(label))
            return false;

        var flags = ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.Float;
        var changed = ImGui.ColorEdit4("危险区填充", ref settings.DangerFill, flags);
        changed |= ImGui.ColorEdit4("危险区边框", ref settings.DangerOutline, flags);
        changed |= ImGui.ColorEdit4("安全区填充", ref settings.SafeFill, flags);
        changed |= ImGui.ColorEdit4("安全区边框", ref settings.SafeOutline, flags);
        changed |= ImGui.Checkbox("危险预告逐渐加深", ref settings.FadeDanger);
        Hint("逐渐加深时，填充颜色的 A 值为最大不透明度。");
        changed |= ImGui.Checkbox("显示危险区", ref settings.ShowDanger);
        changed |= ImGui.Checkbox("显示安全区", ref settings.ShowSafe);
        ImGui.Text("显示的形状");
        changed |= ImGui.Checkbox("圆形", ref settings.ShowCircles);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("月环", ref settings.ShowDonuts);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("扇形", ref settings.ShowFans);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("矩形／直线", ref settings.ShowRectangles);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("多边形", ref settings.ShowPolygons);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("其他形状", ref settings.ShowOtherShapes);
        ImGui.TreePop();
        return changed;
    }

    private static void DrawAutomationTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        SectionHeader("战斗中跟随");
        changed |= Check("启用跟随", settings.EnableFollow, v => settings.EnableFollow = v);

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
        if (ImGui.SliderFloat("跟随距离", ref distance, 0.5f, 10f, "%.1f y"))
        {
            settings.FollowDistance = distance;
            changed = true;
        }
        Hint("怪物按双方目标圈边缘间距计算，玩家按中心距离计算。");

        changed |= Check("战斗中生效", settings.FollowDuringCombat, v => settings.FollowDuringCombat = v);
        changed |= Check("非战斗中也生效", settings.FollowOutOfCombat, v => settings.FollowOutOfCombat = v);

        SectionHeader("智能躲避");
        var margin = settings.DodgeDangerMargin;
        if (ImGui.SliderFloat("危险区安全余量", ref margin, 0f, 2f, "%.2f y"))
        {
            settings.DodgeDangerMargin = margin;
            changed = true;
        }
        Hint("注册危险区时向外扩张的安全余量，智能躲避会与危险区边缘至少保持该距离。");
        Hint("开启雷达或真实世界绘制会启用 PR 安全区系统；开启智能躲避还会启用 PR 自动移动。只开启绘制不会启用自动移动。");

        if (changed)
            settings.Save();
    }

    private static void DrawScriptsTab()
    {
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null)
        {
            ImGui.TextDisabled("（ScriptManager 未初始化）");
            return;
        }

        if (ImGui.Button("刷新脚本"))
            manager.ReloadScripts();

        ImGui.SameLine();
        if (ImGui.Button("打开脚本目录"))
            OpenScriptsDirectory(manager.ScriptsDirectory);

        ImGui.SameLine(0f, 18f);
        HintInline($"当前区域 ID：{Svc.ClientState.TerritoryType}    脚本数：{manager.Scripts.Count}");

        if (!MasterPlayerSettings.Instance.EnableScripts)
            ImGui.TextColored(WarningColor, "脚本框架总开关已关闭，运行中的脚本已全部停止。");

        if (!string.IsNullOrEmpty(manager.StatusMessage))
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), manager.StatusMessage);

        if (!string.IsNullOrEmpty(manager.ErrorMessage))
        {
            if (ImGui.BeginChild("##script_errors", new Vector2(-1f, 72f), true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, ErrorColor);
                ImGui.TextWrapped(manager.ErrorMessage);
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
        }

        if (manager.Scripts.Count == 0)
        {
            Hint("脚本目录为空。把 .cs 脚本放入脚本目录后点「刷新脚本」。");
            return;
        }

        if (ImGui.BeginChild("##script_list", new Vector2(-1f, -1f), true))
        {
            foreach (var descriptor in manager.Scripts)
                DrawScriptRow(manager, descriptor);
        }
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
                ImGui.Text($"异步解算：运行中={status.IsAsyncSolveRunning} 发起={status.AsyncSolveStartedCount} 采纳={status.AsyncSolveAcceptedCount} 取消={status.AsyncSolveCancelledCount} 丢弃={status.AsyncSolveDiscardedCount}");
                ImGui.Text($"区域快照 v{snapshot.Version}：危险区 {snapshot.DangerAreas.Count} / 目标区 {snapshot.GoalAreas.Count} / 障碍 {snapshot.HardObstacles.Count} / 边界 {(snapshot.ArenaBoundary != null ? "有" : "无")}");
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

    private static void DrawScriptRow(ScriptManager manager, ScriptDescriptor descriptor)
    {
        var metadata = descriptor.Metadata;
        var id = metadata.Id;
        var running = manager.Running.TryGetValue(id, out var run);

        ImGui.PushID(id);

        var enabled = manager.IsScriptEnabled(id);
        if (ImGui.Checkbox("##enabled", ref enabled))
            manager.SetScriptEnabled(id, enabled);

        ImGui.SameLine();

        var info = string.IsNullOrWhiteSpace(metadata.Name) ? metadata.SourcePath : metadata.Name;
        if (!string.IsNullOrWhiteSpace(metadata.Version))
            info += $"  v{metadata.Version}";
        if (!string.IsNullOrWhiteSpace(metadata.Author))
            info += $"  by {metadata.Author}";
        if (!string.IsNullOrWhiteSpace(metadata.Region))
            info += $"  [{metadata.Region}]";

        if (running)
        {
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), info);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.4f, 1f, 0.5f, 1f), "●运行中");
        }
        else
        {
            ImGui.Text(info);
        }

        if (ImGui.TreeNode("详情"))
        {
            if (!string.IsNullOrWhiteSpace(metadata.Note))
                ImGui.TextWrapped(metadata.Note);

            ImGui.TextDisabled(metadata.Territorys.Length == 0
                ? "适用区域：通用（不限区域）"
                : $"适用区域：{string.Join(", ", metadata.Territorys)}");
            ImGui.TextDisabled($"来源：{metadata.SourcePath}");

            if (running && run != null)
            {
                DrawScriptSettings(run);
                DrawMechanismToggles(manager, id, run);
            }
            else
            {
                ImGui.TextDisabled("（设置项与机制开关在脚本运行中时可编辑）");
            }

            ImGui.TreePop();
        }

        ImGui.PopID();
        ImGui.Separator();
    }

    private static void DrawScriptSettings(RunningScript run)
    {
        var store = run.Settings;
        if (store.RegisteredSettings.Count == 0)
            return;

        ImGui.Text("脚本设置");
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

        ImGui.Text("机制开关");
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
        var changed = ImGui.Checkbox(label, ref newValue);
        if (changed)
            apply(newValue);
        ImGui.SameLine();
        ImGui.TextColored(newValue ? OnColor : OffColor, newValue ? "● 已启用" : "○ 已关闭");
        Hint(description);
        ImGui.Spacing();
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
