# 正统优雷卡 Boss 原生机制

本规格只覆盖 Boss 战斗机制。禁止迁移深宫道中探索、开箱、机关交互、自动前进、道具使用和楼层推进；楼层 Zone 共存不计作 Zone 已迁。统一寻路试点不在此实现。

## 入口与来源

| Boss | Territory | CFC | OID | NameID | MP 入口 |
|---|---:|---:|---:|---:|---|
| 10层蜜言妖 Gancanagh | 1099 | 897 | 0x3D54 | 12240 | `EurekaGancanagh` |
| 20层复制系统 Cloning Node | 1100 | 898 | 0x3E77 | 12261 | `EurekaCloningNode` |
| 30层提亚马特复制体 Tiamat's Clone | 1101 | 899 | 0x3D9A | 12242 | `EurekaTiamat` |
| 40层双塔尼亚复制体 Twintania's Clone | 1102 | 900 | 0x3D1D | 12263 | `EurekaTwintania` |
| 50层自控化奇美拉14X | 1103 | 901 | 0x3D9C | 12265 | `EurekaChimera` |
| 60层自控化弥诺陶洛斯16 | 1104 | 902 | 0x3DA1 | 12267 | `EurekaMinotaur` |
| 70层 Aeturna | 1105 | 903 | 0x3D1B | 12246 | `EurekaAeturna` |
| 80层 Proto-Kaliya | 1106 | 904 | 0x3D18 | 12247 | `EurekaProtoKaliya` |
| 90层 Administrator | 1107 | 905 | 0x3D23 | 12102 | `EurekaAdministrator` |
| 99层 Excalibur | 1108 | 906 | 0x3CFC | 12100 | `EurekaExcalibur` |

映射按本机简中 Lumina 的 ContentFinderCondition、TerritoryType、BNpcBase 与 BNpcName 定向核对；CFC 不作为 Territory 使用。源为 `MechanicRuntime/Upstream/Modules/Endwalker/DeepDungeon/EurekaOrthos/` 中对应 DD10–DD99 模块及其直接消费的 `GenericAOEs`、`ChasingAOEs`、`Twister`、`Voidzone`、`Knockback`、`BaitAwayChargeCast`、`StretchTetherDuo/Single`、`ActorState`、`Angle` 和 `BossModuleManager`。

生产模型在 `Presets/EurekaOrthos/`，由 `DungeonScript → DungeonEncounter → DungeonOutput → SafeZone` 输出。模型没有创建 BossModule、消费 BMR 输出或依赖寻路节点。入口只按本 Boss 的 Territory/OID 接管，不压制楼层其他模块。

## 10层：地雷与范围

三个实际激活组件：31478 为不限数量的 R10 圈；32700 同为 R10，但显示及导航只消费按开始顺序的前六项；31479 为源实际实现的 R20 圈，不采用注释中的 range60。31477 只有枚举，不增加机制。

场地为中心 `(-300,-300)`、半宽19.5方场。读条锁定 Loc、方向及含 NPC 0.3 秒修正的判定钟；等待真实 Finish、取消、替换或施法者销毁，不因预计时间经过、普通死亡或不可选而提前清理。32700 保存全部隐藏尾项，前项结束后按原时钟和原位置晋升；用显式顺序表防止字典删除后复用槽位改变顺序。

## 20层：吐息队列

两个实际激活组件：32547 为前长40、全宽5的锁定矩形；FlameBreath 由完整原生32552效果包创建，32864仅作为真实 Finish 输入，不从其 Start 合成历史。32543、32544、32545、32553不扩成新机制；32554只是 NPC Teleport，不注册玩家传送边或执行。

每个完整32552包追加独立代次，重复非零或零序号、无目标包均消费；legacy按目标展开的事件不重复添加。保存来源、原位置/朝向、准备时间和判定时间。原点取事件消费当时的 caster 世界状态，准备钟为消费钟，判定为 `now+9.6d`，不使用包的旧 Timestamp 或目标落点。

离中心平方距离严格大于625时，按来源朝向前推22码；与四个实际斜角常量之差归一后小于等于1度时改为22.627417码。浮点常量乘法、循环归一和 double SinCos 再转 float 保持源运算顺序。先半码舍入，再仅对远分支的 `(-315,-315)` 修正为 `(-315.5,-315.5)`，最后量化。近分支只舍入及量化。模型保留原位置与结果，不能只剩投影终点。

吐息为R50、总角30度锥。显示、危险区及范围文字消费相同的严格批次：从队首开始，遇到 `Activation >= 队首Activation+1秒` 即停止；不能排序、全表过滤或按初始容量5截断。任意来源真实32864 Finish删全局首项，不要求来源与预测相同；没有预测超时。创建后不随来源移动、死亡或销毁而改变；施法中的 helper 销毁先按真实 Finish 消费。范围文字沿源圆锥包含判据，源点和隐藏批次不提示。

