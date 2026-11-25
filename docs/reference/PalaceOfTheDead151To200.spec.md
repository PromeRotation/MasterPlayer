# 死者宫殿151–200层原生脚本与录像复核

## 入口和边界

`Presets/PalaceOfTheDead/Entries/Palace151To200.cs`，ID `promeradar.potd151-200`，默认启用并尊重已有逐脚本关闭设置。实际 Territory 为 **603/604/605/606/607**，对应 CFC **214/215/216/217/218**。200层没有对应 BMR Boss 模块，不能凭区域范围增加 Boss。

本次迁移依据仓库 `MechanicRuntime/Upstream/Modules/Heavensward/DeepDungeon/` 的 `PalaceFloorModule.cs`、`SharedBounds.cs` 和 `DD160Todesritter.cs`、`DD170Yulunggu.cs`、`DD180Dendainsonne.cs`、`DD190TheGodfather.cs`，以及 `Global/DeepDungeon/AutoClear.cs` 的机制分支、`BossModule/AIHintsBuilder.cs` 和真实 `MechanicWorldFeed` 输入。许可沿用 `ThirdPartyNotices/BossModReborn.LICENSE`。普通读条由 `PalaceTrashSpell` 按源 `GuessShape` 读取本机Action表，保留十字、固定内孔3及初始零长冲锋，避免沿用通用推断器的不同参数和VFX25过滤。

状态、危险几何、目标和计时全部由 MP 持有，不调用 BMR 机制会话。上述五个区域退出旧模块宿主；脚本运行时压制重复通用读条，脚本关闭后通用躲避可恢复。脚本的“道中范围与视线”“Boss机制”“战斗控制”可以分别控制。智能走位总开关继续门控移动、选敌、打断和朝向；绘制不受移动开关影响。

## 迁移清单

| 部分 | MP 行为 |
|---|---|
| 道中视线 | Zone的6351 R100、6356 R8.2/90°、6953 R30动态跟随施法Actor姿态；公共VFX25另锁起手Target Actor引用，动态跟随其位置，缺目标才取当前raw Loc/rotation。两路联合PR面向，在命中前0.75秒控制；眼睛/瞳孔/视角弧共用MP双视图状态，不画成移动避让圆 |
| 道中打断 | 6386/6388/6397/7049/7014/7029/7058；坦克7538和物远7551，检查等级、冷却、距离和可打断状态 |
| 道中反伤 | 197/198仅停自动攻击10秒，真实刷新重计，移除立即撤销；连续快照不重新武装，不扩大为普通动作禁攻 |
| 道中其他 | 已揭示陷阱R2；6365的HintDisabled只针对source级冲锋，不删除Source=0的公共普通圆，故仍按真实Action表输出。普通推断只处理Enemy/Helper且非Ally，排除27503/33626及普通type2/5的range≥30；不按技能名字补机制 |
| 160 | 无读条R11.92/90°顺劈、7155落点R9地面（效果后1.3秒）、7090分身R7、7156全宽6动态点名冲锋与25码径向击退，碰墙停止并排除危险落点 |
| 170 | 7158 R8浸泡及效果后0.8秒持续水圈、7161锁定R8落点、R15.75/90°顺劈；当前仇恨玩家在Boss泡水时获得拉出水圈目标 |
| 180 | 7163 R6漩涡、龙卷R10持续区、7162 R16.6/120°、7165 R51.6/60°；阶段/十步循环/陨石提示；15%–15.8%严格开区间禁攻Boss，保留治疗和自保 |
| 190 | 7169 R8、R11.75/120°顺劈、7170 R7.2；灰炸弹距Boss≤7.2禁攻；小炸弹出生10秒R6.6预告并由实际读条/效果接管；Remedy和特大爆炸提示 |

非当前玩家点名的顺劈是危险；当前玩家作为仇恨目标时顺劈仅预览，同时禁止将顺劈朝向队友。Boss场地是BMR原始R24.256的64边形并扣去入口，中心(-300,-300)，无额外场地收缩。用户危险余量仅在解算层应用一次。

