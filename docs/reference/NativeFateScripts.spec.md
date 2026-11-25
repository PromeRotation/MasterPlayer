# FATE 原生脚本

2026-11-24。全部六个源 FATE 模块通过 `DungeonScript → DungeonEncounter → DungeonOutput → SafeZone/Draw/Hints` 输出，不创建 BMR 会话或消费桥结果。接管只匹配 Territory + 主 OID，不整体压制区域。

| 源模块 | Fate ID → Territory（本机 sqpack 核对） | 主 OID / NameID | 原生入口 |
|---|---|---|---|
| Archaeotania | 1432 → 818，黑风海 | 0x27AD / 8234 | `Presets/Fates/Entries/Archaeotania.cs` |
| Daivadipa | 1763 → 957，萨维奈岛 | 0x356D / 10269 | `Presets/Fates/Entries/Daivadipa.cs` |
| Formidable | 1464 → 814，珂露西亚岛 | 0x294D / 8822 | `Presets/Fates/Entries/Formidable.cs` |
| Chi | 1855 → 960，天外天垓 | 0x34CB / 10400 | `Presets/Fates/Entries/Chi.cs` |
| MicaTheMagicalMu | 1922 → 1192，活着的记忆 | 0x43EB / 13049 | `Presets/Fates/Entries/MicaTheMagicalMu.cs` |
| Ttokrrone | 1871 → 1190，夏劳尼荒野 | 0x41DF / 12733 | `Presets/Fates/Entries/Ttokrrone.cs` |

来源为 `MechanicRuntime/Upstream/Modules/` 下对应 FATE 目录，以及 GenericAOEs、StackSpread、Knockback、ForcedMarch、GenericRotatingAOE、UnavoidableDamage、BossModule 和 ActorState。Fate ID 不是 Territory 或 CFC；这些野外区域 CFC 为 0。Archaeotania 的 Fate.Location=7945703 在黑风海 planevent.lgb 中有唯一对应实例。Chi、Mica、Ttokrrone 按本机客户端 `2026.11.03.0000.0000` 的 Fate.Location→planevent.lgb EventRange.InstanceId 双向核对，分别为 8865314、10553038、10420637；OID/NameID 配对来自源模块声明，BNpcBase/BNpcName 各行已另核验，不将两张表假称直接外键。

## 公共生命周期和输入

- 主怪可选、InCombat，且源队伍槽 0 距固定中心严格小于指定拉战半径，才激活。槽 0 缺少对象时使用当前视角位置；不读取实时本地对象补猜离线输入。个人机制、文字和投影始终使用 CurrentPlayer。
- 激活后单纯脱战、不可选、离开拉战圈或玩家死亡不结束。主怪死亡、销毁、HP0 结束；初见可拉战但已 HP0 同样记本代结束。原对象恢复 HP 不重启，真实缺席/销毁后新代可启动。
- 普通 AOE 按实际 CastInfo 锁位置、朝向和判定时刻；读取 NPC 总时长 +0.3 秒的实际完成时间，不从技能注释猜固定时长。预测时间经过不清，实际 Finish、取消或施法者销毁才清。
- raw 的 Action/Target/Location/Rotation/TotalTime 真实变化触发 Finish→Start。保留独立 raw 基线跨 Finish，区分首次 raw 替换、未接管视觉动作、合法后续变化及旧包/不变旧 raw；合法新记录提交后释放替换墓碑。
- 同代 packet 补正可以修正锁定几何，但不重复球阵 Start/Finish 的追加、反转或消费副作用。真正 raw 变化仍按源组件回调推进。
- 有意义的完整效果使用 `NativeActionEffectEvent`：主目标读取原生包 `AnimationTargetId`，不从 PR 按受击者展开的 `TargetId` 推断。对应展开事件不再重复推进。序号 0 按来源、动作、主目标和发生时间区分，重发不能清掉后来新点名。
- 活跃 BossModule 不执行 auto invincible 降级。基础目标候选、原生对象槽、FATE/高差、待结算 HP 和 NPC 队伍规则沿用公共原生选敌；`Enemy.Priority` setter 自身保护 Pointless=-1，Adds 的直接属性赋值同样不能绕过。
- 两个模块默认队伍/伤害名单保持旧 MP Feed 实际采集的普通 0–7 槽范围；源基类可接受 64 槽不代表旧桥已经采齐联盟和 NPC。

