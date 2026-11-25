# 新人训练原生脚本

2026-11-24。五个源模块均已完成 MP 原生离线迁移；Tactical 提供三个区域入口，源模块只计一次。入口继承 `DungeonScript`，模型为 `DungeonNoviceTraining.cs`、`DungeonNoviceTank04.cs`、`DungeonTactical.cs`，经 `DungeonOutput → SafeZone/Draw/Hints` 输出；脚本不创建运行库会话或读取桥结果。

| 源模块 | CFC → Territory（本机表双向核对） | 主 OID | MP 入口 |
|---|---|---|---|
| NoviceTank04 | 154 → 540 | 0x1564 | `Presets/HallOfTheNovice/Entries/NoviceTank04.cs` |
| NoviceDPS03 | 159 → 545 | 0x1571 | `Presets/HallOfTheNovice/Entries/NoviceDPS03.cs` |
| NoviceDPS05 | 161 → 547 | 0x157A | `Presets/HallOfTheNovice/Entries/NoviceDPS05.cs` |
| NoviceBasicFinal | 166 → 552 | 0x1591 | `Presets/HallOfTheNovice/Entries/NoviceBasicFinal.cs` |
| NoviceTactical | 1012 → 1127 / 1013 → 1128 / 1014 → 1129 | 0x463B | `Presets/HallOfTheNovice/Entries/NoviceTactical.cs`、`NoviceTacticalFloor.cs`、`NoviceTacticalAdvanced.cs` |

四个模块各激活一个组件，无专属 AID、SID 或连续 AOE。来源为 `MechanicRuntime/Upstream/Modules/Global/HallOfTheNovice/` 下同名文件，以及 `AIHintsBuilder.FillEnemies`、`AIHints.Enemy.Priority`、`BossModule.CheckPull`、`BossModuleManager` 和 `StateMachineBuilder.TrivialPhase`。

| 机制 | 源模型与 MP 行为 | 显示 / 导航 / 执行边界 |
|---|---|---|
| DPS03 指定攻击目标 | 取持久 ActorState 字典枚举的首个 0x1574，不筛死亡或可选；其非零目标存在时仅设 priority1。未解析目标不改优先级，零目标不提示。对象帧重排不换绑；缺席删除按持久字典顺序进行，保留删除及新建后的空槽复用语义。 | `GenericInvincible` 仅保留 `Attacking invincible target!` 文字；源 AI override 不调用基类，不能改成整目标禁攻或全停手。 |
| DPS05 增援 | 全部非 0x157A 的合法候选设 priority1，不限定已知增援 OID。主怪保留基础优先级。 | 复用 PR 选敌门面；没有禁攻主怪、自动施放或专属站位。 |
| Final 指定敌人 | 全部 0x1591 合法候选设 priority1，其余保留基础优先级。 | 不因当前 Primary 的身份而遗漏同 OID 的其他对象。 |
| 基础目标规则 | 原生对象索引、敌我、可选和死亡过滤；高差/FATE 在 PendingDead 前，Pointless=-1 抵抗 setter；NameID541 例外及待结算伤害/治疗参与计算。敌人攻击友方训练 NPC 时保留源普通/NPC 队伍基础规则。 | 活跃 BossModule 不执行 CalculateAutoHints，故无敌状态不额外降低这三场的基础优先级。无目标不扩大为禁止顺带 AOE。 |
| 生命周期 | 主怪可选且 InCombat 才拉战；单纯脱战或不可选不结束。死亡/HP0 结束本代，首次见到可拉战但已 HP0 也记结束；原 Actor 恢复 HP 不重启，真实缺席或 Remove 后同 ID 新代可启动。 | 模型不按预计时长清理，也不因视角玩家死亡结束战斗。切人更新个人选敌；停止、禁用和 Seek 由 owner/新会话重建清理。 |
| 正式场地 | R20；DPS03/DPS05 圆心(0,0)，Final 精确 float 圆心(457.383,271.721)。 | 拉战后登记正式边界；没有额外危险区、目标区、障碍、特殊移动或移动暂停。 |
| 默认源绘制 | 仅 Primary，死亡/不可选不画；保留朝向，场外按精确圆径向投到 R20 并画轮廓，不使用64边形最近边近似。 | 雷达与世界通过同一原生输出验收。BMR 原始 MiniArena 在拉战前有场地与主怪标记，但旧 MP 转换器丢弃白色边框及 ActorMarker；本次按完整双端绘制要求补回拉战前圈与方向标记，均 Navigate=false，不将新增显示当作旧桥既有显示或新增导航。 |