普通层仅保留墙体、隐藏/已揭示陷阱、AOE、视线、打断和反伤停手等机制保护。开箱与宝藏接近、探索房间、转移石塔导航、清层选敌、满仓魔陶器使用及其设置和房间导航入口已永久移除；不依赖FSD会话状态，结束会话也不会恢复。`PalaceFloorHazards`只发布障碍和陷阱危险，不发布普通层目标。历史配置中的旧开关不再读取，不触发动作。手动忽略错误历史陷阱仍供避险使用。没有推测灰炸弹搬运或额外强制选中Remedy。

FSD负责普通楼层任务。可选跟随在FSD会话内暂停，会话结束按原跟随设置恢复。会话状态改变先停止MP旧路线并释放交接，重复同值请求不重置正在执行的机制路线。

控制权释放继续使用`GoalWindow.RequiresMovementHandoff`区分机制站位与可选目标；正优先级本身不代表必须占用FSD。死宫普通层不再产生业务目标；机制站位默认保留`RequiresMovementHandoff=true`，危险等待及相关伤害窗口的占用规则保持不变。

2026-11-25职责收敛验证：主构建通过；普通层及机制回归19项、控制权交接回归24项通过。实际DLL回放验证旧设置无效、会话内外均无普通层目标、旧执行类不再打包，并覆盖161条ARR读条和四个Boss输出。独立`tests/PalaceOfTheDead`模型测试为4项通过、9项失败；使用改前源码和测试在隔离项目中复跑得到相同9项失败，不计为本次通过。部署文件已校验不代表游戏内加载或通关。

## 时钟、方向和生命周期

- 普通扇形、矩形和落点读取当前raw cast `loc/rotation`。普通charge每帧使用当前量化caster→raw Loc重算长度和方向；即使起手距离为零也保留描述，不因通用推断器的0.5码阈值永久丢弃。Zone凝视使用当前Actor.Position/Rotation，公共凝视独立使用起手目标引用；目标销毁冻结旧对象，同ID新代不换绑。
- 道中判定钟按源逐帧float累计Elapsed，封顶 `TotalTime + Action.ExtraCastTime100ms * .1f`，输出 `now + ((TotalTime + .3f) - Elapsed)`，保留可能为负的剩余钟。实际raw为空才清理，预测到期、Effect或单纯死亡均不代替实际结束。Boss原有窗口与结束逻辑独立；早期统一±0.2秒记录不再用于道中生命周期验收。
- 7155/7158先生成有限预测，实际地面对象出现后永久交接；`EventState=7`、死亡、销毁移除持续地面。预测8秒超时仅用于缺对象的异常清理，不能用来结束已出现的真实地面。
- 取消/打断按来源和技能匹配，旧技能取消不删除替换后的新读条。已经观察到的道中raw非空时，Cancel/Interrupt日志不伪造Finish；packet-only暂存项仍可撤销。真实raw结束后保留已结束动作/起手水位，迟到同代packet不能复活输出。该build的category15真实动作在Param3；兼容PR旧捕获把Param1暴露成ActionId的情况。
- Zone凝视、Interrupts和HintDisabled保存施法Actor原引用，连续非空CastInfo替换后保留列表直到实际空值；公共AOE/gaze每次真实替换分别Finish→Start。GameObjectId及ObjectIndex已知且变化时清旧代；旧录像缺字段或被采样彻底压平的重建不能凭空恢复。
- 反伤以60个真实状态槽逐槽比较ID/source/param/expiry；expiry增量≤1秒仅更新，不触发Gain，真实刷新按处理钟重新加10秒。Lose197或198清该Actor全部反伤，即使另一状态仍在。packet回执不能屏蔽其后的真实raw刷新。旧录像无SlotIndex只能按压缩序还原，不能称恢复真实槽。
- 公共读条用 `Kind=2/SubKind=5或11` 且原生 `IsEnemy=true` 判定，不能以独立的 Hostile 标志替代。VFX25 在 IsSpell 检查之前；普通 AOE 要求真实 `Cast.IsSpell=true`。旧录像缺少原生分类或类型时该分支输入不足，不能据Action表补猜；161条录制参数验证中这两个字段是明确合成的输入。
- 普通charge端点重合时，源 `SDRect(from,to)` 的归一方向和Distance为NaN，没有可迁的有限导航合同。MP保留raw cast和初始零长描述，此时不向几何构造器提交零长度；端点分开后恢复真实矩形。不得把该边界称为已验证的安全落点。
- Zone gaze、Interrupts、HintDisabled、Spikes输出只在实际非Boss楼层且无活动Boss phase时消费；Boss楼层未开战时仍允许独立公共AOE/gaze。Zone列表按源 `IterAndExpire` 在消费窗口清除raw为空项；门控期间null→另一非空cast可以保留旧列表，不能提前按事件或时间清空。未知楼层的旧录像仍不能证明实际Boss楼层门控已还原。
- 原生状态剩余零值通过 `ReplayStatus.ZeroRemainingTime` 保真，在反伤槽差分中投影到采样时钟；通用录制 `ExpiresAt` 的零→MaxValue兼容语义保留。正剩余→零不再伪造大幅刷新；真实比较仍保留源增幅大于1秒规则，不额外屏蔽稀疏输入中的真实Gain。DungeonScript对原始StatusSlots消费者同样执行此投影。
- 换区、换层系统日志7248、停止重置；切换观看角色只清个人输出。回放消费录制时钟和世界，不访问实时对象、朝向、选敌、打断或移动。
- 战斗控制使用PR公开 `Hosting.MechanicControl` owner请求，每100ms刷新（宿主500ms过期）。禁攻先通过已核对的FFCS `UIState.WeaponState.AutoAttackState.Get/Set(false)`停止当前禁攻目标的普攻，再撤销目标和已排队动作；不主动重新开启普攻。持续普攻停止及恢复需实机验收。宿主缺选敌/禁攻接口会提示，不伪称停手已完整执行。

