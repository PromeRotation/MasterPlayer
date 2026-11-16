using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;

namespace MasterPlayer.Windows;

/// <summary>
/// 机制库页面：脚本列表（每项右侧直接开关）+ 详情；斗兽奇弈单独成页：六个框（五盘 + 通用），
/// 点框进入该盘的机制开关与脚本设置。
/// </summary>
public sealed partial class MainWindow
{
    private enum LibraryView { List, CrucibleGrid, CrucibleBoard }

    private LibraryView _libraryView;
    private int _mechanicsPage, _scriptScope, _crucibleBoardIndex;
    private string _scriptFilter = "";
    private string _territoryFilter = "";
    private string? _selectedScript;
    private readonly Dictionary<int, float> _crucibleRowHeights = [];

    private const string CruciblePrefix = "masterplayer.crucible.";

    private sealed record BoardInfo(string Name, uint Territory, string Range, int Fights, int LevelCap, string Description);

    // Facts only: fight count from the boss range, the final boss, the beast level cap.
    private static readonly BoardInfo[] CrucibleBoards =
    [
        new("低1", 1339, "B01–B06", 6, 5, "入门盘，6 场战斗。尾王帕德索；有蛛蝎小怪的格子先清小怪。"),
        new("低2", 1340, "B07–B13", 7, 10, "7 场战斗。尾王鲁斯弗洛克斯，和芋头一起出场。"),
        new("低3", 1341, "B14–B21", 8, 15, "8 场战斗。尾王加特勒。"),
        new("高1", 1342, "B22–B31", 10, 20, "10 场战斗。尾王博格尼。"),
        new("高2", 1343, "B32–B45", 14, 25, "14 场战斗，最难的一盘。尾王劳妲；粘液痰小怪要拉离毒池。"),
    ];

    /// <summary>Jumps to a script's settings from elsewhere (e.g. the overview).</summary>
    private void OpenScript(string id)
    {
        _page = 3;
        _mechanicsPage = 0;
        if (id.StartsWith(CruciblePrefix, StringComparison.Ordinal))
        {
            var descriptor = MasterPlayerPlugin.Instance?.Scripts?.Scripts.FirstOrDefault(d => d.Metadata.Id == id);
            _crucibleBoardIndex = descriptor == null ? 0 : Math.Max(0, Array.FindIndex(CrucibleBoards, b => descriptor.Metadata.Territorys.Contains(b.Territory)));
            _libraryView = LibraryView.CrucibleBoard;
            return;
        }
        _libraryView = LibraryView.List;
        _selectedScript = id;
        _scriptFilter = "";
        _scriptScope = 0;
    }

    private static ScriptDescriptor? BoardScript(ScriptManager manager, BoardInfo board)
        => manager.Scripts.FirstOrDefault(d => d.Metadata.Id.StartsWith(CruciblePrefix, StringComparison.Ordinal) && d.Metadata.Territorys.Contains(board.Territory));

    private void DrawMechanicsTab()
    {
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager != null && _libraryView == LibraryView.CrucibleGrid)
        {
            DrawCrucibleGrid(manager);
            return;
        }
        if (manager != null && _libraryView == LibraryView.CrucibleBoard)
        {
            DrawCrucibleBoard(manager);
            return;
        }
        _libraryView = LibraryView.List;

