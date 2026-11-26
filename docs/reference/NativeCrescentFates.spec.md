# 新月岛 FATE 原生机制

本批源为 `MechanicRuntime/Upstream/Modules/Dawntrail/Foray/FATE/`。生产位于 `Presets/Fates/Crescent/`，继承 `MasterPlayerScript`，不消费 BossModule 或旧桥输出。源类型按原清单计入 Foray；下列五个入口和十三个组件不能换算成十三个源模块。

| 源类型／入口 | Territory / CFC | FATE / Primary OID | 实际激活组件 |
|---|---|---|---|
| TheWingedTerror | 1252 / 1018 | 1965 / 46C1 | GaleCannon：前40、宽10矩形；SphereShatter：R7 |
| CrescentGremlin | 1346 / 1093 | 2072 / 4D8A | CrudeTaunt：前25、宽6；NonsenseTaunt：R5 |
| CrescentFan | 1346 / 1093 | 2073 / **4D8C保护罐** | HighPressureTornado：50221、50222共用前15、宽4列表 |
| EyeToEye | 1346 / 1093 | 2075 / 4BA7 | AllEyes动态全队预测；SeeNoEvil R30凝视；Jettatura R8；ColdStare R40／总90°；SinisterSight R50凝视 |
| DemiMedusa（源 ScaleModel.cs） | 1346 / 1093 | 2083 / 4C6A | CursedSight：48252／48253共用R60／总60°；LamianLesion R25／总180°；Dark R6 |

本机匹配客户端表实际双向核对两组 CFC/Territory，读取五个 FATE 的正式名称；OID来自源元数据。入口 GUID 为 `masterplayer.fate.` 加表中类型名小写，默认启用，显式关闭优先且刷新保留。50223仅在枚举中，无激活组件；注释提及的共享罐47CB不获得4D8C的生命周期。

## 输入和源生命周期

- `ReplayActiveFate` 保存原始当前FATE身份、中心、半径；与等级资格 `AllowedFateId` 分开。API按实际DLL对应 FFCS commit `c306aa6c980c38db9ad5155a9f219e2931f4f1c2` 的 `FateManager.CurrentFate` / `FateContext.FateId,Location,Radius` 核对。新录像完整和增量均写入，旧文件null保持未知。原始状态槽、其他录制字段及v2版本不变。
- 普通四场沿用 `OpenWorldFate`：主怪可选中且战斗、源槽零玩家进入圆才拉战；主怪非零FateID优先于元数据NameID匹配当前圆。尚未匹配时跟随主怪位置、R30；匹配后缓存中心和半径，变化阈值为X/Z各0.01以及半径0.01。源 UpdateModule 先更新场地，再 CheckReset；脱战、已缓存ID改变、源玩家缺失或离开R+10均重建。死亡、HP0、销毁完成当前原引用；同帧同ID/OID重新创建的Loaded主怪按源Manager直接重绑，新Actor shell不继承旧状态，其他组件保存的原引用保持。
- 保护罐不要求战斗或可选中，只接受当前FATE2073有效圆以及真实4D8C存在；记住BigFan4D8E曾出现，之后不存在则重置。死亡但仍存在不算消失；第一批小怪消失不完成保护罐。当前2073仍有效时以当前圆R+10判断离场，否则回源基类检查。
- 拉战位置取已记录 `SourcePlayerId`，旧文件回退其记录的LocalPlayerId；显示和个人面向取当前观看角色。切换观看角色不改变源拉战角色。ARR/MP仍禁止实际执行。
- 冷激活按组件顺序，再按世界插入顺序补播所有原生NPC的当前Spell；玩家、Pet、Chocobo、Buddy和非Spell排除。真实raw总长、落点或方向变化触发旧Finish→新Start；单纯actor移动和时钟推进不重启。销毁先Finish，旧引用标记Destroyed，新同ID不得继承。
- 主怪Loaded/Pending保留源默认500码三维加载门槛：初始世界先建Actor后交Party，候选逆插入序晋升；已有源槽位时新建主怪可立即按创建序加载。观察者销毁立即失效，同ID重建后按输入序恢复槽位。旧主怪本帧死亡/消失先退休，候选下一同步晋升；重置或未激活且超距者退回Pending，不永久挡住近处候选。500是本批固定默认值，不消费BMR可配置的MaxLoadDistance。源观察者缺失时MP保守清个人输出，不声称完全复制源Manager暂停Update的行为。

旧录像 `ActiveFate=null` 是明确的输入缺口：脚本给出诊断，不猜R30或实时场地，不输出场地依赖AOE/导航/控制。此保守适配区别于已知 `Id=0` 的源分支，不声称可以完整还原未采集场地的历史录像。

## 组件消费与输出

