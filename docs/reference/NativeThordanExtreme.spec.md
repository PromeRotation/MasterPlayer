# 圆桌骑士（极）MP 原生机制

## 来源和接管

本机客户端核表：Territory **448**，CFC **91**，Primary OID **0x1453**，NameID **3632**。来源为 `MechanicRuntime/Upstream/Modules/Heavensward/Extreme/Ext3Thordan/` 的八个文件及其实际公共组件。该类型包含34个实际激活组件，不是34场战斗。

入口 `Presets/Dungeons/Entries/ThordanExtreme.cs`，GUID `masterplayer.trial.thordan.extreme`。生产状态由 `Presets/Trials/Thordan/ThordanEncounter*` 和 `ThordanTimeline` 持有，经 `DungeonEncounter → DungeonOutput → ScriptSafeZoneApi/ScriptDrawApi` 发布。生产代码不构造 BossModule/MechanicSession，不读取桥输出。未迁区域继续保留桥。

## 机制表

表中 F 为源 NPCRemainingTime 的当前预测结束；预测结束不替代真实事件。raw CastInfo 变空或被替换会触发旧施法 Finish，已知玩家、Buddy、Pet、Chocobo和明确非Spell不触发NPC组件施法回调；主怪状态机仍观察原始CastInfo。完整原生Effect仅接受Spell，旧缺失ActionType保持未知兼容。

