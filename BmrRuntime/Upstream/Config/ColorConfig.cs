namespace BossMod;

[ConfigDisplay(Name = "配色方案", Order = -1)]
public sealed class ColorConfig : ConfigNode
{
    [PropertyDisplay("场地: 背景")]
    public Color ArenaBackground = new(0xc00f0f0f);

    [PropertyDisplay("场地: 边框")]
    public Color ArenaBorder = new(0xffffffff);

    [PropertyDisplay("场地: 典型危险区域 (AOE)")]
    public Color ArenaAOE = new(0x80008080);

    [PropertyDisplay("场地: 典型安全区域")]
    public Color ArenaSafeFromAOE = new(0x80008000);

    // TODO: imminent aoes and dangerous players should use separate color
    [PropertyDisplay("场地: 典型危险前景元素（连线等）")]
    public Color ArenaDanger = new(0xff00ffff);

    [PropertyDisplay("场地: 典型安全前景元素（连线等）")]
    public Color ArenaSafe = new(0xff00ff00);

    [PropertyDisplay("场地: 敌人")]
    public Color ArenaEnemy = new(0xff0000ff);

    [PropertyDisplay("场地: 非敌人的重要物件（不可选中的连线源、可交互物体等）")]
    public Color ArenaObject = new(0xff0080ff);

    [PropertyDisplay("场地: 玩家角色")]
    public Color ArenaPC = new(0xff00ff00);

    [PropertyDisplay("场地: 陷阱")]
    public Color ArenaTrap = new(0x80000080);

    [PropertyDisplay("场地: 光照")]
    public Color ArenaLight = new(0xffffffff);

    [PropertyDisplay("场地: 需要特殊关注的脆弱目标")]
    public Color ArenaVulnerable = new(0xffff00ff);

    [PropertyDisplay("场地: 即将变为脆弱的目标")]
    public Color ArenaFutureVulnerable = new(0x80ff00ff);

    [PropertyDisplay("场地: 近战范围指示器")]
    public Color ArenaMeleeRangeIndicator = new(0xffff0000);

    [PropertyDisplay("场地: 其他")]
    public Color[] ArenaOther = [new(0xffff0080), new(0xff8080ff), new(0xff80ff80), new(0xffff8040), new(0xff40c0c0), new(0x40008080), new(0xffffff00), new(0xffff8000), new(0xffffa080)];

    [PropertyDisplay("场地: 重要玩家（机制相关）")]
    public Color ArenaPlayerInteresting = new(0xffc0c0c0);

    [PropertyDisplay("场地: 普通/无关玩家（可根据设置被职能颜色覆盖）")]
    public Color ArenaPlayerGeneric = new(0xff808080);

    [PropertyDisplay("场地: 非当前队伍/团队的玩家（通常不绘制）")]
    public Color ArenaPlayerReallyGeneric = new(0x80808080);

    [PropertyDisplay("场地: 普通/无关 坦克")]
    public Color ArenaPlayerGenericTank = Color.FromComponents(30, 50, 110);

    [PropertyDisplay("场地: 普通/无关 治疗")]
    public Color ArenaPlayerGenericHealer = Color.FromComponents(30, 110, 50);

    [PropertyDisplay("场地: 普通/无关 近战")]
    public Color ArenaPlayerGenericMelee = Color.FromComponents(110, 30, 30);

    [PropertyDisplay("场地: 普通/无关 法系")]
    public Color ArenaPlayerGenericCaster = Color.FromComponents(70, 30, 110);

    [PropertyDisplay("场地: 普通/无关 远敏")]
    public Color ArenaPlayerGenericPhysRanged = Color.FromComponents(110, 90, 30);

    [PropertyDisplay("场地: 普通/无关 焦点目标")]
    public Color ArenaPlayerGenericFocus = Color.FromComponents(0, 255, 255);

    [PropertyDisplay("轮廓和阴影")]
    public Color Shadows = new(0xFF000000);

