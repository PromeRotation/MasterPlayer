# 天青斗场第25、26场原生机制

## 身份、来源和公共接入

源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage25DirtyRottenAzulmagia/Stage25Act1.cs`、`Stage25Act2.cs`、`Stage25Act3.cs` 与 `Stage26PapaMia/Stage26Act1.cs`、`Stage26Act2.cs`。同时核对实际基类 GenericAOEs、Voidzone、CastHint、UnavoidableDamage、GenericKnockback，及 ActorState、PartyState、模块补播和Primary重绑。

本机简体中文表双向核对：

| 场次 | CFC → InstanceContent → Territory | 主OID / NameID |
|---|---|---|
| 邪恶青魔法师——启示者 | 635 → 35025 → 796 | 2678、267F、2680 / 8129 |
| 强大的老爹——洪巴巴之父 | 695 → 35026 → 796 | 2C84 / 9230；2C58 / 9231 |

第26场CFC是695；636实际为瓯博讷修道院，不能用关卡号顺推CFC。全部17个实际消费动作的 `ExtraCastTime100ms=0`；第26场18605的Cast100ms也为0，由效果清除图标击退，不启动读条组件。

共五模块、42组件实例、28条逐幕读条规则。两场前幕大场为中心(100,100)、R24.5的64边形；第25场第三幕及第26场第二幕小场为同中心、R16.01379的32边形。均按默认Primary可选且InCombat启动，Primary死亡/销毁/HP0结束，单纯脱战不结束。构造攻略在拉战后撤销，不按小怪存活延长生命周期。

生产沿用 `Entries/Stage01.cs → DungeonEncounter → DungeonOutput → SafeZone/Draw`。普通危险、硬障碍和预览通过公共输出适配；模型不依赖求解器节点。新组件晚激活接当前raw，过期剩余钟保留源NPC延迟 `(double)0.3f`，已知raw的激活前取消保留结束水位。实际raw字段变更按Finish→Start，纯Elapsed重复不重启。

## 第25场普通组件

第一幕九组件、第二幕六组件、第三幕十四组件（包含各幕构造Hints）。规则分别7、4、10，其中第三幕9个普通AOE和1个Web提示开关。

| AID | 幕 | 源几何 |
|---|---|---|
| 14766 ApocalypticBolt | 1 | 前长51.2、半宽45，全宽90 |
| 14766 ApocalypticBolt | 2、3 | 前长51.2、半宽4，全宽8 |
| 14767 ApocalypticRoar | 全部 | R36.2、总120°锥 |
| 14763 TheRamsVoice | 全部 | R8圆 |
| 14764 TheDragonsVoice | 全部 | 6–30环 |
| 14765 Plaincracker | 1、3 | R7.2圆 |
| 14774 TremblingEarth | 1、3 | 10–20环 |
| 14775 TremblingEarth2 | 1、3 | 20–30环 |
| 14773 Charybdis | 3 | R8圆 |
| 14771 Meteor | 3 | R15圆 |

第一幕Bolt的异常宽度按源构造保留，未依注释改成8码。普通组件Start锁落点、朝向和判定钟；同AID真实Finish按source删首项，Effect和预测到期不删。Plaincracker和两环各由自己的事件驱动，不虚构自动连续推进。

第三幕Maelstrom为全部2681实体的R8原始动态位置/朝向硬障碍，不筛Dead、Targetable或EventState7。复用第23场已有实体障碍输出，14780不重复造读条圈。只画Primary敌人标记，Maelstrom和熔岩不画敌人箭头。

## 第25场Meteor两组件及三路熔岩

R15读条圈与R10 MeteorVoidzone是独立组件，不能合成同一锁点圈。

- Start路径保留caster旧Actor引用及首次判定钟；每帧读该引用当前CastInfo目标，目标存在时跟随其当前位置，否则读当前Loc。该R10预测不随普通R15圈锁点。
- 真实效果按MainTarget当前坐标、其次包落点锁位置；activation为处理时钟加 `1.2d`，不按包历史时间加延迟，也不要求此前有Start。保留来源、目标、sequence、发生/处理/判定钟；无超时回收。
- 实体1EA8BB只筛EventState!=7，不筛Dead或可选。R10实体输出采用源量化位置，进入普通危险导航；不变成硬障碍。

公共 `DungeonCarnivaleClaws` 继续适配上述三路。每次Update每个有效实体用原始位置匹配第一条距离≤2的效果预测；一帧每实体只删一条，下一帧同一实体还能消费下一条。EventState7既不输出也不匹配。真实Finish分别清读条R15及R10 cast预测，不清效果预测或实体；效果也不主动清cast路径，短暂并存是源行为。

Web14770保留每模块一个全局bool：任意匹配Start置true，任意匹配Finish置false，重叠来源不计数。同代packet位置补正不是实际Start，不重新开提示；真实raw变更的Finish→Start仍按源推进。文字仅说明靠边诱导及Loom/减伤，不生成自动传送、边缘目标、移动保持或动作请求。

反伤状态1307/556和个人Doom910只产生源文字。第三幕先查556、else-if查1307，两者并存只提示魔反。读取完整原始状态槽，不按ExpiresAt自行隐藏；实际槽清空或Lose才更新。切换观看者重新取其Doom。第一幕构造成就文字保留。

第二幕按创建序首个未Dead的2682提示快速击杀，不要求可选；标记为橙色。全部合法候选2682 priority1，其余0，保留Pointless=-1。没有Burn伤害预测、伤害类型禁攻、全停手、自动净化或Loom执行；源只有攻略或TODO的部分未扩展。

## 第26场读条与实体

第一幕五组件、第二幕八组件（含构造Hints），规则分别3、4。

| 幕 / AID | 模型与当前输出 |
|---|---|
| 1 / 18687 Gust | R3普通锁点危险及双视图 |
| 1 / 18686 AlternatePlumage | `Prepare to dispel buff`，纯CastHint |
| 1 / 18688 CaberToss | 独立来源、完整读条及真实结束的打断意图，无自动技能 |
| 2 / 18602 VoidThunderII | R4普通锁点危险及双视图 |
| 2 / 18604 RawInstinct | `Prepare to dispel buff`，纯CastHint |
| 2 / 18603 VoidThunderIII | 当前raw动态钟、存活0–63槽Actor Raidwide预测与原提示，无R20危险圈 |
| 2 / 18601 BodyBlow | 当前raw目标/钟，self-target回退caster当前TargetID，名单允许Dead或缺Actor；Tankbuster预测及Soft Tankbuster文字 |

Thunderhead全部2C59中只按EventState!=7筛选，R8原始动态位置/朝向硬障碍；死亡和不可选仍存在，不依赖18606。不画Thunderhead敌人marker，两幕均只画Primary。无专属priority覆盖。

Primary状态63/1797只提示驱散；当前观看者269/271只提示净化。与第25场复用完整原始状态槽读取，不按截止时间伪造Lose。未扩展增益期间禁攻、自动驱散或净化。

## 第26场图标击退

生产 `CarnivaleDadJoke` 保存代际、来源/目标、图标、发生/处理/判定时间、Primary身份与距离15；`DungeonCarnivalePapa` 按模型动态求落点，再经既有 `QuestKnockbackProjection → DungeonOutput` 发布显示。

图标23将共享判定钟覆盖为处理时钟+5d，不建立队列。要求图标来源Actor仍存在，沿源ActorState门控丢弃未知或已移除source；不要求source为Primary或target为观看者。历史图标不随组件激活补播。真实18605效果同样要求caster存在，任意现存helper也能清除；Finish、取消、图标目标死亡/销毁及5秒到期均不清除。

每次按当前Primary及观看者位置计算方向，保留DirForward类型、无shape、15码、不截墙、不扣hitbox。两者同点时Atan2(0,0)=0，实际沿+Z位移15码，不能按AwayFromOrigin跳过。Boss与party0同时缺席时模块暂存；同ID/OID主怪回来时沿源Manager重绑，模型原点也更新为新引用，图标代际和判定钟不变。普通目标引用冻结规则不因此改变。

免疫复用三类别覆盖槽：160/1209/3054为Role，1722/2663为Job，2345为Duty；Gain覆盖该类别，Lose清该类别，任一expiry严格大于activation才免疫。相等仍受击退。落点危险仅检查当前有效Thunderhead R8或出界，不预测整段碰撞或未来实体运动。

源DadJoke无击退导航输出；当前只提供模型、双视图落点线/轮廓及危险文字，没有自动免疫、反向安全区、移动保持或原生动作。不能将显示验证记作击退导航或执行已完成，也未扩展统一寻路试点。

## 验证边界

147项斗场集中模型通过。第25场9组及第26场8组覆盖20个普通AOE实例、两种Bolt宽度、Meteor两组件/三路池、处理钟、真实结束、量化与逐实体首项消费、Web重叠与同代补正、完整状态槽、个人视角、图标覆盖/超时/未知source、Primary同ID重绑、同点+Z、严格免疫、单体缺Actor/Dead名单及Raidwide存活槽。

五模块共用生产编译；独立复核后定向重跑第26场身份边界。实际DLL专项覆盖五幕每幕5772点源场地Contains对照、双视图发布、普通AOE导航和硬障碍、两类Meteor并存、图标投影及危险文字、raw状态槽、反向Seek、开关与owner。初次专项将 `cast/` 子串误包含Meteor的 `carnivale/ray/cast/`，修正测试选择器后通过，没有压掉合法双组件输出。生产最终0错误/4条既有Crucible警告，首次专项20条PASS及身份修后第26场11条专项PASS；最终全目录774条PASS/0失败。证据为 `.tmp/halatali-verify/native-carnivale-azulmagia-papa-model-final.log`、`native-carnivale-azulmagia-papa-build-final.log`、`native-carnivale-azulmagia-papa-focused-retry.log`、`native-carnivale-papa-focused-review-retry.log`、`native-carnivale-azulmagia-papa-catalog-final.log`。

显示发布及当前实际提供的普通危险/障碍导航已离线验证；真实解算闭环、宿主执行及游戏最终像素仍待实机。回放只做规划和绘制，不发送GreenMove、面向或原生动作。
