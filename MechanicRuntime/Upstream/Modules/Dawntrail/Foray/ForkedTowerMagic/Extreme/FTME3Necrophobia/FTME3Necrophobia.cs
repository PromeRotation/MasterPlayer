using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME3Necrophobia;

// Extreme 超魔之塔 Boss3: Necrophobia. 黑暗奔涌 60x10 + 步进地火、真空波 180 度、
// 联动爆炎/冰封/暴雷、古代爆炎 18m、古代冰封十字、分身古代暴雷。
sealed class NecrophobiaAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeRect DarkSurge = new(30f, 5f, 30f);
    private static readonly AOEShapeCone Vacuum = new(30f, 90f.Degrees());
    private static readonly AOEShapeCircle LinkageFire = new(13f);
    private static readonly AOEShapeCross LinkageIce = new(45f, 7.5f);
    private static readonly AOEShapeCone LinkageThunder = new(60f, 22.5f.Degrees());
    private static readonly AOEShapeCircle AncientFire = new(18f);
    private static readonly AOEShapeCross AncientBlizzard = new(45f, 7.5f);
    private static readonly AOEShapeCone CloneThunder = new(60f, 22.5f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.DarkSurge => new(DarkSurge),
        (uint)AID.VacuumWave => new(Vacuum),
        (uint)AID.LinkageFire => new(LinkageFire),
        (uint)AID.LinkageIce => new(LinkageIce),
        (uint)AID.LinkageThunder => new(LinkageThunder),
        (uint)AID.AncientFire => new(AncientFire),
        (uint)AID.AncientBlizzard => new(AncientBlizzard),
        (uint)AID.CloneAncientThunder => new(CloneThunder),
        _ => null
    };
}

// 黑暗奔涌的左右步进地火（同普通版）：读条后 4s 起，左右各两轮 60x10，每轮外移 10y。
sealed class DarkSurgeTreads(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(30f, 5f, 30f);
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.DarkSurge || spell.EventHappened)
            return;
        _displayed.Clear();
        var origin = spell.LocXZ;
        var side = (spell.Rotation + 90f.Degrees()).ToDirection();
        for (var i = 0; i < 2; ++i)
        {
            foreach (var sign in new[] { 1f, -1f })
            {
                var center = origin + side * (10f * (i + 1) * sign);
                _displayed.Add(new(Shape, center, spell.Rotation, activation: WorldState.FutureTime(4d + i * 2d), risky: i == 0));
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.DarkSurge)
            _displayed.Clear();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = (uint)OID.Necrophobia)]
public sealed class Necrophobia : BossModule
{
    public Necrophobia(WorldState ws, Actor primary) : base(ws, primary, new(100f, 789f), new ArenaBoundsSquare(25f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}
