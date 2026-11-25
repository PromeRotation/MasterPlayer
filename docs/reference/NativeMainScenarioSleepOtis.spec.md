# ASleepDisturbed 与 OtisOathbroken 原生机制

日期：2026-11-24。本批迁移两个 BossModule，共17个激活组件、16条读条规则。入口为 `Presets/Quests/Entries/ASleepDisturbed.cs` 和 `OtisOathbroken.cs`；机制保存在 `MainScenarioQuestCatalog`、`DungeonSleepOtis`、共享 `DungeonQuestParty`，经 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints` 消费。没有运行时 BMR 状态或结果依赖。

## 区域、生命周期与接管

本机游戏表核对：Quest69301 → CFC724 ↔ Territory914；Quest70478 → CFC998 ↔ Territory1213。69301/70478是任务ID，不能传入Territory接口。

| Boss | 场地与拉战 | 结束及代际 | 接管边界 |
|---|---|---|---|
| Huaca，OID2D3C | 中心(100,100)，半宽19.5方场；主怪可选中即可启动，无入战要求 | 主怪死亡、销毁、HP0结束，脱战不结束；同对象回血不重启，真实Remove/Create解锁新代 | 仅914/2D3C；默认只画主怪 |
| Otis，OID4342 | 中心(349,-14)，源Polygon真实20顶点/R19.5；按原始double角增量、float Sin/Cos乘法复制；当前观看玩家InCombat启动，主怪可不可选、可未入战 | 同上；helper创建不能清主怪完成账本 | 仅1213/4342；只画4342/4344/4345/4346/4343/4347 |

同任务 `E2Gwyddrud`（4349）仍待迁。`QuestBattle/.../ASleepDisturbed.cs` 的雕像、卡片与出口，`TheProtectorAndTheDestroyer.cs` 的居民/LayoutId/Director交互仍走原桥。脚本保留 `PreservesTerritoryMechanics`，没有整个区域压制，也没有认领未激活动作或未知读条。

## Huaca：八组件

来源：`MechanicRuntime/Upstream/Modules/Shadowbringers/Quest/MSQ/ASleepDisturbed.cs` 的44–67行、72–79行；共享组件 `BaitAway.cs`、`StackSpread.cs`。

| 组件/输入 | 来源与目标 | 锁定、判定与结束 | MP显示与导航 |
|---|---|---|---|
| TouchOfShadow，19617 | 任意来源；全队 | 独立真实读条FinishAt，实际Finish/取消/销毁清理；效果和预计到期不清 | Raidwide文字/存活队伍预测，不造危险圆；19616视觉不接管 |
| MarrowOfFlame，19613 | 真实cast目标，含NPC | 原Actor引用与独立结束钟；位置动态，目标销毁冻结；目标死亡隐藏，可原地复活 | R8轮廓/R9个人避让；本人远离同组件未标记队友；真实结束按action+目标删首 |
| GraceOfCalamity，19615 | 真实cast目标，含NPC | 同上；最少2，无最大人数 | R6轮廓；本人找原始队友位置R3同伴、避其他分摊标记R12；未标记者进入任一合法R6并集；无同伴不虚构站位 |
| BurningBeamNPC，TID1 | Player/Buddy ↔ 其它Actor，双向；不按FriendlyNpc、职业或OID猜分类 | 独立首次合法连线消费钟，delay0；全断后重连仍保留首次钟 | 动态原位置矩形前长40/半宽2；自己的圈仅轮廓，对其他存活队友提供R100/半角asin(2/d)禁向锥；他人beam参与避让 |
| BurningBeamPlayer，TID84 | 同上，不因名字限制必须本人 | 独立于TID1的列表和首次钟 | 同上；目标Tankbuster预测按实际队伍槽合并，队伍DTO仍有ID时保留缺席目标预测 |
| SoundOfHeat，19618 | 任意来源 | 起手落点/朝向，真实读条结束 | R60/总60°锥，双视图与导航 |
| DeceitOfPain，19629 | 任意来源 | 起手cast落点，真实读条结束 | R14圆，双视图与导航 |
| BalmOfDisgrace，19627 | 任意来源 | 起手cast落点，真实读条结束 | R12圆，双视图与导航 |

两种beam均保留来源和目标Actor代际、原始有向连线和首次activation。`OpTether`语义为先撤旧再建新，包括相同值重发及跨TID替换；raw/typed首次确认只消费一次。合成source销毁清理绕过旧raw收据；packet-only source从快照缺席同样发其出线Untether。目标死亡仍显示，source死亡暂隐藏，同引用复活恢复；目标销毁只冻结旧引用，不自动撤销incoming bait或绑定同ID新对象。Untether时目标已不能Find则不能虚构成功清理；有合法旧出线且目标仍存在时，source销毁删首pair。19623/19624及其它普通效果均不清bait。

两视图使用 `ScriptDrawApi.AddLinePreview` 及原始端点，沿用危险线显示样式。线不是白色实心矩形，也不参与导航。主怪priority显式0保留，其他合法敌人继续基础优先级；没有额外停手、面向或移动保持。

## Otis：九组件

来源：`MechanicRuntime/Upstream/Modules/Dawntrail/Quest/MSQ/TheProtectorAndTheDestroyer/E1OtisOathbroken.cs` 的42–76、81–94行；共享 `GenericAOEs.cs`、`StackSpread.cs`、`CastHint.cs`。普通AOE不限制helper OID。

| 组件/输入 | 模型与独立时序 | 显示、导航及结束 |
|---|---|---|
| StormlitShockwave，38202 | 独立Raidwide读条，原FinishAt | 仅文字/队伍预测，真实结束撤销 |
| ValorousAscension，38203 | 独立于38202的Raidwide | 同上，不造R40危险区 |
| RendPower，38200 | R40/总30°锥，开始插入顺序，缓存所有来源 | 前6显示且导航；真实结束晋升保留原时钟/朝向，不按预计结束排序 |
| SearingSlashThrownFlames，38197/38205 | 同一R8组，开始顺序；Count≥99才按源right-pivot严格小于排序 | 全部显示及导航；任意动作Finish按source删首组项；Effect只计数，不消费/超时 |
| BastionBreaker，38198 | 独立cast目标分散 | R6显示/R7导航，NPC及本人视角，真实结束 |
| HolyBlade，38199 | 独立cast目标分摊，R6/min2/max∞ | 同Huaca分摊的R3同伴、其他标记R12避让及未标记union加入 |
| Electrobeam，38207 | 起手落点/朝向锁定，前长40/半宽2 | 全部显示/导航，实际Finish/取消/销毁撤销 |
| SteadfastWill，38201 | 原始cast目标/FinishAt | 仅单体预测/文字，无危险几何 |
| Rush，38209 | 先以原始`cast.Loc−caster.Position`求长度/方向，最后仅起点量化；锁定原始SourcePosition；Count==99才排序 | 缓存所有，前4显示，只有前2导航及危险文字；总数>2时前2黄色，总数≤2恢复默认；任意动作Finish按source删首，隐藏项晋升不改时钟 |

未知动作只记录真实Start/Finish，供两个group组件按source消费，不加入通用躲避接管集合。真实raw CastInfo变化执行Finish→Start并尾插；同代packet补正按已有MP契约更正原记录、保留开始/结束及组索引，不能复活已消费项。Rush显式原始SourcePosition补正即使量化后几何完全相同也保留，供后续缺省起点的补正使用。38206/38208/38210自爆只出现在枚举而未激活，不造全屏；Formation和Teleport同样不扩大接管。

Rush是敌人的读条危险形状，源及旧MP消费没有玩家强制位移或传送执行；本批没有新增玩家路线、跳跃或原生操作。普通危险区、站位和线段适配集中在公共输出层，不依赖搜索器内部类型。统一寻路模型另行推进。

## 验证与边界

- 模型与公共适配：`tests/MainScenarioSleepOtisRegression.cs` 19项，含精确场地顶点、pull/完成/Create代际、真实结束/死亡/过期、跨TID替换、raw收据/销毁、缺目标冻结及预测、99排序、packet原始起点补正、FIFO晋升和NPC站位。补充raw换目标的Lose/Gain双收据与source/target同时缺席的八种历史销毁顺序反例。真实 `DungeonOutput`/`SafeZoneSolver` 验证beam脱险和分摊加入，线段显示及owner清理独立检查。
- 实时输入：914区域逐Framework采样，避免普通100ms门把20ms/40ms先后创建误合为同一帧的对象索引顺序；无效FF高字节ID与旧采样一致排除。模型另覆盖先后创建、后加入者索引更低的顺序反例。跟踪从MP脚本开始观测时建立；中途启用前未记录的生命史无法从当前快照恢复，实时调度和热重载的显示效果仍需实机确认。标准回放/Seek从同一起点按每个真实world版本重建。
- 集中回归：`native-sleep-otis-model-verified.log` 共68项全部通过；旧月读夹具同钟End→Start仅推进夹具时钟后补验，未改生产旧包屏障；启动备份确认屏障原已存在。
- 生产：`native-sleep-otis-build-final.log`，0错误/4项既有Crucible警告。
- 实际DLL：`tests/ScriptCompilation/NativeDungeonReplaySleepOtis.cs` 七组，覆盖两区域入口、真实取消、独立预测、连线、NPC/Buddy、死亡复活、双视图、导航、四显示/二风险/隐藏晋升、机制开关、反向Seek、回放禁原生控制及owner撤销。续批补入raw换目标去重及八种同时销毁顺序；原588条证据保留，最新全目录 `native-slickshell-catalog-final.log` 594条PASS、0失败。
- 上述日志位于 `.tmp/halatali-verify/`；SDK10.0.303临时选择文件验证，根 `.NET11` 配置未更改。

显示与现有导航已离线验证；未执行游戏内验收，PR真实执行、最终像素与录像时序校准仍待实机。本批源没有护盾禁攻/凝视/停手/玩家特殊移动组件，没有增加这些控制或自动任务交互。独立任务区域及第二Boss仍保留待迁状态。
