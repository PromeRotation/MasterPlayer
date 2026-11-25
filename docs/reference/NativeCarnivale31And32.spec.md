# 假面狂欢第31、32场原生脚本

两场沿用 `Presets/MaskedCarnivale/Entries/Stage01.cs` 的796区域入口。状态由原生 `DungeonEncounter` 维护，经 `DungeonOutput → SafeZone/Draw` 输出，不读取 BMR 桥结果。模型保留完整读条、来源、代际、原始姿态、判定时间及结束条件；几何与当前导航的转换集中在输出适配层。

## 来源与身份

| 场次 / 幕 | 本机名称 | 上游目录 / 文件 | CFC → InstanceContent → Territory | Primary / NameID |
|---|---|---|---|---|
| 31 / 1 | 黄金头巾——模仿师格格·第一幕 | Stage31AnythingGogos / Stage31Act1.cs | 754 → 35031 → 796 | 30F5 / 9908 |
| 31 / 2 | 黄金头巾——模仿师格格·第二幕 | 同目录 / Stage31Act2.cs | 同上 | 30F7 / 9908 |
| 32 / 1 | 黄金斗士——金德尔·第一幕 | Stage32AGoldenOpportunity / Stage32Act1.cs | 948 → 35032 → 796 | 3FA5 / 12471 |
| 32 / 2 | 黄金斗士——金德尔·第二幕 | 同目录 / Stage32Act2.cs | 同上 | 3FA7 / 12471 |

