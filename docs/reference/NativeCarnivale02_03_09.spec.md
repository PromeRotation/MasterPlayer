# 假面狂欢第02、03、09场 MP 原生规格

## 来源与身份

来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage02MuchAdoAboutPudding/Stage02Act1.cs`、`Stage02Act2.cs`、`Stage03WaitingForGolem/Stage03.cs`、`Stage09ToKillAMockingslime/Stage09.cs`。共四模块、14个实际激活组件、7条逐幕规则，5种不同Action。没有将枚举或攻略中的技能当作已激活组件。

本机Lumina与简体中文sqpack核对：第02场「布丁拼盘」CFC612→Instance35002；第03场「最初的岩壁——希帕克纳」CFC613→35003；第09场「七彩甜点——棉花软糖」CFC619→35009，均为Territory796。主OID依次为25C0/25C1、25D4、242D；NameID8078/8079、8084、8099分别对应斗场软糊怪/斗场明胶怪、希帕克纳、棉花软糖。沿用GUID `masterplayer.carnivale.stage01`，四模块分别有机制开关。

## 生命周期和机制

| 模块 | 构造/战斗组件 | 阶段和显示 |
|---|---|---|
| 02 Act1，25C0 | 构造Hints，战斗Hints2和GoldenTongue | 任意25C0/25C2/25C4进战拉战，全部DeadOrDestroyed完成；HP0不当作Dead。预战攻略开战替换，全部三类敌人均标记 |
| 02 Act2，25C1 | 仅战斗Hints和GoldenTongue | 任意25C1/25C5/25C3进战拉战，全部DeadOrDestroyed完成；预战仍有场地和敌人标记，没有攻略文字 |
| 03，25D4 | 构造Hints，战斗Hints2、EarthShake、EarthenHeart、Obliterate | 默认Primary可选且InCombat拉战，Primary HP0/Dead/Destroyed结束；预战与战斗攻略不同 |
| 09，242D | 构造Hints，战斗GoldenTongue、Dark、DarkVoidzone | 默认Primary生命周期；开战撤攻略，无战斗常驻攻略；2711–2716六种add保橙色 |

02两幕及03用源CircleBig，09用CircleSmall。场地为真实64边形，不替换为数学圆；模型、显示与导航继续经 `DungeonEncounter → DungeonOutput → SafeZone`，不消费BMR运行输出。

| Action/对象 | 源参数 | 原生模型与输出 |
|---|---|---|
| 14265 GoldenTongue | CastInterruptHint | 02两幕及09各自独立打断意图、原caster引用/代际/时钟；文字Interrupt!，没有伤害预测、危险圈或自动打断 |
| 14363 Boulder Clap | SimpleAOEs，R14、总角120° | 锁CastInfo落点和朝向，真实Finish/取消/销毁清理，普通AOE双视图及导航 |
| 14364 EarthenHeart | 读条及效果预测R6，Event对象1E8FEA | 动态读条目标、真实MainTarget或包落点效果预测、live实体三路独立；效果用处理钟加 `(double)1.2f` |
| 14365 Obliterate | CastInterruptHint | 纯打断意图，未增加Raidwide预测 |
| 15057 Dark | SimpleAOEs R5，加独立R4毒区三路 | 普通R5锁点AOE与R4动态目标/效果/live三路独立；效果处理钟加1d；live OID1E9C9D |
| 1E8FEA/1E9C9D | PersistentVoidzoneAtCastTarget | 仅按EventState!=7过滤，不额外按死亡或Targetable过滤；每个有效实体只接替首个距离≤2预测 |

毒区效果必须存在真实source，不要求此前Start；真实MainTarget可解析时取其位置，否则使用包点。零序号包仍为独立事件，legacy展开不重复消费Native动作。普通读条真实Finish清理，效果及预计到期不清；效果预测与live交接不冒充真实读条结束。

09对全部合法PotentialTargets无条件覆盖Boss=0、其他=1，包括PendingDead候选；并非只认可六个add，也不继承其他场次保留-1的setter。模型和实际DLL以HP1 add验证该覆盖。没有新增禁攻或全停手。

## 激活、代际与撤销

02两幕各自使用PuddingNeedsBootstrap，激活后在raw对账完成时补播当前14265；普通Start分发跳过尚待补播的owner，避免首帧重复。晚加入后台模块即使raw字段完全不变，也能接到当前打断，展示切换不丢该历史。

五种Action的raw身份按Action、类型、目标、TotalTime、位置和朝向判断，纯StartedAt/FinishAt饱和漂移不触发Finish→Start。已接受记录保留原钟及generation；首次激活未认领raw可以建立记录，NPC已饱和时保留当前时钟加float0.3的余时。真实字段变化仍走新代。预战真实Finish为未认领读条建立墓碑，unchanged raw不得在拉战时复活；合法新字段变化仍可接受。Cancel/销毁及迟旧Finish沿用公共代际屏障。

## 验证与边界

`tests/CarnivalePuddingGolemMockRegression.cs`含12组专项；集中Carnivale模型227项通过。`tests/ScriptCompilation/NativeDungeonReplayCarnivalePuddingGolemMock.cs`使用真实ScriptManager、ReplayController和输出组件，覆盖四模块各5772点源DLL场地Contains对照、双视图/导航、群体结束、纯打断、毒区三路、真实效果源、HP1优先级、晚owner补播、五动作饱和钟与预战取消、反向Seek及owner清理。实际DLL专项14条PASS（含两条公共检查）。

证据为 `.tmp/halatali-verify/native-carnivale-pudding-golem-mock-model-authoritative.log`、`native-carnivale-pudding-golem-mock-build-authoritative.log`、`native-carnivale-pudding-golem-mock-focused-authoritative.log`、`native-carnivale-pudding-golem-mock-catalog-authoritative.log`。生产编译0错误、4项既有Crucible警告；实际DLL全目录832条PASS、0失败，复用同一生产DLL。

显示及来源提供的普通AOE/毒区导航离线已验；四模块没有特殊移动、凝视或自动动作消费者，不据攻略扩大控制。预战只有显示，无导航或PR朝向请求。真实宿主加载、GreenMove及最终游戏像素仍待实机，ARR/MP回放不发送原生控制。
