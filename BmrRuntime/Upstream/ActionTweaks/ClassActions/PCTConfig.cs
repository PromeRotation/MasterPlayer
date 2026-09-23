namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class PCTConfig : ConfigNode
{
    [PropertyDisplay("重影步与镜头方向对齐")]
    public bool AlignDashToCamera = false;
}
