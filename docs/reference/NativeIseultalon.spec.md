# 暗夜来访者：Iseultalon 原生机制

日期：2026-11-24。入口 `Presets/Quests/Entries/Iseultalon.cs`，GUID `masterplayer.quest.iseultalon`。机制模型为 `DungeonIseultalon`，普通施法沿用 `MainScenarioQuestCatalog`，单人塔复用本批两场实际消费的 `DungeonQuestTower` / `DungeonQuestTowers`；输出经 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`，无运行时BMR结果依赖。

## 身份与生命周期

源 `MechanicRuntime/Upstream/Modules/Global/Quest/FF15Collab/Iseultalon.cs`，Quest68695、Primary252C、NameID7895“青爪魔”。本机Quest Id `FesBkc002_03159`，QuestParams Territory839 / QuestBattle184 → QuestBattleResident184.SoloDuty647 → CFC647.Territory839，Territory839.CFC647反向一致。

场地中心 `(-289,-30)`，源实际使用R25圆；注释所称六边形没有实现，不据此猜造顶点。只接管本Boss；其他同任务Boss和任务交互仍由各自模块处理。

默认可选且InCombat拉战，Primary死亡、HP0或raw实际缺席结束；完成后持续同ID回血不重拉，raw缺席后重现才解锁新代。40000005清会话并阻止旧帧重拉，40000011解锁。实时逐Framework扫描，PR ObjectCreate/Remove不直接改变生命周期，连续raw ID保留首次OID。

## 八组件与观察输入

| 源组件 | 原始参数及锁定 | 判断与撤销 | 显示、导航、预测 |
|---|---|---|---|
| Thunderbolt | Icon129回调Actor，非TargetID；R5；default时钟 | 每次追加，包括同Actor重复；任意来源15083 Effect全清；死亡暂隐，复活恢复；销毁保留旧引用/位置，同ID替身不换绑 | 量化中心R5轮廓，活party≥2时R6避让；被标记槽位去重预测，default时钟 |
| Electrocution | 14609；R3/min1/max1；Start时self目标取caster当前位置，否则目标位置/LocXZ；不量化，Activation=default | 14609/14610实际Finish按结束时位置逐轴≤1删全部匹配，且每次NumCasts++；取消、销毁若结束读条也如此；效果和预测到期不清 | 依占员显示绿轮廓/危险填充；允许塔union或避满塔；合法塔内活成员mask，首塔default时钟 |
| Electrocution2 | 14981；R3/min1/max1；Start锁LocXZ，保留FinishAt及caster | Finish按caster删首塔；无超时自动删除 | 继承占员显示/文字；导航覆盖父类，始终进插入顺序首塔，即时，无伤害预测 |
| DeathRay | 14603；长40/宽2；锁施法位置/方向，危险黄色 | 普通真实Finish/取消/销毁结束清；Effect和到期不清 | 双视图、导航及GTFO文字；显式Colors.Danger对应默认黄色 |
| DeathRay2 | 14602 Start锁消费钟+6.1秒；长40/宽22；原点/方向动态跟Primary原始姿态 | 同代补正不滑钟；普通Finish不清；任意来源14604 Effect清；无超时 | 双视图、导航及GTFO文字；原点不额外量化 |
| Stomp | 14605 | 实际Finish结束 | 仅Raidwide预测，不造R80危险圈 |
| NeedleShot | 14606，R5/min2/max∞，Start解析原目标引用，中心量化 | Finish按TargetID删首匹配，效果不清；死亡暂隐，销毁保留旧引用 | 轮廓、同伴R2.5及其他R5分摊join；max∞允许带标记者加入其他圈；无伤害预测 |
| TailWhip | 14607，R12/总角270° | 普通真实读条合同 | 双视图及危险区 |

14610只是源Finish观察输入，不新增失败全屏伤害组件。15082视觉、Icon93未被源处理，不额外接管。

## 单人塔细节与公共适配

Electrocution首次匹配Finish前所有塔均不禁本人；NumCasts>0后，新塔捕获当时观看角色的party槽ForbiddenSoakers。已有塔不随切人重写；查看者按当前槽查询原名单。禁踩者不计占员及伤害mask；死亡成员不计。

保留源GenericTowers特殊分支：`inTower`检查当前人在塔内且合法占员恰一人，但不检查当前人Forbidden。禁踩玩家与唯一合法NPC同处塔内时，仍有“GTFO from tower!”及危险填充，可能没有塔避让AI。不能凭文字反推导航。

raw缺席销毁先于幸存者本帧位置更新及新Start。模型按持久对象字典顺序，在删除缓存对象前保存该Finish的DeterminePosition并提前消费Finish。自目标施法者先移动再销毁时，使用最后接受的位置；源先于目标销毁时，目标仍可解析。旧塔销毁与新塔同帧同点时，旧Finish先增加NumCasts，新塔才创建并捕获禁踩槽，不能先建新塔再被旧Finish一起清除。

塔保存稳定Id、原始施法、来源/目标、位置、判定时间及禁踩槽。同代补正只修现存塔；已由其他Finish消费的本代塔不复活。raw CastInfo真实改变仍按Finish→Start建立新记录。

`default(DateTime)`在模型中保持源“立即生效”语义；`DungeonOutput`把此值适配为persistent危险区，避免交给时间缓冲减秒而下溢。非default时钟仍为定时区域；Finish/Effect/阶段退出及owner撤销均清句柄。

Noctis2650使用Vulnerable色标记，Primary普通敌人色；不增加额外选敌、自动动作、凝视、护盾停手或特殊移动。

## 验证边界

- 模型：`tests/QuestIseultalonDikaiosyneRegression.cs`覆盖上述八组件、同代/真新代、销毁历史位置、切人禁踩名单、NPC占员、default时钟、首塔强制进入、旧引用、宽窄线、真实解算入塔及owner清理。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayIseultalonDikaiosyne.cs`覆盖双视图、default约束、源圆6,084点Contains对照、销毁、重复图标、动态宽线、NPC、首塔晋升、完成锁、wipe、开关、反向Seek和执行隔离。
- 显示和当前塔/普通AOE导航离线验证；本场没有额外原生操作。GreenMove实际走位、PR控制权交接及最终游戏像素待实机验收。
- 集中证据：`.tmp/halatali-verify/native-iseult-dikaios-model-final.log`235项通过；`native-iseult-dikaios-build-final.log`生产0错误/4项既有Crucible警告；`native-iseult-dikaios-focused-final.log`三组本批DLL场景通过；`native-iseult-dikaios-catalog-final.log`全目录616条PASS/0失败。独立复核确认历史销毁位置及同帧Finish→Start修正已闭合。
