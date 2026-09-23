// 剑舞者战状态机：相位拆分基于 2026-08-06 三场回放实测时序（boss 读条序列；时长取回放实测值）：
// 开战（剑技风暴 5.0s）→ 主循环（投剑→秘法剑×2→风旋剑×2→回转→剑舞→跃进步法→剑技爆发→强袭→剑技风暴，
// 约 2 轮）→ 尾段（风旋剑→剑舞→强袭→投剑→本体死亡，约 4 分钟）。
// 阶段切换用 boss 读条驱动（与 FTMN1 同模式）：P1 内"风旋剑出鞘"出现多次，故附加状态链位置判断
// （ActiveState.ID 需已进入循环 2 结尾的剑技风暴状态；注意 seqID<<24 溢出绕回：0x20000<<24=0x200 为
// p1 状态基址，循环 2 结尾剑技风暴 = 0x200+0x110 = 0x310，修复 2026-08-07：原 0x20100 永不可达致状态机
// 卡死 p1、boss 死亡后模块不卸载、雷达被占用），避免循环 1 的风旋剑出鞘误触发切段。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

[SkipLocalsInit]
sealed class SwordDancerStates : StateMachineBuilder
{
    public SwordDancerStates(BossModule module) : base(module)
    {
        // 相位 0：开战（剑技风暴，全屏）
        var p0 = SimplePhase(0x10000, id =>
        {
            Cast(id, (uint)AID.SwordStorm1, 5.0f, 5.0f, "剑技风暴")
                .ActivateOnEnter<SwordStorm>()
                .ActivateOnEnter<MartialMystique>()
                .ActivateOnEnter<SpinRing>()
                .ActivateOnEnter<SpinOut>()
                .ActivateOnEnter<SpinOutFar>()
                .ActivateOnEnter<SwordDance>()
                .ActivateOnEnter<Pierce>()
                .ActivateOnEnter<Swordspear>()
                .ActivateOnEnter<Rush>()
                .ActivateOnEnter<Turn>()
                .ActivateOnEnter<CenterGoal>();
        }, "开战");
        // 剑技风暴读条结束 → 切主循环
        p0.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID != (uint)AID.SwordStorm1;

        // 相位 1：主循环（循环 1 + 循环 2 拼接，回放实测两轮约 187s）
        var p1 = SimplePhase(0x20000, id =>
        {
            Cast(id, (uint)AID.ThrowingSwords, 3.0f, 3.0f, "投剑")
                .ActivateOnEnter<SwordStorm>()
                .ActivateOnEnter<MartialMystique>()
                .ActivateOnEnter<SpinRing>()
                .ActivateOnEnter<SpinOut>()
                .ActivateOnEnter<SpinOutFar>()
                .ActivateOnEnter<SwordDance>()
                .ActivateOnEnter<Pierce>()
                .ActivateOnEnter<Swordspear>()
                .ActivateOnEnter<Rush>()
                .ActivateOnEnter<Turn>()
                .ActivateOnEnter<CenterGoal>();
            CastMulti(id + 0x10, [(uint)AID.MartialMystique1, (uint)AID.MartialMystique3], 5.5f, 5.5f, "秘法剑"); // 左/右手侧随机
            CastMulti(id + 0x20, [(uint)AID.MartialMystique1, (uint)AID.MartialMystique3], 5.5f, 5.5f, "秘法剑");
            Cast(id + 0x30, (uint)AID.CycloswordsUnsheathed, 3.0f, 3.0f, "风旋剑出鞘");
            Cast(id + 0x40, (uint)AID.Cycloswords, 3.0f, 3.0f, "风旋剑");
            CastMulti(id + 0x50, [(uint)AID.Spin, (uint)AID.Spin1], 1.0f, 1.0f, "回转"); // 月环/钢铁随机
            Cast(id + 0x60, (uint)AID.CycloswordsUnsheathed, 3.0f, 3.0f, "风旋剑出鞘");
            Cast(id + 0x70, (uint)AID.Cycloswords, 3.0f, 3.0f, "风旋剑");
            CastMulti(id + 0x80, [(uint)AID.Spin, (uint)AID.Spin1], 1.0f, 1.0f, "回转");
            Cast(id + 0x90, (uint)AID.SwordDance1, 5.0f, 5.0f, "剑舞");
            Cast(id + 0xA0, (uint)AID.LeapingLift, 3.0f, 3.0f, "跃进步法");
            Cast(id + 0xB0, (uint)AID.Swordpointe, 3.0f, 3.0f, "剑技爆发");
            Cast(id + 0xC0, (uint)AID.SurgeswordsUnsheathed, 3.0f, 3.0f, "强袭剑出鞘");
            Cast(id + 0xD0, (uint)AID.SwordStorm1, 5.0f, 5.0f, "剑技风暴");
            // 循环 2（回放：循环 2 比循环 1 少风旋剑回合，直接接剑技风暴）
            Cast(id + 0xE0, (uint)AID.ThrowingSwords, 3.0f, 3.0f, "投剑");
            CastMulti(id + 0xF0, [(uint)AID.MartialMystique1, (uint)AID.MartialMystique3], 5.5f, 5.5f, "秘法剑");
            CastMulti(id + 0x100, [(uint)AID.MartialMystique1, (uint)AID.MartialMystique3], 5.5f, 5.5f, "秘法剑");
            Cast(id + 0x110, (uint)AID.SwordStorm1, 5.0f, 5.0f, "剑技风暴");
        }, "主循环");
        // 循环 2 结尾的剑技风暴读完、尾段风旋剑出鞘读条开始 → 切尾段
        // （循环 1 的风旋剑出鞘（0x230/0x260）也会匹配到该 AID，故附加状态链位置判断）
        // 2026-08-07 深查修复：条件由 >= 0x310 放宽为 > 0x260（循环 1 第二次风旋剑出鞘之后）——
        // 状态机在循环 2 期间（0x2E0~0x311）任意位置出现风旋剑出鞘都必为尾段（循环 2 无此技能），
        // 原条件在状态机因循环 2 变体/非预期读条漂移未停在 0x310 时错过切换 → 卡死 p1 致模块不卸载；
        // boss 提前死亡的兜底见模块 UpdateModule（StateMachine.Reset）。
        p1.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.CycloswordsUnsheathed
            && (Module.StateMachine.ActiveState?.ID ?? 0) > 0x260;

        // 相位 2：尾段（风旋剑→剑舞→强袭→投剑→本体死亡；死亡前可能还有秘法剑/剑技风暴，状态链不等待直接收尾）
        DeathPhase(0x40000, id =>
        {
            Cast(id, (uint)AID.CycloswordsUnsheathed, 3.0f, 3.0f, "风旋剑出鞘")
                .ActivateOnEnter<SwordStorm>()
                .ActivateOnEnter<MartialMystique>()
                .ActivateOnEnter<SpinRing>()
                .ActivateOnEnter<SpinOut>()
                .ActivateOnEnter<SpinOutFar>()
                .ActivateOnEnter<SwordDance>()
                .ActivateOnEnter<Pierce>()
                .ActivateOnEnter<Swordspear>()
                .ActivateOnEnter<Rush>()
                .ActivateOnEnter<Turn>()
                .ActivateOnEnter<CenterGoal>();
            Cast(id + 0x10, (uint)AID.Cycloswords, 3.0f, 3.0f, "风旋剑");
            Cast(id + 0x20, (uint)AID.SwordDance1, 5.0f, 5.0f, "剑舞");
            Cast(id + 0x30, (uint)AID.SurgeswordsUnsheathed, 3.0f, 3.0f, "强袭剑出鞘");
            Cast(id + 0x40, (uint)AID.ThrowingSwords, 3.0f, 3.0f, "投剑");
        });
    }
}
