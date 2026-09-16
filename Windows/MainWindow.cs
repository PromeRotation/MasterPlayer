using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;

namespace MasterPlayer.Windows;

public sealed class MainWindow : IDisposable
{
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

        ImGui.SetNextWindowSize(new Vector2(640, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin("MasterPlayer 主面板", ref _visible))
        {
            ImGui.End();
            return;
        }

        DrawModuleToggles();
        ImGui.Separator();
        DrawRadarOverlaySection();
        ImGui.Separator();
        DrawAoeSection();
        ImGui.Separator();
        DrawFollowSection();
        ImGui.Separator();
        DrawDodgeSection();
        ImGui.Separator();
        DrawScriptsSection();
        ImGui.Separator();
        DrawMechanicHandlers();
        ImGui.Separator();
        DrawStatus();

        ImGui.End();
    }

    private static void DrawModuleToggles()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        var enableRadar = settings.EnableRadar;
        if (ImGui.Checkbox("真实世界绘制（3D 范围提示）", ref enableRadar))
        {
            settings.EnableRadar = enableRadar;
            changed = true;
        }

        var enableAutoDodge = settings.EnableAutoDodge;
        if (ImGui.Checkbox("智能躲避（自动走位）", ref enableAutoDodge))
        {
            settings.EnableAutoDodge = enableAutoDodge;
            changed = true;
        }

        var enableMechanics = settings.EnableMechanics;
        if (ImGui.Checkbox("智能机制处理", ref enableMechanics))
        {
            settings.EnableMechanics = enableMechanics;
            changed = true;
        }

        var enableScripts = settings.EnableScripts;
        if (ImGui.Checkbox("机制脚本框架", ref enableScripts))
        {
            settings.EnableScripts = enableScripts;
            changed = true;
        }

        var debugMode = settings.DebugMode;
        if (ImGui.Checkbox("调试模式", ref debugMode))
        {
            settings.DebugMode = debugMode;
            changed = true;
        }

