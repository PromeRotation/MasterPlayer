# 黑衣青年：MA-X 原生机制

日期：2026-11-24。迁移类型 `BossMod.Global.Quest.FF15Collab.MAx.MAx`，三个激活组件。入口 `Presets/Quests/Entries/MAXQuest.cs`；模型 `MainScenarioQuestCatalog`、`DungeonMaxQuest`，公共输出为 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`，不读取运行库会话结果。

## 来源与边界

- 源：`MechanicRuntime/Upstream/Modules/Global/Quest/FF15Collab/MA-x.cs`，Quest68694、NameID7898、Primary252B；正式简中任务标题“黑衣青年”，本机脚本Id为 `FesBkc001_03158`。
- 本机 QuestParams[5] 为 FATE0=1409；Fate1409“机械士兵”，MapIcon60501、ClassJobLevelMax50、RequiredQuest0。对应 `bg/ffxiv/wil_w1/fld/w1f2/level/planevent.lgb` 的 `FATE_BLACK_2018_01`、Instance7687175，EventRange位置 `(291.9534,-15.55195,-20.11887)`，位于Territory141“中萨纳兰”。Quest的两个POPRANGE也指向141。
- 本场是野外FATE，不是独立任务战，不增加“持有任务”限制，不整体接管141区域。仅按141/OID252B停用旧Boss桥；战前原生预览接替该Boss绘制，其他区域脚本及桥继续共存。
- 源默认颜色可配置；当前原生沿用源默认Boss红、Noctis洋红、Trooper橙色，未新增对应的源颜色配置界面。

## 生命周期及机制

默认pull为Primary可选中且InCombat。死亡、HP0或销毁结束；脱战不结束。已完成同一对象不能靠恢复HP重新启动，真实Create/Destroy建立新代。初见HP0/Dead的可选战斗对象也记作完成。Director40000005卸载并阻止旧战斗快照重新pull，40000011仅解除屏障。停用机制清理全部输出。

| 源组件 | 模型与参数 | 时序、锁定与撤销 | 显示及导航 |
|---|---|---|---|
| Chainsaw | 14601，R10、总90°锥 | 起手落点/朝向锁定；真实Finish、取消或销毁清理 | 雷达/世界预览及危险区 |
| Shock | 14600，R10圆 | 同普通AOE；Effect、施法者死亡及预测到期不充当Finish | 双视图及危险区 |
| MagitekMissile | 14599，R5圆 | 所有合法来源可施法，包括不可选Helper；旧代包不能复活已取消读条 | 双视图及危险区 |

14598仅为未激活visual，不接管。场地为中心 `(295,-22)`、R25圆；战前仅显示轮廓，不注册硬场地或移动目标，进入战斗后才提供真实场地导航。

标记仅画当前选中的Primary实例、全部OID2529 Noctis和全部OID252A Trooper，不画Helper或额外同OID Boss。Noctis即使为友方仍画洋红，Trooper默认橙色；全部仍要求可选中、未死。朝向三角及场外夹边轮廓保留。战前先排除Dead/Completed候选，再优先Targetable，旧尸体或不可选对象不能遮住新Boss。

选敌保留原生敌对身份、合法偶数对象槽、FATE同步资格、高差、PendingDead、玩家仇恨与攻击普通/NPC队伍的基础优先级。Noctis/Trooper的特殊显示颜色不改变攻击优先级。AllowedFateId由现有实时/录制输入提供，回放不读取实时FATE。

Fate1409图标60501不在源 `Utils.IsBossFate` 的60502/60802集合中；同步到1409且目标自身属于1409时，保留基础 `ForbidDOTs` 意图及诊断。范围仍要求合法敌人槽、存活及可选中。按总迁移规格的现有PR接口边界，禁DoT暂不执行，不扩大为禁攻整目标、停手或取消所有动作；离开同步上下文立即撤销意图。

## 分层验证

- 模型：`tests/MainScenarioMaxRegression.cs` 八项，覆盖精确激活表、战前预览与多实例优先级、默认pull/完成/新代/wipe、真实结束及旧包屏障、FATE优先级/禁DoT意图、开关和真实解算器脱离Shock。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayMax.cs` 两组，按GUID定位共存脚本，覆盖三组AOE、战前双视图/无导航、多实例旧尸体与不可选对象、FATE禁DoT意图不变成禁攻、死亡、Create、wipe、逆向Seek、回放隔离及owner清理。
- 执行：普通导航走公共安全区/移动入口；本场无专属面向、特殊位移或全停手请求。真实PR选敌、GreenMove移动及最终游戏像素待实机验收。

本批证据与集中验证结果见总迁移规格的MA-X/再渡红玉海段。早期实际DLL失败为野外共存脚本却使用`Single()`的夹具假设，已改为按GUID取目标实例。
