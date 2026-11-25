# 死者宫殿原生 Boss：20、60、70、80、90、100、120 层

本规格记录源 `MechanicRuntime/Upstream/Modules/Heavensward/DeepDungeon/` 的七个 Boss 实现，以及 MP 当前实际消费边界。仅接管 Boss OID；`PreservesTerritoryMechanics` 保留独立楼层 Zone 会话，不能把楼层共存检查算作楼层原生迁移。

2026-11-25用户明确禁止迁移深宫道中自动化：不移植探索、开箱、机关交互、自动前进、道具使用或层间推进。后续楼层源码只审查和迁移机制部分，不将原生迁移扩展为流程自动化。

## 来源、入口与适配

| Boss | 源模块 / 区域身份 | 原生入口 |
|---|---|---|
| Spurge | `DD20Spurge`；Territory562 / CFC175 / OID169F / Name4999 | `Presets/Dungeons/Entries/PalaceSpurge.cs` |
| The Black Rider / 深黑骑士 | `DD60TheBlackRider`；Territory593 / CFC204 / OID1814 / Name5309 | `Presets/Dungeons/Entries/PalaceBlackRider.cs` |
| Yaquaru / 水虎龙亚夸虏 | `DD70Yaquaru`；Territory594 / CFC205 / OID1815 / Name5321 | `Presets/Dungeons/Entries/PalaceYaquaru.cs` |
| Gudanna / 古丹纳 | `DD80Gudanna`；Territory595 / CFC206 / OID1816 / Name5333 | `Presets/Dungeons/Entries/PalaceGudanna.cs` |
| The Godmother / 爆弹怪教母 | `DD90TheGodmother`；Territory596 / CFC207 / OID1817 / Name5345 | `Presets/Dungeons/Entries/PalaceGodmother.cs` |
| Nybeth Obdilord | `DD100NybethObdilord`；Territory597 / CFC208 / OID1808 / Name5356 | `Presets/Dungeons/Entries/PalaceNybeth.cs` |
| Kirtimukha | `DD120Kirtimukha`；Territory599 / CFC210 / OID1819 / Name5384 | `Presets/Dungeons/Entries/PalaceKirtimukha.cs` |

区域映射来自本机中文客户端数据核表，不能将 CFC ID 当成 Territory。模型在 `HeavenswardIntro/`，事件和观察时钟由 `DungeonEncounter` 接入；普通几何与动态诱导输出经 `DungeonOutput → SafeZone`，显示经脚本预览注册表同时提供雷达与世界绘制数据。副本模型不依赖寻路节点或调度器内部类型。

七个 Boss 共用深宫读条身份处理：权威对象快照决定创建/销毁；不重复消费 PR 对象日志。保留原始输入结束时间与首次 NPC 饱和计时后的判定时间。真实 CastInfo 字段变化结束旧代、建立新代，时钟漂移不重复触发；旧取消不能结束新代。已观察读条等待真实结束，不因效果或预测超时消失。死亡辅助体仍可保有读条，生产脚本的通用躲避接管使用相同条件。

## 20 / 120 层双植物

参数来源为两个源模块及 `Components/Cleave.cs`、`Components/UnavoidableDamage.cs`。

| 机制 | 20 / 120 动作 | 显示与导航 |
|---|---|---|
| Acid Mist | 6422 / 7134 | 开始锁定，R9.6 |
| Gold Dust | 6423 / 7135 | 开始锁定，R8 |
| Rotten Stench | 6425 / 7137 | 开始锁定，前长48.6、全宽12 |
| Leafstorm | 6424 / 7136 | Raidwide 文字及普通八槽存活角色伤害预测，无空间危险圆 |
| Final Sting | 仅120层919 | 单目标 Tankbuster 预测及源98%文字；自目标回退当前仇恨目标，无自动打断 |
| Bloody Caress | 动态组件，无6421/7133读条规则 | R11.6、总角120°；全部同 Boss OID、存活、可选、未进行任何读条、目标可解析时启用 |

