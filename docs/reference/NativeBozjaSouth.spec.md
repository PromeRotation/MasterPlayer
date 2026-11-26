# 南方博兹雅战线原生机制

## 范围与来源

本批从 `MechanicRuntime/Upstream/Modules/Shadowbringers/Foray/CriticalEngagement/` 迁入 MP 原生状态模型。生产入口 `Presets/Foray/Entries/BozjaSouth.cs`，GUID `masterplayer.foray.bozja.south`；模型及输出在 `Presets/Foray/Bozja/`。不创建 BossModule 或 MechanicSession，不在生产读取 BMR 输出。

实际客户端表双向核对 CFC735 ↔ Territory920。CriticalEngagement 的 GroupID 是 CFC，NameID 指向 DynamicEvent，不能当 Territory 或 BNpcName 使用。当前十个 CE、七十三个实际激活组件已离线验收登记：CE14守夜、CE21地生人、CE12恐惧妖犬、CE13皮里福尔、CE11阿克巴巴、CE24爱国者、CE31达因斯莱瓦、CE34耶鲁、CE33奇尔维尼克、CE23火焰百夫队。各批来源与合同见下文，其他 CE 保留桥。OID 依据源枚举，不宣称已独立核证 BNpcBase 映射。

## CE14 八个实际激活组件

| 组件 | 实际消费 | 几何与语义 |
|---|---|---|
| LightLeapExplosiveFlare | 20146、20152 | R10，跨动作共享队列，任意 Finish 按 caster ID 删除首项 |
| ChemicalMissile | 20148 | R12；超过两项时前两项 Danger 红色，所有项仍 Risky |
| TailMissile | 20150 | R30 普通 watched Finish |
| Shockwave | 20151 | R16 普通 watched Finish |
| CripplingBlow | 20153 | 当前原 caster 的动态单体伤害预测，无伪造 AOE |
| PlasmaField | 20154 | 当前原 caster 的动态全队伤害预测，无伪造 AOE |
| Towers | 21266 | R6，最少/最多一人，开放世界原 Actor 惰性缓存 |
| MagitekRay | 21268 | 前长50、后长0、全宽4 |

不为 visual20147/20149 或失败塔21267 新建机制。九个实际消费动作的客户端 CastTimeExtra 均为0；NPCRemainingTime 的 `.3f` 是独立运算，不因此取消。

## 输入、队列与生命周期

场地为固定世界中心 `(451,830)`、R29.5 的32边形，首点朝 +Z，浮点角增量及双精度 SinCos 后转 float 与源 Polygon 一致。CheckPull 需要主怪可选中且战斗中，以及源玩家 X/Z 到固定中心不超过30码。它不消费当前临界遭遇报名或动态场地数据。

原对象引用保留到实际销毁，AOE锁读条落点、方向及激活钟；销毁先 Finish 再移除。相同 EntityID 的 OID、非零 GameObjectId 或已知 ObjectIndex 变化创建新代。这是 MP 对输入代际的明确适配，比现有 BMR Feed 连续 EntityID 差分更强，不宣称 Feed 本身会因这些字段重建。

原始世界 Cast transition 负责 Start/Finish。expanded CastStart、包 cancel、ActionEffect、经过预告时刻均不自动清队列；没有统一 TTL。普通 AOE Finish 要求 watched action，再按 caster ID 删除首项；共享分组对任何 action 的 Finish 按 caster ID 删除首项。单体/全屏组件保存原 caster 引用，按原引用删除首项。

分组默认插入顺序，到99项开始使用源 RefSort 的右端 pivot、严格小于分区。等钟项会重排，稳定 OrderBy 不等价；98/99/100和全等钟均有真实源对照。

NPC elapsed 以 float 递增并封顶 TotalTime，激活钟一次计算 `Now.AddSeconds((TotalTime + .3f) - elapsed)`。重复伤害 caster 不去重；单体 raw Target=self 时回退该原 caster 的当前 Target，找不到槽位仍保留零 mask。全队只包含当前存活 Party 成员，伤害仅作诊断，不新增治疗策略。

旧回放 `IsSpell=null` 按原 ReplayFeedAdapter 接受，明确 false 排除；缺 raw Rotation 使用录像自身 Actor.Rotation，绝不查实时对象。缺 TotalTime 按 `max(.05f, float(FinishAt-StartedAt)-.3f)` 归一。短 `.301s`、不同浮点时长、EventSeen 冷激活已与真实私有 ConvertActor 对照。

每帧 Actor 回调完成后才提交 Party/source。旧 source 同帧重建能影响后续主怪的即时加载；pending 逆序，加载/卸载用三维500码。事件扫描不推进 manager 生命周期，Framework 更新才晋升/退休；source 缺席时冻结管理器。主怪同ID销毁后在 Update 前新建可重绑旧 loaded module；其他队列仍引用各自旧 Actor。

默认 TrivialPhase 以 HP0、Dead 或 Destroyed 完成，不以脱战/超时清空。先更新 phase，再先检查超距卸载回 pending，最后检查之前 active/Destroyed 退休。初次 pull 当次即 HP0/Dead 可完成但仍 loaded，不能伪造此前 active 后退休，也不能 HP 恢复后重新 pull。Director0005 卸载 loaded、保留 pending 并设屏障；0011 仅解除屏障，不复活已经卸载的旧 Primary。

## 开放世界塔

每塔第一次输出/计数才缓存当时世界所有 OID0 原 Actor；不筛职业、死亡、队伍、Kind、可选性或距离。后建对象不加入旧塔；销毁的缓存原引用继续用保留位置计数，同ID新代不替换；各塔缓存相互独立。

背景危险填充与前景轮廓分开。提示先检查任何“本人不允许且在内”的塔，再检查本人允许且在内的第一塔；不能把多个塔折成统一绿圈。源 `inTower` 的正确人数判断不要求 viewer allowed。

有可加入塔时提交所有倒置 R6 圈的交集，并抑制同组件已经收集的普通危险；无 join 且不在正确塔中时才提交普通危险。所有塔导航与整组件一条伤害预测使用首塔激活钟。伤害 mask 的 allowed 判断针对 viewer，非逐预测成员；存活 Party 成员在对应塔中才入 mask，零 mask 不删除。

倒置圈交集（包含单圈情况）继续经过现有 `GeometryGridSampler`。采样拒绝的补集不升级成硬目标；真实 ConvertForbiddenZone 的返回数量及每点 SDF 均作对照。这一消费边界可能使空塔只显示安全轮廓而没有加入导航，不代表源 SDF 缺失。

## 公共输出与隔离

ScriptManager 只接管实际 native source 的已实现动作。MechanicHost 两处区域级压制跳过本入口，按 Territory920 + 精确主 OID 禁桥，不吞同区其他 CE。

导航以稳定区域句柄输出 exact snapshot，预览独立；原始危险只在执行解算层应用一次用户余量。背景按组件激活顺序提交，之后按同序提交前景，沿用桥的场地裁剪。二维几何输出高度采用当前观看角色 Y。场地只清理自己仍持有的引用，不删除其他 owner 的边界。

普通 Dictionary 的空位复用不能作为绘制顺序合同。脚本通过内部 `Draw.SetPreviewOrder` 显式提交自有预览序列；注册表只排列该 owner 已占据的输出位置，其他消费者保留既有枚举与分层。覆盖已有 ID 时不重新登记导航/预览生命周期。包含先 Ray 后 Circle、其他 owner 低位空槽、99项同键排序及 Stop/Seek 重启的真实注册表验收。

事件完整采样不推进 `nextScan`，Framework 更新才推进0.1秒截止钟，避免高频事件持续推迟生命周期更新。跨 CE 管理器保存全局 Created 顺序及重新入 Pending 的新序号；超距重新排队不能沿用 Actor 出生顺序。按 Loaded 顺序处理旧模块的更新，最终按全局 Pending 逆序选择；当前两个独立 CE 不跨模块消费 Update，逐模型晋升/更新交错与源全局两阶段结构的差异未产生已知输出偏差，不据此宣称任意后续模块都等价。

CE34新增通过现有Hints门面提交凝视联合面向与选敌，其他CE保持各自原有消费；没有新增ACR或原生动作执行。ARR/MP回放只规划与显示，自动移动请求为false，面向/目标实际请求为空；移动总开关关闭撤销owner请求。停止、切区、机制关闭、逆向Seek清理预览、导航、面向、目标和边界。真实像素与PR/GreenMove执行仍待游戏内验收。

## CE21 六个实际激活组件

场地中心 `(644,228)`、R29.5 的32边形，拉起圆R30，基础生命周期与守夜相同。七个实际消费动作已用本机客户端表核对 extra 全零。

| 组件 | 实际消费 | 保留语义 |
|---|---|---|
| GraspingRancor | 20132；手 OID `0x2DBC` | R12，SafeFromAOE、Risky=false；安全显示不注册目标区 |
| HatefulMiasma | 20125 | R6、最少2人、无限容量；原目标引用、按 Finish TargetID 删首 |
| PoisonedWordsStepsOfDestruction | 20126、21019 | R6 跨动作共享队列；99项源排序，任意 Finish 按 caster ID 删首 |
| CoffinNails | 20129 | R60、半角45°；超过两项时前两项 Danger，始终仅前两项 Risky；大于两项追加首项 Origin R4 目标区 |
| Stab | 20130 | 单体原 caster 动态预测钟，无伪造几何 |
| GripOfPoison | 20131 | 全队原 caster 动态预测钟，无伪造几何 |

