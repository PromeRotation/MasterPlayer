# 丰水之地七类恶名精英原生机制

本批对应 `MechanicRuntime/Upstream/Modules/Stormblood/Foray/Hydatos/` 七个源类型；T827 / CFC639 已用本机简中客户端表双向核对。七者均继承 `SimpleBossModule`，不按 `OpenWorldFate` 的当前FATE、等级、圆内拉战合同激活。生产实现位于 `Presets/Foray/Hydatos/`，统一入口为 `masterplayer.foray.hydatos`。

本批七源已完成本机离线验收，正式审计更新为745 verified /468 pending /1 review。真实宿主执行、位移物理及最终像素仍需实机。

## 来源与组件

| 源类型 / Primary OID | 激活组件 | 生产消费 |
|---|---|---|
| Ceto / 2765 | SickleStrike、Petrifaction、AbyssalReaper、CircleOfFlames、TailSlap、Petrattraction、CircleBlade、GrimFate、Desolation、FireToss | 单体预测、凝视、普通危险与显示型拉拽 |
| Daphne / 2744 | Spellwind、Upburst、RoilingReach、Wallop、ChillingGlare、Netherwater、Spiritus | 全屏预测、十字/直线/圈、凝视、4–24人图标分摊 |
| Molech / 275D | W11TonzeSwipe、W111TonzeSwing、W111TonzeSwingAdds、W111TonzeSwingBig、ZoomIn | 扇/圈、量化起点与真实落点长度的冲锋 |
| Ovni / 2685 | PullOfTheVoid、Megastorm、ConcussiveOscillation、VitriolicBarrage、RockHard、TorrentialTorment、IonShower、Fluorescence、PlainPound | 拉拽显示、普通AOE、全屏预测、三次效果分散、驱散意图 |
| ProvenanceWatcher / 2686 | TheScarletPrice、TheScarletWhisper、EuhedralSwat、Touchdown、PillarImpact、PillarPierce、Thunderstorm、IceAndLevin、Charybdis、HotTail、HotTailSecond、AkhMorn | 目标中心诱导、普通危险、延迟全屏、真实效果队列、默认2人分摊 |
| Phuabo / 2741 | SpineLash1 | 普通扇形；毁灭/可选Tristitia阶段结束 |
| Tristitia / 2740 | ShockSpikes、MightyStrikes、TornadoIIAerogaIVDualCast、WatergaIII、重复激活DualCast、SpineLash2、Meteor | 驱散意图、唯一最终DualCast实例、普通危险与全屏预测 |

共50个唯一组件、51次激活；不按七个入口或七个副本计数。Catalog的51个不同动作经真实 `ActionID.CastTimeExtra()` 验为0。与位于同一区域的巴尔德西昂兵工厂等未迁源模块分别接管，不全区域禁桥。

## 生命周期与输入

- 每个定义保存持续Actor引用壳、60个真实状态槽、NPC读条时钟和独立机制列表。缺失/原生身份变化使旧壳销毁，同ID新壳不改写其他组件已持有的目标引用。
- 同OID仅一个Loaded，其余Pending逆序晋升。首帧Actor先于Party提交；已提交的源Party0与新对象顺序决定直接Loaded。距离为三维500码。相同主怪ID换代按源Manager重绑Primary；完成退休与同OID空位晋升分帧，重置重新排到Loaded尾部。
- 七个定义全部更新，`HydatosRegion`只让唯一Selected发布边界与输出。优先级为活跃4、存活可选3、其他Loaded1，已有选择只被严格更高优先级取代。不按当前距离或Catalog顺序逐帧切换。
- 默认Pull为Primary可选且InCombat，Reset为离战。默认完成为Primary死亡、销毁或HP0。Phuabo仅在全部2741销毁或出现可选2740时结束，尸体本身不代表销毁。
- 普通读条在真实原生Cast变化/结束时移除，不将PR Cancel、展开Effect或预测时间到期统一当Finish。NPC elapsed按真实总长封顶，预测采用源float运算后一次DateTime转换。
- 原生TargetId和Cast.TargetId的E0000000按真实Feed归0；E0000000到0不伪造Finish/Start。同一回放世界版本的连续事件仅推进NPC钟，不各自退休/晋升Pending；下一Framework扫描才推进管理器生命周期。
- Status仅从对象帧进入。零剩余保留零时钟，其他剩余取绝对值并封顶100000秒；源ID E0000000归0。槽身份变化先Lose后Gain；Param变化或expiry延长严格大于1秒仅Gain，缩短不重发。冷广播包含Pet，热Gain/Lose按创建时ActorType排除Pet。Dispel追加原引用，Lose只删首个，不能自动按期限删历史重复项。
- 两种拉拽分别持有Role/Job/Duty免疫回调时钟。同类后回调覆盖前回调，Lose仅清对应类，严格expiry大于activation。热状态先消费旧Party槽，帧末更新Party，冷激活消费新Party。不取当前状态的最大expiry，也不在换槽时擅自迁移历史。

