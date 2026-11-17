using System.Numerics;
using BossMod;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using MasterPlayer.MechanicHost;
using MasterPlayer.Scripting;
using MasterPlayer.Settings;

namespace MasterPlayer.Windows;

/// <summary>
/// 机制库 · 副本列表：全部副本按类型／版本／支持度筛选，左侧列表、右侧详情。
/// 支持度由 <see cref="MechanicDutyCatalog.Evaluate"/> 从专属脚本和首领模块推导。
/// </summary>
public sealed partial class MainWindow
{
    private IReadOnlyList<DutyEntry>? _duties;
    private string[] _dutyExpansions = [];
    private (uint Id, string Name, uint Icon)[] _dutyTypes = [];
    private string? _dutyError;
    private string _dutyFilter = "";
    private int _dutySupportFilter; // 0 = all, 1 = full, 2 = partial, 3 = none
    private int _dutyExpansionFilter = -1;
    private bool _dutyHighEndOnly;
    private readonly HashSet<uint> _dutyTypeFilter = [];
    private uint _selectedDuty;

    private static readonly string[] SupportNames = ["未支持", "部分支持", "完全支持"];
    private static Vector4 SupportColor(DutySupport level) => level switch
    {
        DutySupport.Full => Md.Success,
        DutySupport.Partial => Md.Warning,
        _ => Md.Muted,
    };

    private static string MaturityName(BossModuleInfo.Maturity maturity) => maturity switch
    {
        BossModuleInfo.Maturity.AISupport => "已验证 · 支持 AI",
        BossModuleInfo.Maturity.Verified => "已验证",
        BossModuleInfo.Maturity.Contributed => "第三方贡献",
        BossModuleInfo.Maturity.WIP => "开发中",
        _ => "测试",
    };

    private void EnsureDuties()
    {
        if (_duties != null) return;
        try
        {
            _duties = MechanicDutyCatalog.Build();
            _dutyTypes = _duties.GroupBy(d => d.TypeId).OrderBy(g => g.Key)
                .Select(g => (g.Key, g.First().TypeName, g.First().Icon)).ToArray();
            var versions = Svc.Data.GetExcelSheet<ExVersion>();
            var count = _duties.Count == 0 ? 0 : (int)_duties.Max(d => d.Expansion) + 1;
            _dutyExpansions = Enumerable.Range(0, count)
                .Select(i => versions?.GetRowOrDefault((uint)i)?.Name.ToString() is { Length: > 0 } name ? name : $"{i + 2}.0").ToArray();
        }
        catch (Exception ex)
        {
            _duties = [];
            _dutyError = ex.Message;
            Svc.Log.Warning(ex, "[MasterPlayer] 副本列表构建失败");
        }
    }

    private void DrawDutiesTab()
    {
        EnsureDuties();
        var duties = _duties!;
        if (_dutyError != null)
            Md.Banner("duty_error", Dalamud.Interface.FontAwesomeIcon.ExclamationCircle, Md.Danger, $"副本列表构建失败：{_dutyError}");

        var settings = MasterPlayerSettings.Instance;
        var manager = MasterPlayerPlugin.Instance?.Scripts;
        var scripts = new Dictionary<uint, ScriptDescriptor>();
        foreach (var script in manager?.Scripts ?? [])
            foreach (var territory in script.Metadata.Territorys)
                scripts.TryAdd(territory, script);
        var current = Svc.ClientState.TerritoryType;

        // ── Filters ──
        Md.SearchField("##duty_filter", "搜索副本名称或区域 ID", ref _dutyFilter, Md.Em * 20);
        ImGui.SameLine(0, Md.Em * 0.8f);
        if (Md.Chip("只看高难##duty_highend", _dutyHighEndOnly)) _dutyHighEndOnly = !_dutyHighEndOnly;
        if (_dutyTypeFilter.Count != 0 || _dutyExpansionFilter >= 0 || _dutyHighEndOnly || _dutySupportFilter != 0 || _dutyFilter.Length != 0)
        {
            ImGui.SameLine(0, Md.Em * 0.8f);
            if (Md.TextButton("清除筛选"))
            {
                _dutyTypeFilter.Clear();
                _dutyExpansionFilter = -1;
                _dutyHighEndOnly = false;
                _dutySupportFilter = 0;
                _dutyFilter = "";
            }
        }
        Md.Gap(0.25f);
        DrawDutyTypeFilter();
        DrawDutyExpansionFilter();

        var text = _dutyFilter.Trim();
        var counts = new int[3];
        var matched = new List<(DutyEntry Entry, DutySupport Level, string Reason)>(duties.Count);
        foreach (var entry in duties)
        {
            if (_dutyTypeFilter.Count != 0 && !_dutyTypeFilter.Contains(entry.TypeId)) continue;
            if (_dutyExpansionFilter >= 0 && entry.Expansion != _dutyExpansionFilter) continue;
            if (_dutyHighEndOnly && !entry.HighEnd) continue;
            if (text.Length != 0 && !entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase) && !entry.TerritoryId.ToString().Contains(text)) continue;
            var (level, reason) = MechanicDutyCatalog.Evaluate(entry, scripts.GetValueOrDefault(entry.TerritoryId)?.Metadata.Name);
            counts[(int)level]++;
            matched.Add((entry, level, reason));
        }
        var total = matched.Count;
        _dutySupportFilter = Md.ChipGroup("duty_support",
            [$"全部 {total}", $"完全支持 {counts[2]}", $"部分支持 {counts[1]}", $"未支持 {counts[0]}"], _dutySupportFilter);
        if (_dutySupportFilter != 0)
        {
            var wanted = (DutySupport)(3 - _dutySupportFilter);
            matched.RemoveAll(m => m.Level != wanted);
        }
        // Current duty first, the rest in duty-finder order.
        var currentIndex = matched.FindIndex(m => m.Entry.TerritoryId == current);
        if (currentIndex > 0)
        {
            var item = matched[currentIndex];
            matched.RemoveAt(currentIndex);
            matched.Insert(0, item);
        }
        if (matched.Count != 0 && !matched.Any(m => m.Entry.CfcId == _selectedDuty))
            _selectedDuty = matched[0].Entry.CfcId;
        Md.Gap(0.25f);

