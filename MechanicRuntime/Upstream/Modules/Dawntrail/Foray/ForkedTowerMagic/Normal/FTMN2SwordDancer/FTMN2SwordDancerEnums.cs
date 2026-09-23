// OID/AID/SID 枚举由 The Combat Reborn Team (LTS) 数据导入生成；缺失项按 2026-08-06 三场国服回放实测补充（中文注释为用途说明）。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

public enum OID : uint
{
    SwordDancer = 0x4D76, // R6.000, x1 本体（剑舞者，可目标化）
    SwordDancer2 = 0x4D7D, // R1.000, x1 分身（无动作，回放无施法）

    DancingSword = 0x4D7C, // R2.000, x16 舞动之剑-突进剑（施放 49616 突进 Rect 30x6）
    DancingSword1 = 0x4D7B, // R2.000, x2 舞动之剑（无动作，回放无施法）
    DancingSword2 = 0x4D7A, // R1.000, x5 舞动之剑-戳地剑（施放 49595 戳地 R5 / 49599 剑气冲击击退）
    DancingSword3 = 0x4D79, // R2.000, x3 舞动之剑-回转剑（施放 49589 月环 / 49592/49593 钢铁）
    DancingSword4 = 0x4D77, // R2.000, x4 舞动之剑-回旋剑（50525/50526 突进移动 + 49563~49574 回旋动画，无玩家伤害）
    DancingSword5 = 0x233C, // R0.500, x29, Helper type（施放回旋扇形/秘法剑落点/剑舞直条/剑气冲击提示）

    Actor1e8f2f = 0x1E8F2F, // R0.500, x1, EventObj type
    Actor1e8fb8 = 0x1E8FB8, // R2.000, x2, EventObj type
    Actor1ea1a1 = 0x1EA1A1, // R2.000, x0 (spawn during fight), EventObj type
    Actor1ec032 = 0x1EC032, // R0.500, x0 (spawn during fight), EventObj type
    Actor1ec033 = 0x1EC033, // R0.500, x0 (spawn during fight), EventObj type
}

public enum AID : uint
{
    AutoAttack = 50925, // SwordDancer->player, no cast, single-target 平A

    // 剑技风暴：全屏 AoE（开战/循环收尾，读条 5.0s）
    SwordStorm1 = 49617, // 0xC1D1 SwordDancer->self, 5.0s cast, 全屏伤害
    SwordStorm2 = 49684, // 0xC1FC DancingSword5->self, no cast, 伤害事件（无读条不绘制）

    // 投剑：引导动作（配合回旋剑突进，无玩家伤害）
    ThrowingSwords = 49559, // 0xC197 SwordDancer->self, 2.0+1.0s cast, 引导动作
    ThrowingSwords1 = 49560, // 0xC198 SwordDancer->self, no cast, 事件

    // 秘法剑：boss 位移到 4 边中点后放 96x96 矩形，玩家去矩形外（XML 提示"去左手侧/右手侧"）
    MartialMystique1 = 49583, // 0xC1AF SwordDancer->self, 4.0+1.5s cast, 秘法剑（去左手侧）
    MartialMystique3 = 49584, // 0xC1B0 SwordDancer->self, 4.0+1.5s cast, 秘法剑（去右手侧）
    MartialMystique2 = 49585, // 0xC1B1 DancingSword5->self, 5.5s cast, range 48 width 96 rect 秘法剑落点（Helper，矩形 96x96）

    // 风旋剑：出鞘+投掷动画（无玩家伤害），随后回转剑 4D79 施放钢铁/月环
    CycloswordsUnsheathed = 49586, // 0xC1B2 SwordDancer->self, 3.0s cast, 风旋剑出鞘（动画）
    Cycloswords = 49587, // 0xC1B3 SwordDancer->self, 3.0s cast, 风旋剑（动画）

    // 回转：4D79 剑在读条结束时放月环（贴剑站）或钢铁（远离），读条 1.0s
    Spin = 49589, // 0xC1B5 DancingSword3->self, 1.0s cast, range 5-60 donut 月环（剑位置 5y 内安全）
    Spin3 = 49590, // 0xC1B6 DancingSword3->self, 1.0s cast, range 5-60 donut 月环（2026-08-09 回放补充：双剑轮另一 id，同形 15~60；ModelState 5 月环验证）
    Spin1 = 49592, // 0xC1B8 DancingSword3->self, 1.0s cast, range 15 circle 钢铁（远离 15y）
    Spin2 = 49593, // 0xC1B9 DancingSword3->self, 1.0s cast, range 20 circle 钢铁（远离 20y，回放未出现备用）

    // 剑舞：boss 读条后 Helper 依次放 4 条直条 Rect 60x20（间隔 2.5s，方向指向场地中心）
    SwordDance1 = 49609, // 0xC1C9 SwordDancer->self, 4.4+0.6s cast, 剑舞（本体）
    SwordDance2 = 49610, // 0xC1CA DancingSword5->self, 5.0s cast, 剑舞提示（Helper x2，无伤害）
    SwordDance3 = 49611, // 0xC1CB DancingSword5->self, no cast, 事件
    SwordDance4 = 49612, // 0xC1CC DancingSword5->self, no cast, 事件
    SwordDance5 = 49613, // 0xC1CD DancingSword5->self, no cast, 事件
    SwordDance6 = 49614, // 0xC1CE DancingSword5->self, 1.5s cast, range 60 width 20 rect 剑舞直条（四连）

