using System.Numerics;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using MasterPlayer.Settings;

namespace MasterPlayer.Radar;

/// <summary>
/// 雷达悬浮窗：随场地轮廓显示的 2D 俯视小地图（原生 ImGui 窗口，UiBuilder.Draw 主线程驱动）。
/// 游戏内 3D 范围提示由 RadarRenderer（真实世界绘制）负责，本窗口才是「雷达」。
/// 内容渲染由 RadarView 完成（回放窗口内嵌同一组件）；本类只管显示门槛、窗口壳与交互。
/// 交互：解锁状态下拖拽移动、滚轮缩放、右键快捷菜单；锁定后鼠标穿透。
/// </summary>
public sealed class RadarOverlayWindow : IDisposable
{
    private readonly RadarView _view = new();
    private bool _disposed;
    private bool _hovered;
    private DateTime _lastErrorLogUtc;
    private DateTime? _pendingSaveUtc;

    public void Draw()
    {
        var settings = MasterPlayerSettings.Instance;
        if (_disposed || !settings.EnableRadarOverlay ||
            (settings.RadarOnlyWithScript && !HasZoneScript()))
        {
            return;
        }

        try
        {
            DrawInternal();
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastErrorLogUtc).TotalSeconds >= 5d)
            {
                _lastErrorLogUtc = now;
                Svc.Log.Error(ex, "[MasterPlayer] RadarOverlayWindow.Draw 失败。");
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (_pendingSaveUtc.HasValue)
        {
            _pendingSaveUtc = null;
            try
            {
                MasterPlayerSettings.Instance.Save();
            }
            catch
            {
            }
        }
    }