        // ── List ──
        var listWidth = Math.Clamp(ImGui.GetContentRegionAvail().X * 0.52f, Md.Em * 20, Md.Em * 30);
        if (ImGui.BeginChild("##duty_list", new Vector2(listWidth, -1), false))
        {
            if (matched.Count == 0) Md.MutedText("没有匹配的副本，可修改搜索或筛选条件。");
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
            var rowHeight = MathF.Round(Md.Em * 3.3f);
            var origin = ImGui.GetCursorPosY();
            // Rows share one height, so only the visible slice is submitted.
            var first = Math.Clamp((int)((ImGui.GetScrollY() - origin) / rowHeight), 0, matched.Count);
            var last = Math.Min(matched.Count, first + (int)(ImGui.GetWindowHeight() / rowHeight) + 2);
            ImGui.SetCursorPosY(origin + first * rowHeight);
            for (var i = first; i < last; i++)
            {
                var (entry, level, _) = matched[i];
                if (DrawDutyRow(entry, level, entry.TerritoryId == current, scripts.ContainsKey(entry.TerritoryId), rowHeight))
                    _selectedDuty = entry.CfcId;
            }
            ImGui.SetCursorPosY(origin + matched.Count * rowHeight);
            ImGui.Dummy(new Vector2(1, 0));
            ImGui.PopStyleVar();
        }
        ImGui.EndChild();

