# 寻宝原生脚本

2026-11-24。来源为 `MechanicRuntime/Upstream/Modules/` 下对应寻宝模块及公共 `TreasureHuntTemplate`、`GenericAOEs`、`UnavoidableDamage`。生产入口继承 `DungeonScript`，机制经 `DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints` 输出，不读取 BMR 会话结果。只按区域和已迁主对象 OID 接管，其他同区域模块保留原桥。

## 巡梦金库Vault Yan与Hexapod

来源为`Dawntrail/TreasureHunt/VaultOneiron/VaultYan.cs`、`Hexapod.cs`及实际激活组件。沿CFC1060/Territory1279与既有方场；新增15组件/16规则，寻宝累计71/71模块、628组件、871规则。寻宝模块类型已全部原生接管，不代表游戏内执行已验收。

Vault Yan使用三类袋的实际OID及创建顺序、精确坐标FIFO塔、奖励优先级和All生命周期。Rush保留起手source位置/目标落点/二维方向与长度、读条代际及真实结束；同代移动不重锁原点，共线终点更正仍更新长度。选袋排除Rush危险并在同分时取先创建者；删除后复用字典槽不能改变顺序。ObjectCreate的packet身份/位置在对应新OID快照确认前保持，旧OID或暂缺快照不能销毁新袋；真实Remove清除屏障。没有新增开袋交互执行。

Hexapod保留真实Pole正负状态和连线、四方向强制行进及Brainjack状态参数/到期时间、Bomb要求和矿实体硬障碍。首次接管按Actor原序处理“创建→读条→状态”，不能预先采完所有玩家状态。状态只在真实Lose/占槽改变时移除，过期但仍占槽的非零Param不归零；packet Lose屏蔽尚未更新的旧raw，event-only Gain同样生效。当前视角死亡即时清Bomb，原地复活且raw未变化不复活旧要求。Bomb及迷乱保持源/旧MP诊断范围，不据暂停或箭头宣称新增移动执行。

生产模型位于`DungeonTreasureYan`、`DungeonTreasureHexapod`和实际复用的`TreasurePlayerStatusState`，经`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`适配；普通AOE/站位/矿障碍提供导航，强制行进保留类型、方向、时间与状态撤销模型，未扩自动执行。Adapter的2161–2164/4551/4552白名单及回放StatusParams占槽语义有实际DLL反例验证。显示、当前实际导航、取消/代际、切人、反向Seek和owner撤销均离线覆盖，真实宿主执行与像素待实机。

独立审查补齐跨OID旧帧、Rush只改长度、首次Actor顺序、过期非零Param、packet正负状态与死亡复活。证据为`.tmp/halatali-verify/native-yan-hex-review-focused-fixed.log`（11条PASS）、`native-final-hunts-model-slot-final.log`及`native-final-hunts-catalog-final.log`（536条PASS）；最终输入修正再以`native-final-five-focused-final.log`定向验收。与本批三个狩猎共用编译和集中回归。

## 巡梦金库Old Bitter Eye与Mobile Fortress

来源为`Dawntrail/TreasureHunt/VaultOneiron/OldBitterEye.cs`、`MobileFortress.cs`及实际激活基类，沿CFC1060/Territory1279和既有方场半边19.5入口。新增23组件/36按模块规则；寻宝累计69模块、613组件、855规则。

Old Bitter Eye主48AA：43675 R11、43678 R40/全60°、43683落点R10、43681前20宽7、43685前19宽7；43677仅带施法者名的打断文字和Raidwide预测，不画R30、不新增自动打断。43682为动态前65宽8诱导死刑，Finish/取消保留，真实Native效果按source删首，来源死/销毁或目标死亡清理。43684建立独立径向50拉近模型，锁LocXZ/F、Toward/min4.9、不叠双方hitbox、不截墙、Shape=null、尊重严格晚于F的免疫；`TreasureRadialKnockback → DungeonTreasureKnockbacks → DungeonOutput`仅发布双视图位移及落点文字，不扩导航或执行。

43684 Start全局覆盖R13/F+4.6预告，43680 Start覆盖环5–40/F，43676 Start与43684 Finish不清。43683 Finish仅当XZ精确`(99.99231f,99.99231f)`时保留旧槽；其他位置覆盖环5–40，判定遵循源`CastFinishAt(oldCast,7.1)`。已核本机43683 `ExtraCastTime100ms=0`，旧读条Elapsed在TotalTime封顶，下一判定为Finish消费钟加`total+.3f-min(elapsed,total)`再加7.1，不把任意迟到结束恒定当旧F+7.1；提前取消仍保留正剩余时间。任意43676/43680真实Finish清全局预测，超时不清。

Mobile Fortress主48B2：43744 Raidwide、43746 R6、43742分散显示R6/导航R7；43748动态前46宽10诱导死刑和真实Native效果结束。Carpet43734–37锁落点/旋转、前10宽40，初显示2/危险色与Risky前1；≥4项每次追加按源快排，Update恰8改4/2/2，降7保留，任意43737 Finish恢复2/1/1。Quiet43732同几何按插入前缀最多4；`F>=firstF+3.5`截止，`i>1 && rotation==rot0 && rotation==rot1`才触发方向截止；源单项会读第二项，MP保留缺历史录制中的已知首矩形，未复现越界异常。Systematic43739–41前10宽10，初Risky=false，恰16才排序且显示9，否则全显示；显示项中`F<firstF+1`单向升Risky，count>7时单向升黄色；之后数量降7不恢复。对应任意Finish删全局首项，不匹配source，Native效果不推进。

两模块完整奖励7技能及All/优先级/洋红标记沿源；Bitter的Eyeclops保活、普通色/priority1，五蔬菜7..3、Cat3/Keeper2；Fortress五蔬菜6..2、Cat2/Keeper1。模型位于`DungeonTreasureBitterEye`、`DungeonTreasureFortress`和公共bait/knockback，经`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`适配，无栅格或寻路内部依赖。

- 406集中模型：`native-vault-fortress-model-verified.log`；早取消/晚封顶补验：`native-vault-fortress-clock-model.log`。真实运行库NPCRemainingTime与本机技能表oracle：`native-bitter-source-clock.log`。
- 生产0错误/4既有Crucible警告：`native-vault-fortress-build-final.log`；实际DLL专项10PASS及全目录520PASS/0失败：`native-vault-fortress-boundaries.log`、`native-vault-fortress-catalog-verified.log`。
- 显示、当前实际导航、PR意图、取消/代际/反向Seek/owner撤销离线已验；回放不执行PR、GreenMove或原生操作。宿主真实执行及最终像素待实机。证据均在`.tmp/halatali-verify/`，入口213个。

## 巡梦金库Paeonia与Spirit of Thunder

同CFC1060/Territory1279/方场，来源为`VaultOneiron/Paeonia.cs`、`SpiritOfThunder.cs`及实际激活基类。新增19组件/35按模块规则，累计67模块、590组件、819规则。

Paeonia主48AE：43701/02默认10000凝视、双视图眼睛和PR面向；43714分散R6/导航R7、43704 R6、43715 Raidwide、43716当前目标单体。43706/07 R40/全45°，expected8才排序、前四全Risky。43710→43712为圆10→环10–30，43713→43709为反序，两队列独立锁LocXZ/F、逐轴≤1/default旋转匹配、真实Finish消费钟+2；跨source与不同caster旋转不能阻止匹配。五奖励正确使用32301–05/R7，48AF苗普通标记/priority1及All保活，五蔬菜6..2。

Spirit主48B5：43773/74前20宽10、expected8前四；43778–81前20宽20、expected4前三、总数>1时首黄、全Risky且独立列表；同一source真实Finish可分别消费两组件的首匹配记录。43776 R30/全30°，43786 Raidwide、43782分散R5/导航R6，43784目标中心R6动态Finish-only死刑，目标死亡保留、销毁冻结且同ID新代不接替，Native效果不清。48B6 Created按原位置量化及原旋转锁前40宽10、消费创建钟+7.7，全部六条仍全显示/导航；死亡/销毁/效果/到期不清，任意43788 Finish全清。All不含portal，七奖励R7/全套技能和优先级沿源。

输出复用`DungeonTreasureIce`、`DungeonTreasureVaultGroups`、`DungeonTreasureBeams`及公共gaze/bait/party，经`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`消费；没有新增原生动作。398集中模型、生产0错误/4既有警告、实际DLL全目录511PASS/0失败见`native-vault-flowers-model-final.log`、`native-vault-flowers-build-final.log`、`native-vault-flowers-catalog-final.log`；后续520集中补强预测目标/类型/独立F5/F9时钟、真实Native效果保留。显示和当前导航离线已验，宿主执行/最终像素待实机。

## 加加财富天坑Golden Molter与Room 1

来源为`Dawntrail/TreasureHunt/CenoteJaJaGural/GoldenMolter.cs`、`Room1.cs`及实际激活基类，沿CFC993/Territory1209和既有入口。新增26组件/37按模块计规则（跨模块29个唯一AID），寻宝累计65模块、571组件、784规则。

Golden Molter主OID4306：38274单体、38275全屏预测；38267 R40/全180°、38273 R5、38580分散显示R6/导航R7；38271 R40/全60°插入前四全Risky、总数大于2时前二黄，隐藏第五项晋升；38335/36/37独立R6组；38279 R11、38278 R15/全120°、38277 R6和五奖励R7。38265/66真实Start置隐藏，任意已知Actor真实Lose3997清隐藏，Gain/Finish/效果/预计到期不清；逐Actor Cast→Status顺序保留。隐藏时使用原Primary动态位置发布Safe R9轮廓和进入9码硬约束；Primary销毁且奖励保活时冻结旧引用，同ID新对象不重绑；销毁占用3997仍先产生Lose。Sprint保存action3/High4000/目标身份意图及诊断，旧桥无执行消费者，不新增原生疾跑。

1EBCB8创建池锁量化位置，R17、default判定钟；任意已知Actor的完整ushort state恰为4删全局首圈，0x0104和未知来源不清，独立重复4继续消费。移动/死亡/原生state7/Destroy/Finish/到期均不删圈。创建/Destroy收据阻止旧快照复活。主怪标记允许dead/untargetable，奖励All、洋红和6..2/Alpaca2/Uolon1优先级沿源。

Room1主控制OID1EBAA4，12种trash存在即启动，含死亡/不可选/非战斗；奖励独自不启动，控制对象不画标记。仅真实原生EventState7或Destroy结束；网络EObjSetState7不改原生状态。场地为36边形、三对斜矩形并集减入口，从源生成并使用最终AABB中心；实际DLL3186点Contains/clamp对照通过。普通扇、矩形、R5/R6圈及奖励共17规则；38281保留动态source/target引用、全宽3冲锋、本人队友禁向asin(1.5/d)、Tankbuster预测，Effect不清、Finish按source消费；目标死保留、销毁冻结。未激活38280吸引等不扩输出。

机制由`DungeonTreasureMolter`、`DungeonTreasureCreatedCircles`和既有分组/诱导模型维护，经`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`适配。显示和当前确实提供的导航已离线验证；回放不执行PR、GreenMove或原生动作，真实宿主执行和最终游戏像素待验。

- 集中模型390项PASS，包含狩猎状态输入消费者及Narkissos三条旧raw屏障补验：`.tmp/halatali-verify/native-cenote-final-model-authoritative.log`。
- 生产0错误/4项既有Crucible警告：`native-cenote-final-build-authoritative.log`；实际DLL全目录505条PASS/0失败：`native-cenote-final-catalog-authoritative.log`。新增历史Lose/新占槽/Primary销毁反例专项另见`native-cenote-final-focused-complete.log`。入口仍213。

## 育体宝殿Acheloios、Hippomenes与Narkissos

沿CFC909/Territory1123、中心(100,100)/R19的48边形及既有`ShiftingGymnasion`入口续迁。来源为`TheShiftingGymnasionAgonon/GymnasiouAcheloios.cs`、`Hippomenes.cs`、`Narkissos.cs`及实际激活基类，共30组件/50消费规则（Hippomenes含六个真实效果动作）；寻宝累计63模块、545组件、747规则。

Acheloios双锤32284/32281按源绝对角89.999°/−90.004°建立两项R30/全180°，用`degrees*(MathF.PI/180f)`保留源float运算。LR负→正、RL正→负，判定F+.8f/F+3.7f；全部显示、仅首导航，多项时首红。32859/32860/32861任意真实Finish推进全局首，不匹配来源。四锤32283/32280与32858为两个独立待配对槽，±90°增量、零角哨兵，原点取最后配对Actor原始位置，首时钟取最后配对F+.8d，间隔(double)3.3f，四击最多显示两项。32858/32860/32861 Finish按消费钟+间隔推进；32860/32861同时推进双、四锤。同代补正更新剩余项，不能复活已消费队列或配对槽。

Hippomenes33164的两击为[0,π]；32342/32343/32344分别为[0,π,+π/2,−π/2]、[0,−π/2,π,+π/2]、[0,+π/2,−π/2,π]。锁Charge LocXZ/Rotation，每项R30/全270°、F+6+3i；仅首显示/导航，余项>1时另发布仅导航R5中心外补集。32345/33165/32346/32348/32349/32347 Native效果不匹配source，消费全局FIFO；Finish、销毁、预计到期不清。