不监听20118、20127、20128、21018等 visual。手列表在组件激活时首次查询全部同 OID 原 Actor，之后 Created 追加、Destroyed 按引用移除；Dead/不可选中不移除。同 ID 重建加入尾部，不占原位置。首个 tether 指向当前角色的手决定提示和线；Frozen ID2 为 Safe 线，其他为 Danger。提示仅在存在20132 caster时出现，线无 caster仍显示。提示按首 caster 锁点判定手应冻结与已冻结的异或，不新增自动面向。

源还输出 `MechanicDrawActorMarker`，现有 `MechanicDrawConvert` 没有消费该命令；本批保留线及完整手模型，不扩大为新的 marker 或导航功能。

分摊目标 Dead 时隐藏而不删；复活恢复，Destroyed 保留原位置与引用，同 ID 新代不替代。Start 时目标缺失不晚领；两个 caster 指向同目标时任一 Finish 按 TargetID 删除首条，不按 finishing caster。无 `RaidwideOnResolve` 预测。

分摊自己的 buddy R3 倒置圈交集继续经过旧 `GeometryGridSampler`；其他目标 R6 以首条激活钟提交 `SDOutsideOfUnion`。原 raw SDF 对多个成员采用源的“恰好一个非负成员”符号计算，并有逐点对照；原桥 `TryConvertComplement` 实际将此类型转换为任一成员的目标区 union，本批沿用该实际消费边界。两种语义明确分开，不以目标区 union 冒充原 SDF。目标区域 priority=1，与旧桥相同。Coffin 的 R4 目标同样用真实注册表，位置必须取首 Origin，不能换场地中心。

## CE12 八个实际激活组件

场地中心 `(154,785)`，32边形R24.5、拉起圆R25。场地及导航采样半径来自本CE定义，不沿用前两CE的29.5/30。十三个消费动作的本机 `ActionID.CastTimeExtra()` 均零。

| 组件 | 实际消费 | 保留语义 |
|---|---|---|
| Hellclaw | 20534 | 原 caster 动态单体预测，不新增几何 |
| TailBlow | 20535 | R19、半角45°普通 watched Finish |
| LavaSpit | 20538 | R5 |
| ScorchingLash | 20553 | 前长50、全宽10 |
| Hellpounce | 20539、20540 | 单项覆盖，原始坐标差算长度与方向，仅Origin量化 |
| DragonsLionsBreath | 20544、20545、20542 | 共用R60半角30°，包含Lions，按源实现而非枚举45°注释；跨动作任意Finish按caster ID删首 |
| VoidTornado | 20546 | `CastHint` 全局“Set hp to 1”，原caster引用列表重复保留，watched Finish按引用删首 |
| VoidQuake | 20549、20550、20551 | R10 / 10–20 / 20–30，未量化Loc、固定朝向0，按Start插入；仅队首显示、提示、导航 |

Hellpounce 任一相关Start完整替换单项。任何caster的20539 Finish均从该Finish的Loc指向关于场地中心的镜像点，判定钟为回调世界钟+3.7d，即使之前无Start也生成；重复Finish重算该钟。任何20540 Finish清空，不匹配原来源或动作。第二段真实Start覆盖预测，不增加注释提及但源组件未输出的击退。

零长度Hellpounce仍保存源矩形及0方向，不夹为正长度。源raw绘图命令存在，但实际绘图转换拒绝长度<=0；导航直转拒绝后，按本CE场地中心和R24.5走既有有限网格采样，不能一律丢弃。源退化线、raw SDF、实际采样转换及显示边界分层验证。

VoidQuake不排序、不去重；任何三动作的Finish弹全局首项，不匹配caster、动作、位置或钟，visual20548不弹。Effect、超时均不晋升。真实世界cast替换先Finish旧cast再Start新cast，可能弹掉别的来源的全局队首，与直接重复Start追加的夹具分开。

冷激活按世界原Actor枚举顺序，Hellpounce最后相关cast覆盖，VoidQuake首个相关cast先输出，不按判定钟排序。空cast历史空档不补造返程或圆环。CastHint只提示，不将20547、20526等未监听动作补成伤害或几何。

## CE13 七个实际激活组件

T920/CFC735，DynamicEvent1「腐烂蔬菜——皮里福尔」，主OID `0x2E2F`；固定中心 `(-90,700)`、R29.5的32边形、pull圆R30。七个实际消费动作extra均经本机客户端核零；Pheromones不消费20512/14/15/16。

| 组件 | 实际消费 | 保留语义 |
|---|---|---|
| Pheromones | OID `0x2E31` | 全同OID原Actor缓存；当前位置/朝向L3、R4移动胶囊；Dead、不可选中、EventState7保留，真实Destroyed移除 |
| DeadLeaves | 20518、20519；SID2301、2302 | R30半角45，双列表；观看槽位Tender优先，否则Jealous，否则无AOE；任一相关Finish清两列表 |
| AnaphylacticShock | 20520 | 前长30、后长0、全宽2 |
| SplashBomb | 20522 | R6 |
| SplashGrenade | 20524 | R6、最少8人；原目标引用，Finish按TargetID删首 |
| PlayfulBreeze | 20525 | 监听Boss visual本身的动态全队伤害诊断，不监听20526 |
| Budbutt | 20527 | 原caster动态单体伤害诊断 |

DeadLeaves按60个真实槽0→59差分。同SID不同槽不去重、Lose任一槽直接清历史bit，不按剩余数量重算。SID/来源变更Lose→Gain；Param变化或到期延长严格大于1秒仅Gain；缩短/不超过1秒只更新缓存、不派发。热事件用回调当时旧Party，Actor回调全部完成才提交新Party；Region第二遍不能重放。冷激活逐Actor补已有Cast及全部状态，含Player/Pet；热状态仅排除创建时为Pet的类型。FindSlot=-1不写bit63，Party-only重排不搬移历史bit，时间经过不补Lose。销毁先Finish，再0→59 Lose，再Removed。

旧录像状态到期钟按真实ReplayFeedAdapter将剩余秒数转float并Clamp至0–100000；残留的过去到期记录不能用绝对值反射到未来，制造刷新Gain。两槽同SID删一槽后历史bit保持清除，另一过期残留不能恢复。`ZeroRemainingTime`和MaxValue分别按既有输入合同恢复Now和Now+100000秒。

Pheromones显示和个人提示使用raw解析L3胶囊，默认AOE钟；实际显示和导航沿用旧桥每半圆24段的折线。每次输出每source追加L1.5/3/6/9、R4胶囊，钟为Now+1.1d/+3d/+10d/MaxValue，另有独立无钟R4圆硬障碍。导航raw列表按source四条；发布层按实际AIHints.Normalize的RefSort右pivot排序禁区，障碍保持独立原顺序。后续移动/旋转重新计算，不锁创建钟、不将MaxValue变即时危险。障碍保留显示，不标NavigationOnly；四个预测只导航，L3显示不另造即时危险。

敌人列表在首次输出查询时惰性缓存，之后Created追加、Destroyed按原引用移除；pull与首次查询之间的世界槽复用按首次查询时真实枚举顺序处理。个人Check采用源的平方距离，导航Distance按世界坐标投影后开方，不能用开方后的Distance<=0替代Check。禁区发布顺序与句柄身份是两个合同：原生按RefSort提交，但使用原Actor/BirthOrder稳定键，不宣称与旧桥Normalize后索引键的句柄迁移完全相同。

MaxValue的旧桥float延迟往返可能因当前钟和舍入方向溢出，并非任何当前钟必然溢出；本夹具已通过真实MapActivation及Registry.AddDelayedDanger复现。原生按绝对DateTime.MaxValue注册精确快照成功，是明确的消费边界适配，不复制旧桥溢出，也不转换为即时危险。

SplashGrenade最少8人仅影响提示与目标自身满足判断。非target恰在一个分摊内即提示已满足，即使总人数不足8；圈外有未满足分摊则警告；自己target人数不足8或与其他target重叠则危险。导航仍以至少两个存活成员为前提，buddy R3/加入R6及源补集转换边界沿用CE21，不把导航门槛改成8，不增加伤害预测。

最终模型`native-bozja-ce13-20261125-model-06.log`为41 PASS/0FAIL（34 Bozja、7 script controls）、exit0。model05新增分摊转换断言时漏走宿主的ComplementSkipped→TryConvertComplement目标区入口，修正夹具后保留实际转换数量、类型与逐点几何断言。生产build02为0错4既有警告、exit0（21.75秒），SHA256 `537f9232d1764be99f44595464b0158ba924eaf2c2730e17901c0e780bfac6a3`；同DLL最终replay04为42 PASS/0FAIL、exit0。无筛选catalog01为1459条以`PASS `开头的场景行/0FAIL、293入口逐文件独立编译，exit0。

本轮核对发现此前全目录总数按PASS子串计数，包含9处正文标记；CE12 catalog01实际为1452场景行，CE21 catalog02实际为1441，分别纠正旧记1461/1450。场景和断言未删减；CE13比CE12新增7条实际DLL专项。后续统一按行首`PASS `计数。

model01误写现有API名导致编译失败；model02在移动胶囊raw距离精确比较失败，修正为源的世界坐标投影运算顺序，未放宽容差。replay01的全量初帧重复100/101对象被真实Reader拒绝；replay02的机制关闭断言缺少驱动下一更新；replay03误读快照不存在的Obstacles成员，按真实HardObstacles修正。全部失败日志保留。

## CE11 十一个实际激活组件

