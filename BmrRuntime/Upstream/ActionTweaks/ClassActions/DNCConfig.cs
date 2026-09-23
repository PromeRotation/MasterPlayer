namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class DNCConfig : ConfigNode
{
    [PropertyDisplay("前冲步与镜头方向对齐")]
    public bool AlignDashToCamera = false;
}