## 输入确认与生命周期

首次 raw 是读条权威确认；与 packet 的落点、目标、开始时间或时长不同只补正字段，不制造 Finish、不消费吐息 FIFO、不改变地雷插入顺序。之后真实 raw 字段变化仍按 Finish→Start；已有 raw 基线时迟到包不能回滚。只有 packet、从未见到 raw 的超时撤销不冒充源组件的32864 Finish。真实相同 raw 不续期；迟加载仍保留源 float 饱和 NPC 余量。

源模块加载身份独立于观看角色和输出。原 Party0 在500码三维距离内时可加载预战身份；缺视角冷启动仍推进世界机制，个人输出由当前视角门控。预战离距卸载回 pending，不记完成；已拉战不因普通脱战或离距清组件。主怪死亡、HP0或缺席结束；持续存在的旧 Actor 回血不能重启，真实缺席后同ID新代可以启动。

Director `0005` 卸载已加载身份和组件、冻结拉战；`0011`只解除冻结，不重新枚举已卸载旧 Actor。wipe期间真实新Actor可加载，恢复后才拉战；pending不错误记为完成。挂起脚本仍接收Director。纯 `DungeonOutput.Clear()` 仅撤销显示和区域，重新Apply保留历史；`ResetFight()`是机制卸载，不宣称保留历史效果。

## 显示、导航和执行边界

场地、源敌人标记、普通读条及当前吐息批次经共享预览提供雷达与世界绘制；危险区由同一原生模型发布。普通范围导航使用当前生产 SafeZoneSolver；预战场地和标记仅显示。这两场没有激活凝视、面向、停手禁攻或玩家特殊移动，不新增对应执行。

ARR及MP回放只规划、绘制和提示，不提交PR面向、ACR请求、GreenMove或原生操作。关闭机制和owner停止撤销全部个人输出；Seek重建事件历史。共享显示数据、模型及实际解算器的离线验证不能替代游戏内最终像素、碰撞和宿主执行验收。

验证实现为 `tests/EurekaOrthosRegression.cs` 和 `tests/ScriptCompilation/NativeDungeonReplayEurekaOrthos.cs`。集中验证结果按最终实际日志登记。

## 30层：五连追踪与移动头

激活五个源组件：DarkWyrmwing、DarkWyrmtail、DarkMegaflare、WheiMorn、CreatureOfDarkness。31846/31844前长40全宽16矩形，31850为R6；召唤与Visual不独立接管。方场中心`(-300,-300)`、半宽19.5。

Icon197保存事件Source Actor原引用，允许重复，TargetId仅为源mask用途；无效Source不回退Target。31847真实raw Start按锁定LocXZ选择当前最近的候选，平局保首；移除其首份候选，建立五击、首次移动距离0的追踪。模型保存来源输入、原目标引用、Previous、Remaining、步长和Activation；同ID重建不能替换旧目标。完整31847/31848每包按离真实落点最近的预测追踪消费，不限制caster或距离，不去重序号。自目标取caster当前量化位置，其他MainTarget取当前原位置，缺目标才取包区域坐标；旧录像两者都缺失时不猜零点、不推进，并明确提示待确认。下一击以消费钟加2秒、步长6推进，第五击删除；全部消失才清候选。Finish/取消/预计时间经过不删除追踪；首击前保留死亡或Removed目标，首击后在实际组件Update中撤销。

显示仍为真实目标预测点R6及Previous至原目标当前位置的线。导航吸收PR #5的追踪策略：本人首击为锁定原点R6，后续为Previous中心R12.5，要求向外逃离；其他人仍避实际目标预测点R7。每击导航窗口为Activation至Activation+0.8秒，不因该窗口结束删除事件历史。本人不再按当前朝向偏移导航中心。

真实raw创建追踪时，以被点名者当时位置及锁定落点在场内网格选择一次固定诱导终点；不从当前观看角色计算，不因目标移动、转向、重复Apply或切视角重选。首击前2.5秒开始发布R1目标，目标窗口覆盖剩余整段五连（下一击Activation加剩余后续击数×2秒再加0.8秒），避免要求在首击前到达远端；之后随真实效果校准，仅原目标引用为当前观看者且存活时生效。非点名队友只接收危险和显示。第五次真实效果后，为该原目标保存10秒的中央R8回位目标；新追踪、死亡、移除、过期或机制重置撤销，其他角色不继承。目标不覆盖危险余量，继续由SafeZone规划安全路线，不直接调用Move。Clear只撤输出，Seek按事件历史重建，owner停止清理危险、目标及显示。