    private void DrawInternal()
    {
        var settings = MasterPlayerSettings.Instance;

        using var theme = PromeRotation.UI.Components.PromeUiTheme.PushWindowStyle();
        var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoFocusOnAppearing;
        if (settings.RadarOverlayLock)
        {
            flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs;
        }

        var viewport = RadarViewportLayout.Create(settings.RadarScale, ImGui.GetFontSize(), ImGui.GetIO().DisplaySize);
        var fullSize = viewport.FullSize;
        var bridge = MasterPlayerPlugin.Instance?.MechanicHost;
        var timelineRows = settings.ShowModuleTimeline && bridge?.CurrentTimeline is { } timeline
            ? MechanicHost.MechanicTimelineView.BuildRows(timeline, bridge.MechanicClockUtc)
            : [];
        var rowHeight = ImGui.GetFontSize() + 4f;
        var stripHeight = timelineRows.Count == 0 ? 0f : timelineRows.Count * rowHeight + 6f;
        ImGui.SetNextWindowSize(new Vector2(fullSize, fullSize + stripHeight), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (!ImGui.Begin("MasterPlayer 雷达", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        try
        {
            var drawList = ImGui.GetWindowDrawList();
            var cursor = ImGui.GetCursorScreenPos();
            var screenCenter = cursor + new Vector2(fullSize / 2);
            ImGui.InvisibleButton("##radar_hit", new Vector2(fullSize, fullSize + stripHeight));
            HandleInteraction(settings, !settings.RadarOverlayLock);
            _view.Draw(drawList, screenCenter, viewport.Radius, _hovered);
            if (stripHeight > 0f)
                DrawTimelineStrip(drawList, cursor, fullSize, timelineRows, rowHeight, settings);
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
        }

        if (_pendingSaveUtc is DateTime pending && DateTime.UtcNow >= pending)
        {
            _pendingSaveUtc = null;
            settings.Save();
        }
    }

    private void HandleInteraction(MasterPlayerSettings settings, bool interactive)
    {
        _hovered = interactive && ImGui.IsItemHovered();
        if (!interactive)
        {
            return;
        }

        if (_hovered)
        {
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f)
            {
                settings.RadarScale = Math.Clamp(settings.RadarScale + wheel * 0.1f, 0.3f, 3f);
                _pendingSaveUtc = DateTime.UtcNow.AddSeconds(0.8);
            }
        }

        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        }

        if (ImGui.BeginPopupContextItem("##radar_menu"))
        {
            var changed = false;
            var lockOverlay = settings.RadarOverlayLock;
            if (ImGui.Checkbox("锁定位置（鼠标穿透）", ref lockOverlay))
            {
                settings.RadarOverlayLock = lockOverlay;
                changed = true;
            }

            var onlyWithScript = settings.RadarOnlyWithScript;
            if (ImGui.Checkbox("仅在有脚本的区域显示", ref onlyWithScript))
            {
                settings.RadarOnlyWithScript = onlyWithScript;
                changed = true;
            }

            var opacity = settings.RadarBackgroundOpacity * 100;
            if (ImGui.SliderFloat("背景不透明度", ref opacity, 0f, 100f, "%.0f%%"))
            {
                settings.RadarBackgroundOpacity = opacity / 100;
                changed = true;
            }

            var compass = settings.RadarShowCompass;
            if (ImGui.Checkbox("显示罗盘", ref compass))
            {
                settings.RadarShowCompass = compass;
                changed = true;
            }

            var rings = settings.RadarShowRangeRings;
            if (ImGui.Checkbox("显示距离环", ref rings))
            {
                settings.RadarShowRangeRings = rings;
                changed = true;
            }

            var pulse = settings.RadarPulseDanger;
            if (ImGui.Checkbox("危险区临期脉冲", ref pulse))
            {
                settings.RadarPulseDanger = pulse;
                changed = true;
            }

            var showTimeline = settings.ShowModuleTimeline;
            if (ImGui.Checkbox("显示机制时间轴", ref showTimeline))
            {
                settings.ShowModuleTimeline = showTimeline;
                changed = true;
            }

            ImGui.Separator();
            if (ImGui.MenuItem("隐藏雷达悬浮窗"))
            {
                settings.EnableRadarOverlay = false;
                changed = true;
            }

            if (changed)
            {
                settings.Save();
            }

            ImGui.EndPopup();
        }
    }

    private static void DrawTimelineStrip(ImDrawListPtr drawList, Vector2 origin, float radarSize,
        IReadOnlyList<MechanicHost.MechanicTimelineRow> rows, float rowHeight, MasterPlayerSettings settings)
    {
        var top = origin.Y + radarSize;
        var background = new Vector4(0.045f, 0.065f, 0.115f, Math.Clamp(settings.RadarBackgroundOpacity, 0f, 1f));
        drawList.AddRectFilled(new Vector2(origin.X, top), new Vector2(origin.X + radarSize, top + rows.Count * rowHeight + 6f),
            ImGui.GetColorU32(background), 6f, ImDrawFlags.RoundCornersBottom);
        var shadow = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.6f));
        var y = top + 3f;
        foreach (var row in rows)
        {
            var color = ImGui.GetColorU32(ToneColor(row.Tone, row.IsCurrent, row.IsBranch));
            var name = row.IsCurrent ? $"▶ {row.Name}" : row.Name;
            var textPos = new Vector2(origin.X + 8f, y + 1f);
            drawList.AddText(textPos + new Vector2(1f, 1f), shadow, name);
            drawList.AddText(textPos, color, name);
            if (!row.IsBranch)
            {
                var seconds = $"{row.Seconds:F1}";
                var size = ImGui.CalcTextSize(seconds);
                var timePos = new Vector2(origin.X + radarSize - 8f - size.X, y + 1f);
                drawList.AddText(timePos + new Vector2(1f, 1f), shadow, seconds);
                drawList.AddText(timePos, color, seconds);
            }
            y += rowHeight;
        }
    }

    private static Vector4 ToneColor(MechanicHost.MechanicTimelineTone tone, bool isCurrent, bool isBranch)
    {
        if (isBranch)
            return PromeRotation.UI.Components.PromeUiTheme.TextMuted;
        var color = tone switch
        {
            MechanicHost.MechanicTimelineTone.Tankbuster => PromeRotation.UI.Components.PromeUiTheme.Warning,
            MechanicHost.MechanicTimelineTone.Raidwide => PromeRotation.UI.Components.PromeUiTheme.Danger,
            MechanicHost.MechanicTimelineTone.Knockback => PromeRotation.UI.Components.PromeUiTheme.Accent,
            MechanicHost.MechanicTimelineTone.Attention => PromeRotation.UI.Components.PromeUiTheme.Warning,
            _ => new Vector4(1f, 1f, 1f, 0.92f),
        };
        if (!isCurrent)
            color.W *= 0.85f;
        return color;
    }

    private static bool HasZoneScript()
    {
        var scripts = MasterPlayerPlugin.Instance?.Scripts;
        if (scripts == null)
        {
            return false;
        }

        foreach (var running in scripts.Running.Values)
        {
            if (running.Descriptor.Metadata.Territorys.Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