Narkissos32336 Sap为显式红色R8；真实Start累计，补正不加，任意Sap Finish只清全局文字而不清其他来源圈。前四次文字`Circles resolve before cross`，第五次起加`, aim forced march into cross`。32330动态单体、32331全屏预测及临时迷乱文字、32332默认10000凝视及PR面向、32333 R8、32339十字长25/半宽3、32338 R25/全90°、32340 R6显示/R7分散避让、32317 R6均原生；未激活32334/32335/3467不接管。

强制行进复用`HuntMarchState`保留原目标身份、1958/1960/1959/1961四方向0、π/2、π、3π/2、按Expiry排序、每段2秒×6.6=13.2；1257/3629按当前朝向及剩余时长先投影，不截墙、不按预计到期清pending。只检查末段落点是否命中当前Sap，不查十字、场外、中途线段或其他AOE，也不比激活钟。状态对账保留真实Gain/Lose及原始占用slot，避免事件先到、同刻替换、小Expiry补正、销毁旧帧和已Lose代际返回导致重复/复活。未知Expiry等待真实时钟；真实新代可恢复，切人/Seek按身份重建。1422/2936/3694/3909 Gain置迷乱bit、任意Lose清bit，不按timer或多状态OR处理。旧MP桥SpecialMode只有诊断；本批保持诊断和行进双视图投影，没有增加自动选朝向、强制行进反推导航、Hold或迷乱原生控制。

模型集中在`DungeonTreasureGymnasionSequences`、`DungeonTreasureNarkissos`及实际有消费者的状态账本，经`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`适配。普通AOE、首项/中心约束、分散和凝视的显示/当前导航/面向意图离线已验；回放不执行PR/GreenMove/原生操作，真实物理、最终像素与宿主执行待实机。

- 锤序214模型、精度补验22专项与487全目录通过：`native-gymnasion-hammers-model-initial.log`、`native-gymnasion-hammers-precision-focused.log`、`native-gymnasion-hammers-precision-catalog.log`；独立角度/时钟oracle、四种Blaster顺序、六Native效果、正反锤及32859单独推进均覆盖。
- 最终231集中模型、生产0错误/4项既有Crucible警告、Narkissos实际DLL专项10条及全目录495条PASS/0失败：`native-narkissos-model-final.log`、`native-narkissos-build-final.log`、`native-narkissos-focused-final.log`、`native-narkissos-catalog-verified.log`。证据位于`.tmp/halatali-verify/`，入口仍213个。

## 梦羽宝殿Cladoselache与Typhon

来源为`TheShiftingOubliettesOfLyheGhiah/SecretCladoselache.cs`、`DaenOseTheAvariciousTyphon.cs`和`Components/RotatingAOE.cs`。沿CFC745/Territory924、中心(100,100)/R19的48边形，新增19激活组件、21读条规则；寻宝累计60模块、515组件、697规则。

| 模块 / 主OID | 实际激活参数 |
|---|---|
| Cladoselache / 3027 | 21705 R40/全60°；21704 R13；21703当前目标单体；21708 R8分散；21709动态宽5冲锋；21706/21707六击旋转；Keeper三动作 |
| Typhon / 301A | 21689 R12/全90°；21771 R8/全120°；21772前40/宽6；21697落点R6；21695实际R20；21688精确八人R6；21690径向20码撞墙；创建中心圈和双旋转；五蔓德拉R6.84 |

`TreasureRotatingSequence`保留来源/代际、原点/旋转锁定、增量、初次/下次判定、间隔、剩余击数及显示上限；`DungeonTreasureRotations → AmbientAoes → DungeonOutput`集中适配显示和导航。Clado的21706 Start与任意Actor图标167/168以两个单槽配对，任意顺序；未知图标清增量、恰0朝向不创建，原点取最后配对调用的Actor量化位置。每次追加六击R40/全60°，最多显示两击；任意来源21706/21707原生效果推进全局首序列，下一击取真实消费钟加精确2.1秒。Finish、销毁和预计到期不清；同代补正不能复活已消费槽。

Typhon创建3051覆盖场地量化中心R5单圈，判定为消费创建钟加14.3秒，任意21691效果清圈，21782不清。创建3082/3050各追加初角及初角+π两条序列，增量分别+6°/−6°，每条31击、最多9项，初判定为创建钟加14.5秒、间隔精确1.1秒。21781按当前效果来源位置逐轴≤1、归一角差≤.05匹配首序列推进，不按source ID。21695读条期间全部创建圈/旋转的显示、导航和文字隐藏，效果仍推进，结束恢复原缓存及旧时钟；21691只清圈，不清旋转。创建输出不因死亡、销毁或超时清除，创建收据/显式Destroy旧帧屏障阻止快照复活。旋转缓存仅在序列数/真实消费变化时重建，future从max(NextActivation,消费钟)逐次加间隔，current保留原判定；全部Risky，当前多击时红色。

21709保留原source/target引用，动态二维长度及宽5，目标死亡保留、来源死亡隐藏/复活，实际Finish按来源删首，Effect不清；本人导航按队友方向及半宽2.5计算，保留Tankbuster预测，不添加未提供的cleave文字。21708为R8显示/R9导航；21688为精确八人R6，不增加默认预测。21690由既有`TreasureRadialKnockback → DungeonTreasureKnockbacks → DungeonOutput`保留来源/代际、Start位置、FinishAt、20码Away、墙截断和严格免疫，仅显示/提示，未扩成击退导航或全屏伤害预测。

All分别为`[3027,3028,3033,3034]`和`[301A,3024,301D..3021]`；Clado优先Fuath3/Keeper2/Shark1，Typhon五蔓德拉6..2/Menace1，奖励洋红。301B、创建体和helper不保活。未激活21687等视觉/注释行为不扩接管。

- 集中模型204项通过，修复21706经公共StartCast入口丢失真实朝向的失败项后单跑7项通过并集中复验：`native-clado-typhon-model-rerun.log`、`native-clado-typhon-model-final.log`。
- 生产0错误/4项既有Crucible警告：`native-clado-typhon-build-final.log`。实际DLL专项11条PASS（含公共输入2条），全目录476条PASS/0失败：`native-clado-typhon-focused-final.log`、`native-clado-typhon-catalog-final.log`。
- 模型、双视图预览、当前确实提供的普通/旋转/诱导/分散/分摊导航、隐藏推进、切人/Seek和owner撤销均离线覆盖；回放不执行PR/GreenMove/原生动作。真实游戏像素、宿主移动和击退物理仍待实机。证据均在`.tmp/halatali-verify/`，入口仍213个。
- 后续独立复核补正：用独立待配对bool区分零角未配对与已消费槽；零→有效同代补正可配对，已消费不能复活。R8/R9/R6、本人分散/分摊、真实20码撞墙及免疫恰等于F/+1tick边界补强，已包含487及最终495全目录集中回归；保留上述476条原始证据。

## 梦羽宝殿Keeper、Ultros、Echidna

来源为`Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/SecretKeeper.cs`、`DaenOseTheAvariciousUltros.cs`、`DaenOseTheAvariciousEchidna.cs`及各自实际激活基类。沿CFC745/Territory924、中心(100,100)/R19的48边形和既有入口，新增33激活组件、42展开规则；寻宝累计58模块、496组件、676规则。未激活视觉、Keeper21770吸引及21678注释中的击退均不增加输出或执行。

| 模块 / 主OID | 实际激活参数与模型 |
|---|---|
| Keeper / 3023 | 21680 R11/全120°；21677 R20/全120°普通扇形及独立20码拉近；21681 R11；21679 R6毒区三路；21678全同OID常驻R12/全120°顺劈；Keeper三奖励动作21769/21767/21768 |
| Ultros / 3030 | 21751 R13/全90°；21754 R8；21755前20/宽10；21752 R11；21745落点R4；21750落点R10；21747 R8分散；21743当前目标单体；21749径向20码撞墙；五蔓德拉R6.84及Keeper三动作 |
| Echidna / 3032 | 21758 R18；21764落点R3；21757当前目标单体；21760精确八人R6分摊；21761固定凝视；21762/21760共享动态凝视；22797–22800 R40/全45°组；五蔓德拉及Keeper三动作 |

拉近与击退由`TreasureRadialKnockback`保留起手来源、代际、LocXZ/Rotation/F、距离、Toward/Away、shape、StopAtWall及MinDistance；`DungeonTreasureKnockbacks`负责当前投影，`DungeonOutput`发布双视图。Keeper的最小距离在Start缓存源槽0和施法者hitbox之和，不随观看角色、变身或补正重算；投影为`min(20, distance-MinDistance)`，先过滤严格免疫、shape及无有效位移，再限制首个成功位移。免疫不撤普通扇形；无拉近逆推导航，保留场外落点提示。Ultros按插入顺序串联20码并保留墙截断，落点仅检查21750水圈；每个水圈另从场地中心发布R20/全60°隐藏方向导航，取首个KB时钟，免疫不撤该导航。不凭注释新增21749伤害圆或全屏预测。

Keeper毒区复用`DungeonTrialVoidzones`：cast动态当前目标/LocXZ回退；Native MainTarget/包落点锁event、消费钟加精确`1.4d`；1E8FC7 live只筛EventState!=7，不筛死亡/可选，每实体每轮距离≤2只消首个event。Finish/取消仅清cast，预计到期不清event，全部是危险导航而非硬障碍。常驻顺劈枚举全部存活可选3023，读条中仍有效，当前TargetId动态；本人引导使用源八槽队友方向R100，其他人避R12。

Echidna动态眼由`TreasurePlayerGaze`保存唯一槽、Start来源/代际/F和原目标引用。任一21762或21760 Start覆盖，未解析目标也覆盖null；任一对应真实Finish全清，不要求最后来源，Effect不清。目标死亡保留，销毁冻结旧位置，同ID新代不重绑；补正仅可更新仍归属本代的槽，不能覆盖后来源或复活已清槽。同代packet目标补正不会伪造全局Finish/Start；21760分摊同时更新有效目标及Finish匹配，未解析目标收据可在改为不同有效目标时恢复，真实消费记录不复活。21760分摊独立于全局凝视槽，跨来源凝视Finish不删其他分摊。圈内人数/参与/重叠显式零容差，R6外的第八人不计入。分散与分摊都无额外默认伤害预测。

固定与动态凝视统一由`Gazes → GazeFacing → Hints`消费，保留默认10000范围、眼形/瞳孔/前90°弧、0.75秒面向窗口和PR朝向意图；不造地面危险区，不增加停手。VoidStream前4插入顺序立即Risky，达到99才按源右pivot严格小于排序；真实Finish删首来源并晋升，12项不提前排序，预计到期不清。

三者All分别为`[3023,3033,3034]`、`[3030,301D..3021,3033,3034]`、`[3032,301D..3021,3033,3034]`。奖励维持阶段，毒区/触手/helper不保活、不额外画敌人。五蔓德拉优先6..2、Fuath2/Keeper1，普通候选显式0，奖励洋红。

模型、双视图输出、当前确实提供的普通/分散/分摊/方向导航、面向意图、Seek及owner撤销已离线验证。拉近/击退沿源仅显示与提示的范围，不冒充已实现特殊移动导航或物理执行；回放不发送PR朝向、GreenMove或原生操作。最终像素、真实宿主面向/移动及游戏物理待实机验收。

- 集中模型193项通过：`native-keeper-daen-model-verified.log`，含14项新增来源/代际、精确边缘与全局槽反例。
- 生产0错误、4项既有Crucible警告：`native-keeper-daen-build-final.log`。
- 最终实际DLL全目录467条PASS/0失败：`native-keeper-daen-catalog-verified.log`；其中本批12项，覆盖完整普通规则、真实结束、双视图/导航、凝视补正/分摊一致性与回放隔离。独立核验发现的补正入口、分摊引用和零容差缺口均已修复；首轮VoidStream预览集合排序断言改为验证模型队列与实际第五项存在，不依赖显示注册表枚举顺序。证据位于`.tmp/halatali-verify/`，内置入口仍213个。

## 巡梦金库与运河祭殿

本机客户端表核对 CFC1060 ↔ Territory1279「巡梦金库」、CFC586 ↔ Territory794。入口分别为 `Presets/TreasureHunt/Entries/VaultOneiron.cs` 和 `ShiftingAltars.cs`。

| 源模块 | 主OID | 实际激活组件与参数 |
|---|---|---|
| Dawntrail/TreasureHunt/VaultOneiron/GildedSentry | 48B0 | 43721 R6；43719十字长50/半宽5；43728前75/宽4；43725动态单体预测；43723动态全队预测；43727前20/宽3；43726 R8 |
| Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/TheGreatGoldWhisker | 2541 | 13364延迟单体；13366仅Spawns adds提示；13365 R12.4 |
| 同目录 AltarMandragora | 2542 | 13367延迟单体；13370仅Calls adds提示；13369落点R3；13368 R22.85/全角90°普通扇形；6448–6452五动作R6.84圈组 |
| 同目录 Hati | 2538 | 13360环9–40；13363落点R10；13741前18/宽4；13362前45.4/宽8；13361 R15.4；6448–6452五动作R6.84圈组 |