        var settings = MasterPlayerSettings.Instance;
        Md.PageHeader(Pages[3], PageSubtitles[3]);
        Md.BeginCard("library_master", Md.CardKind.Outlined);
        var enabled = settings.EnableMechanics;
        if (Md.SwitchRow("启用机制库", "统一控制内置机制和自定义脚本；关闭某副本会停用该区域的全部机制。", ref enabled))
        {
            settings.EnableMechanics = enabled;
            settings.Save();
        }
        Md.EndCard();
        _mechanicsPage = Md.Segmented("##mechanics_sections", ["机制脚本", "按区域启停"], _mechanicsPage, Md.Em * 14);
        Md.Gap(0.3f);
        if (_mechanicsPage == 0) DrawScriptsTab();
        else DrawTerritories();
    }

    // ── Script list + details ────────────────────────────────────────────────────────────

    private enum ScriptState { Running, Enabled, Disabled, AreaOff, LibraryOff }

    private static ScriptState StateOf(ScriptManager manager, MasterPlayerSettings settings, ScriptDescriptor descriptor)
    {
        var id = descriptor.Metadata.Id;
        return manager.Running.ContainsKey(id) ? ScriptState.Running : !settings.EnableMechanics ? ScriptState.LibraryOff
            : descriptor.Metadata.Territorys.Length > 0 && descriptor.Metadata.Territorys.All(t => !settings.IsTerritoryEnabled(t)) ? ScriptState.AreaOff
            : manager.IsScriptEnabled(id) ? ScriptState.Enabled : ScriptState.Disabled;
    }

    private static string StateText(ScriptState state) => state switch
    {
        ScriptState.Running => "运行中",
        ScriptState.Enabled => "已启用 · 进入区域后运行",
        ScriptState.AreaOff => "区域已关闭",
        ScriptState.LibraryOff => "机制库已关闭",
        _ => "已停用",
    };

    private static Md.Lead StateLead(ScriptState state) => state switch
    {
        ScriptState.Running => new(FontAwesomeIcon.Play, Md.Success),
        ScriptState.Enabled => new(FontAwesomeIcon.Check, Md.Accent),
        ScriptState.AreaOff => new(FontAwesomeIcon.MapMarkerAlt, Md.Warning),
        ScriptState.LibraryOff => new(FontAwesomeIcon.PowerOff, Md.Muted),
        _ => new(FontAwesomeIcon.Pause, Md.Muted),
    };

    private static Vector4 StateColor(ScriptState state) => state switch
    {
        ScriptState.Running => Md.Success,
        ScriptState.Enabled => Md.Accent,
        ScriptState.AreaOff => Md.Warning,
        _ => Md.Muted,
    };

    private static string SourceLabel(ScriptSource source) => source switch
    {
        ScriptSource.BuiltIn => "内置",
        ScriptSource.Visual => "可视化",
        _ => "自定义",
    };

    private void DrawScriptsTab()
    {
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        if (manager == null) { Md.MutedText("脚本管理器尚未就绪。"); return; }
        var settings = MasterPlayerSettings.Instance;

        if (Md.TextButton("刷新脚本")) manager.ReloadScripts();
        ImGui.SameLine(0, Md.Em * 0.4f);
        if (Md.TextButton("打开脚本目录")) OpenScriptsDirectory(manager.ScriptsDirectory);
        ImGui.SameLine(0, Md.Em * 0.4f);
        if (Md.TonalButton("新建可视化脚本")) _visualEditor.Open();
        ImGui.SameLine(0, Md.Em * 0.6f);
        ImGui.AlignTextToFramePadding();
        Md.Info("内置预设随插件加载，自定义脚本放在配置目录；专属脚本优先于内置机制模块。");
        if (!string.IsNullOrEmpty(manager.StatusMessage)) Md.MutedText(manager.StatusMessage);
        if (!string.IsNullOrEmpty(manager.ErrorMessage))
            Md.Banner("script_errors", FontAwesomeIcon.ExclamationCircle, Md.Danger, manager.ErrorMessage);
        Md.Gap(0.2f);

        var territory = Svc.ClientState.TerritoryType;
        var filter = _scriptFilter.Trim();
        var entries = manager.Scripts.Where(d =>
            (_scriptScope != 1 || d.Metadata.Territorys.Length == 0 || d.Metadata.Territorys.Contains(territory)) &&
            (_scriptScope != 2 || manager.Running.ContainsKey(d.Metadata.Id)) &&
            (d.Metadata.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
             d.Metadata.Region.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
             d.Metadata.Territorys.Any(t => t.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(d => manager.Running.ContainsKey(d.Metadata.Id)).ThenBy(d => d.Metadata.Name).ToArray();
        var singles = entries.Where(d => !d.Metadata.Id.StartsWith(CruciblePrefix, StringComparison.Ordinal)).ToArray();
        if (singles.Length != 0 && !singles.Any(d => d.Metadata.Id == _selectedScript))
            _selectedScript = singles[0].Metadata.Id;

        var listWidth = Math.Clamp(ImGui.GetContentRegionAvail().X * 0.4f, Md.Em * 15, Md.Em * 19);
        if (ImGui.BeginChild("##script_list", new Vector2(listWidth, -1), false))
        {
            // Activation lives on this page, so its entry cannot depend on downloaded scripts or list filters.
            DrawCrucibleLibraryEntry(manager, settings);
            Md.Gap(0.2f);
            Md.SearchField("##script_filter", "搜索脚本名称或区域 ID", ref _scriptFilter, ImGui.GetContentRegionAvail().X - Md.Em * 0.3f);
            _scriptScope = Md.ChipGroup("scope", ["全部", "当前区域", "运行中"], _scriptScope);
            Md.Gap(0.2f);
            if (entries.Length == 0)
                Md.MutedText(manager.Scripts.Count == 0 ? "暂无脚本。可在编辑器中新建，或将脚本放入目录后刷新。" : "没有匹配的脚本，可修改搜索或筛选条件。");

            foreach (var descriptor in singles)
            {
                var id = descriptor.Metadata.Id;
                var enabled = manager.IsScriptEnabled(id);
                var state = StateOf(manager, settings, descriptor);
                if (Md.ListItem(id, StateLead(state), descriptor.Metadata.Name, $"{SourceLabel(descriptor.Source)} · {StateText(state)}",
                        _selectedScript == id, ref enabled, out var toggled))
                    _selectedScript = id;
                if (toggled) manager.SetScriptEnabled(id, enabled);
            }
        }
        ImGui.EndChild();
        ImGui.SameLine(0, Md.Em * 1.2f);
        if (ImGui.BeginChild("##script_detail", new Vector2(-1, -1), false))
        {
            var selected = singles.FirstOrDefault(d => d.Metadata.Id == _selectedScript);
            if (selected != null) DrawScriptDetails(manager, selected, null);
            else Md.MutedText("点左侧「斗兽奇弈」进入斗兽页面，或选择脚本查看详情。");
        }
        ImGui.EndChild();
    }

    private void DrawCrucibleLibraryEntry(ScriptManager manager, MasterPlayerSettings settings)
    {
        var boards = manager.Scripts.Where(d => d.Metadata.Id.StartsWith(CruciblePrefix, StringComparison.Ordinal)).ToArray();
        var runningBoards = boards.Count(d => manager.Running.ContainsKey(d.Metadata.Id));
        var enabledBoards = boards.Count(d => manager.IsScriptEnabled(d.Metadata.Id));
        var state = runningBoards != 0 ? ScriptState.Running : !settings.EnableMechanics ? ScriptState.LibraryOff
            : enabledBoards != 0 ? ScriptState.Enabled : ScriptState.Disabled;
        var subtitle = boards.Length == 0 ? "未找到斗兽脚本"
            : runningBoards != 0 ? $"{boards.Length} 盘 · {runningBoards} 盘运行中" : $"{boards.Length} 盘 · 已启用 {enabledBoards} 盘";
        var allOn = boards.Length != 0 && enabledBoards == boards.Length;
        if (Md.ListItem("crucible", StateLead(state), "斗兽奇弈", subtitle, false, ref allOn, out var toggled,
                hasSwitch: boards.Length != 0, crown: true,
                tooltip: "点击进入斗兽页面，开关五盘并调整每一盘的机制"))
            _libraryView = LibraryView.CrucibleGrid;
        if (toggled)
            foreach (var board in boards) manager.SetScriptEnabled(board.Metadata.Id, allOn);
    }

    private void DrawTerritories()
    {
        var plugin = MasterPlayerPlugin.Instance;
        if (plugin == null) return;
        var settings = MasterPlayerSettings.Instance;
        var current = Svc.ClientState.TerritoryType;

        Md.BeginCard("territory_current", Md.CardKind.Outlined);
        var enabled = settings.IsTerritoryEnabled(current);
        if (Md.SwitchRow($"当前区域：{MechanicHost.MechanicTerritoryCatalog.ResolveName(current)}（{current}）",
                "关闭区域后，该区域的机制绘制、提示和走位同时停用。未列出的区域可在进入后用这个开关管理。", ref enabled))
        {
            settings.TerritoryEnabled[current] = enabled;
            settings.Save();
        }
        Md.EndCard();

        Md.SearchField("##territory_filter", "搜索副本名称或区域 ID", ref _territoryFilter);
        Md.Gap(0.2f);
        var entries = (plugin.MechanicHost?.Territories ?? []).ToDictionary(entry => entry.TerritoryId);
        foreach (var script in plugin.Scripts?.Scripts ?? [])
            foreach (var territory in script.Metadata.Territorys)
                if (!entries.ContainsKey(territory))
                    entries[territory] = new(territory, MechanicHost.MechanicTerritoryCatalog.ResolveName(territory), 0);
        foreach (var territory in settings.TerritoryEnabled.Keys.Append(current))
            if (!entries.ContainsKey(territory))
                entries[territory] = new(territory, MechanicHost.MechanicTerritoryCatalog.ResolveName(territory), 0);

        if (!ImGui.BeginChild("##territory_list", new Vector2(-1, -1), false)) { ImGui.EndChild(); return; }
        var filter = _territoryFilter.Trim();
        foreach (var entry in entries.Values.OrderByDescending(entry => entry.TerritoryId == current).ThenBy(entry => entry.Name))
        {
            if (!entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) && !entry.TerritoryId.ToString().Contains(filter)) continue;
            var active = settings.IsTerritoryEnabled(entry.TerritoryId);
            var lead = new Md.Lead(entry.TerritoryId == current ? FontAwesomeIcon.MapMarkerAlt : FontAwesomeIcon.Map, entry.TerritoryId == current ? Md.Accent : Md.Muted);
            Md.ListItem(entry.TerritoryId.ToString(), lead, entry.Name, $"区域 {entry.TerritoryId}{(entry.TerritoryId == current ? " · 当前区域" : "")}", false, ref active, out var toggled);
            if (toggled)
            {
                settings.TerritoryEnabled[entry.TerritoryId] = active;
                settings.Save();
            }
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

    // Older Crucible script copies describe their porting origin ("本地 BMR … 机制迁移，B01–B06");
    // show what the script does instead, keeping the boss range.
    private static string DisplayNote(ScriptMetadata metadata)
    {
        var note = metadata.Note?.Trim() ?? "";
        if (!note.Contains("BMR", StringComparison.Ordinal) && !note.Contains("迁移", StringComparison.Ordinal))
            return note;
        var range = System.Text.RegularExpressions.Regex.Match(note, @"B\d+\s*[–-]\s*B\d+");
        const string summary = "读条范围预测、自动躲避与机制提示";
        return range.Success ? $"{range.Value}：{summary}" : summary;
    }

    /// <summary>Script header card, mechanism switches and script settings, one item per row.</summary>
    private void DrawScriptDetails(ScriptManager manager, ScriptDescriptor descriptor, string? titleOverride)
    {
        var metadata = descriptor.Metadata;
        var id = metadata.Id;
        var settings = MasterPlayerSettings.Instance;
        var state = StateOf(manager, settings, descriptor);
        ImGui.PushID(id);

        Md.BeginCard("header");
        Md.Title(titleOverride ?? metadata.Name);
        ImGui.SameLine(0, Md.Em * 0.6f);
        Md.Pill(StateText(state).Split(' ')[0], StateColor(state));
        Md.MutedText($"v{metadata.Version} · {metadata.Author} · {(descriptor.Source == ScriptSource.BuiltIn ? "插件内置" : "配置目录")}");
        if (DisplayNote(metadata) is { Length: > 0 } note)
            ImGui.TextWrapped(note);
        Md.Gap(0.2f);
        var enabled = manager.IsScriptEnabled(id);
        if (Md.SwitchRow("启用此脚本", "进入适用区域后自动运行。", ref enabled)) manager.SetScriptEnabled(id, enabled);
        if (descriptor.Source == ScriptSource.Visual)
            Md.ActionRow("可视化脚本", null, Md.ButtonWidth("编辑脚本"), () => { if (Md.TonalButton("编辑脚本")) _visualEditor.Open(descriptor.SourcePath); });
        if (id == "masterplayer.goldsaucer.blunderville")
            Md.ActionRow("糖豆人路网", "一二三关的路网编辑器。", Md.ButtonWidth("编辑路网"), () => { if (Md.TonalButton("编辑路网")) _routeEditor.Open(); });
        Md.ValueRow("适用区域", metadata.Territorys.Length == 0 ? "通用"
            : string.Join("、", metadata.Territorys.Select(t => $"{MechanicHost.MechanicTerritoryCatalog.ResolveName(t)}（{t}）")));
        Md.EndCard();

        var running = manager.Running.TryGetValue(id, out var run);
        var mechanisms = running ? run!.Script.GetMechanisms().ToArray() : Mechanisms(descriptor);
        if (mechanisms.Length != 0)
        {
            Md.BeginCard("mechanisms", Md.CardKind.Outlined);
            Md.SectionLabel("机制");
            foreach (var mechanism in mechanisms)
            {
                var on = manager.IsMechanismEnabled(id, mechanism);
                if (Md.SwitchRow(mechanism, null, ref on))
                    manager.SetMechanismEnabled(id, mechanism, on);
            }
            Md.EndCard();
        }

        Md.BeginCard("script_settings", Md.CardKind.Outlined);
        Md.SectionLabel("脚本设置");
        if (running && run!.Settings.RegisteredSettings.Count != 0)
            foreach (var (propName, displayName) in run.Settings.RegisteredSettings)
                DrawScriptSetting(run.Settings, propName, displayName);
        else
            Md.MutedText(running ? "这个脚本没有可调的设置。" : "脚本设置在脚本运行后显示；上面的机制开关现在就能调，进入区域后生效。");
        Md.EndCard();

        if (ImGui.TreeNode("文件信息"))
        {
            Md.MutedText(metadata.SourcePath);
            ImGui.TreePop();
        }
        ImGui.PopID();
    }

    private static void DrawScriptSetting(ScriptSettingStore store, string propName, string displayName)
    {
        var type = store.GetPropertyType(propName);
        var value = store.GetPropertyValue(propName);
        ImGui.PushID(propName);
        var width = Md.Em * 10;
        if (type == typeof(bool))
        {
            var b = value is bool bv && bv;
            if (Md.SwitchRow(displayName, null, ref b))
                store.SetPropertyValue(propName, b);
        }
        else if (type == typeof(int))
        {
            var i = value is int iv ? iv : 0;
            Md.ActionRow(displayName, null, width, () =>
            {
                ImGui.SetNextItemWidth(width);
                if (ImGui.InputInt("##int", ref i)) store.SetPropertyValue(propName, i);
            });
        }
        else if (type == typeof(float))
        {
            var f = value is float fv ? fv : 0f;
            Md.ActionRow(displayName, null, width, () =>
            {
                ImGui.SetNextItemWidth(width);
                if (ImGui.DragFloat("##float", ref f, 0.1f)) store.SetPropertyValue(propName, f);
            });
        }
        else if (type == typeof(string))
        {
            var s = value as string ?? "";
            Md.ActionRow(displayName, null, width * 1.4f, () =>
            {
                ImGui.SetNextItemWidth(width * 1.4f);
                if (ImGui.InputText("##text", ref s, 256)) store.SetPropertyValue(propName, s);
            });
        }
        else if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var current = value != null ? Math.Max(0, Array.IndexOf(names, value.ToString())) : 0;
            if (Md.ComboRow(displayName, null, ref current, string.Join('\0', names) + '\0'))
                store.SetPropertyValue(propName, Enum.Parse(type, names[current]));
        }
        else if (type == typeof(ScriptColor))
        {
            var color = value as ScriptColor ?? new ScriptColor();
            var v4 = color.V4;
            Md.ActionRow(displayName, null, ImGui.GetFrameHeight(), () =>
            {
                if (ImGui.ColorEdit4("##color", ref v4, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel))
                {
                    color.V4 = v4;
                    store.SetPropertyValue(propName, color);
                }
            });
        }
        else
        {
            Md.ValueRow(displayName, $"不支持的类型 {type.Name}", Md.Muted);
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

    // ── 斗兽奇弈 ─────────────────────────────────────────────────────────────────────────

    private void DrawCrucibleTitle(string title, string subtitle)
    {
        Md.BigText(title, Md.FontRole.Title);
        var titleMin = ImGui.GetItemRectMin();
        var titleMax = ImGui.GetItemRectMax();
        ImGui.SameLine(0, Md.Em * 0.45f);
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, titleMin.Y + (titleMax.Y - titleMin.Y - ImGui.GetTextLineHeight()) / 2));
        Md.Icon(FontAwesomeIcon.Crown, Md.Gold);
        Md.MutedText(subtitle);
        Md.Gap(0.5f);
    }

    /// <summary>Six boxes: one per board (click to open it) and a general box with bulk switches and licence state.</summary>
    private void DrawCrucibleGrid(ScriptManager manager)
    {
        var settings = MasterPlayerSettings.Instance;
        if (Md.Link("‹ 机制库"))
        {
            _libraryView = LibraryView.List;
            return;
        }
        Md.Gap(0.3f);
        DrawCrucibleTitle("斗兽奇弈", "点任一盘的框，进入这一盘的机制开关和设置。");

        var boardScripts = CrucibleBoards.Select(b => BoardScript(manager, b)).ToArray();
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(Md.Em * 0.45f, Md.Em * 0.45f));
        if (ImGui.BeginTable("##crucible_grid", 3, ImGuiTableFlags.SizingStretchSame))
        {
            for (var i = 0; i < 6; i++)
            {
                ImGui.TableNextColumn();
                var row = i / 3;
                var rowHeight = _crucibleRowHeights.GetValueOrDefault(row);
                var natural = i < 5
                    ? DrawBoardBox(manager, settings, i, boardScripts[i], rowHeight)
                    : DrawGeneralBox(manager, boardScripts, rowHeight);
                // Equalise card heights per grid row using last frame's tallest content.
                if (i % 3 == 0) _crucibleNextRowHeight = natural;
                else _crucibleNextRowHeight = MathF.Max(_crucibleNextRowHeight, natural);
                if (i % 3 == 2) _crucibleRowHeights[row] = _crucibleNextRowHeight;
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private float _crucibleNextRowHeight;

    private float DrawBoardBox(ScriptManager manager, MasterPlayerSettings settings, int index, ScriptDescriptor? script, float minHeight)
    {
        var board = CrucibleBoards[index];
        var state = script == null ? ScriptState.Disabled : StateOf(manager, settings, script);
        var kind = script == null ? Md.CardKind.Outlined : state == ScriptState.Running ? Md.CardKind.Running : Md.CardKind.Outlined;
        Md.BeginCard($"board_{index}", kind);
        var top = ImGui.GetCursorScreenPos().Y;
        var rect = Md.CurrentCardRect();
        var hovered = Md.CardHovered(rect);
        if (hovered && script != null)
        {
            ImGui.GetWindowDrawList().AddRectFilled(rect.Min, rect.Max, Md.U32(Md.Tone(Md.Text, 0.04f)), Md.Radius);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        // Title + status.
        Md.BigText(board.Name);
        var titleMin = ImGui.GetItemRectMin();
        var titleMax = ImGui.GetItemRectMax();
        var statusText = script == null ? "脚本缺失" : StateText(state).Split(' ')[0];
        var statusWidth = Md.StatusDotWidth(statusText);
        // Status sits right-aligned on the title line; on a narrow card it drops to its own line.
        if (titleMax.X + Md.Em * 0.8f + statusWidth <= Md.RightEdge())
        {
            ImGui.SameLine();
            ImGui.SetCursorScreenPos(new Vector2(Md.RightEdge() - statusWidth, titleMin.Y + (titleMax.Y - titleMin.Y - ImGui.GetTextLineHeight()) / 2));
        }
        Md.StatusDot(script == null ? Md.Muted : StateColor(state), statusText);

        ImGui.PushStyleColor(ImGuiCol.Text, Md.Tone(Md.Text, 0.8f));
        ImGui.TextWrapped(board.Description);
        ImGui.PopStyleColor();
        Md.Tag(board.Range, Md.Muted, true);
        ImGui.SameLine(0, Md.Em * 0.35f);
        Md.Tag($"魔兽 {board.LevelCap} 级", Md.Muted, true);

        // Normal / Beastway availability. Both run on this board's script today.
        var on = script != null && manager.IsScriptEnabled(script.Metadata.Id);
        Md.Tag("普通", on ? Md.Success : Md.Muted, on);
        Md.Tooltip("普通和兽道目前共用这一盘的脚本，随右下角开关一起启停。");
        ImGui.SameLine(0, Md.Em * 0.35f);
        Md.Tag("兽道", on ? Md.Success : Md.Muted, on);
        Md.Tooltip("普通和兽道目前共用这一盘的脚本，随右下角开关一起启停。");

        // Footer: open link on the left, board switch on the right.
        var footerY = ImGui.GetCursorScreenPos().Y;
        var footerTarget = minHeight > 0 ? top + minHeight - ImGui.GetFrameHeight() : footerY;
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, MathF.Max(footerY, footerTarget)));
        var open = false;
        if (script != null)
        {
            ImGui.AlignTextToFramePadding();
            open |= Md.Link("设置 ›");
            ImGui.SameLine();
            var track = PluginUi.SwitchSize;
            ImGui.SetCursorScreenPos(new Vector2(Md.RightEdge() - track.X - 2, ImGui.GetCursorScreenPos().Y));
            if (PluginUi.Switch("##board_on", ref on))
                manager.SetScriptEnabled(script.Metadata.Id, on);
        }
        else
        {
            Md.MutedText("没有找到这一盘的脚本。");
        }
        var natural = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y - MathF.Max(0, footerTarget - footerY) - top;
        open |= script != null && Md.CardClicked(rect);
        Md.EndCard(0f, 0.2f);
        if (open)
        {
            _crucibleBoardIndex = index;
            _libraryView = LibraryView.CrucibleBoard;
        }
        return natural;
    }

    private float DrawGeneralBox(ScriptManager manager, ScriptDescriptor?[] boardScripts, float minHeight)
    {
        Md.BeginCard("board_general", Md.CardKind.Gold);
        var top = ImGui.GetCursorScreenPos().Y;
        Md.BigText("通用");
        var runningBoard = Array.FindIndex(boardScripts, s => s != null && manager.Running.ContainsKey(s.Metadata.Id));
        ImGui.PushStyleColor(ImGuiCol.Text, Md.Tone(Md.Text, 0.8f));
        ImGui.TextWrapped($"五盘共用的开关。当前运行：{(runningBoard >= 0 ? CrucibleBoards[runningBoard].Name : "无")}");
        ImGui.PopStyleColor();

        var footerY = ImGui.GetCursorScreenPos().Y;
        var footerTarget = minHeight > 0 ? top + minHeight - ImGui.GetFrameHeight() : footerY;
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, MathF.Max(footerY, footerTarget)));
        if (Md.TextButton("全部停用"))
            foreach (var s in boardScripts) if (s != null) manager.SetScriptEnabled(s.Metadata.Id, false);
        ImGui.SameLine(0, Md.Em * 0.4f);
        if (Md.TonalButton("全部启用"))
            foreach (var s in boardScripts) if (s != null) manager.SetScriptEnabled(s.Metadata.Id, true);
        var natural = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y - MathF.Max(0, footerTarget - footerY) - top;
        Md.EndCard(0f, 0.2f);
        return natural;
    }

    /// <summary>One board: switcher across the five boards, then the board script's header, mechanisms and settings.</summary>
    private void DrawCrucibleBoard(ScriptManager manager)
    {
        var settings = MasterPlayerSettings.Instance;
        if (Md.Link("‹ 斗兽奇弈"))
        {
            _libraryView = LibraryView.CrucibleGrid;
            return;
        }
        Md.Gap(0.3f);
        var index = Math.Clamp(_crucibleBoardIndex, 0, CrucibleBoards.Length - 1);
        var board = CrucibleBoards[index];
        DrawCrucibleTitle($"斗兽奇弈 · {board.Name}", $"{board.Range} · {board.Fights} 场战斗 · 魔兽等级上限 {board.LevelCap}");

        var scripts = CrucibleBoards.Select(b => BoardScript(manager, b)).ToArray();
        var dots = scripts.Select(s => s == null ? Md.Muted : StateColor(StateOf(manager, settings, s))).ToArray();
        _crucibleBoardIndex = Md.Segmented("##board_switcher", CrucibleBoards.Select(b => b.Name).ToArray(), index, 0f, dots);
        Md.StatusDot(Md.Success, "运行中");
        ImGui.SameLine(0, Md.Em);
        Md.StatusDot(Md.Accent, "已启用");
        ImGui.SameLine(0, Md.Em);
        Md.StatusDot(Md.Muted, "已停用");
        Md.Gap(0.3f);

        var script = scripts[_crucibleBoardIndex];
        if (script == null)
        {
            Md.MutedText($"{CrucibleBoards[_crucibleBoardIndex].Name}：没有找到这一盘的脚本。");
            return;
        }
        Md.BeginCard("board_note");
        ImGui.TextWrapped(CrucibleBoards[_crucibleBoardIndex].Description);
        Md.MutedText("普通和兽道目前共用这一盘的脚本，开关和设置是同一份。");
        Md.EndCard();
        DrawScriptDetails(manager, script, $"{CrucibleBoards[_crucibleBoardIndex].Name} · {script.Metadata.Name}");
    }
}