        // ── Details ──
        ImGui.SameLine(0, Md.Em * 1.2f);
        if (ImGui.BeginChild("##duty_detail", new Vector2(-1, -1), false))
        {
            var index = matched.FindIndex(m => m.Entry.CfcId == _selectedDuty);
            if (index >= 0) DrawDutyDetails(matched[index].Entry, matched[index].Level, matched[index].Reason, scripts.GetValueOrDefault(matched[index].Entry.TerritoryId), settings, current);
            else Md.MutedText("在左侧选择副本查看支持情况。");
        }
        ImGui.EndChild();
    }

    private void DrawDutyTypeFilter()
    {
        var size = MathF.Round(Md.Em * 2.1f);
        var right = Md.RightEdge();
        for (var i = 0; i < _dutyTypes.Length; i++)
        {
            var (id, name, icon) = _dutyTypes[i];
            if (i > 0)
            {
                ImGui.SameLine(0, Md.Em * 0.3f);
                if (ImGui.GetCursorScreenPos().X + size > right) ImGui.NewLine();
            }
            var selected = _dutyTypeFilter.Contains(id);
            if (IconToggle($"##duty_type_{id}", icon, selected, _dutyTypeFilter.Count == 0, name, size))
            {
                if (selected) _dutyTypeFilter.Remove(id);
                else _dutyTypeFilter.Add(id);
            }
        }
        if (_dutyTypes.Length != 0) Md.Gap(0.15f);
    }

    private void DrawDutyExpansionFilter()
    {
        var right = Md.RightEdge();
        for (var i = -1; i < _dutyExpansions.Length; i++)
        {
            var label = i < 0 ? "全部版本" : _dutyExpansions[i];
            if (i >= 0)
            {
                ImGui.SameLine(0, Md.Em * 0.4f);
                if (ImGui.GetCursorScreenPos().X + ImGui.CalcTextSize(label).X + Md.Em * 1.1f > right) ImGui.NewLine();
            }
            if (Md.Chip($"{label}##duty_exp_{i}", _dutyExpansionFilter == i)) _dutyExpansionFilter = i;
        }
        Md.Gap(0.15f);
    }

    /// <summary>Square game-icon toggle: highlighted when selected, dimmed when another one is.</summary>
    private static bool IconToggle(string id, uint icon, bool selected, bool noneSelected, string tooltip, float size)
    {
        var pressed = ImGui.InvisibleButton(id, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        var radius = Md.Em * 0.4f;
        if (selected)
        {
            drawList.AddRectFilled(min, max, Md.U32(Md.Tone(Md.Accent, 0.22f)), radius);
            drawList.AddRect(min, max, Md.U32(Md.Accent), radius, ImDrawFlags.None, 1.5f);
        }
        else if (hovered) drawList.AddRectFilled(min, max, Md.U32(Md.Tone(Md.Text, 0.08f)), radius);
        var inset = new Vector2(size * 0.1f, size * 0.1f);
        DrawGameIcon(drawList, icon, min + inset, max - inset, selected || noneSelected || hovered ? 1f : 0.4f);
        if (hovered) Md.ShowTooltip(tooltip);
        return pressed;
    }

    private static void DrawGameIcon(ImDrawListPtr drawList, uint icon, Vector2 min, Vector2 max, float alpha = 1f)
    {
        if (icon == 0) return;
        var wrap = Svc.Texture.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrDefault();
        if (wrap != null)
            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, Md.U32(new Vector4(1, 1, 1, alpha)));
    }

    private bool DrawDutyRow(DutyEntry entry, DutySupport level, bool isCurrent, bool hasScript, float rowHeight)
    {
        ImGui.PushID((int)entry.CfcId);
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var clicked = ImGui.InvisibleButton("##row", new Vector2(width, rowHeight));
        var hovered = ImGui.IsItemHovered();
        var height = rowHeight - MathF.Round(Md.Em * 0.25f);
        var max = start + new Vector2(width, height);
        var drawList = ImGui.GetWindowDrawList();
        if (entry.CfcId == _selectedDuty) drawList.AddRectFilled(start, max, Md.U32(Md.Tone(Md.Accent, 0.16f)), Md.Radius);
        else if (hovered) drawList.AddRectFilled(start, max, Md.U32(Md.Tone(Md.Text, 0.05f)), Md.Radius);

        var iconSize = MathF.Round(Md.Em * 2.1f);
        var iconMin = start + new Vector2(Md.Em * 0.55f, (height - iconSize) / 2);
        DrawGameIcon(drawList, entry.Icon, iconMin, iconMin + new Vector2(iconSize, iconSize));

        // Support tag, right-aligned.
        var color = SupportColor(level);
        var tag = SupportNames[(int)level];
        var tagSize = ImGui.CalcTextSize(tag);
        var tagPad = new Vector2(Md.Em * 0.5f, Md.Em * 0.14f);
        var tagMin = new Vector2(max.X - Md.Em * 0.6f - tagSize.X - tagPad.X * 2, start.Y + (height - tagSize.Y) / 2 - tagPad.Y);
        var tagMax = tagMin + tagSize + tagPad * 2;
        if (level == DutySupport.None) drawList.AddRect(tagMin, tagMax, Md.U32(Md.Outline), (tagMax.Y - tagMin.Y) / 2, ImDrawFlags.None, 1f);
        else drawList.AddRectFilled(tagMin, tagMax, Md.U32(Md.Tone(color, 0.16f)), (tagMax.Y - tagMin.Y) / 2);
        drawList.AddText(tagMin + tagPad, Md.U32(color), tag);

        var x = iconMin.X + iconSize + Md.Em * 0.65f;
        var lineHeight = ImGui.GetTextLineHeight();
        var y = start.Y + (height - lineHeight * 2 - Md.Em * 0.1f) / 2;
        drawList.PushClipRect(new Vector2(x, start.Y), new Vector2(tagMin.X - Md.Em * 0.5f, max.Y), true);
        drawList.AddText(new Vector2(x, y), Md.U32(Md.Text), entry.Name);
        var subtitle = $"{entry.TypeName} · {ExpansionName(entry.Expansion)} · {entry.Level} 级";
        if (hasScript) subtitle += " · 专属脚本";
        else if (entry.Modules.Count != 0) subtitle += $" · 模块 {entry.VerifiedCount}/{entry.Modules.Count}";
        if (isCurrent) subtitle = "当前副本 · " + subtitle;
        drawList.AddText(new Vector2(x, y + lineHeight + Md.Em * 0.1f), Md.U32(isCurrent ? Md.Accent : Md.Muted), subtitle);
        drawList.PopClipRect();
        ImGui.PopID();
        return clicked;
    }

    private string ExpansionName(uint expansion) => expansion < _dutyExpansions.Length ? _dutyExpansions[expansion] : "—";

    private void DrawDutyDetails(DutyEntry entry, DutySupport level, string reason, ScriptDescriptor? script, MasterPlayerSettings settings, uint current)
    {
        ImGui.PushID((int)entry.CfcId);
        Md.BeginCard("duty_header");
        var iconSize = MathF.Round(Md.Em * 2.8f);
        var iconMin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(iconSize, iconSize));
        DrawGameIcon(ImGui.GetWindowDrawList(), entry.Icon, iconMin, iconMin + new Vector2(iconSize, iconSize));
        ImGui.SameLine(0, Md.Em * 0.7f);
        ImGui.BeginGroup();
        Md.Title(entry.Name);
        Md.Pill(SupportNames[(int)level], SupportColor(level));
        ImGui.SameLine(0, Md.Em * 0.5f);
        Md.MutedText(reason);
        ImGui.EndGroup();
        Md.Gap(0.2f);
        var enabled = settings.IsTerritoryEnabled(entry.TerritoryId);
        if (Md.SwitchRow("在这个副本启用机制", "关闭后，这个副本的机制绘制、提示和走位同时停用。", ref enabled))
        {
            settings.TerritoryEnabled[entry.TerritoryId] = enabled;
            settings.Save();
        }
        Md.ValueRow("类型", entry.HighEnd ? $"{entry.TypeName} · 高难" : entry.TypeName);
        Md.ValueRow("版本", ExpansionName(entry.Expansion));
        Md.ValueRow("等级", $"{entry.Level} 级");
        Md.ValueRow("区域", entry.TerritoryId == current ? $"{entry.TerritoryId} · 当前所在" : entry.TerritoryId.ToString());
        Md.EndCard();

        if (script != null)
        {
            Md.BeginCard("duty_script", Md.CardKind.Outlined);
            Md.SectionLabel("专属脚本", "有专属脚本的副本由脚本接管，优先于首领模块。");
            Md.ActionRow(script.Metadata.Name, $"v{script.Metadata.Version} · {script.Metadata.Author}", Md.ButtonWidth("脚本设置"),
                () => { if (Md.TonalButton("脚本设置")) OpenScript(script.Metadata.Id); });
            Md.EndCard();
        }

        if (entry.Modules.Count != 0 || entry.HasZoneModule)
        {
            Md.BeginCard("duty_modules", Md.CardKind.Outlined);
            Md.SectionLabel($"首领模块（{entry.Modules.Count}）", "成熟度低于「诊断」页门槛的模块不会启用。");
            var threshold = Math.Clamp(settings.MechanicModuleMinMaturity, 0, 4);
            foreach (var module in entry.Modules)
            {
                var active = (int)module.Maturity >= threshold;
                var verified = module.Maturity >= BossModuleInfo.Maturity.Verified;
                Md.ValueRow(module.Name, active ? MaturityName(module.Maturity) : $"{MaturityName(module.Maturity)} · 未达门槛",
                    !active ? Md.Muted : verified ? Md.Success : Md.Warning);
            }
            if (entry.HasZoneModule) Md.ValueRow("区域模块", "有（道中机制）", Md.Success);
            Md.EndCard();
        }

        Md.BeginCard("duty_legend", Md.CardKind.Outlined);
        Md.SectionLabel("支持度说明");
        Md.StatusDot(Md.Success, "完全支持：有专属脚本，或每个首领都有已验证的模块");
        Md.StatusDot(Md.Warning, "部分支持：有模块未验证、缺首领，或只有区域模块");
        Md.StatusDot(Md.Muted, "未支持：暂无机制模块，只按读条数据画普通范围");
        Md.EndCard();
        ImGui.PopID();
    }
}
