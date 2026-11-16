using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.ManagedFontAtlas;
using ECommons.DalamudServices;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

/// <summary>
/// Material-style building blocks for the settings window: cards, setting rows, list items,
/// segmented buttons, chips, buttons and tooltips. Colours come from the PR theme so the window
/// follows it; sizes scale with the font.
/// </summary>
internal static class Md
{
    public static Vector4 Accent => PromeUiTheme.Accent;
    public static Vector4 Success => PromeUiTheme.Success;
    public static Vector4 Warning => PromeUiTheme.Warning;
    public static Vector4 Danger => PromeUiTheme.Danger;
    public static Vector4 Text => PromeUiTheme.Text;
    public static Vector4 Muted => PromeUiTheme.TextMuted;
    public static Vector4 Surface => ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg] with { W = 1f };
    public static Vector4 Outline => Muted with { W = 0.30f };
    public static Vector4 Gold => new(1f, 0.80f, 0.22f, 1f);

    public static Vector4 Tone(Vector4 color, float alpha) => color with { W = alpha };
    public static uint U32(Vector4 color) => ImGui.GetColorU32(color);

    /// <summary>One font-relative unit; all spacing is a multiple of it.</summary>
    public static float Em => ImGui.GetFontSize();
    public static float Radius => MathF.Round(Em * 0.75f);

    // ── Cards ────────────────────────────────────────────────────────────────────────────
    // The background is drawn before the content using last frame's height, so cards work
    // inside tables and nest without draw-list channel splitting.

    public enum CardKind { Filled, Outlined, Warning, Selected, Gold, Running }

    private sealed class CardFrame
    {
        public uint Key;
        public Vector2 Min;
        public float Width, Pad;
        public int Rows;
    }

    private static readonly Dictionary<uint, float> CardHeights = [];
    private static readonly Stack<CardFrame> Cards = new();

    public static void BeginCard(string id, CardKind kind = CardKind.Filled, float width = 0f, float padding = -1f)
    {
        var pad = padding >= 0 ? padding : MathF.Round(Em * 0.9f);
        ImGui.PushID(id);
        var key = ImGui.GetID("##card");
        var min = ImGui.GetCursorScreenPos();
        var w = width > 0 ? width : ImGui.GetContentRegionAvail().X;
        var h = CardHeights.TryGetValue(key, out var cached) ? cached : pad * 2 + Em;
        var max = min + new Vector2(w, h);
        var drawList = ImGui.GetWindowDrawList();
        switch (kind)
        {
            case CardKind.Filled:
                drawList.AddRectFilled(min, max, U32(Tone(Accent, 0.06f)), Radius);
                break;
            case CardKind.Outlined:
                drawList.AddRect(min, max, U32(Outline), Radius, ImDrawFlags.None, 1f);
                break;
            case CardKind.Warning:
                drawList.AddRectFilled(min, max, U32(Tone(Warning, 0.13f)), Radius);
                break;
            case CardKind.Selected:
                drawList.AddRectFilled(min, max, U32(Tone(Accent, 0.08f)), Radius);
                drawList.AddRect(min, max, U32(Accent), Radius, ImDrawFlags.None, 2f);
                break;
            case CardKind.Gold:
                drawList.AddRectFilled(min, max, U32(Tone(Gold, 0.10f)), Radius);
                break;
            case CardKind.Running:
                drawList.AddRectFilled(min, max, U32(Tone(Success, 0.06f)), Radius);
                drawList.AddRect(min, max, U32(Success), Radius, ImDrawFlags.None, 2f);
                break;
        }

        ImGui.SetCursorScreenPos(min + new Vector2(pad, pad));
        ImGui.BeginGroup();
        ImGui.PushTextWrapPos(LocalX(min.X + w - pad));
        Cards.Push(new CardFrame { Key = key, Min = min, Width = w, Pad = pad });
    }

    /// <summary>Closes the card; <paramref name="minContentHeight"/> pads it so cards in one grid row line up.</summary>
    public static float EndCard(float minContentHeight = 0f, float gapAfter = 0.75f)
    {
        var frame = Cards.Peek();
        var contentTop = frame.Min.Y + frame.Pad;
        var natural = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y - contentTop;
        if (minContentHeight > natural)
            ImGui.Dummy(new Vector2(0, minContentHeight - natural - ImGui.GetStyle().ItemSpacing.Y));
        Cards.Pop();
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        var height = ImGui.GetItemRectMax().Y - frame.Min.Y + frame.Pad;
        CardHeights[frame.Key] = height;
        Advance(frame.Min.X, frame.Min.Y + height + Em * gapAfter, frame.Width);
        ImGui.PopID();
        return natural;
    }

    public static void SearchField(string id, string hint, ref string text, float width = 0f)
    {
        var w = width > 0 ? width : RightEdge() - ImGui.GetCursorScreenPos().X;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, ImGui.GetFrameHeight());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(Em * 0.9f, ImGui.GetStyle().FramePadding.Y + Em * 0.1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, Tone(Accent, 0.08f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Tone(Accent, 0.12f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, Tone(Accent, 0.14f));
        ImGui.SetNextItemWidth(w);
        ImGui.InputTextWithHint(id, hint, ref text, 256);
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    /// <summary>Screen rectangle of the card being drawn, for hover and click tests.</summary>
    public static (Vector2 Min, Vector2 Max) CurrentCardRect()
    {
        var frame = Cards.Peek();
        var h = CardHeights.TryGetValue(frame.Key, out var cached) ? cached : frame.Pad * 2 + Em;
        return (frame.Min, frame.Min + new Vector2(frame.Width, h));
    }

    /// <summary>Right edge (screen X) of the current content area: the card's inner edge inside a card.</summary>
    public static float RightEdge()
    {
        if (Cards.Count != 0)
        {
            var frame = Cards.Peek();
            return frame.Min.X + frame.Width - frame.Pad;
        }
        return ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X;
    }

    private static float LocalX(float screenX) => screenX - ImGui.GetWindowPos().X + ImGui.GetScrollX();

    // Moves the cursor to (x, y) and submits an empty item there so the window's content size
    // includes it; the item spacing that follows is cancelled so callers control the gap.
    private static void Advance(float x, float y, float width)
    {
        ImGui.SetCursorScreenPos(new Vector2(x, y - ImGui.GetStyle().ItemSpacing.Y));
        ImGui.Dummy(new Vector2(width, 0));
    }

    public static void Gap(float em = 0.75f) => ImGui.Dummy(new Vector2(0, Em * em));

    // ── Text ─────────────────────────────────────────────────────────────────────────────

    // Larger text uses fonts baked at that size, not SetWindowFontScale: scaling the default
    // atlas makes titles blurry.
    public enum FontRole { Heading, Title }

    private static IFontHandle? _headingFont, _titleFont;

    private static IFontHandle FontHandle(FontRole role)
    {
        var atlas = Svc.PluginInterface.UiBuilder.FontAtlas;
        return role == FontRole.Title
            ? _titleFont ??= atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(24f)))
            : _headingFont ??= atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(20f)));
    }

    public static IDisposable PushFont(FontRole role) => FontHandle(role).Push();

    public static void DisposeFonts()
    {
        _headingFont?.Dispose();
        _titleFont?.Dispose();
        _headingFont = _titleFont = null;
    }

    public static void BigText(string text, FontRole role = FontRole.Heading, Vector4? color = null)
    {
        using (PushFont(role))
        {
            if (color is { } c) ImGui.TextColored(c, text);
            else ImGui.TextUnformatted(text);
        }
    }

    public static void PageHeader(string title, string? subtitle)
    {
        BigText(title, FontRole.Title);
        if (!string.IsNullOrEmpty(subtitle))
            MutedText(subtitle);
        Gap(0.6f);
    }

    public static void SectionLabel(string text, string? help = null)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
        if (help != null)
        {
            ImGui.SameLine();
            Info(help);
        }
    }

    public static void Title(string text) => BigText(text, FontRole.Heading);

    public static void MutedText(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static void Icon(FontAwesomeIcon icon, Vector4 color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
        ImGui.PopFont();
    }

    /// <summary>Small "ⓘ" marker whose explanation shows as a tooltip.</summary>
    public static void Info(string text)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(Muted, FontAwesomeIcon.InfoCircle.ToIconString());
        ImGui.PopFont();
        Tooltip(text);
    }

    /// <summary>Material plain tooltip: small inverse-coloured label, shown while the last item is hovered.</summary>
    public static void Tooltip(string text)
    {
        if (!ImGui.IsItemHovered()) return;
        ShowTooltip(text);
    }

    public static void ShowTooltip(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.PopupBg, Tone(Text, 0.96f));
        ImGui.PushStyleColor(ImGuiCol.Text, Surface);
        ImGui.PushStyleColor(ImGuiCol.Border, Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(Em * 0.6f, Em * 0.4f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Em * 0.35f);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Em * 20);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(3);
    }

    public static void Pill(string text, Vector4 color)
    {
        var size = ImGui.CalcTextSize(text);
        var padX = Em * 0.5f;
        var padY = Em * 0.12f;
        var min = ImGui.GetCursorScreenPos();
        var max = min + size + new Vector2(padX * 2, padY * 2);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, U32(Tone(color, 0.16f)), (max.Y - min.Y) / 2);
        ImGui.GetWindowDrawList().AddText(min + new Vector2(padX, padY), U32(color), text);
        ImGui.Dummy(max - min);
    }

    /// <summary>Licence-style tag: solid tinted when active, dashed-looking outline when only reserved.</summary>
    public static void Tag(string text, Vector4 color, bool solid)
    {
        var size = ImGui.CalcTextSize(text);
        var padX = Em * 0.45f;
        var padY = Em * 0.1f;
        var min = ImGui.GetCursorScreenPos();
        var max = min + size + new Vector2(padX * 2, padY * 2);
        var drawList = ImGui.GetWindowDrawList();
        if (solid)
            drawList.AddRectFilled(min, max, U32(Tone(color, 0.16f)), Em * 0.3f);
        else
            drawList.AddRect(min, max, U32(Tone(color, 0.6f)), Em * 0.3f, ImDrawFlags.None, 1f);
        drawList.AddText(min + new Vector2(padX, padY), U32(color), text);
        ImGui.Dummy(max - min);
    }

    /// <summary>Coloured dot + label on one line; drawn directly so it never wraps.</summary>
    public static void StatusDot(Vector4 color, string text)
    {
        var min = ImGui.GetCursorScreenPos();
        var r = Em * 0.22f;
        var lineH = ImGui.GetTextLineHeight();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircleFilled(new Vector2(min.X + r, min.Y + lineH / 2), r, U32(color));
        drawList.AddText(new Vector2(min.X + r * 2 + Em * 0.3f, min.Y), U32(color), text);
        ImGui.Dummy(new Vector2(StatusDotWidth(text), lineH));
    }

    public static float StatusDotWidth(string text) => Em * 0.44f + Em * 0.3f + ImGui.CalcTextSize(text).X;

    // ── Buttons ──────────────────────────────────────────────────────────────────────────

    private static bool PillButton(string label, Vector4? fill, Vector4 textColor, Vector4? border, float width = 0f)
    {
        var split = label.IndexOf("##", StringComparison.Ordinal);
        var text = split >= 0 ? label[..split] : label;
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(width > 0 ? width : textSize.X + Em * 1.4f, ImGui.GetFrameHeight());
        var pressed = ImGui.InvisibleButton(label, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        var radius = size.Y / 2;
        if (fill is { } f) drawList.AddRectFilled(min, max, U32(f), radius);
        if (ImGui.IsItemHovered()) drawList.AddRectFilled(min, max, U32(Tone(textColor, ImGui.IsItemActive() ? 0.16f : 0.08f)), radius);
        if (border is { } b) drawList.AddRect(min, max, U32(b), radius, ImDrawFlags.None, 1f);
        drawList.AddText(min + (size - textSize) / 2, U32(textColor), text);
        return pressed;
    }

    public static bool TextButton(string label) => PillButton(label, null, Accent, null);
    public static bool TonalButton(string label) => PillButton(label, Tone(Accent, 0.20f), Text, null);
    public static bool OutlinedButton(string label) => PillButton(label, null, Accent, Outline);
    public static bool WarningButton(string label) => PillButton(label, null, Warning, Tone(Warning, 0.6f));

    /// <summary>Clickable text link with an arrow, used to navigate deeper.</summary>
    public static bool Link(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Accent);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        var hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(new Vector2(min.X, max.Y), max, U32(Accent), 1f);
        }
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    /// <summary>Filter chip: tinted when selected, outlined otherwise.</summary>
    public static bool Chip(string label, bool selected)
    {
        var split = label.IndexOf("##", StringComparison.Ordinal);
        var text = split >= 0 ? label[..split] : label;
        var textSize = ImGui.CalcTextSize(text);
        var size = new Vector2(textSize.X + Em * 1.1f, ImGui.GetFrameHeight());
        var pressed = ImGui.InvisibleButton(label, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        var radius = Em * 0.4f;
        if (selected) drawList.AddRectFilled(min, max, U32(Tone(Accent, 0.20f)), radius);
        else drawList.AddRect(min, max, U32(Outline), radius, ImDrawFlags.None, 1f);
        if (ImGui.IsItemHovered()) drawList.AddRectFilled(min, max, U32(Tone(Text, 0.06f)), radius);
        drawList.AddText(min + (size - textSize) / 2, U32(selected ? Text : Muted), text);
        return pressed;
    }

    public static int ChipGroup(string id, IReadOnlyList<string> labels, int active)
    {
        for (var i = 0; i < labels.Count; i++)
        {
            if (i > 0) ImGui.SameLine(0, Em * 0.4f);
            if (Chip($"{labels[i]}##{id}_{i}", i == active)) active = i;
        }
        return active;
    }

    /// <summary>Segmented button spanning <paramref name="width"/> (0 = to the right edge); optional status dots per segment.</summary>
    public static int Segmented(string id, IReadOnlyList<string> labels, int active, float width = 0f, IReadOnlyList<Vector4>? dots = null)
    {
        var min = ImGui.GetCursorScreenPos();
        var total = width > 0 ? width : RightEdge() - min.X;
        var height = ImGui.GetFrameHeight() + Em * 0.15f;
        var segment = total / labels.Count;
        var radius = height / 2;
        var drawList = ImGui.GetWindowDrawList();
        ImGui.PushID(id);
        for (var i = 0; i < labels.Count; i++)
        {
            ImGui.SetCursorScreenPos(new Vector2(min.X + segment * i, min.Y));
            if (ImGui.InvisibleButton($"##seg{i}", new Vector2(segment, height))) active = i;
            var sMin = ImGui.GetItemRectMin();
            var sMax = ImGui.GetItemRectMax();
            var corners = labels.Count == 1 ? ImDrawFlags.RoundCornersAll
                : i == 0 ? ImDrawFlags.RoundCornersLeft
                : i == labels.Count - 1 ? ImDrawFlags.RoundCornersRight
                : ImDrawFlags.RoundCornersNone;
            if (i == active)
                drawList.AddRectFilled(sMin, sMax, U32(Tone(Accent, 0.22f)), radius, corners);
            else if (ImGui.IsItemHovered())
                drawList.AddRectFilled(sMin, sMax, U32(Tone(Text, 0.06f)), radius, corners);
            var textSize = ImGui.CalcTextSize(labels[i]);
            var dotSpace = dots != null ? Em * 0.6f : 0f;
            var textPos = new Vector2(sMin.X + (segment - textSize.X - dotSpace) / 2 + dotSpace, sMin.Y + (height - textSize.Y) / 2);
            if (dots != null)
                drawList.AddCircleFilled(new Vector2(textPos.X - Em * 0.42f, sMin.Y + height / 2), Em * 0.2f, U32(dots[i]));
            drawList.AddText(textPos, U32(i == active ? Text : Muted), labels[i]);
            if (i > 0)
                drawList.AddLine(new Vector2(sMin.X, sMin.Y), new Vector2(sMin.X, sMax.Y), U32(Outline), 1f);
        }
        drawList.AddRect(min, min + new Vector2(total, height), U32(Tone(Muted, 0.6f)), radius, ImDrawFlags.None, 1f);
        ImGui.PopID();
        Advance(min.X, min.Y + height + ImGui.GetStyle().ItemSpacing.Y, total);
        return active;
    }

    // ── Setting rows ─────────────────────────────────────────────────────────────────────
    // One setting per row: name (and an optional one-line explanation) on the left, the control
    // right-aligned. Rows inside a card are separated by thin dividers.

    private static (float Left, float Right, float Top) BeginRow()
    {
        var start = ImGui.GetCursorScreenPos();
        var right = RightEdge();
        if (Cards.Count != 0)
        {
            var frame = Cards.Peek();
            if (frame.Rows++ > 0)
                ImGui.GetWindowDrawList().AddLine(new Vector2(start.X, start.Y), new Vector2(right, start.Y), U32(Outline), 1f);
        }
        return (start.X, right, start.Y);
    }

    private static float RowHeight(string label, string? description, float labelWidth, float controlHeight)
    {
        var block = ImGui.GetTextLineHeight();
        if (!string.IsNullOrEmpty(description))
            block += Em * 0.15f + ImGui.CalcTextSize(description, false, labelWidth).Y;
        return MathF.Max(block, controlHeight) + Em * 0.9f;
    }

    private static void DrawRowLabel(string label, string? description, float left, float top, float rowHeight, float labelWidth, bool dim = false)
    {
        var block = ImGui.GetTextLineHeight();
        Vector2 descSize = default;
        if (!string.IsNullOrEmpty(description))
        {
            descSize = ImGui.CalcTextSize(description, false, labelWidth);
            block += Em * 0.15f + descSize.Y;
        }
        var y = top + (rowHeight - block) / 2;
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        ImGui.PushStyleColor(ImGuiCol.Text, dim ? Muted : Text);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        if (!string.IsNullOrEmpty(description))
        {
            ImGui.SetCursorScreenPos(new Vector2(left, y + ImGui.GetTextLineHeight() + Em * 0.15f));
            ImGui.PushTextWrapPos(LocalX(left + labelWidth));
            ImGui.PushStyleColor(ImGuiCol.Text, Muted);
            ImGui.TextWrapped(description);
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
        }
    }

    private static void EndRow(float left, float top, float right, float rowHeight)
        => Advance(left, top + rowHeight, right - left);

    public static bool SwitchRow(string label, string? description, ref bool value, bool enabled = true)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var track = PluginUi.SwitchSize;
        var labelWidth = right - left - track.X - Em;
        var h = RowHeight(label, description, labelWidth, track.Y);
        // The whole row toggles, not only the switch.
        ImGui.SetCursorScreenPos(new Vector2(left, top));
        ImGui.BeginDisabled(!enabled);
        var pressed = ImGui.InvisibleButton("##row", new Vector2(right - left, h));
        var hovered = ImGui.IsItemHovered();
        ImGui.EndDisabled();
        if (pressed) value = !value;
        DrawRowLabel(label, description, left, top, h, labelWidth, !enabled);
        PluginUi.DrawSwitch(ImGui.GetWindowDrawList(), ImGui.GetID("##row"), new Vector2(right - track.X, top + (h - track.Y) / 2), value, hovered && enabled, enabled);
        EndRow(left, top, right, h);
        ImGui.PopID();
        return pressed;
    }

    public static bool SliderRow(string label, string? description, ref float value, float min, float max, string format, bool enabled = true)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var sliderWidth = MathF.Min(Em * 12f, (right - left) * 0.5f);
        var labelWidth = right - left - sliderWidth - Em;
        var h = RowHeight(label, description, labelWidth, ImGui.GetFrameHeight());
        DrawRowLabel(label, description, left, top, h, labelWidth, !enabled);
        ImGui.SetCursorScreenPos(new Vector2(right - sliderWidth, top + (h - ImGui.GetFrameHeight()) / 2));
        ImGui.SetNextItemWidth(sliderWidth);
        ImGui.BeginDisabled(!enabled);
        var changed = ImGui.SliderFloat("##slider", ref value, min, max, format);
        LastSliderEngaged = ImGui.IsItemActive() || ImGui.IsItemHovered();
        ImGui.EndDisabled();
        EndRow(left, top, right, h);
        ImGui.PopID();
        return changed;
    }

    /// <summary>True while the slider of the last <see cref="SliderRow"/> is hovered or dragged.</summary>
    public static bool LastSliderEngaged;

    public static bool ComboRow(string label, string? description, ref int index, string itemsSeparatedByZeros, float comboWidth = 0f)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var width = comboWidth > 0 ? comboWidth : MathF.Min(Em * 12f, (right - left) * 0.5f);
        var labelWidth = right - left - width - Em;
        var h = RowHeight(label, description, labelWidth, ImGui.GetFrameHeight());
        DrawRowLabel(label, description, left, top, h, labelWidth);
        ImGui.SetCursorScreenPos(new Vector2(right - width, top + (h - ImGui.GetFrameHeight()) / 2));
        ImGui.SetNextItemWidth(width);
        var changed = ImGui.Combo("##combo", ref index, itemsSeparatedByZeros);
        EndRow(left, top, right, h);
        ImGui.PopID();
        return changed;
    }

    public static bool SegmentedRow(string label, string? description, IReadOnlyList<string> options, ref int index)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var width = MathF.Min(Em * 4.5f * options.Count, (right - left) * 0.55f);
        var labelWidth = right - left - width - Em;
        var controlH = ImGui.GetFrameHeight() + Em * 0.15f;
        var h = RowHeight(label, description, labelWidth, controlH);
        DrawRowLabel(label, description, left, top, h, labelWidth);
        ImGui.SetCursorScreenPos(new Vector2(right - width, top + (h - controlH) / 2));
        var before = index;
        index = Segmented("##seg", options, index, width);
        EndRow(left, top, right, h);
        ImGui.PopID();
        return before != index;
    }

    public static bool ColorRow(string label, ref Vector4 color)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var sliderWidth = MathF.Min(Em * 10f, (right - left) * 0.4f);
        var swatch = ImGui.GetFrameHeight();
        var controlWidth = swatch + Em * 0.5f + sliderWidth;
        var labelWidth = right - left - controlWidth - Em;
        var h = RowHeight(label, null, labelWidth, ImGui.GetFrameHeight());
        DrawRowLabel(label, null, left, top, h, labelWidth);
        var y = top + (h - ImGui.GetFrameHeight()) / 2;
        ImGui.SetCursorScreenPos(new Vector2(right - controlWidth, y));
        var rgb = new Vector3(color.X, color.Y, color.Z);
        var changed = ImGui.ColorEdit3("##color", ref rgb, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
        if (changed) { color.X = rgb.X; color.Y = rgb.Y; color.Z = rgb.Z; }
        ImGui.SetCursorScreenPos(new Vector2(right - sliderWidth, y));
        ImGui.SetNextItemWidth(sliderWidth);
        var percent = Math.Clamp(color.W * 100, 0, 100);
        if (ImGui.SliderFloat("##opacity", ref percent, 0, 100, "不透明度 %.0f%%"))
        {
            color.W = percent / 100;
            changed = true;
        }
        EndRow(left, top, right, h);
        ImGui.PopID();
        return changed;
    }

    /// <summary>Read-only "name ……… value" row.</summary>
    public static void ValueRow(string label, string value, Vector4? color = null)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var valueWidth = MathF.Min(ImGui.CalcTextSize(value).X, (right - left) * 0.6f);
        var labelWidth = right - left - valueWidth - Em;
        var valueSize = ImGui.CalcTextSize(value, false, (right - left) * 0.6f);
        var h = MathF.Max(ImGui.GetTextLineHeight(), valueSize.Y) + Em * 0.7f;
        DrawRowLabel(label, null, left, top, h, labelWidth, dim: true);
        ImGui.SetCursorScreenPos(new Vector2(right - valueWidth, top + (h - valueSize.Y) / 2));
        ImGui.PushTextWrapPos(LocalX(right));
        ImGui.PushStyleColor(ImGuiCol.Text, color ?? Text);
        ImGui.TextWrapped(value);
        ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();
        EndRow(left, top, right, h);
        ImGui.PopID();
    }

    /// <summary>Row holding arbitrary content on the right (buttons etc.), drawn by <paramref name="control"/>.</summary>
    public static void ActionRow(string label, string? description, float controlWidth, Action control)
    {
        ImGui.PushID(label);
        var (left, right, top) = BeginRow();
        var labelWidth = right - left - controlWidth - Em;
        var h = RowHeight(label, description, labelWidth, ImGui.GetFrameHeight());
        DrawRowLabel(label, description, left, top, h, labelWidth);
        ImGui.SetCursorScreenPos(new Vector2(right - controlWidth, top + (h - ImGui.GetFrameHeight()) / 2));
        control();
        EndRow(left, top, right, h);
        ImGui.PopID();
    }

    public static float ButtonWidth(string label)
    {
        var split = label.IndexOf("##", StringComparison.Ordinal);
        return ImGui.CalcTextSize(split >= 0 ? label[..split] : label).X + Em * 1.4f;
    }

    // ── List items ───────────────────────────────────────────────────────────────────────

    public readonly record struct Lead(FontAwesomeIcon Icon, Vector4 Color);

    /// <summary>
    /// Material list item: optional round leading icon, title (+ optional trailing gold crown) and
    /// supporting text, and an optional trailing switch. Returns true when the row body is clicked.
    /// </summary>
    public static bool ListItem(string id, Lead? lead, string title, string? subtitle, bool selected,
        ref bool switchValue, out bool toggled, bool hasSwitch = true, bool crown = false, string? tooltip = null)
    {
        ImGui.PushID(id);
        var start = ImGui.GetCursorScreenPos();
        var right = RightEdge();
        var height = MathF.Round(Em * (subtitle != null ? 3.0f : 2.4f));
        var track = PluginUi.SwitchSize;
        var switchArea = hasSwitch ? track.X + Em * 1.2f : 0f;
        var bodyWidth = MathF.Max(1, right - start.X - switchArea);
        var clicked = ImGui.InvisibleButton("##body", new Vector2(bodyWidth, height));
        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        var rowMax = new Vector2(right, start.Y + height);
        if (selected) drawList.AddRectFilled(start, rowMax, U32(Tone(Accent, 0.16f)), Radius);
        else if (hovered) drawList.AddRectFilled(start, rowMax, U32(Tone(Text, 0.05f)), Radius);

        var x = start.X + Em * 0.7f;
        if (lead is { } l)
        {
            var r = Em * 0.85f;
            var center = new Vector2(x + r, start.Y + height / 2);
            drawList.AddCircleFilled(center, r, U32(Tone(l.Color, 0.16f)));
            ImGui.PushFont(UiBuilder.IconFont);
            var glyph = l.Icon.ToIconString();
            var g = ImGui.CalcTextSize(glyph) * 0.8f;
            drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize() * 0.8f, center - g / 2, U32(l.Color), glyph);
            ImGui.PopFont();
            x += r * 2 + Em * 0.7f;
        }

        var textRight = start.X + bodyWidth - Em * 0.4f;
        drawList.PushClipRect(new Vector2(x, start.Y), new Vector2(textRight, start.Y + height), true);
        var lineH = ImGui.GetTextLineHeight();
        var block = subtitle != null ? lineH * 2 + Em * 0.1f : lineH;
        var y = start.Y + (height - block) / 2;
        drawList.AddText(new Vector2(x, y), U32(Text), title);
        if (crown)
        {
            ImGui.PushFont(UiBuilder.IconFont);
            var glyph = FontAwesomeIcon.Crown.ToIconString();
            ImGui.PopFont();
            drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize() * 0.75f,
                new Vector2(x + ImGui.CalcTextSize(title).X + Em * 0.35f, y + lineH * 0.15f), U32(Gold), glyph);
        }
        if (subtitle != null)
            drawList.AddText(new Vector2(x, y + lineH + Em * 0.1f), U32(Muted), subtitle);
        drawList.PopClipRect();
        if (hovered && tooltip != null) ShowTooltip(tooltip);

        toggled = false;
        if (hasSwitch)
        {
            var switchPos = new Vector2(right - track.X - Em * 0.6f, start.Y + (height - track.Y) / 2);
            ImGui.SetCursorScreenPos(switchPos - new Vector2(Em * 0.3f, Em * 0.3f));
            if (ImGui.InvisibleButton("##switch", track + new Vector2(Em * 0.6f, Em * 0.6f)))
            {
                switchValue = !switchValue;
                toggled = true;
            }
            PluginUi.DrawSwitch(drawList, ImGui.GetID("##switch"), switchPos, switchValue, ImGui.IsItemHovered());
        }
        Advance(start.X, start.Y + height + Em * 0.25f, right - start.X);
        ImGui.PopID();
        return clicked;
    }

    /// <summary>Material navigation-drawer item: icon + label in a pill, tinted when active.</summary>
    public static bool NavItem(string id, FontAwesomeIcon icon, string label, bool active)
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = MathF.Round(Em * 2.2f);
        var pressed = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var drawList = ImGui.GetWindowDrawList();
        var max = start + new Vector2(width, height);
        if (active) drawList.AddRectFilled(start, max, U32(Tone(Accent, 0.22f)), height / 2);
        else if (hovered) drawList.AddRectFilled(start, max, U32(Tone(Text, 0.06f)), height / 2);
        var color = active ? Text : Muted;
        ImGui.PushFont(UiBuilder.IconFont);
        var glyph = icon.ToIconString();
        var g = ImGui.CalcTextSize(glyph);
        ImGui.PopFont();
        var slot = Em * 1.3f;
        drawList.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), new Vector2(start.X + Em * 0.8f + (slot - g.X) / 2, start.Y + (height - g.Y) / 2), U32(color), glyph);
        drawList.AddText(new Vector2(start.X + Em * 0.8f + slot + Em * 0.6f, start.Y + (height - ImGui.GetTextLineHeight()) / 2), U32(color), label);
        Advance(start.X, start.Y + height + Em * 0.15f, width);
        return pressed;
    }

    /// <summary>Compact text + switch row for the navigation drawer.</summary>
    public static bool DrawerSwitch(string label, ref bool value)
    {
        // "text###id" keeps the id stable while the text changes (e.g. a running timer).
        var idStart = label.IndexOf("###", StringComparison.Ordinal);
        var textEnd = label.IndexOf("##", StringComparison.Ordinal);
        ImGui.PushID(idStart >= 0 ? label[idStart..] : label);
        if (textEnd >= 0) label = label[..textEnd];
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var track = PluginUi.SwitchSize;
        var height = MathF.Round(MathF.Max(Em * 2f, track.Y + Em * 0.5f));
        var pressed = ImGui.InvisibleButton("##row", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (pressed) value = !value;
        var drawList = ImGui.GetWindowDrawList();
        if (hovered) drawList.AddRectFilled(start, start + new Vector2(width, height), U32(Tone(Text, 0.05f)), height / 2);
        drawList.AddText(new Vector2(start.X + Em * 0.8f, start.Y + (height - ImGui.GetTextLineHeight()) / 2), U32(value ? Text : Muted), label);
        PluginUi.DrawSwitch(drawList, ImGui.GetID("##row"), new Vector2(start.X + width - track.X - Em * 0.5f, start.Y + (height - track.Y) / 2), value, hovered);
        Advance(start.X, start.Y + height, width);
        ImGui.PopID();
        return pressed;
    }

    /// <summary>Banner with a leading icon, wrapped message and an optional action button on the right.</summary>
    public static bool Banner(string id, FontAwesomeIcon icon, Vector4 color, string message, string? action = null)
    {
        var pressed = false;
        BeginCard(id, color == Warning ? CardKind.Warning : CardKind.Filled, padding: Em * 0.7f);
        var right = RightEdge();
        var actionWidth = action != null ? ButtonWidth(action) + Em * 0.5f : 0f;
        Icon(icon, color);
        ImGui.SameLine(0, Em * 0.6f);
        var textLeft = ImGui.GetCursorScreenPos().X;
        ImGui.PushTextWrapPos(LocalX(right - actionWidth));
        ImGui.PushStyleColor(ImGuiCol.Text, Text);
        ImGui.TextWrapped(message);
        ImGui.PopStyleColor();
        ImGui.PopTextWrapPos();
        if (action != null)
        {
            var textTop = ImGui.GetItemRectMin().Y;
            ImGui.SetCursorScreenPos(new Vector2(right - actionWidth + Em * 0.5f, textTop - (ImGui.GetFrameHeight() - ImGui.GetTextLineHeight()) / 2));
            pressed = color == Warning ? WarningButton(action) : TextButton(action);
        }
        _ = textLeft;
        EndCard();
        return pressed;
    }

    /// <summary>Equal-width statistic tiles: big value over a small caption.</summary>
    public static void Stats(string id, IReadOnlyList<(string Value, string Caption, Vector4? Color)> stats)
    {
        var width = RightEdge() - ImGui.GetCursorScreenPos().X;
        if (!ImGui.BeginTable(id, stats.Count, ImGuiTableFlags.SizingStretchSame, new Vector2(width, 0))) return;
        foreach (var (value, caption, color) in stats)
        {
            ImGui.TableNextColumn();
            BigText(value, FontRole.Heading, color ?? Text);
            ImGui.TextColored(Muted, caption);
        }
        ImGui.EndTable();
    }

    /// <summary>Clickable whole-card test: true when the card was clicked outside any inner widget.</summary>
    public static bool CardClicked((Vector2 Min, Vector2 Max) rect)
        => ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rect.Min, rect.Max)
           && !ImGui.IsAnyItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left);

    public static bool CardHovered((Vector2 Min, Vector2 Max) rect)
        => ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
}