四模块共21个激活组件，组动作展开后29条读条规则。普通AOE锁定Start落点、方向和判定时间，实际Finish/取消/施法者销毁撤销；预计时间过去及普通Effect不清理。43725每帧消费当前CastInfo目标及结束时间，self目标才回退caster.TargetId；43723只预测，不造全场危险圈。13368源仅激活普通扇形，13361未激活变身控制，13359未激活；不添加凝视、变身或额外控制。

Gilded场地为中心(100,100)、半宽19.5的轴对齐方场。其余为中心(100,100)、外接半径19的48边形，保留源Custom.ClampToBounds中心竖轴 `abs(offset.X)<0.1` 不投影例外。场外敌人画空心方向标记，方场逐轴clamp；源Vulnerable使用洋红，普通敌人红色。只画PrimaryActor那一实体及源All列表内的奖励/小怪，不画同OID第二个主怪。

All集合分别为：`[48B0,48B7,48B8]`、`[2541,2544]`、`[2542,255C,2546..254A]`、`[2538,2569,2546..254A]`。死亡或销毁均满足该实体结束条件；主怪已死而奖励怪存活时继续，HP0但未Dead不结束，helper不延长阶段。奖励优先级依次为Gilded的48B7=2/48B8=1、Gold Whisker的2544=1、其余两场2546..254A=6..2及普通add=1，全部其他合法候选显式0。Pointless抵抗setter，高差/FATE的源基础判断顺序保留。

### 延迟单体队列与输入身份

13364/13367 Start解析目标Actor并保存原目标ID、当时源队伍槽和activation。Finish、取消、施法者销毁和预测到期均保留队列；相同Action的Native Effect按真实MainTargetId删除第一条匹配原目标的记录，不要求同施法者。不是PR展开后的逐目标TargetId。阶段结束清全部。

预测消费当前占该捕获槽的成员，效果仍按原目标ID匹配。源slot=-1保留Tankbuster提示但没有mask预测。每施法者独立当前读条收据：同代packet更正更新尚未命中项，已命中项不因补正复活；真实raw Action/Target/Location/Rotation/TotalTime变化按Finish→Start追加历史项。合法替换提交后清旧结束墓碑，覆盖大幅时长变化后的再次目标变化。取消后快速重施仍是新代。

源队伍输入独立于PR角色槽：实时LocalPlayer在0，其余按PartyHelper原顺序有效ID去重、最多8；MP回放LocalPlayer优先，其余沿world.Party数组顺序截8。旧会话先处理actor/raw事件、帧尾更新名册；新会话先Bootstrap名册。这保留当前原桥的八槽消费范围，不宣称扩展到上游64槽或补全旧录像没有保存的席位历史。

### 验证与覆盖

- 模型：14个寻宝场景，集中相关模型238项通过；覆盖参数、组生命周期、优先级/颜色、场地、真实结束、raw/packet代际及延迟队列。
- 公共适配：生产DLL全目录297条PASS、0失败；寻宝定向包含双视图预览、危险导航、动态预测、源槽交换、FIFO、Seek和owner清理。
- 最后代际修正：生产重新编译0错误、4项既有Crucible警告；受影响raw模型1项及两个Delayable实际DLL场景单独重跑通过。
- 证据位于 `.tmp/halatali-verify/`：`native-treasure-model-batch.log`、`native-treasure-catalog.log`、`native-treasure-raw-generation.log`、`native-treasure-build.log`、`native-treasure-delay-final.log`。
- 显示与普通危险区导航已离线验证。四场没有源特殊移动、交互、停手或面向控制，不增加原生动作执行。实际游戏中的最终绘制和宿主移动仍待实机验收，回放不执行GreenMove或原生操作。

## 加加财富天坑：第2–4房

同CFC993/Territory1209，沿用Cenote入口。来源`CenoteJaJaGural/Room2.cs`、`Room3.cs`、`Room4.cs`。共32激活组件、56规则，寻宝累计45模块、381组件、516规则。

| 房间/主对象OID | 已激活动作与参数 |
|---|---|
| 2 / 1EBAA5 | 38296/38298 R8；38293前40/宽8；38290前14/宽4；38300前10/宽4；38299 R8/全90°；38295/38291 R10/全90°；38297/38302 R10/全120°；38301/38294/38277 R6 |
| 3 / 1EBAA6 | 38308前40/宽8；38307 R18/全60°普通扇；38303 R8；38304 R12/全120°；38310 R13/全60°；38313/38311/38277 R6；38306 R8/全90°；38312/38309 R5 |
| 4 / 1EBAA7 | 38286/38316/38314/38277 R6；38287前20/宽4；38317 R8；38319/38318 R8/全180°，方向直接用读条Rotation；38288 R10/全120°；38289前10/宽4；38315 R12/全120° |
| 三房共用 | 38279 R11；38278 R15/全120°；32301–32305 R7五动作组 |

`NativeCenoteRoomCatalog`保存完整trash及奖励列表；`DungeonEncounter.Synchronize`仅在对应房间trash存在时启动，不筛trash死亡/可选/战斗，奖励独存不启动。房间主对象同样不要求可选或存活；结束只取原主对象销毁或EventState7，不按trash全死、消失或主对象Dead结束，同ID其他OID不替代旧房间。只画trash和奖励，不画房间对象；五蔓德拉优先6..2、Alpaca2/Uolon1，其他0，七奖励洋红。38280 Inhale、38305 Proboscis、9630不接管，38307不扩击退。

普通机制复用`DungeonPendingCast → DungeonOutput → SafeZone/Draw`：每source保留Start代际、LocXZ锁点、方向、FinishAt及真实撤销。Effect和预计到期不清；短取消、销毁及实际Finish独立撤销。来源只提供普通AOE，无专属动作控制、特殊移动或额外单体伤害预测，不扩大执行范围。

场地按各源public静态`ArenaBounds`经`--treasure-cenote-rooms-arenas`导出为`NativeCenoteRoomArenas.cs`，不依赖运行时BMR。三场均保留36边形和六个斜矩形并集再减入口，49个真实轮廓点；源基准(0,192)/(160,19)/(-197,-145)，最终AABB中心Z分别187.78568/14.785681/-149.21432，不能套Bull场地或只用名义中心。

- 143项集中模型通过，生产编译0错误/4项既有Crucible警告，实际DLL全目录418条PASS/0失败；专项8条PASS含公共输入2条，每房3186点源Contains对照。
- 显示双视图与普通AOE导航已离线验证；独立时钟、真实结束、短取消、同ID换OID、切人、逆向Seek和owner清理有覆盖。无本批新增原生执行；最终像素及真实宿主移动仍待实机。
- 证据`.tmp/halatali-verify/native-cenote-rooms-model-final.log`、`native-cenote-rooms-build-final.log`、`native-cenote-rooms-focused-final.log`、`native-cenote-rooms-catalog.log`。首轮场地顶点数及圆形无意义朝向断言按真实几何修正，生产生命周期OID遗漏修复后集中回归通过。

## 梦羽宝殿Djinn/Worm与育体宝殿Lyssa

来源分别为`TheShiftingOubliettesOfLyheGhiah/SecretDjinn.cs`、`SecretWorm.cs`及`TheShiftingGymnasionAgonon/LyssaChrysine.cs`。Territory924/1123沿已核对的CFC745/909映射，复用各区域入口及源中心(100,100)、R19的48边形。三模块共24激活组件、28展开规则；寻宝累计48模块、405组件、544规则。

| 模块/主OID | 实际激活规则与参数 |
|---|---|
| Djinn / 300F | 21655落点R6；21657前40/宽8；21656前55/宽40；21653前15/宽4；21654全屏预测及25码径向撞墙击退；21769 R11、21767前13/宽4、21768 R15/全120° |
| Worm / 3029 | 21713落点R8；21711前46/宽4；21710动态单体预测；21715仅Finish消费创建圈；6448–6452 R6.84 |
| Lyssa / 3D43 | 32315仅Finish清创建柱；32316前80/宽4；32306动态单体；32314精确8人R6分摊；32296仅Calls adds；32309 R5、32317 R6；32310/32312启动两套同心序列，32311/32313真实Finish推进 |

Djinn的`TreasureRadialKnockback`保留读条来源/代际、锁点、FinishAt、25码Away、撞墙及严格晚于判定的免疫条件。多个来源沿插入顺序从上个落点串联，真实Finish/取消/销毁撤销，Effect和预计到期不撤销。通过`DungeonTreasureKnockbacks → QuestKnockbackProjection → DungeonOutput`提供雷达/世界位移线、角色落点和危险提示；源无击退导航，未增加自动位移。21657/21656的注释击退及21770未激活吸引不扩执行。

Worm的图标23消费事件Source Actor，逐条保存原目标引用及处理时钟+5秒，不按预计到期清理；任意21713 Start清全表，同代补正不再清新图标。动态R10诱导显示/导航与首目标中心R17.5导航独立；首目标文字`Bait bubble away!`替代base文字，主怪Dead隐藏普通诱导但保留首目标导航/文字。销毁冻结旧引用，同ID新目标不冒充旧本人。没有新增伤害预测或21714处理。

`TreasureCreatedCircle`保留Actor身份、创建锁点/处理时钟和判定；Worm bubble302A为+5.7秒R10，Lyssa pillar3D44为+3.7秒R6，均不筛死亡、可选或state7，销毁和预计过期保留历史。激活时已有对象补创建；显式Destroy清去重收据并建立旧帧屏障，真实Create/absence开启新代。任意21715真实Finish删全局首圈，任意32315真实Finish清全圈；Effect均不清，仍存活的旧对象不能重建已消费圈。

`TreasureIceSequence`保留原视觉读条、量化场地中心、F+0.5判定及阶段。32310为R10→环10–20，32312为反序，各自FIFO且每次32311/32313 Finish分别派给两组件，逐轴≤1匹配，下一段为消费时钟+2秒。源未传rotation，helper朝向不参与匹配；锁点不取visual落点。只显示当前阶段，同代补正只改未消费order0，活动视觉已推进后补正也不能复活首段。普通危险、创建圈和同心序列经公共门面导航；八人分摊复用精确容量/原目标FIFO、死亡隐藏/复活和owner清理，无额外Shared伤害预测。

All分别为`[300F,3010,3033,3034]`、`[3029,301D..3021]`、`[3D43,3D4D,3D4E]`。Djinn优先Fuath3/Keeper2/Tail1，Worm五蔓德拉5..1，Lyssa Lampas2/Lyssa1；奖励洋红、普通怪红。Bubble、pillar、helper不画敌人标记且不保活。主怪已死而奖励存活继续。

- 155项集中模型、补充活视觉已消费补正1项定向、生产0错误/4项既有Crucible警告；实际DLL专项13条PASS、全目录429条PASS/0失败。
- 双视图、普通危险及诱导/分摊导航已离线验证；包含独立时钟、全局Finish、显式Create/Destroy同ID新代、旧帧屏障、取消、切人、逆向Seek与owner清理。Djinn击退仅投影/提示已验，真实物理及宿主移动待实机。回放不发送原生操作。
- 证据在`.tmp/halatali-verify/`：`native-djinn-worm-lyssa-model-final.log`、`native-djinn-worm-lyssa-correction.log`、`native-djinn-worm-lyssa-build-final.log`、`native-djinn-worm-lyssa-focused-final.log`、`native-djinn-worm-lyssa-catalog-final.log`。同次集中复验覆盖Cenote持久房间顺序、非当前先完成、同对象完成不重开及销毁先于快照反例。

## 育体宝殿Pithekos、Styphnolobion、Meganereis

源`TheShiftingGymnasionAgonon/Gymnasiou{Pithekos,Styphnolobion,Meganereis}.cs`，Territory1123/CFC909、源48边形及入口沿上文。共26组件/39规则，寻宝累计51模块、431组件、583规则。

| 模块/主OID | 完整已激活规则 |
|---|---|
| Pithekos / 3D2B | 32216环14–30；32212落点R3；32217/32215落点R6；32211动态单体；32213雷球自身R18；32317落点R6；32301–32305 R7 |
| Styphnolobion / 3D37 | 32245动态单体；32246全屏预测；32253落点R6；32251动态R60/总30°诱导；32247创建R10→环10–20，32248/32249 Finish推进；32317落点R6；32301–32305 R7 |
| Meganereis / 3D39 | 32255动态单体；32259落点R10；32258径向20码撞墙击退；32262落点R8；32264前17/宽3、32256前27/宽6；32260 R8分散；32263全屏预测；32317落点R6；32301–32305 R7 |

Pithekos沿`DungeonTreasureBaits`保存Icon111的Source Actor原引用，每条处理时钟+6秒、动态R18，首目标另避中心R17.5，文字`Bait levinorb away!`替代base文字。任意32212 Start清全部，同代补正不清后来图标；死亡目标不删除，主怪死亡只隐藏base几何，首目标导航/文字保留。雷球3E90不在All，不另造常驻危险。

Styphnolobion沿`TreasureIceSequence`保存32247视觉来源/代际、量化场地中心和F+0.5秒；32248/32249真实Finish按order和逐轴≤1匹配，下一段消费时钟+2秒。源创建与推进都省略rotation，helper朝向不参与。仅一套正序，不复制Lyssa反序。32251沿`TreasureCastBait`动态跟随原source/target；本人避队友方向R100/总30°禁向锥，他人避真实R60锥。Finish/取消保留，Native Effect按source删首项且sequence去重；source死亡/销毁、target死亡永久删，target销毁冻结旧引用。预测仅目标、类型Raidwide、共用首项activation，不发布`Tankbuster cleave`全局提示。

