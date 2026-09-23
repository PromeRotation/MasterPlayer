namespace BossMod;

[ConfigDisplay(Name = "全副本自动化", Order = 6)]
public sealed class ZoneModuleConfig : ConfigNode
{
    [PropertyDisplay("加载区域模块所需的最低完成度")]
    public BossModuleInfo.Maturity MinMaturity = BossModuleInfo.Maturity.Contributed;

    [PropertyDisplay("自动执行任务战斗和单人任务")]
    public bool EnableQuestBattles = false;

    [PropertyDisplay("在游戏世界中绘制路径点")]
    public bool ShowWaypoints = false;

    [PropertyDisplay("寻路时使用位移技能（重影步、回避跳跃等）")]
    public bool UseDash = true;

    [PropertyDisplay("锁定区域模块窗口的位置及鼠标交互")]
    public bool Lock = false;

    [PropertyDisplay("使区域模块窗口透明", tooltip: "移除区域模块窗口周围的黑色背景；如果将雷达移到其他显示器，此功能可能失效。")]
    public bool TransparentMode = false;
}