移动头仍只按OID3D9B及AnimationState1==1筛选，保留死亡标志、可选性和EventState不参与筛选的原契约；真实位置R2、长6胶囊用于显示；当前位置R2以当前时刻至其后0.2秒的移动占用窗参与导航，随观测续期。它不再作为整条路线期间不动的硬障碍，避免提前封死龙头经过后会腾出的通道；当前占用仍阻止穿越，并只允许已重叠的玩家先退出。独立轨迹保存原Actor引用、原位置/朝向和首次激活后1秒的起步钟，按3码/秒预测；观测水平偏差超过0.9码或方向差超过0.05弧度时用当前观测重新锚定，失活、销毁和代际替换撤销。普通阶段以0.5秒步长、提前0.8秒/尾随0.9秒发布R2.5胶囊；31849真实raw观测后26秒内改为0.15秒步长、提前0.15秒/尾随0.25秒。每帧保留仍有效的尾段并向前覆盖约15秒，所有切片具有有限起止时间及HasMotionEnvelope标记，解算器不重复叠加读条准备时间。Visual只选择时间精度，不新增独立AOE。

EO30只发布危险窗口和个人目标，复用现有A*、安全时间区间搜索及移动执行，不申请独立解算策略。此前新增的`preferTimedRefuge`请求、设置、搜索分支、等待点锁及读条/终点停留特例已撤回；这些核心文件恢复为迁入EO30导航前的实现。移动控制继续受总开关及现有owner生命周期管理，ARR/MP回放只规划和绘制。

31844/31846/31850由EO30机制组件保存锁定范围，沿通用Ambient输出预计命中至其后0.6秒的明确有限窗；不再同时发布普通OutputCasts。真实Finish沿现有completed标志区分正常完成与取消：正常完成保留到命中窗结束，明确取消即使接近命中也撤销；来源死亡/移除、新读条代际及机制重置撤销。共享DungeonOutput不再识别EO30技能ID或猜测自然结束；输出重建不会丢失机制历史，owner清理撤销所有已发布区域。不改变真实点名和五连效果的消费语义。

回退后的对照与最终验证保存在`.tmp/eo30-simplify-20261125/`。`without-mode.log`验证只撤回核心策略、仍保留原机制数据的版本：两个起点各106次实际范围判定、21751个龙头位置样本，在300ms模拟后台延迟下均为0命中、0重叠，说明通过该录像无需额外策略。`model-final.log`验证机制生命周期调整后的最终版本；录像仍使用PR原始夹具`eo30-030529.json`驱动本地`DungeonEncounter → DungeonOutput → SafeZoneSolver → Dispatch → FakeMove`。录像没有raw结束记录，夹具按显示读条时长模拟CastInfo消失；追踪效果按模拟玩家位置独立推进，龙头碰撞以观测位置独立检测。此闭环没有模拟职业读条或真实游戏物理。

`replay-targeted.log`记录实际DLL的EO Boss/导航回放与采集验证，包括固定诱导、第五击回位、目标移动、时间窗、反向Seek及停止清理；`build.log`记录Release构建。普通范围回归还覆盖自然结束、明确取消、输出重建、死亡、移除、新施法替换、过期及重置。回退前备份为`before-simplify.zip`，更早的迁入前备份仍在`.tmp/eo30-native-navigation-20261125/before-edit.zip`。ARR/MP回放隔离不变，实机PR/GreenMove与最终双视图像素仍待验收。

## 40层：双旋风、风池、俯冲与涡轮

八个激活组件分别为两套CastTwister、两条CastHint、BitingWind、MeracydianSquall、Turbine、TwistingDive。圆场中心`(-600,-300)`、R20；31466为R5普通圈。Twisters提示仅为文字，不新增必须持续移动的自动执行。

两套旋风分别由31468/31471真实raw Start排程，互不覆盖。阈值为含NPC余量的FinishAt减`.25f`；有预测或存在任何OID1E8910对象时不在Update采样，State7对象同样阻塞。首次满足条件的实际Update采当前存活队员原位置，Activation为该消费钟加`(double)(.25f+.4f)`；提前真实Finish尚未采样时改为消费钟加`(double).4f`，不要求对象列表为空。每个同OID对象Created同时清两组预测，但不清等待阈值；只按原Actor代际发生一次。State7不显示，实际对象显示R1.5且不量化；无预测TTL。Created和CastFinish按源对象索引顺序交错处理，不能统一提前Created。

31463风池保留raw读条目标当前位置/缺失回退LocXZ、完整效果MainTarget/区域坐标加`(double).9f`、OID3D1E且EventState!=7实体三路。实体只量化导航/轮廓位置，不加死亡或可选过滤；每次实际组件Update，每个有效实体只消费首个距离真实位置≤2的效果预测。事件前刷新只采输入和对象事件，不执行组件Update，避免一帧多个包或Timeline扫描重复消耗。packet-only读条不建立风池。已知主怪Timeline同样刷新姿态，锁定当前值。