源为`CE11ShadowOfDeathHand.cs`，CFC735/T920，DynamicEvent5「黑死鸟——阿克巴巴」，主OID`0x2DA7`。本机简体中文客户端逐行核对源AID31/31及extra全零；BNpcName9400仅按源注释核名，不推定独立OID映射。初始中心(825,640)、32边形R29.5、pull圆R30。

| 组件 | 实际消费 | 保留语义 |
|---|---|---|
| ArenaChange | 20182；Deathwall OID1EB02E Created | 单项覆盖donut20–30，当前中心/0朝向，源NPC钟+.8d；Created改R25圆并量化当前中心、清donut；Finish/超时/墙销毁不恢复 |
| BestialLoyalty | 20163 | CastHint“Summon crows”，原caster列表 |
| RunWild | 20166 | CastInterruptHint；单caster“Interrupt Name!”，多caster“Interrupt!”；名称仅Start/Finish刷新，意图不执行 |
| HardBeak | 20171 | 动态单体伤害诊断，不包含20190 |
| PiercingBarrage | 20172、20191 | 跨动作共享矩形，前40/后0/全宽8；任意Finish按caster ID删首 |
| Helldive | 20173 | R6最少8人，与CE13同源分摊家族 |
| BroadsideBarrage | 20174 | 前40/后0/全宽40；不补造注释提到的失败击退 |
| BlindsideBarrage | 20175 | 动态全队诊断及“Raidwide + deathwall appears”提示 |
| RollingBarrage | 20188 | R8，不监听20180 visual |
| Whirlwind | OID2DAA | 与CE13同父类，首次输出惰性缓存；L3/R4显示、四预测钟与独立障碍，dead/state7保留 |
| Wind | 20183；Effect20184–20187 | 单项覆盖，锁Loc/Rotation/NPC钟+.1d，向前30；任意NPC来源四实际Spell Effect清除，Finish/销毁/超时不清 |

死墙Created和当前读条在冷激活时逐Actor回补，顺序可影响最终donut，不能全部回补后再统一按“墙存在”清除。量化会使整数中心也发生微小变化，后续圆环、定向导航和实际边界使用当前中心；边界缓存同时识别几何引用变化，不能仅比较中心。当前中心与形状重置只发生于新会话，不因墙销毁恢复。

Wind三类免疫按旧Party槽保存：Role(3054/160/1209)、Job(1722/2663)、Duty2345。Gain覆盖当前类别，Lose直接清类别，按真实60槽顺序；不取最大值、不按剩余状态重算。严格Expire>判定钟才免疫；冷补含Pet、热事件排除创建时Pet。Party-only重排不搬运历史。

非免疫时从当前viewer位置投影30码，落点超出当前场地时提示“About to be knocked into danger!”；源绘制的Projected Actor命令旧桥不消费，保留实际可消费的危险线。自定义导航独立输出当前中心反方向10码处的R30、半角135°/总角270°禁区，钟为锁定钟；不是20183注释中的矩形。免疫同时隐藏位移线、危险提示和此禁区，不新增玩家强制位移执行。

DrawEnemies的额外Beastmaster/Crow marker沿用既有旧桥不消费边界，不新增marker执行或目标优先级。现有公共移动/回放隔离保持；最终模型、DLL、目录及审计证据待补。

## CE12 本机验证

- `native-bozja-ce12-20261125-model-06.log`：33 PASS/0 FAIL（26 Bozja、7 script controls），exit0；独立复核后进一步重建确定的冷枚举顺序，最终 `model-07.log` 为9个CE12定向PASS/0 FAIL、exit0。分别从创建时构造[2,3]/[3,2]，显式断言源枚举，再对照两侧冷激活；不依赖Dictionary删除重插的空位复用。
- `native-bozja-ce12-20261125-build-01.log`：0错误、4项既有Crucible警告，exit0，21.80秒。DLL SHA256 `14c9278bd01507d3caf8bab612a1983a1b50e0034989ed6ae1bd6e554a3b47fb`；此后仅完善测试与规格，未改生产源码。
- 同DLL最终 `native-bozja-ce12-20261125-replay-02.log`：35 PASS/0 FAIL、exit0，包含CE12新增11项及前两CE/公共消费者。零长charge直接调用实际DLL `MechanicHintsConvert.ConvertForbiddenZone` 核对导航数量；R24.5边界及R10脱险通过真实SafeZoneSolver且无fallback。
- 同DLL无筛选 `native-bozja-ce12-20261125-catalog-01.log`：1461 PASS/0 FAIL，293入口逐文件独立编译，exit0。最终审计761 verified /452 pending /1 review，45 excluded，目标1214不变。
- 中间model01的测试API误用、model02的夹具未保存raw cast、model05的Region参数遗漏保留原失败日志。model06通过后独立复核发现冷夹具旧Actor销毁会清Cast，且重插不保证尾插；最终model07以两种确定创建顺序补证。没有改动生产、放宽容差或减少分支。

## CE14/CE21 本机验证

在 `.tmp/halatali-verify/` 使用隔离 .NET10.0.303，未改根 SDK11 配置，未部署。`FFXIV_SQPACK`/`PALACE_GAME_DATA` 指向本机实际 sqpack，不缺数据静默跳过。

- 最终 `native-bozja-ce21-20261125-model-05.log`：24 PASS/0 FAIL（17 Bozja、7 script controls），exit0。直接运行 model04 最终编译产物，包含真实源组件的形状/时钟/原引用/提示/导航差分、98/99/100共享队列和真实 BossModuleManager 超距重排反例。
- 最终 `native-bozja-ce21-20261125-build-03.log`：0错误、4项既有 Crucible 警告，exit0，23.12秒。DLL SHA256 `4ae2e19c398d16dbd536391f058f76c9cd0f1e8191d00478d87848f3110efe25`。
- 同 DLL `native-bozja-ce21-20261125-replay-04.log`：24 PASS/0 FAIL（3公共输入、13守夜/公共输出、8地生人），exit0。实际注册表覆盖外部空槽后先 Ray 后 Circle、98/99/100同键重排、重启；变化世界完整事件采样不推迟 Framework 截止钟，未声称在游戏进程采样了实时 IObjectTable。CE14 塔/伤害、CE21 安全显示/手线/首手/隐藏晋升/原目标及真实目标区皆已接入；实际 SafeZoneSolver R10脱险与R6入分摊均无fallback。
- 同 DLL 无筛选 `native-bozja-ce21-20261125-catalog-02.log`：1450 PASS/0 FAIL，293入口逐文件独立编译，exit0；覆盖保留既有枚举顺序的 Lapis 及其他公共消费者。

此前守夜单批 model06 的15 PASS、build02 的旧指纹 `a617fb…6024`、replay03 的13 PASS及catalog01的1439 PASS/293独立入口均为修补前证据，不用于证明最新公共顺序和地生人实现。

中间失败日志全部保留。守夜初批 model01为 TextHints 嵌套类型编译修正；replay01为 csproj 漏 Include；replay02在求解器通过后因测试反射不存在的 `_target` 中止，已按实际 `target`、`holdActions`、`blockCasts` 字段补正。本批 `native-bozja-ce21-...-model-03` 的真实管理器夹具漏 SubKind5，源因此不创建 Enemy 模块，修正真实输入后 model04/05通过；replay01把射线矩形类名误写为普通矩形；replay02对分摊显示误用未量化位置断言，现同时检查销毁原引用/原位置和真实量化位置，不放宽容差。build02/replay03使用全局 OrderedDictionary，catalog01在379 PASS后因 Lapis 显示顺序断言中止（exit127）；最终 build03收窄为显式 owner 排序，未改 Lapis 或放宽断言，replay04及catalog02均通过。build02旧指纹 `40d78ad7…c4174d` 不能替代最终证据。


CE11最终验证前缀`native-bozja-ce11-20261125-`：model05为49 PASS/0FAIL（42 Bozja+7 controls），build02为0错4既有警告（23.06秒），replay03为57 PASS/0FAIL，catalog01为1474条行首PASS场景/0FAIL、293入口独立编译，均exit0。最终DLL SHA256 `78368e51fb164955f948233634938e74869287e44aceac31227fcee6c0b67780`。RunWild原caster目标ID经Script内部InterruptTargetIds发布诊断，不接自动打断；Finish、机制禁用、失效视角和Stop清空。

model04的墙后导航差分失败因夹具只激活源Wind、没有ArenaChange，导致源仍用旧中心；补上真实源组件激活后，保留72方向逐点转换及所有clock断言通过。model02的原生中心量化缺陷与重复Start夹具问题见上文。replay01局部Lines函数重名编译失败，replay02在Stop后重新查询已经移除的运行脚本失败，分别改为WindLines及保留原实例验证清理；全部失败日志保留，未降低断言。实际源绘制经MechanicDrawConvert后的Start/End/Safe及最终DLL的真实注册表线字段均精确比较。

## CE24 七个实际激活组件

源为 `CE24PatriotGames.cs`，CFC735/T920、DynamicEvent10「据点防卫魔导兵器——爱国者」、主OID `0x2E14`。固定中心 `(-239.999,413.999)`；场地为半边24.5的正方形，拉起门槛为半边25的正方形，不能改为半径25圆。全部源AID的客户端extra为0，正式名直接取本机简体中文DynamicEvent10核对。

