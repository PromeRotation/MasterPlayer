# 天青斗场第23、24场原生机制

## 来源与共同接入

对照 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage23BehemothsAndBroomsticks/Stage23.cs`、`Stage24AmazingTechnicolorPitFiends/Stage24Act1.cs`、`Stage24Act2.cs`、`Stage24Act3.cs`，及实际激活的 GenericAOEs、Voidzone、BaitAway、Knockback、UnavoidableDamage 和 CastInterruptHint 组件。生产入口沿用 `Entries/Stage01.cs → DungeonEncounter → DungeonOutput → SafeZone/Draw`，不消费桥输出。

本机简体中文表核对：第23场「魔兽皇太子——贝希摩斯太子」，CFC633 → InstanceContent35023 → Territory796，主OID2732 / Name8124；第24场「异形人偶师——艾匹罗基」，CFC634 → InstanceContent35024 → Territory796，三幕主OID2735 / Name8127、2736 / Name8128、2739 / Name8125。不能用Territory796反向CFC覆盖分场映射。

两场共四模块，26组件实例、17条逐幕规则。大场中心(100,100)、R24.5/64边，小场同中心、R16.01379/32边。第23场和第24场第三幕用小场，前两幕用大场。普通AOE保留起手落点、朝向、判定时间及来源代际，真实Finish/取消/打断/销毁清理，Effect和预测超时不清；死亡不冒充销毁。当前输入重复不刷新锁定钟。

晚激活组件可以认领尚未接管的当前raw读条；接受时保留NPC剩余时间下限 `now + (double)0.3f`。真实取消即使发生在组件激活前，也为已知raw记录结束水位，防开战后旧快照复活。第23场四个AID及第24场11个AID的本机Action表 `ExtraCastTime100ms` 均为0。

## 第23场

默认Primary可选且InCombat拉战，Primary死亡、销毁或HP0结束；脱战不结束。只画Primary敌人标记，无专属优先级setter。构造攻略常驻，五个战斗组件如下：

| 来源 | 机制模型与当前输出 |
|---|---|
| 15258 Charybdis | 起手锁定R6圆，危险与双视图 |
| 15256 Trounce | 起手锁定R55.8、总60°锥，危险与双视图 |
| 15261 Comet | 起手锁定R10圆，危险与双视图 |
| 15257 Meteor | 当前raw动态判定时间、存活0–63槽Raidwide预测及源减伤提示，无R50危险圈 |
| 2733 Maelstrom实体 | 全部现存引用，原始动态位置和朝向、R8硬障碍、双视图及圈内文字 |

Maelstrom不筛Dead、Targetable或EventState7；实际销毁才移除。15259读条或效果不关闭池，也不生成重复圈。源没有40码拉近消费组件，不能根据枚举注释制造位移模型或执行。15260、6499只有枚举，无额外输出。

## 第24场前两幕

第一幕由任意2735或2734 InCombat拉战，不附加可选、Dead或HP条件；全部两类实体DeadOrDestroyed结束，HP0不代Dead。画固定Primary和全部2734，全红。固定Primary未Dead时输出魔免文字；2734只检查创建序首项，首项死后不越过它找下一只。没有专属优先级或伤害类型限制，源TODO不作为已实现功能。

第二幕使用默认Primary生命周期。画Primary及全部2737、2738，全红；候选2737/2738的priority为1，其余0，源setter无Pointless保护。保留源文字行为：魔免和物免两分支都查询首个未Dead的2738 Magus，同时输出两条文字；只有2737 Viking不产生这些提示。不将该文字推断为PR禁攻、反伤停手或伤害类型过滤。

| 幕 / AID | 模型与输出 |
|---|---|
| 第一、二幕15317 | R5圆，普通危险与双视图 |
| 第一、二幕15316 | R5、总90°锥，普通危险与双视图 |
| 第一幕15318 / 第二幕15321 | 独立打断意图、来源身份和当前截止钟，真实Finish清理，无自动技能 |
| 第二幕15322 | R6圆，普通危险与双视图 |
| 第二幕15319、15320 | 同一单体伤害组件；按当前raw目标与截止钟预测Tankbuster，目标等于caster时改用caster当前TargetID |

单体目标按0–63名单查槽，名单中Dead或缺Actor仍可预测；不把两个动作的Finish互相清除，不生成危险圈或自动减伤。源无Libra专属连续阶段、自动净化或青魔动作消费者。

## 第24场第三幕

默认Primary生命周期。画Primary及全部273A，全红，不画273B敌人marker。候选273A的priority为1，其余0，同样无Pointless保护。首个未Dead的273A保留源快速击杀及远程物理攻击文字。

15327为R8圆；15324为R8、总90°锥，采用源实现而非注释R9。15326仅动态raw Raidwide预测和 `Adds + Raidwide` 提示，不生成全屏危险圆。15323、15329、14266及自动攻击仅枚举。

Vacuum实体273B仅按 `!Dead` 过滤，R3原始动态位置硬障碍；不可选及EventState7仍存在。死亡隐藏、复活恢复、真实销毁移除，不依赖15328。实体圆进入导航和双视图，15328不重复生成cast圈。

15325由两个独立组件消费，不能合并：

- 冲锋诱导保留caster/target原Actor引用、动态方向及半宽4矩形。目标缺失不建立诱导；源死亡隐藏，目标销毁后冻结旧引用，同ID新对象不换绑。本人轮廓/导航、其他队友方向禁区及Tankbuster预测复用 `DungeonCarnivaleCharge` 和公共输出。
- 击退保留 `CarnivaleKnockbackCast` 的来源代际、完整cast、距离10、Forward类型、Start锁定位置/朝向/判定时间、无shape资格和不截墙规则。公共StartCast明确保留15325朝向，不能因shape为空而清零。`CarnivaleCastKnockbackMovements` 按多source插入序从上个落点继续，免疫必须严格晚于各次activation。

该击退即使没有冲锋目标也存在，不要求玩家位于诱导矩形。落点危险检查当前存活电场R3或出界。当前适配只发布投影线、玩家轮廓与危险文字；源击退组件没有新增KB导航，不以这些显示声明已自动处理击退，也不增加主动移动、免疫技能或原生操作。模型与几何适配继续独立，未启动统一寻路试点。

## 验证与边界

130项斗场集中模型通过；第23场5组与第24场9组包括生命周期、原序文字、全部规则、raw锁钟/动态钟、真实结束、晚激活及激活前取消、实体障碍不同过滤、目标销毁代际、独立前向击退链及严格免疫时刻。生产共用一次编译，0错误、4条既有Crucible警告。

实际DLL专项17条PASS（含2条公共输入检查），四模块每个5772点与源场地Contains逐点对照。覆盖危险/硬障碍实际发布、双视图显示源、普通AOE和诱导导航、独立击退无目标与电场落点、反向Seek、开关及owner撤销。首次专项中的快照属性名错误只修测试为实际 `HardObstacles`，随后专项通过。证据位于 `.tmp/halatali-verify/native-carnivale-behemoth-pit-model-fixed.log`、`native-carnivale-behemoth-pit-build-final.log`、`native-carnivale-behemoth-pit-focused-retry.log`，全目录755条PASS/0失败，证据为 `native-carnivale-behemoth-pit-catalog-final.log`。

显示发布和当前提供的导航已离线验证；真实解算闭环、宿主动作执行及游戏最终像素仍待实机。回放不执行GreenMove、面向或原生技能。本批没有源护盾禁攻、视线、停手或动作执行组件，不增加这些行为。