主怪Timeline1E3A设置准备门闩，之后每个1E43覆盖长50全宽15矩形，锁当时量化位置/原朝向与消费钟+6.9秒。1E3A不清旧矩形，1E43无需31471 Start；任何来源真实31471 Finish清矩形与门闩。其他Finish、效果、时间经过不清；不从普通31471 Start补一份矩形。

`EurekaTurbineKnockback`保留原Cast来源/代际、锁点、判定与真实结束，类型AwayFromOrigin、距离15、IgnoreImmunes=true、StopAtWall=false，无R60资格条件。逐源落点投影与原角色标记沿`QuestKnockbackProjection`仅显示；落点在任意风池内或场外时提示。导航独立取首源判定时间，禁区为“场地中心R5外 ∩ 全部当前风池方向R20/半角20度锥”。`DungeonOutput`集中适配：无风时直接发布覆盖场内的R5外环，有风交集以源Distance和既有半码采样输出；不拆成并集，也不额外叠加击退落点反投影。无风直接环形适配防止通用采样器覆盖率保护漏掉补集。模型保留全部位移参数，不依赖解算栅格或内部节点。

## 30/40层输入与验收边界

EO真实Spell来源排除Player/Pet/Chocobo/Buddy，原生类型由实时及回放同一路输入保留；不以FriendlyNpc代替这四类。完整效果包另保存可选ActionType，当前采集读FFCS公开Header.ActionType，已知非Spell不消费；旧录像缺字段保持未知并沿已有按ActionId路由的范围处理，不伪造类型。实际DLL专项同时验证Capture队列与生产录制/读回对该字段的保真。

两场均无凝视、PR面向、停手或禁攻激活组件，不新增相关请求；涡轮是机制强制位移的模型、显示及源导航，没有新增玩家跳跃、传送或原生动作执行。30/40模型验证见`EurekaTiamatRegression.cs`、`EurekaTwintaniaRegression.cs`；实际DLL继续共用`NativeDungeonReplayEurekaOrthos.cs`。离线显示数据、当前SafeZone导航与真实游戏物理、最终双视图像素、PR/GreenMove执行分别验收，后者仍待实机。


## 50层：双队列咆哮、冲锋与球线

八个实际激活组件全部保留。圆场中心`(-300,-300)`、R19.5；31863/31861为R40总180度半圆，其他Visual/AutoAttack不独立接管。

`EurekaVoice`分别保存raw队列和Model队列。31851按R9圆→8–40环、31852反序，以锁定LocXZ与FinishAt/FinishAt+3秒追加；raw存在时隐藏但不删除Model。Model/Anim元组真实变化到4/5且两队列空时建两条消费钟+2.4/+5.5秒的预测，保留Model来源原引用、原Model值与准备时间，位置动态取主怪量化位置。raw仅首条Risky，Model首条非Risky而第二条Risky；首条颜色按源保留。六种完整Voice效果逐包删全局首，raw优先；Finish、取消、时间经过不删除。相同Actor输入按源Model→Cast→Tether顺序，活动期间新建Actor按Create→初始Tether→Dead/Model→Cast顺序；激活已有Actor不回补Model事件，Pet不分发Model事件。对象索引稳定排序，晚于来源首次Created的目标不能被前方连线提前解析。

`EurekaCharge`保留31855/31856的source/target原引用和Cast。显示全宽8、长度与朝向动态取两端，目标同ID重建不换绑；仅真实Finish按source/action删首。目标本人显示轮廓，其他人避矩形；本人按每个活队友发布R100、半角asin(4/distance)的方向禁区。两种action分别保留原目标伤害预测及原列表首判定钟，只作当前已有的诊断消费。

每次OID3D9D真实TetherGain均追加球原引用，不限定TetherID且允许重复；完整31865仅删caster原引用的首份。Untether、死亡、销毁不删除球历史。显示原位置R6，导航为原位置/原朝向半径6、长度10胶囊。普通Tether操作按清洗后的Target是否非零发Gain/Lose，激活回溯按Type非零补播，不能混用两种门控。

`EurekaStretchTether`保存17单线与57/1双色两套独立bait、membership和首玩家原引用timer，分别为0秒与源`(double)5.1f`。Gain重复登记并复用尚存timer；实际Update到activation+1秒只删timer，不删bait。Sides按调用当时活的原Party成员决定方向；Actor输入期间使用上一帧已提交Party，全部Actor事件完成后才提交新Party；随后packet消费新Party。Lose删标准化pair所有bait但只删原始Target一份membership，保留源死亡反向等边界。Cancel按Source存的旧线消费，忽略取消包Target/Type。单线Kite提示与父类Stretch提示互斥。活来源打开个人前景门后显示全部原bait线，导航仅取活来源且目标原引用为当前观看者的R15禁区。绘制与导航不能用ID相等替代引用身份。