| 实际组件 | 关键输入和参数 | 锁定、消费及输出 |
|---|---|---|
| AscalonsMight | 5246，11.8码总90° | 主怪到当前目标动态顺劈；目标本人诱导远离队友，其他人避让；轮廓与导航分离 |
| HeavenlySlashAdelphel / HeavenlySlashJanlenoux | 5268，OID1455/1456，10.2码总90° | 两独立组件，动态目标及真实Effect计数 |
| Meteorain | 5252，R6 | Start锁Loc/Rotation/F，真实Finish消费 |
| AscalonsMercy / AscalonsMercyHelper | 5247/5248，34.8/34.5码总20° | 两独立施法列表，不以枚举视觉动作额外补AOE |
| DragonsGaze | 5257/5258，默认10000范围；八眼OID1E9E27–1E9E2E动画 | 每次Start按Loc/F加眼，任意匹配Finish删首；眼位提示由真实动画设置；眼形、瞳孔、本人前90°弧与联合PR面向 |
| LightningStorm | 5250，R5 | 捕获原目标Actor，R5轮廓/R6导航，目标FIFO Finish；本人分散与非本人避让分开 |
| DragonsRage | 5259，R6恰8人 | 原目标引用/槽位；容量、重叠与死亡过滤保留；单一加入圈priority1，多候选使用并集补集；目标本人buddy约束沿用公共有限采样 |
| ArenaChange | 常驻环21–24 | 仅组件激活期间发布；场地切换由阶段完成驱动 |
| Heavensflame | 5292，R6 | 锁Loc/F，真实Finish |
| BurningChains | Tether9，Effect35308 | 旧Party槽对称保存原伙伴；显示连线、提示与距离加1危险圈，不伪造断链距离 |
| Conviction | 5277，R3/一人塔 | 原位置/F；进入任一空塔；有合法加入塔时不叠加满塔避让；满员外部填充危险、塔内超员轮廓；塔内玩家并集伤害预测 |
| SerZephirin | OID1454 | 实际可选存活实体标记/组件Active条件 |
| SpiralThrust1 / SpiralThrust2 | 4120 Effect预测10/12.1秒；5286真Start | 预测54.2×12、不Risky；首真Start清全部预测，真54.2×12锁Loc/Rotation/F；Finish计数但持久线留至组件停用 |
| SwordShieldOfTheHeavens | OID1455/1456，SID944/945，5270目标 | Created原引用，状态槽Gain/Lose；减伤/换目标/集火/分离文字及红色标记；源无禁攻输出，**不扩大为ForbiddenTargets或全停手** |
| HoliestOfHoly | 5269 | 动态NPC钟的Raidwide提示和当前存活队伍预测，不画全屏危险圈 |
| SkywardLeap | Source Icon14；5289 Effect | 主怪→原目标R20诱导，default activation；每个真实Effect全局删首，无预计到期 |
| SpiralPierce | Tether5；5287 Effect | 玩家/非玩家识别，原Source/Target动态50×12诱导及连线；解绑删除、末次效果重置共享钟；伤害mask取活Source，钟取首CurrentBait |
| DimensionalCollapse | 5274，R9 | 锁Loc/F，真实Finish |
| HiemalStormSpread | Source Icon29，处理钟+3秒；5295 Effect | 原目标引用，R6显示/R7导航；任意真实Effect清全部；Raidwide预测按目标ID查当前Party槽，缺Actor不丢保留槽 |
| HiemalStormVoidzone | OID1E9729，EventState!=7，R6 | 当前实体位置硬障碍与双视图，不以死亡或预计时间清除 |
| FaithUnmoving | 激活时首OID145A位置；径向16码，忽略免疫 | 原点一次锁定，导航生效激活钟+4.5秒；源落点R18硬约束，真实5275 Effect和组件停用结束；双视图落点/连线 |
| CometCircle / MeteorCircle | OID1462/1461 | 独立Adds标记及Active；小彗星全DeadOrDestroyed和大彗星Active条件分别推进状态机 |
| HeavyImpact | 5280起手；5280–5283 Finish | 总270°的R6.5、6.5–12.5、12.5–18.5、18.5–27.5四段；跨Source按序号、逐轴≤1、角差≤.05匹配首序列；每Finish钟+2秒，第四段删除；Effect不消费 |
| BossReappear | 4187 Effect | 独立计数；不是预计主怪返回时间 |
| LightOfAscalon | 4187后7次5263 | 原点(-.822,-16.314)，各3码，忽略免疫，处理钟+10.7+i×1.3；逐次真实Effect删首并连续投影；源weight2弱目标旧MP无消费者，保持显示/提示 |
| UltimateEnd | 5262 Effect | 计数/状态机，不补源没有的自动技能或全屏危险 |
| HolyShieldBash | 5271/5266 Start/Effect | 5271回调时旧Party前八槽和主怪目标分Target/Share/ShareNotFirst/Avoid；5266持原Source，raw Finish不清；首5271仅置1，5266清Source置2；Share/Target用既有priority1目标，Avoid为矩形内危险 |
| HeavenswardLeap / PureOfSoul / AbsoluteConviction | 5290/5297/5285 Effect | 三个独立计数组件和状态条件 |

## 连续时间线和输入寿命

原生三phase分别为 `Phase 1`、`Add phase`、`Boss death`，完整335状态、五组trio与末尾100段enrage原生持有。所有状态ID、float时长、Name/Comment/EndHint、134个预期主怪CastStart动作及有序Enter/Exit组件均由源运行库逐项验证。主怪不可选进入Add phase；两骑士在模块Update后惰性缓存，不能在本帧状态机前提前缓存跳过阶段；最后主怪HP0/死亡/销毁分别按源phase条件处理。

raw等待不以预计时长跳转；组件条件保留源overdue窗口、缺组件诊断和仍推进规则。非预期主怪AID报告诊断但不停止源允许的推进；主怪状态机不擅加Spell过滤。新时间线DTO `ScriptTimeline*` 由 `MasterPlayerScript.GetTimeline → ScriptManager.CurrentTimeline` 供雷达、回放窗和主窗原有时间线UI消费；无原生时间线时保留桥时间线。生成工具为 `tools/generate_thordan_timeline.py`，元数据输入可由实际DLL专项设置 `THORDAN_METADATA_EXPORT` 导出；生产无运行库类型依赖。

