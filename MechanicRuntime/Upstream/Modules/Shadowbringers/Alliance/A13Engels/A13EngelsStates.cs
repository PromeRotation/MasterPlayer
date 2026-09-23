namespace BossMod.Shadowbringers.Alliance.A13Engels;

class A13MarxEngelsStates : StateMachineBuilder
{
    public A13MarxEngelsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<DemolishStructureArenaChange>()

            .ActivateOnEnter<PrecisionGuidedMissile2>()
            .ActivateOnEnter<DiffuseLaser>()
            .ActivateOnEnter<LaserSight1>()
            .ActivateOnEnter<GuidedMissile2>()
            .ActivateOnEnter<IncendiaryBombing2>()
            //.ActivateOnEnter<IncendiaryBombing1>()
            .ActivateOnEnter<GuidedMissile>()
            .ActivateOnEnter<DiffuseLaser>()
            .ActivateOnEnter<MarxSmash1>()
            .ActivateOnEnter<MarxSmash2>()
            .ActivateOnEnter<MarxSmash3>()
            .ActivateOnEnter<MarxSmash4>()
            .ActivateOnEnter<MarxSmash5>()
            .ActivateOnEnter<MarxSmash6>()
            .ActivateOnEnter<MarxSmash7>()
            .ActivateOnEnter<MarxCrush>()
            .ActivateOnEnter<SurfaceMissile2>();
    }
}
