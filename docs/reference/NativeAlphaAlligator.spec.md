# 家人的温暖·首领短吻鳄原生脚本

2026-11-24。来源为 `MechanicRuntime/Upstream/Modules/Dawntrail/Quest/MSQ/TheWarmthOfTheFamily/AlphaAlligator.cs`，父组件为 `Components/Tethers.cs` 的 **InterceptTether**（非 InterceptTetherAOE）及 `GenericAOEs.cs` 的 SimpleAOEs。生产实现为 `Presets/Quests/MainScenario/DungeonAlphaAlligator.cs`，入口为 `Presets/Quests/Entries/TheWarmthOfTheFamily.cs`。不消费 BMR 桥的机制输出。

## 身份和场地

本机简中表：Quest70785「家人的温暖」的 `QUESTBATTLE0=5081` → InstanceContent5081.CFC1009 → CFC1009.Territory1244；Territory1244 反指 CFC1009。源模块 Primary 为 **4632**，不是4637；源标题 NameID13593 为「首领短吻鳄」，不将模块标题当作 Primary 的名称外键。仅接管 T1244/Primary4632，其他模块继续由既有宿主处理。

场地复制源的60个世界顶点，保留凹边界及包围盒中心 `(412.745,-110.995)`；不改成矩形、凸包或圆。实际 DLL 使用源静态 ArenaBoundsCustom 作 oracle，覆盖6,643个点。

源 DrawEnemies 按集合 `4633/4637/4635/4634/4636` 绘制，排除Primary4632；死亡和不可选实体不画。复用 MP 任务敌人 marker 输出及场外轮廓规则，仅显示、不新增目标站位。源没有特殊选敌优先级、护盾禁攻、面向、自动动作或特殊位移，本场不添加这些能力。

## 激活组件和机制模型

| 组件 | 输入和模型 | 结束及输出 |
|---|---|---|
| CriticalBite | 任意来源40563真实Start；锁LocXZ、rotation、FinishAt；R10、总角120° | 真实Finish按source结束；无超时清除、无伤害预测。普通危险区在雷达/游戏世界显示并参与导航；圈内 GTFO |
| FeedingTime | 84/17均接受；保存Player/Enemy原Actor引用，按存活Raid是否包含source确定双方；槽bit直接Set/Clear | 断线重新解析双方，仅删除首个相同引用二元组。目标已销毁则解析失败，不替源清残留；不按死亡或effect清线 |
| FeedingTime时钟 | 初始default；仅40562真实Start将共享时钟设为FinishAt+1.2秒；多来源最后写入 | 40561 visual、Finish、40562 effect、断线均不清时钟。同代packet补正不当新Start；真实raw变化保留Finish→Start |
| FeedingTime导航 | 任意线使Active后，在4635列表中取首个逐轴距`(403,-105)`≤1的来源；不筛dead/targetable，不按观看者是否已接线跳过 | 重新Find来源当前Tether.Target；找不到目标便不导航，不尝试下一个来源。仅导航，不造矩形预览或伤害预测 |

导航允许矩形从 `target.Position + (target.HitboxRadius+.1) * target.DirectionTo(source)` 到 `source.Position`，全宽1。位置、hitbox、方向均保留原始精度；允许矩形包含边界。模型保留真实来源/目标引用、当前单条Tether、共享判定钟及撤销条件；几何通过 `DungeonAmbientAoe → DungeonOutput → SafeZone` 公共输出适配。旧 MP 桥 `MechanicHintsConvert.TryConvertComplement` 可解析源 SDInvertedRect 的标量字段，`MechanicNavigationHost` 将其标为 Goal，`MechanicNavDiffer` 以优先级1、Nearest 注册内矩形持久安全目标。原生现保留该实际消费语义：`Safe=true, Display=false, GoalPriority=1`；不将整场矩形补集发布为硬危险。

目标hitbox外推越过source时，按最终from→to反向构建，不能裁成零长。若最终端点恰重合，源SDInvertedRect产生NaN方向且不同查询互不一致，没有可迁移的有限导航合同；MP保留实体、连线、时钟及提示，省略此项导航，不构造任意方向矩形、不新增暂停，也不让零长几何使整场枚举异常。两种零长输入及反向端点已有模型反例；实机是否产生此输入未证，退化导航不声明已支持。

`AlphaActorState` 表示出生代：幸存者更新原引用的快照，销毁后冻结；相同ID再次出现建立新引用。持久世界字典保留缺席实体销毁顺序；蜥蜴的 Enemies 列表惰性建立后保持幸存者顺序，新实体追加，避免字典复用空槽改变首个来源。单Tether按采样字典首个非零目标归一，与现有Feed的单条输入一致；没有引入多连线并行解释。

raw新实体先Tether后CastInfo；既有实体先更新CastInfo再替换Tether；组件激活重播每实体Cast再Tether。typed OpTether 即使相同值也先Lose再Gain；source销毁先Untether再Finish，目标先销毁可能留下旧线。父类mask不改为引用计数：同槽两线断一条也直接Clear，旧线仍可能存在。源excluded4632只影响线色，不过滤接线、提示或导航。连线通过 `DungeonOutput` 的 `Draw.AddLinePreview` 同时供雷达和游戏世界消费，存活Raid目标且非excluded为安全色，其余危险色。

## 会话和执行边界

拉战仅 Primary4632.Targetable && InCombat，不额外要求HP>0或非Dead。结束仅首次缓存4637原引用的IsDead，或五类战斗对象全部Destroyed；Primary死亡/HP0/缺席/脱战、4637仅HP0、五类全部死亡均不能替代此条件。状态机检查先于首次缓存4637，因此首次见到已死4637延后一个检查周期结束。完成标记按真实raw新生解除；Primary先缺席、之后完成、相同ID再出生亦可建立新模块。Director0005/0011、机制停用和owner停止沿现有公共生命周期清理。

本场只提供普通步行站位导航，不调用原生操作。没有强制位移、跳跃、免疫动作或停手意图，未扩展自动执行范围。default判定钟保留在模型，由公共输出适配为立即persistent目标；未配置raw变化不能提前清除已取消读条的记录，迟到旧packet不能复活机制，有效新raw提交后才撤旧记录。

## 验证

- `tests/QuestAlphaCarnivaleRegression.cs`：形状、原始端点、共享钟、已接线观看者、NPC/excluded线色、直接清槽、两种历史销毁序、旧引用与同ID新生、缓存列表顺序、raw取消/未接管动作/旧包/真实新Start、独立终结条件；实际 `DungeonOutput` 和 `SafeZoneSolver` 通过优先级1安全目标进入窄矩形并撤销。
- `tests/ScriptCompilation/NativeDungeonReplayAlphaCarnivale.cs`：使用实际生产 DLL，检查真实线预览注册表、普通AOE预览与导航分离、源场地oracle、主怪死亡销毁保活、反向Seek、wipe、机制开关及owner清理。
- 集中证据位于 `.tmp/halatali-verify/native-alpha-stage-model-final.log`、`native-alpha-stage-build.log`、`native-alpha-stage-focused.log`、`native-alpha-stage-catalog-final.log`。

本批 `native-bar-four-five-*` 集中验证补验上述旧桥 Goal 消费和 P1 共用模型，替代早前禁外硬约束的扩大。显示及当前导航的离线覆盖与实际GreenMove执行验收分开记录。合成回放中没有发送真实移动、面向或动作；真实宿主控制权、物理移动、游戏世界最终像素仍需实机验收。