- 普通SimpleAOEs只接受watched Finish；两种SimpleAOEGroups对该caster的任意Spell Finish删除首项。每个组件拥有独立列表；未满99保持插入序，达到99才在本组件Start时按源右枢轴排序，包含相同时钟的非稳定顺序。复用现有 `DungeonAoeOrder`，不重构旧消费者。
- 位置/方向/判定钟在Start锁定。钟为float `TotalTime + .3f - ElapsedTime` 仅一次转换DateTime；Elapsed封顶Total+客户端ExtraCastTime：47147为0.5秒，另外14动作均为零，真实ActorState.Tick对照饱和3.5秒后的NPCRemainingTime。普通区域从原判定开始持续到真实Finish撤销，不统一按预计结束或0.6秒尾窗删除。包Cancel/Effect不合成raw Finish。
- 凝视锁定原位置与判定钟，R30和R50独立。眼睛及危险颜色按源显示，圆内才提示、画视角弧并加入联合面向；既有MP执行适配在判定前0.75秒启动联合安全方向，PR请求通过统一门面续期，移动总开关/回放/owner门控不变。
- AllEyes保存原caster引用，预测使用其当前raw时钟；按已记录旧Party槽提交存活且有Actor的成员。不生成全屏危险区，不新增治疗策略，预测和机制文字接入机制库诊断。
- 普通AOE以AddExactDangerSnapshot注册navigation-only区域，同时独立发布世界/雷达原尺寸预览；用户DodgeDangerMargin只在owner求解层应用一次。圆场边界按own-reference释放，外部替换边界不会被本脚本停止清掉。规划器复用现有SafeZoneSolver，无寻路重构、自动道具或区域推进。
- 通用躲避仅在当前原生FATE活跃、对应机制开启、实际原生施法者存在及动作被接管时压制。宿主按Territory+PrimaryOID精确禁桥；五脚本不压制整个岛的其他BMR模块。用户关闭原生脚本也不让已迁模块重新输出旧桥。

## 生成与验证

`tools/generate_crescent_fates.py` 是五个已审查模块的固定manifest生成器，生成Catalog和入口；叶源字节SHA256变化立即失败，必须重新核对继承、override和状态机。它不分析任意C#，不按类名猜语义，不包装运行库；未知模式不在支持集合内。模型与实际DLL专项注册及验证仍显式编写。

源差分使用真实运行库组件，同一输入逐事件比较列表、原身份、位置、方向、精确形状参数、DateTime、风险/颜色及有限凝视范围；五源十三组件、75个事件边界、49,572个独立形状点。另将分组推进到98/99/100项，包含相同时钟、另组件Start和非watched Finish；原生NPC类型、raw变化、代际、冷激活、圆缓存和保护罐均有反例。

2026-11-25本机最终证据（均位于`.tmp/halatali-verify/`）：

- `native-hydatos-crescent-20261125-model-03.log`：退出0，33模型场景/0FAIL，其中新月岛10、公共选敌9、脚本控制7、下一批Hydatos模型7；Hydatos模型通过不代表七源已迁。新增真BossModuleManager对照首帧远近候选和观察者同ID重建，不用复制算法作为预期。
- `native-crescent-20261125-build-review-04.log`：退出0，0错4既有Crucible警告，隔离SDK10.0.303，未部署；根SDK11仍未验。
- `native-crescent-20261125-catalog-review-02.log`：同DLL无筛选全目录退出0，1337条PASS/0FAIL，包含278入口逐文件独立编译、五脚本客户端元数据/真实回放及新月岛15条专项。真SafeZoneSolver检查raw R7、owner硬余量1、逃离距离大于8且小于9、无fallback；全目录同时覆盖其他公共消费者、PvP与回放执行门控。
- DLL SHA256：`e68e78ecff1612897e38c97a5d8bfe8636b61b4176b6c0bca378ff228440f15e`。前一轮review-03构建及review-01专项属于中间版本，不代替此指纹。
- 保留失败日志：catalog-final-01/02分别暴露旧新人训练内部常量和Arkveld混合入口独立编译缺口；新人训练改为相同territory字面量，Arkveld类型体精确拆到同目录独立类型、入口薄继承，未改GUID/设置/机制行为。build-review-02暴露Hydatos草稿数组Count错误，修为Length后重建。

所有实际宿主执行、原生采集时序、最终游戏内像素仍需实机验收。

## 十一源变体扩展（2026-11-25）

本次沿用上述五源生命周期与固定manifest，新增十一源、56个实际激活组件（44个普通组件、12个变体组件）。合计为16个源类型、16个入口、69个激活组件；这些计数不互相换算。入口仍位于`Presets/Fates/Crescent/Entries/`，GUID为类型名小写前缀；MadMudarch、GaleForceEncounter和RelicIcewolf分别映射源文件SwornToSoil、GaleForceEncounter和NH110RelicIcewolf。生成器固定叶源哈希与审查过的动作、形状、激活顺序，不把未知C#类自动转换成普通规则。

