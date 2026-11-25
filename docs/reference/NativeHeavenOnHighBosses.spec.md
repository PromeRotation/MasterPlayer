# 天之御柱 Boss 原生脚本

本规格分别记录 Boss 接管与楼层 Zone 的边界。后文“楼层 Zone 授权机制”记录2026-11-25的续迁，前面各批“保留楼层桥”是当时的历史边界。禁止迁移探索、主动拉怪、开箱、机关交互、自动前进、道具和楼层推进。生产实现不消费 BMR 桥结果；测试中的运行库反射仅用于独立来源对照。

## 来源与身份

来源为 `MechanicRuntime/Upstream/Modules/Stormblood/DeepDungeon/DD10Mojabune.cs` 至 `DD50Gozu.cs`，以及同目录 `SharedBounds.cs`、`Components/Knockback.cs`、`Gaze.cs`、`StackSpread.cs`、`Voidzone.cs`。本机中文客户端表按 CFC 的 TerritoryType/Content 及 BNpcName 独立核对：

| Boss | CFC | Content.RowId | Territory | Boss OID | NameID |
|---|---:|---:|---:|---|---:|
| 10层亡者船 | 540 | 60021 | 770 | 23E1 | 7480 |
| 20层别蝶 | 541 | 60022 | 771 | 23E7 | 7481 |
| 30层蝶化身 | 542 | 60023 | 772 | 2251 | 7482 |
| 40层怖军 | 543 | 60024 | 782 | 23E2 | 7483 |
| 50层牛头 | 544 | 60025 | 773 | 23E9 | 7485 |

以上 ContentType 均21，Territory 的反向 CFC 均0；未套用普通副本的反向非零条件。入口为 `Presets/HeavenOnHigh/Entries/HeavenOnHigh{10,20,30,40,50}.cs`，GUID 为 `masterplayer.hoh.{层数}`。

## 机制模型及公共适配

五个 Boss 共20个实际组件、18个读条规则。普通危险区、目标分散、硬障碍仍沿 `DungeonEncounter → DungeonOutput → SafeZone/Draw` 输出；凝视沿现有 `GazeFacing → Hints → PR MechanicControl`，没有引入独立控制循环。

| Boss | 源机制及精确参数 |
|---|---|
| 10 | 11878 R7；11876 R27.4总180°；11877径向Away30、忽略免疫、撞墙停止 |
| 20 | 11899 R53总90°；11901 R8；11900默认10000范围凝视 |
| 30 | 11293前55.25全宽100；11291 R6；11292前55.25全宽6；11294内1外8圆环 |
| 40 | 11905前52.4全宽8；11903 R6；11904径向Away30、忽略免疫、撞墙停止；11902分散R6；23E3/EventState≠7原位置R6持续硬障碍 |
| 50 | 11919 R11.45总120°；11921 R20；11920前48.45全宽6；11922默认10000范围凝视；23EA无死亡/EventState过滤的原位置R8持续硬障碍 |

源枚举中的11290、11906、11923、11924没有新增组件，不推断跳跃、引爆或其他动作。只画Primary的源红色方向标记，不新增辅助体标记、优先级或自动选敌。

模型保留 source/target、起点/朝向、起止时钟、原始输入结束钟与撤销水位。原始 `InputFinishAt` 用于识别同代，`FinishAt` 用于实际判定，避免晚加载的 NPC elapsed cap 被迟到同代packet回滚。raw仅起止钟漂移不重建；真实位置/朝向/目标/总时长变化先结束旧代再开始新代。实际Finish/取消/销毁清对应记录，效果和预计到期不冒充Finish；旧事件时间早于替换水位不能清新代。销毁和缺席均撤销旧raw基线，同ID新Actor可产生新代。

默认Primary可选且InCombat拉战；HP0、死亡或销毁结束，单纯脱战不结束。BattleEnded及当前玩家死亡保留仍在进行的Boss模型，撤个人执行请求；不会以本机脱战覆盖队友仍在推进的机制。机制停用、切区和owner释放清理该脚本输出。

### 位移、凝视、分散与地板

`HeavenOnHighKnockback` 保存完整Cast、锁定spell落点、距离30、IgnoreImmunes=true、StopAtWall=true。更正Cast时同步Origin；不追踪caster后续移动。`HeavenOnHighMovements` 按Start接受顺序从上一落点继续投影，`TreasureWallLanding` 使用源射线与hitbox扣减：