顺劈方向实时取当前目标，显示始终为实际R11.6轮廓。非目标避开实际锥；目标本人避开朝向其他存活普通队员的R100方向锥。提示保留源的位置量化差异。任意未配置技能的原始读条同样使顺劈暂时隐藏，不能只检查已登记技能。

Boss固定priority1；对应蜂 OID1763/1905 固定priority2，不附加仇恨条件。禁DoT仅保留独立意图与提示，按总迁移规格暂不新增禁DoT执行；没有把它变成全面禁攻。蜂标记已消费。Leafstorm与919预测逐帧读取当前原始结束钟，保留NPC饱和结束延迟；不要将其改成过去的静态预测。

20层场地复用源 `SharedBounds.ArenaBounds2090110` 的正24边形减入口矩形；120层复用 `ArenaBounds120130` 的精确凹轮廓。未拉战只画场地和Boss标记，场外标记夹到最近边界且画轮廓，不产生导航危险区。各自与实际源程序集的 `Contains` 作5772点比较。

## 100 层 Nybeth

### 激活机制

仅激活五条动作及CorseAdds：Abyss6872、Catapult6878、Doom6875、Shackle6874、SummonDarkness6876。6873/6877/6879仅为源枚举，不据此增加机制。

Catapult为R6；Doom为R47.4、总角120°普通锥，不附加凝视或Doom状态；Shackle为前长52.4、全宽8。SummonDarkness只有源文字，不按攻略里的90/65/40%阈值产生阶段，也不自动使用Resolution/Lust。

Abyss使用 `BaitAwayCast(module, aid, 6f)` 的实际默认值：圆心在原目标、R6、真实Finish结束、默认伤害类型Raidwide。它只预测active目标mask，不等于全队伤害或Tankbuster。

- 记录来源、目标的原 Actor 代际及施法；目标位置动态更新，目标死亡仍保留。销毁冻结最后位置，同ID新对象不重绑旧记录。
- 来源死亡仅隐藏输出，仍保留存储项；来源销毁先触发Finish，按来源删除首项。
- 本人按ID显示轮廓，其他人填充；导航按原引用判本人。旧代目标被同ID新代替换后，可能本人轮廓但导航仍避旧代，这是源比较方式的结果。
- 非目标避目标R6；目标本人避其他存活普通队员R6；来源与目标同一引用时不加目标专用禁区。
- 预测取active目标的当前队伍槽，离队目标不回退冻结旧槽。时钟取首条stored bait，即使其来源已死；效果和超时不清除记录。
- packet→raw更正不会重复创建。raw确认后，同代迟到packet不能重新指定目标；真实raw身份变化仍结束旧记录、创建新记录。原始模型保留施法身份，几何只是其输出。

CorseAdds只增加源180A/1809/180B的敌人标记，不改变攻击优先级、禁攻或自动道具。源bait Interesting玩家标记未被旧MP桥消费，未借迁移扩大消费范围。

### 构造攻略及模块实例

源构造函数激活EncounterHints，首次pull的阶段入口关闭它；普通脱战、个人死亡、战斗Reset或单机制开关不能重新激活已消费攻略。`PalaceGuideModule` 与 `DungeonPalaceGuide` 保留这一具体实例状态，与战斗输出清理分离。

模块创建由权威对象Added驱动；相同OID的其他对象先pending，同ID仍loaded时只重绑Primary。以队伍0槽和默认500码加载距离处理pending晋升与非active远离重建；活动模块不因离远而卸载。实际缺席跨过更新后、或活动阶段因死亡/销毁/HP0结束后卸载，旧实体恢复血量不会自动重建。未开战死亡与已有活动阶段结束分别处理。

