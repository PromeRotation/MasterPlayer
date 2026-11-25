# 暗杀芝诺斯：The Time Between the Seconds 原生机制

日期：2026-11-24。入口 `Presets/Quests/Entries/TheTimeBetweenTheSeconds.cs`，GUID `masterplayer.quest.time-between-the-seconds`；模型 `MainScenarioQuestCatalog`、`DungeonTimeBetween`；公共输出沿用 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`。

## 来源及区域

源 `MechanicRuntime/Upstream/Modules/Stormblood/Quest/MSQ/TheTimeBetweenTheSeconds.cs`，Primary OID1A36、NameID5954、Quest68034。本机简中任务“暗杀芝诺斯”，Id `StmBda322_02498`；QuestParams TERRITORYTYPE0=685 / QUESTBATTLE0=163 → QuestBattle163 → QuestBattleResident163.SoloDuty465 → CFC465.Territory685 ↔ Territory685.CFC465。614是CFC0野外延夏，不能混用。

场地复现源八顶点凹多边形，AABB中心 `(-248.45,544.81)`；不换成圆或凸包。实际DLL通过源 `ArenaBoundsCustom.Contains` 对照4,422点及中心，火圈量化通过源 `WPos.Quantized` 精确对照。

## 阶段、身份与创建对账

默认拉战要求原Primary可选且InCombat，不额外过滤Dead/HP0。P1唯一结束条件是**当前模块Primary不可选**，死亡、HP0、销毁、脱战或BattleEnded均不替代该条件。对象帧缺席期间保留原引用的最后Targetable；同ID/OID重现时按`BossModuleManager.ActorAdded`重绑仍加载模块的Primary，保留阶段及组件，不将其当成全新战斗。

进入P2先替换四个通用组件，再启用ArtOfTheSword及P2Boss。重播当前仍存在的火圈及当前读条；P1已取消/结束的输入收据跨组件替换保留，迟到包不复活。P2唯一结束条件是全局演员中OID1EBC数量为0，Dead/HP0/不可选但未移除的对象仍计数；同一Update进入P2后立即判断空列表，允许直接结束。

完成对原Primary设latch，回血/重新可选或重复Create不重开；对象帧已观察到缺席后，同ID重现才能建立新代。Director40000005卸载并阻止旧快照重拉，40000011解锁。停止、区域退出及机制禁用走公共owner清理。

创建语义以完整对象帧为准。`MechanicWorldFeed.ApplyFrame` 按EntityId是否缺席执行OpDestroy/OpCreate；持续存在的同ID保持原演员OID，新的DTO或OID字段变化不另起代际。旧实时宿主未订阅PR对象创建/移除，`ReplayFeedAdapter`也不转换这两类事件。本模块在`DungeonEventAdapter`忽略这两类日志的直接生命周期作用；模型保留首次OID，按真实帧缺席解除完成锁和已见火圈标记，重现时建立新代。

PR `ObjectListCapture`的Create时间是差分扫描发现时间，可能晚于MP首次采样；Remove还含250ms宽限，不能将这两个时间戳当作演员出生/销毁证据。Create先到而raw尚缺席时不提前建圈；同钟首次raw出现后短暂缺席再出现，仍产生两个真实代际。不能从未记录的单帧推导此前演员生命史。

## 完整机制表

| 激活组件 | 参数及模型 | 时序与撤销 | 显示、导航、执行 |
|---|---|---|---|
| VeinSplitter | 8987，R10圆 | 两阶段；锁起手，真实Finish/取消/销毁清读条组件 | 双视图AOE及危险区 |
| Concentrativity | 8986，RaidwideCast | 独立当前施法者及结束钟，全队伤害预测 | 提示/预测，无虚构R80圈 |
| LightlessFlame | OID1CED创建，R11，创建位置源量化 | 消费钟+7秒；保留真实Source/Generation/Origin/Activation；阶段替换/结束清组件 | 双视图及危险区，保持源持续行为 |
| LightlessSpark | 8985，R40.92，总90°锥 | 两阶段，普通读条锁定与真实结束 | 双视图AOE及危险区 |
| ArtOfTheSword | 8993，前长41、全宽6矩形 | 仅P2，阶段进入重播当前读条 | 双视图AOE及危险区 |
| P2Boss | OID1EBC与1A37红色方向标记 | 仅P2；默认原Primary标记另外保留，Destroyed/Dead/不可选不画 | 仅显示，不额外设置目标/移动 |

源LightlessFlame创建的 `AOEInstance` **未传ActorID，默认0**；后续8988的Start/Finish按非零caster ID匹配不到。因此普通8988开始、结束、取消、效果、演员销毁、预测超时均不改时钟或清圈。本迁移保留此可查证源行为，没有把实际Source字段偷偷用于清圈。阶段进入会对仍存在的1CED重新调用创建语义，以进入阶段时的当前位置量化和消费钟+7秒重建。

## 适配和分层验证

模型保留机制时钟和身份，几何及解算类型转换集中于公共输出。本模块没有特殊位移、凝视、护盾禁攻或停手请求，不增加自动动作或任务交互。普通危险区通过既有SafeZone求解、公共移动执行组件消费；回放只绘制和规划。

- 模型：`tests/MainScenarioTimeRegression.cs` 11项，覆盖精确组件表、主怪冻结、P2真实移除及同Update完成、完成latch/真实帧新代、火圈历史与晚到/重复Create不划代、同钟raw出现后短暂缺席再现、组件替换及迟到P1包、队伍伤害预测、标记、wipe/开关和真实解算器脱离R10圆。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayTime.cs` 三组，覆盖双视图与P2独占组件、源火圈量化及场地oracle、真实取消/效果/帧缺席、死亡P2保留、HP0默认拉战、同ID替身、晚到/重复Create及Create早于raw时不提前建圈、同钟确认后的短暂缺席、新帧代际重建、逆向Seek、回放执行隔离与owner清理。
- 显示和当前普通AOE导航已离线验证；真实GreenMove、宿主控制权交接及最终游戏像素待实机验收。没有用预览充当执行完成证据。
