# 决战！佐戈永结桥 P1/P2/P3 原生脚本

2026-11-24。来源为 `MechanicRuntime/Upstream/Modules/Dawntrail/Quest/Role/BarThePassage/P1Trash.cs` 、`P2Trash.cs` 和 `P3Apyaahi.cs`，分别激活六、五、十一个组件。生产模型为 `Presets/Quests/JobBattles/DungeonBarThePassage.cs`；P1 截线复用 `DungeonAlphaAlligator.cs` 的原引用模型。入口为 `Presets/Quests/Entries/BarThePassageP2.cs`，同一入口负责三个阶段主对象，保留最初的 GUID `masterplayer.quest.bar-the-passage-p2`，不重复创建区域脚本。

## 身份和会话

本机简中 CFC1016 对应 Territory1246「决战！佐戈永结桥」，Territory 反查结果一致。接管 P1 友方 Loashkhana（465D）、P2 友方 Tentoawa（4660）和 P3 Apyaahi（466A）；独立任务区域模块继续保留桥。模块标题 NameID 不作为 Primary 的名字外键。

P1/P2 默认开战都只要求 Primary 可选且战斗中，不附加敌对、存活或 HP 条件。完成谓词替换默认死亡阶段：`!Primary.Targetable || CurrentCfcId != 1016`。死亡、HP0、脱战、Primary 暂时缺席、小怪全灭均不替代该谓词。缺席期间保存 Primary 最后快照；同 ID/OID 真实出生依源 `BossModuleManager.ActorAdded` 重绑 Primary。P1 完成后同帧可以选取 P2，旧组件输出先清理。

Director0005 对应源管理器卸载已加载模块：清旧输出并封锁未重新出生的 Primary；0011 只解除 wipe 门控，不单独清盾、计数或场地，也不凭原样旧 Actor 重建已卸载模块。真实缺席/出生后可以重新加载。机制开关、owner 停止及反向 Seek 沿公共生命周期撤销、重建。

三个阶段各自保留 Loaded/Pending 和启动历史。新会话首帧 Actor 在 Party 表更新前创建，Pending 倒序晋升；已有 Party[0] 时按逐 Actor 更新中的三维位置决定直接加载，读取本项目旧 Host 实际默认500码（源码配置未接入持久化/UI/该字段setter）。相同 OID 已加载对象阻塞后来的 Pending；晋升先于卸载，已启动模块不因脱战或越距卸载。

源会更新所有 Loaded 阶段。原生保留未选中阶段的读条、P1连线时钟、P2盾/次数/Map；按独立启动/完成状态更新，选择切换不重置另一阶段。输出仅发布选中的阶段。已启动 Primary 缺席仍保留模块快照；后台缺席模块也能在当前阶段完成后接入，不让同 OID Pending 替代其 Primary 身份。同 ID/OID 真正新生按源重绑。完成单阶段只清自身机制与读条，Owner/Wipe 沿公共层完整清理。

模型保留独立 `CurrentCfcId` 输入。生产适配复用 `LiveWorldSampler.ResolveCfc`，与旧 MP 桥一样从 Territory 表获取 CFC，实际 DLL 验证使用本机 sqpack 的真实表。现有录像没有独立动态 CFC 字段，不能声称已实录支持同 Territory 内单独改变 CFC；模型已验证该输入会结束阶段，输入适配仍维持既有能力边界。

## P1 机制

场地固定世界中心 `(0,-336.3)`，半宽34.6、半长43.7；不跟随友方 Primary。

| 组件 | 模型和时钟 | 输出与结束 |
|---|---|---|
| NopalFan | 40896 Start，锁真实 LocXZ/rotation/FinishAt，前长15、全宽4 | 普通危险区双视图及导航；真实 Finish 按来源清理 |
| PulverizingPound | 40898，R5 | 同上 |
| BodySlamPredatorySwoop | 40901/41787，共享 R6 组，全部显示和导航 | Effect 不清；实际 CastInfo 交接先结束旧记录，再处理新动作，未接管动作也不跳过旧 Finish |
| ZillionNeedles | 40900，R4 | 普通危险区双视图及导航 |
| FlockFrenzy | 41960，当前读条时钟 | 仅 Raidwide 文字及存活队伍/联盟/NPC 伤害诊断，无 R50 危险圆、无新增治疗执行 |
| ToxicSpit | 84/17 连线，保存 Player/Enemy 出生引用及直接 Set/Clear 槽位；41125 Start 将共享时钟设为 F+1.1 | 原引用线段双视图；Finish/40902 Effect 不撤线、不清共享钟；Untether 按源重新解析并删除首个引用 pair |