Meganereis的`TreasureRadialKnockback`保留原始来源/代际、起手锁点、F、Away20、stopAtWall及严格免疫期限，多个来源顺序串联。落点危险只检查当前32259 Hydrobomb，不检查32262、分散或预计时钟。只要KB存在，对各Hydrobomb从场地中心发布R20/总60°方向导航，统一首KB的F；免疫只去位移投影，不撤方向禁区，首KB结束改取下一KB时钟。击退自身无源导航/全屏伤害预测，不能按注释新增R40危险圆。32260复用公共原目标引用及target FIFO，显示量化R8轮廓、导航R9，死亡隐藏/复活、销毁冻结，无额外伤害预测。

All共同包含各主怪、普通add（3D2C/3D38/3D3A）及六奖励3D4E..3D53，Meganereis另含3D4D；奖励洋红、普通红。五蔓德拉priority6..2，Lyssa2、Meganereis Lampas2，普通add1，其余0。主怪死亡而奖励仍活继续，helper及雷球不保活。所有未激活视觉、自动攻击和Telega保持不接管。

- 模型164项集中通过，生产0错误/4项既有Crucible警告；实际DLL专项16条PASS、最终全目录439条PASS/0失败。后者增加Cenote两项持久顺序及跨OID完成收据反例，同ID换成新房间不允许旧Active覆盖新完成收据。
- 普通AOE、图标/读条诱导、同心序列、分散与源方向禁区的显示/导航已离线验证；包含独立时钟、当前/未来阶段、真实结束、取消、死亡/销毁、切人、反向Seek及owner撤销。无本批原生操作执行；击退物理、真实宿主移动及最终像素待实机。
- 日志`.tmp/halatali-verify/native-gymnasion-trio-model-final.log`、`native-gymnasion-trio-build.log`、`native-gymnasion-trio-focused.log`、`native-gymnasion-trio-catalog-final.log`。

## 巡梦金库Gwyddneu、GreatGimmeCat、Roadripper、Fastitocalon

来源`Dawntrail/TreasureHunt/VaultOneiron/`四个同名源码，沿用已核对的CFC1060/Territory1279、世界中心(100,100)/半宽19.5方场及VaultOneiron入口。四模块32激活组件、51展开规则；寻宝累计55模块、463组件、634规则。

| 模块/主OID | 完整已激活规则 |
|---|---|
| Gwyddneu / 48A4 | 43645 R70/全45°前四；43648/43654全屏预测；43650目标R5诱导；43652落点R6；43656真实Finish清创建球；43727前20/宽3、43726 R8；32301–32305 R7 |
| GreatGimmeCat / 48AC | 43687/43688两腰20/全90°真三角；43692落点R5；43696 R5分散；43694全屏预测；43698目标R5诱导；43690真实Finish推进创建beam |
| VaultRoadripper / 入口48A1 | 43635落点R6、43636 R8、43637环3–12；43640动态冲锋宽8；43638延迟单体；43641 R40/全180°；43639全屏预测；43727前20/宽3、43726 R8；32301–32305 R7 |
| Fastitocalon / 48A8 | 43659–43662 R6组；43664创建R10→环10–20→环20–30，43664–43666真实Finish推进；43668目标R5诱导；43670落点R6；43672全屏预测；43727前20/宽3、43726 R8；32301–32305 R7 |

`DungeonTreasureVaultGroups`保留原读条身份/代际、锁点/朝向/F及插入顺序。Violet只输出前四，不排序；Prism达到8条、Uplift达到12条后每次新增按源右pivot/严格小于的原地快排，包含相同时钟非稳定顺序。Prism显示/导航前四；Uplift显示全部12或16项，各项独立在F−3后才进入导航和踩圈文字，总数>4时前四黄色，其余默认色。补正原位更新，不追加或重新排序，真实Finish按source删首条并晋升；预计到期不删。原生脚本在对象扫描之间继续推进Uplift风险时钟，实际DLL覆盖跨100ms扫描间隙。

`TreasureCreatedCircle`保存Gwyddneu的48A7/R16、48A6/R8创建锁量化位置和消费钟+7.9秒，不筛死亡、可选或state7，销毁与过期保留预测。任意43656真实Finish清全批；43655不新增普通读条圆或清理，但仅对活跃Gwyddneu的48A6源压制通用躲避，防止创建球旁路重复注册。创建收据按OID区分同ID换球类型，显式Destroy旧帧不能复活已消费代际。

`TreasureCreatedBeam`保存48AD创建身份、原点、朝向、创建与判定时间。原点为世界Z减21后量化，不能改为局部朝向偏移；锁创建朝向及消费钟+7.7秒。队列仅输出前四，每收到第四次43690真实Finish且至少四条时删首四；空队列和不足四条也累计次数。销毁、死亡及预计到期不删，后四晋升保留原时钟/位置。

Fastitocalon复用`TreasureIceSequence`，43664以自身LocXZ/F启动，仅显示当前段；43664/65/66真实Finish按阶段及逐轴≤1匹配第一条，忽略helper朝向，下一段为消费钟+3秒。单独后两段不造首段；已推进的活动首段补正不复活。

四种`TreasureCastBait`均由真实Finish按source删首条，Effect不清。三个圆动态跟随原目标中心，显示R5、本人避队友R5；RunAmok从原source到原target，动态二维距离作为矩形长、宽8，无hitbox/余长，本人沿队友方向R100/asin(4/d)避让。目标死亡保留；源死亡隐藏空间输出及预测、复活恢复，销毁冻结引用且正在读条时只消费一次Finish，不与正常结束双消费。四者保留首列表时钟的目标Tankbuster预测，只有三个圆带全局Tankbuster cleave。Wheel复用捕获原目标/槽位的延迟单体，Finish保留、Native MainTarget FIFO清理；43696显示R5/导航R6，死亡隐藏复活、目标FIFO，无额外伤害预测。

Gwyddneu All为主怪、48A5和七奖励；Fastitocalon为主怪、48A9炮及七奖励；Cat仅主怪/48B7。Roadripper的All仅48A1/48A2及七奖励，尸体仍保活，另由首个缓存48A3的死亡/销毁结束，不换绑同ID新代或后续同OID。奖励B9..BD依次6..2、Goldy2/Keeper1，Fastitocalon均高一级且炮1，Cat仅Goldy1；奖励洋红。球、猫环、helper不画敌人标记或保活。

- 179项集中模型通过，生产0错误/4项既有Crucible警告；实际DLL专项18条PASS，最终全目录455条PASS/0失败。旧Gwyddneu“未接管”断言随本批改为首帧原生接管验证，先单跑25条通过后再集中通过。
- 普通几何、创建物、真三角、连续阶段、动态诱导及分散的双视图/导航已离线验证；覆盖风险时钟、真实结束、死亡/销毁、同ID代际、逆向Seek和owner清理。本批源无额外面向/停手/特殊移动执行；RunAmok是敌方冲锋的危险几何，不为玩家新增位移。真实宿主移动和最终像素待实机，回放不发送原生操作。
- 证据`.tmp/halatali-verify/native-vault-four-model-verified.log`、`native-vault-four-build-final.log`、`native-vault-four-focused-final.log`、`native-vault-four-ownership-rerun.log`、`native-vault-four-catalog-verified.log`。

## 育体宝殿第一批

本机客户端表核对CFC909 ↔ Territory1123「厄尔庇斯育体宝殿」。入口 `Presets/TreasureHunt/Entries/ShiftingGymnasion.cs`，与源 `Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/` 三模块对应。仍按已迁主OID接管，不压制同区其他Boss。场地和场外中心轴投影复用上述48边形。

| 模块 | 主OID | 实际激活组件与参数 |
|---|---|---|
| GymnasiouMandragoras | 3D41 | 32297普通动态单体；32300仅Calls adds；32299落点R3；32301–32305五动作自身R7圈组 |
| GymnasiouLeon | 3D27 | 32204前46/宽20；32201与32816共享R12圈组；32205 R8/全角120°；32200普通动态单体；32203落点R8；32317落点R6；32301–32305 R7圈组 |
| LampasChrysine | 3D40 | 32292落点R5；32294 R40/全角60°；32290 helper来源普通动态单体；32288仅Calls bonus adds |

三模块15个激活组件，组动作展开为24条规则。总计已迁7模块、36组件、53条规则。Leon的32816虽注释R40，源构造实际R12，不增加衰减导航或Raidwide预测。普通单体不是Delayable：每帧读取当前目标/结束时间，取消即清，无历史队列；仅FinishAt变化、其他raw字段不变时也必须更新预测，不依赖重新建cast。

Lampas的32294保留Start插入队列，不按预计结束时间排序。输出前4项，全部显示与导航；完整活动数大于2时，输出中的前2项黄色Danger，其余默认AOE色；活动数降为2时全部恢复默认。第5项保留原位置/朝向/activation，前项真实取消或销毁后递补。同代packet补正不重排；真实raw变化按Finish→Start重排；销毁后同ID重新创建从尾部进入。危险文字与前4项使用同一机制输出，第5项隐藏期间不提示、不导航。模型不依赖显示注册表自身的枚举顺序。

三组All分别是：`[3D41,3D42,3D4F..3D53]`、`[3D27,3D28,3D4D..3D53]`、`[3D40,3D4D]`，helper均不计入。Mandragoras奖励怪3D4F..53优先6..2、3D42=1；Leon同五奖励优先7..3、3D4E=3/3D4D=2/3D28=1；Chrysine的3D4D=1，其他合法候选均显式0。五蔓德拉及源列入bonus的Lampas/Lyssa使用洋红，普通add红色。

显示、普通危险区导航、伤害文字预测和owner撤销已离线验证；没有源特殊位移、停手、凝视或交互，不增加执行范围。当前真实GreenMove及最终像素仍待实机验收。

- 集中模型242项、实际DLL全目录304条PASS、0失败：`native-gymnasion-model-final.log`、`native-gymnasion-catalog.log`。
- 复核后补齐危险文字及额外边界，生产重新编译0错误、4项既有Crucible警告：`native-gymnasion-build-final.log`。
- 最终18个寻宝模型及当前CastInfo仅时钟变化/helper单体、Lampas同代补正/同ID重建/隐藏文字的实际DLL定向检查通过：`native-gymnasion-treasure-final.log`、`native-gymnasion-clock-lights-final.log`。
- 首轮模型真实发现销毁未撤销顺序名额，修正后单跑通过；实际DLL首轮的预览数组顺序断言改为按来源验证集合和颜色，机制插入顺序仍由模型独立断言。日志均在 `.tmp/halatali-verify/`，不以失败日志计完成。

## 梦羽宝殿第一批

本机客户端表核对CFC745 ↔ Territory924「梦羽宝殿」。入口 `Presets/TreasureHunt/Entries/ShiftingOubliettes.cs`，源为 `Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/` 四模块。仍复用48边形场地及公共输出，只接管下列主OID。

| 模块 | 主OID | 专属实际激活组件 |
|---|---|---|
| SecretUndine | 3011 | 21663 R44/全角30°；21659 R30/全角120°；21660前12/宽4；21664与21658共享R8；21662 helper全屏预测 |
| SecretSwallow | 302B | 21719 R11圈；21720环8–60；21717前24/宽6；21766落点R8；21716动态单体；21718仅落点R10圈 |
| SecretPegasus | 3016 | 21667前47/宽6；21668 R12；21666仅Calls thunderclouds；21669 R8 |
| GreedyPixie | 3018 | 21686前40/宽8；21684落点R6；21682全屏预测；22779/21683共享R12；22780/21685共享R30/全角120° |

四场都激活21769 R11、21767前13/宽4、21768 R15/全角120°。Undine、Pegasus、Pixie另激活6448–6452 R6.84五动作组；Swallow虽然定义该组件却未激活，不能接管这些动作。四场组件数分别9/9/8/9，规则数14/9/12/15，合计35组件/50规则；全部寻宝当前11模块、71组件、103条展开规则。

Swallow源组件类名与动作名交叉，严格按AID建几何；21718只有普通AOE，不能凭注释新增20码击退。四场21770吸引、Pegasus的21665冲锋及其他未激活视觉动作不接管。Hypnowave/NatureCall只有普通扇形，不新增凝视或状态控制。Pixie分身的7秒和本体5秒读条分别记录来源、落点、方向、开始/判定/实际结束；组形状相同不合并时钟，也不要求先有本体前驱动作。

公共奖励OID为`301D..3021,3034,3033`。All为Undine`[3011,3013,奖励]`、Swallow`[302B,302C,奖励]`、Pegasus`[3016,奖励]`、Pixie`[3018,3019,奖励]`。Bubble3012、Thunderhead3017、Double304C/304D及helper不画敌人标记、不延长战斗，但它们的已激活动作正常产生输出。不可选中的奖励怪不显示标记，却仍维持阶段。

| 模块 | Onion/Egg/Garlic/Tomato/Queen/Fuath/Keeper/普通add优先级 |
|---|---|
| Undine、Swallow | 7/6/5/4/3/3/2/1，普通add分别3013、302C |
| Pegasus | 5/4/3/2/1/2/1，无普通add |
| Pixie | 6/5/4/3/2/3/2/1，普通add3019 |

