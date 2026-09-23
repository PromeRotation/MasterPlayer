namespace BossMod.Stormblood.Alliance.A11Mateus;

class A11MateusStates : StateMachineBuilder
{
    public A11MateusStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<HypothermalCombustion>()
            .ActivateOnEnter<IceSpiral>()
            .ActivateOnEnter<DarkBlizzardIII>()
            .ActivateOnEnter<Chill>()
            .ActivateOnEnter<BlizzardIV>()
            .ActivateOnEnter<IceBubbleBlizzardIIITowers>()
            .ActivateOnEnter<FinRays>()
            .ActivateOnEnter<FlashFreeze>()
            .ActivateOnEnter<Froth>()
            .ActivateOnEnter<Snowpierce>()
            .ActivateOnEnter<BlizzardSphere>();
    }
}