ToxicSpit Active 后，在惰性 4664 列表中选首个逐轴距 `(13,-302)` 不超过1的来源，不筛死亡或可选性。导航目标每次取惰性 465D 列表的第一项，与 PrimaryId、当前连线目标或最近目标分开。两列表保持幸存者顺序，新代追加；首目标销毁后改用下一项。父类画线先建立 excluded465D 列表的情况也保留。

矩形从 `target.Position + (target.HitboxRadius+.1)*target.DirectionTo(source)` 到 `source.Position`，全宽1。位置、方向、hitbox 保留原精度，当前玩家已经接线仍参与导航。源输出 SDInvertedRect；旧 MP 桥 `MechanicHintsConvert.TryConvertComplement` / `MechanicNavDiffer` 将其转换为立即参与解算、优先级1、Nearest 的内侧安全目标。原生通过 `DungeonAmbientAoe → DungeonOutput → SafeZone` 保留该消费语义，不发布整场补集硬危险、不生成矩形预览。源原始共享判定钟仍保存在模型。

若所需目标缺失，源直接索引列表而没有有限导航结果；原生保留线/提示并不凭空替换目标。端点重合沿共用截线合同省略无有限定义的导航，原引用、时钟和显示仍保留。目标销毁后旧线冻结，同 ID 新对象不冒充原目标；直接清槽不改为引用计数。

DrawEnemies 精确包含 `4664/4666/46B0/4662/46AC/473A/4661/4665/46AF/4663/46AD`，不画465D、46BC或233C；46BC 虽不画敌人标记，其41787仍产生普通 AOE。死亡、不可选标记及场外投影复用公共输出。41958/42026/40902只按已接管阶段压制通用躲避，不补不存在的危险区。

## P2 机制

初始场地世界中心 `(0,28.5)`，半宽34.6、半长23.5。激活后仅 Map `index3/state0x00020001` 替换成中心 `(0,57)`、半宽34.6、半长24；不取并集、不按位置推断、不重播激活前 Map，不存在自动回切。

| 组件 | 模型和时钟 | 输出与结束 |
|---|---|---|
| SavageSwipe | 40904 Start，R9/总120° | 锁真实 LocXZ/rotation/FinishAt，双视图普通危险区和导航；真实 Finish 按 source 清 |
| OneOneOneTonzeSwing | 40905，R10 | 同上 |
| OneOneTonzeSwipe | 40906，R10/总120° | 同上 |
| ArenaChange | 激活后的精确 Map 输入 | 替换场地，无自行恢复 |
| IceAegis | 40866 真实完整 Effect 包锁当前 caster 原位置、rotation+π、default activation；保存唯一 R5/总120°安全锥 | 绿色双视图及优先级1持久安全目标；40907/40908累计恰第五个真实 Effect 才清 |

40866覆盖旧盾但不重置累计次数；无盾时 Slash 也累计。第六击不清后建的盾，不能用 `>=5` 或模5。Start、Finish、取消不算 Slash 效果；40907/40908源虽枚举长80宽70矩形，却没有激活该普通 AOE 组件，原生来源级接管防止通用躲避额外生成整场矩形。

40866/40907/40908消费 `NativeActionEffectEvent`，先扫描当前 caster，再锁姿态；PR按目标展开事件不重复消费，零目标包照常处理。非零序号按 source/action/sequence 去重；零序号额外保留原事件 UTC timestamp，延迟重复派发不变成新一击，收据到整次组件停止才清。没有独立 Native 完整包的旧录像不声明支持这三个效果；当前 DTO 也不能区分 source/action/timestamp 完全相同的两份零序号真实包。

文字保留 `Go behind shield!`；源风险判据，包括锥顶零方向不算入圈，保留在模型的 `BarP2ShieldUnsafe`。现有脚本文字出口是字符串，不消费源 TextHints 的风险颜色，不把模型谓词测试冒充风险颜色已呈现。普通 AOE 另保留 GTFO。

## P3 Apyaahi

生产模型为 `DungeonBarApyaahi.cs`，与 P1/P2 共用 Loaded/Pending 及 owner。拉战只取惰性 `Enemies(4670)[0].InCombat`，不附加死亡/可选性或 Primary 战斗条件。完成独立取 Primary 死亡、HP0 或销毁，不套 P1/P2 的 CFC 与可选性谓词。仅画 Primary、4670 Mycotender、4672 Arms；未激活的40925拉近不接管。