## Archaeotania：15 个激活组件

固定中心 (279,249)，半宽 29 的正方形；拉战 XZ 距离平方严格 `<900`。未额外要求 AllowedFateId=1432 才显示或登记 AOE。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| BlizzardBreath 16445 | R60、半角22.5° | 普通危险区、双视图和导航 |
| FlamesOfTheApocalypse 16448 | 环15–30 | 同上 |
| MindBlast 16447 | R21 | 同上 |
| WindSlash 16454 | R8，非视觉16446 | 同上 |
| CivilizationBuster1/2 17089/16441 | 向前62、全宽15 | 同上 |
| Touchdown 16442 | 实际实现 R30，非注释60 | 同上 |
| PillarImpact 16455 | R7 | 同上，死亡但仍有 CastInfo 的辅助体也不额外过滤 |
| PillarPierce 16456 | 向前65、全宽8 | 同上 |
| HeadlongRush 17042 | 实际实现向前13.2、全宽10，不再叠加 hitbox | 同上 |
| Megaflare | Source Actor 的图标62每次追加，独立发生时间+5秒；R6、min8/max∞ | 自己圈与其他圈颜色、人数/重叠提示；本人靠非点名同伴 R3，其他目标可进入任一 R6；单人不导航，无凭空 Shared 预测 |
| Megaflare 撤销 | 主怪无任意 CastInfo 时，保留有存活 0x2AA0 以该原目标为 TargetID 的点名；17483 按真实 MainTargetID 清全部匹配 | 死亡暂隐藏、复活恢复；销毁冻结旧 Actor，同 ID 重建不换绑；无超时清理 |
| Windwinder | 所有 !Dead 的 0x27B2，动态原位置 R5，不额外筛选可选/EventState | 原生硬障碍和双端预览，圈内 `GTFO from voidzone!`；不改成普通定时危险或重复外扩 |
| TidalWave 16452 | `ArchaeotaniaTidalWave` 保留原 Cast 身份、起止时钟、锁点/方向；Forward48，shape=null，不撞墙，不忽略免疫 | 按来源开始顺序从上次落点连续推进；免疫 expiry 必须严格晚于本次判定；仅位移线/角色投影与边界危险提示，无击退专用导航或自动免疫 |
| AncientAevis/Aether | 0x2A8E/0x2A8F，priority0、forbidDots=false | 源默认 Primary 加两类 Adds 的红色朝向标记；不扩禁攻/停手 |

枚举但未激活的 Gigaflare16451、四个实际击退辅助动作18121/18122/18123/16775不追加普通危险或第二套击退模型。TidalWave 的落点危险仅查场地边界，不混入旋风、分摊或普通 AOE。

## Daivadipa：10 个激活组件

固定中心 (-608,811)，半宽24.5 的正方形；拉战 XZ 距离平方严格 `<625`。默认只绘制 Primary，不为球和 helper 追加敌人标记。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| LeftwardTrisula/RightwardParasu 26508/26509 | R65、半角90°，不额外偏转朝向；全部当前项，不把排序阈值99当显示上限 | 普通双视图危险、文字和导航 |
| ErrantAkasa 26514 | R60、半角45° | 同上 |
| CosmicWeave 26513 | R18 | 同上 |
| YawningHells 26512 | R8 | 同上 |
| KarmicFlames 26516 | 实际实现 R20，非注释伤害范围50 | 同上 |
| Drumbeat 26510 | 从当前 CastInfo 读取目标/结束时间；目标为施法者本人时回退施法者当前 TargetID | 单体文字和承伤预测，不造危险圈、治疗或自动减伤 |
| InfernalRedemption 26517→26518 | 任意视觉 Start 覆盖单槽 activation=CastFinishAt+1秒；真实26518效果清；取消、Finish、预测到期不清 | Raidwide 提示/预测，无 R60 危险圈或重复预测队列 |
| LitPath | 蓝先26499：蓝0x3570 +2.2秒、红0x356F +4.4秒；红先26498交换延迟，随后反转整个现存列表 | 向前50、全宽10；只输出队首 activation+1秒内严格连续前缀，等于上界即停止，不扫描跳过中间项 |
| Burn | 同样视觉 Start；蓝0x3572/红0x3571，蓝先延迟2.2/6.2秒，红先交换并反转整个现存列表 | R10，只显示/导航前8项；隐藏项逐次以原时钟晋升，不另按颜色或判定波次筛选 |
| DivineCall | 1958/1960/1959/1961依次为前/左/后/右，pending 用真实 expiry 排序、每段2秒；1257/3629实际行进结束时间独立保存 | 当前角色位置/朝向，每段6.6码/秒连续投影；实际剩余行进先于 pending；不撞墙、不使用击退免疫；仅投影/提示，无专用导航、PR面向、停手或移动保持 |

