using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using MasterPlayer.Scripting;
using MasterPlayer.Spatial.Geometry;

namespace MasterPlayer.Mechanics.Enuo;

internal sealed class EnuoDebugDisplay(ScriptSafeZoneApi safe, ScriptDrawApi draw, Action close) : IDisposable
{
    private readonly HashSet<string> _ids = [];
    private EnuoDebugFrame? _frame;
    private bool _subscribed, _replay;
    private object? _ui;
    private EventInfo? _drawEvent;

    public void Update(EnuoDebugFrame? frame, bool replay)
    {
        _frame = frame; _replay = replay;
        if (frame != null && !_subscribed)
        {
            // The installed ScriptCompiler does not reference System.ComponentModel,
            // inherited by IUiBuilder. Resolve the same Draw event without changing MP.
            var property = typeof(IDalamudPluginInterface).GetProperty("UiBuilder")!;
            _ui = property.GetValue(Svc.PluginInterface);
            _drawEvent = property.PropertyType.GetEvent("Draw")!;
            _drawEvent.AddEventHandler(_ui, (Action)Draw); _subscribed = true;
        }
        if (frame == null && _subscribed)
        {
            _drawEvent!.RemoveEventHandler(_ui, (Action)Draw); _subscribed = false;
            _ui = null; _drawEvent = null;
        }
        var wanted = new HashSet<string>();
        using var group = safe.BeginDrawingGroup("恩欧 / 调试：全队目标（仅显示）");
        foreach (var marker in frame?.Markers ?? [])
        {
            var id = "debug/party/" + marker.FirstRole;
            // Pure preview, never a registered navigation destination.
            draw.AddAoe(id, new CircleGeometry(marker.Position, .65f), true);
            wanted.Add(id);
        }
        foreach (var id in _ids.Where(id => !wanted.Contains(id))) draw.Remove(id);
        _ids.Clear(); _ids.UnionWith(wanted);
    }

    private void Draw()
    {
        if (_frame is not { } frame) return;
        var open = true;
        ImGui.SetNextWindowSize(new(480, 720), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new(400, 520), new(900, 1200));
        if (ImGui.Begin("恩欧 · 全队目标（调试）###MasterPlayer_EnuoPartyDebug", ref open))
        {
            ImGui.TextUnformatted("空心圈 = 目标；实心点 = 当前位置；固定上北下南");
            var side = Math.Clamp(ImGui.GetContentRegionAvail().X, 240, 440);
            var origin = ImGui.GetCursorScreenPos(); var center = origin + new Vector2(side / 2);
            var scale = (side / 2 - 30) / frame.ArenaRadius;
            Vector2 Project(Vector3 p) => center + new Vector2(p.X - 100, p.Z - 100) * scale;
            var canvas = ImGui.GetWindowDrawList();
            canvas.AddCircleFilled(center, frame.ArenaRadius * scale, 0xE02A2522, 96);
            canvas.AddCircle(center, frame.ArenaRadius * scale, 0xFF8E969F, 96, 1.5f);
            Label(canvas, center + new Vector2(0, -frame.ArenaRadius * scale - 16), "A / 北", 0xFFFFFFFF);
            Label(canvas, center + new Vector2(frame.ArenaRadius * scale + 14, 0), "B", 0xFFFFFFFF);
            Label(canvas, center + new Vector2(0, frame.ArenaRadius * scale + 14), "C", 0xFFFFFFFF);
            Label(canvas, center + new Vector2(-frame.ArenaRadius * scale - 14, 0), "D", 0xFFFFFFFF);
            foreach (var target in frame.Targets)
            {
                if (target.Position is not { } player) continue;
                var color = Color(target.Role);
                canvas.AddCircleFilled(Project(player), 3, color);
                if (target.Goal != null) canvas.AddLine(Project(player), Project(target.Goal.Position), color & 0x66FFFFFF, 1);
            }
            foreach (var marker in frame.Markers)
            {
                var point = Project(marker.Position);
                canvas.AddCircle(point, 6, Color(marker.FirstRole), 24, 2);
                Label(canvas, point + new Vector2(0, -16), marker.Label, Color(marker.FirstRole));
            }
            ImGui.Dummy(new(side, side));
            ImGui.Separator();
            foreach (var target in frame.Targets)
            {
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(Color(target.Role)), EnuoStrategy.Roles[target.Role]);
                ImGui.SameLine(); ImGui.TextUnformatted(target.State);
                if (ImGui.IsItemHovered() && target.Goal is { } goal)
                    ImGui.SetTooltip($"X {goal.Position.X:F2} / Z {goal.Position.Z:F2}");
            }
        }
        ImGui.End();
        if (!open) { close(); Update(null, false); return; }
        // Recorded coordinates belong to the replay map, never the current live scene.
        if (_replay) return;
        var overlay = ImGui.GetBackgroundDrawList();
        foreach (var marker in frame.Markers)
            if (Svc.GameGui.WorldToScreen(marker.Position, out var screen))
                Label(overlay, new(screen.X, screen.Y - 16), marker.Label, Color(marker.FirstRole));
    }

    private static uint Color(int role) => role < 2 ? 0xFFFFBB66u : role < 4 ? 0xFF88DE88u : 0xFF809FFFu;
    private static void Label(ImDrawListPtr drawList, Vector2 center, string text, uint color)
    {
        var p = center - ImGui.CalcTextSize(text) / 2;
        drawList.AddText(p + Vector2.One, 0xFF000000, text);
        drawList.AddText(p, color, text);
    }
    public void Dispose() => Update(null, false);
}