| 源类型 | Territory / CFC | FATE / Primary OID | 激活数／变体 |
|---|---|---|---|
| ARottenAffair | 1346 / 1093 | 2081 / 4D61 | 6／无 |
| ADelicateBalance | 1252 / 1018 | 1968 / 46B8 | 6／模块导航和选敌override |
| KingOfTheCrescent | 1252 / 1018 | 1964 / 46D7 | 5／Airburst |
| MadMudarch | 1252 / 1018 | 1969 / 46BD | 6／Rupture |
| AllureOfTheOccult | 1346 / 1093 | 2078 / 4D56 | 3／Burst |
| AnUnendingDuty | 1252 / 1018 | 1966 / 4735 | 7／ThriceComeThunder |
| GaleForceEncounter | 1346 / 1093 | 2082 / 4BEC | 4／FreefallSequence、FocusedTremor |
| RelicIcewolf | 1346 / 1093 | 2080 / 4D5E | 5／StormWithinWithout、AgeOfEndlessFrost |
| Thunderregnum | 1346 / 1093 | 2084 / 4D63 | 4／ThunderboltPuddle |
| APryingEye | 1252 / 1018 | 1970 / 47DC | 4／SearchMarkOfDeath |
| ServingDarkness | 1252 / 1018 | 1972 / 4772 | 6／SweepingMenacingCharge、MenaceCharge |

客户端证据在`.tmp/crescent-table-review/query-03.log`、`report.json`和`review.txt`：新增规则95个动作及完整源AID142个动作均存在，95个规则动作的ExtraCastTime100ms均为零。未激活视觉动作41973/41975/41977/41979/41983/41987/41991为700ms，不据枚举补机制。CFC1018/1093与T1252/1346双向核对；FATE正式中文名称来自本机Fate表。Fate行没有TerritoryType字段，未将地点行误当成FATE至Territory的直接外键；`query-02.log`的错误关联尝试保留且不计通过。

变体生产语义：

- Airburst只在原生NPC OID46D8创建时追加量化R11、当前钟+9.8秒。销毁保留旧预测与原Actor引用；同ID新代追加，41386真实Finish按ID只删首项，无统一超时。
- Rupture读取60个真实状态槽，4221且Param=2的Gain追加量化R16、当前钟+7.1秒，不去重，不因Lose或时间删除，29808真实Finish按来源ID删首项。冷激活逐组件、逐Actor执行Created→Cast→各状态槽，状态包含Pet；热状态仅排原创建类型Pet，Player/Chocobo/Buddy允许。原生E000来源归零、零剩余时长、负值绝对值和100000秒封顶，延长严格大于1秒才Gain，其余仍更新比较基线；同代类型快照不改原ActorType。
- Burst每次查询恢复基础风险/默认颜色；只比较WildWildBreath原列表第一项，cross严格早于circle时将圆改为非危险且显式AOE色，等时不抑制。不是取最早cross。源默认显式AOE黄褐色、Danger黄色和默认主题三态分别传入实际预览，不读取旧桥颜色配置。
- 两种Concentric保存独立序列、顺序及原锁点。真实Finish可来自其他实体，以order、X/Z每轴1码、方向0.05弧度匹配首条，晋升用消费时钟+2秒；三段R10、R10–20、R20–30。只有Update后刷新显示缓存，普通事件查询不提前刷新。
- Freefall仅47598且当前cast尚未生效时建立R12三项，精确额外延迟0.23/3.06/5.84秒；首项可见、全部Risky=false、默认颜色。47584真实Effect消耗首项，组件独立非零GlobalSequence去重，零序号逐包消费；Start重置序号集，严格超过activation+1秒才晋升超时项，普通Finish不消费。
- Storm以caster当前位置锁R10或R10–40、float NPC钟加1秒，任意新对应Start替换单项。真实Effect或后续同cast原生响应快照保留EventHappened；正常Finish保留，取消清理，正确49766/49767 helper Effect清理，错误helper不清，严格超过activation+1秒清。后续快照补历史不重启cast及判定钟；已发生Freefall的冷激活也不补造队列。
- Frost与九动作Puddle在查询时原地排序，最早activation+0.2秒包含边界内为Risky/Danger，后项保持可见且AOE色。Puddle按来源ID避免重复、使用NPC钟减1，Finish/超时不清；组件独立非零序号去重、零序号逐包按source清除，消费后尾项晋升。精确DateTime边界和+1tick另测，不将`3.2f+.3f`误作`(3f+.3f)+.2d`。
- Observer按47DD组后4818组、各自世界插入序输出，死亡隐藏；实时位置/方向、R9总120°、now+1.1秒持续更新。源额外即时R6总90°导航独立注册，不替换R9。Oogle默认10000范围凝视按源保留。
- Charge用真实delta长度、量化caster起点和锁定方向、宽8；自身分组到99项时才排序。Menace圆在42179 Start锁落点、NPC钟+5.2秒，只有42180 Finish清全，无超时；与Charge本身的Finish独立。
- SweepingCharge仅主怪当前42178提供闭R5、内5外0的精确弱goal与“Follow the charge!”全局提示。既有`MechanicHintsConvert.ConvertGoalZones`要求总峰值至少100，本模块单独weight5不会产生导航；原生保留`ChargeGoal/ChargeGoalScore`和提示，**不借迁移新增SafeArea、自动跟冲或治疗策略**。普通Charge危险导航照常输出。
- ADelicateBalance保留真实FATE显示圆，但导航使用当前观看玩家中心R30；主怪priority1、其余战斗目标0、其余-3，PendingDead不可被setter抬升；CryOfHavoc4770额外标记。共享选敌对1252/1346沿用Hydatos已验证的源List.Sort顺序、当前合法最高优先级及X/Z最近规则。