Director0005只卸载loaded并暂停模块更新，不清pending；0011只恢复更新，不扫描旧实体创建owner。wipe期间真正Added或pending晋升仍可拥有构造攻略。脚本已暂停时仍消费该Boss所需Director生命周期；Seek/整脚本重启重新构造实例，owner停止清理输出。

生产快照不表达帧内同ID移除并重建的两个独立原生操作；若同一次快照仍持续包含该ID，则与原Feed一致不会虚构一次销毁。特殊测试构造的 `SpawnIndex=-99` 不在当前生产Feed输入内。

场地44项（含闭合重复点）直接提取自源 `vertices`，中心按原边界包围盒计算，保留凹角；实际源程序集5694点 `Contains` oracle一致。不得换成近似圆。

## 80 层 Gudanna

来源为 `DD80Gudanna`、`Components/Voidzone.cs`、`Components/CastHint.cs` 及 `AIHints.GoalSingleTarget`。仅四个动作接管读条：7096 R6、7098 R51.6/总60°、7095 R16.6/总120°、7099纯CastHint。7164/7167只在源枚举中，不能额外做读条圈；7099不是RaidwideCast，不额外创建空间危险或伤害预测。Tornado OID18F0全部保留，死亡、不可选及EventState7均不筛；原位置R10既画实际范围，也作为持续硬障碍，真实销毁才撤销。

`DungeonGudanna` 仅由完整native 7095/7096/7098效果推进八段循环：Thunderbolt、Charybdis、Thunderbolt、East、Charybdis、Thunderbolt、Charybdis、West；恰八次归零。7099效果锁存关闭后续循环；源文字里的10%并非代码阶段阈值，不按血量推测切换。Start、Finish、Cancel、时钟超时和legacy展开目标不推进；同序号的两个独立完整native包、两个零序号包分别消费，与源每次OnEventCast一致。

`PalaceGudannaPositioning` 保留原来源引用、当前索引/下一动作、真实AABB场心及精确0/0.5评分：Boss原位置R6内、场心R22外、场心X+20东半平面或X−20西半平面，边界均包含；R6不加hitbox。源构造攻略与Nybeth共用loaded/pending/Director实例生命周期，首次pull关闭，单机制设置/Reset不重显；结束旧Actor回血或0011恢复不会凭空新建模块。预战场地和Boss标记仅显示。轮转计数及Meteor latch存放在模块实例，普通输出Reset、脱战、观看角色死亡/缺失均不重建；实际卸载/新模块才归零。缺失视角期间继续消费全局事件和真实读条结束，个人控制仍由公共层撤销。

场地为源 `SharedBounds.ArenaBounds607080`：(-300,-220)、R24.94正48边形减中心(-300,-245.68)、半宽20/半高1.55矩形；真实中心取结果AABB。模型和实际DLL覆盖精确参数、真实效果/读条分离、原位置硬障碍、构造攻略、反向Seek与owner清理。

## 90 层 Godmother

来源为 `DD90TheGodmother`、`Components/Cleave.cs`、`Components/Adds.cs`、`Components/UnavoidableDamage.cs` 与 `AIHints.PullTargetToLocation`。7104为R7.2，7101为R8；7105/7102仅源Raidwide文字和普通活八槽预测，分别保留80%/99%原文，不生成50码躲避圆。7106只跟踪真实Finish，由创建预测提供R6.6，不重复读条圈。7100不建读条规则。

`PalaceGodmotherExplosion` 保留原来源代际、创建时量化位置及组件消费钟+10秒；拉战bootstrap以激活钟计时。后续移动、死亡、销毁或超过预计时间均不撤销无读条预测。同ID销毁重建另建新项，旧项冻结；任意来源的7106真实Finish/取消/打断全清整波。7106效果不清；施法中销毁按Finish→Destroy全清。已清波的现存爆弹不被下一快照重新补出。

