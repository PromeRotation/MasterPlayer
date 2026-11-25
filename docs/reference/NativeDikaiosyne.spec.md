# 你曾说过的话：Dikaiosyne 原生机制

日期：2026-11-24。入口 `Presets/Quests/Entries/ToHaveLovedAndLost.cs`，GUID `masterplayer.quest.to-have-loved-and-lost`。模型 `DungeonDikaiosyne`、`JobQuestCatalog`；单人塔复用本批两场实际消费的 `DungeonQuestTower` / `DungeonQuestTowers`，输出经 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`。

## 身份、场地及会话

源 `MechanicRuntime/Upstream/Modules/Shadowbringers/Quest/Role/ToHaveLovedAndLost.cs`，Quest68784“你曾说过的话”、Id `LucKba151_03248`，Primary2927、NameID8922“义德”。

本机QuestParams Territory869 / QuestBattle187 → QuestBattleResident187.SoloDuty672 → CFC672.Content(QuestBattle187)/Territory869，Territory869.CFC672反向一致。同Bg的野外816没有本CFC，不混用。

场地中心 `(-798.6,-40.49)`、R20圆。默认可选且InCombat拉战，Primary死亡/HP0/raw缺席结束。完成同ID回血不重拉，真实raw新代解锁；40000005清会话、阻止旧帧，40000011重开。PR对象创建/移除日志不直接划代；实时逐Framework扫描，持续raw ID保留首次OID。

## 十一组件、十六动作

| 源组件 | 输入及参数 | 生命周期 | 输出 |
|---|---|---|---|
| Bloodstain | 4747，R5 | 普通真实Finish/取消/销毁清，效果和超时不清 | 双视图/导航 |
| BrandOfSin | 16132，锁Start LocXZ，径向向外10码 | Finish按caster删首个；正常免疫；不截墙；多源顺序投影 | 仅位移投影和越界提示，见下 |
| BladeOfJustice | 16134，目标R6/min1/max∞ | Start必须解析目标；原引用动态中心，死亡暂隐、销毁冻结；Finish按TargetID删首项 | 分摊显示及站位导航；无额外伤害预测 |
| SanctifiedHoly | 17431，目标R6分散 | 同目标引用及Finish合同 | 量化中心R6轮廓，R7导航；无额外伤害预测 |
| SanctifiedHolyII | 17427，R5 | 普通真实读条合同 | 双视图/导航 |
| SanctifiedHolyIII | 17430，R6 | 同上 | 双视图/导航 |
| Fracture | 15576/13207/13208/13209/15374/16612，共享R3单人塔列表 | Start锁LocXZ和FinishAt；六动作任意Finish按LocXZ精确相等删首塔 | 占员显示、候选塔union/满塔避让、首塔钟及塔内成员预测 |
| HereticsFork | 17552，Cross长40/半宽3 | 普通真实读条合同 | 双视图/导航 |
| HereticsQuoit | 17470，Donut5–15 | 同上 | 双视图/导航 |
| SpiritsWithout | 4746，Rect长3.5/宽3 | 同上 | 双视图/导航 |
| SeraphBlade | 16131，R40/总角180° | 同上 | 双视图/导航 |

Fracture没有caster/action分组，也不作距离容差清理。同一点不同动作塔共享插入顺序：B的Finish可以删除A塔，只删一条；差0.001的位置不能命中。raw销毁仍经旧Feed的CastFinished规则处理；不存在独立无条件销毁清塔。已消费塔的同代packet补正只更新收据，不复活；随后真正新代仍能追加。

min1分摊的单独目标已满足源文字人数阈值；源导航仍要求本人找R3同伴或进入其他R6圈。max∞时即使本人也有标记，仍可加入另一分摊；不从其他圈危险预览推导R12避让。两种cast组件默认构造链不产生伤害预测，塔预测独立保留。NPC加入原生party槽24起。

## BrandOfSin原始位移与当前适配

模型保留施法来源、原始Cast、Start锁定的LocXZ原点、距离10和判定FinishAt。类型为AwayFromOrigin、`stopAtWall=false`、`ignoreImmunes=false`，没有额外shape过滤。非零方向按 `from + normalize(from-origin)*10`；位置恰在原点时无位移；多源按插入顺序以前一落点继续投影。

免疫复用公共`WhorlImmune`，本区域已加入状态事件入口。有效期必须严格晚于本次activation；等于activation不免疫。模型和当前投影不会把场外落点夹回场内。

显示复用 `QuestKnockbackProjection`：路径画到真实落点，场外角色标记可投到边缘；真实落点仍用于越界文字。源SimpleKnockbacks没有AddAIHints覆盖，所以此组件不发布导航、移动暂停、原生移动或免疫动作。预览不能计作击退站位或执行完成；以后统一寻路接入另行实现，本次保持旧消费范围。

本场无凝视、护盾禁攻、全停手、任务交互或自动动作，不补造源中没有的控制。

## 分层验证

- 模型：`tests/QuestIseultalonDikaiosyneRegression.cs`覆盖精确动作/几何、共享塔首匹配/位置反例/补正/真新代、min1及多标记导航、R6/R7分散、锁原点/连续10码/不截墙/严格免疫、完成/代际/wipe，以及公共真实解算入塔和owner撤销。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplayIseultalonDikaiosyne.cs`覆盖R20源运行库4,096点对照、双视图、共享塔消费、同代补正、击退真实场外终点、NPC标记旧引用、反向Seek和清理。
- 普通AOE及塔/分摊导航离线已验；击退显示和越界提示离线已验，源没有此项导航/执行。GreenMove实际走位、宿主执行和最终游戏像素待实机验收。
- 集中证据：`.tmp/halatali-verify/native-iseult-dikaios-model-final.log`235项通过；`native-iseult-dikaios-build-final.log`生产0错误/4项既有Crucible警告；`native-iseult-dikaios-focused-final.log`本批三组DLL场景通过；`native-iseult-dikaios-catalog-final.log`全目录616条PASS/0失败。独立复核确认共享消费后的补正屏障、max∞导航及default时钟适配修正。
