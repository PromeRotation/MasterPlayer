using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME1TwoHeadedAevis;

// Extreme 超魔之塔 Boss1: Two-Headed Aevis. 毒吐息/冰柱 18-20m 圈、雷电赋格 18-60 月环、
// 冰焰交错十字、冰焰凝环 5-60、大小双头恐惧直条、前后雷电/冰柱赋格。
sealed class TwoHeadedAevisAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Poison = new(18f);
    private static readonly AOEShapeDonut ThunderFugue = new(18f, 60f);
    private static readonly AOEShapeCircle IceFugue = new(20f);
    private static readonly AOEShapeRect CrossRect = new(17.5f, 5.5f, 17.5f);
    private static readonly AOEShapeDonut IceFlameRing = new(5f, 60f);
    private static readonly AOEShapeRect SmallTerrors = new(20f, 10f, 20f);
    private static readonly AOEShapeRect LargeTerrors = new(20f, 20f, 20f);
    private static readonly AOEShapeCircle Cluster = new(15f);
    private static readonly AOEShapeRect Beacon = new(30f, 2.5f, 30f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.PoisonBreath => new(Poison, true),
        (uint)AID.ThunderFugue or (uint)AID.FrontThunderFugue or (uint)AID.RearThunderFugue => new(ThunderFugue, true),
        (uint)AID.IceFugue or (uint)AID.FrontIceFugue or (uint)AID.RearIceFugue => new(IceFugue, true),
        (uint)AID.IceFlameCross => new(CrossRect),
        (uint)AID.IceFlameRing => new(IceFlameRing),
        (uint)AID.SmallTwoTerrors => new(SmallTerrors),
        (uint)AID.LargeTwoTerrors => new(LargeTerrors),
        (uint)AID.LightningCluster or (uint)AID.IceCluster => new(Cluster, true),
        (uint)AID.Shock or (uint)AID.HypothermalCombustion => new(Cluster),
        (uint)AID.ArcaneBeacon => new(Beacon),
        _ => null
    };
}

// 冰焰交错: 一个 2s cast 变成以施法者为中心的前后左右四条 35x11 直条。
sealed class IceFlameCross(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(17.5f, 5.5f, 17.5f);
    private readonly List<AOEInstance> _displayed = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.IceFlameCross || spell.EventHappened)
            return;
        _displayed.Clear();
        var activation = Module.CastFinishAt(spell);
        for (var i = 0; i < 4; ++i)
        {
            var rot = spell.Rotation + i * 90f.Degrees();
            _displayed.Add(new(Shape, caster.Position, rot, activation: activation));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.IceFlameCross)
            _displayed.Clear();
    }
}

// 超魔麻将机制: 大绿头(雷)/大蓝头(冰)用 Tether 019B 点名，TargetIcon 02D2-02D5 是麻将 1-4。
// 点名位置画 15m 危险圈；15m 内同属性导流球（雷 19487/冰 19488）画 60 度 60m 扇形。
sealed class MahjongMechanics(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Mark = new(15f);
    private static readonly AOEShapeCone ConduitFan = new(60f, 30f.Degrees());
    private readonly List<AOEInstance> _displayed = [with(24)];
    private readonly Dictionary<ulong, bool> _colors = []; // target -> isThunder

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID != (uint)TetherID.MahjongLine)
            return;
        var isThunder = source.OID switch
        {
            (uint)OID.BigGreenHead => true,
            (uint)OID.BigBlueHead => false,
            _ => (bool?)null
        };
        if (isThunder is { } color)
            _colors[tether.Target] = color;
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (iconID is < (uint)IconID.Mahjong1 or > (uint)IconID.Mahjong4)
            return;

        // 麻将1 立即显示 17s；麻将2/3/4 等 11.5-12.5s 后显示 4s。
        var index = (int)(iconID - (uint)IconID.Mahjong1 + 1);
        var delay = index == 1 ? 0d : 11.5d + (4 - index) * 0.5d;

        var isThunder = _colors.GetValueOrDefault(actor.InstanceID);
        var activation = WorldState.FutureTime(delay);
        _displayed.Add(new(Mark, actor.Position, activation: activation, color: Colors.Danger, risky: index == 1));

        // 15m 内同属性导流球画 60 度 60m 扇形。
        var conduitOID = isThunder ? (uint)OID.ConduitThunder : (uint)OID.ConduitIce;
        foreach (var conduit in Module.Enemies(conduitOID))
        {
            if (conduit.IsDeadOrDestroyed || (conduit.Position - actor.Position).LengthSq() > 225f)
                continue;
            _displayed.Add(new(ConduitFan, conduit.Position, conduit.Rotation, activation: activation));
        }
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _displayed.RemoveAll(aoe => now > aoe.Activation.AddSeconds(1d));
        if (_colors.Count > 32)
            _colors.Clear();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP, PrimaryActorOID = (uint)OID.TwoHeadedAevis)]
public sealed class TwoHeadedAevis : BossModule
{
    public TwoHeadedAevis(WorldState ws, Actor primary) : base(ws, primary, new(-900f, 700f), new ArenaBoundsSquare(20f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override bool CheckPull()
        => base.CheckPull()
        || Enemies((uint)OID.GreenHead).Any(h => h.IsTargetable && h.InCombat)
        || Enemies((uint)OID.BlueHead).Any(h => h.IsTargetable && h.InCombat);

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var head in Enemies((uint)OID.GreenHead))
            Arena.Actor(head, allowDeadAndUntargetable: true);
        foreach (var head in Enemies((uint)OID.BlueHead))
            Arena.Actor(head, allowDeadAndUntargetable: true);
    }
}