首Giddy按持久对象创建顺序取，快照重排不换首项；真实销毁后继任，同ID新对象排尾。首项距Boss≤7.2时只禁止该目标，不要求施法、存活或可选；PR ACR禁攻及原生普攻停止复用公共 `Hints.SetActionRestrictions` 的owner/回放/开关门控。灰炸弹独立priority5/禁DoT意图，禁DoT执行仍暂缓，不变成灰炸弹全面禁攻。

`PalaceGodmotherPull` 保留原来源、目的地、目的范围2及坦克距离2。仅首Giddy已死、当前观看角色为Boss当前目标时存在；没有Giddy或非当前目标不产生。7104完整native效果才增加索引，不要求有MainTarget或命中目标；不消费legacy展开事件。四点严格为(-305,-240)、(-295,-240)、(-295,-240)、(-300,-235)，保留重复点及四项循环。每个完整包都消费，不以GlobalSequence去重。

Boss距目的>2时源评分点为 `destination - normalize(BossPosition-destination)*(2+hitbox+.5)`、R.5、weight10。当前模型保留该几何和权重，但不发布目标区，见下述消费边界。没有新增自动击退炸弹、挑衅或路线执行。

动态顺劈R11.75/总120°，取全部同BossOID的合法来源，当前目标决定方向。仅MassiveBurst7102或Sap7101真实活动期间同时抑制文字、显示和导航；其他/未知读条不抑制。显示与导航原点量化，文字判据用源原位置和量化玩家；目标本人避队友方向R100，非目标避实际锥。场地复用20层正24边形减矩形差集。

Godmother复用共享loaded/pending生命周期：Director0005当次清组件输出并卸载owner，旧InCombat快照和0011均不能重启旧owner；真实新Actor可重新加载。死亡/HP0结束后旧Actor原地回血不重启。挂起脚本仍接收该Director撤销。

## 60 层 The Black Rider

来源为 `DD60TheBlackRider`、`BaitAwayChargeCast`、`GenericKnockback`、`Cleave` 和 `VoidzoneAtCastTarget`。五个实际激活组件迁入原生；仅7157、7088、7089作为读条输入。7157为R7；7087/7179没有读条规则或估计轮转。

- Valfodr7089分为两个独立模型。`PalaceBlackRiderCharge`保存原来源/目标引用、代际、施法和判定钟，全宽6、前长为当前两者距离，方向随这两个实例动态变化；同ID新对象不替换旧引用。没有目标Actor时不建bait，但独立KB仍可捕获有效队伍槽。
- `PalaceBlackRiderKnockback`保存起读raw原点、原始目标ID及起读队伍槽、AwayFromOrigin、距离25、判定钟、撞墙和不忽略免疫。枚举注释的forward没有覆盖构造默认AwayFromOrigin。普通raw回调使用Feed提交Party前的旧名单，冷激活补播使用当前名单；负槽保留模型但拒绝个人输出消费。
- 击退由当前角色位置相对锁定原点计算，恰同点不移动。以真实48边形入口差集作射线首交点，距离内缩为`max(.5-.499, hitbox-.499)`；不能换成包围盒夹点。六种免疫保留三类覆盖槽和严格到期时间大于判定时刻，免疫只撤销位移预览/落点提示。
- 本人bait画轮廓、别人填充；非目标避实际动态矩形，目标本人避队友方向R100、半角asin(3/d)。来源死亡隐藏，目标死亡保留；显示按ID、导航按原引用分别判断本人。预测使用active目标当前槽及首stored bait钟，源死亡不将队首钟换成后项。
- KB独立方向禁区读取**当前主Boss位置**及所有Voidzone（含State7）/Infatuation caster原点，R100、半角asin(9/d)或asin(7/d)，使用KB判定钟；免疫不撤掉此组约束。非法asin项按源NaN消费忽略，不clamp扩大禁区。
- 击退落点提示读取全部7157与毒区cast/event/live AOE并检查场外，不按AOE预计生效时刻过滤。该反投影不增加导航；位移线及角色marker经现有`QuestKnockbackProjection`仅显示。
- 常驻Cleave为R11.92、总90°，任意真实读条（含未知动作）隐藏。显示/导航原点量化、方向使用raw目标位置，文字以raw源位置和量化玩家判断；目标本人避队友R100方向锥。