| 组件 | 消费 | 保留语义 |
|---|---|---|
| PlasmaGun | 20451 | 原caster动态单体诊断 |
| Neutralization | 20444 | R30、半角60° |
| OrderedFire | 20446 | R10 |
| SearingConduction | 20455 | R15 |
| ElectrifyingConduction | 20453 | 源实际为SingleTargetCast；不按枚举注释改为全屏 |
| LightningRod | Effect20448/20449/20450 | 量化出生位置的R4塔、无判定钟；实际爆炸按逐轴误差≤1删首项 |
| ElectrochemicalReaction | Tether114/115、Effect20443 | 最多4个量化位置，每处两次充能追加前50/全宽25矩形，第二次事件锁向及Now+10.2d |

三普通AOE只由各自watched Finish按source ID删除首条，不因Effect或超时清除。两单体组件使用原caster引用及当前raw Target，self目标回退当前TargetId，不伪造20452/20454等visual输出。组件顺序与源七项激活顺序一致，Reaction构造时的塔引用在模型内指向同一Mines列表。

地雷使用GenericTowers，不能复用CE14的开放世界OID0缓存。Forbidden只在真正Framework Update按旧Actor回调后提交的新Party重算：前8个槽、包含死者，非Tank且HP严格小于40000。每塔独立保存mask；出生Effect追加的新塔默认mask0，即使旧塔已有mask，也要等下一Update。坦克角色枚举与真实ClassRole核对，HP40000不禁。

计数和伤害预测使用默认Party的64槽存活成员。禁止本人时ActiveTowers创建完整R8塔，显示、Inside、人数、导航及预测都使用R8；允许本人仍R4。源inTower人数正确判断不额外检查本人Forbidden，不能擅自“修正”为所有禁止者都必须避塔。提示、背景危险填充和前景安全轮廓分别保留，背景先逐组件提交，再按组件顺序提交前景。

可加入塔依旧输出倒置圈交集，经真实有限网格采样；即使只有一座塔也不转成目标区。采样拒绝时保留提示及显示，不扩成强制入塔。整塔组件仅一组Raidwide诊断，默认钟和零mask均保留。Effect爆炸采用逐轴AlmostEqual，不能改成欧氏距离或删除全部重合塔；来源Destroyed、Finish和经过时间不移除已有地雷。

Reaction的4个上限约束位置缓存，不限制AOE总数。每次真实连线回调都充能，位置合并取首个逐轴≤1匹配；第二来源当前Rotation用于锁向。20443只清AOE，不清位置或半充能；Finish和超时不清。导航严格由“存在塔且第一塔Forbidden包含本人”决定，不能换成任意塔条件；无塔、首塔允许时，矩形仍显示并产生个人AOE提示。

冷激活逐Actor补首个非零ID连线，包含ID非零但Target0的已保存状态，与源BossModule激活合同相同。热帧快照只在首连线ID或Target变化时回调，Progress变化不回调；热Target0或哨兵0xE0000000不派发Tethered。**相同ID/Target的真实重复事件不能按快照规则去重**；Cancel仅更新已观察连线，不清位置/充能/AOE。ID保留宿主uint，不截断为ushort。

脚本将Director/Tether/Cancel/NativeEffect按原顺序暂存，在同次Framework采样连线后、组件生命周期Update前统一消费，期间不重读连线快照；事件Scan仍可采其他Actor输入，不晋升组件。冷激活读取已被真实事件更新的有效连线态，不能再次用旧快照覆盖。这样保留真实Feed的ApplyFrame→Flush→Update合同：固定快照B、同批包A/B恰3次充能；Cancel后无关事件不从旧快照恢复充能；空快照之后先到的真实包能参与本帧冷激活。MP回放依照实际逐记录tick消费，时钟冻结及反Seek沿原控制器。源真实Feed的ApplyFrame与ApplyEvent分别作为对照，不将直接重复OpTether伪装成未变帧。

本批模型覆盖全部职业枚举及HP边界、死亡/槽8、每塔异龄mask、方形边界、实际raw SDF与Convert逐点、四位置上限、时钟/方向锁定、冷热Actor顺序和真实Feed；实际DLL补原生Effect、真实重复Tether、切换/反Seek/owner清理。最终产物与日志见后续本机验证记录。


CE24最终验证前缀`native-bozja-ce24-20261125-`：model07为59相关PASS/0FAIL（52 Bozja+7 controls），build03为0错4既有警告（20.95秒），同DLL最终replay03为67PASS/0FAIL，catalog02无筛选为1484条行首PASS场景/0FAIL及293入口独立编译，均exit0。DLL SHA256 `0c3bc3f6b32c0fda934d9629819d022dc485c4461b22120ee6154ce29b492dbe`。replay03额外补真router的Tether→Effect20443→Tether混排，保持半充能并生成一条AOE；无生产变化，因此不重复全目录。

model01为ActorTetherInfo构造API误用；model02的三个失败分别为两侧职业不一致、只比较Reaction却消费完整塔导航、将未变帧模拟成真实重复OpTether，按真实输入修正夹具，保留全部数量/时钟/几何断言。独立复核进一步发现生产热target0错误充能、真实重复包被吞，以及同帧包之间重复采快照和冷激活有效态遗漏；实际修复后新增真实Feed和DLL router反例。build01暴露LogSystemTetherId为uint，按源类型保留，未截断；build02/replay01/catalog01虽通过，但对应批处理修补前产物`0796c8…1905`，不能替代最终证据。所有旧日志保留，未放宽断言。

## CE31 五个实际激活组件

源为 `CE31MetalFoxChaos.cs`，CFC735/T920、DynamicEvent13「钢铁魔兽——达因斯莱瓦」、主OID `0x2DB5`；名称及全部源AID的客户端extra零均由本机简体中文表核对。中心 `(-234,262)`，场地为半边29.5正方形，pull为半边30正方形。按源顺序激活SatelliteLaser、DiffractiveLaser、LaserShower、MagitekBitLasers、Rush。

Satellite20137是原caster动态NPC钟的Raidwide诊断，文本为“Raidwide + all lasers fire at the same time”；Diffractive20138为R60/半角75°，Shower20140为R10。普通AOE只在各自watched Finish按source ID删首，不被Effect或超时删除。

Bit组件仅在None接受模式Start，启动不清NumCasts；None不查询Enemies，非None在时间门前首次查询原世界顺序的bit OID `0x2DB6`，此后按真实Created追加/Destroyed原引用移除，模式重置不清缓存。不因死亡、Targetable或EventState过滤，当前位置量化、当前方向动态读取。Satellite锁Now+2.5/+12.3；Diffractive锁spell.Rotation+π及Now+2/+8.8/+10.6/+12.4；Shower锁caster.Rotation及Now/+6.5/+8.3/+10.1。前两模式严格Now>gate，Shower立即可见。

归一化方向误差≤1°。计数0–4时正向Danger@times1、左右默认色@times2；5–8时背向默认色@times3、左右Danger@times2；9–13仅背向默认色@times3。**默认色仍Risky，保持个人提示和导航。**任意NPC Effect20141全局累加，包含None时；恰14清mode和count。Finish、bit销毁及时间不结束模式。显示保持原序，导航执行与真实AIHints.Normalize一致的全禁区RefSort，不能换成稳定OrderBy。

Rush20139保留原source/target引用，缺目标的Start不补建，重复Start逐条追加，Activation在Start锁定。Update只刷新长度，查询按原Actor当前位置算方向；自引用取source.Rotation，不同Actor同坐标取Atan2(0,0)。source死亡暂隐藏、复活恢复，target死亡不隐藏。Finish按source ID删首，Effect不删；source销毁先对当前cast Finish，残余重复条目保留冻结原引用。同ID新target不替换旧引用：显示/文字仍按ID，导航按引用，预测按ID查当前Party槽。

其他target导航为动态长度、半宽7矩形；本人原target为每个存活Party队友的R100锥，半角保留未Clamp的Asin(7/distance)。距离<7产生NaN并在实际旧桥转换为空，不伪造导航；恰7的180°二等分线恢复失败，走现有有限网格采样；>7按真实恢复方向/半角转换。本人红色outline在所有组件background之后提交，他人普通填充；不新增父类未启用的“Tankbuster cleave”。预测mask仅取存活source目标，但时刻仍取原Rushes[0]，即使首source死亡。源玩家priority marker原桥不消费，不扩展输出。

最终前缀 `native-bozja-ce31-20261125-`：model04为67相关PASS/0FAIL（60 Bozja+7 controls），build01为0错4既有警告（21.29秒），同DLLreplay03为74条行首PASS/0FAIL，catalog01无筛选为1491条行首PASS/0FAIL及293入口独立编译，全部exit0。DLL SHA256 `0c35ac0938d46681810120cf876381149046d8177ef3bf2e03cba026ae35d782`。覆盖原source销毁重复条目、冷已有cast/缺target、正反Actor顺序、精确旋转矩形边界、首死钟、真实solver R10脱险无fallback、跨组件绘制分层、NaN零handle、切换/反Seek/owner及公共控制。

model01在恰7码的真实180°转换比较失败，揭示直接Fan与旧桥有限采样的生产差异；已按实际RecoverWedge及源SDF补正，model02/03/04覆盖所有既有Bozja扇形消费者。replay01/02的新绘制断言把场地outline算入技能列表，实际输出明确为arena→circle→other background→own outline；夹具限定技能键后保持精确数量/顺序断言，replay03通过。没有放宽容差或省略分支；失败日志保留。构建使用隔离SDK10.0.303，根SDK11未验；游戏内双视图像素、实时采样及执行仍待实机。


## CE34 七个实际激活组件

