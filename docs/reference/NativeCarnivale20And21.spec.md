# 天青斗场第20、21场原生机制

## 来源与接入

对照 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage20MissTyphon/` 三幕、`Stage21ChimeraOnAHotTinRoof/` 两幕，以及 `Components/Knockback.cs`、`GenericAOEs.cs`、`CastHint.cs`、`Voidzone.cs` 和组件激活补播实现。生产沿用 `Entries/Stage01.cs → DungeonEncounter → DungeonOutput → SafeZone/Draw`，不消费机制桥输出。

| 场次 | CFC / InstanceContent / Territory | 主OID / NameID | 场地 |
|---|---|---|---|
| 著名组合——奥尔特罗斯&提丰·第一幕 | 630 / 35020 / 796 | 272A / 3046 | 中心(100,100)，R24.5、64边 |
| 著名组合——奥尔特罗斯&提丰·第二幕 | 同上 | 272B / 7111 | 中心(100,100)，R16.01379、32边 |
| 著名组合——奥尔特罗斯&提丰·第三幕 | 同上 | 272C / 3046 | 同第二幕 |
| 愤怒合成兽——阿帕德马克·第一幕 | 631 / 35021 / 796 | 272F / 8120 | 大场 |
| 愤怒合成兽——阿帕德马克·第二幕 | 同上 | 2730 / 8121 | 小场 |

五模块共31个唯一激活组件实例、21条逐模块读条规则。第20场第二幕激活链重复书写 LightningBolt，最终只有一个组件；不能重复生成输出。构造攻略在拉战时撤销，青魔技能推荐不生成自动动作。

## 第20场普通组件与阶段

三幕按固定 Primary 可选且 InCombat 拉战，Primary 死亡、销毁或 HP0 完成；不等第三幕小怪，也不以脱战结束。第一、三幕成就文字只在战前保留。第三幕画 Primary、橙色272D奥尔特罗斯和272E触手；选敌优先级触手2、奥尔特罗斯1、其他0，源覆盖没有 Pointless 保护。前两幕使用默认 Primary 标记和基础选敌。

| 幕 | AID | 源组件及输出 |
|---|---|---|
| 一 | 14706 | Fireball，R8圆 |
| 一、三 | 14704 | Snort纯提示 `Use Diamondback!`，另有独立击退组件 |
| 二 | 14713 / 14714 | R13.1总90°锥 / R11.1圆 |
| 二 | 14718 / 14717 | R4 / R3圆 |
| 二 | 14712 | ImpSong，仅打断意图和不具名文字 |
| 三 | 14745 / 14746 | R13.1总90°锥 / R11.1圆 |
| 三 | 14747、14706 | 同一个 SimpleAOEGroups，R8圆 |
| 三 | 14748 | Wallop，前57.2、半宽5矩形，另有独立击退组件 |
| 三 | 14744 | ImpSong，单施法者具名、多施法者不具名 |

普通危险区保持锁定 Loc/Rotation、原始判定钟及真实 Finish 生命周期。三幕火球各自持有队列，第一幕14706不能因后台第三幕同动作规则而重复或失去几何。第三幕两动作共享插入序，达到99项才按 activation 排序；任意动作 Finish 按 caster 删除首项，不限于组内动作。

14744文字只在 Start/Finish 更新缓存；单纯 Actor 改名不改当前文字。另一个 caster 开始后变为 `Interrupt!`，其 Finish 后按剩余 caster 当时名字重建。取消、raw消失和销毁同样走真实 Finish。14705、14707、14749只有枚举，不新增其无组件的火球、锥或击退。

## 第20场击退模型及适配

`CarnivaleKnockbackCast` 保存代际、完整施法、距离、撞墙标志和可选命中形状。完整施法包含来源/目标身份、锁点/方向和开始/判定钟。模型由真实 Start/Finish 驱动，纯预计到期和 Effect 不撤销；来源销毁按原读条收据结束。新组件补播与现存组件的原锁钟分离。

| 幕 / 组件 | 规则 |
|---|---|
| 一 SnortKB | 自锁定 cast.Location 向外30，StopAtWall=true |
| 三 SnortKB | 自锁定 cast.Location 向外30，不截墙 |
| 三 WallopKB | 自锁定 cast.Location 向外20，仅链当前中间点处于锁定矩形时生效，不截墙 |

各独立组件均从当前观看角色位置起算，各自内部按插入顺序从前一落点继续；Snort 与 Wallop 不串成跨组件位移。位置/朝向不跟随之后的 caster 变化。免疫复用 `CarnivaleKnockbackImmunity` 的原生状态槽差分、三类覆盖及严格 `expiry > activation`；等于判定时刻仍受击退。免疫只撤位移投影，不撤 Snort 提示，也不自动使用超硬化。

公共适配复用 `TreasureWallLanding`、`QuestWindRay` 和 `QuestKnockbackProjection`，以正式多边形截墙并扣源 hitbox 内缩，发布位移线及玩家朝向投影。非截墙落点越界时保留 `About to be knocked into danger!`。源击退没有导航输出，迁移也不把箭头冒充自动站位或物理执行；普通 Wallop 矩形仍独立参与导航。

源射线差异明确保留证据：`PolygonBoundaryIndex2D` 将边按Z升序归一，标量和SIMD求交均排除上端点；顶部共同顶点同时落在两条边的排除端。源 `CircleBig.IntersectRay((0,13),(0,1))` 返回 `float.MaxValue`，其包含端点的 `Intersect.RaySegment` 逐边求交为11.5。生产沿用MP既有正确多边形截墙，普通0.5 hitbox时落在世界Z=124.499，未复制错误的Z=143。实际DLL定向从源Polygon读取真实顶点，调用源逐边RaySegment作独立oracle，并明确断言/计数优化器差异；不是声称源索引射线完全一致。没有修改BMR优化器或其他机制的导航范围。

## 第21场阶段及三路毒池

第一幕任意272F InCombat拉战，不额外要求可选或存活；全272F DeadOrDestroyed才完成，HP0不替代Dead。画全部272F，战斗中持续显示弱火抗冰及打断建议。第二幕使用默认 Primary 生命周期，只画 Primary 和橙色2731；只要存在 `!Dead` 小魔精就显示该建议，不按 HP、Targetable 或 InCombat 过滤，无专属优先级。

| AID | 源组件及输出 |
|---|---|
| 15064 | 两幕各自的 Icefall，R5圆 |
| 15063 | 两幕各自的 VoidBlizzard，仅打断意图 |
| 15079 | 第二幕 TheRamsVoice，R9圆 |
| 15080 | 第二幕 TheDragonsVoice，8–30环 |
| 15081 | 第二幕 TheRamsKeeper，三路R9毒池，另有独立打断组件 |

15081复用 `CarnivaleVoidzoneCast`、`CarnivaleVoidzonePrediction` 及既有公共输出：

1. cast保存原caster引用、完整cast和代际；显示位置动态取当前 CastInfo 的目标Actor，缺失回落Loc，判定钟在组件 Start 锁定。
2. 真实 NativeActionEffect 保存 source/MainTarget/action/sequence、事件钟、消费钟和锁点；位置先取真实 MainTarget 当前坐标，缺失才取包落点。空 Targets 仍消费，零 sequence 独立包不误合并，legacy逐目标展开跳过。activation为消费钟加 **0.9d**。
3. OID1E972A实体仅按 EventState!=7 筛选，Dead实体也有效；位置按源量化，每Update每实体仅删除首个距离≤2的事件预测。事件预测不超时，Finish只清对应 cast/interrupt，不清历史事件池。

这些毒池是危险区及双视图预览，不改成硬障碍。后台两幕独立消费共享动作，切换前台不重建或丢失事件。14267、6497仅枚举，无额外单体预测；没有凝视、击退或自动技能执行。

## 验证边界

机制模型覆盖阶段、全组与 Primary 完成差别、原始击退参数/免疫/独立链、99项共享火球队列、具名提示缓存、真实结束、三路毒池精确delay与原生主目标、后台交接和原始输入代际。实际 DLL 回放验证两视图发布、普通危险区导航、正式场地源几何、真实状态输入、反向Seek、机制开关及owner清理。

最终109项斗场集中模型、生产0错误/4条既有Crucible警告、20关12条及21关9条实际DLL专项（各含2条公共）、733条全目录PASS/0失败。五幕各5772点场地源oracle通过，击退180点逐边oracle中明确检出1个源索引差异。证据位于 `.tmp/halatali-verify/`：`native-carnivale-typhon-chimera-model-final.log`、`native-carnivale-typhon-chimera-build-final.log`、`native-carnivale-typhon-focused-retry-final.log`、`native-carnivale-chimera-focused-initial.log`、`native-carnivale-typhon-chimera-catalog-final.log`。免疫严格相等夹具改为读取同一序列化FinishAt，消除手算4.4与double加法造成的1tick偏差；未修改生产免疫规则。回放只规划和绘制，不发送原生操作；击退物理、真实PR执行和游戏内最终像素仍待实机验收。本批不实施统一寻路试点或桥清退。