Hall of Sorrow7088是R9三路池：读条动态目标或Loc、真实完整native包的MainTarget当前位置（不存在用包落点）及EventState!=7实体。事件判定为当前消费钟+1.3d；不以GlobalSequence合并不同完整包，不消费legacy展开目标。预测不超时，每个有效实体每帧只消耗同OID、距离≤2的首项。实体AOE原点量化，死亡不代替销毁。真实Finish/取消/打断/施法中销毁撤销对应读条及bait，7089效果本身不清bait或KB。

## 70 层 Yaquaru

来源为 `DD70Yaquaru` 的Douse、DousePuddle、Electrogenesis和FangsEnd四个激活组件。Douse7091为R8三路池，事件延迟.8d，交接/时钟/完整包规则同60层；Electrogenesis7094为R8普通AOE。7093/Drench和7092仅为源枚举，不据此新增顺劈或Tankbuster。

`PalaceGuideModule.YaquaruPuddles`仅在首次活动阶段激活时捕获当时EventState!=7的1E9998原Actor引用；预战不提前捕获，后来新池不追加。捕获后移动更新、State7和死亡不移除，销毁冻结旧引用，同ID新对象不换绑。主怪raw位置进入任一捕获池R(8+hitbox)时，仅显示主怪当前hitbox危险轮廓；当前Boss目标是观看角色时保留拉出池文字。

`PalaceYaquaruPull`保留来源、全捕获池、TankDistance默认2和精确评分：每池半径`9+2*hitbox+TankDistance`，**全部池并集之外严格Distance>0**得1000，其余0。显示不是该22.5码评分圈，评分不是硬危险。旧MP的`ConvertGoalZones`还以正分格为分母做65%覆盖过滤；纯0/1000有正值时filledCount==cells必被过滤，因此原生保留评分模型但不发布新GoalArea、不顺手修转换器或新增自动拉怪。该结论只针对当前实际组件组合。

`PalaceYaquaruHeavy`保留ShouldCleanse四槽锁存：Update只看Raid0–3，按最低原生Status Slot取首Heavy14，剩余严格>8秒置位；≤8、到期、死亡、切视角不自动清位。真正Lose按回调当时队伍槽清位，同状态槽换来源同样先Lose；Feed所有销毁/状态Lose先完成，再按新名单统一Update置位，避免误清同槽新人。原始StatusSlots实时与回放透传，不用按到期过滤后的状态ID集合或最大期限替代第一份。网络Lose只暂排相应丢失副本，权威快照变化即确认，不能压掉仍存在的另一份Heavy。旧MP桥没有ShouldCleanse实际驱散消费者；当前仅保意图与诊断，不接入新的自动驱散。

60/70复用loaded/pending、主怪死亡/销毁/HP0结束、0005卸载及0011仅恢复门控；没有新增构造攻略文字。捕获池、Heavy和击退输入随实际模块实例，输出重置不重构仍存在的bait引用。预战只显示真实场地与Boss；独立楼层Zone仍保留，不计入这两个Boss完成数。场地复用`SharedBounds.ArenaBounds607080`，当前普通AOE、bait及明确的方向约束经`DungeonOutput → SafeZone`；没有导航或原生执行的源分支保持原范围。

## 弱站位意图与现有导航消费边界

`MechanicHintsConvert.ConvertGoalZones` 累加同一点正分后，仅总峰值≥100才转换目标区。Gudanna当前仅0.5、Godmother当前仅10，单独均不被旧MP桥消费。原生保留精确意图和完整参数，不以整数priority1、放大权重、虚拟大圆、硬危险或直接移动冒充等价迁移；两项当前不发布GoalArea。该结论只针对本Boss现有组件组合，不推广成任意低权重单项永不参与其他强目标的总和。