| 组件/事件 | 模型与时钟 | 显示、导航及撤销 |
|---|---|---|
| ArenaChange/Proximity | 40914 R12；初始中心(0,379.27)、R24.5正20边形；40914起手建20–25环F+.5，40920覆盖为F+2 | Map6/00020001切中心(0,379.241)、R20正64边形并清环；00008004还原初场但不清环；取消/Finish不清环 |
| MysteriousLight/Decay | 40922/40916只保留当前NPC读条和Raidwide预测，不造矩形 | 真实Finish清理；float elapsed逐帧增量并封顶Total，仍读条时预测保持now+约.3；首raw用真实StartedAt补正elapsed，同刻不重复推进 |
| Burst | 40917锁caster原位置、double SinCos后转float的5码方向、首F、间隔1.2、九击 | 40917/40918真实Finish按旧spell.Loc逐轴≤1首匹配，Next=FinishLoc+step、下一钟处理时钟+1.2；销毁/取消也推进、Effect不推进；future先/head后且仅线版本变化重建四圈缓存 |
| PathogenicPower | 40910锁Loc/F，插入有序队列，初始Risky=false | 全部预览；严格早于首F+1才置Risky，颜色按早于末F−1更新，其他保留旧值；任意Finish删全局首项，非按caster；Effect与超时不删 |
| Fungus/RottenHeart/StrongStart | 40912 R6、40920环5–40、40909径向13不截墙 | KB模型保留原Loc/rotation/F，按插入序从前落点继续，零方向跳过、免疫expiry严格>F；每个真实落点越界检查；首caster独立R6优先级1即时安全Goal，免疫不撤Goal |
| TerminalBloom | 40927首caster当前remaining的f1倒计时 | 无预测伤害或AOE；Total+.3−elapsed的动态float钟，取消/Finish才撤 |
| SpiritMustard/CorruptBiogas | 40930独立R6/min2/max∞分摊，40932独立R6分散、导航R7 | 两组件名单不交叉过滤；旧目标引用动态位置、销毁冻结、同ID新生不重绑、Finish按target首项；不产生伤害预测，本人分摊仍可加入另一分摊 |

Power默认危险颜色按源黄色 `0xff00ffff`。Burst首packet后首raw可补正原点且不推进旧线；SourcePosition-only权威包也可补正，后续纯移动raw及无SourcePosition重复包保留锁点。击退预览复用 `QuestKnockbackProjection`，普通输出和站位统一经 `DungeonOutput → SafeZone`；本来源未提供特殊移动动作执行，不扩大为自动击退免疫动作或40925拉近。

分摊本人buddy的原始队友坐标/R3与SDIntersection保存在 `DungeonStackBuddyTemplate`。公共 `DungeonOutput.AdaptAmbient` 和旧桥共用从旧桥提取的 `GeometryGridSampler`：.5格、最大60半径、超过理论圆面积65%拒绝、四邻接取前四连通域/至少两格且只保最长轮廓。两人常见布局沿旧桥拒绝，稀有多buddy布局仍保留消费；不把原模型全部禁用或放大成强制R3站位。

`tests/QuestBarApyaahiRegression.cs`覆盖14个模型场景，含首raw时间偏移、无KB/全免疫不造假提示、逐段落点、不可选首Mycotender、后台手交及真实安全目标求解。`NativeDungeonReplayBarApyaahi.cs`经生产DLL验证Power FIFO/sticky、九击缓存/销毁后helper续接、两种场地7,442点源oracle、独立party旧引用、float钟饱和、免疫、Map、默认完成、反向Seek和owner撤销。最终本批206项集中模型通过、生产0错误/4项既有Crucible警告、实际DLL全目录633条PASS/0失败；证据 `.tmp/halatali-verify/native-bar-p3-model-final.log`、`native-bar-p3-build-final.log`、`native-bar-p3-focused-second.log`、`native-bar-p3-catalog-final.log`。

## 公共适配与验证边界

普通 AOE 都按真实结束保留，不以 Effect 或预计时钟超时清理。真实 raw 改变走 Finish→Start；首次 raw 对 packet 的位置、方向、目标和时长补正使用 raw 自己的时钟。取消后不变 raw 不复活；未知 raw 不提前清取消屏障；有效新 raw 提交后才撤记录。NPC槽在截线输入之前分配，避免首次连线把合法友方当作队伍外对象。

`tests/QuestBarPassageRegression.cs`覆盖独立模型、效果包收据、生命周期和两个真实 `SafeZoneSolver` 站位；`tests/ScriptCompilation/NativeDungeonReplayBarPassage.cs`经实际 ScriptManager、ReplayController、生产输出和本机游戏表验证。最终249项集中模型、生产0错误/4项既有警告、实际DLL全目录628条PASS/0失败；日志为 `.tmp/halatali-verify/native-bar-four-five-*`，源矩形标量与P2两场地23,664点对照另见定向日志。共用截线的Alpha补验仍包含6,643点源凹场和旧Actor引用/Seek/owner回归。

显示与当前实际提供的普通导航按上述层次验证；本任务没有新的护盾禁攻、PR面向、特殊位移或自动动作意图，不添加这些控制。回放不发送移动、朝向或动作。真实 GreenMove 路线、宿主控制权及最终游戏世界像素仍需实机验收；未启动统一寻路重构。