源为 `CE34TrampledUnderHoof.cs`，CFC735/T920、DynamicEvent11「邪眼妖兽——耶鲁」、主OID `0x2ECF`；本机客户端表核对全部实际AID extra为零。中心 `(-450,262)`，初始R24.5的32边形、pull圆R25。按ArenaChange、BullHorn、GlimmerInTheDark、AccursedLight、BalefulGlint、ArrestingGaze、DemonEye顺序激活。多边形直接构造WorldPolygonGeometry，按源float角增量、双精度SinCos转float，保留精确顶点边界；不绕LocalPolygon旋转变换。此修补覆盖全Bozja既有多边形消费者。

ArenaChange只在20968 Finish覆盖一项Donut20–25，钟为当时旧cast NPCRemainingTime+6d，不在Start预告。OID `0x1EB030` Created才量化当前中心、改R20并清环；不筛死亡/可选性，销毁不恢复，后续Finish仍可再建环。冷激活按每组件→全部Actor回补Created再Start，不合并成墙存在性判断。

BullHorn20968继承延迟单体队列：Start锁原source/target、当时Party槽与NPC钟，缺目标不建，target=self回退source.TargetId。Finish、死亡、销毁、换槽、同ID重生或超时均不重算/删除；slot=-1保留零mask。非空输出全局Tankbuster。任意NPC实际20968 Effect按完整MainTargetId删首个匹配目标项，不要求原caster；真实NativeActionEffect到模型的target已完整转发。

Glimmer的眼球OID `0x2ED1`每次Created追加R6锁量化出生点、Now+4.9d；源容量2不是上限，冷Dead/不可选也补。不跟随后续移动，不因Destroy/超时删除。任意NPC20963 Effect按caster当前位置逐轴≤.1匹配首项，计数10删首；原caster与target不参与匹配。显示保持追加顺序，导航全禁区执行源RefSort非稳定排序。AccursedLight20967为R8/至少8人的分摊，自己buddy为R4；BalefulGlint20966为普通R8，ArrestingGaze20964为R40/半角45°，保留watched Finish语义。

DemonEye组件在FalseDemonEye20962 Start追加spell.LocXZ锁点眼与固定NPC钟，默认range10000。20961 Start置反向并翻转全部已有眼，未来眼也反向；任意20962 Finish全清眼并复位，20961 Finish不清。普通提示Turn away、禁向半宽45°，反向提示Face、背离眼禁向半宽135°；危险点积≥.707107，反向取反，同点普通安全/反向危险。眼图不受range门控，视角弧受门控，弧和分段严格按源DrawSink；模型Y0，显示适配到viewer.Y，实际DLL验Y5。联合禁向使用既有DungeonFacingDirections，是MP消费适配；旧桥仅取最后满足提前.75秒的禁向中心+π，不宣称逐值相等。

CE34对完整合法PotentialTargets设主OID0/其他1，不覆盖源PendingDead=-1。基础Fate等级和高度来自Party槽0，最近目标距离仍取观看角色XZ；MP回放读取录制AllowedFate。复用QuestTargetPriority时仅T920且显式callback进入既有foray分支，OwnsTerritory未扩展，height可选参数保留旧调用。SetTarget为本批新增的原生消费，旧MechanicNavigationHost没有执行PotentialTargets选敌。不新增敌人marker、ForbiddenTargets、DoT或停手能力。Hints沿用移动/回放门控，每帧续期与显式清理；实际DLL确认Planned非空但回放执行请求为空。

最终前缀 `native-bozja-ce34-20261125-`：model06为92场景PASS/0FAIL（69 Bozja+16 Hydatos+7 script controls；额外一条PASS汇总行不算场景），build01为0错4既有警告（7.84秒），同DLLreplay01为99条行首PASS/0FAIL，catalog01无筛选为1507条行首PASS/0FAIL、293入口独立编译，全部exit0。SHA256 `8990467bfc10ae6ac7d81f9731e68364fe3fdd9b068cdf5914d3ea2acab5ff90`。覆盖真实源冷热/代际、完整MainTarget效果、精确边界、联合面向与选敌、solver/双视图/反Seek/owner及公共消费者；隔离SDK10.0.303，根SDK11、游戏采样/像素/执行仍待验。

model01为测试Polyline/ActorCastEvent API误用；model02中五项为Hydatos先初始化后Bozja.data未取既有服务、另两项为未构造真实cast transition及未走旧桥补集转换，按实际输入和消费修正。model03/04精确32边形顶点反例揭示生产LocalPolygon差异，修为源WorldPolygon；model05的容差反例把+.09叠加在未量化原点，改为已核实锁点偏移且保留.1边界断言。独立审查指出导航RefSort、眼图Y和Party0基础输入，均实修并加入反例；未放宽容差、减少断言或隐藏失败，旧日志保留。


## CE33 六个实际激活组件

源为 `CE33WhereStrodeTheBehemoth.cs`，T920/CFC735、DynamicEvent15「战栗之角——奇尔维尼克」、主OID `0x2DCB`，全部源AID extra本机核零。中心 `(231,95)`，初始R29.5的32边形，pull圆R30。按BlazingMeteor、ZombieJuice、WildBolt、WildHorn、ZombieBreath、Thunderbolt顺序激活。生产使用MP原生模型，不创建BossModule或MechanicSession；源运行库仅作测试oracle。

BlazingMeteor20194维护原caster列表，以当前NPC剩余时间严格最小者的Loc作为原点，平钟保留先者。构造Refresh即首次建立corpse OID `0x2DCC`原引用缓存，之后只在Start/Finish重算原点。Start追加caster、Refresh，再按本次spell的判定钟追加安全区；Finish按原引用删caster、Refresh、FIFO删除安全区首项，不能按caster匹配安全项。每个corpse锁当前原始位置和实际hitbox，不筛Dead、Targetable或EventState。阴影内径为distance+.2、外径30、半角asin(radius/distance)，radius>=distance时半角90；联合阴影后扣除全部原hitbox圆，不额外+.5。保留源三步Clipper、每步整数/float往返、2^20比例、.05弧精度及偶数segments范围4..512。Check复制源包围盒、行号、行X范围、水平边、SIMD精确yspan/FMA与scalar epsilon分支；本机执行覆盖实际硬件分支，不宣称其他硬件已实跑。

三条消费合同分别验证：原模型Check含洞；旧DrawConvert只画各外环并省略holes；旧导航对反向SDF进行有限网格采样，危险填充超过65%即无输出。无corpse不创建AOE；有corpse但裁剪结果为空时保留原队列，原生拒绝空预览注册，导航维持实际采样结果。`ForbiddenDistance`仅服务该符号采样：源SDPolygonWithHolesBase使用SpatialIndex及带1e-8分母，MP遍历全部边，**原始距离幅值不宣称逐值相等**；安全半环深处源可为sqrt(float.MaxValue)，MP为有限正值。真实ConvertForbiddenZone与MP转换按数量和几何Check逐场比较，采样仅消费Distance<=0。没有把原始数值差异伪称导航失败，也没有把它包装为完整SDF等价。

ZombieJuice20196输出按cast预测、Effect预测、实对象三组原序。Cast跟当前target位置，缺失回spell.Loc，判定钟不加spawn delay；Finish按caster ID删首个cast。每个完整Effect独立追加，MainTarget当前Actor优先，否则用完整nullable TargetPosition，钟Now+.9d；20199 Repeat不预测，不做包去重。target和包点均缺失时不补Vector3.Zero，累加MissingJuicePositionCount并显示明确诊断；显式记录的零点有效。仅event预测非空时Update才首次查询OID `0x1E972C`缓存；输出查询也可初始化缓存，之后按Created追加/Destroyed原引用删除。非state7实对象每次Update在R2内删首项，一个对象一次最多消费一项，不筛死亡；实对象输出量化位置和default钟。真实Feed验证两个重复包与一次Update、下一帧消费第二项，以及corpse顺序正反和helper先于target冷枚举。

WildBolt在20200 Start覆盖单个固定钟，按原NPC剩余+.9d的float到double顺序；Finish、Destroy与超时不清，任意NPC20201实际Effect清除。仅全局Raidwide和伤害诊断，不虚构Circle。ZombieBreath20198为长60、半宽3的普通锁点锁向矩形，只按watched Finish清首项。

WildHorn20193保存原source/target，缺target不建，自目标不回退。方向随原引用动态，R18半角60；Finish不清，同source ID真实Effect删首项。Update永久移除sourceDestroyed/sourceDead/targetDead，targetDestroyed单独不删。同ID新target不替换旧引用。别人目标导航R18，本人原目标向每个活Party成员输出R100同半角避让；本人前景outline在所有组件background之后。伤害mask聚合活source的目标槽，钟仍取原Horns[0]，包括首source刚死未Update；保留Tankbuster cleave全局提示。

Thunderbolt仅在20196 Finish按spell.Loc R6从corpse缓存取首项，20198 Finish按长60/半宽7.35矩形取全部；保存量化位置、当时rotation和Now+12.8d。20197任意Finish清全部，不在该动作Start推断，不按时间、corpse死亡或销毁删除。冷已有20197 cast不能补造前序Finish历史。

最终证据前缀 `native-bozja-ce33-20261125-`：model11为105场景PASS/0FAIL（84 Bozja、14 Goldor、7 controls），build03为0错4既有警告，同DLLreplay03为88条行首PASS/0FAIL，catalog02无筛选为1514条行首PASS/0FAIL、293入口独立编译，全部exit0。SHA256 `7329e703bb2d7c07a5191f89796e011e461950cd7ad5eec73dc22fead0a64e13`。模型直接覆盖真实源/Feed、原Actor代际与FIFO、精确显示顶点、实际旧桥转换、真实solver脱险无fallback；实际DLL覆盖双视图、空几何拒绝、完整Effect录制输入、反Seek、机制开关、owner及回放无执行请求。公共Goldor多边形填充仅原样提取，14场景回归通过。隔离SDK10.0.303；根SDK11和游戏实时采样/像素/执行仍待验。