球列表维护持久 Actor 枚举和缓存列表顺序，首次查询前快照重排不换顺序；后续新对象追加。每项 `DaivadipaOrbAoe` 保存原 Actor、锁定量化坐标、方向及判定时刻；不按球死亡/可选筛掉。Lit 方向判断读取原始 X：严格 `<-632` 为89.999°，其余为180°；量化只应用锁定位置，使用源 float 运算顺序。球移动或无读条销毁不撤已复制预测。

任意26501/26500真实 Finish 消费全局 Lit 队首，任意26507/26506真实 Finish 消费 Burn 队首；取消和读条中销毁也经过该真实 Finish，Effect 不推进。不以施法者颜色、身份或位置匹配队首。

DivineCall 的特殊落点危险只查**首个半圆 caster**和**完整 Lit 队列**（含未显示后波）；不查 Burn、其他普通圈或场外。半圆存在时保留 `Aim for safe spot!` 分支；仅 Lit 存在时保留 `Aim into AOEs!` 分支及其原风险语义。主怪27080/26520/27079/26519只提示相应 debuff，不从读条凭空生成 pending。状态事件/raw对账复用已有 `TreasurePlayerStatusState` 和 `HuntMarchState`；明确观察到新 Actor 代际后允许相同状态重建，迟到旧样本仍保留屏障。

## Formidable：12 个激活组件

来源 `Shadowbringers/FATE/Formidable.cs`、`SimpleBossModule`、`MiniArena`、`StayMove`。可选且 InCombat 即拉战，不要求固定中心距离；脱战重置，主怪 HP0/死亡/销毁结束本代。源本地玩家独立于 PR 队伍槽及 CurrentPlayer：回放取录制 LocalPlayerId，实时取本地对象 ID；显示中心每帧跟随，R30 圆。导航保留源 cachedCenter：初始原点，任一 XZ 轴差严格大于5才按 MathF.Round 更新，半宽30正方形。`FormidableArenaBoundary` 保持真实方框的 Contains/轮廓/遍历索引，ViewCenter 仅供雷达，隐藏导航方框边线。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| Spincrush17408 / FireShot17397 / ExplosionGrenade17411 | R15总120°锥、位置R7、实际R12；真实 CastInfo 与结束事件 | 普通危险、双端预览与导航，预测到期不清 |
| FiresOfMtGulg17395 / 18002 | Start 覆盖单槽，冻结 LocXZ、环10–50、实际 FinishAt；任意真实效果使下一击为处理钟+3.1，恰第7击清；取消/Finish不清 | 原时钟危险和导航；同代补正不能复活已消费七击 |
| BarrageFire17393 | 实际读条时钟、Raidwide + 7 repeats after | 文字与承伤诊断；不虚构七个危险圆 |
| DrillShot Icon62 | Source Actor独立追加，R6/min2/max∞、事件钟+5；17401不在Start建圈 | 真实Finish/取消/销毁按原cast.TargetId删首匹配；Native17401按真实MainTarget删首匹配，两路独立；分摊双端/导航 |
| ExplosionMissile OID2BD5 | 创建时保存原Actor，18003效果/销毁移除，不能由后续快照复活 | 强制橙色Actor及原位R6轮廓，仅显示；死亡/不可选仍画，场外Actor径向投到R30圆周并用轮廓 |
| DwarvenDischarge OID2A9C / 2BDC | 创建量化锁点，环9–60/+9.3秒与圆8/+8.1秒；17404/17405按source删首，销毁清 | 普通危险/双端/导航；不因死亡、移动、预计超时清除 |
| AutomatonEscort2A74 | Adds priority0，保留Pointless setter保护 | 源红色朝向标记；不扩禁攻、停手 |
| SteamDome17394 | `FormidableSteamDome`保留原cast身份、起止钟、锁点、AwayFromOrigin15、shape=null、不撞墙与免疫，按Start顺序连续位移 | 经公共`QuestKnockbackProjection`，显式R30显示圆/黄色，完整路径与场外圆周角色轮廓分离；仅显示及边界危险提示，无新增击退导航、自动免疫或操作 |
| DynamicSensoryJammer1269 | 原状态activation及队伍槽，末3秒进入Stay；真实Lose清，预计expiry不清，死亡提示后清该槽，缺席沿旧槽撤销 | 保留`Pyretic / Stay`及`Stop everything!`诊断；旧MP仅消费SpecialMode诊断，未扩大为PR全停手、禁读条或移动Hold |

