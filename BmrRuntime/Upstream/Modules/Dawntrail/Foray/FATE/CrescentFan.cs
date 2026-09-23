namespace BossMod.Dawntrail.Foray.FATE.CrescentFan;

public enum OID : uint {
    CrescentFan = 0x4D8D, // 新月风扇 (main target of FATE 2073)
    BigFan = 0x4D8E, // 大风扇 (spawn during fight)
    Helper = 0x233C,
    Pot = 0x4D8C, // 撒娇罐 (protected NPC, shared pot OID 0x47CB also used)
}

public enum AID : uint {
    AutoAttack = 40542, // CrescentFan/BigFan->player, no cast, single-target
    HighPressureTornado = 50221, // 高压龙卷, CrescentFan->self, 2.7s cast, range 15 width 4 rect
    HighPressureTornadoBig = 50222, // 高压龙卷, BigFan->self, 2.7s cast, range 15 width 4 rect
    Tempest = 50223, // 暴风, BigFan->self, 5.7s cast, raidwide (whole arena, no avoid)
}

sealed class HighPressureTornado(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.HighPressureTornado, (uint)AID.HighPressureTornadoBig], new AOEShapeRect(15.0f, 2.0f));

[SkipLocalsInit]
sealed class CrescentFanStates : StateMachineBuilder {
    public CrescentFanStates(BossModule module) : base(module) {
        TrivialPhase()
            .ActivateOnEnter<HighPressureTornado>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(CrescentFanStates),
    ConfigType = null,
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = null,
    TetherIDType = null,
    IconIDType = null,
    // 撒娇罐从不进入战斗；以新月风扇为主目标会在小怪清空时提前卸载模块。
    PrimaryActorOID = (uint)OID.Pot,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.ForayFATE,
    GroupID = 1093u,
    NameID = 2073u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
public sealed class CrescentFan(WorldState ws, Actor primary) : OpenWorldFate(ws, primary)
{
    private bool _bigFanSeen;

    // 2073 以非战斗中的撒娇罐作为主目标，不能使用 OpenWorldFate 默认的 InCombat 条件。
    protected override bool CheckPull()
    {
        var fate = WorldState.Client.ActiveFate;
        if (fate.ID != Info?.NameID || fate.Radius <= 0f)
            return false;

        var player = Raid.Player();
        return player != null
            && player.Position.InCircle(new WPos(fate.Center.XZ()), fate.Radius)
            && WorldState.Actors.Any(a => a.OID == (uint)OID.Pot);
    }

    public override bool CheckReset()
    {
        var bigFanExists = WorldState.Actors.Any(a => a.OID == (uint)OID.BigFan);
        _bigFanSeen |= bigFanExists;
        if (_bigFanSeen && !bigFanExists)
            return true;

        var fate = WorldState.Client.ActiveFate;
        if (fate.ID == Info?.NameID && fate.Radius > 0f)
        {
            var player = Raid.Player();
            return player == null || !player.Position.InCircle(new WPos(fate.Center.XZ()), fate.Radius + 10f);
        }

        return base.CheckReset();
    }
}