    [PropertyDisplay("标点: A")]
    public Color WaymarkA = new(0xff964ee5);

    [PropertyDisplay("标点: B")]
    public Color WaymarkB = new(0xff11a2c6);

    [PropertyDisplay("标点: C")]
    public Color WaymarkC = new(0xffe29f30);

    [PropertyDisplay("标点: D")]
    public Color WaymarkD = new(0xffbc567a);

    [PropertyDisplay("标点: 1")]
    public Color Waymark1 = new(0xff964ee5);

    [PropertyDisplay("标点: 2")]
    public Color Waymark2 = new(0xff11a2c6);

    [PropertyDisplay("标点: 3")]
    public Color Waymark3 = new(0xffe29f30);

    [PropertyDisplay("标点: 4")]
    public Color Waymark4 = new(0xffbc567a);

    [PropertyDisplay("方位: 北")]
    public Color CardinalN = new(0xff0000ff);

    [PropertyDisplay("方位: 东")]
    public Color CardinalE = new(0xffffffff);

    [PropertyDisplay("方位: 南")]
    public Color CardinalS = new(0xffffffff);

    [PropertyDisplay("方位: 西")]
    public Color CardinalW = new(0xffffffff);

    [PropertyDisplay("身位颜色")]
    public Color[] PositionalColors = [new(0xff00ff00), new(0xff0000ff), new(0xffffffff), new(0xff00ffff)];

    [PropertyDisplay("规划器: 背景")]
    public Color PlannerBackground = new(0x80362b00);

    [PropertyDisplay("规划器: 背景高亮")]
    public Color PlannerBackgroundHighlight = new(0x80423607);

    [PropertyDisplay("规划器: 冷却")]
    public Color PlannerCooldown = new(0x80756e58);

    [PropertyDisplay("规划器: 选项备选颜色")]
    public Color PlannerFallback = new(0x80969483);

    [PropertyDisplay("规划器: 效果")]
    public Color PlannerEffect = new(0x8000ff00);

    [PropertyDisplay("规划器: 窗口")]
    public Color[] PlannerWindow = [new(0x800089b5), new(0x80164bcb), new(0x802f32dc), new(0x808236d3), new(0x80c4716c), new(0x80d28b26), new(0x8098a12a), new(0x80009985)];

    [PropertyDisplay("按钮按压颜色")]
    public Color[] ButtonPushColor = [new(0xff000080), new(0xff008080), new(0xff000050), new(0xff000060), new(0xff005050), new(0xff006060)];

    [PropertyDisplay("文字颜色")]
    public Color[] TextColors = [new(0xffffffff), new(0xff00ffff), new(0xff0000ff),
     new(0xff00ff00), new(0xff0080ff), new(0xffff00ff), new(0x80808080), new(0x80800080),
     new(0x80ffffff), new(0x8000ff00), new(0xffffff00), new(0x800000ff), new(0xff404040),
     new(0xffff0000), new(0xff000000), new(0x80008080), new(0x8080ff80), new(0xffc0c0c0)];

    [PropertyDisplay("碰撞调试颜色")]
    public Color[] CollisionColors = [new(0xff00ff00), new(0xff00ffff), new(0xff0000ff)];

    [PropertyDisplay("寻路调试颜色")]
    public Color[] PathfindingColors = [new(0xff007fff), new(0xff808080), new(0xff0000ff), new(0xffff0080)];

    [PropertyDisplay("寻路调试颜色 5")]
    public Color PathfindingColors5 = new(0xffff0000);

    [PropertyDisplay("寻路调试颜色 6")]
    public Color PathfindingColors6 = new(0xffffffff);

    [PropertyDisplay("寻路调试颜色 7")]
    public Color PathfindingColors7 = new(0x80164bcb);

    [PropertyDisplay("寻路调试颜色 8")]
    public Color PathfindingColors8 = new(0x808236d3);

    public static ColorConfig DefaultConfig => new();
}
