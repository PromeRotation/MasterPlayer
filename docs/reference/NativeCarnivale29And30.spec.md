# 假面狂欢第29、30场原生脚本

本批使用 `Presets/MaskedCarnivale/Entries/Stage01.cs` 的既有区域入口，自行维护对象、读条、原生效果与状态账本。输出沿用 `DungeonEncounter → DungeonOutput → SafeZone/Draw`，不消费 BMR 桥结果。特殊移动保留来源引用、代际、位移类型、原点、方向及判定时间，不引入求解器内部依赖。

## 来源与身份

| 场次 / 幕 | 本机名称 | 上游目录 / 文件 | CFC → InstanceContent → Territory | Primary / NameID |
|---|---|---|---|---|
| 29 / 1 | 水与火之歌·第一幕 | Stage29RedFraughtAndBlue / Stage29Act1.cs | 698 → 35029 → 796 | 2C5B / 9239 |
| 29 / 2 | 水与火之歌·第二幕 | 同目录 / Stage29Act2.cs | 同上 | 2C5D / 9241 |
| 30 / 1 | 世界第一剑士——齐格弗里德·第一幕 | Stage30TheCatchOfTheSiegfried / Stage30Act1.cs | 699 → 35030 → 796 | 2C67 / 9245 |
| 30 / 2 | 世界第一剑士——齐格弗里德·第二幕 | 同目录 / Stage30Act2.cs | 同上 | 2C6A / 9245 |
| 30 / 3 | 世界第一剑士——齐格弗里德·第三幕 | 同目录 / Stage30Act3.cs | 同上 | 2C6C / 9245 |