## 输出与适配

- 显示场地为源Party0中心R30；Ceto/Daphne/Molech/Ovni/Phuabo/Tristitia的AI override不调用SimpleBoss基类，因此保留基础Circle30导航。Watcher使用SimpleBoss缓存中心的60×60方形导航。缓存按任一轴偏差严格大于5更新整数舍入值，不按欧氏距离或五码格。
- 标准危险、队列首项和源分摊/分散发布真实安全区。危险原始几何使用精确门面，用户余量仅在owner解算层应用一次。预览与导航分别注册、分别撤销。
- 分摊保留Source目标身份、重复图标、原FIFO、本人靠拢未点名伙伴半径3、他人圈半径6、Daphne容量24和多圈独占加入。源ForbiddenPlayers全0时不产生Shared伤害预测，不补造。
- IonShower图标取TargetId；Daphne取SourceId。Ion第三个真实完整效果清全部；Daphne按主目标首匹配，否则FIFO，空列表时计数不归零。
- Ovni本人Ion的0/100二值远离首领目标被现有桥转换器丢弃。本批保留明确诊断，不擅自扩成R40外硬目标；其他玩家的R20显示/R21导航仍发布。
- Scarlet Price原目标中心R3、本人轮廓/他人填充、源死亡隐藏，目标死亡仍可显示。Finish删首来源，Effect不删；伤害预测保持源Raidwide类型及列表首项判定钟。
- 拉拽仅发布位移线、落点与危险文字，无自动抗击退、无额外移动目标。Ovni冻结spell.Location与源Party0/caster hitbox；Ceto冻结caster位置，MinDistance为0。零方向无位移。
- IceAndLevin由15006清；Charybdis出生锁点、4秒判定、按来源恰19次效果消费，销毁不清。DualCast真实重复激活替换前实例，保留冷Actor顺序、float3.1延迟、全局FIFO、读时原地Risky/危险色和尾项晋升。
- HotTailSecond由14999真实效果生成，位置取当时caster、方向取完整包RotationInt，3.1秒后判定；15007清。旧录像缺方向时清旧段、保留诊断，不从Actor当前方向猜测。
- 凝视发布眼睛/瞳孔/视角弧，按实际范围联合计算面向，执行仍由统一Hints门面门控。驱散只有文字/标记与意图，不自动驱散，不扩成停手或禁攻。
- 源伤害预测仅作现有诊断，不新增治疗决策。回放禁止PR实际面向/动作、GreenMove及遥控；owner停止仅释放自己的区域、边界、绘制与请求。
- 六种显式选敌保留Boss=1、战斗候选=0、非战斗=-3，Ceto/Molech特殊小怪=-1及Phuabo/Tristitia双OID=1。Watcher保留活跃模块基础候选，不附加无模块自动提示的无敌状态过滤。实时采样传本人仇恨表；合法候选在完整优先级归一后先保留最高组原目标，再按X/Z距离选择，不以Y差改变同组最近目标。PendingDead的-1仍保护。无候选只撤销自己的请求；未新增禁AOE/禁DoT执行。
- 分摊导航按实际边界、伙伴位置或候选圈集合比较，相同输入保留原句柄，避免数组引用变化每帧重建。不同输入仍更新真实几何。
- 统一选择器只管理本批七种Loaded；未将其表述为已复刻含未迁BA在内的全区域候选选择。