失败证据保留：model01为正式名占位和夹具重复Finish；model03/04为源SIMD/FMA精确顶点差异，model05修后13场景通过；model07为新增测试对ReadOnlySpan调用Single的编译误用；model08虽15场景通过，平移反例未同步Native输入，不能作为该边界证据；纠正输入后model09在真实源False/MPTrue精确失败，加入源行范围后model10/11通过。replay01完整包地面点断言失败来自新夹具默认JsonSerializer将Vector3字段写为空对象，改为显式XYZ后保留原精确相等断言，replay02/03通过。model11/replay03额外覆盖零半径corpse产生空外环时不注册空预览。没有放宽容差或降低断言；旧日志及本轮备份保留。


## CE23 八个实际激活组件

源 `CE23FiresOfWar.cs`，T920/CFC735、DynamicEvent9「炎兽训练师——火焰百夫队」、主OID0x2E75，全部源AID extra本机核零。固定中心(83,563)，R19.5的32边形，pull圆R20。八组件按 CantTouchThis、Pyroplexy、CatchingFire、ExplosiveCountdown、HotTemper、TooHotToHandle、SlowDeflagration、ScaldingScolding 激活。

完成条件严格为cached原Mater.Dead或四种Trash（2E75/2E72/2E73/2E77）全Dead/Destroyed；空列表为true，HP0不等于Dead。先阶段完成，再UpdateModule首次缓存Mater，再Component.Update；原Mater销毁不被新同ID替代，Primary同ID仍按源Manager重绑。保留Enemy首次查询/Created追加/Destroyed原引用删除、预pull Draw初始化列表时机。采样帧输入→Flush→Update，无新采样的Framework仍推进组件/生命周期，然后AI一次、Render；切视角回调不额外消费AI。真实Feed已覆盖首帧缓存dead Mater、下一Framework仅BeginFrame/Session.Update和Region.Update即结束，以及Primary换代后不意外重启。

CantTouchThis任意Actor SID2056 Gain追加原引用R6、Now+16；真实60槽、冷Pet/热Pet及extra/expiry差分沿用输入合同。Lose不清，20661/20662真实Effect按caster原引用删首，死亡隐藏不删，销毁但未死不隐藏。提示检查每个原stack自身位置，死目标仍可提示。participants首次AI才捕获场内R20/OID0原Actor，不筛Party/Dead/Destroyed，填表前countP仍0；保留源participants[i]重复读取的消费。HP严格>30000的0/5弱Goal不越过旧桥100门槛，不新增强安全目标。参与者索引失败时显式诊断viewer/HP/stack/count，整个导航（危险/目标/边界/选敌）撤销，保留历史、arena轮廓、其他绘图与全局Raidwide，下Framework重新计算并可恢复。

Pyroplexy20659是R4开放世界塔，复用原Actor惰性soakers，所有判定/导航/背景/前景使用实际Radius；旧CE14的R6同时回归。CatchingFire20666是普通Raidwide，保存原caster并取当前NPC钟/Party mask，watched Finish按原引用删首，只有诊断无新Circle。时钟精确读取源ActorCastInfo.NPCRemainingTime核对，不能将(Total+.3f)-Elapsed简化为常量.3f。

ExplosiveCountdown仅20665 Start锁Loc与首NPC钟，形状Donut11–18→Donut5–12→Circle6。20665/20664/20663 Finish以order+位置逐轴≤1匹配首项，跨caster可推进；有sequence时有效order先增NumCasts，找不到仍计数。下一钟是回调Now+4d。任意2E78 Destroyed清raw sequences，Dead不清。发布列表仅Component.Update且count/version变化重建；同批clear+add等数量且未Finish时保留旧发布，不能实时替换。HotTemper20668真实R24，普通watched Finish，无TTL，旧注释R30不作为参数。

TooHot20667锁Loc/R10/钟，Finish按casterID删首。恰9项才首次锁原Mater位置值，严格>找首最远，按XZ排除所有同最远坐标；其他数量全部显示且PotentialTargets全部Pointless。恰9时最远坐标逐轴≤1的目标priority1，其余−1，保留PendingDead保护。缺Mater对应源索引异常：整导航及个人提示失败，保留其他绘图、全局Raidwide及arena轮廓，保留9条历史并重试，不猜中心。重复最远导致源固定8数组带null尾项，实际Source DrawCapture验证先保留有效圆再报错；原生发布有效7等数量圆和明确诊断，不补造第8圆、不把全列表清空。

SlowDeflagration20669仅动态首caster“Enrage! n.n秒”提示，无伤害预测，20670不清；watched Finish清首。ScaldingScolding21255是常驻Cleave：主OID活且可选中的原Enemy，当前TargetID动态解析，不要求Start/Finish/Effect。R8半角60、default钟；个人文字用量化viewer与原始source，导航/outline量化source；目标本人向其他活Party每人R100锥，其他人R8锥。没有Tankbuster cleave全局与预测，不借WildHorn的锁目标队列。

证据前缀 `native-bozja-ce23-20261125-`：model09为123场景（100 Bozja+16 Hydatos+7 controls）PASS/0FAIL，build03为0错4既有警告，同DLLreplay03为97条PASS，catalog01无筛选为1523条行首PASS/0FAIL、293入口独立编译，全部exit0。SHA256 `99089e9fb077d83fe13b2f75d35fa8b1c9018aecff38ebd47066e634ccddeea6`。实际DLL覆盖solver无fallback、双视图、真实完整Effect、缺Mater整导航/边界/选敌撤销与恢复、参与者失败绘图保留、无scan连续Framework和视角回调单次AI、反Seek、开关、owner及回放禁执行。隔离SDK10.0.303，根SDK11及游戏实时采样/像素/执行待验。

失败日志全部保留：model01测试字段/类型编译误用，model02遗漏旧桥Complement转换导致差分失败，model03 tuple/params编译误用，model04起逐步增加真实Feed/Enemy缓存/重复最远等覆盖，model09为最终公共更新版本。replay01在精确动态钟断言失败：夹具误把源4f+.3f−4f当.3f，改为直接读取真实源ActorCastInfo.NPCRemainingTime，保留DateTime严格相等；replay02通过后独立复核发现切视角同帧多AI，移除重复回调并在replay03新增反例通过。没有放宽容差或用原测试PASS替代新增边界。


## 札杜诺区域接入后的共同回归

CE51接入时独立审查发现南方GUID未列入ScriptManager空配置默认启用清单，旧实际专项先显式Enable掩盖了这个接入缺口。本轮补南方与札杜诺两GUID的默认启用，同时全目录验证空配置默认、显式关闭/刷新保留、两区设置互不影响。Region按Definition.Territory过滤、机制列表按本区、精确Owns同时匹配区域与PrimaryOID；南方既有十项索引/参数未重排。普通CastGaze新增独立Kind，不改变耶鲁20961/20962特例或选敌。共同最终证据为 `native-zadnor-ce51-20261125-model-06/build-02/replay-03/catalog-02.log`，135模型、0错4既有警告、109联合专项、1536全目录行首PASS、294独立入口，全部exit0，SHA256 `7b89388e6cd28269139b7c7236f4af555a0d88fd70f1d33924b5db577b2a747b`。新入口独立编译失败及修复见NativeZadnor.spec.md。前述历史CE23等DLL证据不自动替代此轮共同验证。


CE22冷激活仍pending：真实源ActivateComponent<ChocoMeteorStream>在只有Rest20587、一First20586后Rest、Rest先于两First三种Actor输入顺序均于OnCastStarted访问initialPositions[0/1]抛ArgumentOutOfRangeException；两First先于Rest成功。tests/BozjaRegression.Blockers.cs保留四种实际源反例，native-bozja-choctober-20261125-blocker-01.log为1场景PASS、exit0。未新增native ownership；缺已结束First历史时不猜两初始点或旋向。解锁需真实历史录制或明确的输入恢复合同，不能用完整热序列PASS关闭冷恢复缺口。继续CE32，正式785/428/1/45不变。

## CE32 魔导劳工X式五组件

源 `CE32RiseOfTheRobots.cs` SHA256 `c0f5a5a8c2255c83ce6bac32d6f985f6afb15000d90d97a395a0abae3bd1901a`，按实际顺序激活 ArenaChange、Incinerate、OrderTowers、OrderForcedMarch、Train。客户端 CFC735 ↔ T920 唯一双向，DE14「新型铁巨人——魔导劳工X式」，20个唯一源AID均存在、无空名、附加读条均0；Boss2E8C、DeathwallHelper2EE8及Helper233C在BNpcBase，MathTowers1EB036/Deathwall1EB032在EObj。BNpcName9426检索名为「魔导劳工X型」，不宣称已证实OID到NameID的映射。初始固定中心(104,237)、R29.5正32边形、pull30。

Arena只在20671 Finish产生Donut25–30，钟取回调时原caster NPCRemainingTime加4.3d。Deathwall Created量化当前中心、换Circle25、清环；Created后后续Finish仍可重新发布环，不借CE42缩场保护。Incinerate仅20687/20681，普通watched Finish按原caster引用清首，动态原NPC钟及活Party伤害mask只诊断；21030不伪造圆或伤害。

