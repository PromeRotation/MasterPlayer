using Dalamud.Bindings.ImGui;

namespace BossMod;

[ConfigDisplay(Name = "首领模块与雷达", Order = 1)]
public sealed class BossModuleConfig : ConfigNode
{
    public override void DrawCustom(UITree tree, WorldState ws)
    {
        if (ImGui.Button("窗口重新居中"))
        {
            Service.BossModWindow?.RecenterWindow();
        }
    }

    // boss module settings
    [PropertyDisplay("加载模块所需的最低完成度", tooltip: "部分模块处于“开发中”状态，除非修改此设置，否则不会自动加载。")]
    public BossModuleInfo.Maturity MinMaturity = BossModuleInfo.Maturity.Contributed;

    [PropertyDisplay("允许模块自动使用技能", tooltip: "例如：模块可以在击退发生前自动使用防击退技能。")]
    public bool AllowAutomaticActions = true;

    [PropertyDisplay("显示测试雷达和提示窗口", tooltip: "无需进入首领战即可调整雷达和提示窗口。", separator: true)]
    public bool ShowDemo = false;

    // radar window settings
    [PropertyDisplay("启用雷达")]
    public bool Enable = true;

    [PropertyDisplay("锁定雷达和提示窗口的位置及鼠标交互")]
    public bool Lock = false;

    [PropertyDisplay("雷达窗口背景透明", tooltip: "移除雷达周围的黑色窗口背景；如果将雷达移到其他显示器，此功能可能失效。")]
    public bool TrishaMode = true;

    [PropertyDisplay("为雷达场地添加不透明背景")]
    public bool OpaqueArenaBackground = true;

    [PropertyDisplay("显示雷达标记的轮廓和阴影")]
    public bool ShowOutlinesAndShadows = true;

    [PropertyDisplay("雷达场地缩放比例", tooltip: "雷达窗口内场地的缩放比例。")]
    [PropertySlider(0.1f, 10, Speed = 0.1f, Logarithmic = true)]
    public float ArenaScale = 1;

    [PropertyDisplay("雷达元素线条粗细比例", tooltip: "统一缩放雷达元素的轮廓粗细。")]
    [PropertySlider(0.1f, 10, Speed = 0.1f, Logarithmic = true)]
    public float ThicknessScale = 1;

    [PropertyDisplay("旋转雷达以匹配镜头方向")]
    public bool RotateArena = true;

    [PropertyDisplay("关闭地图旋转时将地图旋转 180°")]
    public bool FlipArena = false;

    [PropertyDisplay("为雷达旋转预留额外空间", tooltip: "使用上方设置时，可在雷达边缘裁切前预留额外空间，避免战斗中旋转镜头或显示方位文字时内容被裁掉。")]
    [PropertySlider(1, 2, Speed = 0.1f, Logarithmic = true)]
    public float SlackForRotations = 1.5f;

    [PropertyDisplay("在雷达中显示场地边界")]
    public bool ShowBorder = true;

    [PropertyDisplay("玩家处于危险时改变场地边界颜色", tooltip: "当玩家站在可能被机制命中的位置时，将白色边界改为红色。")]
    public bool ShowBorderRisk = true;

    [PropertyDisplay("在雷达中显示方位名称")]
    public bool ShowCardinals = false;

    [PropertyDisplay("方位名称字号")]
    [PropertySlider(0.1f, 100, Speed = 1)]
    public float CardinalsFontSize = 17f;

    [PropertyDisplay("场地标点字号")]
    [PropertySlider(0.1f, 100, Speed = 1)]
    public float WaymarkFontSize = 22f;

    [PropertyDisplay("角色三角标记缩放比例")]
    [PropertySlider(0.1f, 10, Speed = 0.1f)]
    public float ActorScale = 1f;

    [PropertyDisplay("在雷达中显示场地标点")]
    public bool ShowWaymarks = false;

    [PropertyDisplay("在雷达中显示目标标记（攻击、止步、禁止及形状标记）")]
    public bool ShowSigns = false;

    [PropertyDisplay("始终显示所有存活的队员")]
    public bool ShowIrrelevantPlayers = false;

    [PropertyDisplay("按职能为雷达中未着色的玩家上色")]
    public bool ColorPlayersBasedOnRole = false;

    [PropertyDisplay("始终显示被设为焦点目标的队员", separator: true)]
    public bool ShowFocusTargetPlayer = false;

    // hint window settings
    [PropertyDisplay("在独立窗口中显示文字提示", tooltip: "将提示窗口与雷达窗口分离，以便单独调整提示窗口的位置。")]
    public bool HintsInSeparateWindow = false;

    [PropertyDisplay("使独立提示窗口透明")]
    public bool HintsInSeparateWindowTransparent = false;

    [PropertyDisplay("显示机制顺序和计时提示")]
    public bool ShowMechanicTimers = true;

    [PropertyDisplay("显示全队提示")]
    public bool ShowGlobalHints = true;

    [PropertyDisplay("显示玩家提示和警告", separator: true)]
    public bool ShowPlayerHints = true;

    // misc. settings
    [PropertyDisplay("在游戏世界中显示移动提示", tooltip: "部分机制会在游戏世界中显示箭头，指示应移动到的位置。")]
    public bool ShowWorldArrows = false;

    [PropertyDisplay("显示近战范围指示器")]
    public bool ShowMeleeRangeIndicator = false;

    [PropertyDisplay("最大加载距离", tooltip: "最大加载距离，单位为亚姆。")]
    [PropertySlider(0.1f, 500f, Speed = 0.1f, Logarithmic = true)]
    public float MaxLoadDistance = 500f;
}