控制覆盖须分开解释：旧MP导航桥没有消费 `PotentialTargets.Priority/Spikes/ShouldBeInterrupted`，也没有RSR禁读条实例。当前Spikes停普攻、可打断职业动作及凝视需要转向时的禁读条/取消是本原生脚本已有的执行接入，不能写成旧桥已验的执行；安全背对时不阻止无关读条。源码 `PalaceFloorModule.CalculateExtraHints` 的0x1842 priority10以及 `AutoClear` 437/1097状态目标修正保留为源码意图边界，不恢复清层选敌或新增强制目标。

已确认宿主限制：本机PR `Hosting/MechanicControl.ApplyTarget` 在存在任何ForbiddenTargets时返回true，`TargetSelectorService`因此跳过普通选敌。灰炸弹精确禁攻本身只提交该目标，但会附带暂停宿主重新评估其他合法目标。`ScriptHintsApi`同样走该接口，不能通过换门面消除此限制；MP不以全停手、禁读条或虚构替代目标补偿，也不擅改PR。190层这一执行分支仍待宿主提供不扩大选敌接管的能力及实机证据，已有合成绘制/禁攻目标数组验证不能证明完整执行等价。

## 五份ARR证据

源目录 `C:/Users/xiaos/Downloads`，全部build **14251465**。`tests/Fixtures/PalaceOfTheDead/recordings.json` 保留匿名实体编号，不保留玩家姓名/真实实体ID。生成器 `tools/arr/potd_fixture.py` 保留NPC和事件对象创建/销毁、读条、效果、死亡、取消、反伤和对象状态；移动只通过读条/效果前的最新来源位置快照保留，不是完整运动录像。对象坐标偏移使用该build实际字节的 **0x34**，不能照抄本机PR旧结构的0x30。

| 文件（2026-11-19） | NPC读条 | 范围输出 | 特殊响应 | 单体/大范围跳过 | SHA256 |
|---|---:|---:|---:|---:|---|
| 地下151～160层 21.05.44 | 15 | 11 | 2 | 2 | a95245ed7bf7414a7eb65fa12c96b762197fa29ee3f13b8171d383e733047e87 |
| 地下161～170层 21.23.45 | 5 | 4 | 1 | 0 | ce60ee02eedf4f8256c2d6c5625635f94b719e428a0bc7791fc304ec2bdf2d1a |
| 地下171～180层 21.44.50 | 99 | 73 | 2 | 24 | f9e0fa688ad5d9291e17092eedc70436bd1938d2ebe230cdc1a0989a30c97c03 |
| 地下181～190层 22.10.11 | 7 | 4 | 1 | 2 | 8b801cc8a2960e2a6cbb8988033fcd576f83b95ed9f17bd22bd4812d4c79e2db |
| 地下191～200层 22.35.37 | 35 | 32 | 0 | 3 | 2b53ab86ee1c5a5f18e18369927bcb6ec8f7677bccdb48f152a5e8f0fce4ec4c |

