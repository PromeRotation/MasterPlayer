namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME4Index;

[SkipLocalsInit]
sealed class IndexStates : StateMachineBuilder
{
    public IndexStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<IndexAOEs>();
    }
}
