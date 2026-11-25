# 天青斗场第4场第一幕与第5场原生脚本

2026-11-24。来源分别为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage04GentlemanPreferSwords/Stage04Act1.cs`、`Stage05TheThreepennyTurtles/Stage05.cs`。生产实现扩展 `Presets/MaskedCarnivale/CarnivaleCatalog.cs` 和 `DungeonCarnivale.cs`，复用现有 `Entries/Stage01.cs` 区域入口及其 GUID，避免同一796区域启动多个相同的 `DungeonScript` owner。设置仍按独立机制名称控制各场。

## 身份与场地

本机简中表确认：InstanceContent35004 ↔ CFC614「怪力铁巨人——克利俄斯」→ Territory796；InstanceContent35005 ↔ CFC615「金币龟兄弟」→ Territory796。Territory796 自身反指 CFC610，这是多场共用地图，不能拿该反向字段识别场次。

第4场第一幕仅接管 Primary25C8，其他绘制对象为25D2蝙蝠；第5场仅接管 Primary25CC，绘制该 OID 的全部实例。第4场第二幕25D5，以及第2/3场保持原有桥接。场地均固定世界中心 `(100,100)`：第4场第一幕使用源 `Layouts.CircleBig` 的 R24.5 正64边形；第5场使用 `CircleSmall` 的 R16.01379 正32边形。实际 DLL 分别与源静态场地作5,183点 Contains 对照。

## 模型和输出

| 场次 | 构造和战前 | 开战与战斗 | 完成条件 |
|---|---|---|---|
| 第4场第一幕 | 准备 Hints 保留原弱点、Ram's Voice/Ultravibration 及 Flying Sardine 建议；显示场地轮廓、固定 Primary 与全部蝙蝠 | 任意25C8/25D2 InCombat；撤准备提示，显示蝙蝠弱雷、狼弱火的 Hints2 | 两种 OID 的全部对象 DeadOrDestroyed |
| 第5场 | 准备 Hints 保留高防御、1000 Needles/Doom 和 Eerie Soundwave 建议；显示小场地和全部金币龟 | 任意25CC InCombat；撤准备提示；源没有战斗组件 | 全部25CC DeadOrDestroyed |

两场拉战不附加主怪可选性、敌对或 HP 条件，其他组员入战也会启动固定 Primary 的会话。启动后主怪死亡、HP0、不可选、脱战或销毁都不替代全组完成；主怪销毁后不将其他同 OID 对象改绑为 Primary。单个组员 HP0 而 Dead=false 仍保活。完成后不从死去的余留对象重新发布准备提示；真实新出生对象可开启下一次会话。

第4场第一幕只激活准备 Hints 和战斗 Hints2 两种组件；14361仅为枚举，不生成矩形、伤害预测或自动动作。文字中的打断建议不等于 ShouldBeInterrupted 意图。第5场开战后没有机制组件是源合同，不增加驱散、Doom、1000 Needles 或停手。两场保留基础选敌规则，没有专属优先级覆盖。

显示经 `DungeonAmbientAoe → DungeonOutput → Draw` 同时进入雷达和游戏世界；战前轮廓及标记仅显示，不建立规划场地或移动目标。开战后发布正式场地，源没有额外 AOE、目标或障碍导航约束。本场无特殊移动、面向、护盾禁攻或动作执行意图。

## 共用加载生命周期的后续补验

第7/8场迁移时，共用斗场模型补齐了源 Loaded/Pending 生命周期，生产实现见 `CarnivaleModuleState.cs` 与 `DungeonCarnivale.cs`。已知旧Party[0]时按逐对象位置直接加载首个近距对象；冷首帧尚未应用队伍表时按Pending倒序晋升，同OID占位、晋升先于卸载、死Pending丢弃及wipe保Pending均按源处理。未启动主对象销毁后只让已有候补在后续更新晋升，已启动主对象仍按全组完成。recommence不重建未新生的已卸载Owner。原固定首龟夹具现在显式先建立Party[0]；另有冷启动倒序和候补死亡复活反例，避免测试把错误首对象选择固定下来。

两场经最新215项集中模型及实际DLL全目录641条PASS/0失败补验；历史249项/628条证据继续保留。最新证据为 `.tmp/halatali-verify/native-carnivale-seven-eight-*`，来源及共用边界详见[第7/8场规格](NativeCarnivale07And08.spec.md)。

## 验证和边界

`tests/QuestAlphaCarnivaleRegression.cs` 新增两组定向模型，覆盖准备/战斗提示交接、任意组员拉战、固定 Primary、死亡/HP0/缺席、全组结束、新代和机制停用。

`tests/ScriptCompilation/NativeDungeonReplayAlphaCarnivale.cs` 通过实际 ScriptManager、ReplayController 和生产 DLL 验证场地 oracle、两视图预览注册、主怪销毁后保活、真实群体结束、逆向 Seek、准备阶段停用及 owner 清理。回放不发移动、面向或动作。

显示和源实际提供的场地输出已离线验证；没有将无导航或无动作意图扩成执行。游戏内最终像素和宿主运行仍待实机验收。集中证据见 `.tmp/halatali-verify/native-bar-four-five-*`。

第4场第二幕随后已独立迁移，见[04/08第二幕及10场规格](NativeCarnivale04Act2And08Act2And10.spec.md)。原04第一幕的群体结束合同不套用于第二幕；普通展示切换保留后台cast和取消历史，真实wipe仍撤销Owner。
