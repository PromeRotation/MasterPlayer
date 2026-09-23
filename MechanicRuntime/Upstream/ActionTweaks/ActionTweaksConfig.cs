using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace BossMod;

[ConfigDisplay(Name = "技能调整", Order = 4)]
public sealed class ActionTweaksConfig : ConfigNode
{
    // TODO: consider exposing max-delay to config; 0 would mean 'remove all delay', max-value would mean 'disable'
    [PropertyDisplay("移除瞬发技能因延迟产生的额外动画锁（请阅读提示）", tooltip: "请勿与 XivAlexander 或 NoClippy 同时使用。检测到这些插件时本功能应自动关闭，但请务必自行确认。")]
    public bool RemoveAnimationLockDelay = false;

    [PropertyDisplay("动画锁最大模拟延迟（请阅读提示）", tooltip: "设置移除动画锁延迟时使用的最大模拟延迟，单位为毫秒。此值不可设为 0。设为 20ms 时自动循环可能三插；避免三插的最低值为 26ms。FFLogs 已接受最低 20ms，通常不会影响日志。")]
    [PropertySlider(20, 50, Speed = 0.1f)]
    public int AnimationLockDelayMax = 20;

    [PropertyDisplay("移除帧率导致的额外冷却延迟", tooltip: "动态调整冷却和动画锁，使队列中的技能不受帧率限制并立即结算。")]
    public bool RemoveCooldownDelay = false;

    [PropertyDisplay("读条时禁止移动", tags: ["slidecast"])]
    public bool PreventMovingWhileCasting = false;

    public enum ModifierKey
    {
        [PropertyDisplay("无")]
        None,
        [PropertyDisplay("Ctrl")]
        Ctrl,
        [PropertyDisplay("Alt")]
        Alt,
        [PropertyDisplay("Shift")]
        Shift,
        [PropertyDisplay("鼠标左键 + 右键")]
        M12
    }

    [PropertyDisplay("按住此键可在读条时移动", tooltip: "需要同时启用上方设置。", tags: ["slidecast"])]
    public ModifierKey MoveEscapeHatch = ModifierKey.None;

    [PropertyDisplay("目标死亡时自动取消读条")]
    public bool CancelCastOnDeadTarget = false;

    [PropertyDisplay("热病类机制即将判定时禁止移动和使用技能（设为 0 关闭；可根据延迟提高阈值）")]
    [PropertySlider(0, 10, Speed = 0.01f)]
    public float PyreticThreshold = 1.0f;

    [PropertyDisplay("自动处理方向混乱：偏转后的移动方向与正常方向夹角超过阈值时禁止移动（设为 180 关闭）")]
    [PropertySlider(0, 180)]
    public float MisdirectionThreshold = 180f;

    [PropertyDisplay("使用技能后恢复角色朝向")]
    public bool RestoreRotation = false;

    [PropertyDisplay("对鼠标悬停目标使用技能")]
    public bool PreferMouseover = false;

    public bool SmartTargeting = false;

    [PropertyDisplay("手动按下的技能使用自定义队列", tooltip: "改善与自动循环的配合，避免自动循环运行时手动按下治疗技能导致三插或 GCD 漂移。")]
    public bool UseManualQueue = false;

    [PropertyDisplay("尽量避免位移进入 AOE", tooltip: "如果指定目标的位移技能（例如战士的猛攻）会进入危险区域，则阻止自动使用。没有对应模块的副本中可能无法正常工作。\n\n启用“手动按下的技能使用自定义队列”后，此选项也会作用于手动使用的位移技能。")]
    public bool DashSafety = true;

    [PropertyDisplay("应用于所有位移，而不只是突进技能", tooltip: "包括后撤（如武士的夜天）、传送（如忍者的缩地）和固定距离位移（如龙骑士的回避跳跃）。")]
    public bool DashSafetyExtra = true;

    [PropertyDisplay("自动管理自动攻击", tooltip: "避免倒计时期间提前开始自动攻击，并在开怪、切换目标或使用不会明确取消自动攻击的技能时自动开始攻击。")]
    public bool AutoAutos = false;

    [PropertyDisplay("使用技能时自动下坐骑")]
    public bool AutoDismount = true;

    public enum GroundTargetingMode
    {
        [PropertyDisplay("再次点击以手动选择位置（游戏默认行为）")]
        Manual,

        [PropertyDisplay("在当前鼠标位置施放")]
        AtCursor,

        [PropertyDisplay("在选中目标的位置施放")]
        AtTarget
    }
    [PropertyDisplay("地面指定技能的自动目标选择")]
    public GroundTargetingMode GTMode = GroundTargetingMode.Manual;

    public bool ActivateAnticheat = true;

    private static bool IsRSREnabled()
    {
        try
        {
            const string rsrName = "Rotation Solver Reborn";
            foreach (var p in Service.PluginInterface.InstalledPlugins)
            {
                if ((p.Name.Equals(rsrName, StringComparison.OrdinalIgnoreCase) || p.InternalName.Equals(rsrName, StringComparison.OrdinalIgnoreCase)) && p.IsLoaded)
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    public override void DrawCustom(UITree tree, WorldState ws)
    {
        ImGui.AlignTextToFramePadding();
        UIMisc.HelpMarker("如果常用目标（鼠标悬停或当前目标）不适用于该技能，则自动选择下一个最合适的目标（例如退避自动选择另一名坦克）。");
        ImGui.SameLine();
        var rsrEnabled = IsRSREnabled();
        using var color = ImRaii.PushColor(ImGuiCol.Text, 0xFF0000FFu, rsrEnabled);
        if (ImGui.Checkbox("智能技能目标选择（请勿与 RSR 同时使用）", ref SmartTargeting))
        {
            Modified.Fire();
        }
    }
}
