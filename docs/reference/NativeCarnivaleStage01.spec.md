# 天青斗场第1场「出道战」原生脚本

2026-11-24。源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage01AllsWellThatStartsWell/Stage01.cs`、同目录上层 `Layouts.cs` 及 SimpleAOEs 父组件。实现为 `Presets/MaskedCarnivale/DungeonCarnivale.cs`、`CarnivaleCatalog.cs`，入口 `Entries/Stage01.cs`。

## 地图身份和接管

本机简中表 CFC610「出道战」→ Content35001/InstanceContent35001，反指CFC610；CFC610.Territory796，ContentType27/InstanceContentType13。BNpcName8077「斗场无头骑士」，源Primary25BE，Slime25BD。

T796为天青斗场共用地图，不能凭其反指CFC610就将同区其他场次认作第1场。本实现以T796＋Primary25BE精确接管，未伪造独立的实时CFC输入；25BD与其他场次Primary均不在模块Owns内。`DungeonScript.PreservesTerritoryMechanics`保留其他桥模块，宿主仅对25BE增加战前sleep例外，以接管源构造即激活的提示及显示。

## 机制和会话

| 组件/阶段 | 源合同及MP输出 |
|---|---|
| 构造战前Hints | 保留“This stage is trivial.”及后续使用任意技能的文字；显示场地outline和固定Primary＋所有Slime，均不参与导航 |
| 拉战 | 两类敌人任意实例InCombat，不附加Targetable、非Dead或HP>0；拉战停用战前Hints并激活IronJustice |
| IronJustice14199 | 真实Start锁LocXZ、rotation、FinishAt，R9.5/总角120°；按source真实Finish清除，预计结束后仍保留；无伤害预测 |
| 结束 | 两类所有实例全部DeadOrDestroyed；Primary先死、先销毁、脱战或HP0不得提前结束，Slime仍活则保持固定Primary会话 |

6497/6499普攻、14198单体伤害只在源枚举存在，未激活组件，故不新增AOE或伤害预测。源没有特殊优先级、护盾禁攻、面向、停手、移动控制或自动动作。普通导航沿公共安全区模型输出，不新增特殊执行能力。

场地是中心`(100,100)`、R24.5的正64边形，使用`sin/cos(i*2π/64)`；不是R25或数学圆。源`IsCircle`不改变Contains的多边形判定。生产DLL与源Layouts.CircleBig比较5,624个点，模型另覆盖圆弧与边中点之间的差异。

战前以仅显示多边形预览提供边界，不向安全区发布正式导航场地；拉战后由`DungeonEncounter.Arena → DungeonOutput → SafeZone`发布。敌人marker保持场外轮廓和Custom轴向不投影例外；死亡/不可选不画。固定Primary不因另一只同OID更近而更换。完成后同一仍在场实体回血不重拉；真实absence/new birth解除完成身份，包括Primary早已销毁、群体后来结束、同ID再出生。

raw CastInfo的真实字段变化与typed同代补正分开：取消后稳定旧raw或迟到旧packet不能复活；未接管raw转换保留旧记录，随后真实有效raw可重启；仅有效读条提交后清除旧取消记录。来源/位置/朝向/判定钟和结束条件仍由模型保存，公共层负责几何和导航投影。

## 验证和待验

`tests/QuestAlphaCarnivaleRegression.cs`覆盖共享区域所有权、战前显示无导航、64边形边界、群怪拉战、不可选Primary、固定身份、全部DeadOrDestroyed、取消/迟到包/raw变化、真实新生、wipe及停用。`tests/ScriptCompilation/NativeDungeonReplayAlphaCarnivale.cs`通过实际生产DLL验证双视图预览和导航、5,624点源oracle、主怪死亡销毁后的群体保活、反向Seek、开关和owner撤销。

集中日志：`.tmp/halatali-verify/native-alpha-stage-model-final.log`、`native-alpha-stage-build.log`、`native-alpha-stage-focused.log`、`native-alpha-stage-catalog-final.log`。离线验证使用合成录像，不能替代游戏内真实场次、宿主控制权、GreenMove和世界最终像素验收；回放不发送原生操作。第2/3场及其他Acts仍保留原状态，本场不声明其打断提示已经迁移为执行。
