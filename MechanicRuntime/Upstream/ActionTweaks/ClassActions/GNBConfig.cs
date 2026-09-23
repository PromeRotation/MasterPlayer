namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class GNBConfig : ConfigNode
{
    [PropertyDisplay("预读阶段禁止过早使用闪雷弹")]
    public bool ForbidEarlyLightningShot = true;
}
