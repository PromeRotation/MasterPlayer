# 天青斗场第13场 MP 原生规格

## 来源与身份

来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage13BeautyAndABeast/Stage13Act1.cs`、`Stage13Act2.cs`。两幕共11个实际组件：第一幕构造Hints和Mow；第二幕构造Hints和8个战斗组件。两幕分别监听1和9个Action，枚举不作为激活依据。

本机简体中文sqpack及匹配Lumina核实：`CFC623「猩红死妖姬——卡蜜拉」→InstanceContent35013→Territory796`，ContentType27、InstanceContentType13。第一幕主OID26F5、源NameID8104「斗场哈加提」，第二幕主OID26F8、NameID8107「卡蜜拉」。共享区域的反向CFC610不用于识别场次。

入口沿用 `masterplayer.carnivale.stage01`，两幕提供独立机制开关。生产模型使用 `CarnivaleCatalog → DungeonEncounter/DungeonCarnivale`，普通危险区和场地经 `DungeonOutput → SafeZone`；凝视复用公共模型出口、`DungeonEddaMechanics`眼形绘制和`DungeonScript.Render → Hints.SetFacing`。模型不消费BMR运行输出、不依赖求解器节点或调度器。

## 分幕合同

| 模块 | 构造与场地 | 拉战/完成 | 敌人显示与优先级 |
|---|---|---|---|
| Act1，26F5 | 完整第一幕攻略及第二幕Flying Sardine建议；CircleBig，R24.5的64边形 | 任意26F5/26F6进战；两类全部DeadOrDestroyed完成，HP0不替代Dead；开战撤攻略并激活Mow | 固定Primary和全部26F6，默认敌色；没有额外优先级 |
| Act2，26F8 | 含Primary.Name的AOE/召唤/打断攻略；CircleSmall，R16.01379的32边形 | 默认Primary Targetable且InCombat拉战，Primary死亡/销毁/HP0结束，不等待Succubus；开战撤攻略并激活8组件 | 固定Primary红色，全部26F7为Object色RGBA(1,128/255,0,1)；沿用基础选敌，不因攻略提高Succubus优先级 |

| Action | 真实源组件与参数 | 模型及输出 |
|---|---|---|
| 14879 Mow | SimpleAOEs，R7.4、总120°扇 | 第一幕普通危险导航及双视图 |
| 14880 VoidFireII | SimpleAOEs，R5 | 锁CastInfo.Loc的圆 |
| 14888 VoidFireIV、14884 DarkMist | 同一个SimpleAOEGroups，R10、MaxCasts无限、达到99才排序 | 两种动作共享按caster撤销语义；任意动作Finish删除该caster首项，仍保护新代不被旧事件消费 |
| 14889 VoidFireIV3 | SimpleAOEs，R6 | helper落点圆 |
| 14881 VoidAero | SimpleAOEs，前长42、半宽4、后长0 | 锁CastInfo.Loc/Rotation的矩形 |
| 14951 DarkSabbath | CastGaze，构造未传range，真实默认10000；普通背对、MaxCasts无限 | 眼睛锁CastInfo.Loc，原始source、时间及代际保留；无平面危险区 |
| 14887 CircleOfBlood2 | SimpleAOEs，内10外20环 | helper锁点环；不迁未激活的15043 |
| 15045 BeguilingMist | CastInterruptHint默认true,false | 完整caster引用/代际/施法记录，仅ShouldBeInterrupted和Interrupt!；没有AOE、伤害预测或自动Flying Sardine |
| 14882 BloodRain | RaidwideCast | 原文`Harmless raidwide unless you failed to kill succubus in time`；整队存活且存在Actor的成员伤害预测，不按TargetId或Succubus是否存活过滤；不生成R50危险圈 |

15043、14886、14885、14952仅枚举，未激活吸引、恐惧或额外AOE组件，不扩大接管范围。

## 时序、凝视与模块作用域

沿用[04/08第二幕及10场规格](NativeCarnivale04Act2And08Act2And10.spec.md)和[11/12场规格](NativeCarnivale11And12.spec.md)的对象身份、代际、首次raw补正、取消收据及后台模块模型。拉战激活时补播已有施法；预计Finish和ActionEffect不代替真实Finish、取消、打断、销毁。普通AOE锁定施法位置和方向，后续Actor移动/转向不改锁；raw CastInfo真变化先结束旧代再启动新代，迟旧Finish受`FinishEventBoundary`保护。

14951的源注释为range60，但实际`CastGaze`构造使用默认10000。危险提示、ForbiddenDirections的当前面向适配和玩家前90°判定弧受10000范围限制，眼睛及瞳孔本身不受范围限制；眼色用当前观看玩家朝向与眼睛方向点积`.707107f`。完整眼形、瞳孔和视角弧均为仅显示，不伪造导航危险圆。公共面向策略在预计判定末0.75秒消费所保留的眼睛时钟，交给PR面向门面续期；超时但施法未结束仍保留约束。

同AID真实Finish仅删除对应source眼睛，异AID取消不清眼睛；双施法者独立。14888/14884的任意Finish规则只作用于该组，不能推广到14951或其他SimpleAOEs。

后台Started模块继续记录施法，但显示只消费当前选中模块。普通文字出口补上`CarnivaleCastVisible`，避免后台14882泄漏到其他幕；伤害出口仅当前26F8模块可见。选中模块交接不Reset后台施法或取消历史。14882预测优先使用当前匹配raw施法的FinishAt，packet同代补正不替换原始raw结束时钟。

## 验证与边界

`tests/CarnivaleBeautyRegression.cs`包含9组新模型用例，分别验证两种拉战/完成规则、全部7种AOE动作参数及锁点、组AOE任意Finish与迟旧保护、凝视packet→raw锁点与真实换代、120/10001码范围、眼形/瞳孔/弧、观看角色切换、双施法者、打断投影、整队伤害、后台模块交接和raw时钟、生产公共输出及owner清理。

`tests/ScriptCompilation/NativeDungeonReplayCarnivaleBeauty.cs`由实际ScriptManager和ReplayController驱动，验证两个场地各5929点源DLL Contains对照、全部AOE的实际导航登记和双视图预览、凝视完整眼形及PR面向模型、回放禁执行、组AOE及凝视的并发/取消/代际边界、15045意图和14882伤害/文字、后台交接、反向Seek和机制关闭。

证据日志位于`.tmp/halatali-verify/native-carnivale-beauty-*`。最终69项集中模型、44条实际DLL专项、生产构建0错误/4条既有Crucible警告、实际DLL全目录669条PASS/0失败，审计生成与一致性检查通过后登记。显示已验证生产预览与源几何；导航已验证本场实际提供的AOE/场地输出。没有新增特殊移动、护盾、停手、自动打断或施法执行消费者；PR真实面向、宿主加载、像素与GreenMove仍需实机，回放不发送原生操作。
