using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME4Index;

// Extreme 超魔之塔 Boss4: Index. 封印武器（竖琴 15m/弓 11m）、居合/风斩 60 度扇、
// 预言现象星落/净化、石化地火与属性点名指路为进阶绘制。
sealed class IndexAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Harp = new(15f);
    private static readonly AOEShapeCircle Bow = new(11f);
    private static readonly AOEShapeCone Iainuki = new(30f, 30f.Degrees());
    private static readonly AOEShapeCircle Starfall = new(10f);
    private static readonly AOEShapeDonut Cleansing = new(0f, 15f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Harp => new(Harp),
        (uint)AID.Bow => new(Bow),
        (uint)AID.Iainuki or (uint)AID.WindSlash => new(Iainuki),
        (uint)AID.Starfall => new(Starfall),
        (uint)AID.Cleansing => new(Cleansing),
        _ => null
    };
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = (uint)OID.Index)]
public sealed class Index : BossModule
{
    public Index(WorldState ws, Actor primary) : base(ws, primary, new(0f, -628f), new ArenaBoundsSquare(25f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}
