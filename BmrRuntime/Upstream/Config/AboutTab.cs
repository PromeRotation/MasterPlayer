using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using System.Diagnostics;
using System.IO;

namespace BossMod;

public sealed class AboutTab(DirectoryInfo? replayDir)
{
    private static readonly Color TitleColor = Color.FromComponents(255u, 165u, default);
    private static readonly Color SectionBgColor = Color.FromComponents(38u, 38u, 38u);
    private static readonly Color BorderColor = Color.FromComponents(178u, 178u, 178u, 204u);
    private static readonly Color DiscordColor = Color.FromComponents(88u, 101u, 242u);

    private string _lastErrorMessage = "";

    public void Draw()
    {
        using var wrap = ImRaii.TextWrapPos(0);

        ImGui.TextUnformatted("BossModReborn（BMR）提供 Boss 战斗雷达、自动循环、冷却规划和 AI 功能。所有模块均可单独开关。如需支持，请前往本标签页底部链接的 Discord 服务器。");
        ImGui.TextUnformatted("这是原始 BossMod（VBM）的分支版本。请在 Combat Reborn Discord 寻求支持。");
        ImGui.TextUnformatted("请确保不要同时加载 VBM 和此分支，同时使用两者的后果未知且不受支持。");
        ImGui.Spacing();
        DrawSection("雷达",
        [
            "提供屏幕上的窗口，显示区域小地图，包含玩家位置、Boss位置、各种即将到来的AOE和其他机制。",
            "实用之处在于你不需要记住技能名称的含义。",
            "精确显示你是否会吃到即将到来的AOE。",
            "对支持的Boss可用，可在\"支持的战斗\"标签页中查看。",
        ]);
        ImGui.Spacing();
        DrawSection("自动循环",
        [
            "尽可能执行完全优化的循环。",
            "前往\"自动循环预设\"标签页创建预设。",
            "各循环模块的成熟度可在提示中查看。",
            "使用此功能请参考 Wiki 上的指南。",
        ]);
        ImGui.Spacing();
        DrawSection("冷却规划",
        [
            "为支持的Boss创建冷却规划。",
            "在特定战斗中替代自动循环。",
            "允许你为特定技能安排在特定时间施放。",
            "使用此功能请参考 Wiki 上的指南。",
        ]);
        ImGui.Spacing();
        DrawSection("AI",
        [
            "在Boss战斗中自动化移动。",
            "根据Boss模块确定的安全区域（在雷达上可见）自动移动你的角色。",
            "不应在与陌生人组队时使用。",
            "可被其他插件调用以实现全副本自动化。",
        ]);
        ImGui.Spacing();
        DrawSection("回放",
        [
            "用于创建Boss模块、分析问题以及制作冷却规划。",
            "寻求帮助时，请务必提供回放文件！请注意回放会包含你的角色名称！",
            "在 设置 > 显示回放管理界面 中启用（或启用自动录制）。",
            $"回放文件位于 '{replayDir}'。",
        ]);
        ImGui.Spacing();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Button, DiscordColor.ABGR))
        {
            if (ImGui.Button("Combat Reborn Discord", new(220, 0)))
            {
                _lastErrorMessage = OpenLink("https://discord.gg/p54TZMPnC9");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("BossModReborn GitHub", new(220, 0)))
        {
            _lastErrorMessage = OpenLink("https://github.com/FFXIV-CombatReborn/BossmodReborn");
        }

        ImGui.SameLine();
        if (ImGui.Button("BossMod Wiki", new(130, 0)))
        {
            _lastErrorMessage = OpenLink("https://github.com/awgil/ffxiv_bossmod/wiki");
        }

        ImGui.SameLine();
        if (ImGui.Button("Open replay folder", new(180, 0)) && replayDir != null)
        {
            _lastErrorMessage = OpenDirectory(replayDir);
        }

        if (_lastErrorMessage.Length > 0)
        {
            using var color = ImRaii.PushColor(ImGuiCol.Text, Colors.TextColor3);
            ImGui.TextUnformatted(_lastErrorMessage);
        }
    }

    private static void DrawSection(string title, string[] bulletPoints)
    {
        using var colorBackground = ImRaii.PushColor(ImGuiCol.ChildBg, SectionBgColor.ABGR);
        using var colorBorder = ImRaii.PushColor(ImGuiCol.Border, BorderColor.ABGR);
        var height = ImGui.GetTextLineHeightWithSpacing() * (bulletPoints.Length + 2);
        using var section = ImRaii.Child(title, new(0, height), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding);

        if (!section)
        {
            return;
        }

        using (ImRaii.PushColor(ImGuiCol.Text, TitleColor.ABGR))
        {
            ImGui.TextUnformatted(title);
        }

        ImGui.Separator();
        ImGui.PushTextWrapPos();
        foreach (var point in bulletPoints)
        {
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.TextUnformatted(point);
        }
        ImGui.PopTextWrapPos();
    }

    private static string OpenLink(string link)
    {
        try
        {
            Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            return "";
        }
        catch (Exception e)
        {
            Service.Log($"Error opening link {link}: {e}");
            return $"无法打开链接 '{link}'，请在浏览器中手动打开。";
        }
    }

    private static string OpenDirectory(DirectoryInfo dir)
    {
        if (!dir.Exists)
        {
            return $"目录 '{dir}' 未找到。";
        }

        try
        {
            Process.Start(new ProcessStartInfo(dir.FullName) { UseShellExecute = true });
            return "";
        }
        catch (Exception e)
        {
            Service.Log($"Error opening directory {dir}: {e}");
            return $"无法打开文件夹 '{dir}'，请手动打开。";
        }
    }
}