其余合法候选显式0；奖励洋红，普通add及Primary红。21662/21682的全屏预测复用当前源八槽、过滤死亡成员，且从通用全屏路径排除以避免重复；21716普通单体仍读取当前CastInfo。所有伤害预测只有诊断消费者，不增加PR治疗策略。

显示、普通危险区导航、源八槽和owner撤销已离线验证。源没有特殊移动/交互/停手/面向控制，保留该范围；实际宿主移动及双视图最终像素待实机验收。

- 集中246项模型，生产编译0错误、4项既有Crucible警告；寻宝实际DLL定向26条PASS、全目录312条PASS，0失败：`native-oubliettes-model.log`、`native-oubliettes-build.log`、`native-oubliettes-focused.log`、`native-oubliettes-catalog.log`。
- 额外九人源名册回放通过：local位于输入尾部仍放槽0，其余沿数组顺序截8，PR角色槽不改顺序；死亡仅删预测，不压缩后续槽，也不放入第九人；反向Seek与owner清理通过。证据 `native-oubliettes-roster.log`，生产未另改，无需重复全目录。
- 复核明确：上游SimpleAOEGroups的Finish局部实现不检查AID，但原桥 `MechanicWorldFeed.ApplyCast → ActorState.OpCastInfo → BossModule` 必定先用旧CastInfo发Finish再发新Start。不能拿不可达的“另一AID直接Finish”放开MP取消包的代际检查；保留旧动作取消不清新cast的验证。

## 加加财富天坑：Bull Apollyon

本机客户端表核对CFC993 ↔ Territory1209「加加财富天坑」，入口 `Presets/TreasureHunt/Entries/CenoteJaJaGural.cs`。Bull主OID4305及上文第2–4房已接管；Golden Molter及其他Room模块保留原桥。来源为 `Dawntrail/TreasureHunt/CenoteJaJaGural/BullApollyon.cs` 及 `SharedArenaBounds.cs`。

| 实际组件 | Action与参数 |
|---|---|
| Blade、Pyreburst | 38261动态单体、38262动态全屏预测；无危险圈 |
| FlameBlade1and2 | 38249/38250前40、宽10，独立4/11秒读条 |
| CrossfireBlade3FlameBlade3 | 38255/38251前40、宽5，2.5秒读条 |
| CrossfireBlade1and2 | 38253/38254十字臂长20、半宽5，独立4/11秒读条 |
| BlazingBreath、BlazingBlast | 38258固定前44/宽10矩形；38260落点R6 |
| Spin、Scoop、RottenSpores | 38279 R11；38278 R15/全角120°；38277落点R6 |
| MandragoraAOEs | 32301–32305 R7圈组 |

11组件展开18条规则；全部寻宝当前12模块、82组件、121条规则。38258虽指向玩家，源仍是起手快照矩形，不改为动态诱导。Teleport38334、Inhale38280及未激活视觉动作不接管，不新增传送、吸引或未来波次。

场地通过现有 `DungeonPolygonDifference` 公共几何复现：中心(0,-372)、32边形半径 `19.5f * 1.004839f`，减去中心(0,-352)、半宽20/半高1.65矩形，再按结果AABB重算中心，约(0,-372.62218)。`CosPI.Pi32th`是源浮点倒数常量，不是余弦；本地多边形的反号坐标与布尔运算的世界轴偏移分别处理。场外标记沿最近边投影，中心X±0.1内保留原位置。

All为`[4305,4300..4304,42FF,42FE]`，helper不延长阶段。五蔓德拉优先6..2，Alpaca42FE=2、Uolon42FF=1，其他合法候选0；七种奖励怪洋红。主怪死亡而奖励仍活时继续，奖励全死才结束。

源模型、普通危险区导航及双视图公共适配均已离线验证；没有源原生操作消费者，不增加执行范围。宿主移动与最终游戏绘制待实机验收。

- 248项集中模型通过；生产编译0错误、4项既有Crucible警告；实际DLL全目录316条PASS、0失败。证据：`native-bull-model.log`、`native-bull-build.log`、`native-bull-catalog.log`。
- 实际DLL加载源静态 `SharedBoundsBoss.ArenaBounds`，对照2025个世界采样点及最近边投影/中心轴例外；验证固定场地、独立4/11秒时钟、真实结束、奖励阶段、机制关闭、逆向Seek和owner清理。证据：`native-bull-arena-final.log`。
- 独立复核后将Bull移动后的场地断言改为重新读取当前快照，防止旧边界对象掩盖替换错误；只修改测试，单跑通过：`native-bull-arena-current-boundary.log`。
- 首轮oracle调用被C# dynamic对`in WDir`的绑定限制阻止，测试改为反射调用后单跑通过；未修改生产行为或削弱比较。最终集中回归已包含该修正。日志均位于 `.tmp/halatali-verify/`。

## 育体宝殿第二批

沿用CFC909 ↔ Territory1123及同一原生入口，新增下列四个主OID。来源为 `Endwalker/TreasureHunt/TheShiftingGymnasionAgonon/` 对应同名源码及 `Components/Gaze.cs`、`Voidzone.cs`、`StackSpread.cs`。四模块28个激活组件、39条展开规则；全部寻宝合计16模块、110组件、160条规则，其他同区模块继续保留原桥。

| 模块 | 主OID | 实际激活组件与参数 |
|---|---|---|
| GymnasiouTigris | 3D29 | 32208 R45/全角90°；32209 R6；32206当前单体预测；32207默认10000范围凝视；32210 R12/全角90°；32317 R6；32301–32305 R7 |
| GymnasiouSatyros | 3D2D | 32221 R40/全角90°；32222 R6；32225 R15；32218当前单体预测；32317 R6；所有3D2F实体当前位置R4 TemporaryObstacle |
| GymnasiouTriton | 3D30 | 32230 R40/全角60°；32231 R13；32228当前单体预测；32301–32305 R7；32229 R5毒区的cast/effect/live交接 |
| GymnasiouSphinx | 3D3B | 32265当前单体、32266当前全屏预测；32273环3–12；32270环12–60；32272十字长50/半宽7；32274前45/宽5；32269 R4；32271跟人分散；32317 R6；32301–32305 R7 |

Tigris眼睛锁起手位置及判定时间，实际结束/取消/销毁清理；眼睛、瞳孔和前90°弧分别发布双视图预览，不造地面危险圈。`DungeonEddaMechanics`复用眼形/颜色/危险文字，`GazeFacing → Hints.SetFacing`复用PR面向及0.75秒提前量。公共面向先选有额外角度余量的合法方向；不存在时回到源45°半角，保留多眼之间小于0.02弧度的合法安全缝。回放只规划、不续期PR实际控制。

Satyros风圈不等待32199或32227，不筛Dead/Targetable/EventState；实体销毁或阶段结束才清理。模型保留来源身份和当前位置，经 `DungeonAmbientAoe(Obstacle:true) → DungeonOutput → AddObstacle` 输出硬障碍，并独立发布原尺寸R4显示；不替换成普通定时危险区。

Triton毒区有三路独立状态：Start保存caster与activation，中心读取当前CastInfo目标位置，否则当前LocXZ；Finish/取消只清cast路，不制造event。Native32229无须先有Start，按真实MainTarget当前位置、否则包区域落点锁定event预测，时间为处理时钟加源`(double)1.4f`；每个Bubble每轮只消费首个距离≤2的event预测，无TTL。全部3D32实体均输出量化位置R5 live圈，不筛Dead/Targetable/EventState。输出保持cast→event→live。模型复用`DungeonTrialVoidzones`，普通危险区/显示仍由公共输出适配；未激活32233拉近和32234囚禁不增加控制。

为补齐真实输入，`NativeActionEffectCapture`从已有FFCS Receive第三参数复制可空区域效果坐标到`NativeActionEffectEvent.TargetPosition`，不是PR逐目标事件中的对象位置；空指针及旧录像缺字段为null，有效零坐标不丢弃。复制参数、完整主目标、队列、录制/读回及回放owner边界有实际DLL验证。Triton消费完整Native事件前强制刷新当前对象采样，避免Framework先Drain效果、后常规Scan导致锁到旧位置；PR展开后的同动作不重复生成event预测。旧录像若同时缺主目标实体与Native包落点，不凭旧caster或零坐标补猜。

Sphinx分散复用 `DungeonQuestParty` 的捕获目标对象及独立activation；移动跟随该对象，销毁后冻结引用，同ID新代不换绑。真实Finish按目标ID删除首项，不按helper来源；效果包和预计时间不结算。死亡只隐藏，复活恢复。双视图显示量化位置R6轮廓；源八槽存活队伍至少2人时导航用R7，点名者额外避开未点名队友，其他点名者独立避让。`SpreadFromCastTargets`默认不发布伤害预测，未扩成Raidwide或停手。

四组All分别为`[3D29,3D2A,3D4E,3D4F..3D53]`、`[3D2D,3D2E,3D4D,3D4E]`、`[3D30,3D31,3D4F..3D53]`、`[3D3B,3D3C,3D4D..3D53]`；Bubble、StormsGrip、Plume和Helper不维持阶段、不画敌人标记。主怪死亡仍由活add/奖励保活。Tigris/Triton/Sphinx五蔓德拉优先6..2、普通add1；Tigris的洋红Lyssa仍priority0，Satyros的Lampas3/Lyssa2，Sphinx的Lampas/Lyssa2；其他候选显式0。颜色和阶段规则不扩大选敌范围。

显示及当前公共危险/硬障碍导航已离线验证；凝视有PR请求规划与回放隔离验证。四场没有源原生位移、停手或交互，不增加执行范围。真实宿主面向/移动与游戏内最终像素仍待实机验收。

- 131个不同集中模型场景最终通过：`native-gymnasion-rest-model-final.log`首轮129项通过、2项失败；修正面向优先保留余量/窄缝回退及旧Lakshmi夹具同一时刻结束又重施的问题后，`native-gymnasion-rest-model-rerun.log`单跑两项失败和Tigris窄缝均通过。没有放开结束墓碑或旧代输入保护。
- 生产编译0错误、4项既有Crucible警告：`native-gymnasion-rest-build-final.log`；实际DLL全目录330条PASS、0失败：`native-gymnasion-rest-catalog-final.log`。
- Native采集/录制读回、Triton真实adapter先采当前目标再锁效果、同位置Bubble交接的独立定向通过：`native-gymnasion-rest-sampling-final.log`。全部日志位于 `.tmp/halatali-verify/`。首轮Sphinx坐标量化及AddSeconds逐步浮点运算的测试断言修正后单跑通过，最终集中回归包含全部修正。

## 运河祭殿第二批

沿用CFC586 ↔ Territory794及原ShiftingAltars入口、48边场地。来源为`Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/`，新增四模块38个实际激活组件、46条读条规则；加上此前批次共20个寻宝模块、148组件、206规则。

| 模块 / 主OID | 专属激活合同 |
|---|---|
| AltarSkatene / 2535 | 13310 R12.48；13309落点R6；13312仅Calls adds；13311延迟单体；五蔓德拉6448–6452 R6.84 |
| AltarBeast / 2536 | 13259 R12.6/全角120°；13261 R34.6/全角120°；13262普通R15；13260前49.6/宽6；13740落点R6；13263默认10000凝视；五蔓德拉R6.84 |
| AltarArachne / 253B | 13342延迟单体；13784默认10000凝视；13455 R24/全角60°；13343仅全队伤害预测；13346 R10.5、13345环10–20、13344环20–30，三项独立真实读条；没有蔓德拉组件 |
| TheOlderOne / 253F | 13386 R45.06/全角60°；13388 helper落点R7；13390不可选Idol前41.72/宽3；13391 R10.72；13389仅全队预测；13385延迟单体；五蔓德拉R6.84；13387视觉未激活 |

四场共同5352落点R6、8598 R8.42/全角120°及8599持续Cleave。`TreasureCleaveOrigins`每次枚举全部存活可选2545，动态解析TargetID及朝目标方向，无须8599或9636前驱事件，Matanga施法中仍生效；找不到目标时不输出。`TreasureCleaves → DungeonOutput`显示量化原点R9.42/全角120°轮廓，非目标本人避让同形扇；目标本人不避自身扇，改向源八槽每位其他存活同伴生成R100方向禁区，仅导航。文字用源原位置及玩家量化位置，保持`GTFO from cleave!`。8599效果和时钟不关闭、不刷新常驻机制。

13311/13342/13385沿公共`DelayableAction`队列保留目标、槽位、代际、起手判定和取消历史，真实Native MainTarget只消费同Action首匹配项；效果先于读条结束时不残留通用死刑预测。13263/13784复用`Gazes → EddaGazeDrawing/GazeFacing → Hints.SetFacing`的锁眼、瞳孔、前90°弧、危险文字及0.75秒提前面向；不依注释半径造地面圈。普通AOE依实际Finish/取消/销毁结束，不按Effect或预测时刻清除，地震不自动生成后续环。

All分别为`[2535,255E,2545,2546..254A]`、`[2536,2567,2544,2545,2546..254A]`、`[253B,2544,2545]`、`[253F,2544,2545,2546..254A]`。EarthquakeHelper、Idol及233C不画敌人、不维持阶段。蔓德拉优先6..2；Skatene的2545=2/255E=1，Beast的2544=2/2567及2545=1，Arachne和Older的2544=2/2545=1，其他合法候选显式0。2545和各源bonus洋红，普通add红色。

