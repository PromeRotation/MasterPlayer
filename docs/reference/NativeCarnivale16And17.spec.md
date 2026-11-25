# 天青斗场第16、17场原生机制

## 来源与模块范围

对照本仓库 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage16SunsetBullevard/`、`Stage17TheSwordOfMusic/` 的两幕实现及当前 `Components/BaitAway.cs`、`Knockback.cs`、`Voidzone.cs`、`Data/ActorState.cs`、`Data/PartyState.cs`。生产实现位于 `Presets/MaskedCarnivale/`，入口仍为 `Entries/Stage01.cs`，不消费 BMR 桥输出。

本机简体中文数据映射：

| 场次 | ContentFinderCondition / InstanceContent | Territory | 第一幕主OID / NameID | 第二幕主OID / NameID |
|---|---|---|---|---|
| 怪力兽王——迪克巴朗 | 626 / 35016 | 796 | 26F2 / 8112 | 26F4 / 8113 |
| 刚柔铁巨人——克利俄斯改 | 627 / 35017 | 796 | 2720 / 8115 | 2721 / 8087 |

第16场共9个组件类型，第17场共11个组件类型。第一幕16只有构造攻略提示，无战斗AOE；17第一幕有Shred、TheHand及反伤文字。16第一幕使用64边R24.5大场，其余三幕使用32边R16.01379小场，中心均为(100,100)。准备文字在拉战后撤销。

## 阶段、敌人和反伤

16第一幕任意26F2入战，全部同OID DeadOrDestroyed结束；17第一幕任意2720/271F入战，全部两类手DeadOrDestroyed结束。两者不以HP0代替Dead，也不要求拉战演员可选中。两场第二幕按Primary可选中且入战拉战，Primary死、毁或HP0结束，不等小怪。模块输入独立于选中幕；后台模块保留机制，前台完成后即时接替。缺观看角色时继续消费对象和起止，仅隐藏个人输出。

16第二幕Cyclops26F3橙色、priority1，其余priority0；17第二幕左右爪2722/2723橙色、priority1，其余0。保留Pointless目标保护。

17第一幕反魔法文字看固定Primary是否Dead；反物理只看第一只271F，首只死亡而后面仍有存活者也不提示。第二幕分别找第一只存活2722/2723。源选敌代码中的物理/魔法过滤未实现；源攻略明确允许承受反伤。迁移保留文字，不据此新增PR停手或动作类型限制。

## 第16场普通机制与凝视

| AID | 已激活源行为 |
|---|---|
| 14686 | RaidwideCast，“Use Diamondback!”；伤害预测使用当前raw结束钟，不制造24码危险圈或自动超硬化 |
| 14872 | R12圆；源未激活其注释中的击退 |
| 14871 / 14876 | 共享R44、总60°锥；任意action Finish按caster删除该组首项 |
| 14875 | 默认range10000的CastGaze；锁Loc，眼、瞳孔、本人90°视线弧及PR面向门面 |
| 14873 | 动态冲锋及独立单槽击退，详见下一节 |
| 15268 | 10–20环 |

6497/14874/14779/14685仅枚举，不新增50码拉近、召唤预测或自动技能。

凝视使用既有 `DungeonEddaMechanics → DungeonOutput → Draw/SafeZone` 显示接点和 `GazeFacing → Hints.SetFacing`。最后0.75秒提供安全面向；回放保留几何和建议，宿主阻止实际PR面向执行。普通AOE锁定来源位置/方向并保留真实结束语义，Effect或预计到期不会代替Finish。

## 14873的两套独立模型

`CarnivaleChargeCast` 保存代际、完整施法记录及来源/目标 `AlphaActorState` 引用。Start时找不到目标则不建立bait；首raw确认可纠正packet阶段尚未解析的目标，但首raw本身缺目标后不会因演员后来出现而补绑。

原点、前长、方向每帧分别取source位置、XZ两者距离及source→target；引用相同才用source朝向。总宽8。目标死亡保留；目标销毁冻结旧引用，相同ID新演员不换绑。source死亡隐藏，source销毁触发Finish。相同AID Finish/取消/销毁按source删除首项；Effect及预计到期不清。

显示和文字按ID识别本人，导航按Actor引用识别本人。因此旧目标销毁、同ID新演员成为视角时，仍显示本人轮廓，但导航避让冻结矩形。本人真正持有目标引用时，按存活Raid0–63槽成员生成R100、半角asin(4/distance)的方向禁区；其他人避让动态矩形。NPC登记复用现有 `ObserveJobQuestParty`，不凭Buddy标志猜测。伤害预测按队伍身份表查槽，保留dead或暂缺Actor的目标；时间取完整原bait列表首项，不取首个活跃项。

`CarnivaleChargeKnockback` 独立保存触发代际、完整cast及20码规则；任意新Start覆盖，任意14873 Finish清槽，即使另一source的bait仍在。原点取固定Primary引用的当前Position；AwayFromOrigin、不截墙、不检测其他AOE落点、重合不位移。模型保留规则，当前只经 `CarnivaleChargeLanding → QuestKnockbackProjection` 输出投影线、角色和场地越界文字；没有击退导航或自动执行。

`CarnivaleKnockbackImmunity` 对每个Raid槽保存Role(160/1209/3054)、Job(1722/2663)、Duty(2345)三类覆盖钟，Gain覆盖该类、Lose清该类，严格expiry>activation。实时输入保留原生状态槽顺序，0剩余时间按当前时刻处理；MP回放沿用记录的SlotIndex/ExpiresAt。按源Feed在槽内先Lose再Gain，保留expiry差≤1不发Gain的单向比较。换队伍名单本身不制造StatusLose。免疫或重合没有投影，也不因本人已在场外额外报告击退危险。

连续非空raw仅起止钟漂移不制造新代；真实Action/Target/Loc/Rotation/TotalTime变化先Finish旧代再Start。观察到null清raw基线，使同参数的下次施法能重新建立。旧Finish仍受代际事件边界限制。

## 第17场魔导激光三路时序

普通组件：14759前长6、半宽2矩形；14760 R8总120°锥；15047前长77.5、半宽2矩形。15049只提供打断意图和文字。注释中的TheHand击退、Shred眩晕及防御状态不扩展为未激活机制。

15048使用 `CarnivaleVoidzoneCast`、`CarnivaleVoidzonePrediction` 和真实地板实体三路模型：

1. 读条保存来源引用、完整cast、代际及结束钟；位置每次读来源当前CastInfo的TargetID，可解析则跟随目标，否则Loc。Finish/取消/销毁只清该source首条cast预测。
2. 真实 `NativeActionEffectEvent` 以MainTarget当前世界位置锁点，缺目标回退TargetPosition。保存source/mainTarget/action/sequence、事件钟、消费钟、activation及锁点，activation为消费钟加 `(double)1.1f`；不因取消、预计到期或后续目标移动清除。
3. OID1E8D9B实体只筛EventState!=7，不筛Dead；位置按源Quantized量化。每个有效实体每次Update最多消费首个距离≤2的事件预测，不能一次清空所有重合事件。

三路均R6、Risky危险区及双视图，文字“GTFO from voidzone!”。真实地板是危险区，不冒充障碍。原始身份和时钟保留在模型，几何在 `DungeonCarnivaleClaws → DungeonOutput → SafeZone/Draw` 适配。

`DungeonEventAdapter` 将15048真实原生包单次交给Effect，先采集主目标世界位置，跳过PR展开后的legacy每目标事件，支持Targets为空的真实包。后台第二幕也保留cast和事件预测；只对前台输出。

## 验证与边界

模型定向测试在 `tests/CarnivaleSunsetRegression.cs`、`CarnivaleClawsRegression.cs`；实际DLL回放在 `tests/ScriptCompilation/NativeDungeonReplayCarnivaleSunset.cs`、`NativeDungeonReplayCarnivaleClaws.cs`，纳入AlphaCarnivale集中回归。

覆盖阶段、反伤文字作用域、目标身份及引用代际、动态几何、独立KB单槽、三类免疫及状态槽顺序、连续raw时间漂移/null后再施法、任意组Finish、迟到事件、真实主目标包/展开事件去重、毒区三路交接、后台激活和切换、反向Seek、owner撤销及回放执行隔离。

源几何对照：16每幕5929个场地点，17每幕5236个场地点，16冲锋矩形7636个点；普通AOE尺寸及导航/显示分离通过生产输出核对。GPU像素、实际PR面向及GreenMove在游戏内的执行仍待实机；离线结果不声明这些已经验收。两场均未新增自动青魔技能、反伤停手、击退寻路或统一寻路框架。

最终83项斗场集中模型通过；生产构建0错误、4条既有Crucible警告；实际DLL专项19条PASS，全目录698条PASS/0失败。证据在 `.tmp/halatali-verify/native-carnivale-sunset-claws-model-final.log`、`native-carnivale-sunset-claws-build-final.log`、`native-carnivale-sunset-claws-focused-final.log`、`native-carnivale-sunset-claws-catalog-final.log`。四个模块按生成审计及一致性检查登记为离线已验，不代表实机验收完成。
