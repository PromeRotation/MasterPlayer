using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME2SwordDancer;

// Extreme 超魔之塔 Boss2: Sword Dancer. 秘法剑 96y 半圆、长短突进、不可见钢铁 20m。
// 舞动之剑（ModelState 预判月环/钢铁）与剑技爆发指路为进阶绘制。
sealed class SwordDancerAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    // 解包 49585: CastType=12, EffectRange=48, 宽 96 -> 48x96 中心对称矩形
    private static readonly AOEShapeRect MartialMystique = new(24f, 48f, 24f);
    private static readonly AOEShapeRect Rush = new(15f, 3f, 15f);
    private static readonly AOEShapeRect RushLong = new(24f, 3.5f, 24f);
    private static readonly AOEShapeCircle InvisibleSteel = new(20f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.MartialMystique => new(MartialMystique),
        (uint)AID.Rush => new(Rush),
        (uint)AID.RushLong => new(RushLong),
        (uint)AID.InvisibleSteel => new(InvisibleSteel),
        _ => null
    };
}

// 超魔舞动之剑: 剑实体放 9710 动画时按当前 ModelState 预判伤害。
// pose 0=小月环(10-40)、4=月环(15-40)、5=大月环(20-40)、6=小钢铁(10)、7=钢铁(15)、31=大钢铁(20)。
sealed class DancingSwordTelegraph(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _displayed = [with(12)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (id != 9710 || actor.OID is not ((uint)OID.DancingSword or (uint)OID.DancingSword1 or (uint)OID.DancingSword2 or (uint)OID.DancingSword3 or (uint)OID.DancingSword4))
            return;

        AOEShape? shape = actor.ModelState.ModelState switch
        {
            0 => new AOEShapeDonut(10f, 40f),
            4 => new AOEShapeDonut(15f, 40f),
            5 => new AOEShapeDonut(20f, 40f),
            6 => new AOEShapeCircle(10f),
            7 => new AOEShapeCircle(15f),
            31 => new AOEShapeCircle(20f),
            _ => null
        };
        if (shape == null)
            return;

        _displayed.RemoveAll(aoe => aoe.ActorID == actor.InstanceID);
        _displayed.Add(new(shape, actor.Position, activation: WorldState.FutureTime(9d), actorID: actor.InstanceID));
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _displayed.RemoveAll(aoe => now > aoe.Activation.AddSeconds(1d));
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = (uint)OID.SwordDancer)]
public sealed class SwordDancer : BossModule
{
    public SwordDancer(WorldState ws, Actor primary) : base(ws, primary, new(600f, 704f), new ArenaBoundsSquare(25f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}
