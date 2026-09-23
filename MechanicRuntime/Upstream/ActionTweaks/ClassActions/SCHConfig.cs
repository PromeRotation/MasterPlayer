namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class SCHConfig : ConfigNode
{
    [PropertyDisplay("预读阶段禁止过早使用死炎法")]
    public bool ForbidEarlyBroil = true;
}