## Chi

来源 `Endwalker/FATE/Chi.cs`。固定中心 (650,0)，半宽29.5方场；源slot0距离平方严格 `<900` 才拉战。创建事件维护独立世界账本和OID代际，先创建后raw、旧OID滞后、同ID重建、组件Reset后packet-only对象补播均不丢失或重复。已复制的创建AOE不因对象销毁撤销。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| AssaultCarapace 25954/25173/25953 | 前120/全宽32，或环16–60 | 普通双视图危险与导航 |
| ForeArms 25959/26523；RearGuns 25962/26524 | R45总180°，不另猜后向偏移 | 同上 |
| FreeFallBombs25968；ThermobaricExplosive25966 | R6、实际R25 | 同上；后者不按注释扩为R30 |
| Hellburner25972 | 动态原目标R5死刑诱导，真实读条结束清理，死亡/销毁保留基类引用语义 | 本人轮廓、同伴避让、伤害诊断，无自动坦克减伤 |
| MissileShower25969 | Raidwide×2 | 仅提示/预测，不造危险圈 |
| 六种2.0视觉25957/25955/25958/25956/25963/25960 | 两段FIFO，首段F、次段F+3.1；直线二段起点取起手source而非LocXZ，环/半圆用LocXZ；次段按对应视觉加π | 全段显示、仅首段Risky；恰两项时另有贴近当前Boss轴带的硬导航；首项黄色保留 |
| 八实际效果25955/25956/25957/25958/25960/25961/25963/25964 | 任意完整Native包删全局队首，取消/Finish不清 | 隐藏导航阶段晋升保留原时钟，按包而非受击者消费 |
| Bunkerbuster创建361E/3505 | 创建位置锁20×20方形、−.003°，创建钟+14.8/+16.9 | 显示到首项+2.5秒（含边界）的连续前缀，Risky严格 `<首项+1秒` |
| BouncingBomb创建364C/364D | 同几何，创建钟+10/+5.7 | 显示窗口+3.5秒，Risky同上；远波存在时当前项黄色保留 |
| Bunker25101/25976；Bomb27485/27486真实Finish | 各自列表全局FIFO删首，不要求来源一致；取消与读条中销毁同样是真实Finish | 不用Effect或预计到期提前清；隐藏项按原时间晋升 |

同代packet补正以实际旧`DungeonPendingCast`引用定位，各队列保留Stage；不同raw代次即使共享StartedAt/FinishAt也不误改旧队列，已消费项不复活。拉战前场地框和Primary红色朝向标记为仅显示。

## Mica the Magical Mu