数学塔只读取Start回调时旧Party的0–7槽Actor（含死，不另按Actor类型过滤），参与条件MaxHP≤9，当前HP无关。四动作20677/78/79/80分别除3/4/5/质数，j=0..4按源outsideSafe分支生成Numbers和选中塔。塔位置固定原中心外20码、R5正20边形，双精度SinCos转float、两次Clipper规范化；显示/Check永远是选中polygon，安全分支仍Risky=true，只反转Distance。Check保留源半开上端点：例如MaxHP2/除3时(104,222)不在内；显示几何的通用边界包含不替代这个判定。40组完整数学组合、每个多边形顶点及Z的相邻ULP均与真实源逐项比较。任意caster的对应watched Finish全清8槽。

原始Distance保留源“普通形状内部0、反转形状外部0”判定合同。现有旧桥没有将反转polygon归入Complement名单，会走有限采样并因面积超阈值丢弃。原生保留该消费边界：安全色数学polygon仍显示且参与个人提示，但不新增Goal；普通polygon转换为实际危险区。行军的反转R7扇形和Train的反转R3圈属于桥已支持的Complement，分别发布安全Goal。

OrderForcedMarch的1293/1295/1294/1296为前/左/后/右，1257/3629覆盖ForcedEnd及独立计数；真实60状态槽、重复Gain追加排序、Lose删首同方向、无TTL。它与CE42只提取状态与逐段float位移算法，继续使用6.6f×3s、双精度SinCos缩float；目标提示仅末段非零时检查数学Numbers与Check，无数学才查Arena。每实例randomOdd为1–99奇数；测试读取同源实例随机参数再对照，未用两个独立随机结果比较算法。AI仅消费首Pending，默认Numbers0时方向45°×odd，非0时方向π−90°×(num−1)，禁向半宽175°；默认禁向不减Pending.dir，非默认减。旧桥只在0.75s窗口取tuple中心+π，未改成凝视联合求解。回放只输出计划，不执行面向或移动。

Train任意Icon重写increment与Now+8d（235=-60°、236=+60°、其他=0）。四个First Start记录原Rotation，0方向有效；恰两项且有increment才初始化两Sequence，最后Start决定6/12次。第三Start导致Count3时不猜补。每条未来再当前、最多2项，Cone100半角30°，间隔1.2d；只有20672/73/83/84前方真实Effect倒序推进全部Sequence、每条独立增NumCasts，后方Effect/Finish不推进。发布缓存只按Sequence count/NumCasts变化，过期不重算未来钟。末次当前仍Risky但FutureColor。与大兀只提取发布算法，回调与缓存版本独立，两个大兀消费者随公共修改回归。

源异常有显式消费适配：数学Start先清8槽Numbers，MaxHP>9跳过且不清旧AOE；重叠不同caster可留下旧polygon+空Numbers，使Tower个人提示抛异常。即使没有旧AOE，空Numbers+PendingMarch也使AI抛异常；联盟槽8以上有非零末段/首Pending也可能越界。真实源组件及完整Feed均有反例。原生不补造候选、不清历史：提示失败撤个人提示，导航失败撤本模块全部危险/目标/边界/面向及执行请求，保留绘图、场地显示轮廓与全局提示并报明确诊断；下次合法输入自动恢复。联盟仅ForcedEnd剩1tick而float投影为零段时不伪报异常。无Party槽viewer按旧宿主睡眠，不回退槽0或保留绘图。


CE32最终验证前缀 `native-bozja-robots-20261125-`：model07为352相关场景PASS/0FAIL，model08和最终model09各为11个CE32定向PASS。model09真实Feed初始即带玩家slot0/59与Pet slot59，热Lose不移除冷Pet记录；数学Start与换Party同帧，断言按旧slot1/MaxHP6生成 `[0,1,2,4]`，查询再取新viewer槽。独立复核无剩余确定差异。

生产build01为0错4既有警告，DLL SHA256 `1a67347032d77e27d693572a31024ea6002a75f4a7d751dba32c982841756cc4`，此后仅测试与文档改动。实际DLL replay02为12条PASS（9 CE32+3基础），最终replay05南方/札杜诺/女王古殿联合294条PASS；catalog01无筛选1714条以 `PASS ` 开头的行、0条FAIL，296入口独立编译，全部exit0。首次收取会话时汇总打印1723，包含9个非行首PASS子串，登记以原始文件行首1714为准。全目录后新增的真实Feed强化及两个owner实际逆序重排断言分别由model09/replay05补验，不把早期编译快照当成最后测试已验。真实solver、显示/导航分层、控制/owner释放、反Seek与回放禁执行通过；SDK10.0.303隔离运行，根SDK11与游戏实时采样/像素/执行待验。

失败历史保留：model01为raw Cast及future color/Complement/真Party夹具；model02为南方计数与过期状态重复采样；model03 List/array类型、model05局部名冲突均编译失败；model06独立March oracle缺OrderTowers依赖，model07补真实依赖后通过。replay01源异常反例未触发，因主怪先于玩家更新回调，改独立新Helper使所需真实事件顺序成立，未改源异常断言；replay03在138条PASS后因CE42人工线全局插入序预期错误失败，最终按真实各owner占位计算expected，并把两owner目标分别Reverse，显式要求排序前确实不同，检查其他owner内容与绝对槽位。没有改变生产排序契约。正式清单786 verified /427 pending /1 review、45 excluded；仅CE32源精确匹配新增ownership，CE22继续pending。


## CLL2 妖术师 阿尔贝雷欧（2026-11-25）

源为 `Shadowbringers/Foray/CastrumLacusLitore/CLL2AlbeleoTheMaleficient/CLL2AlbeleoTheMaleficient.cs`，类型名为 `CLL2AlbeleoTheMaleficent`。客户端实表核得 CFC735↔T920、BNpcName9433=`妖术师 阿尔贝雷欧`，全部源枚举动作 CastTimeExtra 为零。生产复用南方入口（1.11.0，共十一CE与CLL2、八十四组件），不创建运行库会话。

实际激活顺序为 DiffractiveLaser 21503/R5、MagitekCannon 21505/R6、MagitekRay 21502/前40全宽6、GrandSword 21506/R27总120°、BalefulGaze 21508/range35、AbyssalCry 21510/可打断。枚举中的其他动作不补造输出。四AOE与凝视Start锁点锁向/原钟、真实Effect仅计数，普通watched Finish按ID删首；打断组件按原Actor引用删首，固定`Interrupt!`不带名字，不新增自动打断执行。

35码边界包含，文字/禁向/玩家弧受距离门控，眼睛及瞳孔保留且按朝向着色；范围0及35上下相邻浮点均对照源。精确方向输入保持既有MP联合面向求解，不宣称等于旧桥0.75秒窗口最后center+π。Priority覆盖全部源合法PotentialTargets：Boss0，其他1，既有Pointless仍保护；实际DLL计划选合法add、濒死回退Boss，回放无可执行请求。原生PR选敌是既有MP接口适配，旧导航桥没有该执行消费者。真实DrawEnemies经sink捕获Primary及列表add；实际Convert跳过ActorMarker，Predator不在绘图清单但仍可参与选敌，不新增敌人图标或危险圈。

默认pull只要求Primary可选且InCombat，不额外要求玩家在走廊中。结束为原Primary Dead/Destroyed或HP**恰1**，HP0但未Dead仍保留；脱战/不可选不重置。冷HP1同Update进入并完成，源ActivePhaseIndex可能为阶段末索引而ActiveState为空；不能以索引非负当作仍运行。热退场与原对象回血不重拉，独立adds未结束也全部撤输出。

场地通过`--castrum-lacus-arenas`从实际运行库私有arena导出，中心(80,-376.45502)，采样半径33.535004。使用世界顶点保留真实凹走廊；不使用包围矩形或凸包。稠密点与全部转角X/Z上下相邻浮点九点对照真实ConvertBounds。初版LocalPolygon在转角失败，已修导出器，仅作用本场地，不改变其他已验场地。MagitekRay旋转3、原点(80,-376.45502)的真实CLR反例：source raw Check与native Ray包含原点，旧Nav/Draw移中心后的矩形不含该点；保留既有MP直接Ray适配以保持源原始语义，明确不声称旧桥逐浮点等价。

验证证据位于`.tmp/halatali-verify/native-cll2-20261125-*`，各有同名.exit：model07共457相关PASS/0FAIL；model10最终13定向PASS/0FAIL；build01为0错4既有Crucible警告、8.57秒；replay02共271相关PASS，replay03为10专项与3公共PASS；catalog01无筛选1826行首PASS（1817测试+9汇总），296独立入口编译。全部exit0，DLL SHA256 `f58f55791e4fdc545c0aa23afa960b785c673f1fe5985cbcd32c6000abadf7a7`。DLL专项包括真实solver无least-danger fallback、六组件/Finish、有限凝视/双视图、HP退场、选敌计划、机制开关、逆Seek、owner与回放执行隔离。

失败历史保留：model01夹具Enemy构造签名错误；model02生命周期查询已卸载模块与缺Enemies索引；model03夹具局部名冲突；model05真实局部多边形转角差分及冷完成索引断言；model06只剩冷完成索引断言；model09 ReadOnlySpan调用需ToArray。没有放宽边界容差或删去关键断言。核心/公共修改前快照在`.tmp/bozja-20261125-backup/cll2/start/`，登记前快照在`registration/`。使用隔离SDK10.0.303的CI构建；根SDK11及实机采样/像素/实际执行未验，未部署。