    // 跃进步法/戳地：boss 读条后 4 把 4D7A 剑在 4 边中点放 R5 圆（读条 3.6s）
    LeapingLift = 49594, // 0xC1BA SwordDancer->self, 3.0s cast, 跃步进步法（引导）
    LeapingLift1 = 49596, // 0xC1BC SwordDancer->self, no cast（瞬发，回放补充）, 跃步进步法第 1 跳（dest=落点=击退顺序第 1 剑）
    LeapingLift2 = 49597, // 0xC1BD SwordDancer->self, no cast（瞬发，回放补充）, 跃步进步法跳 2-4（dest=落点）
    Pierce = 49595, // 0xC1BB DancingSword2->self, 3.6s cast, range 5 circle 戳地（贴剑 5y 外）

    // 剑技爆发/剑气冲击：boss 读条后 4D7A 剑依次放剑气冲击（全屏击退，从剑位置向外，间隔 ~2.5s）
    Swordpointe = 49685, // 0xC215 SwordDancer->self, 2.0+1.0s cast, 剑技爆发（引导）
    Steelsbreath = 50359, // 0xC4B7 DancingSword5->self, 2.0s cast, 剑气冲击（Helper，与 49599 成对）
    Steelsbreath1 = 49599, // 0xC1BF DancingSword2->self, 2.0s cast, 剑气冲击（4D7A 剑，击退）

    // 强袭剑出鞘：动画（无玩家伤害，尾段标记）
    SurgeswordsUnsheathed = 49615, // 0xC1CF SwordDancer->self, 3.0s cast, 强袭剑出鞘（动画）

    // 突进：4D7C 剑 8 把同时（横排交替朝向或竖排同向），Rect 30x6，读条 4.0s
    Rush2 = 49616, // 0xC1D0 DancingSword->self, 4.0s cast, range 30 width 6 rect 突进（玩家伤害）
    Rush = 50525, // 0xC55D DancingSword4->location, 3.0s cast, width 7 rect 回旋剑突进（移动，无玩家伤害）
    Rush1 = 50526, // 0xC55E DancingSword4->location, 3.0s cast, width 7 rect 回旋剑突进（移动，无玩家伤害）

    // 回旋扇形：Helper 在场地中心放扇形（回旋剑 4D77 只做动画），读条 3.5s，方向随剑位置变化
    Turn1 = 49575, // 0xC1A7 DancingSword5->self, 3.5s cast, Fan 90° R14 回旋扇形
    Turn2 = 49577, // 0xC1A9 DancingSword5->self, 3.5s cast, Fan 90° R24 回旋扇形（2026-08-07 回放查证修正）
    Turn5 = 49578, // 0xC1AA DancingSword5->self, 3.5s cast, Fan 65° R14 回旋扇形（2026-08-07 回放查证修正）
    Turn7 = 49580, // 0xC1AC DancingSword5->self, 3.5s cast, Fan 54° R24 回旋扇形（秘法剑回合，回放补充）
    Turn8 = 49883, // 0xC2DB DancingSword5->self, 3.5s cast, Fan 65° R14 回旋扇形（秘法剑回合，2026-08-07 回放查证修正）
    Turnabout = 49889, // 0xC2E1 DancingSword5->self, 3.5s cast, Fan 90° R24 回旋扇形（秘法剑回合，2026-08-07 回放查证修正）
    TurnFan90R19 = 49576, // 0xC1A8 DancingSword5->self, 3.5s cast, Fan 90° R19（ACT 表有、回放零出现，兜底）
    TurnFan57R19 = 49579, // 0xC1AB DancingSword5->self, 3.5s cast, Fan 57° R19（ACT 表有、回放零出现，兜底）

    // 回旋剑动画（4D77 剑施放，无玩家伤害，无需绘制）
    Turn = 49563, // 0xC1AB DancingSword4->location, 3.5s cast, 回旋动画
    Turn9 = 49565, // 0xC19D DancingSword4->location, 3.5s cast, 回旋动画（回放补充）
    Turn10 = 49566, // 0xC19E DancingSword4->location, 3.5s cast, 回旋动画（回放补充）
    Turn3 = 49568, // 0xC1A0 DancingSword4->location, 3.5s cast, 回旋动画
    Turn4 = 49569, // 0xC1A1 DancingSword4->location, 3.5s cast, 回旋动画
    Turn11 = 49571, // 0xC1A3 DancingSword4->location, 3.5s cast, 回旋动画（回放补充）
    Turn12 = 49572, // 0xC1A4 DancingSword4->location, 3.5s cast, 回旋动画（回放补充）
    Turn13 = 49574, // 0xC1A6 DancingSword4->location, 3.5s cast, 回旋动画（回放补充）

    // 回放出现但回放数据无玩家伤害、XML 无描述的占位技能（保持枚举完整性）
    UnknownAbility = 49558, // 0xC196 SwordDancer->location, no cast, single-target
    UnknownAbility1 = 49557, // 0xC195 SwordDancer2->self, no cast, range ?-30 donut
}

public enum SID : uint
{
    VulnerabilityUp = 2347, // DancingSword5/DancingSword/DancingSword3/DancingSword4->player, extra=0x1/0x2/0x4/0x3
    UnknownStatus1 = 3558, // none->DancingSword3, extra=0x46E/0x46F
    UnknownStatus2 = 2056, // none->SwordDancer/DancingSword2, extra=0x47A/0x47B

}
public enum TetherID : uint
{
    Tether_chn_sworddancer_r01t1 = 423, // DancingSword4->SwordDancer
    Tether_chn_sworddancer_l01t1 = 424, // DancingSword4->SwordDancer
}