## 60层：八连挥击、雷球与击退

五个实际激活组件全部保留。圆场中心`(-600,-300)`、R20；32795为R40总90度锥，31875为R13圆。

`EurekaSwipe`保存不限数量31867真实raw预告及原Cast，首项为FinishAt+8.7秒，其余以当前首activation+追加前count×2秒计算；仅显示前二且两条均Risky，第二条追加时将首条染为Danger。31872/31868–31871完整效果每包删全局首并将新首染为Danger，不去重相同序号；Finish、取消与预计时间经过不消费。隐藏尾项晋升保留原Cast、方向及判定钟。

OID3DA2每个真实Created按出生时量化位置、消费钟+13秒追加`EurekaShock`，保留原Actor引用与PreparedAt；无数量上限或超时，普通死亡/销毁不删历史。同ID重建是新代。任何来源真实已Observed31874 Finish全清，未知或只有packet的取消不伪造清理。观看角色缺失仍推进Created/读条及真实结束，恢复视角不重新计时或重绑出生点。

`EurekaGroanKnockback`保存31876原Cast、来源/代际、锁定原点、判定与真实结束，AwayFromOrigin15、免疫有效、StopAtWall=false。`QuestKnockbackProjection`只适配双视图投影和场外落点提示；独立导航始终取首source原点R5外禁区，即使本人免疫也不撤源导航。没有新增原生强制位移操作或传送/跳跃路线。

50/60模型由`DungeonEncounter`消费事件，经`DungeonOutput`发布共享AOE预览、独立Line预览和SafeZone危险区；Line不能在AOE预览数组中冒充已验证。两场没有激活凝视、PR面向、停手或禁攻组件，不新增执行。回放保持只绘制/规划，物理位移、真实宿主执行及最终像素仍待实机。验证为`EurekaChimeraRegression.cs`、`EurekaMinotaurRegression.cs`及共用实际DLL回放；结果按最终日志登记。

## 2026-11-25：10/20层验证证据

- `native-eureka-final-model.log`：集中798场景，795首过。三项旧佐特/巴别夹具在同一时刻结束后又开始新读条，被已有旧事件屏障拒绝；只将下一次开始推进100毫秒，原断言全部保留，`native-eureka-failures-corrected.log`三项单跑通过。本批13项EO模型全部首过。
- `native-eureka-final-build.log`：生产0错误、4项既有Crucible警告，使用同一DLL进行后续集中回放。
- `native-eureka-final-replay.log`：实际DLL两场各6724点源场地Contains对照；直接调用源CalculatePosition和Quantized核对72例包含一度容差相邻float及跨圈角度。前六地雷/隐藏晋升、完整包与legacy隔离、首次raw/迟旧包、范围文字、源Party0缺视角冷启动、Director卸载/同ID新代、真实SafeZoneSolver、逆向Seek及owner清理通过。
- `native-eureka-final-catalog-rerun.log`：实际DLL全目录902条PASS、0条FAIL，退出码0。首次命令误用不受支持的`--catalog`，随后按Usage改为`--catalog-replay`；错误调用不计验证。

累计678个源类型离线已验、536个待迁，256个内置脚本入口；本轮没有实机执行或最终渲染像素验收。所有日志位于`.tmp/halatali-verify/`。


## 2026-11-25：30/40层验证证据

- `native-eureka3040-focused-model.log`：30项EO10–40定向模型全部通过。新增原Cast溯源、来源类型/ActionType、输入扫描不消费Update、双向ObjectIndex与Created/Finish交错反例均进入本次重编产物。
- `native-eureka3040-build.log`：生产0错误、4项既有Crucible警告。
- `native-eureka3040-replay.log`：实际DLL14条专项PASS，包括四场各6724点源边界对照、涡轮5043点直接源Distance对照、追踪五击/缺输入提示、头四窗口/硬障碍、旋风采样与提前Finish、State7、Timeline、当前SafeZone导航、反向Seek和owner清理。共用冒烟同时验证原生ActionType捕获及生产录制/读回。
- `native-eureka3040-catalog.log`：同一实际DLL全目录907条PASS、0条FAIL，退出码0。
- `native-eureka3040-model.log`为扩大到整个默认模型集合的另一次运行，有其他区域失败，保留失败原文并独立核查；不得把上述定向或全目录回放说成全模型通过。早期定向重编曾被该运行锁住DLL，最终定向使用独立临时输出目录成功；失败的编译尝试不计通过。

本批登记后累计680个源类型离线已验、534个待迁、258个内置入口。真实游戏物理、PR/GreenMove执行与最终像素待实机。


## 70层：碎裂、凶暴连线与延迟钢爪