模型、显示和当前危险区导航已离线验证；凝视PR意图与回放隔离通过，实际GreenMove/PR面向及游戏内最终像素仍待实机验收。这四场没有源特殊位移、交互或停手，不扩大执行。

- 50项相关模型通过：`native-altars-four-model-final.log`；新增即时预测与效果先到边界断言。
- 生产编译0错误、4项既有Crucible警告：`native-altars-four-build-final.log`。
- 实际DLL全目录345条PASS、0失败：`native-altars-four-catalog-final.log`。
- 首轮实际DLL发现三个新Delayable重复走通用死刑，移除重复入口后失败项及专项重跑通过：`native-altars-four-focused.log`；随后全目录通过。证据位于`.tmp/halatali-verify/`。

## Korrigan、Serpent及Megakantha

沿已核对CFC745/Territory924与CFC909/Territory1123的原入口续迁。三个模块22组件、34条读条规则；另有Megakantha三种真实效果。当前共23个寻宝模块、170组件、240规则。

| 源模块 / 主OID | 实际激活合同 |
|---|---|
| ShB/TheShiftingOubliettesOfLyheGhiah/SecretKorrigan / 3022 | 21674默认10000凝视；21675落点R3；21676仅Calls adds；21673延迟单体；6448–6452五动作R6.84 |
| 同目录 SecretSerpent / 3025 | 21701/21702独立R8圈组；21699动态单体；21700 R15.29/全角90°；22771 R13.45/全角90°；五蔓德拉R6.84；21769 R11、21767前13/宽4、21768 R15/全角120°；1EA7D5实体水池R7.5 |
| EW/TheShiftingGymnasionAgonon/GymnasiouMegakantha / 3D33 | 32240 helper落点R8；32244前15/宽4；32238动态单体；32237 R12/全角120°；32243与32317 R6；32301–32305 R7；32242及32242/33015/33016效果驱动持续半圆 |

Korrigan复用源槽捕获/原目标MainTarget FIFO队列，Finish、取消、来源销毁和预计时间不消费，真实效果删首匹配项。凝视复用公共锁眼、眼形/瞳孔/90°视角弧和PR面向，回放禁执行。

Serpent水池只筛EventState!=7，不筛死亡或可选；原位置动态R7.5经`TreasureObstacles → DungeonOutput → AddObstacle`输出硬障碍，独立发双视图预览及踩圈文字。读条R8不与水池合并，也不生成cast/event/live交接队列。21770吸引仅枚举未激活，不增加位移/自动移动。

Megakantha模型保存32242来源、代际、起手落点/方向/activation和组件全局命中计数。32242 Start覆盖R40/全角180°半圆，不重置计数；32242/33015/33016真实Native包共同计数，无须当前AOE存在，恰第五次撤销并归零。Finish、取消、施法者销毁及预计时间不清。重复Native包及PR逐目标展开不重复计数；同代补正不能覆盖后来的其他来源，不能复活已结算半圆。公共`DungeonOutput`消费其冻结几何；33015/33016只在该Boss及合法来源范围压制通用躲避，不创建独立AOE。32241/32239/32199未激活。Megakantha源`Arena.Actors(rest)`画全部3D33/3D35/3D36，保留同OID第二主怪。

All分别为`[3022,301C,301D..3021]`、`[3025,3026,301D..3021,3033,3034]`、`[3D33,3D35,3D36,3D4D..3D53]`。Korrigan五蔓德拉优先6..2、301C=1；Serpent同五优先7..3、3033=3/3034=2/3026=1；Megakantha五蔓德拉优先7..3、3D4D=3/3D4E=2/3D35及3D36=1。全部其他合法候选显式0；奖励洋红、普通add红色，Helper和水池不延长阶段。

显示、当前危险/硬障碍导航和伤害诊断已离线验证；凝视PR意图与回放隔离覆盖，真实宿主移动/面向及最终像素仍待实机验收。没有源特殊位移/停手/交互，不扩执行范围。

- 53项相关模型通过：`native-korrigan-serpent-megakantha-model.log`。
- 生产最终编译0错误、4项既有Crucible警告：`native-korrigan-serpent-megakantha-build-final.log`。
- 实际DLL专项通过：`native-korrigan-serpent-megakantha-focused.log`；最终全目录353条PASS、0失败：`native-korrigan-serpent-megakantha-catalog-final.log`。
- 首轮实际DLL夹具未保持活读条快照，按真实输入补齐Korrigan凝视和Serpent读条帧后失败项通过；独立复核补齐Megakantha两效果步骤的来源级通用躲避压制，再用最终DLL全目录验证。日志均位于`.tmp/halatali-verify/`。

## 运河祭殿：Chimera、Diresaur、Totem

沿CFC586/Territory794及原`ShiftingAltars`入口续迁，新增32个实际激活组件、24条读条规则。当前共26个寻宝模块、202组件、264规则。参数与撤销规则来自`Stormblood/TreasureHunt/TheShiftingAltarsOfUznair/AltarChimera.cs`、`AltarDiresaur.cs`和`AltarTotem.cs`，共同遵守`BMRToMPScripts.spec.md` §3.1/3.2，不实施统一寻路改造。

| 模块 / 主OID | 生产模型与源参数 |
|---|---|
| Chimera / 2539 | 13393 R11.92/全角90°；13394 R9.92；13395环8–30；13392 R11.92/全角120°；13742 R8.07/全角90°普通扇形，非凝视；13396 R6毒区，Native效果至生成延迟`(double).9f`，实体1E8D9C |
| Diresaur / 253A | 13217延迟单体原目标FIFO；13218 R14.6/全角90°；13220 R26.6/全角90°，不额外翻转；13283仅全队伤害预测；13835 helper落点R4；13743 R10；13219 R6毒区，延迟`(double).7f`，实体1EA140 |
| Totem / 2534 | 13451 R13.06/全角120°；13454落点R5；13453 R11.06；13739前45.06/宽4；13452 R10毒区，延迟`(double)1.2f`，实体1EA8BB |

三者均保留Matanga的5352 R6、8598 R8.42/全角120°和8599常驻Cleave；共用上一批原八槽、来源/目标与读条中顺劈的合同。All分别为`[2539,256A,2545]`、`[253A,256F,2544,2545]`、`[2534,2566,2545]`，地板和Helper不维持阶段。Matanga均priority2/洋红，普通add为1/红色；Diresaur的GoldWhisker额外priority3/洋红，其余合法候选显式0。

`DungeonTreasureBaits`保存主怪与目标的对象代际及最后快照，Icon23取事件Source Actor，忽略包Target字段，按事件逐条追加而不去重。R6/R10圈持续读取原目标位置，显示与导航均不额外量化；本人按InstanceID画轮廓，其他圈填色。导航按原Actor引用身份区分本人：本人逐条避源八槽其他存活队友的同半径圈，其他人避原目标圈；Source==Target时保留基类不额外避队友的例外。仅首目标额外避场地中心R17.5，无该禁中心的显示圈；首目标文字分别为`Bait away!`与`Bait away! (3 times)`，后续目标保留基类夹人/被圈覆盖文字。

目标死亡不清理或隐藏；销毁后冻结，同ID新代仍可看到“自己”的旧轮廓，但导航按他人旧圈避让，不继承旧首目标禁中心。主怪死亡只隐藏基类圈/导航，首目标禁中心与专用文字仍读CurrentBaits；复活原代可恢复。主怪未死亡即销毁后保留旧引用，后续Icon仍使用旧Source，同ID新主怪不接管它。Chimera任意来源13396真实Start清全表；另外两场各自第三次13219/13452真实Start清表归零，无Icon时也计数、Icon不重置、取消不扣数、效果不推进。同代packet补正不重数，raw比较字段真正变化仍按Finish→Start重启计数。没有图标TTL或新增伤害预测。

毒区复用`DungeonTrialVoidzones → DungeonOutput → SafeZone/Draw`：cast圈跟当前CastInfo目标或LocXZ，activation锁Start；Native效果先采样当前世界，再按MainTarget或包落点追加独立固定预测，使用处理时钟加源float提升后的延迟，PR逐受击者展开不追加。Finish/取消/来源销毁只清cast，不合成event。live只筛EventState!=7，不筛死亡或可选，输出量化位置；每个有效实体每轮按原位置距离≤2仅消费第一条event，无TTL。输出顺序cast→event→live，全部为ForbiddenZone导航，非硬障碍。

Chimera13394/13395保留独立危险几何、`TreasureInterruptTargets`意图及`Interrupt!`文字，按实际Finish撤销，不按预计结束到期。源默认允许打断、不允许眩晕，不额外按CastInfo.Interruptible过滤。旧MP桥未消费ShouldBeInterrupted执行敌方打断，因此本批不把意图扩成自动打断、停手或取消自己的读条。三场没有源特殊位移、交互或停手；真实宿主移动和游戏内最终像素仍待实机验收。

- 模型与公共控制：61项集中模型及新增2项代际反例通过，共63个不同场景，证据`native-altars-bait-model-final.log`、`native-altars-bait-generation-model.log`。
- 首轮模型发现诱导圈误量化，修正为源原始坐标；另一失败为夹具新Start与前Finish同刻被合法墓碑拒绝，修正夹具真实时间间隔后定向重跑通过，见`native-altars-bait-model-rerun.log`。
- 生产编译0错误、4项既有Crucible警告：`native-altars-bait-build.log`。
- 实际DLL专项13条PASS（含三动作主目标先采样和原Triton回归）：`native-altars-bait-focused.log`；全目录363条PASS、0失败：`native-altars-bait-catalog.log`。
- 显示、当前危险区导航、机制模型、反向Seek及owner撤销已离线验证；回放不执行GreenMove或原生操作。证据均在`.tmp/halatali-verify/`，入口仍209个。

## Fuath Troublemaker 与 Vault Gargantuan

复用CFC745/Territory924和CFC1060/Territory1279的原入口，均已以本机客户端表双向核验。源为`Shadowbringers/TreasureHunt/TheShiftingOubliettesOfLyheGhiah/FuathTroublemaker.cs`与`Dawntrail/TreasureHunt/VaultOneiron/VaultGargantuan.cs`。新增14组件、19条规则，当前共28个寻宝模块、216组件、283规则。

| 模块 / 接管主OID | 源实际激活合同 |
|---|---|
| Fuath Troublemaker / 302F | 21738仅Calls adds；21734当前CastInfo目标单体预测；21736 helper落点R8；21740四臂长40/半宽2.5十字；21737 R6、MinSize=8/MaxSize=8分摊，不发布默认Shared伤害；21739/21735视觉未接 |
| Vault Gargantuan / 4898 VaultCrab | 43612/43613同组R9/全角270°；43614落点R6；43618实际R15/全角120°；43619 R11；43615延迟单体；43616仅全队预测；32301–32305 R7组；43727前20/宽3；43726 R8；43617 Inhale仅枚举未激活，不新增吸引 |

Fuath沿用`DungeonQuestParty`的来源/目标代际、真实Start/Finish、原目标FIFO及销毁冻结，输出由`DungeonTreasureStacks → DungeonOutput → SafeZone/Draw`适配。分摊目标死亡只隐藏，复活恢复；同ID新代不继承旧目标本人身份，未解析的起手目标不延迟补建。Effect与预计时刻不清，Finish按目标ID只消费首项。导航读存活源八槽：少于2人不加导航；目标本人找非其他点名者的原位置R3同伴圈；其他角色按当前R6圈内人数，圈外须少于8才加入，已在圈内允许恰8；不能加入时，其他分摊目标避R12、普通角色避R6；多个合法候选进入任一圈。显示本人始终绿色轮廓，其他圈按容量及原始全部Stacks中的标记身份着色，死亡标记者仍影响其他圈颜色；导航只读ActiveStacks。文字保留Stack语义并显示目标人数`n/8`，恰8且不重叠时显示满足状态，未将最少人数降成2。

Fuath的All为`[302F,3033]`，主怪死亡而Trickster存活时继续；3033洋红/priority1，其他合法候选显式0，Helper不保活。场地仍为中心(100,100)、顶点半径19的48边形。

Vault Gargantuan入口是4898，489A不是独立接管入口。场地为中心(100,100)、半宽19.5的方场。All为`[4898,4899,48B9,48BC,48BB,48BA,48BD,48B7,48B8]`，必须全部销毁；单纯Dead且尸体仍在时继续。另缓存首次观察到的489A Actor，All销毁后它仍活着则继续，缓存死亡或销毁才结束；从未缓存时此半条件为真，第二个489A不替代首引用。按源`StateMachine.Update → UpdateModule`，完成检查先于首次缓存；旧缓存每帧先更新当前生死，销毁后冻结不重绑。标记画全部4898/4899、全部奖励及仅缓存的489A，读条packet替换不可变DTO时保持标记。48B9..48BD优先6..2，48B7=2/48B8=1、其他显式0；奖励洋红，trash/缓存巨人红。Mash复用取消/销毁保留的原目标延迟队列，Native MainTarget跨来源消费首项。

这两场没有已激活的特殊位移、交互、停手或凝视，不增加原生动作执行。模型、显示、当前危险区导航及owner撤销已离线验证；真实宿主移动与游戏内最终像素仍待验。

