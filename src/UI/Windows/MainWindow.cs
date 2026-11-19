using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.DalamudServices;
using MasterPlayer.Settings;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

/// <summary>
/// 设置面板（Material 风格）：左侧导航抽屉（总览/显示与绘制/智能走位/机制库/录制回放/诊断/技能序列/PR3 遥控 + 快捷开关），右侧为当前分区的卡片页面。
/// 由 UiBuilder.Draw（主线程）驱动；PR 插件管理器按钮或聊天指令 /prpr 开关。
/// 机制库与斗兽页面见 MainWindow.Library.cs；控件见 Md.cs。
/// </summary>
public sealed partial class MainWindow : IDisposable
{
    private bool _visible;
    private bool _disposed;
    private string _aoeFilter = "";
    private int _page, _displayPage;
    private static readonly string[] Pages = ["总览", "显示与绘制", "智能走位", "机制库", "录制回放", "诊断", "技能序列", "遥控"];
    private static readonly string[] PageSubtitles =
    [
        "当前区域、功能模块和运行状态",
        "雷达悬浮窗、游戏内绘制和按机制显示",
        "接近目标、跟随与避险偏好",
        "机制脚本和按区域启停",
        "插件录制与回放窗口",
        "解算、机制模块和处理器的运行情况",
        "PR 技能序列的编辑与运行",
        "XSZToolbox 遥控房间（需 PR3 权限）",
    ];
    private static readonly FontAwesomeIcon[] PageIcons =
        [FontAwesomeIcon.Home, FontAwesomeIcon.Eye, FontAwesomeIcon.Running, FontAwesomeIcon.Book, FontAwesomeIcon.Film, FontAwesomeIcon.Stethoscope, FontAwesomeIcon.ListOl, FontAwesomeIcon.Wifi];
    private readonly SkillSequencePage _sequencePage = new();
    private readonly RemoteControlPage _remotePage = new();
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

        var remoteVisible = Integration.RemoteControlClient.HasPermission;
        if (!remoteVisible)
        {
            if (_page == 7) _page = 0;
            _remotePage.Reset();
        }

        using var theme = PromeUiTheme.PushWindowStyle();
        ImGui.SetNextWindowSize(new Vector2(1060, 780), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(860, 560), new Vector2(2000, 1600));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        // New window id so the larger default size applies instead of the size saved for the old layout.
        var open = ImGui.Begin("PromeRadar 设置###MasterPlayer 设置 Material", ref _visible);
        ImGui.PopStyleVar();
        if (!open)
        {
            ImGui.End();
            return;
        }

