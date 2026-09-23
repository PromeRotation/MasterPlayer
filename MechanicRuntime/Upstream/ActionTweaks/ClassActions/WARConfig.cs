namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class WARConfig : ConfigNode
{
    [PropertyDisplay("优先对自己使用死斗；需要启用智能选择目标（允许通过鼠标悬停覆盖目标）")]
    public bool HolmgangSelf = true;

    [PropertyDisplay("预读阶段禁止过早使用飞斧")]
    public bool ForbidEarlyTomahawk = true;
}