Actor保存原引用并逐帧更新；缺席、OID/GameObjectId/ObjectIndex换代先旧Finish、Tether Lose、Status Lose，再销毁，随后创建新对象。状态保60个原生槽，队伍表在输入回调后更新；slot0 tether及旧录像无SlotIndex的首项回退分别保留，不宣称恢复旧录像未记录的原始槽。热Created排除Pet等原生非NPC，冷激活按源每Actor Created→Cast→Tether→Status补播。切人刷新个人输出，保全局事件；Seek重建脚本，不复用后续清理。

普通AOE锁原Loc/Rotation/F，不随演员走动。凝视文字/危险色使用源归一方向判据，面向请求以.75秒提前窗口联合求合法角；PR请求由公共Hints每帧续期。回放只规划、绘制和时间线，不发送PR面向/ACR、GreenMove或原生动作。

## 几何、导航和执行边界

初始场地是源48边形R23.5，缩场为源40边形R21.052；`ThordanArenas`保存源真实顶点，不能用三角函数近似或圆代替。源量化使用乘倒数后Round再减.5格；已覆盖除法导致格号不同的反例。源Marker有效性过滤保留，不把盾Buff解释为禁攻。

Faith保留原位移模型、原点、16码、无免疫、+4.5导航钟与真实结束。当前适配为 `ThordanFaithTemplate/ThordanFaithGeometry`，最终进入公共安全区/真实SafeZoneSolver。旧桥有限采样可能因填充率>65%丢弃这项**源已有硬导航**；本迁移用源Distance谓词修复此适配缺口，未将击退变成主动传送或新执行API。

Holy Avoid的源`SDOutsideOfUnion.Distance`与其`Contains`方向不一致；真实单矩形Distance在内部为负，应离开矩形。旧桥union归一会反成进入矩形目标，本迁移按源实际Distance修正；Share/Target仍为原priority1加入目标，不扩大为所有角色必须同向站位。

SpiralPierce本人诱导方向的源asin在队友距Source<6码时非有限：保留模型、显示和明确诊断，不编造安全扇。目标本人buddy导航继续使用现有公共采样边界；没有输出的采样不得说成已执行。LightOfAscalon只显示/提示，不将未消费weight2目标扩成自动走位。普通AOE、分散、分摊、塔、连线、Faith和Holy各自通过当前公共导航消费；实际物理位移与宿主GreenMove仍待实机。

## 本机验证证据

唯一前缀 `.tmp/halatali-verify/native-thordan-20261125-*`，生产DLL SHA256：

`dca1d7373aae6624b87568b677d0f2ab79d50533952e09bdfc47ad4dab43dd3b`

- `model-05.log`：36 PASS/0 FAIL，包含14项Thordan、3项公共cast边界、Faded Thordan及18项回放世界/槽位往返。
- `build-02.log`：0错误，4项既有Crucible警告；使用临时SDK10.0.303，根SDK11配置未改。
- `replay-07.log`：11条PASS/0FAIL，包括两条公共输入、真实三phase推进、PR联合面向与回放禁执行、Heavy四段、未来/真Thrust、动态Pierce线、盾marker/无扩大禁攻、Holy/塔/Hiemal/切人、完整335源状态和UI行构建、Faith实际解算无least-danger fallback。
- 同专项真实源Faith Distance对照28,227点，初始/缩场各26,244点。
- `catalog-03.log`：同一生产DLL共974条PASS、0FAIL，进程exit0；集中目录中三处旧`DungeonCastObservation`反射调用补齐新增`IsSpell=null`参数，失败证据保留于`catalog-01/02.log`，没有降低原断言。

`replay-03`至`06`保留失败证据：手工激活后Seek重建、WorldFrame省略Party导致清空队伍、线与AOE采用不同生产注册表，均已修夹具且未降低阶段/导航/连线断言。`replay-07`使用上述同一生产DLL，无为测试添加的生产后门。

这些是合成事件、真实源运行库oracle、生产组件和解算器验证；没有本批真实战斗录像或游戏内物理/PR执行/最终双视图像素验收证据。集中目录结果及最终审计另由主迁移规格记录。
