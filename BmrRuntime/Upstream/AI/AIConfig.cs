namespace BossMod.AI;

[ConfigDisplay(Name = "自动移动", Order = 7)]
sealed class AIConfig : ConfigNode
{
    [PropertyDisplay("在服务器信息栏显示状态")]
    public bool ShowDTR = false;

    [PropertyDisplay("显示 AI 界面")]
    public bool DrawUI = false;

    [PropertyDisplay("跟随主角色的焦点目标")]
    public bool FocusTargetMaster = false;

    [PropertyDisplay("向其他窗口广播按键", tooltip: "部分电脑可能出现卡顿，仅在确实需要时启用。此功能主要用于多开。")]
    public bool BroadcastToSlaves = false;

    [PropertyDisplay("跟随队伍位置")]
    public int FollowSlot = 0;

    [PropertyDisplay("禁止使用技能")]
    public bool ForbidActions = false;

    [PropertyDisplay("手动选择目标")]
    public bool ManualTarget = false;

    [PropertyDisplay("禁止移动")]
    public bool ForbidMovement = false;

    [PropertyDisplay("战斗中跟随")]
    public bool FollowDuringCombat = true;

    [PropertyDisplay("首领模块运行时跟随")]
    public bool FollowDuringActiveBossModule = true;

    [PropertyDisplay("脱战时跟随")]
    public bool FollowOutOfCombat = false;

    [PropertyDisplay("跟随目标")]
    public bool FollowTarget = true;

    [PropertyDisplay("跟随目标时的期望身位")]
    [PropertyCombo(["任意", "侧面", "背面", "正面"])]
    public Positional DesiredPositional = Positional.Any;

    [PropertyDisplay("与队伍位置的最大距离")]
    public float MaxDistanceToSlot = 1f;

    [PropertyDisplay("与目标的最大距离（近战/坦克）")]
    public float MeleeMaxDistanceToTarget = 2.6f;

    [PropertyDisplay("与目标的最大距离（远程/治疗）")]
    public float RangedMaxDistanceToTarget = 24.5f;

    [PropertyDisplay("与目标判定框的最小距离")]
    public float MinDistance = default;

    [PropertyDisplay("与危险区域的首选距离")]
    public float PreferredDistance = default;

    [PropertyDisplay("启用自动挂机", tooltip: "脱战时进入自动挂机。挂机期间 AI 不会运行自动循环或选择目标。")]
    public bool AutoAFK = false;

    [PropertyDisplay("自动挂机计时", tooltip: "脱战多少秒后进入挂机模式。任何移动都会重置计时；挂机已启用时移动会退出挂机。")]
    public float AFKModeTimer = 10f;

    [PropertyDisplay("禁用障碍地图加载", tooltip: "部分内容（例如深层迷宫）可能需要启用此选项。")]
    public bool DisableObstacleMaps = false;

    [PropertyDisplay("移动决策延迟", tooltip: "请谨慎修改并保持较低数值！数值过高会导致部分机制无法及时移动。不同内容可能需要重新调整。")]
    public double MoveDelay = default;

    [PropertyDisplay("骑乘时保持待机")]
    public bool ForbidAIMovementMounted = false;

    [PropertyDisplay("将斜杠命令回显到聊天")]
    public bool EchoToChat = true;

    [PropertyDisplay("跟随 RotationSolverReborn 请求的身位", tooltip: "启用后，自动移动将通过 IPC 使用 RotationSolverReborn 当前请求的身位。")]
    public bool FollowRSRDesiredPositional = true;

    public string? AIAutorotPresetName;
}
