namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME3Necrophobia;

[SkipLocalsInit]
sealed class NecrophobiaStates : StateMachineBuilder
{
    public NecrophobiaStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<NecrophobiaAOEs>()
            .ActivateOnEnter<DarkSurgeTreads>();
    }
}