接管只按 Territory + 主 OID。专属脚本、机制项关闭或未拉战时，已迁主模块不回退发布 BMR 输出；不整区压制未迁模块或通用 AOE。上述三个模块没有面向、视线、停手、护盾禁攻和原生动作机制，不新增这些控制。公共 PR 面向、ACR 限制与回放隔离继续参加集中回归。

离线验证：

- `native-novice-build-final.log`：生产编译 exit0，0错误、4项既有 Crucible 警告。
- `native-novice-model-final.log`：568项集中模型全部通过，含8项新人训练模型；覆盖首角斗士重排/逆序 Remove 后槽复用、首次HP0、旧代复活、缺席新代、NPC队伍基础优先级、无敌状态分支、待结算伤害/治疗及 dummy 例外。
- `native-novice-catalog-final.log`：真实生产 DLL 全目录541条 PASS、0失败，内置入口216个；新人训练五场实际回放覆盖三个入口、精确投影、双端预览、正式边界、机制关闭、源优先级、DTO预测伤害/治疗、初见HP0、同ID新代、反向Seek及停止清理。
- 集中回归曾暴露旧 Porta 夹具在同一时刻 Finish 后发新 Start，已仅推进夹具时钟；生产防旧代屏障保留，最终集中复验通过。

以上确认原生模型、双端显示、当前导航输出与回放控制隔离。游戏内最终像素、PR 实际目标切换及真实执行仍待实机；没有将离线回放视为实机验收。

Tank04 续迁：

| 机制 | 模型与来源 | 公共适配及消费边界 |
|---|---|---|
| 主对象及拉战 | 源 `NoviceTank04.CheckPull` 仅要求已绑定 Primary 可选，不要求 InCombat。首个同 OID 对象即绑定，不跳过不可选主对象。主对象销毁/完成的这一轮先卸载，下一轮按 Pending 插入逆序晋升；快照重排不更换 Primary。 | 原生绑定账本与代际撤销独立于目标选择。R20 原点场地沿用公共输出；失去可选不结束已激活训练，死亡/HP0结束，同ID真实重建可重新开始。 |
| 全部木桩禁攻 | `Tank04Dummies.AddAIHints` 对所有 0x1564 调用 priority−4；不以木桩数量是否为3门控。`QuestTargetPriority.ForbiddenTargets` 先执行合法敌人、可选/存活、槽位、高差/FATE和预测HP规则，保留 Pointless setter 保护及 NameID541 例外。 | 经 `DungeonScript → Hints.SetActionRestrictions` 发布本 owner 的指定目标禁攻，复用 PR ACR 与原生普攻停止；不扩大为全停手、禁读条、面向或移动保持。回放保留模型集合但不发送执行请求。 |
| 三木桩弱目标 | 计数遍历全部 0x1564，死亡、不可选、远处对象仍计数；严格恰3个才建立XZ包围盒中心、R1、weight1模型。 | 旧桥 `MechanicHintsConvert` 的强目标阈值100过滤此目标；原生保留模型与诊断，不发布导航目标，不把此提示作为自动走位证据。 |
| 职业动作意图 | XZ距离严格小于1时，MRD/WAR=41、GLA/PLD=7381、DRK=3621、其他=11；Spell、null目标、priority1，清理本地 ActionsToExecute 后记录请求。 | 旧MP没有 ActionsToExecute 消费者。`NoviceTank04Plan` 保留意图及清理语义，诊断输出动作ID；不清PR队列或自动施放，没有虚构开始/完成确认。 |