合计161条：124次固定范围输出、6次视线/打断/冲锋响应、31次单体或通用R≥30排除。生产回放逐cast测试把160冲锋加为个人落点约束，故导航输出合计125。ARR NPC创建包没有可靠hitbox尺寸，匿名录像复核使用0；Boss的显式尺寸与合成测试独立覆盖，不能把道中加hitbox后的边界声称为录像精确测量。

2026-11-25输入补验：上述161条测试现在明确使用「真实录制参数 + 合成raw帧」：Actor.Rotation取录制sourceRot，Cast.Rotation取rotation，位置/落点/时长保持录制值；补齐Enemy对象类型，以测试raw开始、普通Cancel日志保留、Boss独立取消及实际raw空值清理。它不是原始完整CastInfo运动录像，也不证明真实网络先后顺序。独立VFX25合成回放用真实本机6356形状验证caster范围外的目标凝视、目标移动/销毁/同ID重建、raw替换和反向Seek。

### Boss实录覆盖缺口

- **160**：1215.408/1219.906录到7179，1215.810录到7154，1223.505录到7155；7156在1231.221开始，1232.926取消后Boss死亡，没有冲锋结算。1232.048六个分身开始7090，但没有效果。愤慨殿真实对象在1224.822出现。
- **170**：1052.320录到7160、1054.712录到6497；没有7158/7161读条与水圈拉怪覆盖。
- **180**：仅1178.676录到6497；没有7162–7167或龙卷覆盖。
- **190**：没有Boss机制cast/effect，也没有三种炸弹覆盖。
- **200**：录像与BMR均无本次四Boss之一；仅复核道中。

四场可见玩家6274技能，BMR把它列为魅魔变身动作。不能把未出现的机制归因为未证实的速杀方式。

7079/7082在本机Action数据是CastType2、R30，BMR该区域无专用月环组件，按通用规则排除。当前没有可靠内圈证据，未编造月环；这两项仍需单独补充机制证据，不能称为已验证的精确躲避。录像未覆盖的6351/6356、大部分打断/反伤、Boss水圈/龙卷/炸弹/完整击退分支以源码和合成测试验证，不等同实机通过。

## 可重复验证

正常具有仓库要求SDK的环境，在根目录执行：

```bash
rtk proxy python tools/arr/potd_fixture.py --recordings C:/Users/xiaos/Downloads --actions tests/Fixtures/PalaceOfTheDead/recordings.json --out tests/Fixtures/PalaceOfTheDead/recordings.json
rtk proxy dotnet build MasterPlayer.csproj -p:CI=true
rtk proxy dotnet run --project tests/PalaceOfTheDead/MasterPlayer.PalaceOfTheDead.Tests.csproj
PALACE_GAME_DATA='C:/Games/最终幻想XIV/game/sqpack' rtk proxy dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- . bin/Debug/MasterPlayer.dll --palace
```

本次机器缺少根 `global.json` 要求的11.0.100，使用忽略目录 `analysis/potd-work/global.json` 固定已安装10.0.303，从该目录运行相应 `../../` 项目路径。根SDK配置未改。CI构建输出到 `bin/Debug`，未部署实际插件目录。

本次结果：主构建0错误（4项原有Crucible警告）；定向回归 **13组通过、0失败**；生产入口独立编译、发现与默认区域启用通过；真实回放暂停/Seek/取消/边界清理通过；161条录制cast经实际生产DLL与本机Lumina输出125项导航结果，0路由异常、0实时对象读取、0移动调用。

真实SafeZoneSolver合成绕行：R9持续水圈、原场地，从中心左16码去右16码，严格`Success`、11路点、38.496码，786个0.05码采样点全部在场内且不进水圈，最近圆心9.791码。它证明一条真实规划路径的安全，不代表全录像闭环模拟或实机无伤。实机仍需验收反伤停普攻、打断执行、PR选敌/治疗门控、换层和完整Boss时间轴。