```text
min(30, IntersectRayBounds(from, dir)
        - clamp(hitbox - .499f, .5f - .499f, hitbox - .499f))
```

其中下界必须保留float差值，不能改写成十进制`.001f`。当前经 `QuestKnockbackProjection` 提供双视图路径与落点三角；源KB无AddAIHints，故仅显示，无专用击退导航、自动移动或免疫过滤。真实强制位移物理仍由游戏执行，未以箭头宣称执行成功。

两种凝视锁spell落点和判定钟，按默认10000距离产生PR朝向限制，提前0.75秒；双视图为眼、瞳孔和前90°视角弧，不造地面危险圆。普通Finish、取消、销毁释放，11923视觉效果不清理。回放只验证面向模型，不向PR发送实际控制。

11902分散保留原目标引用、量化位置和目标FIFO。显示R6；多人避让按源额外1码发布R7导航，单人不发布分散导航。死亡隐藏，销毁冻结旧目标，同ID新对象不重绑旧点名。两种持续地板在原位置发布硬障碍，圈内文字为`GTFO from voidzone!`；源不筛Dead的分支保持不筛。

### 场地及楼层保留

10–40层为源HoHArena1，48边旋转3.75°与入口差集；半径使用源码的`24.5f * CosPI.Pi48th`，其中常量为1.0021457f。50层为HoHArena2，独立48边与偏心入口。由已有Arena exporter从实际运行库导出，生产无BossMod类型依赖。裁切后实际AABB中心分别为`(-300,-299.625)`、`(-300,-299.84)`。

`DungeonScript.PreservesTerritoryMechanics`和宿主Boss OID接管保留真实楼层Zone会话，包括预战和Boss结束后。Boss完成计数不包含这些Zone，测试保留会话也不代表其导航或执行已迁移。

## 验证与边界

`tests/HeavenOnHighRegression.cs`验证机制模型；`tests/ScriptCompilation/NativeDungeonReplayHeavenOnHigh.cs`通过实际生产DLL验证公共适配、双视图、普通导航、硬障碍、面向模型、反向Seek及owner清理。

- 五场地每场5772点与实际源Contains对照，含真实中心；两种KB落点使用源IntersectRay和精确float公式对照。
- 同代packet更正、真实raw替换、迟旧取消与当前取消、效果不清、过期不清、晚加载elapsed cap及随后迟同代packet保原钟。
- 缺席/销毁×此前已Finish/未Finish，同ID重建、死亡helper读条、Primary HP0/死亡/缺席/销毁及脱战保持。
- 分散目标FIFO与销毁冻结，地板过滤和文字；真实五个WIP楼层Zone在预战、战斗、Primary死亡间保持同一session。
- 回放无实际面向、ACR或GreenMove操作。普通AOE/分散导航与硬障碍离线已验证；凝视PR出口沿既有共享接口，宿主真实执行和最终像素需实机确认；KB本批提供的是来源已有的显示覆盖。

本批证据前缀为`.tmp/halatali-verify/native-hoh-`，以`model-final`、`build-final`、`replay-final`、`catalog-final`为最终日志。正式完成数仅在这些检查通过后登记。

## 60、80、90层续迁

三个模块分别为同源目录 `DD60Suikazura.cs`、`DD80Kajigakaka.cs`、`DD90Onra.cs`，共17个激活组件、16个读条输入，未将70层犬蛊并入此批。

| Boss | CFC | Content.RowId | Territory | Boss OID | NameID |
|---|---:|---:|---:|---|---:|
| 60层忍冬 | 545 | 60026 | 783 | 23E4 | 7487 |
| 80层锻冶媪 | 547 | 60028 | 784 | 23EC | 7490 |
| 90层温罗 | 548 | 60029 | 775 | 23EF | 7493 |

本机按相应CFC/BNpcName独立查询，ContentType21、Territory反向CFC0。60和80复用精确HoHArena2；90使用源半径25的独立48边入口差集，不能套半径24.5的场地。三场每场5772点Contains及AABB中心与实际源DLL对照。