## 公共效果包朝向

`NativeActionEffectCapture`复制`Header.RotationInt`到可空`NativeActionEffectEvent.RotationInt`，完整录制器/Reader自动往返。旧数据为null。只有真实完整包进入Hydatos效果状态机，PR按目标展开事件不重复消费；序号0的独立包也各自消费。

本轮实际读到的本地FFCS源码HEAD为`f76f33bdc572d69992ef771ea5a4cd26a8035a6a`，`ActionEffectHandler.Header.RotationInt`为ushort、offset0x1A。实际编译引用由测试反射核对同字段类型与布局。此前记录的运行DLL来源commit `c306aa6c980c38db9ad5155a9f219e2931f4f1c2`本地Git对象缺失，本轮没有成功读取该commit正文，不将本地源码HEAD表述为已证实相同版本。

## 验证证据

所有命令在`.tmp/halatali-verify/`复用SDK10.0.303，主项目CI构建，不部署，不改根SDK11配置。

- `native-hydatos-20261125-model-05.log`：独立Hydatos运行9场景通过，修正先绑定运行库数据再创建World的初始化顺序。
- `native-hydatos-20261125-model-07.log`：11场景通过，新增真实Feed免疫旧槽与真实Manager七源排序。
- `native-hydatos-20261125-model-08.log`：12场景通过，新增源AI分摊/分散/诱导逐点差分。标准部分34动作家族、170事件边界、67146形状点；RotationInt另穷举65536个raw值对照源float转换。
- `native-hydatos-20261125-build-03.log`：生产构建退出0、0错4既有Crucible警告。
- `native-hydatos-20261125-input-replay-01.log`：新增公共朝向采集/录制读回与PT实时联合专项通过，共7条PASS。
- `native-hydatos-20261125-replay-01.log`：七源冷世界、真实导航/边界、Finish/Cancel/超时、Seek/开关/owner及Tail/Daphne专项先通过；Ovni夹具玩家与拉拽原点同点导致位移投影断言失败，失败日志保留。后续复验使用真实非零位移夹具，未放宽断言。
- `native-hydatos-20261125-replay-02.log`：夹具将落点preview误当同时包含线，仍失败。后续分别断言一个落点preview、一条线及精确起止点，停止后线清零；`replay-03.log`退出0，17条PASS。
- `native-hydatos-20261125-model-09.log`：14个Hydatos模型通过，增加七源真实模块导航圆/方场逐点差分与动态单体目标/NPC钟。
- `native-hydatos-20261125-model-11.log`：退出0，32场景PASS/0FAIL，其中16个Hydatos、9个共享选敌、7个脚本控制。新增真实Feed无效目标归零及七源模块全优先级setter对照。
- `native-hydatos-20261125-build-05.log`：退出0，0错4既有Crucible警告，构建耗时20.58秒。DLL SHA256：`a34d292067ef13a34c9580f327446ec6fabeb4e108b33a37dc3c9ed08e26088c`。
- 同DLL的`native-hydatos-20261125-replay-05.log`：退出0，21条PASS/0FAIL，其中18条Hydatos及3条公共输入；新增重复事件/下一帧晋升、稳定分摊句柄、凝视切视角/回放禁执行、动态单体预测及Watcher状态/高度/仇恨候选。真实仇恨表内存需游戏内复核，离线夹具不冒充实机输入。

- 同DLL无筛选`native-hydatos-20261125-catalog-01.log`：进程退出0，1356条PASS/0FAIL，279入口逐文件独立编译，包含其他原生区域及公共输入/输出/控制消费者。

标准组件复用既有原生几何、分摊/凝视及Feed对照；本批未抽取全局万能运行框架。源自动选敌执行器正文另读`BossModReborn-7.56/BossMod/AI/AIBehaviour.cs`，该独立检出commit未证明与导入运行库一致；完整优先级及地图采用本仓真实模块oracle，单目标最近规则按已读源码适配。真实游戏内像素、位移物理与宿主执行仍需实机。