八个实际激活组件全部原生实现。圆场中心`(-300,-300)`、R20；31436为R15圆、31437为6–30环、31441为R3、31438为R5。31435只保全屏伤害文字和预测，不画避让圆；31446为NPC Teleport枚举，不新增玩家传送或动作。

`EurekaShatter`保存原触发Cast、原水晶引用、锁定当时量化位置/原朝向、准备钟及FinishAt+0.5秒。31442真实Start置Ferocity闩锁，其Finish不清；未锁存的31436/31437 Start分别为全部当前OID3D1C水晶追加R23.95总150度锥/R8圆，不筛死亡、可选性或截断数量。水晶列表首次请求才按持久世界字典建表，此后按创建追加/销毁删除，字典空槽复用不重排已建列表；同帧逐Actor更新，不能提前读取后方Actor移动或创建。冷激活补播按持久世界字典，活动期按真实对象索引。预测不随水晶移动、死亡、销毁、时间经过删除；31439/31440任意真实Finish全清，31436/31437真实Finish仅复位闩锁。Cancel/Interrupt只刷新raw，不提前制造源Finish。危险文字按源无epsilon圆/锥判据，cone原点不报警，显示/导航仍消费原几何。

`EurekaSteelClaw`保存31445原Cast、来源/目标原引用及起读时旧Party槽。只有TargetID等于caster自身才回退caster当前TargetID；零TargetID不回退，未解析目标不加。Finish、取消、死亡、销毁与预测到期均不删；完整31445按MainTargetID删首份匹配，重复序号逐包消费，错目标不删。仅提供Tankbuster文字与原目标/原槽/原判定伤害预测，没有额外AOE导航。

Ferocity复用真实消费者已有的`EurekaStretchTether`，57/1双色、15码导航和源`(double)5.7f`独立timer；保留原引用、死亡反向、重复membership、Source-only取消以及timer过期不清bait。好线仍保留源R15导航。活来源门控、线段安全色及全部旧bait显示与导航分别验证。

三场均沿`DungeonEncounter → DungeonOutput → SafeZone`发布；普通AOE/拉线危险与当前解算器导航已离线验证，连线通过独立Line预览向两视图发布。没有激活凝视、PR面向、停手或禁攻组件，不增加对应执行。回放不发送原生操作，真实PR/GreenMove、强制位移物理及最终像素仍待实机。

## 2026-11-25：50/60/70层集中验证证据

- `native-eureka70-final-model.log`：59项EO10–70及公共读条边界模型全部通过。覆盖同一时间结束过的全部action水位、T02 ADS旧raw恢复常驻顺劈、旧/新Party时序、原引用/代际、真实结束/取消、冷回溯、惰性列表及隐藏队列；未将历史全默认模型失败计为通过。
- `native-eureka70-final-build.log`：生产0错误、4项既有Crucible警告，后续共用该DLL。
- `native-eureka70-final-replay.log`：21条实际DLL专项全部通过，包含七场各6724点源场地对照，双视图预览和独立连线、导航及真实SafeZoneSolver、完整效果/真实结束、Cancel/raw先后、缺视角全局历史/主目标消费、同ID新代、原判定时钟、逆向Seek和活动owner撤销。
- `native-eureka70-final-catalog.log`：同一实际DLL全目录914条PASS、0条FAIL，退出码0。
- 50/60独立复核前一版的48项模型、18条专项及911条全目录证据保留在`native-eureka5060-*-parity-final.log`；最终上述59/21/914验证包含随后Party顺序修正和70层。首次新增实际DLL反例跨Seek保存旧实例导致断言失败，已改为每次检查当前重建状态，保留失败日志`native-eureka70-replay.log`，未弱化原引用/槽位断言。

累计683个源类型离线已验、531个待迁、261个内置入口。深宫楼层Zone与道中流程均未因本批计完成；未进行实机执行或最终像素验收。

## 80/90/99层来源与输入合同

来源为上述目录的`DD80ProtoKaliya.cs`、`DD90Administrator.cs`、`DD99Excalibur.cs`及其直接使用的GenericAOEs、Cleave、BaitAwayCast、Knockback、ActorState、BossModule、SortHelpers和ShapeDistance实现。三场分别激活7、6、12个组件，共25个；仅接管对应Territory/OID的Boss机制，不迁深宫道中自动化，不将楼层Zone计作完成。1108是地下91～100层地图；源库没有独立DD100 Boss，EO100只是楼层Zone。

生产模型为`DungeonEurekaKaliya.cs`、`DungeonEurekaAdministrator.cs`和`DungeonEurekaExcalibur.cs`，保存原Cast、Actor引用/代际、锁定规则、准备/判定时间及独立结束条件。几何与当前导航转换集中在`DungeonOutput`，沿`DungeonEncounter → DungeonOutput → SafeZone`发布，不让副本模型依赖搜索节点或调度器内部状态。

