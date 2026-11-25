# 凯夫卡 O8N 原生机制

2026-11-25。来源 `MechanicRuntime/Upstream/Modules/Stormblood/Raid/O8NKefka/O8NKefka.cs` 及其实际公共基类。本机表双向核对 Territory **751**、CFC **289**、Primary OID **0x2153**、NameID **7131**；神像 OID **0x18D6**。18个实际消费动作的 `ExtraCastTime100ms` 均为0，查询证据为 `.tmp/halatali-verify/native-kefka-20261125-action-table.log`。

入口 `Presets/Dungeons/Entries/KefkaNormal.cs`，GUID `masterplayer.raid.kefka.normal`。生产由 `KefkaEncounter` 持有原生 Actor 引用、原生读条与组件列表，经 `DungeonKefka → DungeonOutput → MP SafeZone/双视图` 输出。未创建 BossModule 或 MechanicSession，BMR 仅供源码和离线 oracle 对照。只按 Territory + Primary OID 接管本模块，不宣称同区域其他内容完成。

## 十四个实际激活组件

| 输入/组件 | 原生状态与消费条件 | 显示、导航及执行范围 |
|---|---|---|
| 10542 Tankbuster | 保存原始 TargetID；自指目标动态回退 caster 当前 TargetID。缺Actor但仍在名单的目标保持预测。使用当前 raw NPCRemainingTime，真实Finish删除 | 单体文字与伤害诊断，无虚构AOE或治疗执行 |
| 10541 Raidwide | 存活 Party 原Actor、当前raw钟。预战不预测，真实Finish结束 | 全屏提示与伤害诊断，不画全场危险圈 |
| 10516/10521 Donut | 共享10–40环队列；任意Finish按source删首 | 双视图与普通危险导航；文字内边界严格排除 |
| 10518/10520 Circle | 共享R10队列；同source任意Finish删首 | 双视图与普通危险导航 |
| 10524/10525 Thunder | 共享前40全宽10矩形；同source任意Finish删首 | 双视图与普通危险导航 |
| 10536 Wave Cannon | 前70全宽6；独立列表，不套用其他组的排序 | 双视图与普通危险导航 |
| 10537/10538 Half cone | R100总180度共享组；同source任意Finish删首 | 双视图与普通危险导航 |
| 10527 Spread | 目标原引用，销毁后冻结、同ID新对象不换绑。死亡隐藏，复活恢复原钟；Finish按action及TargetID全局删首，与caster无关 | 全部R5轮廓；至少两名存活队友时R6导航避让，目标本人跳过其他spread目标。源构造传入RaidwideOnResolve=false，不发布额外伤害预测 |
| 10528 Stack | R6精确8人。缺目标Actor的Start不建marker，其Finish仍可消费已有同TargetID项 | 目标本人及其他队友分别判断满员/多圈；复用既有进入合法圈导航，不增加Shared预测 |
| 10530 Revolting Ruin | 辅助体与Primary逐轴不在1以内时锁起手位置/朝向，R102.7总120度，F+3.1秒。10532Start或任意10531完整Effect清理；Finish和预计到期不清 | 双视图、危险导航和源文本检查 |
| 10532 Aero Assault | 锁Loc/rotation/F，径向10码，严格判定时刻免疫；不截墙 | 全部caster按源顺序连续投影；该组件首caster导航保持落点R19 |
| 10535 Shockwave | 锁Loc/rotation/F，固定方向15码，严格免疫，不截墙；方向按源Math.SinCos单次float转换 | 同上，独立首caster导航。模型保方向、距离、钟与免疫；不直接操作GreenMove |
| 10539反凝视 | range10000，原Loc及原F；禁止背眼方向±135度 | 眼睛、瞳孔、270度弧；PR联合面向请求 |
| 10540正凝视 | range10000，原Loc及原F；禁止朝眼方向±45度 | 眼睛、瞳孔、90度弧；PR联合面向请求 |

普通组中只有四个共享AOE组在Count≥99时使用源不稳定快排；其他列表不强加排序。10515/10517/10523是Fake，未按枚举增加自动化消费者。所有列表按真实回调消费；raw-only结束可删除，Cancel包本身不伪造已经观测到的raw结束。原packet/raw确认、同帧交接、迟旧事件和同ID重建由现有输入代际对账处理。