Tank04 五项模型及四组实际 DLL 回放覆盖不可选首对象、同OID主对象晋升、同ID新代、count3/4、死亡/不可选次对象计数、严格距离1、六职业动作、NameID541/待结算HP、双视图、正式边界、机制关闭、逆向Seek和owner清理。`native-tank04-build-final.log` 生产0错误/4项既有警告；`native-tank04-model-authoritative.log` 集中592条PASS/0失败；`native-tank04-catalog-authoritative.log` 实际DLL全目录557条PASS/0失败，入口219个。首次测试夹具集合读取修正后单跑通过，多Primary差异修正后重新集中验收。以上为显示、现有导航范围及回放隔离的离线证据，真实PR禁攻/普攻停止及游戏最终像素待实机。

Tactical 续迁：源 `Global/HallOfTheNovice/NoviceTactical.cs` 的33个激活组件全部迁入。28条读条规则、19种完整 Native 效果按包消费一次；主怪不要求可选或入战，死亡、HP0或销毁结束，同代恢复不重启。源33组件同时激活，三区域不人为裁掉另一训练的组件。源只以 CFC1012 声明模块，组件另有1013/1014分支；本机表已对三区域双向核对。

| 组件组 | 输入、身份、锁定与时序 | 显示 / 导航 / 执行 |
|---|---|---|
| StartingPositions / Interact | 复制各区域 Map 索引表；20001指定准备位置，80004清除并恢复R1；1128的18号R4粘滞，普通索引不恢复。Director严格匹配Command及四参数；首个MasterOfTrials不可选时不跳到后项。 | 准备圈仅显示，并另发进入该圈的导航约束。保留交互目标意图供诊断；旧桥无交互消费者，原生不新增自动对话/YesNo/点击，也不宣称已有动作完成确认。 |
| 两独立分散 / 两独立分摊 | 139/375各R6、事件时刻+5；139由40680/40692/40678全清，375由40692按MainTarget删首，无匹配删队首。318/317各R6、min2、图标即时；四真实效果按主目标删全部匹配。旧Actor销毁冻结；分散死亡永久删，分摊死亡暂隐藏。 | 分散R7导航，少于两名存活队员不导航；分摊本人找原位置R3未点名队友、避其他分摊R12，未点名人加入同组件任一R6。NPC24–63参与；分散保留最早钟Raidwide预测，不虚构源未产生的Shared预测。 |
| ScorchingStreak | Icon412目标Find(Target)否则事件Actor；source为原Primary引用，动态前44半宽5矩形、原始位置，+5.1。40685/40698/40686/40699按MainTarget删全部；真实Finish不清，目标死亡永久删。 | 自己轮廓、别人填充；本人对每个队友避源半角asin(5/d)扇区，别人避矩形。记录存在即Tank进(0,-15)R3，其余进(0,15)R3；source死亡隐藏诱导但不删除角色站位。Tankbuster仅诊断，不新增ACR策略。 |
| 两独立PiercingStone | Icon197分别追加事件Actor引用，首40689/40702各从自己的Targets取离Loc最近者并删首引用。首击MoveDist0/F，随后MoveDist5、消费钟+2.5，总五个真实效果；先预测再量化，效果目标存在时取当前原位置，主目标是source时取source量化位置。 | 首击仍显示R4并对本人避后退4的R9，别人R5；保留Previous→原目标线。40689另避(0,13)R30，40702另进中心R10。首击前死/毁仍保留旧引用，真实推进后才清；两组独立。 |
| FrigidRing | 四视觉Start按消费钟分别+7/+13.5/+11/+10，锁Loc/方向，排序仅显示导航首条；40694/40693/40760/40759任意真实效果全局FIFO删首。 | 环10–20或R10，首条显示与导航；取消/Finish不清，不用效果预测时钟超时。 |
| 普通范围 / Border / Withdraw | 九普通形状含40710实际R5、40713R18、40730R20，锁Start Loc/F，按匹配source/action真实Finish撤销。Border源WatchedAction=0，不将声明但不生效的四动作造边缘环。40761视觉Withdraw按F预测。 | 普通AOE经公共Output双视图/导航；Withdraw仅Raidwide文字与存活队员预测，40762未激活变体不接管。未知读条只观察真实Finish以服务group清理，不加入通用躲避压制集合。 |
| 四径向击退 | 40706/40722距离15、作用R25；40709距离10、40726距离20、无作用shape。`TacticalKnockback`保留cast来源/代际、起点/F、距离、作用范围；均径向、不截墙，严格免疫expiry>F，真实Finish/取消/销毁撤销。四组件各从玩家当前位置起，同组件内部才连续推进。 | 公共QuestKnockbackProjection仅画原位移线/落点，出界保留提示，未新增击退落点导航。40706额外进sourceR3；40722进R3朝90°总80°扇；40726进R8朝270°总60°扇；额外站位在免疫时仍保留。真实物理待实机。 |
| 两独立ThunderingPillar | 40716/40727各R4 min/max1，锁Loc/F，匹配action/source Finish删首，当前普通队员及NPC计数。 | 同组件空塔/自己正确人数塔取union进入，不合并两组件；本人所在首塔决定人数提示。当前塔内成员mask去重，时间取各组件首塔F，Raidwide诊断；不虚构塔分配或自动动作。 |
| BlazingSurge / FanOfFlames | Surge40732/40733 R20总180°仅首；Fan40752/40753 R60总46°，count≥4才排序，严格`activation<headF+1`连续前缀，不足4仍显示。任意source的任意动作真实Finish删本source首条。 | 所有可见项Risky并参与导航，未到显示前缀的条目保留独立时钟；同代packet更正不冒充Finish。 |
| Tether57 / Intercept84 | 57允许Player/Buddy↔Enemy，两端原引用、首个合法连线时钟持续复用；R19在enemy中心，目标死亡有效、source死亡隐藏，Untether按pair删首。84只检查目标OID4640，追加引用不去重，恰count1生效，Untether只有重新Find成功才删首引用；无自动死/毁清理。raw/event确认收据防止同一转换双消费，真实重复事件保留。 | 57本人避队友R19、别人避sourceR19，Active时另避场心R19，并画两端线。84保留源`d=Primary−Bomb`、center=arena−d/2、半长`|d|/2/3*2`、半宽1进入矩形；显示另取世界首Bomb，保留源TODO。均沿公共几何适配，不发送原生操作。 |
| 两凝视 | 40743/40757各独立CastGaze，锁Loc/F，默认10000，匹配真实Finish清理。 | 共用眼睛/瞳孔/当前视角前90°弧；45°禁向进入PR面向门面，回放禁止执行；不造R60危险区。 |

