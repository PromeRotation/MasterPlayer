using System.Numerics;
using Dalamud.Bindings.ImGui;
using PromeRotation.UI.Components;

namespace MasterPlayer.Windows;

internal static class PluginUi
{
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
            return PromeUi.ToggleRow(id, label, description, ref value);
        var changed = PromeUi.ToggleRow(id, label, "", ref value);
        Hint(description);
        return changed;
    }

    public static void Section(string title)
    {
        ImGui.Spacing();
        ImGui.TextColored(PromeUiTheme.Accent, title);
        ImGui.Separator();
    }

    public static bool Slider(string label, ref float value, float minimum, float maximum, string format)
    {
        ImGui.TextUnformatted(label);
        ImGui.SetNextItemWidth(-1);
        return ImGui.SliderFloat("##" + label, ref value, minimum, maximum, format);
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
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        var rgb = new Vector3(color.X, color.Y, color.Z);
        var changed = ImGui.ColorEdit3("##color", ref rgb, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel);
        if (changed) { color.X = rgb.X; color.Y = rgb.Y; color.Z = rgb.Z; }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1);
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