        try
        {
            var em = ImGui.GetFontSize();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(em * 0.5f, em * 0.45f));
            ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, em * 0.35f);
            ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, em * 0.5f);
            if (ImGui.BeginTable("##layout", 2, ImGuiTableFlags.None, new Vector2(-1, -1)))
            {
                ImGui.TableSetupColumn("##nav", ImGuiTableColumnFlags.WidthFixed, MathF.Round(MathF.Max(190f, em * 11.5f)));
                ImGui.TableSetupColumn("##content", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                DrawNavigation(em);
                ImGui.TableNextColumn();
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(em * 1.4f, em * 1.1f));
                if (ImGui.BeginChild("##page_" + _page, new Vector2(-1, -1), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
                {
                    switch (_page)
                    {
                        case 0: DrawOverviewTab(); break;
                        case 1: DrawAppearanceTab(); break;
                        case 2: DrawAutomationTab(); break;
                        case 3: DrawMechanicsTab(); break;
                        case 4: DrawReplayTab(); break;
                        case 5: DrawDiagnosticsTab(); break;
                        case 6: Md.PageHeader(Pages[6], PageSubtitles[6]); _sequencePage.Draw(); break;
                        case 7 when remoteVisible: Md.PageHeader(Pages[7], PageSubtitles[7]); _remotePage.Draw(); break;
                    }
                }
                ImGui.EndChild();
                ImGui.PopStyleVar();
                ImGui.EndTable();
            }
            ImGui.PopStyleVar(4);
        }
        finally { ImGui.End(); }
    }

    // ── Navigation drawer ────────────────────────────────────────────────────────────────

    private void DrawNavigation(float em)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Md.Tone(Md.Accent, 0.045f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(em * 0.6f, em * 1f));
        if (ImGui.BeginChild("##navigation", new Vector2(-1, -1), false, ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            ImGui.Indent(em * 0.8f);
            ImGui.PushStyleColor(ImGuiCol.Text, Md.Accent);
            Md.Title("PromeRadar");
            ImGui.PopStyleColor();
            ImGui.Unindent(em * 0.8f);
            Md.Gap(0.6f);
            var remoteVisible = Integration.RemoteControlClient.HasPermission;
            for (var i = 0; i < Pages.Length; ++i)
                if ((i != 7 || remoteVisible) && Md.NavItem($"##nav{i}", PageIcons[i], Pages[i], _page == i))
                    _page = i;
            Md.Gap(0.5f);
            var start = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddLine(start + new Vector2(em * 0.6f, 0), start + new Vector2(ImGui.GetContentRegionAvail().X - em * 0.6f, 0), Md.U32(Md.Outline), 1f);
            Md.Gap(0.6f);
            ImGui.Indent(em * 0.8f);
            Md.SectionLabel("快捷开关");
            ImGui.Unindent(em * 0.8f);
            DrawQuickSwitches();
        }
        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    // Always-visible switches for the toggles flipped most often, so they never need a page change.
    private static void DrawQuickSwitches()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;
        var smart = settings.EnableSmartMovement;
        if (Md.DrawerSwitch("智能走位", ref smart))
            MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(smart);
        var aoe = settings.WorldAoe;
        var areas = settings.EnableRadar && aoe.ShowAreas;
        if (Md.DrawerSwitch("范围绘制", ref areas))
            changed |= ApplyWorldLayer(settings, x => aoe.ShowAreas = x, areas);
        var path = settings.EnableRadar && aoe.ShowPath;
        if (Md.DrawerSwitch("路线绘制", ref path))
            changed |= ApplyWorldLayer(settings, x => aoe.ShowPath = x, path);
        var radar = settings.EnableRadarOverlay;
        if (Md.DrawerSwitch("雷达", ref radar))
        {
            settings.EnableRadarOverlay = radar;
            changed = true;
        }
        var mechanics = settings.EnableMechanics;
        if (Md.DrawerSwitch("机制库", ref mechanics))
        {
            settings.EnableMechanics = mechanics;
            changed = true;
        }
        if (changed)
            settings.Save();

        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        if (recorder == null) return;
        var recording = recorder.IsRecording;
        var label = recording ? $"录制 {(int)recorder.Elapsed.TotalMinutes:D2}:{recorder.Elapsed.Seconds:D2}" : "录制";
        if (Md.DrawerSwitch(label + "###quick_record", ref recording))
        {
            if (recording) recorder.StartRecording();
            else recorder.StopRecording();
        }
    }

    // ── 总览 ─────────────────────────────────────────────────────────────────────────────

    private void DrawOverviewTab()
    {
        var settings = MasterPlayerSettings.Instance;
        Md.PageHeader(Pages[0], PageSubtitles[0]);

        DrawOverviewWarnings(settings);
        DrawCurrentArea();

        var changed = false;
        Md.BeginCard("modules", Md.CardKind.Outlined);
        Md.SectionLabel("功能模块");
        changed |= WorldAreaRow(settings);
        changed |= WorldPathRow(settings);
        changed |= Row("雷达悬浮窗", "俯视场地、机制和走位路线。", settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        changed |= Row("机制库", "自动加载当前区域已启用的机制，按副本管理。", settings.EnableMechanics, v => settings.EnableMechanics = v);
        Row("智能走位", "统一控制接近目标、躲避与机制移动；关闭后仅绘制和指路。", settings.EnableSmartMovement, v => MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(v));
        Md.EndCard();
        if (changed)
            settings.Save();

        DrawOverviewStatus();

        Md.BeginCard("record", Md.CardKind.Outlined);
        Md.SectionLabel("录制与反馈", "躲错、漏画或卡住时：用 ARR 录下那一把，把录像和出问题的时间点交给 AI，按 docs/ARR解析与模拟流程.md 修复。");
        DrawRecordRow();
        Md.EndCard();
    }

    // Problems that need the user's attention go first as banners, so they are not lost among
    // the status lines.
    private static void DrawOverviewWarnings(MasterPlayerSettings settings)
    {
        try
        {
            var plugin = MasterPlayerPlugin.Instance;
            if (plugin == null) return;
            var needsSafeZone = settings.EnableRadar || settings.EnableRadarOverlay || settings.EnableSmartMovement;
            if (needsSafeZone && !plugin.SafeZone.Setting.EnableSafeZone)
                Md.Banner("warn_safezone", FontAwesomeIcon.ExclamationTriangle, Md.Warning, "插件解算被外部关闭，机制范围可能无法显示；重新加载插件可恢复。");
            if (settings.EnableSmartMovement &&
                (Spatial.HostMovement.AutomaticMovementEnabled || Spatial.HostMovement.HasControl) &&
                Md.Banner("warn_host", FontAwesomeIcon.ExclamationTriangle, Md.Warning, "PR 本体安全区正在使用自动移动，插件已让位。", "关闭本体自动移动"))
                Spatial.HostMovement.DisableAutomaticMovement();
            if (!settings.EnableRadar && !settings.EnableRadarOverlay && !settings.EnableSmartMovement && !settings.EnableMechanics)
                Md.Banner("warn_off", FontAwesomeIcon.InfoCircle, Md.Accent, "所有模块均已关闭，打开下方开关即可开始使用。");
        }
        catch (Exception ex)
        {
            Md.MutedText($"状态读取失败：{ex.Message}");
        }
    }

    // Current area, whether its mechanics are enabled, and which scripts run here.
    private void DrawCurrentArea()
    {
        var plugin = MasterPlayerPlugin.Instance;
        var territory = Svc.ClientState.TerritoryType;
        var settings = MasterPlayerSettings.Instance;
        var here = plugin?.Scripts?.Scripts.Where(d => d.Metadata.Territorys.Contains(territory)).ToArray() ?? [];
        var runningHere = here.Count(d => plugin!.Scripts!.Running.ContainsKey(d.Metadata.Id));

        Md.BeginCard("area");
        Md.SectionLabel("当前区域");
        var enabled = settings.IsTerritoryEnabled(territory);
        var subtitle = here.Length == 0
            ? plugin?.MechanicHost?.ActiveModuleName is { } module ? $"区域 {territory} · 无专属脚本，使用内置机制模块：{module}" : $"区域 {territory} · 无专属脚本，读条范围由通用躲避识别"
            : $"区域 {territory} · {here.Length} 个专属脚本，{runningHere} 个运行中";
        var lead = new Md.Lead(runningHere != 0 ? FontAwesomeIcon.Play : FontAwesomeIcon.MapMarkerAlt, runningHere != 0 ? Md.Success : Md.Accent);
        Md.ListItem("current_area", lead, MechanicHost.MechanicTerritoryCatalog.ResolveName(territory), subtitle, false, ref enabled, out var toggled,
            tooltip: "右侧开关：启用或停用本区域的全部机制");
        if (toggled)
        {
            settings.TerritoryEnabled[territory] = enabled;
            settings.Save();
        }
        if (PlayerPerspective.IsDutyReplay)
            Md.MutedText(PlayerPerspective.LivePlayer is { } viewed
                ? $"ARR 观看角色：{viewed.Name.TextValue}（仅预览）" : "ARR 观看角色暂不可用，等待对象加载。");
        foreach (var descriptor in here)
        {
            var running = plugin!.Scripts!.Running.ContainsKey(descriptor.Metadata.Id);
            ImGui.PushID(descriptor.Metadata.Id);
            ImGui.Indent(Md.Em * 0.7f);
            Md.StatusDot(running ? Md.Success : Md.Muted, $"{descriptor.Metadata.Name} · {(running ? "运行中" : "未运行")}");
            ImGui.SameLine(0, Md.Em);
            if (Md.Link("脚本设置 ›"))
                OpenScript(descriptor.Metadata.Id);
            ImGui.Unindent(Md.Em * 0.7f);
            ImGui.PopID();
        }
        Md.EndCard();
    }

    private static void DrawRecordRow()
    {
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        if (recorder == null)
            return;
        var recordLabel = recorder.IsRecording ? "停止录制" : "开始录制";
        var status = recorder.IsRecording
            ? $"录制中 {(int)recorder.Elapsed.TotalMinutes:D2}:{recorder.Elapsed.Seconds:D2}（{recorder.EventCount} 事件）"
            : "插件录制用于在回放窗口检查绘制；修机制请用 ARR 录像。";
        var width = Md.ButtonWidth(recordLabel) + Md.ButtonWidth("打开回放窗口") + Md.Em * 0.5f;
        Md.ActionRow(recorder.IsRecording ? "● 录制中" : "插件录制", status, width, () =>
        {
            if (Md.TonalButton(recordLabel + "##record"))
            {
                if (recorder.IsRecording) recorder.StopRecording();
                else recorder.StartRecording();
            }
            ImGui.SameLine(0, Md.Em * 0.5f);
            if (Md.TextButton("打开回放窗口"))
                MasterPlayerPlugin.Instance?.ReplayWindow?.Toggle();
        });
    }

    private static void DrawOverviewStatus()
    {
        try
        {
            var status = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
            var scripts = MasterPlayerPlugin.Instance?.Scripts;
            var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow);
            Md.BeginCard("status");
            Md.SectionLabel("状态");
            Md.Stats("##overview_stats",
            [
                ($"{scripts?.Running.Count ?? 0} / {scripts?.Scripts.Count ?? 0}", "运行中脚本", null),
                (snapshot.DangerAreas.Count.ToString(), "活动危险区", null),
                (status.HasControl ? "持有" : "无", "移动控制权", status.HasControl ? Md.Success : null),
                (status.PhaseText, "插件安全区", null),
            ]);
            Md.EndCard();
        }
        catch (Exception ex)
        {
            Md.MutedText($"状态读取失败：{ex.Message}");
        }
    }

    // ── 显示与绘制 ───────────────────────────────────────────────────────────────────────

    private void DrawAppearanceTab()
    {
        var settings = MasterPlayerSettings.Instance;
        Md.PageHeader(Pages[1], PageSubtitles[1]);
        _displayPage = Md.Segmented("##display_sections", ["雷达悬浮窗", "游戏内绘制", "按机制显示"], _displayPage, Md.Em * 20);
        Md.Gap(0.3f);
        if (_displayPage == 0)
        {
            DrawRadarTab();
            if (DrawAoeAppearance("radar", settings.RadarAoe)) settings.Save();
            return;
        }
        if (_displayPage == 1)
        {
            Md.BeginCard("world_layers", Md.CardKind.Outlined);
            Md.SectionLabel("图层");
            var worldChanged = WorldLayerRows(settings);
            Md.EndCard();
            worldChanged |= DrawAoeAppearance("world", settings.WorldAoe);
            if (worldChanged) settings.Save();
            return;
        }

        var changed = false;
        Md.BeginCard("aoe_sources", Md.CardKind.Outlined);
        Md.SectionLabel("按技能／机制显示", "隐藏只是不绘制，危险区仍参与躲避解算；识别过的技能会自动加入列表。");
        Md.SearchField("##aoe_filter", "筛选名称…", ref _aoeFilter);
        var tableWidth = Md.RightEdge() - ImGui.GetCursorScreenPos().X;
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(Md.Em * 0.5f, Md.Em * 0.35f));
        if (ImGui.BeginTable("##aoe_sources", 3, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY, new Vector2(tableWidth, Md.Em * 22)))
        {
            ImGui.TableSetupColumn("技能／机制", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("游戏内", ImGuiTableColumnFlags.WidthFixed, Md.Em * 4);
            ImGui.TableSetupColumn("雷达", ImGuiTableColumnFlags.WidthFixed, Md.Em * 4);
            ImGui.TableHeadersRow();
            foreach (var (key, visibility) in settings.AoeVisibility.OrderBy(pair => pair.Value.Name))
            {
                if (!visibility.Name.Contains(_aoeFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
                ImGui.PushID(key);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextWrapped(visibility.Name);
                ImGui.TableNextColumn();
                changed |= PluginUi.Switch("##world", ref visibility.World);
                ImGui.TableNextColumn();
                changed |= PluginUi.Switch("##radar", ref visibility.Radar);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
        Md.EndCard();
        if (changed)
            settings.Save();
    }

    private static void DrawRadarTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;

        Md.BeginCard("radar_display", Md.CardKind.Outlined);
        Md.SectionLabel("显示");
        changed |= Row("启用雷达悬浮窗", null, settings.EnableRadarOverlay, v => settings.EnableRadarOverlay = v);
        changed |= Md.SwitchRow("显示机制范围", null, ref settings.RadarAoe.ShowAreas);
        changed |= Md.SwitchRow("显示走位路线", null, ref settings.RadarAoe.ShowPath);
        changed |= Row("仅在有机制或地形时显示", null, settings.RadarOnlyWithScript, v => settings.RadarOnlyWithScript = v);
        changed |= Row("显示周边墙体与悬崖", "灰色表示碰撞障碍，橙色表示落差或无支撑地面。", settings.RadarShowTerrain, v => settings.RadarShowTerrain = v);
        changed |= Row("画面随摄像机旋转", null, settings.RadarRotateWithCamera, v => settings.RadarRotateWithCamera = v);
        var centerMode = settings.RadarCenterMode;
        if (Md.ComboRow("中心模式", centerMode == 0 ? "完整显示脚本注册的场地" : "跟随自身 25 码视野", ref centerMode, "场地基准\0玩家摄像机基准\0"))
        {
            settings.RadarCenterMode = centerMode;
            changed = true;
        }
        Md.EndCard();

        Md.BeginCard("radar_look", Md.CardKind.Outlined);
        Md.SectionLabel("外观", "拖动或悬停「雷达大小」「背景不透明度」时，雷达悬浮窗会临时显示，方便边调边看。");
        var scale = settings.RadarScale;
        if (Md.SliderRow("雷达大小", null, ref scale, 0.3f, 3f, "%.2f 倍"))
        {
            settings.RadarScale = scale;
            changed = true;
        }
        PreviewRadarWhileEditing();
        var opacity = Math.Clamp(settings.RadarBackgroundOpacity * 100, 0, 100);
        if (Md.SliderRow("背景不透明度", null, ref opacity, 0, 100, "%.0f%%"))
        {
            settings.RadarBackgroundOpacity = opacity / 100;
            changed = true;
        }
        PreviewRadarWhileEditing();
        changed |= Row("显示罗盘", null, settings.RadarShowCompass, v => settings.RadarShowCompass = v);
        changed |= Row("显示距离环", null, settings.RadarShowRangeRings, v => settings.RadarShowRangeRings = v);
        changed |= Row("危险区临期脉冲", "危险区快到判定时闪烁提示。", settings.RadarPulseDanger, v => settings.RadarPulseDanger = v);
        Md.EndCard();

        Md.BeginCard("radar_interact", Md.CardKind.Outlined);
        Md.SectionLabel("交互");
        changed |= Row("锁定位置（鼠标穿透）", "解锁后可拖动雷达，滚轮调整大小，右键打开快捷菜单。", settings.RadarOverlayLock, v => settings.RadarOverlayLock = v);
        Md.EndCard();

        if (changed)
            settings.Save();
    }

    // Keeps the real overlay on screen while a radar appearance slider is hovered or dragged.
    private static void PreviewRadarWhileEditing()
    {
        if (Md.LastSliderEngaged)
            Radar.RadarOverlayWindow.PreviewUntilUtc = DateTime.UtcNow.AddSeconds(1.5);
    }

    private static bool DrawAoeAppearance(string id, AoeViewSettings settings)
    {
        Md.BeginCard(id + "_colors", Md.CardKind.Outlined);
        Md.SectionLabel("范围颜色", "点击色块改颜色，拖动滑条改不透明度。0% 为透明，100% 为不透明。");
        var changed = Md.SwitchRow("显示危险区", null, ref settings.ShowDanger);
        changed |= Md.SwitchRow("显示安全区", null, ref settings.ShowSafe);
        changed |= Md.SwitchRow("危险预告逐渐加深", "危险越接近判定，填充越明显；滑条控制最大不透明度。", ref settings.FadeDanger);
        changed |= Md.ColorRow("危险填充", ref settings.DangerFill);
        changed |= Md.ColorRow("危险边框", ref settings.DangerOutline);
        changed |= Md.ColorRow("安全填充", ref settings.SafeFill);
        changed |= Md.ColorRow("安全边框", ref settings.SafeOutline);
        Md.EndCard();

        Md.BeginCard(id + "_shapes", Md.CardKind.Outlined);
        Md.SectionLabel("按形状显示");
        changed |= Md.SwitchRow("圆形", null, ref settings.ShowCircles);
        changed |= Md.SwitchRow("月环", null, ref settings.ShowDonuts);
        changed |= Md.SwitchRow("扇形", null, ref settings.ShowFans);
        changed |= Md.SwitchRow("矩形／直线", null, ref settings.ShowRectangles);
        changed |= Md.SwitchRow("多边形", null, ref settings.ShowPolygons);
        changed |= Md.SwitchRow("其他形状", null, ref settings.ShowOtherShapes);
        Md.EndCard();
        return changed;
    }

    // ── 智能走位 ─────────────────────────────────────────────────────────────────────────

    private static void DrawAutomationTab()
    {
        var settings = MasterPlayerSettings.Instance;
        var changed = false;
        Md.PageHeader(Pages[2], PageSubtitles[2]);

        Md.BeginCard("smart_master", Md.CardKind.Outlined);
        Row("智能走位", "接近目标、脱险和机制站位由同一个规划器协调；关闭后只绘制不移动。DTR 或 /prpr ai on/off 也可切换。",
            settings.EnableSmartMovement, v => MasterPlayerPlugin.Instance?.SetSmartMovementEnabled(v));
        Md.EndCard();

        Md.BeginCard("approach", Md.CardKind.Outlined);
        Md.SectionLabel("接近目标", "对怪物计入双方目标圈，并预留停步余量。跟随队友使用下方距离。");
        var combatDistance = settings.CombatDistance == CombatDistanceMode.Ranged ? 1 : 0;
        var distanceHint = combatDistance == 1
            ? "射程内优先保持安全站位，超出射程才接近。"
            : "优先选择 3 码攻击范围内的安全位置，危险结束后回到近战范围。";
        if (Md.SegmentedRow("战斗距离", distanceHint, ["近战", "远程"], ref combatDistance))
        {
            settings.CombatDistance = (CombatDistanceMode)combatDistance;
            changed = true;
        }
        if (settings.CombatDistance == CombatDistanceMode.Ranged)
        {
            var range = settings.RangedAttackRange;
            if (Md.SliderRow("远程射程", "按常用技能的射程设置。", ref range, 5f, 25f, "%.1f 码"))
            {
                settings.RangedAttackRange = range;
                changed = true;
            }
        }
        Md.EndCard();

        Md.BeginCard("follow", Md.CardKind.Outlined);
        Md.SectionLabel("跟随", "按小队列表顺序（第 1 位是自己）。不指定时按下方选项决定是否跟随当前目标。");
        var slot = Math.Clamp(settings.FollowSlot, 0, 7);
        if (Md.ComboRow("跟随队友", null, ref slot, "不指定\0小队第 2 位\0小队第 3 位\0小队第 4 位\0小队第 5 位\0小队第 6 位\0小队第 7 位\0小队第 8 位\0"))
        {
            settings.FollowSlot = slot;
            changed = true;
        }
        changed |= Row("无有效槽位时跟随当前目标", "支持玩家和怪物；不跟随已死亡或不可选中的目标。", settings.FollowTargetWhenNoSlot, v => settings.FollowTargetWhenNoSlot = v);
        // Following does nothing with no slot and no target fallback; grey its details out.
        var following = settings.FollowSlot > 0 || settings.FollowTargetWhenNoSlot;
        var distance = settings.FollowDistance;
        if (Md.SliderRow("跟随距离", "仅用于玩家，按双方中心距离计算。", ref distance, 0.5f, 10f, "%.1f 码", following))
        {
            settings.FollowDistance = distance;
            changed = true;
        }
        var duringCombat = settings.FollowDuringCombat;
        if (Md.SwitchRow("战斗中生效", null, ref duringCombat, following))
        {
            settings.FollowDuringCombat = duringCombat;
            changed = true;
        }
        var outOfCombat = settings.FollowOutOfCombat;
        if (Md.SwitchRow("非战斗中也生效", null, ref outOfCombat, following))
        {
            settings.FollowOutOfCombat = outOfCombat;
            changed = true;
        }
        Md.EndCard();

        Md.BeginCard("dodge", Md.CardKind.Outlined);
        Md.SectionLabel("避险", "所有机制的移动控制均受智能走位总开关控制，脚本内的自动竞速等选项只在总开关开启时生效。");
        changed |= Row("优化撤离时机", "有安全路线时延后起步，保留输出和读条时间；按实际路程与判定时间撤离。", settings.OptimizeDodgeTiming, v => settings.OptimizeDodgeTiming = v);
        var margin = settings.DodgeDangerMargin;
        if (Md.SliderRow("危险区安全余量", "智能躲避会与危险区边缘至少保持该距离。", ref margin, 0f, 2f, "%.2f 码"))
        {
            settings.DodgeDangerMargin = margin;
            changed = true;
        }
        changed |= Row("检测周边地形", "没有机制场地时读取周边碰撞模型，只在确认可通行的范围内规划；移动前实时复核。", settings.EnableTerrainCollision, v => settings.EnableTerrainCollision = v);
        var terrainRadius = settings.TerrainScanRadius;
        if (Md.SliderRow("地形建模半径", "优先构建近身地形，再扩展至完整范围；范围越大，建模所需时间越长。", ref terrainRadius, 8f, 30f, "%.0f 码"))
        {
            settings.TerrainScanRadius = terrainRadius;
            changed = true;
        }
        Md.EndCard();

        if (changed)
            settings.Save();
    }

    // ── 录制回放 ─────────────────────────────────────────────────────────────────────────

    private static void DrawReplayTab()
    {
        Md.PageHeader(Pages[4], PageSubtitles[4]);
        var recorder = MasterPlayerPlugin.Instance?.Recorder;
        var replay = MasterPlayerPlugin.Instance?.Replay;
        if (recorder == null || replay == null)
        {
            Md.MutedText("录制回放尚未初始化。");
            return;
        }

        if (replay.SessionActive)
            Md.Banner("replay_active", FontAwesomeIcon.Play, Md.Success, $"回放中：{replay.ReplayScriptCount} 个脚本，{replay.Clock:F1} s");

        Md.BeginCard("recording", Md.CardKind.Outlined);
        Md.SectionLabel("插件录制");
        DrawRecordRow();
        if (!recorder.IsRecording && !string.IsNullOrEmpty(recorder.StatusMessage))
            Md.ValueRow("最近结果", recorder.StatusMessage);
        var autoRecord = MasterPlayerSettings.Instance.AutoRecordOnBattleStart;
        if (Md.SwitchRow("战斗开始时自动录制", "战斗结束自动停止。", ref autoRecord))
        {
            MasterPlayerSettings.Instance.AutoRecordOnBattleStart = autoRecord;
            MasterPlayerSettings.Instance.Save();
        }
        Md.EndCard();

        Md.BeginCard("replay_help");
        Md.SectionLabel("说明");
        Md.MutedText("回放窗口可查看场地预览、拖动时间轴或对比多份日志；/prpr rec、/prpr replay 也可操作。");
        Md.MutedText("插件录制（.mpreplay.jsonl）用于在回放窗口检查绘制；修机制请用 ARR 录像，见 docs/ARR解析与模拟流程.md。");
        Md.EndCard();
    }

    // ── 诊断 ─────────────────────────────────────────────────────────────────────────────

    private static void DrawDiagnosticsTab()
    {
        var settings = MasterPlayerSettings.Instance;
        Md.PageHeader(Pages[5], PageSubtitles[5]);

        Md.BeginCard("debug", Md.CardKind.Outlined);
        var debugMode = settings.DebugMode;
        if (Md.SwitchRow("调试模式", "在下方显示解算计数等诊断信息。", ref debugMode))
        {
            settings.DebugMode = debugMode;
            settings.Save();
        }
        Md.EndCard();

        DrawSafeZoneStatus(settings);
        DrawTerrainDiagnostics();
        DrawMechanicModules(settings);
        DrawMechanicHandlers();
    }

    private static void DrawSafeZoneStatus(MasterPlayerSettings settings)
    {
        Md.BeginCard("safezone", Md.CardKind.Outlined);
        Md.SectionLabel("插件安全区");
        try
        {
            var setting = MasterPlayerPlugin.Instance!.SafeZone.Setting;
            var status = MasterPlayerPlugin.Instance!.SafeZone.MovementStatus;
            var snapshot = MasterPlayerPlugin.Instance!.SafeZone.Manager.BuildSnapshot(DateTime.UtcNow);
            Md.ValueRow("解算", setting.EnableSafeZone ? "开启" : "关闭", setting.EnableSafeZone ? Md.Success : Md.Warning);
            Md.ValueRow("智能走位", settings.EnableSmartMovement ? "开启" : "关闭", settings.EnableSmartMovement ? Md.Success : null);
            Md.ValueRow("移动控制权", status.HasControl ? "持有" : "无", status.HasControl ? Md.Success : null);
            Md.ValueRow("阶段", status.PhaseText);
            Md.ValueRow("最新解算方案", status.LatestSolution != null ? "有" : "无");
            Md.ValueRow("当前危险区", snapshot.DangerAreas.Count.ToString());
            Md.ValueRow("周边地形", MasterPlayerPlugin.Instance!.SafeZone.TerrainStatus);
            Md.ValueRow("运行中脚本", (MasterPlayerPlugin.Instance?.Scripts?.Running.Count ?? 0).ToString());
            if (settings.DebugMode)
            {
                Md.ValueRow("地形更新本帧耗时", $"{MasterPlayerPlugin.Instance!.SafeZone.TerrainScanMilliseconds:F2} ms");
                Md.ValueRow("派发次数", $"{status.DispatchCount}（最近 {status.LastDispatchUtc:HH:mm:ss.fff} UTC）");
                Md.ValueRow("异步解算", $"运行中={status.IsAsyncSolveRunning} 发起={status.AsyncSolveStartedCount} 采纳={status.AsyncSolveAcceptedCount} 取消={status.AsyncSolveCancelledCount} 丢弃={status.AsyncSolveDiscardedCount}");
                Md.ValueRow($"区域快照 v{snapshot.Version}", $"危险区 {snapshot.DangerAreas.Count} / 目标区 {snapshot.GoalAreas.Count} / 障碍 {snapshot.HardObstacles.Count} / 边界 {(snapshot.ArenaBoundary != null ? "有" : "无")}");
            }
        }
        catch (Exception ex)
        {
            Md.MutedText($"状态读取失败：{ex.Message}");
        }
        Md.EndCard();
    }

    private static void DrawMechanicModules(MasterPlayerSettings settings)
    {
        var bridge = MasterPlayerPlugin.Instance?.MechanicHost;
        Md.BeginCard("mechanic_modules", Md.CardKind.Outlined);
        Md.SectionLabel("机制模块", "实时世界输入机制运行库，驱动内置机制模块。");
        if (bridge == null)
        {
            Md.MutedText("机制模块宿主未初始化。");
            Md.EndCard();
            return;
        }
        if (!bridge.IsAvailable)
        {
            Md.ValueRow("状态", $"机制运行库不可用：{bridge.LastError ?? "初始化失败"}", Md.Danger);
            Md.EndCard();
            return;
        }

        var maturity = Math.Clamp(settings.MechanicModuleMinMaturity, 1, 4) - 1;
        if (Md.ComboRow("模块成熟度门槛", null, ref maturity, "开发中 (WIP)\0第三方贡献 (Contributed)\0已验证 (Verified)\0支持 AI (AISupport)\0", Md.Em * 13))
        {
            settings.MechanicModuleMinMaturity = maturity + 1;
            settings.Save();
        }
        Md.ValueRow("状态", !bridge.IsEnabled ? "已停用" : bridge.IsSuppressed ? "当前区域由专属脚本接管" : "运行中", bridge.IsEnabled && !bridge.IsSuppressed ? Md.Success : null);
        Md.ValueRow("效果包捕获", bridge.CaptureActive ? "正常" : "不可用（效果数组组件降级）", bridge.CaptureActive ? null : Md.Warning);
        Md.ValueRow("模块", $"已加载 {bridge.LoadedModuleCount} / 待激活 {bridge.PendingModuleCount}");
        Md.ValueRow("激活模块", bridge.ActiveModuleName ?? "无");
        if (bridge.ActiveZoneModuleName is { } zoneModule)
            Md.ValueRow("区域模块", zoneModule);
        if (bridge.DrawModuleName is { } drawModule)
            Md.ValueRow("绘制来源", drawModule);
        if (bridge.NavigationActive)
            Md.ValueRow("导航区", $"危险 {bridge.NavDangerZoneCount} / 目标 {bridge.NavGoalZoneCount} / 障碍 {bridge.NavObstacleCount} / 边界 {(bridge.NavBoundaryActive ? "有" : "无")}");
        if (bridge.SpecialModeHint is { } specialMode)
            Md.ValueRow("特殊模式", specialMode, Md.Warning);
        if (bridge.PredictedDamageText is { } predicted)
            Md.ValueRow("预计伤害", predicted);
        if (bridge.CurrentTimeline is { } timeline)
        {
            var rows = MechanicHost.MechanicTimelineView.BuildRows(timeline, bridge.MechanicClockUtc);
            var current = rows.FirstOrDefault(r => r.IsCurrent);
            Md.ValueRow("阶段", timeline.PhaseName.Length != 0 ? timeline.PhaseName : "—");
            Md.ValueRow("当前", current != null ? $"{current.Name}（剩 {current.Seconds:F1}s）" : "—");
            var next = rows.Where(r => !r.IsCurrent).Take(3).Select(r => r.IsBranch ? "分支?" : $"{r.Name} {r.Seconds:F1}s").ToArray();
            if (next.Length != 0)
                Md.ValueRow("后续", string.Join(" → ", next));
        }
        if (bridge.CaptureErrorCount != 0)
            Md.ValueRow("绘制捕获错误", $"累计 {bridge.CaptureErrorCount} 起（{bridge.DrawCaptureError ?? "详见日志"}）", Md.Warning);
        if (bridge.LastError is { } error)
            Md.ValueRow("最近错误", error, Md.Warning);
        var hints = bridge.CurrentHints;
        foreach (var (text, risk) in hints)
            Md.ValueRow("机制提示", text, risk ? Md.Danger : Md.Success);
        Md.EndCard();
    }

    private static void DrawMechanicHandlers()
    {
        var dispatcher = MasterPlayerPlugin.Instance?.Mechanics;
        Md.BeginCard("handlers", Md.CardKind.Outlined);
        Md.SectionLabel("机制处理器");
        if (dispatcher == null)
            Md.MutedText("MechanicDispatcher 未初始化。");
        else if (dispatcher.Handlers.Count == 0)
            Md.MutedText("无已注册处理器。");
        else
            foreach (var handler in dispatcher.Handlers)
            {
                ImGui.PushID(handler.Id);
                var enabled = handler.Enabled;
                if (Md.SwitchRow(handler.DisplayName, null, ref enabled))
                    handler.Enabled = enabled;
                ImGui.PopID();
            }
        Md.EndCard();
    }

    // ── Shared helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Switch row for a settings property (read value, write through <paramref name="apply"/>).</summary>
    private static bool Row(string label, string? description, bool value, Action<bool> apply)
    {
        var newValue = value;
        if (!Md.SwitchRow(label, description, ref newValue))
            return false;
        apply(newValue);
        return true;
    }

    // World drawing is two independent layers; EnableRadar stays as "any layer on" for the
    // safe-zone/dodge gates, so turning one layer off never hides the other.
    private static bool WorldLayerRows(MasterPlayerSettings settings)
        => WorldAreaRow(settings) | WorldPathRow(settings);

    private static bool WorldAreaRow(MasterPlayerSettings settings)
    {
        var aoe = settings.WorldAoe;
        return Row("机制范围绘制", "在游戏场景中画出 AOE 危险区、安全区和场地边界。", settings.EnableRadar && aoe.ShowAreas, v => ApplyWorldLayer(settings, x => aoe.ShowAreas = x, v));
    }

    private static bool WorldPathRow(MasterPlayerSettings settings)
    {
        var aoe = settings.WorldAoe;
        return Row("走位路线绘制", "画出智能走位规划的移动路线（青色点线）。", settings.EnableRadar && aoe.ShowPath, v => ApplyWorldLayer(settings, x => aoe.ShowPath = x, v));
    }

    private static bool ApplyWorldLayer(MasterPlayerSettings settings, Action<bool> set, bool value)
    {
        var aoe = settings.WorldAoe;
        if (!settings.EnableRadar)
            aoe.ShowAreas = aoe.ShowPath = false;
        set(value);
        settings.EnableRadar = aoe.ShowAreas || aoe.ShowPath;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            _visible = false;
            Md.DisposeFonts();
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