- 最终74项集中模型通过：`native-fuath-gargantuan-model-final.log`；独立复核补充的两项反例先单跑通过：`native-fuath-gargantuan-review-model.log`。
- 生产最终编译0错误、4项既有Crucible警告：`native-fuath-gargantuan-build-final.log`。
- 实际DLL专项9条PASS（含公共原生效果/队伍输入场景），最终全目录370条PASS、0失败：`native-fuath-gargantuan-focused-final.log`、`native-fuath-gargantuan-catalog-final.log`。
- 十字以真实几何点位验证总宽5；缓存巨人packet瞬间丢标与死亡标记者颜色均已修正并由模型、实际DLL独立覆盖。回放不执行GreenMove或原生操作；证据位于`.tmp/halatali-verify/`，入口仍209个。

## Secret Basket 与 Secret Porxie

复用CFC745/Territory924及原`ShiftingOubliettes`入口。来源为同目录`SecretBasket.cs`、`SecretPorxie.cs`及`ConcentricAOEs`、`Exaflare`、`RaidwideCastDelay`基类。新增18组件、28条读条规则，当前共30个寻宝模块、234组件、311规则。遵守迁移规格§3.1/3.2，不实施统一寻路改造。

| 模块 / 主OID | 原生模型及参数 |
|---|---|
| Basket / 302D | 21724起手创建三段全角270°序列：R6.5扇形、环扇6.5–12.5、环扇12.5–18.5；21729视觉结束预测+1.2秒全屏，21730真实效果清理；21722 R8；21721当前CastInfo动态单体；21732前15/宽4；21728环10–20；6448–6452 R6.84组；Keeper的21769 R11、21767前13/宽4、21768 R15/全角120° |
| Porxie / 3014 | 21670 R5/全角60°普通扇形；21671/22936 R6；22937扫帚起手创建R6、前进12、间隔4.5秒、总4爆/最多3爆的独立地火；Keeper三组件及五蔓德拉R6.84组 |

`TreasureConcentricStrike`保存源/代际、首次原位置/朝向、当前段和判定时刻。只有21724 Start建序列；21725/21726 Start不替代后续预测。21724/25/26实际Finish分别推进order0/1/2，按插入顺序匹配首个原点X/Z各±1及归一角差≤0.05弧度的序列，不绑定caster。下一段判定时间为处理时钟+1.1秒，始终保持首次几何；取消、打断、销毁产生的Finish仍推进。错段、位置/方向不匹配、普通Effect和预计到期均不清理；第三段真实Finish才移除。

Earthquake保留组件唯一activation及最后真实Start的来源/代际。Start覆盖为finish+1.2秒，取消/销毁/预计到期不清，跨来源21730 Native效果才清；PR逐目标展开效果不重复消费。旧来源同代packet补正不能覆盖后来源时钟或复活已结算状态。向当前存活源八槽发布全队预测并持续提示`Raidwide`，不造全场危险圈，也不增加治疗策略。

`TreasureSweepLine`保存源/代际、未量化Next、首次锁定方向、12码advance、NextExplosion和剩余次数。起点取caster起手位置而非LocXZ，朝向取spell.Rotation；首判定为finish+0.9秒，已包含公共NPC补偿0.3秒。21672 Native效果先采样当前世界，用当前caster位置按X/Z各±1匹配首条Line，无SourceID限制；以实际位置+advance补正Next，下一判定为处理时钟+4.5秒，第四次匹配才移除。22937效果、Finish、取消、死亡/销毁和预计到期均不推进或清理。

Sweep仅在Line数量/版本变化时重建future缓存，基准为max(NextExplosion,处理时钟)，单纯Tick不逐帧滑动。未来最多两圈先输出、当前危险红圈后输出；全部Risky并参与导航，第四圈初始不显示/不导航。仅输出位置量化，模型及匹配保持原位置，不按场边截断。同代packet有明确SourcePosition时可补正原点，缺失时保持该代已接受锁位；不因caster随后移动重新取原点，不追加重复Line。踩任一有效地火保留`GTFO from exaflare!`。

Basket的All为`[302D,302E,301D..3021,3033,3034]`，Porxie为`[3014,301D..3021,3034]`。死亡或销毁满足单Actor结束条件；扫帚/Helper均不保活，3033只保活Basket。Basket五蔓德拉优先6..2、Fuath=3/Keeper=2/Echivore=1；Porxie五蔓德拉5..1、Keeper=1、Fuath=0。奖励洋红，普通敌人红，其余合法候选显式0。

机制模型经`DungeonTreasureSequences → DungeonEncounter → DungeonOutput → SafeZone/Draw`适配，两种显示共用原生几何和当前/后段时钟。两场没有源已激活的特殊位移、凝视、停手或交互；BrewingStorm注释虽有击退但源没有KB组件，Keeper吸引未激活，不增加自动控制。模型、显示、当前危险区导航、反向Seek及owner撤销已离线验证；回放不发送原生操作，真实宿主移动和最终像素待实机验收。

- 最终88项集中模型通过：`native-basket-porxie-model-final.log`；独立复核后的14项专项通过：`native-basket-porxie-review-model.log`。
- 生产最终编译0错误、4项既有Crucible警告：`native-basket-porxie-build-final.log`。
- 实际DLL专项10条PASS（含公共输入验证），全目录378条PASS、0失败：`native-basket-porxie-focused-final.log`、`native-basket-porxie-catalog-final.log`。
- 复核修正旧来源全屏时钟回滚、扫帚同代原点丢失及积压Finish使用事件时间的问题；模型与实际DLL均加入独立反例。证据位于`.tmp/halatali-verify/`，内置入口仍209个。

## Vault Bat、Fluid Soul 与 Wind Spirit

沿CFC1060/Territory1279及原`VaultOneiron`入口续迁。源为同目录`VaultBat.cs`、`VaultFluidSoul.cs`、`VaultWindSpirit.cs`与`Components/BaitAway.cs`。新增28个激活组件、43条规则，当前共33个寻宝模块、262组件、354规则。

| 模块 / 接管主OID / 后续缓存Boss | 全部专有激活组件 |
|---|---|
| Bat / 489E / 48A0 | 43630前60/宽8动态诱导死刑；43627落点R3；43632 R40/全角45°；43628/43631 R6组；43629前40/宽8；43633只提示及全屏预测 |
| Fluid Soul / 4895 / 4897 | 43607 R13/全角90°动态诱导死刑；43602 R6；43603环6–40；43610落点R5；43606/43605 R8组；43609 R39/全角30°；43608只提示及全屏预测 |
| Wind Spirit / 489B / 489D | 43620前14/宽4；43621落点R4；43622/43624 R6组；43625前60/宽40；43623前40/宽8动态诱导死刑；43626只提示及全屏预测 |

三者共同激活43727前20/宽3、43726 R8和32301–32305 R7组。场地仍为中心(100,100)、半宽19.5的方场。复用原Gargantuan生命周期并按真实消费者参数化为`CachedBossOid/TreasureCachedBossObserve`：All分别是主OID及下一个trash OID（489F/4896/489C）加奖励`[48B9,48BC,48BB,48BA,48BD,48B8,48B7]`；必须全部Destroyed，Dead尸体仍保活。后续Boss不在All，不独立接管；只缓存第一个引用，已有缓存状态先刷新、阶段结束检查先于首次缓存，原引用销毁后冻结，同ID/第二个Boss不替换。绘制全部trash和奖励及仅缓存Boss；packet替换DTO不漏标。

三场奖励优先级48B9..48BD=6..2、48B8=1；仅Bat的48A0额外2，仅Fluid的48B7额外2；其余合法候选显式0。全部奖励均洋红，与优先级0相互独立，普通trash/Boss红色，保留Pointless基础筛选。

三诱导死刑复用`DungeonTreasureCastBaits`：实际Start解析原TargetID，保存来源/目标引用、来源代际、起手判定时间与独立条目ID；不把self目标回退成当前仇恨目标。源位置、目标位置及两者方向动态更新，self-target读取source当前朝向；形状尺寸固定，无自动锁角。当前caster已不在该cast时仍保留，Finish/取消和预计时刻不清；只有Native对应动作按source的首项FIFO消费，与MainTarget无关，PR展开及重复原生包不重复消费。来源死亡/销毁或目标死亡永久删除；目标仅销毁冻结末位置，同ID/OID重建不继承原引用。

显示本人按InstanceID画原位置轮廓，他人填色；导航按原Actor引用区分本人：他人避全动态形状，本人逐个避源八槽存活队友方向的R100禁扇。Fluid取原半角45°，两个矩形取asin(半宽4/来源至队友距离)；source==target不生成本人队友约束。伤害目标按当前源名单槽位汇总、统一使用原列表首项判定时间，不要求caster仍读条，保留名单但Actor缺失时也保留预测。文字保留`Tankbuster cleave`、`Bait away from raid!`及`GTFO from baited aoe!`。

同代packet补正只更新当前条目，不追加或复活已消费项；首次raw可补建起手未解析目标，但有已消费凭据时不得补建。目标已销毁时，同目标ID的补正继续使用旧引用，不因当前世界缺Actor删除。OID变化即锁定旧目标脱离，后续原OID返回也不重绑。真实raw变化仍按Finish→Start追加，旧效果终结队列保留。

模型及几何经`DungeonEncounter → DungeonOutput → SafeZone/Draw`适配。三场没有源已激活的特殊位移、凝视、停手或交互；不凭技能名称新增控制。显示、当前危险区导航、模型时序/身份、Seek和owner清理已离线验证；回放不发送原生操作，真实宿主移动和最终像素待实机验收。

- 102项集中模型通过：`native-vault-trio-model-final.log`，包括三场完整几何/缓存/代际、动态诱导、原目标冻结、缺Actor槽位、首次raw和source FIFO反例。
- 生产最终0错误、4项既有Crucible警告：`native-vault-trio-build-final.log`。
- 实际DLL专项11条PASS（含公共输入验证）：`native-vault-trio-focused.log`；最终全目录387条PASS、0失败：`native-vault-trio-catalog-final.log`。
- 全目录首轮的旧“Vault Bat未迁移”断言已换成仍待迁移的Gwyddneu；失败项先单跑通过，见`native-vault-trio-rerun.log`。独立复核所发现的四个诱导身份/预测差异均已修正并复核关闭。证据位于`.tmp/halatali-verify/`，入口仍209个。

## 梦羽宝境 Alichino 与 Goliath

本机客户端表确认CFC688 ↔ Territory879「梦羽宝境」，入口`Entries/DungeonsOfLyheGhiah.cs`。源`Shadowbringers/TreasureHunt/TheDungeonsOfLyheGhiah/`两模块共21个唯一激活组件、29规则；当前寻宝共35模块、283组件、383规则。Alichino重复激活的HeatGazeConeSmall按`BossModule.ActivateComponent`替换旧实例，最终仅一份17886输出，不按11次激活计成11种组件。

| 模块 / 主OID | 全部专有激活组件 |
|---|---|
| Alichino / 2BA7 | 17885 R6；17883/17887环5–10组；17881 R43.3/全120°；17886 R19.9/全60°；17882全屏提示/预测；17880动态单体预测 |
| Goliath / 2BA5 | 17874落点R15；1E9998实体水池R8硬障碍；17879十字长100/半宽3.5；17878前102.1/宽7；17877 R6精确8人分摊；17876全屏提示/预测；17873动态单体预测 |

两者共同激活17854 R11、17852前15.23/宽4、17853 R15/全120°、6448–6452 R6.84组。HeatGaze为普通AOE，不是凝视；17884、17855和Goliath的17875类均未激活，不增加控制或接管。普通读条各自锁起手几何/时钟，实际Finish、取消或施法者销毁清除，Effect和预计到期不清；半途启动只保留一份当前小扇。

场地复制`SharedArenaBounds.BuildArena`全部130个世界XZ顶点，Y取场地输入，以源AABB中点(约0.005,-393.74)取景。使用`WorldPolygonGeometry(worldPoints, center)`直接保留世界坐标；`GeometryHelper.LocalToWorld`零旋转会反转局部X/Z，因此不能把世界差直接作为零旋转局部点。生产实际DLL与源私有BuildArena所得ArenaBoundsCustom逐2964点Contains对照通过；移动主怪不改变场地，场外marker仍用源最近边/中心轴例外。

Goliath水池直接读取当前1E9998实体，仅筛`EventState!=7`，不筛Dead/Targetable，绘制原位置R8并发布硬障碍；没有读条/效果预测和交接。实体移动实时跟随，7撤销、恢复0重现，销毁清理。与17874落点R15时序独立。八人分摊与Fuath参数完全相同，复用`TreasureEightStacks/StackHints`：Start捕获原目标引用及判定时间；Finish按动作和原目标ID只删首项，与caster无关，取消/销毁读条同样完成一项，Effect与预计到期不删。目标死亡暂隐藏、原引用复活恢复；目标销毁冻结，重建同ID不继承本人身份。本人靠近未被点名同伴R3，其他人容量`圈外<8/圈内<=8`，合法圈取任一，满员R6避让、自己也被点名时另一圈R12避让；队伍不足2人不导航，无额外Shared预测。

All分别为`[2BA7,2BA8,2BA9,2A05..2A0A]`和`[2BA5,2BA6,2A05..2A0A]`，AllDeadOrDestroyed，主怪死亡后存活add或奖励继续保活，尸体不延长；SweetSong、水池及helper不保活、不画敌人marker，但SweetSong读条仍生效。只画Primary主怪与所有对应add/奖励，奖励洋红、其他红色。Onion/Egg/Garlic/Tomato=6/5/4/3，Queen及Keeper均2，Alich/Ino/Javelin=1，其余显式0。

