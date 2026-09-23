// 魔之塔（The Forked Tower: Magic，国服"两岐塔 魔之塔"）Normal 第 4 战：目录（Index）。
// 场地中心 (0, -628)、boss 模型 0x4B5F（BNpcName 14717，国服"目录"/英文 Index）等实体数据来自
// 2026-08-06 国服回放实测（ZoneID 1346 新月岛北部）。OID/AID/SID 枚举由 The Combat Reborn Team (LTS)
// 数据导入生成。
namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

[ModuleInfo(BossModuleInfo.Maturity.Contributed, // 恢复显示继续测试（2026-08-09）
    StatesType = typeof(IndexStates),
    ConfigType = null, // 如需要可替换为 typeof(IndexConfig)
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = typeof(IconID),
    PrimaryActorOID = (uint)OID.Index,
    Contributors = "The Combat Reborn Team (LTS)",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CFC,
    GroupID = 1093u,
    NameID = 14717u, // 目录（Index）的 BNpcName 行；原 14503 为惧死者误用，已修正
    SortOrder = 4,
    PlanLevel = 0)]
[SkipLocalsInit]
// 异形场地（2026-08-07 用户实测，爆弹怪三点硬验证，详见 Arena.cs）：中心六边形边长 15 + 外接正方形平台，
// 初始 3 个（南/东北/西北）→ 元素控制读条结束 6 个 → 元素整合读条结束回收 3 个；内圈边长 6 正六边形
// 以挖洞实现即死区（2026-08-07 用户实测修正，无独立绘制组件）。
// 注意：ArenaBoundsCustom 中心=形状包围盒中心（初始 3 平台组合为 (0,-624.25)），UpdateModule 每帧同步 Arena.Center。
public sealed class Index : BossModule
{
    public Index(WorldState ws, Actor primary) : base(ws, primary, new(0f, -628f), IndexArena.InitialBounds)
    {
        ActivateComponent<ArenaShapes>();
        ActivateComponent<FlyingDecreeGuide>(); // 击退禁区常驻（KeepOnPhaseChange：相位切换不重建，_active 窗口状态跨相位保持；2026-08-12 修复封印武器相位切换致禁区消失）
        ActivateComponent<FlyingDecreeKnockbacks>(); // 雷达击退箭头常驻（同根因：相位切换重建致 _active 丢失 → 封印武器期间箭头消失；2026-08-12 同模式修复）
    }

    protected override void UpdateModule()
    {
        // 同步 Bounds 中心（2026-08-07 用户要求固定：Initial/Full 两版 Bounds 均以 CenterOverride 固定为 (0,-628)，
        // 切换 bounds 时中心不再跳变；Arena.Center 必须与 Bounds.Center 一致，否则路径图/判定错位）
        if (Arena.Bounds is ArenaBoundsCustom bounds)
        {
            Arena.Center = bounds.Center;
        }

        // 死亡兜底（2026-08-14 参照 FTMN2 深查修复补齐）：boss 提前死亡（DIE+）时强制结束状态机（StateMachine.Reset），
        // 保证模块被 BMM 卸载（BMM 仅当 ActiveState==null 时卸载）——覆盖状态机卡在中间相位、
        // boss 提前死亡等场景，避免雷达被本模块持续占用挡掉后续事件
        if (PrimaryActor.IsDeadOrDestroyed && StateMachine.ActiveState != null)
        {
            StateMachine.Reset();
        }
    }
}
