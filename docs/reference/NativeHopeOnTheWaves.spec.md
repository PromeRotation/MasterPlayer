# 再渡红玉海：Imperial Centurion 原生机制

日期：2026-11-24。八个激活组件、七条读条规则。入口 `Presets/Quests/Entries/HopeOnTheWaves.cs`；生产模型 `MainScenarioQuestCatalog`、`DungeonHopeOnTheWaves`；输出复用 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`。

## 来源、范围与阶段

源 `MechanicRuntime/Upstream/Modules/Stormblood/Quest/MSQ/HopeOnTheWaves.cs`，Primary21B1、NameID4148、Quest68560。本机简中Quest标题“再渡红玉海”、Id `StmBdc103_03024`，字段链为QuestParams TERRITORYTYPE0=757 / QUESTBATTLE0=181 → QuestBattle181.Quest=68560 → QuestBattleResident181.SoloDuty=472 → CFC472.TerritoryType757 ↔ Territory757.CFC472。CFC表Name为空，标题取Quest；另一个Quest区域613是CFC0野外红玉海，不能混用。

按757/OID21B1隔离Boss桥，独立 `QuestBattle/Stormblood/MSQ/HopeOnTheWaves.cs` 的事件物体1EA1A1状态8、后续坐标连接与区域优先级仍待迁，不把Boss迁移算成整个任务Zone完成。

默认pull为Primary可选且InCombat，不加HP或Dead过滤。源自定义阶段以CFC不再是472结束，因此主怪死亡、HP0、销毁、脱战及普通BattleEnded均保活，其他来源随后施法仍处理。现有实时及回放CFC从Territory表派生，退出757负责撤销本脚本。Director40000005立即卸载并锁住旧快照pull，40000011仅解锁。

## 完整机制表

| 激活组件 | 生产参数 | 时序与结束 | 显示、导航及执行 |
|---|---|---|---|
| AssaultCannon | 10823，前向长75、宽2矩形 | 起手落点/朝向；真实Finish/取消/销毁结束 | 雷达/世界预览及危险区 |
| CircleOfDeath | 9428，实际R10.24 | 同普通AOE，不按注释6+R重算 | 双视图及危险区 |
| TwoTonzeMagitekMissile | 10929，R6 | 锁起手落点，效果或到期不清 | 双视图及危险区 |
| MagitekMissileProximity | 10893，实际R11.75 | 不照注释画R60 | 双视图及危险区 |
| CermetPile | 9425，前向长42、宽6矩形 | 起手锁定，真实结束清理 | 双视图及危险区 |
| SelfDetonate | 10928，CastHint | 独立caster FIFO；队首当前CastInfo的NPCRemainingTime，float总时长+0.3及elapsed裁剪；取消队首晋升，复用source追加到尾 | `Kill before detonation! ...s left`；没有R100危险圈或虚构Raidwide预测 |
| MineSelfDetonate | 10930，R6 | 真实Finish/取消/销毁结束 | 双视图及危险区 |
| Adds | 首个OID21AC当前读10894时取LocXZ；OID2114设ShouldBeTanked=true、DesiredPosition=LB中心、priority5；否则当前读10928者priority5，其他明确0 | 首NPC按完整全局Actor字典历史枚举，不按当前快照顺序或首个读条NPC；保留销毁空槽被其他OID复用的顺序；Pointless=-1不可覆盖 | 保留敌人拉位意图及提示；没有PreferProvoking；不新增自动拉位执行 |

普通AOE不限制施法者OID或可选中状态。所有原生`IsEnemy==true`对象按源显示红色方向三角，并保留Dead/Targetable过滤和场外夹边轮廓，不能按Hostile推测或只画Primary。

场地是中心定义 `(473.25,751.75)`、半轴34×21、50顶点、旋转140°的真实椭圆多边形，浮点运算复现源Ellipse，正式中心取轮廓AABB中心。雷达、世界显示与求解共用这一几何，不换成近似圆场。

## 适配与执行边界

旧链路为 `AIHintsBuilder → BossModule.CalculateAIHints → AIHints.Normalize → MechanicNavigationHost`。Hope Adds只改PotentialTargets的敌人字段，没有写GoalZones；`MechanicHintsConvert`只转换已有GoalZones，导航桥未消费DesiredPosition/ShouldBeTanked。当前原生通过既有`DungeonEnemyDirective`保留这份意图并给出拉位提示，但不接`NativeEnemyGoals`或挑衅执行，以免扩大旧消费范围。期望敌人位置不等于玩家目的地。

普通危险区继续通过DungeonOutput发布并由现有公共安全区解算、移动组件消费；本模块没有特殊位移、凝视、护盾禁攻或全停手请求。ARR/MP回放只绘制和规划，不发送原生操作。

全局Actor顺序从原生脚本开始观察建立；757实时采样逐Framework执行，排除源也过滤的FF高位无效ID。若脚本在一段未记录的对象生命史之后才启用，启用前的字典空槽历史无法从单帧恢复；这与其他依赖持久全局枚举的迁移一样，需实机确认中途启用边界，不能借回BMR历史补猜。

## 分层验证

- 模型：`tests/MainScenarioHopeRegression.cs` 八项，覆盖精确激活表、区域生命周期、wipe、普通cast身份/真实结束、独立自爆FIFO与NPC float时钟、全局Actor空槽/快照重排、完整priority setter、源marker、开关和真实解算器躲避R11.75导弹。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayHope.cs` 三组，覆盖六组双视图AOE及hint-only自爆、原生敌人标记、主怪死毁/BattleEnded后继续施法、源场地4,623点Contains及中心对照、首NPC真实交替、无额外拉位导航、wipe/重开、普通对象占用空槽的反例、逆向Seek、回放隔离及owner清理。
- 执行：显示及当前普通AOE导航离线验证；真实GreenMove、PR选敌与最终像素待游戏内验收。Adds原生自动拉位不在当前已有执行范围，不能把意图或提示写作执行成功。

本批集中验证证据见总迁移规格对应段。独立复核发现的“仅存NPC子字典”顺序差异已改为完整Actor持久字典，并补模型与实际DLL反例。