| Boss | 激活组件与参数 |
|---|---|
| 60 | 11925 R10总90°；11927 R5；11926 R5.5；11929前61全宽4；11928全队伤害预测/文字；23E5创建内6外12环，创建处理钟+7.9秒，11930真实Finish按source删首 |
| 80 | 11985 R12.5总120°；11990 R6.5；11989前82全宽4；11987全队伤害预测/文字；23ED创建R8圈，创建处理钟+8.5秒，仅11992真实native效果按source删首 |
| 90 | 12213 R8；12216 R9；12215前63全宽10；12214全队伤害预测/文字；12212 R20；2413创建R9圈，创建处理钟+3.2秒，12210每次真实Finish按位置首项累计三击 |

11986、11988和12211等仅枚举的动作不扩成组件或动作执行。三种Raidwide不画全屏危险圆。

`HeavenOnHighSpawnAoe`保留创建代际、来源ID/OID、量化锁点、创建/判定时间和命中计数；`DungeonHeavenOnHighSpawns`消费快照创建与缺席、真实Finish和native效果。沿旧桥按快照构建对象身份，不重复消费PR对象创建/销毁日志。激活时已有对象的预测取当前处理钟+延迟，不倒推对象真实出生时刻。预测无容量上限，源码`with(8/10/4)`只是预分配容量。

- 忍冬：死亡、移动、超时、11930效果和无读条销毁均不清原环；持11930读条销毁时先Finish，故删除该source首环。同ID重新创建可再加一项，下一次Finish仍消费最早source项。
- 锻冶媪：移动、死亡、Finish、销毁、超时均不清冰球预测。11992消费首项，PR展开效果被排除，不能和native输入双计；非零sequence收据去重，零序号保留独立真实包事件。
- 温罗：每次12210真实Finish用caster当前原位置逐轴`≤0.1`匹配首条，前两次只增计数，第三次删除；判定钟始终为创建+3.2。销毁也按当时位置删除首匹配项，先完成当前读条Finish再执行销毁，两者可消费原先已存在的同点两球。缺席旧对象必须先于本帧任何新建/读条处理，不能误删刚在同点创建的新球。移出容差后不按ID回退。

普通AOE与三种创建预测的显示和来源已有导航沿公共`DungeonOutput`发布，导航窗口从模型Activation开始、无伪造超时；不添加冲刺、起跳、交互或其他动作执行。Raidwide预测仍为提示消费者，未新增ACR治疗策略。机制停用/owner清理删除预测与对象账本，反向Seek重建原创建钟。真实60/80/90楼层Zone仍保留且不计作已迁。

续迁证据为`.tmp/halatali-verify/native-hoh-later-*`。模型补验覆盖11根杖/12冰球、冷激活、源真实Finish与效果区别、旧对象冻结、同ID重建、无超时、同点多球FIFO、逐轴与欧氏差异、销毁先Finish以及同帧新球；实际DLL检查双视图、生产导航窗口、三种Raidwide预测、反向Seek、owner及真实楼层会话。游戏物理、真实宿主执行与最终像素仍需实机确认。

## 70层犬蛊

源`DD70Kenko.cs`，CFC546、Content60027、Territory774、OID23EB、NameID7489已按本机表核对。入口`HeavenOnHigh70.cs`，GUID`masterplayer.hoh.70`；复用真实HoHArena2，实际DLL5772点源Contains和中心对照通过。七个激活组件、五条读条及两个真实native效果均由原生模型消费；不是复用BMR桥结果。

- 12205 R15总120°；12203 R8；12207分散R3双视图轮廓、R4多人导航，单人无分散导航及伤害预测；12208只有全队预测/文字。
- `HeavenOnHighPool`保留独立normal与target槽的锁点和Activation。12207真实效果按当前主目标量化位置覆盖normal，判定为处理钟+7.2；1E9829创建按原位置覆盖normal，默认时钟。Icon1按TargetID捕获当时普通小队槽及已有normal中心，不能在后建normal时补捕获，后续normal也不能移动target槽。
- 被点名视角且非Ululation显示target槽，其他情况显示normal。12208 Start/真实Finish切换该门控，不以超时撤销。仅首个缓存1E9829的EventState7清normal，死亡、其他实体State7及无实体不清；12204清target槽/目标槽位和全部Devour，保留normal。正常池存在时，目标无论池内外都保留`Go inside puddle!`及风险布尔；池内非目标保留危险文字。原生脚本当前文字消费者是节流日志，风险模型已保留，未宣称复制旧桥面板的绿色/红色文本渲染。
- `HeavenOnHighBait`保存代际、原Source/Target引用、Cast及独立Activation。Hound12206为动态前长source到target距离/全宽14矩形，判定F；Devour为动态R10总90°锥，判定F+`3.1f`。Hound真实Finish/取消按source删首，Devour不随Finish、超时或普通销毁清除，只由12204清全组。
- Source死亡只隐藏，Target死亡仍允许；销毁冻结原引用，同ID新Actor不换绑。显示本人按ID画轮廓，导航本人按原引用判定。只有视角为任一Active Hound原目标时，整个Hound组件才输出导航及Tankbuster预测；预测目标取全部Active bait，判定钟保留首存储bait，即使首source死亡。Devour无伤害预测。
- 非目标避实际矩形/锥，被点名目标对其他存活普通小队员生成source起点R100诱导方向禁区；Hound半角`asin(7/distanceXZ)`，Devour半角45°，不按R10裁剪队友。非有限角沿旧桥可转换能力不发明替代角。普通危险区由公共门面应用一次用户危险余量，R4为机制自身半径，实际默认0.5余量后为4.5。