源SHA256：

- CLL2：`869e2744d689fa45bc22012d18d524489635f3cb826a42c3971ec3fde79ef6ad`
- Gaze：`c287a08c62abd366b5e6b8b52c931a4c372b26a9172258f1ca0864904a06efad`
- GenericAOEs：`faa9adeaac23a0d7ee8ca6c4c9e50d896d7d1e04082d9bb996c531faa9621b45`
- CastHint：`c0a923b1d0683d67890b7afa768f19dabdfb27493c2f6586e7f2da86c9d88657`

## 2026-11-25：FATE1605/1612 标准遭遇战

两个标准Skirmish（破坏劳动十号／破坏魔导劳工）十六实际组件完成原生离线验收并登记：model07为483相关场景PASS/0FAIL，最终build02为0错4既有警告（7.50秒）；同DLL replay05为156条相关PASS（含22个遭遇战专项及3公共），catalog02无筛选1848条行首PASS（1839测试及9汇总）、296入口独立编译，全部exit0。DLL SHA256 `4b6b2780011fc64be4616ba7a7e5d6698cd3195030bedfd2548afe87f13b34cb`。真实Manager/Feed差分覆盖创建代FateID、旧Party0创建／新Party0更新与选择、同帧reset重建／双模块Loaded正序尾部与Selected、R+10收敛；十六规则冷热、原目标分摊FIFO／换代／异钟／少于二人导航及实际solver、计划选敌、开关、逆Seek、owner、回放禁执行均已验。源oracle显式补ActiveFate及创建FateID，不宣称旧桥完整输入链消费。正式审计795 verified /418 pending /1 review，另45 excluded，1214目标不变；296为入口数。继续PyromancerSupreme的每个Pending独立构造ThermalShock／六组件阶段变体，尚未实现登记；CE22、DRN5、Quantum、DD190与Zone决斗农场缺口保留。SDK11与游戏实时采样/像素/执行未验，未部署。

- `tools/generate_bozja_skirmishes.py`仅生成已审的两源十六条配表，核对两源、GenericAOEs/StackSpread、OpenWorldFate/Manager/PartyState及真实Feed指纹；不解析类名猜override，不包含Pyromancer。输出仍是MP原生规则，不创建运行库会话。
- FATE1605=破坏劳动十号，OID0x2EA3；FATE1612=破坏魔导劳工，OID0x2EA5。各八组件按源状态机顺序，Compress均为Cross50/3.5；Accelerate为R6/min8；MagitekLaser前75全宽4。全部源AID额外延迟核零；min8只是人数提示，不补造分摊伤害。
- 源Actor.FateID只在真实Feed OpCreate写入，原生以BozjaActor.CreationFateId冻结每代；快照字段变化不伪造新代，模块reset也不重采。当前FATE为null时诊断且不猜边界；已知Id0则合法使用默认R30。命中时按ID或中心逐轴>.01或半径>.01更新缓存；失配保留旧缓存直到Manager reset。
- 源Manager以实际Party[0]决定更新与选择。Actor创建回调使用旧Party，全部Actor回调结束才提交新Party；无Party0时不加载／选择新模块，但已有组件继续消费真实事件。源玩家缺失不能清空分摊或完成计数。
- reset卸载、尾部重建并在同帧冷回补；区分pending逆序晋升与reset正序追加。真实双模块Loaded顺序、同优先级Selected、离R+10后下一帧不继续重建均有断言，不能统一延后一帧。原目标死亡只隐藏、复活恢复；Destroy/同ID新代不夺取原引用；不同caster同目标Finish按ID删首；非Party目标仍登记；多分摊导航取第一判定钟。队伍实际Feed只提交0–7，少于二个存活成员不发分摊导航。
- 绘图保留一个十字复合预览，现有导航转换拆为两个矩形；分摊前景显示与Goal导航分别断言。目标规划保持Boss1／战斗中其他0／其余-3及既有Pointless保护。伤害预测无虚构输出，回放不持有PR执行请求。
- 真实oracle将ActiveFate写入ClientState，并为Actor创建DTO补FateId；旧ReplayFeedAdapter丢FateId、旧WorldFrame不携带FATE的边界未修改。此处验证源算法合同及新增原生录制输入，不宣称旧桥完整输入链等价。zone决斗农场不计为迁完。

证据均位于`.tmp/halatali-verify/`，前缀`native-skirmish-20261125-`：model07=483 PASS、build02=0错4警告、replay05=156 PASS、catalog02=1848行首PASS；每份.exit=0。model03=20 PASS、model05=22 PASS、model06=482 PASS为阶段证据。model01/02的18PASS/2FAIL保留：初因旧转换丢创建FateID，进一步暴露真实Feed只在创建写FateID，不能以在同代更改字段的夹具预期修生产。replay01/02失败揭示Cross导航实为两个矩形，按真实转换合同补精确断言。最终独立复核修正双reset次序、旧Party0创建与Region选择门控；所有公共改动用最终DLL重新全目录验证。备份`.tmp/bozja-20261125-backup/skirmish/`。


## 2026-11-25：FATE1616 最强的火焰法师

最强的火焰法师（PyromancerSupreme）七实际组件已完成原生离线验收并登记。model06为498相关PASS/0FAIL；build02为0错4既有警告（7.62秒），同DLL replay03为12专项加3公共PASS，replay04相关168 PASS；catalog01无筛选1860行首PASS（1851测试及9汇总），296入口独立编译，各.exit=0。DLL SHA256 `606a5c9fbe4e8dc1143e1f51770ae02eca9c9b23669b1f2ea2d01f6e845f7ecd`。每个Pending独立构造ThermalShock R20，保留冷热列表及晋升历史；构造期无导航/选敌/通用接管，phase局部停用；真实旧Zone桥优先与脚本显示互补、wipe只卸载Loaded保Pending/冻结场地、Destroyed远距释放、HP0与不可选退场、同帧相同ID重绑及六组件八动作均已源差分和实际DLL验证。正式审计796 verified /417 pending /1 review，另45 excluded，1214目标不变，296为入口数。下一批继续CLL1双场地/磁力/接线；CE22、DRN5、Quantum、DD190与Zone决斗农场缺口保留。隔离SDK10.0.303，根SDK11与游戏实机采样/像素/执行未验，未部署。

- 源 `Shadowbringers/Foray/Skirmish/PyromancerSupreme.cs`，SHA256 `e7803338d9570c4527b28c77d060dac122b117f8e4a7bf4e55402e6df8897b94`。OID0x2EF7/FATE1616/CFC735/T920，入口1.13.0，南方现为十一CE、CLL2和三遭遇战共107组件。确定性标准生成器仍只包含前两源，本变体显式手写，不伪装通用转换支持。
- 构造即激活ThermalShock；实际半径20而非枚举注释30。按NPC真实Spell Start锁Loc/Rotation/当前raw剩余钟；watched Finish按caster ID删首，Effect只计数。死caster与过期保留，Pet/Ability不消费。每个实际构造实例有独立列表/计数，Pending同时接事件；晋升不得按最新快照重建，较早热历史与较晚冷历史可保持不同顺序。phase只停用本实例Thermal。
- 阶段顺序为PyreticEruptionPyroscatter20929/20930 R8、TridirectionalFlame20927 前60全宽8、Pyroburst20931 R10、GrandCrossflame20928 Cross40/9、Firestarter20926 Donut6–40、GarleanFireSnowsOfBozja21246/21274 R5。两组保留任意Action Finish按caster ID删首及99项阈值RefSort；其余普通watched Finish。阶段完成只认Dead/Destroyed或不可选，HP0存活不退出。
- 构造期仅显示和GTFO提示，不发布规划边界、禁区、计划选敌或通用躲避接管。实际Party内观看者缺失只撤显示不删历史；存在Active Zone时由旧Zone显示优先。Host原生Owns门控补充`ActivePhase != null || ActiveZone == null`，预战有Zone时放行未迁Zone桥，phase或无Zone时互补接管。实际DLL以真实Host和源StateMachine/Zone对象核验三种组合，不调用其动作执行。
- 同帧Destroy/Create相同ID保留已Loaded实例及phase；真正卸载后下帧新模块重构。wipe只释放Loaded当前Primary，保留Pending且事件继续；wipe期间不更新FATE场地，解除后保历史恢复；Destroyed且超500码路径先释放构造缓存。独立复核确认此前两处释放遗漏已闭合。
- 真实源Feed oracle显式补ReplayFeedAdapter旧转换丢失的FateId/IsSpell；Ability不得被夹具强制改成Spell。不修改旧桥，不宣称旧桥输入链等价。伤害预测无虚构输出，回放不持PR执行请求。

证据前缀`.tmp/halatali-verify/native-pyromancer-20261125-`：model06=498 PASS，build02=0错4既有Crucible警告，replay03=15 PASS，replay04=168 PASS，catalog01=1860行首PASS，均exit0。实际DLL包含六组件八动作、真实solver、计划选敌、机制开关、逆Seek、owner、回放禁执行及Host Zone互补显示。失败历史保留：model02同帧重绑夹具漏OpCombat；model03静态Settings缺Svc服务；model04真实wipe更新差异后修生产；model05旧adapter强制Spell后修oracle。replay01跨Seek持有已清字典、replay02源Config缺默认节点，均按真实生命周期补夹具而未降断言。build01本次nullable警告已修，最终仅4既有警告。备份`.tmp/bozja-20261125-backup/pyromancer/start/`及`registration/`。