机制经`DungeonEncounter → DungeonOutput → SafeZone/Draw`适配，不引入搜索器依赖或特殊移动。显示、当前导航、源时序/引用及owner清理已离线验证；真实宿主移动、最终像素仍待实机，回放不发送原生操作。

- 110项集中模型：`native-lyhe-model-verified.log`，新8项场景包含完整规则/未激活边界、场地、容量/目标FIFO/代际、All/标记及预测；失败项单跑修正后回归通过。
- 生产0错误、4项既有Crucible警告：`native-lyhe-build-final.log`。
- 实际DLL专项6条PASS（含公共输入2项）、全目录391条PASS、0失败：`native-lyhe-focused-final.log`、`native-lyhe-catalog-final.log`。证据位于`.tmp/halatali-verify/`，入口210个。

## 运河祭殿 Altar Airavata 与 Kelpie

同CFC586/Territory794，复用`ShiftingAltars`及48边场地。共19激活组件/19消费规则（含只观察真实结束的13443）；寻宝累计42模块、349组件、460规则。

| 模块 / 主OID | 源激活与普通合同 |
|---|---|
| AltarAiravata / 2543 | 13372落点R6；13373 R30/全120°；13375普通R14；13371延迟单体；13374 Icon击退与独立读条诱导；Matanga顺劈、5352/8598 |
| AltarKelpie / 2537 | 13439落点R5；13442前49.4/宽44普通AOE；13438延迟单体；13440全队预测与独立20码径向撞墙击退；255B创建水圈/13443真实结束；Matanga与五蔓德拉 |

Airavata生产模型在`DungeonTreasureBuffet`：`TreasureBuffetIcon`保留原目标引用、创建消费时刻、+3.1秒判定、20码和撞墙参数。Icon23使用事件Source Actor，最新Icon覆盖旧槽；没有读条也持续保留，任意13374真实Finish/取消/施法者销毁清除。击退是DirForward：原目标当前位置为起点，方向每帧由目标减原Primary决定；同点时角0、向+Z，不能套径向零向量跳过。免疫严格晚于判定只隐藏投影，R17.5场地中心诱导导航与Bait away文字仍保留。目标销毁冻结旧引用，同ID新对象不能成为旧目标；死亡不撤销状态。

Buffet读条另外保存`TreasureCastBait`来源/目标引用和原`DungeonPendingCast`代际。首条诱导锥R30/全120°的显示/导航锥心按原Primary、方向按该bait的Source→Target动态计算；任一有效bait目标看到首条轮廓，其余观看者看到实心并避让。文字沿base使用目标位置锥心检查存活队友，与显示故意不同；死亡来源仅影响base文字/IsBaitTarget，派生首条显示不整组删除。Effect/预计到期不清，Finish按caster删首条。源重写AI未调用base，故不增加Buffet伤害预测、本人队友方向导航或免疫动作。

Kelpie的`TreasureHydrosphere`保存Source、创建量化位置、创建消费时钟与+8.6秒判定。`DungeonTreasureKelpie`从激活时现有水球补发创建，也接真实ObjectCreated；处理事件早于对象快照及同ID重建，超过3圈仍全量保存。水球移动/死亡/不可选/EventState7/空闲销毁/预计到期不清圈；任意13443真实结束清全组，Effect不清，销毁施法者触发实际Finish。13443不另造起手圆，也不把水圈变成硬障碍。

Rising Seas复用`TreasureRadialKnockback`20码径向、锁spell.LocXZ/朝向/判定、顺序串联、免疫与源墙公式。危险落点检查所有缓存水圈，不比较圈的激活先后；导航另按每个圈相对Arena.Center的方向发布R20/全75°扇区，统一取首条KB判定，免疫时仍发布。13442未激活击退组件，13441 Gallop未激活，不新增特殊位移执行。

两模块只画Primary主怪及奖励，水球/helper不参与All/敌人marker。Airavata All`[2543,2544,2545]`；Kelpie`[2537,2547,2548,2546,2549,254A,2545,2544]`，AllDeadOrDestroyed。GoldWhisker优先2、Matanga1，Kelpie蔓德拉6..2，奖励洋红，其余0。延迟单体按原目标/槽位FIFO；取消、销毁和Finish不消费，实际相同动作MainTarget匹配消费首项。

适配统一沿`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`，击退投影复用`QuestKnockbackProjection`。双视图、源实际提供的导航、模型和撤销已离线验证；不执行原生强制位移/动作，真实物理、最终像素及GreenMove/PR执行待实机。证据位于`.tmp/halatali-verify/`：

- `NativeTreasureWaterRegression`新增9项；集中134模型通过：`native-altars-water-model-final.log`。同时补强前批“首击免疫/零方向后继续取下一击”“中段危险终点安全”和正常时钟下即时状态覆盖窗。
- 生产0错误/4项既有警告：`native-altars-water-build.log`。
- 实际DLL专项7条PASS（含公共2项）、全目录412条PASS/0失败：`native-altars-water-focused-final.log`、`native-altars-water-catalog.log`。覆盖双视图、Icon Source/动态目标/免疫、bait引用冻结及显示文字差异、水圈代际/真实结束、导航范围、All、逆向Seek和owner撤销。
- 首轮夹具两处复用了旧代起手或合并浮点加时，修正为真实新代及按生产顺序计算时钟，失败项单跑通过；生产未为通过夹具回退代际防护。

## 运河祭殿 Dullahan 与 The Winged

沿用CFC586/Territory794、`ShiftingAltars`入口和48边场地，新增20激活组件/24规则；寻宝累计40模块、330组件、441规则。

| 模块 / 主OID | 实际激活合同 |
|---|---|
| AltarDullahan / 2533 | 13316 R11.8/全120°；13477与13644落点R6；13315 R6恰好8人分摊；13314全队预测与独立20码撞墙击退；Matanga顺劈、5352/8598及五蔓德拉 |
| TheWinged / 253D | 13376前11.36/宽4；13377 R43.36/全60°及独立20码撞墙击退；13379落点R6；13381羽毛前40.5/宽3；13380全队预测；Matanga与五蔓德拉 |

`TreasureRadialKnockback`持有原始`DungeonPendingCast`（来源、起手代际、spell.LocXZ锁点/朝向、准备和预计判定）；`DungeonTreasureKnockbacks`保存20码径向与撞墙规则。Dullahan按登记顺序串联全部有效来源，不按Finish时间重排；Winged通过免疫、锥形和零方向门控后只应用首个有效来源。同代补正原位替换；Finish/取消/销毁清该来源，预计到期和Effect不清；Reset/Seek重建。墙边距沿源精确Clamp，正常角色hitbox≥.5；异常更小hitbox源公式会抛错，不声称支持该输入。

免疫读取严格`expiry > activation`并接即时Status事件；锥范围门控不带MP公共几何默认余量。Dullahan仅把当前13644 TerrorEye圆当击退危险落点，Cloudcover/其他危险不混入。投影经公共`QuestKnockbackProjection → DungeonOutput → Draw`供雷达/世界显示，**源KB没有AddAIHints，故不新增KB导航和强制位移执行**；普通AOE与八人分摊仍经SafeZone导航。无新增PR动作、停移或原生位移操作，真实游戏物理待实机。

All：Dullahan `[2533,2563,2547,2548,2546,2549,254A,2545]`；Winged `[253D,2547,2548,2546,2549,254A,2544,2545]`。Feather253E不参与All/敌人marker，13381仍正常接管；13378视觉不接管。奖励洋红、普通红；蔓德拉2546..A优先6..2，Dullahan2545=2/2563=1，Winged2544=2/2545=1，其余0；AllDeadOrDestroyed不等尸体销毁。

验证证据位于`.tmp/halatali-verify/`：

- `NativeTreasureWallRegression`新增8项；集中模型125项通过：`native-altars-wall-model-final.log`，涵盖模型原始参数、补正顺序、命中/免疫边界、即时状态、撞墙、落点危险、八人分摊和切人。
- 生产0错误/4项既有警告：`native-altars-wall-build.log`。
- 实际DLL专项8条PASS（含公共2项）：`native-altars-wall-focused-final.log`；全目录407条PASS/0失败：`native-altars-wall-catalog.log`。双视图发布、普通导航与KB无导航范围、真实结束/销毁、逆向Seek及owner撤销已验证；最终像素和实际宿主执行待实机。
- 专项首轮新测试把量化目标位置与原始浮点比较导致断言失败；改为按生产网格计算预期后单跑通过，生产行为未因此修改。

## Lucky Face、Airavata 与 Canal Icebeast

本机客户端表双向核对CFC819 ↔ Territory1000「惊奇百宝城」、CFC276 ↔ Territory725「运河宝物库深层」、CFC268 ↔ Territory712「运河宝物库」。对应入口`Excitatron`、`HiddenCanals`、`LostCanals`仅接管主OID377F、1FCC、1F15。三模块共27组件、34规则；寻宝累计38模块、310组件、417规则。

| 模块 | 实际激活组件 |
|---|---|
| LuckyFace | 27990/27994/27992/27996四独立R20半圆；27998/28190两个独立R10圆；28189/28000两个独立环10–20；28001落点R6；27981动态单体；28002 R6分散；27982全队预测；6448–6452 R6.84组 |
| Airavata | 9891/5352 R6组；9892 R20/全120°；9894普通R14；9893动态单体；8598 R8.42/全120°；2064常驻顺劈；6448–6452 R6.84组 |
| CanalIcebeast | 9557默认10000凝视；9558 R45.5/全90°；4486实际R6；4487 R4.56；5352 R6；8598 R8.42/全120°；1EBF常驻顺劈 |

普通几何由真实helper或怪物Start锁落点/方向，独立时钟保持到实际Finish/取消/销毁，预测到期或Effect不清。Lucky的所有Fake、MerryGoRound、28045未激活；Airavata虽声明618/8727/8629/8660类但未激活，967/966/969/968/971也未接管；Icebeast不增加9556/967输出。实际DLL验证这些动作及玩家来源不被原生规则压制。

`NativeTreasureArenas`由现有`NativeDungeonArenaExport --treasure-canals-lucky-arenas`解析匹配版本运行库的静态Arena导出。Canals保留八边形、横长矩形和南侧入口三形状并集再减北侧矩形，20顶点，最终AABB中心Z=-425.425；Lucky保留32边形减入口矩形，31顶点，最终中心Z=-460.25。导出局部点取源offset负值以匹配MP零旋转局部轴，三个场地均逐3186点与真实源Contains对照通过，不用名义中心或规则圆替代。

常驻Cleave沿用`TreasureCleaveOrigins/Cleaves`，来源OID按真实消费者映射，旧运河祭殿2545分支保持。每帧枚举全部存活可选2064或1EBF，有目标引用才输出；正在施法仍保持，动态朝当前目标。非目标避R9.42/全120°扇，本人是目标时逐个避源八槽存活队友方向R100扇，实际轮廓与导航分离，不等待8599事件启动。Icebeast凝视复用`Gazes → GazeFacing → Hints`和双视图眼睛/瞳孔/前90°弧，锁Start落点、默认10000范围和0.75秒面向窗口，实际Finish撤销；MP回放只保留面向规划，不发送PR请求。

Lucky分散复用现有`DungeonQuestParty`：R6轮廓/R7避让，读取寻宝八槽名单，少于2名存活成员不导航；Start保存目标引用，Finish按动作和目标ID删首项，Effect和预计时刻不清。原目标死亡暂隐藏、复活恢复，销毁冻结，同ID重建不替换；同代补正不扩展目标身份。此组件没有额外Shared/Raidwide预测。

All与绘制：Lucky为377F与3808..380C，只画Primary主怪及五奖励；Airavata完整16OID为`[1FCC,1F13,1F0F,1F0D,1F11,1F0C,1F10,1FEC,1FEB,1FCE,1FCF,1FCD,1FD0,1FD1,2064,2063]`，所有同OID主怪均画；Icebeast为`[1F15,1EBF,1F0E,1F0F]`，只画Primary主怪。三者均AllDeadOrDestroyed，主怪死后奖励/小怪保活、尸体/helper不保活。Lucky3808..C优先6..2、主怪1；Airavata1FCD..1FD1=7..3、2063=3、2064=2、普通八小怪1；Icebeast1EBF=2、两trash1；其他候选显式0。奖励组洋红、普通红。

生产机制沿`DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints`适配。显示、当前导航、凝视面向意图、回放隔离和撤销已离线验证；三场不新增特殊位移/交互执行。最终像素和真实PR/GreenMove执行待实机。

- 117项集中模型：`native-canals-lucky-model-final.log`；凝视夹具按实际0.75秒窗口修正后失败项先单跑通过，见`native-canals-lucky-rerun.log`。
- 生产0错误、4项既有Crucible警告：`native-canals-lucky-build.log`。
- 实际DLL专项12条PASS（含公共输入2项）、全目录401条PASS/0失败：`native-canals-lucky-focused-final.log`、`native-canals-lucky-catalog-final.log`。证据位于`.tmp/halatali-verify/`，入口213个；子代理独立复核无确定缺口。
