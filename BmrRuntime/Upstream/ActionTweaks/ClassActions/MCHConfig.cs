namespace BossMod;

[ConfigDisplay(Parent = typeof(ActionTweaksConfig))]
class MCHConfig : ConfigNode
{
    [PropertyDisplay("引导喷火器时暂停自动循环")]
    public bool PauseForFlamethrower = false;
}
