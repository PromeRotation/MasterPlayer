namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class PLDConfig : ConfigNode
{
    [PropertyDisplay("预读阶段禁止过早使用神圣")]
    public bool ForbidEarlyHolySpirit = true;

    [PropertyDisplay("预读阶段禁止过早使用飞盾（未习得神圣时）")]
    public bool ForbidEarlyShieldLob = true;

    public enum WingsBehavior : uint
    {
        [PropertyDisplay("游戏默认（角色相对，向后）")]
        Default = 0,

        [PropertyDisplay("角色相对，向前")]
        CharacterForward = 1,

        [PropertyDisplay("镜头相对，向后")]
        CameraBackward = 2,

        [PropertyDisplay("镜头相对，向前")]
        CameraForward = 3,
    }

    [PropertyDisplay("武装戍卫方向")]
    public WingsBehavior Wings = WingsBehavior.Default;
}