实时LogEvent/HandlesCast允许更新原始对象和组件事件，但不运行模块退休/晋升；Framework同步才执行该步骤，避免同帧重复事件提前拉起Pending。回放相同World.Version的事件扫描只推进时钟，不重复生命周期。稳定AOE使用原句柄；移动、朝向和选敌均经公共门控，ARR/MP回放仍禁执行，停用与owner释放不清其他owner替换的边界。

新增差分使用真实构造的十一BossModule，按实际激活顺序注册全部组件，以真实ActorState的CastInfo/CastEvent对照MP模型。44普通组件覆盖2247个组件事件边界和150984个独立几何点；变体逐项比较精确形状、顺序、原Actor、DateTime、Risky和三态颜色。Growth另用真实`MechanicWorldFeed`覆盖60槽、零/负剩余、100000秒、严格刷新和冷热类型；Charge对照源闭R5得分及既有桥不生成goal。模型对照不代替实际门面与求解器验收。

本次最终本机证据（`.tmp/halatali-verify/`，均退出0）：

- `native-crescent-variants-20261125-model-05.log`：56场景PASS/0FAIL（新月岛24、Hydatos及共享选敌/控制32），包含后续同cast响应快照与重复事件不晋升反例。
- `native-crescent-variants-20261125-build-02.log`：最终生产0错误、4个既有Crucible警告，隔离SDK10.0.303，未部署。
- `native-crescent-variants-20261125-replay-01.log`：首次构建DLL的72条PASS/0FAIL，包括元数据、完整Effect、四组Concentric真实Finish、稳定句柄、全部十二变体、R9/R6双导航真实SafeZoneSolver无fallback、Seek和owner；随后修正Danger为源默认黄色并重建。
- `native-crescent-variants-20261125-catalog-02.log`：最终DLL无筛选全目录1396条PASS/0FAIL，含290入口逐文件独立编译、上述专项及所有旧公共消费者、PvP/移动/回放执行隔离；比首次专项额外覆盖Freefall/Storm后续同cast原生响应快照和实际黄色预览。
- 最终DLL SHA256：`a5dc7747036cc8026ab5a598965e10ae68703ec61479e72d48b71814f250321d`。`build-01/catalog-01`及其`faa03b7c...`指纹为配色修正前中间版本。`tools/generate_crescent_fates.py --check`验证16源/69组件声明一致。

失败证据继续保留：`model-02`是新增oracle Enemy构造参数缺失的编译错误；`model-03`为19PASS/3FAIL，分别是直接ActorState夹具绕过Feed的负时长归一化、跨组Concentric测试残留，以及float钟与精确0.2秒DateTime边界混淆。修夹具后`model-04`22PASS；随后补真实Feed与接入修复得到`model-05`，没有放宽容差、删除断言或降低旧十三组件覆盖。

耗时采用日志创建至最后写入的本机文件时间，仅作命令墙钟证据：集中模型29.506秒、首次生产构建21.548秒、共享DLL专项18.134秒、首次全目录92.568秒，合计161.756秒；此合计不含最终配色修正重建及第二次全目录，亦非整批编码耗时。前半批跨上下文审查、编码和返工没有独立连续计时，不能倒推或声称已证明总体提速。本次增加十一真实源，集中初构建后有一次显示修正重建；后续批次继续分别记录审查/编码/验证/返工，而不按生成代码行数评价速度。真实采集时序、仇恨内存、游戏像素及宿主执行仍待实机；缺失原生历史的旧录像不从实时补造。