普通危险区、动态顺劈和Tornado硬障碍仍经 `DungeonEncounter → DungeonOutput → SafeZone` 提供当前导航；显示经同一预览注册表提供雷达/世界数据。回放只读模型、显示和规划，禁攻、面向、移动及原生动作不执行。真实PR控制、GreenMove、最终游戏像素与对象事件时序仍需实机验收。

## 验证与执行边界

模型回归覆盖精确参数、普通/未知读条、目标代际、死亡与销毁、当前队伍mask、原始NPC时钟、迟到取消、效果/Finish区别、指南与loaded/pending生命周期、wipe与owner撤销。实际DLL专项覆盖场地oracle、顺劈和Abyss双视图数据、生产导航区域、选敌和独立DoT意图、预测、真实结束、逆向Seek及楼层会话共存。

离线回放不提交面向、PR ACR限制、移动或原生动作。历史20/100/120层批次没有新增特殊位移、全局停手或凝视执行；蜂的禁DoT执行按总规格仍待支持。世界绘制最终宿主像素、真实游戏对象时序和移动效果仍需实机验收；离线数据和区域验证不等于已完成实机执行。
## 2026-11-25：80 / 90 层集中验证证据

- `native-palace-pair-model-lifetime-final.log`：505个集中回归场景通过，覆盖死宫、天宫、假面狂欢、HW、任务、视角和控制；新增Boss有10组定向模型。
- `native-palace-pair-build-lifetime-final.log`：生产0错误、4项既有Crucible警告。
- `native-palace-pair-replay-lifetime-final.log`：实际DLL五Boss专项通过；80/90各5772场地点与实际源程序集Contains一致。覆盖双视图数据、真实Danger/HardObstacle/GoalAreas消费边界、独立native包/legacy排除、预测保留/实际Finish、死亡/缺视角计数、Director撤销、反向Seek及owner清理。
- `native-palace-pair-catalog-lifetime-final.log`：最终实际DLL全目录退出码0，889条以`PASS `开头的检查输出；252个内置入口。检查输出行数不等于独立副本数。

以上日志位于`.tmp/halatali-verify/`。只统计当前离线证据，不把预览当实机执行，不增加道中流程自动化。

## 2026-11-25：60 / 70 层最终验证

Heavy网络Lose账本逐副本保存原SlotIndex/来源；连续Lose不相互覆盖，Gain只释放对应来源，确认后一份移除不使前一份旧状态复活。全部Lose处理完才按新Party0–3置位；缺槽号录像按原数组位置兼容，不宣称恢复真实槽。

BlackRider在输出Reset后从幸存模块恢复真实已消费raw基线；没有当前视角时也恢复Active和原始收据，确保同帧只换目标仍按真实Finish→Start捕获新目标/KB槽。首raw即使已超过原deadline仍保留float饱和NPC余量，后续相同raw不续期。

- `native-aglaope-palace-model-first.log`：死宫14组与Aglaope8组共22组定向通过。
- `native-aglaope-palace-model-final.log`：修正后687个集中模型场景通过。
- `native-aglaope-palace-build-final.log`：生产0错误、4项既有Crucible警告。
- `native-aglaope-palace-replay-final.log`：实际DLL两Boss各5772点源场地对照、真实方向约束与毒圈解算、双视图共享数据、逆向Seek及owner清理通过。
- `native-aglaope-palace-catalog-final.log`：同一生产DLL全目录回放退出码0。随后强化Aglaope的超2.1秒等待及活动owner撤销专项也通过；不把输出行数当副本数量。

254个内置入口；楼层会话共存不计Zone完成，禁止深宫道中流程自动化。普通导航已通过生产组件离线验证，特殊移动仅按源实际消费范围适配，真实宿主执行和最终像素待实机。