来源 `Dawntrail/FATE/MicaTheMagicalMu.cs`。中心 (791,593)，半宽20.5×19.5方场，源slot0距离平方严格 `<420`。世界Actor保持首登记顺序，同ID不同OID新代追加到尾；销毁旧对象、旧raw滞后和抽卡时缓存晋升均保持正确代际。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| Shimmerstorm38703 | R6、真实Finish | 普通双视图危险/导航 |
| Shimmerstrike38705 | 动态原目标R5死刑诱导 | 本人轮廓、同伴避让、真实Finish与承伤诊断 |
| SparkOfImagination38707 | 实际F的Raidwide | 仅提示/预测 |
| Draw38666–38677 | 六安全索引取模；跳安全牌，其余五槽取对应43EE–43F3中首个现存Actor；缺Actor保留空槽，不后补 | 前20/宽14矩形，Actor位置Z−10后量化、−.003°；默认时钟，仅显示不导航 |
| Draw缓存/38678 CardTrick/39156 Fake | 已有牌时后续Draw入FIFO；CardTrick当前牌统一F+.6并Risky；任意Fake真实Finish清当前并用晋升当刻Actor生成下一组 | 五槽/安全牌/缓存独立，不由取消或Effect清理 |
| Flourish38697/38698及Ring38700/38701 | R10或环10–30；只比较交换前两项，不全局排序；真实对应Finish全局删首 | 只首项显示和导航，隐藏项保留F |
| DoubleMisdirect38694/38695 | R40总60°；count≥6用源严格右pivot排序 | 前三项；任意source真实Finish删该source首项，不要求同action |
| TwinkleToss38692 | 前42/宽5 | 前四项，匹配动作Finish按source删首 |
| Rolling38996–38999 | 前20/29/43/52、宽10，锁Start LocXZ/朝向；F+5+当时count×.6 | 全项显示且全Risky；列表count变化才更新首项黄色缓存，原黄色不回退 |
| Rolling效果38687/38688 | 完整Native包全局FIFO删首，Finish/取消/销毁不清 | 单包多目标只消费一次；同代补正不改变追加时Stage |

## Ttokrrone：8 个激活组件

来源 `Dawntrail/FATE/TheSerpentlordSeethes/`。场心 (53,−820)，使用运行库实际导出的48顶点polygon，而非R29.5理想圆；源slot0距离平方严格 `<900`。`TtokrroneAoe`、`TtokrroneRotation`保留cast引用、锁点、方向、判定时钟、段数和消费次数；所有投影沿公共输出适配，不引用求解器节点或执行循环。

| 组件/输入 | 保留的机制模型 | 显示、导航与执行 |
|---|---|---|
| Devour37327 / Touchdown37339 | R8普通AOE / F的Raidwide诊断 | 前者危险及导航，后者无危险圆 |
| Tempest视觉37331–37334 | R19圆/环14–60/左右半圆半环；首侧+90°、次侧−90°，LocXZ，F+1 | 全项显示且全Risky；六helper动作37328/37329/37335–37338任意真实Finish全清，Effect不清 |
| Sandspout37313–37316 | 前/后/右/左锥R60总90°+R13圈，F+`(double)6.1f` | 两项全Risky；仅39813/39814/39843/39816的完整效果全清 |
| Landswallow37578–37583 | 前68/宽27、F+.4，恰6项时源严格排序 | 全项显示导航，首项黄色永久保留；完整效果数值38642–38646含未命名38643全局FIFO |
| Sandspheres38647/39245/39246 | R6/R12，锁LocXZ和F；仅严格 `<首项F+4.5秒`连续前缀 | 不跳过中间隐藏项；匹配动作真实Finish按source删首，取消也推进 |
| DustcloakDustdevil | 独立R13圈，首次F+.9，helper Finish后改为helper LocXZ、消费钟+2.6d | 与锥独立显示/导航；最后一次真实消费才清 |
| DesertDustdevil37317/37321/37318/37322 | CW/CCW、前/后向，步进±90°；新序列7击，已有序列覆盖为4击；F+.9 | 下一/当前两锥按此顺序全Risky，当前剩余>1黄色；四helper39818/39817/39819/39820真实Finish推进，Effect不推进 |
| 旋转时钟/缓存 | 锥原点锁视觉Start，下一钟为消费钟+`(double)2.6f`；future=max(next,更新钟)+float间隔 | 只有序列count或消费次数变化才重建锥；同count重启及同代packet补正均保留已发布旧锥缓存，内部序列/圆仍更正，下一helper消费后切新几何 |