上游目录前缀为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/`。第32场第二幕的源类型实际名为 `BossMod.Global.MaskedCarnivale.Stage32.Act2.Stage31Act2`，沿用源声明登记，不能根据文件名猜类型。

本机Lumina表核对上述CFC、场次、区域和OID；26种第31场消费动作、21种第32场消费动作的 `ExtraCastTime100ms` 均为0。四幕均使用中心100/100、R16.01379的32顶点小场地和默认Primary拉战/完成生命周期。第一幕有构造攻略，第二幕没有攻略但仍有预战场地及Primary标记。第31场含构造共22组件、25条逐幕读条规则及23127真实效果；第32场含构造共29组件、29条逐幕读条规则及34452/34454真实效果。

## 第31场

| 机制 / 输入 | 原生模型与时序 | 输出与边界 |
|---|---|---|
| 第一幕Sap23101/23105、Protean23112、FireBlast23110 | Sap共享R8组；Protean R50总30°；FireBlast前长70.5宽4。锁Start姿态/钟，真实Finish撤销；组按来源删首项 | 普通危险及双视图，普通导航 |
| 第二幕MeteorSmall23121 | R5，真实Finish；效果/预计时间不撤销 | 普通危险及双视图 |
| MeteorMedium23131/23122/20055 | 共用R8组，99项排序；任意有效Finish按source删除首项 | 普通危险及双视图 |
| MeteorLarge23123/23129 | 独立R16列表；组规则同上，不能与R8合并 | 普通危险及双视图 |
| Fire23117、Blizzard23118 | Fire Start覆盖全局单槽，锁R8落点，activation=FinishAt+5.1d。Fire Finish、效果和超时均不清；任意23118真实Finish清当前槽 | 普通危险、时间窗及双视图 |
| Pyretic / SID960 Lose | Fire Start填满64个人Stay要求。任意真实960 Lose清全部，死亡只清查询槽；逐actor先Cast后Statuses，保留同帧顺序 | `CarnivalePyreticState`保留机制语义，Stop文字及SpecialMode诊断；源及旧MP未执行，不新增ACR停手或移动保持 |
| Icestorm23126 → 23127真实效果 | Start覆盖独立全局槽，FinishAt+.9d预测；取消/超时不清，存在source的任意真实效果清，零目标包有效 | 存活0–63槽伤害预测及文字，没有普通地面危险圈 |
| Flare23128、Holy23130、MeteorBig23124 | 独立当前raw结束钟，存活0–63槽 | Raidwide预测及文字 |
| ThunderIII23119、1E9684实体 | 动态读条目标、真实原生主目标/包点和live三路R6；效果冻结处理钟+.8d，source缺席不生成预测；live仅筛state!=7，每实体每帧按原位置消耗首个≤2预测 | 普通危险及双视图；不是硬障碍 |
| Maelstrom30F9 | 全实体原引用，不筛Dead/Targetable/EventState；动态原始位置R8硬障碍 | 双视图及障碍导航，保留GTFO文字；销毁撤销 |
| Mimic23097、Doom23113、Raw23115、防御23098/23100/23102/23103、打断23114 | 纯提示/打断意图；2450、1797、1107、268按原始占槽提示 | 不新增禁攻、净化、防御、自动打断或治疗执行 |

两幕只绘Primary，不额外标记Imitation或Maelstrom为敌人；障碍仍按机制输出。未激活的23125拉近、23108传送及其他枚举动作不接管，不从注释虚构执行。

## 第32场

| 机制 / 输入 | 原生模型与时序 | 输出与边界 |
|---|---|---|
| Blast34450、Burn34446、Thunder34455 | 前长60宽10、R10、R6，锁Start，真实Finish | 普通危险及双视图/导航 |
| Fire34448/34449第一幕；34449第二幕 | 第一幕共享R8组，任意有效Finish按source删除首项；第二幕独立SimpleAOE，其他action Finish不能误清 | 普通危险及双视图/导航 |
| Cross34465、Swing34467 | Cross长100/半宽3.5，R12；保朝向和真实结束 | 普通危险及双视图/导航、危险文字 |
| Quake34457/34458/34459 | 34457 Start追加无来源绑定的完整序列；只输出当前R10→环10–20→环20–30。真实Finish（含取消）按order及逐轴≤1匹配首条，下一钟=处理钟+1.5d，第三段清；不按超时跳段 | `CarnivaleQuakeSequence`保存原StartCast、Origin、Generation、Order、Activation；普通导航/双视图 |
| Aero34460、Rush34468、Pull34466 | 三独立组件，Away10/Away10/Pull30；无半径命中门控、不撞墙，Pull最多抵达中心，同点无位移。同组件多源串联，组件之间独立从本人起算；真实Finish撤销 | `CarnivaleKnockbackCast`保存来源/代际/规则/钟/类型；公共位移投影仅显示。Aero落点检查第一幕Burn/第二幕Cross和出界，Rush/Pull只查出界；不发布击退导航 |
| Aero/Rush伤害 | 当前raw结束钟、存活0–63槽 | 独立Raidwide预测，不添加普通R50危险圆 |
| Gravity34451→34452、Thunder34453→34454 | 两独立全局槽；第一幕FinishAt+(double).8f，第二幕+.8d。取消/超时不清；存在source的真实效果只清对应槽，零目标有效 | 延迟Raidwide预测及文字 |
| GoldenBeam34464 | 当前原始Marionette3FA8引用恰1时覆盖单R40总120°扇形，包含死亡实体。其他数量逐Start追加，恰4条发布缓存，第5条不重算；任意真实34464 Finish清Inputs/Output | 模型保存完整原始读条数组；几何缓存仅在公共输出转换，普通导航及双视图 |
| Rupture34463 | 有序保留原caster引用/读条代际，取首caster当前raw的NPCRemainingTime。后开始但更早deadline不能抢首，首项Finish后晋升 | 动态一位小数倒计时，只有提示，不生成危险/伤害预测 |
| Summon34462、打断34589、状态3779/240/3621 | 纯提示、打断意图及原始占槽文字；第一幕火球存活提示 | 不增加禁攻、自动水晶/净化、Loom、打断或动作执行 |

GoldenBeam四路不是四扇形并集：按源使用edges=2的ConeV折线多边形，逐项Intersection与逐项Xor后再Union，覆盖1/3/4路为危险、2路安全。`CarnivaleGoldenBeamGeometry`复用现有vendored Clipper2（无BossMod模型依赖），保2^20整数缩放、NonZero、PreserveCollinear=false、每步float往返及源差空集步骤。外环/孔洞保留；填充分条处理布尔结果的自接触顶点和孔洞，避免简单耳切失败停止脚本。单Beam仍用真正扇形，不降为折线。

第一幕另画Fireball3FA6、Slime3FAB橙色标记；第二幕只Primary，不新增水晶/小怪标记或选敌优先级。未消费的GoldFever34469、Teleport34129、34461等保持未接管。

## 验证与待验

215项Carnivale集中模型通过，覆盖本批25项及已有全部连续机制。生产编译0错误、4项既有Crucible警告。实际DLL专项21条PASS（含2公共项），经过真实ScriptManager/ReplayController及生产注册表；四幕各5772点对照源场地。GoldenBeam三种布局各10080点源Check与真实填充双对照，并分别检查48/216/24个边两侧0.001码偏移点，覆盖孔洞、自接触和四路重合；这不是最终像素或任意浮点边界等价的证明。

模型和公共适配已覆盖同帧actor顺序、组Finish、未知来源真实效果、状态原槽、代际/取消、迟激活补播、超时不撤、反向Seek、机制停用和owner清理。普通危险/障碍导航数据及双视图几何已离线验证。Forward/Away/Pull仍遵循源及旧MP仅显示/提示消费；Pyretic仍为诊断，不将箭头或暂停冒充特殊移动导航/执行。

证据在`.tmp/halatali-verify/`：`native-carnivale-gogo-goldor-model-final.log`、`native-carnivale-gogo-goldor-build-final.log`、`native-carnivale-gogo-goldor-focused-final.log`及`native-carnivale-gogo-goldor-catalog-final.log`。实际DLL全目录820条PASS、0失败，复用同一生产DLL。ARR实录顺序、真实PR/GreenMove与游戏物理、最终雷达/世界像素仍待实机；回放不发送原生操作，未部署。
