# 衣锦还乡：Slickshell Captain 原生机制

日期：2026-11-24。迁移一个 BossModule、四个激活组件、三条读条规则。入口为 `Presets/Quests/Entries/HisForgottenHome.cs`，生产模型为 `MainScenarioQuestCatalog`、`DungeonSlickshell`，输出复用 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints` 和 `DungeonTrialVoidzones`，不消费 BMR 会话或桥结果。

## 来源与接管

- 源：`MechanicRuntime/Upstream/Modules/Stormblood/Quest/MSQ/HisForgottenHome.cs`；类型 `BossMod.Stormblood.Quest.MSQ.HisForgottenHome.SlickshellCaptain`，Quest68563、NameID6891、Primary OID213A。
- 本机简中表：Quest68563正式标题“衣锦还乡”；Territory634为延夏，CFC469反查634，枚举TerritoryType时只有634引用469。映射采用本机表字段，不把Quest或CFC当Territory。Quest脚本参数至QuestBattle的直链本轮未另行复核。
- 旧MP桥实时与回放均从TerritoryType推导CFC，因此当前可达输入中，退出CFC469等价于离开Territory634；区域脚本停止负责撤销。仅按Territory634/OID213A接管Boss，不整体压制任务Zone或其他Boss。
- 普通动作仅接管5831、8585、1087。图标26独立处理；图标R5与1087实际毒区R6不可合并。

## 生命周期与身份

源默认pull为Primary可选中且InCombat，无HP非零或未死亡条件。阶段的自定义结束条件为CFC不再是469；Primary死亡、HP0、消失、脱战及普通BattleEnded均不结束活动阶段。Primary销毁只清图标bait，其他施法者读条及效果毒区继续。

另保留BossModuleManager的权威wipe生命周期：Director40000005立即清场并锁住pull，40000011仅解除屏障，随后符合真实pull条件才启动新空阶段。个人死亡不能替代wipe。

Primary同ID/OID重建时，后续图标绑定新Primary；旧bait各自保存来源/目标引用及移除标记，不跟随同ID新对象。目标死亡由Update永久删bait，目标销毁则冻结原引用。显式Create即使早于缺席快照，也标记旧引用已移除。Primary死亡只隐藏旧bait，原对象复活可恢复；Primary销毁清全部旧bait。

## 完整机制表

| 组件 | 生产模型/原参数 | 时钟、锁定及撤销 | 显示与当前导航 |
|---|---|---|---|
| WaterIII | 5831，R8圆；允许所有合法caster OID | 起手LocXZ/Rotation锁定；实际Finish/取消/销毁撤销，Effect或预测超时不清 | 雷达/世界共同预览及危险区 |
| Kasaya | 8585，R7.6、总120°锥 | 同普通AOE；不是按注释重新计算6+hitbox | 双视图及危险区 |
| BlizzardIIIIcon | Icon26，Primary来源，TargetID优先/失败回退事件Actor，R5 | 每次追加；消费时钟+5.1d，独立保留；任意来源1087真实Start清全部，同代packet补正/Finish/Effect不清后来的Icon | 自己轮廓、他人填充；他人bait避R5，自己bait按源对存活队友发布R5禁区；source=target不额外导航；危险文字及目标Raidwide诊断保留，预测使用原列表首activation |
| BlizzardIIICast | 1087，R6，实体OID1E8D9C/EventState!=7，delay0 | cast预测跟随当前CastInfo TargetID，缺失回退LocXZ；真实Finish清cast首项；Native Effect取主目标当前位置，失败回退包落点，消费钟生成独立event预测；每个有效实体每Update仅消费距离≤2的首条event预测 | cast/event/live均为危险AOE，不改成硬障碍；live位置量化，不筛死亡/可选/战斗；PR展开效果不重复消费 |

四种OID213A/213B/213C/213D均保留红色方向marker及源可选/未死条件。七顶点原顺序与包围盒中心保留，不用多边形重心替代：

```text
(464.25,320.19), (455.65,313.35), (457.72,308.20), (445,292.92),
(468.13,283.56), (495.55,299.63), (487.19,313.73)
```

选敌对全部合法PotentialTargets执行setter：当前目标Actor的OID为2138则priority1，否则明确0；源Pointless=-1保护保留，FATE过滤/高差先于PendingDead的顺序保留。不是仅为213A或正被玩家攻击的目标赋值。PR选敌门面及owner撤销沿用公共实现，没有新增自动攻击或任务交互。

## 分层验证

- 模型及公共适配：`tests/MainScenarioSlickshellRegression.cs` 13项，覆盖HP0/Dead pull、Primary死亡/销毁/同ID代际、wipe屏障、Icon目标回退和消费时钟、真实Start与补正、目标死亡/冻结、首项预测时钟、三路毒区交接、FATE/Pointless顺序和真实解算器脱离bait。Sleep/Otis的19项同步覆盖，包含raw换目标两收据及八组同时销毁顺序。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplaySlickshell.cs` 四组，覆盖实际脚本发现、主怪死亡/销毁后的BattleEnded和新施法、双视图/导航、机制开关、Native主目标去重、EventState、同ID新Primary、wipe重开、逆向Seek、回放隔离及owner清理。源场地4,292点Contains及中心对照通过。
- 执行：本模块无特殊位移、面向或停手专属请求；普通导航通过既有公共安全区/移动执行入口。ARR/MP回放只规划和绘制，不发送原生控制。真实PR选敌、GreenMove执行及最终像素仍待游戏内验收。

证据：`.tmp/halatali-verify/native-slickshell-model-final.log`、`native-slickshell-build-final.log`、`native-slickshell-lifecycle-focused.log`、`native-slickshell-catalog-final.log`。此前44项集中模型及首轮专项日志保留；新增夹具曾误用不存在的Actor hp参数，修正为显式HP快照后重跑通过，没有改动生产API以适配测试。
