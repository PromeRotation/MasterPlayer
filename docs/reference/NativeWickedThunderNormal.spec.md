# 阿卡狄亚轻量级4普通：Wicked Thunder 原生机制

入口为 `Presets/Dungeons/Entries/WickedThunderNormal.cs`，GUID `masterplayer.raid.wickedthunder.normal`。生产模型在 `Presets/Raids/WickedThunder/`，经 `DungeonScript → DungeonEncounter → DungeonOutput → SafeZone` 输出；生产不实例化 BossModule，不读取运行库组件输出。

权威来源为 `MechanicRuntime/Upstream/Modules/Dawntrail/Raid/M04NWickedThunder/` 及其直接使用的 `GenericAOEs`、`SimpleAOEs`、`BaitAwayCast`、`StackWithIcon`、`SpreadFromCastTargets`、`RaidwideCast`、`ActorState`。源 CFC991、主 OID `0x4263`；生产 Territory1231，专项要求本机客户端表双向核对 CFC 与 Territory。

## 实际激活的十三个组件

| 源组件 | 原生状态与输出 |
|---|---|
| ArenaChanges | 默认中心100/100、40×40方场；Map index0的00020001改中心85/100、10×40，00200010改中心115/100、10×40；00400004/00800004恢复 |
| WickedHypercannon | 仅缩场后37553/37554生成20×40绿色安全显示，锁中心X及Z80，读条结束加0.6秒；37555独立累计恰十次撤销，不按Finish或超时删除；保留全队伤害诊断，无新治疗策略 |
| WickedJolt | 37576动态原Source/Target引用、长60宽5矩形；Finish保留，真实对应Source的Effect删除首条；点名本人仅轮廓及向队友避让禁向，其他人按完整矩形避让 |
| WickedBolt | Source图标316、R5精确八人、处理钟加5秒；独立五次37571效果计数，单记录目标转移不刷新判定钟 |
| WickedCannon | 60真实状态槽2970；三/四/五连Start更新延迟表，其他Spell Start清延迟表；前二显示、首项Risky与原列表危险色；五种真实效果全局FIFO晋升 |
| WrathOfZeus | 37575只提示和存活成员伤害诊断，钟随原CastInfo推进；不画全屏危险圆 |
| SidewiseSpark | 四种读条及Replica Timeline11D8/11D6的半圆，Burst矩形；显示前二、相同钟共享风险；返回副本而非修改原列表颜色；六种Sidewise/Burst的真实Finish全局FIFO |
| SoaringSoulpress | Source图标161、R6精确八人、处理钟加5.4秒；独立一次37569效果消费，不与WickedBolt共用计数或名单 |
| StampedingThunder | 37547/37548完整效果锁量化95/80或105/80、原caster方向、40×30矩形及处理钟加9.4秒；只有对应缩场Map撤销 |
| BewitchingFlight | 37560长40宽5锁读条落点及方向；只消费自己的WatchedAction Finish |
| Thunderslam | 37574锁落点R5，只消费自己的WatchedAction Finish |
| Thunderstorm | 37573原目标引用、R6显示/R7导航；按WatchedAction的Finish目标删除，不以来源替代目标 |
| WitchHunt | 37557锁落点、读条结束加6.3秒；全列表显示、前十二Risky和原列表危险色；37558真实效果全局FIFO，保留隐藏尾项原钟 |

普通读条、Source/Target原引用与同ID新代分开。状态冷激活按各Actor先Cast后真实槽，热状态排除Pet；新Party在Actor操作之后提交。缺观看者仍维护全局历史，个人导航需有效队伍槽。预战只画场地和主怪，不注册战斗边界或危险。主怪死亡、HP0或销毁结束；旧对象回血不重新激活，新出生可重新加载。回放逆向Seek重建模型，停止按owner释放显示、危险与控制。

## 源时钟与差分方法