预战polygon描边、Primary朝向标记恢复源显示；场外标记采用最近线段投影，并保留源`abs(localX)<.1`及几乎场心的豁免。世界坐标float减中心后比较，不能将`53f+.1f−53f`假定为精确`.1f`。实际DLL已与源Contains及Clamp oracle对照。

## 双端绘制和能力边界

普通 AOE、序列及站位通过统一公共输出登记，雷达与世界绘制读取同一原生预览。正式边界仅拉战后参与导航；源拉战前场地框与 Primary 朝向标记补回为 Navigate=false。方场外标记按源逐轴 clamp，而非圆形径向投影。

源 MiniArena 的 ActorMarker、部分白色边框和投影角色曾被旧 MP 转换器丢弃；本次补回属于恢复源绘制，不能写成旧 MP 原本已完整显示。方向线与角色三角保留当前角色朝向及各段位移方向；显示与当前导航分别验收，不能把箭头计作导航或执行成功。

Formidable的雷达使用源R30圆裁剪；世界预览沿用旧MP原始几何并补显示圆，旧sink丢失的MiniArena圆裁剪尚未恢复，不能宣称世界圆裁剪已验收。显示方场与导航方场没有混用，默认Primary/Escort和强制导弹标记属于补回源显示。

六个模块没有凝视、护盾禁攻或主动原生动作机制。PR 面向、ACR 限制和 owner 合并继续参加公共集中回归；未借强制位移新增自动朝向、免疫、Hold、特殊路线或统一寻路框架。Formidable的Stay保持源模型及旧MP实际诊断边界。源伤害预测继续只是 MP 诊断，没有 PR 治疗消费者。

## 验证

生产/模型和实际 DLL 证据位于 `.tmp/halatali-verify/`：

- `native-fates-build-final.log`：exit0，生产0错误、4条既有 Crucible 警告。
- `native-fates-model-final.log`：exit0，587项集中模型 PASS、0失败，含19项 FATE 定向模型。
- `native-fates-catalog-final.log`：exit0，实际 DLL 全目录553条 PASS、0失败，内置入口218；含 Archaeotania 7组、Daivadipa 5组生产回放检查。
- `native-formidable-build-final.log`：exit0，生产0错误、4条既有警告；`native-formidable-model-batch.log`：600项集中模型 PASS、0失败，含8项Formidable模型。
- `native-formidable-focused-final.log`：5组Formidable实际DLL回放及2项公共输入检查通过；`native-formidable-catalog-final.log`：562条全目录 PASS、0失败，内置入口220。生产夹具覆盖显示圆/缓存方场/脱战重建、七击、创建锁点及导弹、普通AOE与非零方向Steam投影、独立Source分摊、Stay真实Lose、机制关闭、反向Seek及owner。
- `native-fate-three-build-final.log`：增量生产编译0错误/0警告；`native-fate-three-model-final.log`：623项集中模型PASS/0失败，含23项Chi/Mica/Ttokrrone定向模型。
- `native-fate-three-focused-final.log`中的六组Chi/Mica检查通过；Ttokrrone夹具反射签名及通用脚本筛选修正后，`native-ttokrrone-focused-rerun.log`五组专项与两项公共输入检查通过。场地4225点及七个Clamp样本与实际源runtime对照。
- `native-fate-three-catalog-final.log`：最终实际DLL全目录573条PASS/0失败，内置入口223；包含三项最新生产实现及全部回放夹具，新增后仅失败专项重跑，最后共用产物集中回归。

定向模型覆盖两场实际参数、独立判定/真实撤销、packet/raw及未知动作交接、原目标/同ID新代、零序号效果、严格免疫、全队列反转、前缀/第九项、源槽0拉战、首半圆/隐藏Lit危险集合、状态代际及 owner 生命周期。

实际 DLL 合成回放使用生产发现、事件适配、队伍 DTO、安全区和双端预览链路；覆盖机制关闭、死亡视角、反向 Seek、停止清理、Native 主目标与展开受击目标的反例，以及回放无 PR/GreenMove 原生执行。正式统计在对应全目录集中验证通过后才更新。

显示和当前提供的导航可离线验证；真实游戏最终像素、宿主选敌、物理位移和 PR 执行仍需实机验收。此次没有把离线回放写成实机验收。
