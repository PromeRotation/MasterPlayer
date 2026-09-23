namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME2SwordDancer;

[SkipLocalsInit]
sealed class SwordDancerStates : StateMachineBuilder
{
    public SwordDancerStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<SwordDancerAOEs>()
            .ActivateOnEnter<DancingSwordTelegraph>();
    }
}