首次失败 `native-wickedthunder-20261125-model-repro-02.log` 已稳定复现。诊断 `native-wickedthunder-20261125-clock-diagnostic-03.log` 给出七个目标100、102～107，实际剩余时间均2,999,999 tick，源 `ActorCastInfo.NPCRemainingTime` 同值；旧夹具常量 `.3f` 为3,000,000 tick。

源 `Actor.cs` 先计算float `TotalTime + NPCFinishDelay` 再减 `ElapsedTime`。3秒读条饱和时真实结果为 `0.29999995`，不能用代数化简后的`.3f`替代。生产算法保留；测试直接调用源属性精确比较目标身份、人数及DateTime，不扩大容差。另覆盖1、3、8.3、12秒读条的连续float推进及饱和。

新增事件差分随后发现独立的生产精度差异：WitchHunt将额外6.3秒加在已经转换为DateTime的读条钟上，形成两次tick截断；源先计算 `NPCRemainingTime + extraDelay` 再一次转DateTime。首事件相差1 tick，见 `native-wickedthunder-20261126-model-07.log`。Hunt与Hyper现均保留源一次转换的运算次序。Cannon的0x2D4保留源`AnglesCardinals[1]`的-0.003度，未知Param仍为零；不以看似正南的方向替代源微小偏角。

公共脚本的`BattleEnded`保留活跃M4历史，后续真实Effect仍消费；Director wipe与主怪真实完成才清理。缺失Actor的新分摊目标是明确的输入降级：源将null写入强制非空目标，无法安全绘制/导航；原生保留旧目标与原判定钟并发诊断，等待可定义的目标输入。不声称该异常输入逐字段等价，不猜新位置。

`tests/WickedThunderRegression.cs` 的源事件差分直接创建真实WitchHunt、SidewiseSpark、WickedCannon组件，在40个Start/Finish/Status/Effect边界对比条目数、顺序、位置、方向、判定tick、颜色、Risky和几何类型/全部尺寸参数，并比较七个形状点。Sidewise覆盖三来源同时排队、相同和不同判定钟、隐藏第三项及跨来源FIFO结束。源仅作测试oracle，使用最小WorldState与PrimaryActor依赖，不与生产侧共用算法；首次不一致报告事件和字段。伤害预测范围仍沿用MP诊断消费，不增加治疗策略。

## 验证入口与验收边界

模型筛选为 `--filter=WickedThunder`。实际DLL专项为 `--native-replay=WickedThunder`，已接入集中目录；需要 `FFXIV_SQPACK` 指向匹配本机客户端sqpack，缺数据失败，不静默跳过。专项覆盖真实生产脚本、事件路由、状态槽、原引用、独立效果、双视图共享预览、导航注册、真实SafeZoneSolver、逆向Seek及owner清理。

本批生产构建 `native-wickedthunder-deepzones-20261126-build-02.log` 退出0、0错误/4项既有警告，DLL SHA256为 `67bfe785916068325338b9ecb2fb149d90877378c42f853b379f8e043e839884`。使用已有SDK10.0.303隔离目录运行，未改根SDK11要求。`native-wickedthunder-20261126-model-final-11.log` 退出0，58项相关模型PASS/0FAIL；`native-wickedthunder-deepzones-20261126-replay-02.log` 退出0，64条PASS/0FAIL，含十条M4、EO/PT/HoH楼层及PT实时路由联合与三组旧追击。实际DLL另精确比较Hunt原始十四钟、Hyper九击后原钟、源Cardinals微角；每次Seek前后与停止前断言RouterErrorCount为0，避免隔离异常被重建吞掉。

ARR/MP回放只规划绘制；真实PR/GreenMove、游戏物理与最终像素仍需实机验收。

同DLL的`--catalog-replay`集中目录验证为`native-wickedthunder-deepzones-20261126-catalog-03.log`，退出0、1028条PASS/0FAIL。不带该参数的入口独立编译在既有FATE引用内部常量处失败，另存`catalog-02.log`，没有计入通过结果。