80/99实时与MP回放均保留60个真实status槽，以SlotIndex重建，不用按Status ID聚合的字典代替；无效来源0xE0000000归零。同槽身份/来源改变先Lose再Gain，Param变化或到期时间相对上一采样增加严格超过1秒重新Gain，其余更新缓存。冷激活补播已有状态，包含Pet；hot时NativeType=0x202跳过状态回调，但仍保留查询状态。旧录像缺SlotIndex时仅兼容下标还原，不宣称恢复真实槽位。

冷激活按持久世界字典回溯，hot按ObjectIndex逐Actor处理；已有Actor按Cast→Status→Tether差分，新Actor初始Tether早于其Status。Actor事件期间使用上一帧已提交Party，全部Actor完成后才提交新Party，随后完整包消费新Party。切人不清全局历史，同ID新代不替换旧Actor引用。

## 80层：护罩、神经毒气与磁力

七组件为Barofield、AutoAttack、Resonance、NerveGasRingAndAutoCannons、NerveGas、CentralizedNerveGas和Magnetism。场地中心`(-600,-300)`、R20；3420 Gain切为源外部64边形减量化偏心R5内部64边形，Lose恢复圆场。源indexed Contains在相对`(0,20)`孤立顶点与MP包含边界的策略有已知差异，不能据此放宽内部判定。31427/31429建立R5预告及独立Active闩锁，判定FinishAt+0.7秒；Gain清预告但不额外置闩锁，Lose复位。

32930追加8–30环和长20/半宽2.5十字；31432追加长43/半宽2.5旋转十字并仅删首条预告十字，采用源实际Cross。对应真实Finish仅删同来源首项。32934/32935为R25.5总180度锥，32933总120度；Sides≥99按源算法排序。31421为R11总90度顺劈；Barofield仅抑制导航/前景，文字独立保留。31422为R12动态原目标锥，Finish不清，完整Effect按source删首，保留旧引用及原判定钟。锥心、归一点乘、量化文字与圆环严格排除内边界沿源判据。

磁力Tether38按旧Party四槽保存原source/target、原点、消费钟+10秒、推拉10码、忽略免疫和撞墙停止；Untether消费source保存的旧线。投影仅在ring存在时显示，导航不附加该门槛。公共EurekaMagnetismDistance保留源Distance的OR：落点超R19或线段命中中心R5.5即返回0，0仍属禁区；不得改为Contains的AND。其余取8–50环与drone前长43/半宽2.5矩形union，经有限采样适配。状态消失和预计时间经过不清槽模型，投影不代表物理执行。

## 90层：拦截序列与独立激光

六组件为AetheroChemicalLaserCombo、AetherLaserLine、AetherLaserLine2、AetherLaserCone、Laserstream和HomingLasers。方场中心`(-300,-300)`、半宽20。图标按Source Actor的OID选择R50总120度锥、前长40全宽5矩形或8–60环，锁定量化位置，环朝向为0；采用实现外径60而非注释40。390/391/392分别为消费钟+7/+10.5/+14秒，其他图标在合法来源上保留零延迟分支。仅恰第六项追加时按源算法排序。

31447的5–60环与31448的长60/半宽5十字进入同一Combo。显示取顺序前缀，遇Activation>首项+5秒即停；严格小于首项+1秒才Risky，后组只显示；存在后批时首批染Danger，已染颜色不反向复位。31451/31452/31453/31447/31448真实Finish删Combo全局首项，不要求来源匹配，普通Effect和预计时间不消费。

31452另存来源列表，仅Combo为空时显示前四、前二Risky，数量大于二时前二染Danger；结束删同source首项。32833矩形、32832锥及31461 R6圈独立按各自真实Finish清理。31456仅发布Raidwide文字与当前队伍伤害预测，不造全屏危险圈；召唤、连招起手和ModelChange枚举不扩成额外机制。

## 99层：属性魔剑、冷热分割与冰花

十二组件为ThermalDivideSides、三组AbyssalSlash、VacuumSlash、ThermalDivide、IceShootIceBloomCircle、Exflammeus、EmptySoulsCaliber、SolidSoulsCaliber、Steelstrike和IceBloomCross。圆场中心`(-600,-300)`、R19.5。普通范围保留2–7/7–12/17–22半环扇、R80总45度锥、前长40全宽8矩形及源圆/环参数；31346/31347共用来源FIFO，任意真实Spell Finish可删同source首项，不按预测超时清理。