特殊移动模型与当前几何适配分离：`DungeonTactical.cs/TacticalKnockback.cs`保存原始机制，`DungeonTacticalOutput.cs → DungeonOutput → SafeZone/Draw/Hints`适配。没有引入栅格、A*/SIPP节点、独立寻路循环或未来统一转移API。普通AOE和站位保留现有消费范围；此训练没有额外护盾禁攻、停手或必须移动状态，不因通用门面存在而新增这些控制。

本轮证据均位于 `.tmp/halatali-verify/`：

- `native-tactical-territory.log`：CFC1012/1013/1014与Territory1127/1128/1129三组本机表双向核对。
- `native-tactical-model-final.log`：78项新人训练/FATE模型通过，含15项Tactical专项；覆盖代际、完整Director、raw/event连线收据、双分摊R12、原位置同伴R3、分散预测、跨组件独立落点、严格免疫及Gain/Lose先于快照。
- `native-tactical-model-batch.log`：492项集中场景通过，Shinryu旧夹具同钟Finish→Start一项失败；仅推进该夹具时钟后，`native-tactical-model-failed-rerun.log`单项通过。493个不同场景结果闭合，没有修改生产防旧包屏障。
- `native-tactical-build-final.log`：生产编译0错误、4项既有Crucible警告；`native-tactical-focused-rerun.log`十组Tactical实际DLL及两项公共输入检查通过。位移落点断言已按预览注册表无序枚举修正，未改生产落点。
- `native-tactical-catalog-final.log`：同一生产DLL全目录583条PASS、0失败，入口226个。双视图、实际危险区/进入约束注册、Native单包消费、免疫事件、地图撤销、机制关闭、反向Seek、回放禁执行与owner清理通过。真实PR面向、GreenMove路线与游戏最终像素仍待实机。