首次raw确认在共享合成Finish之前捕获原两组记录，仅重建当时仍存在的组；效果已消费Devour不能被确认复活。后续真实raw字段变化仍按真实Finish→Start处理，保留旧Devour并新建下一代。取消后稳定raw不复活。对象退休沿权威快照与真实Remove入口；不从PR对象日志重复构建身份，也不推测快照间未观测到的销毁。

模型、普通导航、软Goal和双视图沿`DungeonEncounter → DungeonOutput → SafeZone/Draw`验证。inverse圈保持既有R3软安全目标，不扩成禁外硬约束；Hound/Devour是危险/诱导模型，不新增自动冲锋或其他原生动作。回放不发面向、ACR和GreenMove控制。测试为`HeavenOnHighKenkoRegression.cs`及`NativeDungeonReplayHeavenOnHighKenko.cs`，证据`native-hoh-kenko-model-final2.log`453集中模型、`build-final2`0错误/4既有警告、`replay-final2`及`catalog-final2`893条PASS。真实宿主执行和最终像素仍待实机，天宫全部楼层Zone继续独立待迁。

## 楼层 Zone 授权机制（2026-11-25）

来源为`HeavenOnHigh/HoHFloorModule.cs`全部十个具体Zone、`Global/DeepDungeon/AutoClear.cs`、`Walls.cs`、`LoadedFloors.cs`、`TrapData.cs`，及`BossModule/AIHintsBuilder.cs`的普通读条消费者。HoH10–100的CFC540–549依次对应Territory770、771、772、782、773、783、774、784、775、785；本机反向CFC为0，785是91–100层地图，无独立100层Boss。不能将CFC540直接用于区域门控。

入口`Presets/HeavenOnHigh/Entries/HeavenOnHighFloors.cs`（`masterplayer.hoh.floors`）继承MP脚本；`Floor/HeavenOnHighFloorEncounter.cs`持有Actor壳、原目标引用、普通与Zone独立列表；`HeavenOnHighFloorScript.cs`负责采样与MP门面。没有创建BossModule或消费MechanicSession输出。

| 源回调 | 完整参数及消费条件 |
|---|---|
| 七种Zone凝视 | 6351 R100；12174 R25；12322/12357 R35；12261/12416 R40；12442 R30。实时来源位置和当前NPCRemainingTime；12174同时HintDisabled |
| 两种Zone圆环 | 12415内9.5外41；12432内8外30；当前Cast.Loc/Rotation，均HintDisabled |
| 打断/眩晕 | 12313/12258/12227为Interrupt；12350为Stun，只保意图及提示，旧MP没有自动动作消费者，不新增自动施法 |
| 普通自动提示 | Enemy/Helper且非Ally；排27503/33626；VFX25先于Spell/raidwide过滤；castType2/5且range≥30不输出普通危险；形状复用`PalaceTrashSpell`源参数 |
| 已揭示陷阱 | 八个源OID保持R2，不按死亡或EventState过滤，不受隐藏陷阱设置及Safety影响 |
| 隐藏陷阱/墙 | 原坐标R2、玩家30码内，忽略点逐轴≤1；25房间的真实连接位、墙半宽20、前后Depth和东西90°保持原值 |