31333首轮十条短刀，后续五角各三方向；32701/31353沿历史角度追加±20全刀，判定FinishAt+3.4秒。2552按循环归一朝向差≤1度登记当时首条刀原快照，0x219为火、其余为冰，恰五火五冰才各自按未归一角排序。3410/3411按回调当时旧Party槽保存mask，输出读取当前槽，不跟随实体换槽重算。下一raidwide与本人同属性时选择相反属性表首尾两刀，显示仍保留原短刀、原Activation/default和原引用；同rotation的其他危险刀不重复发布。

导航独立用两条前后20码、半宽2安全带union的补集，公共层裁成四种外侧半平面组合、最多四个有效多边形，圆场独立限制路线。这避免通用采样器因补集覆盖率过大拒绝导航，不能把原短刀显示范围当作导航范围，也不能改成软目标。世界平移后按GeometryHelper.DefaultEpsilon合并相邻/首尾重复点，只发布至少三点且绝对面积严格大于该epsilon的多边形；不把float舍入退化片交给WorldPolygonGeometry。缺所需属性刀快照时只保普通危险；非空单刀允许首尾同引用，不要求先凑齐五火五冰才能选择。

31334 Finish只清当前刀AOE；31335/31336/31337才同时清angles和属性刀，保留下一属性、mask及NumCasts。31331/31332 Effect仅清下一raidwide状态，31351 Effect独立清Thermal。31349/31350锁pattern、朝向及侧移4码，判定FinishAt+1.9秒；危险半圆直接导航，安全半圆只显示，其inverse补集经公共有限采样发布。颜色与风险判据分离，安全侧内保留中性提示，外侧提示风险。31346另追加44.998度/−0.003度两条冰花十字，判定FinishAt+5.2秒，仅前六显示/导航；31348完整Effect逐包删全局首，同sequence不去重，普通冰圈Finish不消费十字。

## 80/90/99层显示、导航与执行边界

场地、敌人标记、普通AOE、属性安全刀及Thermal两侧经共享预览提供雷达与世界绘制；磁力沿既有位移投影显示。普通Risky危险、磁力Distance、完整安全刀补集和Thermal安全侧补集分别发布当前SafeZone导航，不能以颜色、箭头或显示成功代替导航证据。输出按源组件顺序组织；最终叠色和像素仍待游戏内确认。

磁力和Thermal采样保留公共半码步长、覆盖率拒绝及组件选择边界；不能宣称连续几何、任意狭缝或所有补集均已精确覆盖，也不在采样拒绝时凭空补导航。双刀补集使用直接半平面裁剪；世界坐标float舍入可能在距源刀边≤0.0001码处产生分类差异，逐点对照将其单独作为边界样本，不声称bit级完全相同。内部差异不豁免，轴向零距离边界和完整后半刀另有明确断言。

三场没有新增凝视、PR面向、停手或禁攻请求，没有新增玩家传送、跳跃、道具、交互及原生强制位移执行。ARR/MP回放仅验证机制、显示、提示和导航，不发送GreenMove、PR面向/ACR或原生操作。机制关闭与owner停止撤销个人输出，切人和Seek依输入合同重建。真实游戏物理、PR/GreenMove执行及最终双视图像素仍待实机。

## 2026-11-25：80/90/99层集中验证证据

- `native-eureka809099-model-final.log`：87项EO10–99及公共读条边界模型全部通过。含同刀、反向、近重合、跨±π、两组退化反例、11组场内网格及首轮80度双刀逐度构造验证；`native-eureka809099-polygons-before.log`保留相邻重复顶点的真实失败，修后`native-eureka809099-polygons-rerun.log`单跑通过，再并入集中回归。
- `native-eureka809099-build-final.log`：最终生产编译0错误、4项既有Crucible警告，后续专项和全目录共用这一DLL。
- `native-eureka809099-replay-final.log`：32条实际DLL专项全部通过。覆盖完整status槽、原刀2552捕获/短刀快照及追加全刀、双安全带硬多边形/真实solver无least-danger fallback、磁力及Cross完整包、raw-only取消、逆向Seek和owner清理。磁力源Distance对照26,244点、Thermal源逆锥19,683点、双刀真实源SDIntersection与生产多边形19,109点；80源带孔场地26,569点仅允许上述孤立顶点差异，其他九场各6,724点源场地对照。
- `native-eureka809099-catalog-final.log`：同一生产DLL的全目录925条PASS、0条FAIL，退出码0。
- 历史`native-eureka-final-model.log`及`native-eureka-final-build.log`在本轮前段被同名日志覆盖；旧798模型和当时构建仅保留本规格历史结果，不能拿现有同名文件证明旧计数。10/20历史replay/catalog及其他独立日志仍保留，本批最终证据统一使用`native-eureka809099-*`前缀。

累计686个源类型离线已验、528个待迁、264个内置入口。正统十个Boss现均原生；所有楼层Zone和被禁止的道中自动化均未因本批计完成。未部署、未进行实机执行或最终像素验收。
