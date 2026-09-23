// 目录战状态机：相位拆分基于 2026-08-06 三场回放实测时序（时长取回放读条实测值）：
// 开场（核爆 48415）→ 封印武器循环（48384 远离/48386 靠近 ×4 顺序随机 + 核爆）
// → 元素阶段 1（控制 48394 → 创造 48400 → 展开 48399 → 创造 48400 → 整合 48401+48905 → 飞翔 48403）
// → 机制堆叠（封印武器 → 圣枪冲击波 → 召唤 48408 → 二连 48390+镰鼬/居合 → 48903 → 全知烈火 48418 → 预言 48412+陨石/天崩 → 封印武器 → 连续咏唱 48407 → 双核爆）
// → 元素阶段 2（控制 → 展开 → 封印武器插入 → 创造 → 整合 → 飞翔，与阶段 1 不同：少一次创造、多一次封印武器）
// → 收尾（封印武器 → 召唤 → 本体死亡）
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

[SkipLocalsInit]
sealed class IndexStates : StateMachineBuilder
{
    public IndexStates(BossModule module) : base(module)
    {
        // 相位 0：开场核爆（进战即读条 4.7s）
        var p0 = SimplePhase(0x10000, id =>
        {
            Cast(id, (uint)AID.Flare, 4.7f, 4.7f, "核爆")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
        }, "开场核爆");
        // 核爆读条结束 → 切封印武器循环
        p0.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID != (uint)AID.Flare;

        // 相位 1：封印武器循环（远离/靠近 ×4 顺序随机，间隔 6.3s；核爆后进入元素阶段）
        var p1 = SimplePhase(0x20000, id =>
        {
            CastMulti(id, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 12.5f, 4.7f, "封印武器")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
            CastMulti(id + 0x10, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 6.3f, 4.7f, "封印武器");
            CastMulti(id + 0x20, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 6.3f, 4.7f, "封印武器");
            CastMulti(id + 0x30, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 6.3f, 4.7f, "封印武器");
            Cast(id + 0x40, (uint)AID.Flare, 6.3f, 4.7f, "核爆");
        }, "封印武器循环");
        // 元素控制读条开始 → 切元素阶段
        p1.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.OmniElements;

        // 相位 2：元素阶段 1（控制→创造→展开→创造→整合→飞翔；两轮球分别在两次创造后生成）
        var p2 = SimplePhase(0x30000, id =>
        {
            Cast(id, (uint)AID.OmniElements, 8.2f, 3.7f, "元素控制")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
            Cast(id + 0x10, (uint)AID.ElementaryEvocation, 4.2f, 2.7f, "元素创造");
            Cast(id + 0x20, (uint)AID.ElementaryExpansion, 13.2f, 2.7f, "元素展开");
            Cast(id + 0x30, (uint)AID.ElementaryEvocation, 13.2f, 2.7f, "元素创造");
            Cast(id + 0x40, (uint)AID.ElementaryChemistry, 17.2f, 3.6f, "元素整合");
            Cast(id + 0x50, (uint)AID.PropulsiveProphecy, 8.3f, 2.7f, "飞翔指令");
        }, "元素阶段1");
        // 封印武器读条开始（靠近/远离随机）→ 切机制堆叠
        p2.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID is (uint)AID.SealedImplements or (uint)AID.SealedImplements1;

        // 相位 3：机制堆叠（封印武器→冲击波→召唤→二连→48903→全知烈火→预言→封印武器→连续咏唱→双核爆）
        var p3 = SimplePhase(0x40000, id =>
        {
            CastMulti(id, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 3.1f, 4.7f, "封印武器")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
            Cast(id + 0x10, (uint)AID.Summon, 6.3f, 2.7f, "召唤"); // 圣枪冲击波与封印武器并行（组件绘制）
            Cast(id + 0x20, (uint)AID.DuologyOfImplements2, 9.2f, 3.7f, "二连召唤·封印武器"); // 伴镰鼬/居合连招
            Cast(id + 0x30, (uint)AID.SealedImplements3, 3.1f, 1.7f, "封印武器·连招");
            Cast(id + 0x40, (uint)AID.AllKnowingFlames, 7.3f, 4.7f, "全知烈火"); // 结束后全知劫火分散
            Cast(id + 0x50, (uint)AID.Predict, 9.2f, 2.7f, "预言"); // 预言现象陨石/天崩
            CastMulti(id + 0x60, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 8.2f, 4.7f, "封印武器"); // 与陨石判定重叠
            Cast(id + 0x70, (uint)AID.Dualcast, 6.3f, 2.7f, "连续咏唱");
            Cast(id + 0x80, (uint)AID.Flare, 2.2f, 4.7f, "核爆");
        }, "机制堆叠");
        // 元素控制读条开始 → 切元素阶段 2
        p3.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID == (uint)AID.OmniElements;

        // 相位 4：元素阶段 2（控制→展开→封印武器插入→创造→整合→飞翔；6 球在创造后一次生成）
        var p4 = SimplePhase(0x50000, id =>
        {
            Cast(id, (uint)AID.OmniElements, 12.4f, 3.7f, "元素控制")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
            Cast(id + 0x10, (uint)AID.ElementaryExpansion, 4.2f, 2.7f, "元素展开");
            CastMulti(id + 0x20, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 19.2f, 4.7f, "封印武器"); // 展开后插入
            Cast(id + 0x30, (uint)AID.ElementaryEvocation, 19.2f, 2.7f, "元素创造");
            Cast(id + 0x40, (uint)AID.ElementaryChemistry, 16.2f, 3.6f, "元素整合");
            Cast(id + 0x50, (uint)AID.PropulsiveProphecy, 8.2f, 2.7f, "飞翔指令");
        }, "元素阶段2");
        // 封印武器读条开始 → 切收尾
        p4.Raw.Update = () => Module.PrimaryActor.CastInfo?.Action.ID is (uint)AID.SealedImplements or (uint)AID.SealedImplements1;

        // 相位 5：收尾（封印武器（远离/靠近随机）+圣枪冲击波并行 → 召唤 → 本体死亡）
        DeathPhase(0x60000, id =>
        {
            CastMulti(id, [(uint)AID.SealedImplements, (uint)AID.SealedImplements1], 3.2f, 4.7f, "封印武器")
                .ActivateOnEnter<FlareCasts>()
                .ActivateOnEnter<SealedImplementsAway>()
                .ActivateOnEnter<SealedImplementsNear>()
                .ActivateOnEnter<ElementOrbs>()
                .ActivateOnEnter<ElementFloor>()
                .ActivateOnEnter<ElementRings>()
                .ActivateOnEnter<ElementWaitGuide>()
                .ActivateOnEnter<ElementaryChemistryRects>()
                .ActivateOnEnter<HolyLanceShockwaves>()
                .ActivateOnEnter<SlashCombos>()
                .ActivateOnEnter<AllConsumingFlames>()
                .ActivateOnEnter<ProphecyMeteors>();
            Cast(id + 0x10, (uint)AID.Summon, 6.3f, 2.7f, "召唤");
        });
    }
}
