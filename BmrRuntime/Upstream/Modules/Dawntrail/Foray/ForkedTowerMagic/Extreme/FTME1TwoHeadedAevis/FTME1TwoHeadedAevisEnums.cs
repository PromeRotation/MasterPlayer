namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Extreme.FTME1TwoHeadedAevis;

public enum OID : uint
{
    TwoHeadedAevis = 0x4C18, // 19480 超魔本体
    GreenHead = 0x4C19, // 19481 超魔绿头
    BlueHead = 0x4C1A, // 19482 超魔蓝头
    BallLightning = 0x4C16,
    SwirlingOrb = 0x4C17,
    BigGreenHead = 0x4C1B, // 19483 大绿头(雷)
    BigBlueHead = 0x4C1C, // 19484 大蓝头(冰)
    SmallGreenHead = 0x4C22, // 19490
    SmallBlueHead = 0x4C23, // 19491
    ConduitThunder = 0x4C1F, // 19487 导流雷球
    ConduitIce = 0x4C20, // 19488 导流冰球
    Helper = 0x233C,
}

public enum AID : uint
{
    StormsBreath = 47638, // 绿头风暴吐息 9s
    PoisonBreath = 47639, // 蓝头剧毒吐息 9s 18m 圈
    ThunderFugue = 47640, // 绿头雷电赋格 9s 月环 18-60
    IceFugue = 47641, // 蓝头冰柱赋格 9s 20m 圈
    IceFlameCross = 47685, // 冰焰交错 2s 十字 35x11
    IceFlameRing = 47686, // 冰焰凝环 2s 月环 5-60
    SmallTwoTerrors = 47703, // 小双头恐惧 7s 40x10
    LargeTwoTerrors = 47702, // 大双头恐惧 7s 60x5 + 小双头直线
    FrontThunderFugue = 50727, // 前雷电赋格 11s 月环 + 绿魔法阵直线
    FrontIceFugue = 50728, // 前冰柱赋格 11s 20m + 蓝魔法阵直线
    RearThunderFugue = 47629, // 后雷电赋格 延迟11s
    RearIceFugue = 47630, // 后冰柱赋格 延迟11s
    ThunderfrostTempest = 47735, // 雷霜暴风雨 剩余球 15m
    LightningCluster = 50697,
    IceCluster = 50698,
    Shock = 47706,
    HypothermalCombustion = 47707,
    ArcaneBeacon = 49720,
    DisplacementBreath = 47643, // 位移吐息
    AutoAttack = 47754,
}

public enum SID : uint
{
    SuperMage = 4228, // 超魔标记
    EasterlyReprise = 5403,
    WesterlyReprise = 5404,
    VulnerabilityUp = 2347,
}

public enum IconID : uint
{
    Mahjong1 = 0x2D2,
    Mahjong2 = 0x2D3,
    Mahjong3 = 0x2D4,
    Mahjong4 = 0x2D5,
}

public enum TetherID : uint
{
    MahjongLine = 0x19B,
}