上游路径前缀为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/`。五幕均为中心100/100、R16.01379的32顶点小场地，默认Primary拉战/完成生命周期。第29场含构造提示共25组件、20条逐幕读条规则；第30场含构造提示共29组件、30条逐幕规则。本机Lumina中文游戏表核对两场38种实际消费动作的 `ExtraCastTime100ms` 均为0；18964的 `Cast100ms=0`，仅真实Start产生其提示，不由Effect虚构读条。

第30场第二、三幕没有构造攻略组件，但战前场地和敌人标记仍显示；只有第一幕显示构造期攻略及成就文字。默认阶段不等待分身死亡。第29场第二幕两手红色；第30场第三幕三种分身橙色；这些模块没有自定义选敌优先级，不新增自动打断、净化或禁攻执行。

## 第29场机制

| 源组件 / 输入 | 原生模型与清理 | 当前输出 |
|---|---|---|
| 第一幕普通AOE：18694、18692、18698、18699 | R6、R5、R10、宽4前长74矩形；锁真实Start落点/朝向/判定钟，真实Finish清，效果/时间不清 | 普通危险导航及双视图 |
| PillarOfFlame：18696、18895 | 共享R8队列；99项排序，任意有效Finish按source删首项；迟旧结束不能跨代 | 普通危险导航及双视图 |
| FluidSwing：18689 / 18961 | 两幕各自独立Forward50，锁施法朝向，无目标/几何命中门槛、不撞墙；真实Finish撤销 | `CarnivaleKnockbackCast` 经公共位移投影，仅显示与出界提示 |
| 第二幕ProteanWave：18971、18984 | 共享R39、总角30°队列；其他规则同Pillar | 普通危险导航及双视图 |
| 第二幕普通AOE：18969、18985 | R5、R10，真实Finish | 普通危险导航及双视图 |
| BigSplash：18965 | 独立Away25，不串联FluidSwing；全队预测读取当前raw结束钟，存活0–63槽；不生成18966重复波 | 位移仅显示；Raidwide文字/预测 |
| Ferrofluid：18963、18962 | 全局单槽覆盖，KB6分别Away/Pull；Pull最多抵达中心，同点无位移。同代补正不重开；任意对应Finish只清KB | `CarnivaleFerrofluidCast` 保存整条读条；公共位移投影，仅显示 |
| FerrofluidAOE：18963、18962 Start；18974、18973效果 | 与KB独立的全局槽，分别Donut10–40 / R6；任意已存在source的两种原生效果清当前AOE，不区分原预测类型；无超时 | 普通危险导航及双视图；KB落点只检查该AOE及出界 |
| Pyretic：18691、SID960 Lose | Start填满64个人Stay要求；Finish/取消不清；任意actor任意960槽真实Lose清全部。逐actor先Cast再Statuses，保同帧顺序。死亡仅清正在查询的个人槽 | 完整 `CarnivalePyreticState`；Stop文字与SpecialMode诊断，保持源/旧MP消费范围 |
| Rush：18690 / 19028；Throttle：18964 | 纯CastHint，真实Finish；700原始占槽仅净化文字 | 仅提示 |
| Cascade：19022 | 活着的0–63槽、当前raw结束钟伤害预测 | Raidwide文字/预测 |
| 两手提示 | 只查首LeftHand，未死则优先；否则首RightHand未死；不搜索第二个存活同类 | 仅提示，不增加选敌setter |

Pyretic的960账本按真实slot/source判定Lose；不是按过期时间隐藏原始占槽。状态Gain不会恢复Stay。首次激活仅建立状态观察基线并重播当前读条，已取消raw不复活。源StayMove的移动比较错误未借迁移暗改；现有桥对SpecialMode只提供诊断，本批没有以提示推导PR ACR停手或移动保持。

## 第30场机制

| 源组件 / 输入 | 原生模型与清理 | 当前输出 |
|---|---|---|
| Bomb2C68 Create（1/3幕） | 每次出生保存Actor引用/代际、处理钟、量化出生点；R8，activation=处理钟+8.4d。死亡、位移、Destroy不清旧记录，同ID重建追加新代 | 普通危险导航及双视图 |
| RubberBullet（Bomb Create / 18847 Finish） | Create覆盖全局Away20；锁当时Primary原始位置、处理钟+6.3d。18847 Start/Effect不建立/清理，真实Finish清 | `CarnivaleSpawnKnockback` 保触发引用、类型、时间、原点；仅显示及当前Bomb/出界落点提示 |
| Explosion：18888 Finish | 任意真实Finish清全部Bomb；Destroy正在读该动作时先Finish，亦清 | 撤销对应危险/显示 |
| IceBoulder2CC6 Create（2/3幕）、18986原生效果 | 量化出生R10、处理钟+8.4d，死亡/Destroy/预计时间不清；任意已存在source的18986原生包清全部 | 普通危险导航及双视图 |
| Hyperdrive：18836/18837；Torch：18838/18839 | 独立R5队列、R34总20°队列，99项排序；任意有效Finish按source删各组首项 | 普通危险导航及双视图 |
| Sparksteel：18841/18897 | 独立R8队列，达到8项排序；仅首项activation+1d以内的严格连续前缀，遇等于/晚于deadline立即停止 | 普通危险导航及双视图，保源实际消费的风险颜色 |
| Swift普通AOE：18843/18844、SphereShatter19027 | R4、Donut8–20、R5，真实Finish，效果/超时不清 | 普通危险导航及双视图 |
| Swiftshift18842 | Away10，同组件多source连续推进、不撞墙；真实Finish撤销 | 共用击退模型/投影，仅显示；落点查全部18843/44及出界 |
| Sparksteel火池18840、1E8D9B | cast当前目标动态位置；真实原生效果冻结MainTarget当前点或packet目标点，处理钟+.8d；source缺席不发事件预测。live仅筛state!=7，量化显示；每个live每Update原位置≤2仅消耗首预测 | 三路R6普通danger，不是硬障碍 |
| MagicDrain18890、Ankle18846 | 纯CastHint；SID2166反射、564 Bind按原始占槽仅文字 | 仅提示，无ACR禁攻或自动驱散 |

Bomb Create与旧18847/18888 Finish按输入actor顺序交错派发；出生时保存原始Primary位置，延迟至公共读条扫描派发时不重新读取。正反顺序及Bomb先于Primary更新均有模型反例；实际DLL覆盖旧Finish先、新Bomb后的保留边界。

两场击退复用 `CarnivaleKnockbackImmunity`，严格expiry&gt;activation。raw回调使用旧完整队伍名单，初始化使用当前名单；Actor缺席而Party仍保留身份时不丢槽映射，Destroy清免疫，带状态同ID重现可重新Gain。免疫只撤位移投影，不删除普通危险圈。三类免疫来源、取消和owner生命周期沿用公共输出层，不发送原生免疫动作。

## 分层验证与边界

本批190项Carnivale集中模型通过，含两场24项、同帧顺序、名单缺席重现、来源缺失、代际、状态Lose、组队列、窗口边界与激活补播。生产编译0错误、4项既有Crucible警告。实际DLL通过真实ScriptManager/ReplayController和生产注册表，专项19条PASS（含2公共项），五幕各5772点对照源场地；覆盖危险时间窗、显示预览、取消/无超时、真实原生包、反向Seek、停用/停止owner清理。

普通危险的当前导航适配、特殊移动模型及显示数据已离线验证。Forward/Away/Pull没有来源或旧MP专用导航消费者，仍只发布投影和提示；Pyretic仅诊断。不得把这些显示当作强制位移导航或自动执行完成。真实PR移动、游戏物理、最终双视图像素与ARR实录时序仍待实机；回放不发送原生操作，未部署。

证据位于 `.tmp/halatali-verify/`：`native-carnivale-red-siegfried-model-review.log`、`native-carnivale-red-siegfried-build-review.log`、`native-carnivale-red-siegfried-focused-final.log`。实际DLL全目录集中回归另记录在同目录 `native-carnivale-red-siegfried-catalog-final.log`。

实际DLL全目录集中回归：801条PASS、0失败，包含本批17条专属检查；复用同一生产编译产物。
