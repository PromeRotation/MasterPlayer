using System.Numerics;
using Dalamud.Bindings.ImGui;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

internal static class PluginUi
{
    private static readonly Func<string, string?, string?, string?, float, bool, float, bool>? HostPageHeader =
        typeof(PromeUi).GetMethod("PageHeader", [typeof(string), typeof(string), typeof(string), typeof(string), typeof(float), typeof(bool), typeof(float)])
            ?.CreateDelegate<Func<string, string?, string?, string?, float, bool, float, bool>>();
    private static readonly Func<string, IReadOnlyList<string>, int, int>? HostTabBar =
        typeof(PromeUi).GetMethod("TabBar", [typeof(string), typeof(IReadOnlyList<string>), typeof(int)])
            ?.CreateDelegate<Func<string, IReadOnlyList<string>, int, int>>();
    // SectionHeader gained optional parameters in the modern theme (3 → 5).
    // A direct call binds one exact signature and the JIT throws
    // MissingMethodException for the whole caller on the other PR build.
    private static readonly System.Reflection.MethodInfo? HostSectionHeader =
        typeof(PromeUi).GetMethods().Where(m => m.Name == "SectionHeader" && m.GetParameters() is [{ ParameterType: var first }, ..] && first == typeof(string)
            && m.GetParameters().Skip(1).All(p => p.HasDefaultValue)).MinBy(m => m.GetParameters().Length);
    private static readonly System.Reflection.MethodInfo? HostToggleRow =
        typeof(PromeUi).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(m => m.Name == "ToggleRow" && m.ReturnType == typeof(bool)
                && m.GetParameters() is [{ ParameterType: var id }, { ParameterType: var label }, { ParameterType: var description }, { ParameterType: var value }, ..]
                && id == typeof(string) && label == typeof(string) && description == typeof(string) && value == typeof(bool).MakeByRefType()
                && m.GetParameters().Skip(4).All(p => p.HasDefaultValue))
            .MinBy(m => m.GetParameters().Length);

    public static void PageHeader(string title)
    {
        if (HostPageHeader != null) HostPageHeader(title, null, null, null, 132f, true, 0f);
        else if (HostSectionHeader != null)
            HostSectionHeader.Invoke(null, [title, .. HostSectionHeader.GetParameters().Skip(1).Select(p => p.DefaultValue)]);
        else Section(title);
    }

    public static int TabBar(string id, IReadOnlyList<string> labels, int active)
    {
        if (HostTabBar != null) return HostTabBar(id, labels, active);
        for (var i = 0; i < labels.Count; i++)
        {
            if (i > 0) ImGui.SameLine(0f, 6f);
            if (PromeUi.TabButton($"{id}_{i}", labels[i], i == active)) active = i;
        }
        return active;
    }

    public static void Hint(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, PromeUiTheme.TextMuted);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    public static bool Toggle(string id, string label, string description, ref bool value)
    {
        // PR's native row does not wrap its description; keep it outside the
        // row when a narrow pane or enlarged font would overlap the switch.
        if (ImGui.CalcTextSize(description).X <= ImGui.GetContentRegionAvail().X - 52)
            return ToggleRow(id, label, description, ref value);
        var changed = ToggleRow(id, label, "", ref value);
        Hint(description);
        return changed;
    }

    private static bool ToggleRow(string id, string label, string description, ref bool value)
    {
        if (HostToggleRow == null)
        {
            ImGui.PushID(id);
            var changed = ImGui.Checkbox(label, ref value);
            if (!string.IsNullOrEmpty(description)) Hint(description);
            ImGui.PopID();
            return changed;
        }

        object?[] arguments = [id, label, description, value, .. HostToggleRow.GetParameters().Skip(4).Select(p => p.DefaultValue)];
        var result = (bool)HostToggleRow.Invoke(null, arguments)!;
        value = (bool)arguments[3]!;
        return result;
    }

