# 天青斗场第18、19场原生机制

## 来源、模型与接入

对照仓库 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage18MidsummerNightsExplosion/`、`Stage19OnAClearDayYouCanSmellForever/` 两幕，以及 `Components/BaitAway.cs`、`Voidzone.cs`、`Gaze.cs`、`Data/ActorState.cs` 和组件激活补播实现。生产使用 `Presets/MaskedCarnivale/`，沿用 `Entries/Stage01.cs → DungeonEncounter → DungeonOutput → SafeZone/Draw`，不消费桥输出。

| 场次 | CFC / InstanceContent / Territory | 第一幕主OID / NameID | 第二幕主OID / NameID | 场地 |
|---|---|---|---|---|
| 爆破死斗 | 628 / 35018 / 796 | 2724 / 8116 | 2725 / 8116 | 中心(100,100)，64边R24.5 |
| 魅惑芳香——反射魔花瑞贝卡 | 629 / 35019 / 796 | 2727 / 8117 | 2728 / 8117 | 中心(100,100)，32边R16.01379 |

共4个模块、27个组件实例类型。第18场每幕构造激活攻略和桶范围，入战激活5个战斗组件；第19场第一幕构造攻略加5个战斗组件，第二幕多一个失明提示组件。攻略在拉战时撤销，不生成自动青魔动作。

机制记录保留原始完整cast、来源/目标演员引用、代际、开始/判定时刻；毒池事件保存source、MainTarget、action、sequence、事件钟、消费钟和锁点。几何、双视图及现有导航经公共输出适配，不引入栅格或搜索器依赖。

## 第18场酒桶与动态冲锋

两幕任意本幕Boss或2726酒桶InCombat拉战，全部本幕Boss及酒桶DeadOrDestroyed结束；HP0不替代Dead。第一幕画固定Primary和全部酒桶，第二幕画全部本幕Boss和酒桶；桶橙色。选敌Boss=1、其他=0，保留Pointless保护。

| AID | 源组件与锁定 |
|---|---|
| 15054 | Explosion，R10普通危险圆 |
| 15051 | Fireball，R6普通危险圆 |
| 15050 | RipperClaw，R8总90°锥 |
| 15052 | TailSmash，R15总90°锥 |
| 15055 | WildCharge，原引用动态bait，半宽4 |

15053 BoneShaker仅枚举；无Raidwide预测或自动护盾。

构造期 `KegExplosion` 与入战才激活的 `WildCharge` 各自维护独立桶缓存。Created加入、Death按引用删除；Destroy不删除，因此冻结旧引用。复活不重新入表；同ID新演员以新引用加入，后续新演员死亡不删除旧冻结项。入战前已毁桶可留在构造缓存，但不能被新激活的冲锋缓存补播。

构造缓存投影R10默认Danger轮廓，提供 `In keg explosion radius!`，仅显示；不登记导航。真实15054是独立普通危险区。15055提示覆写基础AddHints：原始bait列表和冲锋自己的桶列表都非空时，对所有观看角色提示 `Aim charge at a keg!`；source死而bait尚未结束时也保留。没有基础bait文字，没有自动朝桶站位目标。

冲锋复用 `CarnivaleChargeCast` 和 `DungeonCarnivaleCharge`：动态source位置、两原引用间XZ长度和朝向；source==target引用时回退source方向。目标销毁冻结、同ID不换绑。本人显示按ID为轮廓，导航按Actor引用识别：本人诱导避开存活Raid队友方向，别人避完整矩形。保留继承Tankbuster预测，时间取完整列表首项。15055不写击退模型。

Start目标缺失及首raw确认沿用16场规则，真实Finish/取消/source销毁按caster删首记录；Effect或到期不删。补播按持久对象创建顺序，不能被当帧快照反序改变首个预测钟。

## 第19场反射与全向视线的实际源行为

两幕按固定Primary可选中且InCombat拉战，Primary死亡、销毁或HP0结束；不等2779 HotHip。默认只画固定Primary，无额外敌人优先级。两幕同AID共享输入，但保留逐模块状态和独立激活钟。

| AID / SID | 源组件与实际行为 |
|---|---|
| 15073 | Reflect；任意caster Start置casting，任意匹配Finish置reflect并清casting |
| 15074 | BadBreath，R17.775总120°锥 |
| 15075 | VineProbe，前11.775、半宽4矩形 |
| 15076 | OffalBreath打断意图，另有三路R6毒池 |
| 15077 | 第二幕Schizocarps Start打开Ink Jet提示，Finish不关 |
| 15078 | 第二幕ExplosiveDehiscence任意caster Finish关闭提示，Start/Effect不关 |
| 15 | Blind Gain/Lose按事件时队伍槽改写mask，控制Ink Jet文字 |

Reflect提示原文：施法时 `Boss will reflect all magic damage!`，之后 `Boss reflects all magic damage!`。这是两个组件级bool，不计caster数、不读反射状态、不定时消失；取消、raw读条消失及销毁的Finish同样锁存。再次Start临时进入casting分支。源禁魔法仅TODO，迁移不新增PR停手或禁攻，也不根据注释中的melee误禁近战。完整最后cast保留在模块模型中。

第二幕虽继承CastGaze，但覆写Start/Finish且不调用base，Eyes实际始终为空。因此15078不接公共Gazes，不生成眼睛、背对提示、方向禁区或PR转向。唯一输出是未Blind且casting时的 `Cast Ink Jet on boss to get blinded!`。15077取消/预计到期仍开；Blind Gain关、Lose恢复；15078 Effect仍开，实际Finish关。中途只见15078不补造15077历史。

`CarnivaleBlindness` 保存原生状态槽基线和独立上一帧队伍表。激活补播用当帧名单；已激活组件按源Feed的“Destroy → Actor/status差分 → roster提交”顺序，Gain/Lose用旧名单。名单变更不迁移mask，未知槽无效。名单仍保留但世界Actor缺席不丢身份；销毁清状态基线但不删名单身份，同ID重建仍可Gain。死亡不额外清位。状态按原生槽序差分，ID/source替换先Lose再Gain；同ID/source/param且expiry差≤1只更新基线，不发Gain。Blind不按expiry自行Lose，也不是多个Blind槽的引用计数，一个Lose即可清bit。

新模块在本帧raw更新后才视为组件激活：不得接收其激活前的Reflect Finish。后台状态在前台完成后即时接替，切人只切个人输出；Seek从事件重建。

## 15076三路毒池与激活时钟

复用17场的 `CarnivaleVoidzoneCast`、`CarnivaleVoidzonePrediction` 及公共输出：

1. cast记录保存原caster引用、完整cast和代际；位置动态取当前CastInfo目标Actor，缺失取Loc；判定钟在该组件Start锁定。Finish只删该source首cast。
2. 真实NativeActionEffect按MainTarget当前位置锁点，缺失才用TargetPosition；Targets为空仍可用。跳过legacy逐目标展开，每个真实包独立，零sequence不同事件不合并。Activation为消费时刻加delay：第一幕 **(double)1.6f**，第二幕 **1.6d**。
3. OID1EA9F9实体仅筛EventState!=7，不筛Dead/Targetable；位置量化。每Update按持久创建顺序，每个有效实体移除首条XZ距离≤2的事件预测。无预测超时，不因取消删除事件预测。

三路都是R6危险导航和双视图，范围文字 `GTFO from voidzone!`，不是硬障碍。15074注释提到打断/毒池不构成组件，只有15076发布打断意图，保持已有消费范围。

持续raw只时钟漂移不制造Finish/Start，现存组件保留原锁钟。晚激活组件独立读取激活时的源剩余钟：本机Action15048/15076/14873/15055的ExtraCastTime100ms均为0，源Elapsed封顶TotalTime后仍保留NPCFinishDelay=0.3f；因此补播钟下限为激活now+0.3f。仅新组件补播应用，不能刷新旧组件。`CarnivaleActivationCast`集中处理这些实际消费者。

## 显示、导航、执行及证据边界

普通圆/扇/矩形、动态冲锋和毒池通过生产公共输出发布两视图及导航。桶构造轮廓仅显示；瞄桶攻略没有导航。第19场反射与失明只文字，无转身、动作限制或额外执行。源未提供的自动超硬化、Ink Jet、眩晕/打断执行均未扩大。回放禁止原生操作。

定向覆盖两套桶缓存、Destroy/Death/复活/同ID、source死仍Aim、首raw/真实结束、持久创建序、两幕全普通组件、原始事件字段及双幕精确delay、Dead实体state7、事件锁点和2码接替、反射锁存与新激活边界、Blind原生槽/名单时序/缺席身份、后台交接、owner、切人、Seek。18每幕5929点、19每幕5236点场地与源oracle比对。最终96项斗场集中模型、生产构建0错误/4条既有Crucible警告、实际DLL专项20条PASS、全目录716条PASS/0失败。证据位于 `.tmp/halatali-verify/native-carnivale-kegs-rebekkah-model-final.log`、`native-carnivale-kegs-rebekkah-build-final.log`、`native-carnivale-kegs-rebekkah-focused-final.log`、`native-carnivale-kegs-rebekkah-catalog-final.log`。四模块仅登记离线已验，实机仍待验。

离线显示发布及几何对照不代替游戏内最终像素。源提供的继承冲锋导航已覆盖；真实解算闭环、PR执行与录像校准仍需实机。本批没有新增统一寻路模型、原生操作或桥清退。
