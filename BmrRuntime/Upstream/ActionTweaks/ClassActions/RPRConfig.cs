namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class RPRConfig : ConfigNode
{
    [PropertyDisplay("预读阶段禁止过早使用勾刃")]
    public bool ForbidEarlyHarpe = true;

    [PropertyDisplay("地狱出/入口与镜头方向对齐")]
    public bool AlignDashToCamera = false;
}