状态按60个实际原生槽保留，使用回调当时的旧Party槽；免疫类别覆盖与独立职业免疫分开。热状态Gain/Lose和Destroy回调排除Pet，只有组件冷激活补播允许Pet。读条剩余时间按源float逐帧累加并封顶TotalTime，raw仍存在但预测已过时保持约0.3秒NPC余量，不按起手F直接过期。

预战、无合法观看角色/名单槽时只显示源主怪方向标记，不向解算器登记战斗场地或导航。战斗场地中心(0,0)、R20；击退导航落点使用R19精确闭边界，不被模板默认epsilon内缩。显示顺序按源组件序：环/圆、分散/分摊、雷、Ruin、两击退、Wave/半圆、凝视。

## 验证与证据

下列日志均位于 `.tmp/halatali-verify/`，本轮使用唯一序号保留历史失败证据。

- `native-kefka-20261125-model-07.log`：119项集中模型PASS/0FAIL，包含Kefka15项、Quantum及受公共精确几何/面向影响的Eureka和已有凝视消费者。
- Quantum最后Pet修正另由 `native-kefka-20261125-quantum-pet-model-01.log` 定向命中1项PASS。
- `native-kefka-20261125-build-05.log`：生产0错误、4项既有Crucible警告，包含上述最终修正。
- 同一DLL的 `native-kefka-20261125-replay-05.log`：46条相关专项PASS/0FAIL，覆盖EurekaOrthos、Quantum、Kefka与公共初始化；Kefka七条专用场景。
- `native-kefka-20261125-catalog-01.log`：948条全目录检查PASS/0FAIL。266个入口被发现，其中Quantum仍pending；不能将入口数当副本数或全部已验数。
- 本次DLL SHA256：`4449d965f34c2f2387c6fb558f4d79b2f95623e2540d8fa3216476766866d466`。后续批次重编后以对应新日志和产物为准。

随后独立复核追踪 `SpreadFromCastTargets → CastStackSpread → UniformStackSpread` 的位置实参，发现前述spread预测测试把源关闭的输出当作预期。已删除额外预测并将模型/实际DLL断言改为零预测；Pet测试也在销毁前恢复状态，实际覆盖携带状态的Destroy Lose。修后 `native-palace-kefka-20261125-model-02.log` 集中85项PASS（包含Kefka15项和Palace）、`build-02.log`生产0错4既有警告、`related-02.log`同DLL46条相关专项PASS/0FAIL。旧119项及948条记录保留为历史，不用于证明这一新修正；最终全目录证据随后更新。

最终公共输入补验为 `.tmp/halatali-verify/native-palace-boundaries-20261125-model-03.log`（106项相关模型）、`build-02.log`（生产0错/4项既有警告）和同DLL `catalog-01.log`（949 PASS/0 FAIL，退出0），前缀相同。DungeonScript给原生状态槽消费者保留 `ZeroRemainingTime=true` 的采样时钟，避免正剩余变为零被误当永久免疫Gain。新增实际Kefka回放先保持已有短免疫、再零值、再真实刷新，分别验证击退硬导航1/1/0及反向Seek恢复1；未修改通用录制MaxValue兼容语义。对应DLL SHA256 `4b6e6510de04b4ffc265f78e839497b139698c5ef334bd3aab770d343ab7d33d`。这份全目录也覆盖前段spread修正，不再仅引用旧948条产物。

实际DLL验证真实生产 SafeZoneSolver 的15码击退落点约束，目的点在R19内且未使用最少危险回退。真实BMR `SDKnockbackInCircle` 三方向径向10/固定15对照 **78,732** 点，含零点和边界。六种AOE三方向的真实 `AOEShape.Check` 与公共 `DungeonSourceGeometry` 对照 **169,362** 点，包含donut内缘、锥心、原始矩形和圆边界；该抽取没有改变已有Eureka算法。

合成事件回放还验证：预战纯显示、真实raw结束、Cancel隔离、Ruin跨来源Effect、精确8人/缺员与分散独立时钟、死亡复活、真实60状态槽、缺Actor名单预测、动态raw钟、正反凝视联合可行heading、逆向Seek、owner清理以及回放不发布PR请求/不调用GreenMove。

这些证据分别证明模型、双视图公共数据和当前实际导航接入。没有本轮真实录制战斗及游戏内验收；PR实际面向/ACR、GreenMove运动、击退物理和最终雷达/世界像素仍待实机。没有新增停手、禁DoT执行、治疗策略或深宫流程自动化。