普通列表与Zone列表不能统一清理。raw非空A→B先结束普通A再登记B，但旧Zone记录保留，继续读取同一个来源壳的B位置、方向和时钟；raw为null或来源销毁后才在源消费时点过期。死亡、Cancel日志、Effect、预计时刻不替代raw结束。Zone输出暂停期间不提前清其列表；恢复时按源规则过期。普通非冲锋危险Source=0不被HintDisabled按来源删掉，因此12415普通内3外41与Zone内9.5外41同时存在；12432普通圆因raidwide过滤而不出现。冲锋读取动态量化caster起点和当前raw落点，量化保留源乘倒数、Round和半格偏移。

VFX25捕获当时目标壳，目标移动动态跟随；原目标销毁后冻结，不换绑同ID新代，起手缺目标则保持raw落点。Actor代际按可观测OID/GameObjectId/ObjectIndex区分；不猜快照之间不可见的销毁。raw先于同actor状态，packet只补Zone回调，不制造普通AOE；packet首见后nullable IsSpell raw仍必须初始化普通列表，结束收据拒迟到旧包。NPC首次elapsed封顶原Total，随后推进才封顶Total+Extra，输出始终用源float的Total+.3−Elapsed。

Zone构造订阅不补播已有Actor/施法；真正对已填充世界冷启不恢复历史Zone眼或Beacon忽略点。录像从空feed启动后首帧是热输入，与冷启分开验证。7248只清Zone和楼层危险历史，保Actor壳、普通读条与结束收据；随后新层Created/施法仍是热事件。7255/7256仅隐藏假想陷阱；tileset2无墙但仍可发布陷阱。无有效普通地图、Boss楼层和过层等待不发布Zone危险；普通公共自动提示按原独立门控处理。

`tools/generate_hoh_floor_data.py`只生成坐标，无房间目标或路线。500房间、4432陷阱逐项与源表对照（十地图分别513、510、606、544、374、387、350、370、368、410点）；560个非default墙中448个Depth1、112个Depth0.5。墙的实际SDRect符号与MP几何有独立网格对照，未用矩形外包场地替代真实通道。

显示从同一`PalaceArea`生产状态发布到`Draw.AddAoePreview`供雷达和世界绘制，导航独立注册精确危险/障碍并仅应用一次用户余量。凝视发布眼、瞳孔、前90°弧，联合方向解算后经`Hints.SetFacing`每帧续期；回放保`PlannedFacing`，不提交PR请求。没有本批动作限制/反伤机制，不复制Palace的Spikes或扩大为全停手。

九个独立Boss通过`DungeonScript.ObserveHeavenOnHighBoss`即时采样及Render交接，不依赖脚本遍历顺序。源Boss动作固定排除，关闭Boss脚本也不恢复对应通用AOE。HoH十区域的BMR输出由`MechanicHostController.ModulesEnabled`关闭，包括无Boss的785；其他未迁区域的桥继续保留。Boss脚本原`PreservesTerritoryMechanics`仍用于逐动作通用躲避门控，不代表楼层桥还在输出。

源StoneCurse437（expire>now+1.5）及1097且绝对HP<10只写目标priority0；61/2056/53是自动清怪提权过滤，可能选中未接战目标。旧MP桥无Zone目标执行消费者，且本次禁止迁移主动清怪，因此记录源范围，不新增选敌、禁攻或拉怪。ForbidDOTs仅意图，执行继续暂缓；箱/Beacon交互、fake exit选择、房间探索、FullClear、MaxPull、道具与层间推进均未迁移。

验证文件为`tests/HeavenOnHighFloorRegression.cs`及`tests/ScriptCompilation/NativeDungeonReplayHeavenOnHighFloors.cs`。日志统一前缀`.tmp/halatali-verify/native-hoh-zones-20261125-`；`model-02.log`38项集中模型通过，生产`build-02.log`为0错误/4项既有Crucible警告。`replay-floor-06.log`16条PASS（含2条公共），同DLL的`catalog-01.log`988条PASS/0FAIL，进程exit0；首轮失败日志保留，反射访问夹具与冷启测试先后已修正，没有降低机制断言。DLL SHA256为`759037d19a4f3b083ca8cdafa4773377ec11c1814b2e57b88867966c9415f09d`，副本保留在同目录`native-hoh-zones-20261125-artifact/MasterPlayer.dll`。真实解算器的隐藏陷阱脱险无least-danger fallback；十区域双视图状态、真实raw结束、冷启/热转层、nullable packet/raw、反向Seek与owner清理均通过。九Boss原专项及区域禁桥也在全目录复验。真实PR朝向、GreenMove物理和最终双视图像素仍待实机，合成事件不是实录录像。