    public static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(PromeUiTheme.Accent, title);
        ImGui.Separator();
    }

    // Material switch used in place of checkboxes; text after "##" is the id only, as in ImGui.
    public static bool Switch(string label, ref bool value)
    {
        var split = label.IndexOf("##", StringComparison.Ordinal);
        var text = split >= 0 ? label[..split] : label;
        var fontSize = ImGui.GetFontSize();
        var track = SwitchSize;
        var height = MathF.Max(ImGui.GetFrameHeight(), track.Y + 2);
        var gap = fontSize * 0.45f;
        var textWidth = text.Length == 0 ? 0 : ImGui.CalcTextSize(text).X + gap;
        var buttonId = label.Length == 0 ? "##switch" : label;
        var animationId = ImGui.GetID(buttonId);
        var pressed = ImGui.InvisibleButton(buttonId, new Vector2(track.X + textWidth + 2, height));
        if (pressed) value = !value;
        var min = ImGui.GetItemRectMin();
        var drawList = ImGui.GetWindowDrawList();
        DrawSwitch(drawList, animationId, new Vector2(min.X + 1, min.Y + (height - track.Y) / 2), value, ImGui.IsItemHovered());
        if (text.Length != 0)
            drawList.AddText(new Vector2(min.X + 1 + track.X + gap, min.Y + (height - fontSize) / 2), ImGui.GetColorU32(PromeUiTheme.Text), text);
        return pressed;
    }

    /// <summary>Track size: about one text line tall (36×20 at the default font), 1.8:1.</summary>
    public static Vector2 SwitchSize
    {
        get
        {
            var trackHeight = MathF.Round(MathF.Max(20f, ImGui.GetFontSize() * 1.1f));
            return new Vector2(MathF.Round(trackHeight * 1.8f), trackHeight);
        }
    }

    private static readonly Dictionary<uint, float> SwitchProgress = [];

    // Material 3 switch: off = outlined track with a small muted thumb on the left; on = accent
    // track with a larger white thumb on the right. The thumb slides and grows over ~0.12 s.
    public static void DrawSwitch(ImDrawListPtr drawList, uint id, Vector2 topLeft, bool on, bool hovered, bool enabled = true)
    {
        var size = SwitchSize;
        var target = on ? 1f : 0f;
        if (!SwitchProgress.TryGetValue(id, out var t)) t = target;
        var step = ImGui.GetIO().DeltaTime / 0.12f;
        t = t < target ? MathF.Min(target, t + step) : MathF.Max(target, t - step);
        SwitchProgress[id] = t;

        var alpha = enabled ? 1f : 0.38f;
        var radius = size.Y / 2;
        var muted = PromeUiTheme.TextMuted;
        var accent = PromeUiTheme.Accent;
        var offTrack = muted with { W = (hovered ? 0.22f : 0.12f) * alpha };
        var onTrack = accent with { W = (hovered ? 0.88f : 1f) * alpha };
        drawList.AddRectFilled(topLeft, topLeft + size, ImGui.GetColorU32(Vector4.Lerp(offTrack, onTrack, t)), radius);
        if (t < 1f)
            drawList.AddRect(topLeft, topLeft + size, ImGui.GetColorU32(muted with { W = (1f - t) * 0.9f * alpha }), radius, ImDrawFlags.None, MathF.Max(1.5f, size.Y * 0.09f));

        var border = size.Y * 0.12f;
        var thumbRadius = radius * (0.48f + 0.3f * t);
        var thumbX = topLeft.X + radius + (size.X - size.Y) * t;
        var thumbColor = Vector4.Lerp(muted with { W = alpha }, new Vector4(1, 1, 1, alpha), t);
        drawList.AddCircleFilled(new Vector2(thumbX, topLeft.Y + radius), MathF.Min(thumbRadius, radius - border), ImGui.GetColorU32(thumbColor));
    }

    // Label and value on one row; the slider keeps a readable width instead of spanning the window.
    public static bool Slider(string label, ref float value, float minimum, float maximum, string format)
    {
        LabelColumn(label);
        return ImGui.SliderFloat("##" + label, ref value, minimum, maximum, format);
    }

    public static void LabelColumn(string label)
    {
        var fontSize = ImGui.GetFontSize();
        var labelWidth = MathF.Max(fontSize * 7, ImGui.CalcTextSize(label).X + fontSize * 0.6f);
        var start = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(start + labelWidth);
        ImGui.SetNextItemWidth(MathF.Min(ImGui.GetContentRegionAvail().X, fontSize * 16));
    }

    public static bool Opacity(string label, ref float alpha)
    {
        var percent = Math.Clamp(alpha * 100, 0, 100);
        if (!Slider(label, ref percent, 0, 100, "%.0f%%")) return false;
        alpha = percent / 100;
        return true;
    }

    public static bool ColorOpacity(string label, ref Vector4 color)
    {
        ImGui.PushID(label);
        LabelColumn(label);
        var rgb = new Vector3(color.X, color.Y, color.Z);
        var changed = ImGui.ColorEdit3("##color", ref rgb, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
        if (changed) { color.X = rgb.X; color.Y = rgb.Y; color.Z = rgb.Z; }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(MathF.Min(ImGui.GetContentRegionAvail().X, ImGui.GetFontSize() * 14));
        var percent = Math.Clamp(color.W * 100, 0, 100);
        if (ImGui.SliderFloat("##opacity", ref percent, 0, 100, "不透明度 %.0f%%"))
        {
            color.W = percent / 100;
            changed = true;
        }
        ImGui.PopID();
        return changed;
    }
}
