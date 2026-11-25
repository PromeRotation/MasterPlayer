# 天青斗场第22场原生机制

## 来源、身份与输出

对照 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage22HereComesTheBoom/Stage22Act1.cs`、`Stage22Act2.cs`，以及 `Components/GenericAOEs.cs`、`UnavoidableDamage.cs`、`Data/Actor.cs`、`ActorState.cs` 和组件激活补播。生产沿用 `Entries/Stage01.cs → DungeonEncounter → DungeonOutput → SafeZone/Draw`，不消费桥结果。

本机简体中文表核对：正式名称「爆炸湮灭——爆弹之祖」，CFC632 → InstanceContent35022 → Territory796；Territory796反向CFC仍为610，不能用其反向字段覆盖分场映射。第一幕主OID26FC / Name8122「斗场榴弹怪」，第二幕26FE / Name8123「爆弹之祖」。第一幕中心(100,100)、R24.5的64边形；第二幕同中心、R16.01379的32边形。

共两模块、八组件实例、五条读条规则。第一幕构造攻略和战斗提示各一组件；第二幕构造攻略及五个战斗组件。没有源连续爆炸序列、分摊、击退、自动拉怪或技能执行组件。

## 第一幕移动拉战

源拉战条件是没有任何可选26FE、玩家本帧XZ净位移非零、存在任意可选26FC。不检查 InCombat，不限 Primary，不额外排除 Dead/HP0 对象。26FE即使死亡，只要仍可选就阻止第一幕拉战；26FC即使HP0而未Dead，也能满足可选条件。

`Actor.LastFrameMovement` 是本帧当前位置减帧开始时的旧XZ。新演员、缺席后同ID重生、单纯Y/朝向变化以及静止重复帧都为零；没有距离epsilon，任何可表示的XZ差均有效。取消阻挡后必须重新发生位移，不能沿用上一帧运动。

MP按统一脚本视角契约使用 `PlayerId`，而非源旧桥的固定 Party[0]。`carnivaleMovingActors` 每次 `CarnivaleInput` 清空，覆写已有 Actor 状态前比较该身份自己的旧/新XZ；全部演员和当前PlayerId提交后才判断拉战。切人本身不能把两人位置差当位移，非观看者移动不能触发。构造加载距离使用的 previousPlayer 不作为运动基线。

全26FC DeadOrDestroyed才结束；Primary死亡、HP0或单个炸弹销毁不替代全组结束。显示全部26FC标记，战斗文字为 `Kill the grenades in one hit each or they will wipe you. They got 543 HP.`。14901只有枚举，无AOE、伤害预测或自动一击杀请求。

## 第二幕组件与连续输入

默认 Primary 可选且InCombat拉战，Primary死亡/销毁/HP0完成，脱战不结束。画Primary和橙色26FC/26FD，不画Helper；无专属priority setter。

| AID | 源组件 / 锁定 / 当前MP输出 |
|---|---|
| 14906、14907 | 同一Sap组，R8，Start锁Loc/Rotation/activation；全部危险与双视图 |
| 14903 | ScaldingScolding，R11.75总120°锥，普通危险与双视图 |
| 14902 | Flashthoom，R7.2圆，普通危险与双视图 |
| 15040 | Ignition，动态当前读条钟的Raidwide预测及原文提示，不生成R50危险圈 |

普通AOE只有匹配AID的真实Finish按source删首项。Sap按源 `SimpleAOEGroups` 保持两个动作的同一插入队列，每次添加达到99项后才排序；任意AID Finish均按source删首项。Effect和预计到期不删除这些圈，取消、打断和施法者销毁沿真实Finish清理；死亡本身不冒充销毁。

Sap模块状态保留代际、完整原始cast、来源/目标、锁点/方向及开始/判定钟；输出几何在公共层适配。开战前的raw观察若尚无组件认领，晚激活时必须允许当前cast进入；不能被重复输入保护误挡。真实已结束代际仍受结束水位约束。该反例同时覆盖Sap和普通Flashthoom。

新组件补播按持久演员创建序，不随快照反序改变。客户端Action14903/14906/14907/15040/14902的ExtraCastTime100ms均为0；源Elapsed封顶后仍有NPCFinishDelay=(double)0.3f，因此晚接管的当前读条下限为激活now加该延迟。现存AOE保持原锁定钟，不逐帧刷新；Ignition每次按当前CastInfo输出，并保留源剩余时间下限，二者不能混用。

Ignition提示为 `Wipe if Grenade is not killed yet, otherwise Raidwide`。伤害目标是存活、有Actor的0–63槽，含联盟/NPC，不把提示变成游戏HP或额外治疗策略。读条变化只调整当前预测钟，真实Finish撤销。

战斗提示按持久创建序分别找首个 `!Dead` 榴弹怪和煤气弹；HP0、不可选和未入战不额外过滤。保留原名字及文字换行，包括源 `bos\nto interrupt the enrage!`。14905、14901、14904、6499仅枚举，不新增自动Sticky Tongue、Burst打断或伤害预测。

## 验证边界

模型覆盖冷创建、XZ/Y/朝向、切视角、同ID重生、相同时间静止帧、可选第二幕阻挡、全组完成与HP0、持久提示顺序、Sap任意Finish/99阈值/锁点/无超时、Ignition动态钟及64槽、未认领旧raw的晚激活和后台即时交接。

实际DLL回放覆盖两幕真实场地源Contains逐点对照、两视图预览、普通危险区导航、Sap共享队列、Ignition只预测、反向Seek、机制开关及owner撤销。结束当前幕不应误清其他仍加载模块的合法战前预览。最终116项斗场集中模型、生产0错误/4条既有Crucible警告、9条实际DLL专项、740条全目录PASS/0失败；两幕各5772点源场地oracle通过。证据位于 `.tmp/halatali-verify/native-carnivale-boom-model-final.log`、`native-carnivale-boom-build-final.log`、`native-carnivale-boom-focused-retry.log`、`native-carnivale-boom-catalog-final.log`。

显示发布与当前普通导航已离线验证后才能登记；真实解算闭环、PR宿主执行、最终游戏像素仍需实机。回放不发送原生动作，本场未扩特殊移动执行，也未启动统一寻路改造或桥清退。
