namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

public enum OID : uint
{
    Index = 0x4B5F, // R7.500, x1
    Index2 = 0x4B72, // R1.000, x3
    IndexHelper = 0x233C, // R0.500, x15 (spawn during fight), Helper type

    BallOfFire = 0x4B65, // R1.500, x0 (spawn during fight)
    BallOfLevin = 0x4B66, // R1.500, x0 (spawn during fight)
    ForetoldPhenomenon = 0x4B63, // R1.000, x0 (spawn during fight)
    HolyLance = 0x4B62, // R1.000, x3
    SummonedBomb = 0x4B60, // R2.100, x0 (spawn during fight)
    SwirlingOrb = 0x4B64, // R1.500, x0 (spawn during fight)
    TranscribedIndex = 0x4B6F, // R7.500, x3

    Actor1e8f2f = 0x1E8F2F, // R0.500, x1, EventObj type
    Actor1e8fb8 = 0x1E8FB8, // R2.000, x1, EventObj type
    Actor1ea1a1 = 0x1EA1A1, // R0.500, x1, EventObj type
    FireSector = 0x1EC008, // 2015240, EventObj type
    IceSector = 0x1EC009, // 2015241, EventObj type
    ThunderSector = 0x1EC00A, // 2015242, EventObj type
    FireRing = 0x1EC00B, // 2015243, EventObj type
    IceRing = 0x1EC00C, // 2015244, EventObj type
    ThunderRing = 0x1EC00D, // 2015245, EventObj type
    Actor1ec00f = 0x1EC00F, // R0.500, x0 (spawn during fight), EventObj type
}

public enum AID : uint
{
    AutoAttack = 48421, // Index->player, no cast, single-target
    Flare = 48415, // Index->self, 5.0s cast, single-target
    Flare1 = 48417, // IndexHelper->self, no cast, ???
    SealedImplements = 48384, // Index->self, 5.0+2.0s cast, 封印武器·远离（伴 Helper 48385 爱之歌）
    RomeosBallad = 48385, // IndexHelper->self, 7.0s cast, 爱之歌：中心 R15 圈（玩家需远离，站圈外）
    UnknownWeaponskill1 = 50665, // Index->self, no cast, single-target
    SealedImplements1 = 48386, // Index->self, 5.0+2.1s cast, 封印武器·靠近（伴 Helper 48387 盯准）
    Aim = 48387, // IndexHelper->self, 7.1s cast, 盯准：场边 R20.5 处 R11 圈 ×3/×6（玩家需靠近中心，圈内危险）
    OmniElements = 48394, // Index->self, 4.0+1.0s cast, single-target
    OmniElements1 = 48395, // IndexHelper->self, no cast, ???
    ElementaryEvocation = 48400, // Index->self, 3.0s cast, single-target
    FireIV = 48396, // IndexHelper->self, no cast, range 30 ?-degree cone
    ElementaryExpansion = 48399, // Index->self, 3.0s cast, single-target
    BlizzardIV = 48397, // IndexHelper->self, no cast, range 30 ?-degree cone
    ThunderIV = 48398, // IndexHelper->self, no cast, range 30 ?-degree cone
    ElementaryChemistry = 48401, // Index->self, 3.9+1.1s cast, single-target
    ElementaryChemistry1 = 48402, // IndexHelper->self, no cast, ???
    UnknownWeaponskill2 = 48905, // IndexHelper->self, 6.0s cast, 元素整合判定：rect 15x15 ×3 @ 场边三角 (0,-656)/(±24.249,-614)，与 48401 同步（回放实测）
    PropulsiveProphecy = 48403, // Index->self, 2.7s cast, 飞翔指令（元素阶段收尾转场）
    Jump = 48404, // TranscribedIndex->self, no cast, single-target
    Shockwave = 48406, // IndexHelper->self, 5.0s cast, 冲击波：与圣枪同位置 R15（视觉/判定冗余，×6）
    Shockwave1 = 48405, // HolyLance->self, 5.0s cast, 圣枪冲击波：圣枪位 R15 圆 ×3（回放实测北枪覆盖玩家、南两枪不覆盖）
    Summon = 48408, // Index->self, 3.0s cast, 召唤：生成被召唤的爆弹怪 4B60 ×3（场边三角位，无技能）
    DuologyOfImplements = 48388, // Index->self, 5.0+1.0s cast, single-target
    DuologyOfImplements2 = 48390, // Index->self, 3.7s cast, 二连召唤·封印武器（连招版：伴 48391 镰鼬/48389 居合/48903，回放实测）
    Iainuki = 48389, // IndexHelper->self, 6.0s cast, 居合斩：60° cone R30 ×3（与 48390 连招，方向 -120/120/0）
    SealedImplements2 = 48904, // Index->self, no cast, single-target
    SealedImplements3 = 48903, // Index->self, 1.7s cast, 封印武器·连招版收尾（回放实测）
    WindSlash = 48391, // IndexHelper->self, 6.0s cast, 镰鼬之风：60° cone R30 ×3（与 48390 连招，方向 180/-60/60）
    AllKnowingFlames = 48418, // Index->self, 4.7s cast, 全知烈火（读条结束后 Helper 48420 对全体玩家 R6 分散，3 批）
    AllConsumingFlames = 48420, // IndexHelper->players, no cast, 全知劫火：玩家位置 range 6 circle（分散判定）
    Predict = 48412, // Index->self, 2.7s cast, 预言（生成预言现象 4B63 ×3，瞬移后读条 48413/48414）
    Cleansing = 48414, // ForetoldPhenomenon->self, 0.5s cast, 天崩地裂：R5-15 donut（北侧 1 个）
    Starfall = 48413, // ForetoldPhenomenon->self, 0.5s cast, 陨石：range 10 circle（南侧 2 个）
    Dualcast = 48407, // Index->self, 2.7s cast, 连续咏唱（后接双核爆 48415+48416）
    Flare2 = 48416, // Index->self, no cast, 核爆·二段（连续咏唱后，全屏）
}

public enum SID : uint
{
    SealOfTheHarp = 5535, // none->Index, extra=0x404
    VulnerabilityUp = 2347, // IndexHelper->player, extra=0x1/0x2/0x3/0x4/0x5/0x6/0x7
    SealOfTheBow = 5534, // none->Index, extra=0x401
    SealOfTheBlade = 5533, // none->Index, extra=0x402
    SealOfTheBell = 5532, // none->Index, extra=0x403
    UnknownStatus = 2552, // none->ForetoldPhenomenon, extra=0x44C/0x44D
    Dualcast = 5438, // Index->Index, extra=0x0

}
public enum IconID : uint
{
    Icon_loc06sp_05ak1 = 466, // player->self
}
public enum TetherID : uint
{
    Tether_chn_m0947_f1_p = 365, // BallOfFire->BallOfFire
    Tether_chn_m0361_mainte_1i = 88, // Index2->ForetoldPhenomenon
    Tether_chn_m0947_t1_p = 363, // BallOfLevin->BallOfLevin
    Tether_chn_m0947_i1_p = 364, // SwirlingOrb->SwirlingOrb
}