2026-11-25续作证据：`.tmp/halatali-verify/native-palace-generic-20261125-model-02.log` 88项相关模型PASS/0FAIL，其中八类普通形状对真实源AOEShape.Check共52,488点；`build-02.log`生产0错误/4项既有Crucible警告，`replay-02.log`同DLL18条专项PASS/0FAIL，包含161条录制参数、125导航输出及五Zone/四Boss/禁止流程回归。该版本早于随后补充的结束水位及零剩余状态元数据，后续验证单独登记。五个混合Zone仍为needs review；不能以本批已通过的分支代替整Zone闭环，也不恢复任何流程自动化。

2026-11-25后续边界修正的最终证据使用唯一前缀 `native-palace-boundaries-20261125-`，均在 `.tmp/halatali-verify/`：`model-03.log` 106项模型PASS/0FAIL；`build-02.log`生产0错误、4项既有Crucible警告；同一DLL的 `replay-rerun-03.log` 18条Palace专项、`native-01.log` 32条Eureka专项、`catalog-01.log` 949条全目录检查均PASS/0FAIL，相关进程退出码均为0。DLL SHA256为 `4b6e6510de04b4ffc265f78e839497b139698c5ef334bd3aab770d343ab7d33d`，另保存在 `artifact.log`。这一轮包含最终的真实Boss楼层/仅消费时过期、原生Enemy及IsSpell、零剩余状态、迟旧packet结束水位、零长charge、同点凝视颜色和安全背对不禁无关读条修正；全目录另验证Kefka真实击退导航的状态零值/刷新/逆向Seek。旧失败日志保留，修正过的夹具分别记录真实状态槽、连续零样本和完整DeepDungeon帧，未以吞异常或降级通过。此处只证明本机可执行的模型、公共双视图数据、当前导航与隔离；没有新增真实物理、宿主执行或最终像素验收。

2026-11-25全20个Zone授权机制闭环：新增`Palace1To150`入口覆盖561–565、593–602（CFC174–178、204–213），明确`IncludesBossMechanics=false`；603–607（CFC214–218）仍由原入口覆盖。早期独立Boss处理自己的Cast/效果/永久地面，普通Zone遇Boss packet先即时Render清除旧墙体与输出再拒绝，不能沿用预先采样的普通层预览。`BossMechanicsEnabled=false`只排除晚期Boss分支，普通raw、陷阱、Spikes和凝视仍消费；共享旋风OID18F0及60/160、70/170地面不会在战前被早期Zone重复接管。实时普通扫描为100ms，事件强制扫描；不是每Framework帧采样。地图生成器对照1000房间/6556历史陷阱，前15区750/5111、后五区250/1445，无探索或交互目标。

该闭环最终证据在`.tmp/halatali-verify/`，唯一前缀`native-palace-earlyzones-20261125-`：`model-03.log`77项模型PASS/0FAIL、`build-04.log`生产0错误/4项既有Crucible警告，**同一DLL**的`related-03.log`29条相关检查、`palace-02.log`18条Palace检查、`catalog-03.log`956条全目录检查均PASS/0FAIL；相应任务退出码均0。DLL SHA256为`b0b5b67f8d2cf337bea6b3a0a8417dc51774ad7e06afae20ca10501670218583`，对应构建后文件时间18:33:34 +08:00。其中全目录首轮Stage28夹具依赖Dictionary枚举首项导致失败；仅改为按`/display/3`、`/display/4`身份各自检查原位置，保留死亡/不可选输入及两危险/两预览断言，单跑和集中复跑通过，未改该机制生产行为。

模型、公共双视图数据、当前墙/陷阱/AOE/站位导航、实际SafeZoneSolver、切人/反向Seek/owner清理和回放禁执行已离线验证。161条录制参数/125导航仍是录制参数加合成raw输入，不冒充原始完整运动回放。20Zone的授权机制登记已验；DD190仅灰炸弹禁攻的宿主限制单独重开review，不能拿本批PASS关闭该执行缺口。真实反伤普攻停止/打断/PR面向与ACR、物理移动及最终雷达/世界像素仍待实机。流程自动化禁止范围、源未提供7079/7082内孔及未知旧录像物理槽等边界不变。