        if (changed)
            settings.Save();
    }

    private static void DrawRadarOverlaySection()
    {
        if (!ImGui.CollapsingHeader("雷达悬浮窗"))
            return;

        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        var enableOverlay = settings.EnableRadarOverlay;
        if (ImGui.Checkbox("启用雷达悬浮窗", ref enableOverlay))
        {
            settings.EnableRadarOverlay = enableOverlay;
            changed = true;
        }

        var centerMode = settings.RadarCenterMode;
        if (ImGui.Combo("中心模式", ref centerMode, "场地基准（脚本注册的场地边界）\0玩家摄像机基准\0"))
        {
            settings.RadarCenterMode = centerMode;
            changed = true;
        }

        var rotateWithCamera = settings.RadarRotateWithCamera;
        if (ImGui.Checkbox("旋转跟随摄像机", ref rotateWithCamera))
        {
            settings.RadarRotateWithCamera = rotateWithCamera;
            changed = true;
        }

        var scale = settings.RadarScale;
        if (ImGui.SliderFloat("缩放", ref scale, 0.5f, 3f))
        {
            settings.RadarScale = scale;
            changed = true;
        }

        var lockOverlay = settings.RadarOverlayLock;
        if (ImGui.Checkbox("锁定位置（鼠标穿透）", ref lockOverlay))
        {
            settings.RadarOverlayLock = lockOverlay;
            changed = true;
        }

        var transparent = settings.RadarOverlayTransparent;
        if (ImGui.Checkbox("透明背景", ref transparent))
        {
            settings.RadarOverlayTransparent = transparent;
            changed = true;
        }

        if (changed)
            settings.Save();
    }

    private void DrawAoeSection()
    {
        if (!ImGui.CollapsingHeader("AOE 绘制设置"))
            return;

        var settings = MasterPlayerSettings.Instance;
        ImGui.TextWrapped("雷达和真实世界分别设置。颜色的 A 值控制不透明度：0 为完全透明，1 为完全不透明。");
        var changed = DrawAoeAppearance("真实世界", settings.WorldAoe);
        changed |= DrawAoeAppearance("雷达", settings.RadarAoe);
        ImGui.TextWrapped("按技能／机制选择显示位置。隐藏的危险区仍参与躲避；未出现过的技能会在识别后加入列表。");
        ImGui.InputText("筛选技能／机制", ref _aoeFilter, 256);
        if (ImGui.BeginChild("##aoe_sources", new Vector2(-1, 220), true))
        {
            foreach (var (key, visibility) in settings.AoeVisibility.OrderBy(pair => pair.Value.Name))
            {
                if (!visibility.Name.Contains(_aoeFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                ImGui.PushID(key);
                changed |= ImGui.Checkbox("真实世界", ref visibility.World);
                ImGui.SameLine();
                changed |= ImGui.Checkbox("雷达", ref visibility.Radar);
                ImGui.SameLine();
                ImGui.TextWrapped(visibility.Name);
                ImGui.PopID();
            }
        }
        ImGui.EndChild();
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
        ImGui.TextDisabled("逐渐加深时，填充颜色的 A 值为最大不透明度。");
        changed |= ImGui.Checkbox("显示危险区", ref settings.ShowDanger);
        changed |= ImGui.Checkbox("显示安全区", ref settings.ShowSafe);
        ImGui.Text("显示的形状");
        changed |= ImGui.Checkbox("圆形", ref settings.ShowCircles);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("月环", ref settings.ShowDonuts);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("扇形", ref settings.ShowFans);
        changed |= ImGui.Checkbox("矩形／直线", ref settings.ShowRectangles);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("多边形", ref settings.ShowPolygons);
        ImGui.SameLine();
        changed |= ImGui.Checkbox("其他形状", ref settings.ShowOtherShapes);
        ImGui.TreePop();
        return changed;
    }

    private static void DrawFollowSection()
    {
        if (!ImGui.CollapsingHeader("战斗中跟随"))
            return;

        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        var enableFollow = settings.EnableFollow;
        if (ImGui.Checkbox("启用跟随", ref enableFollow))
        {
            settings.EnableFollow = enableFollow;
            changed = true;
        }

        var slot = settings.FollowSlot;
        if (ImGui.SliderInt("跟随槽位", ref slot, 0, 7))
        {
            settings.FollowSlot = slot;
            changed = true;
        }
        ImGui.TextDisabled("0 = 自己/不跟随队友；1~7 = 小队 UI 序");

        var targetWhenNoSlot = settings.FollowTargetWhenNoSlot;
        if (ImGui.Checkbox("无槽位时跟随当前目标", ref targetWhenNoSlot))
        {
            settings.FollowTargetWhenNoSlot = targetWhenNoSlot;
            changed = true;
        }

        var distance = settings.FollowDistance;
        if (ImGui.SliderFloat("跟随距离", ref distance, 0.5f, 10f, "%.1f y"))
        {
            settings.FollowDistance = distance;
            changed = true;
        }

        var duringCombat = settings.FollowDuringCombat;
        if (ImGui.Checkbox("战斗中生效", ref duringCombat))
        {
            settings.FollowDuringCombat = duringCombat;
            changed = true;
        }

        var outOfCombat = settings.FollowOutOfCombat;
        if (ImGui.Checkbox("非战斗中也生效", ref outOfCombat))
        {
            settings.FollowOutOfCombat = outOfCombat;
            changed = true;
        }

        if (changed)
            settings.Save();
    }

    private static void DrawDodgeSection()
    {
        if (!ImGui.CollapsingHeader("AI 躲避"))
            return;

        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        var margin = settings.DodgeDangerMargin;
        if (ImGui.SliderFloat("危险区安全余量", ref margin, 0f, 2f, "%.2f y"))
        {
            settings.DodgeDangerMargin = margin;
            changed = true;
        }
        ImGui.TextWrapped("注册危险区时向外扩张的安全余量。AI 躲避会与危险区边缘至少保持该距离。");
        ImGui.TextWrapped("开启雷达或真实世界绘制会启用 PR 安全区系统；开启智能躲避还会启用 PR 自动移动。只开启绘制不会启用自动移动。");

        if (changed)
            settings.Save();
    }

    private static void DrawScriptsSection()
    {
        if (!ImGui.CollapsingHeader("脚本"))
            return;

        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null)
        {
            ImGui.TextDisabled("（ScriptManager 未初始化）");
            return;
        }

        if (!MasterPlayerSettings.Instance.EnableScripts)
            ImGui.TextDisabled("脚本框架总开关已关闭，运行中的脚本已全部停止。");

        if (ImGui.Button("刷新脚本"))
            manager.RefreshCatalog();

        ImGui.SameLine();
        if (ImGui.Button("打开脚本目录"))
            OpenScriptsDirectory(manager.ScriptsDirectory);

        ImGui.SameLine();
        ImGui.Text($"当前区域 ID：{Svc.ClientState.TerritoryType}    脚本数：{manager.Scripts.Count}");

        if (!string.IsNullOrEmpty(manager.StatusMessage))
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), manager.StatusMessage);

        if (!string.IsNullOrEmpty(manager.ErrorMessage))
        {
            if (ImGui.BeginChild("##script_errors", new Vector2(-1, 72), true))
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.45f, 0.45f, 1f));
                ImGui.TextWrapped(manager.ErrorMessage);
                ImGui.PopStyleColor();
            }
            ImGui.EndChild();
        }

        if (manager.Scripts.Count == 0)
        {
            ImGui.TextDisabled("脚本目录为空。把 .cs 脚本放入脚本目录后点「刷新脚本」。");
            return;
        }

        if (ImGui.BeginChild("##script_list", new Vector2(-1, 260), true))
        {
            foreach (var descriptor in manager.Scripts)
                DrawScriptRow(manager, descriptor);
        }
        ImGui.EndChild();
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
        ImGui.Text("机制处理器");

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

    private static void DrawStatus()
    {
        ImGui.Text("本体状态");

        try
        {
            var plugin = PromeRotation.Plugin.Instance;
            var setting = plugin.SafeZoneSetting;
            var status = plugin.SafeZoneMovementStatus;

            ImGui.Text($"安全区解算：{(setting.EnableSafeZone ? "开" : "关")}    自动移动接管：{(setting.EnableAutoMovement ? "开" : "关")}");
            ImGui.Text($"移动控制权：{(status.HasControl ? "持有" : "无")}    阶段：{status.PhaseText}");
            ImGui.Text($"最新解算方案：{(status.LatestSolution != null ? "有" : "无")}");

            var settings = MasterPlayerSettings.Instance;
            var snapshot = plugin.SafeZoneManager.BuildSnapshot(DateTime.UtcNow);
            ImGui.Text($"当前危险区：{snapshot.DangerAreas.Count}    运行中脚本：{MasterPlayerPlugin.Instance?.Scripts?.Running.Count ?? 0}");
            if ((settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableAutoDodge) && !setting.EnableSafeZone)
                ImGui.TextColored(new Vector4(1f, 0.65f, 0.3f, 1f), "PR 安全区系统已关闭，机制范围无法显示；请在 PR 中重新开启。");
            if (settings.EnableAutoDodge && !setting.EnableAutoMovement)
                ImGui.TextColored(new Vector4(1f, 0.65f, 0.3f, 1f), "PR 自动移动已关闭，智能躲避不会走位；请在 PR 中重新开启。");

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
