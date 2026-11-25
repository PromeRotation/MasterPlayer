# 天青斗场第11/12场 MP 原生规格

## 来源、身份与入口

来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage11SomeLikeItExcruciatinglyHot/Stage11Act1.cs`、`Stage11Act2.cs` 及 `Stage12ThePlantomOfTheOpera/Stage12Act1.cs`、`Stage12Act2.cs`。四个模块共8个实际激活组件：第11场两幕和第12场第一幕各有一个构造Hints，第12场第二幕有构造Hints和四个战斗组件。枚举不等于激活组件。

本机简体中文sqpack及匹配Lumina核实：CFC621「爆前速攻」↔Instance35011，CFC622「寄生植物——火花草」↔Instance35012，两者ContentType27、Territory796。第11场主OID2718/2719，源NameID2280=雷汞弹怪；第12场主OID271A/271B，源NameID8103=斗场小红花、8102=火花草。共享Territory796的反向CFC610不用于识别场次。未用AOZContent行号推算直接外键。

沿用原脚本GUID `masterplayer.carnivale.stage01`，四幕分别提供独立机制开关。生产模型继续使用 `CarnivaleCatalog`、`DungeonCarnivale`、`CarnivaleModuleState` 和 `CarnivaleInterruptCast`，几何经 `DungeonOutput` 发布SafeZone与双视图，无BMR运行输出消费、无新求解器依赖。

## 分幕机制表

| 模块 | 构造提示/场地/绘制 | 拉战与完成 | 战斗机制 |
|---|---|---|---|
| 11 Act1，2718 | Sticky Tongue聚集、攻击中断、弱风抗火攻略；CircleBig；画所有2718 | 任意2718进战拉战，所有2718 DeadOrDestroyed完成；HP0不代替Dead；开战移除Hints | 无实际AOE、打断、动作或伤害组件 |
| 11 Act2，2719 | 四炸弹聚中央及持续AOE中断攻略；精确Layout4Quads；画所有2719 | 任意2719进战拉战，所有2719 DeadOrDestroyed完成；开战移除Hints | 同样没有Fulmination组件 |
| 12 Act1，271A | Ice Spikes/Bomb Toss及下一幕弱水抗土攻略；CircleBig；画所有271A | 默认Primary可选中且InCombat拉战，Primary Dead、Destroyed或HP0结束；开战移除Hints | 14750只有枚举，无实际战斗组件 |
| 12 Act2，271B | 含Primary.Name的Bomb Toss眩晕、Ice Spikes及弱水抗土攻略；CircleBig；固定Primary敌色，所有271C为Object色 | 默认Primary可选中且InCombat拉战，Primary Dead、Destroyed或HP0结束；不等Roselet；开战移除Hints、激活下列四组件 | 14751、14754普通AOE；14752纯提示；14753仅眩晕意图 |

默认Primary阶段与群体阶段分别消费原始对象身份。271A同OID候补可能在原Primary完成后的下一次更新加载，不能把此新模块的启动误作旧Primary未结束；模型和生产回放分别验证原Primary不等待其他同OID存活者，以及Pending实际交接。

| Action | 来源组件/参数 | 输出、开始与结束 |
|---|---|---|
| 14751 WildHorn | SimpleAOEs，R16.96、总角120° | 锁CastInfo.Loc/Rotation的扇形，实际危险导航及双视图 |
| 14754 Trounce | SimpleAOEs，R46.96、总角60° | 独立锁定扇形，不按Actor转向更新 |
| 14752 SporeSac | CastHint，原文 `Calls Roselets. Prepare Ice Spikes if available.` | 仅文字及原始读条；没有50码AOE、伤害预测、打断或自动Ice Spikes |
| 14753 InflammableFumes | CastInterruptHint(false,true) | 完整caster引用/代际/时钟，`ShouldBeStunned=true`、`ShouldBeInterrupted=false`，文字为`Stun!`；没有50码AOE或自动Bomb Toss |

四组件都只在拉战激活，激活时补播当时已有读条。匹配Finish、取消、打断及销毁按真实输入撤销；Effect和预计截止不清。对象移动不改变已锁几何；raw Loc/Rotation/Target/TotalTime真变化则旧Finish→新Start。沿用[04/08第二幕及10场规格](NativeCarnivale04Act2And08Act2And10.spec.md)的代际、后台模块、迟旧Finish和初始延迟事件约定。

第11场14583是23秒Fulmination枚举，源没有激活对应AOE或CastInterruptHint；不将攻略“攻击可中断”转成自动动作或敌方打断标志。原生模块活跃时，`DungeonScript.HandlesCast`只在2718/2719精确拦截14583的通用AOE推断，防止迁移后额外生成60码危险；不向Casts目录加入假组件，不吞未知动作、其他幕或玩家/伙伴/友方NPC。预战仍按原有通用躲避边界处理。

## 选敌、颜色与控制范围

12 Act2对所有合法PotentialTargets设271C=1、其他=0，保留Pointless(-1)不可覆盖。不是仅枚举中的怪物才有候选资格；ObjectIndex、死亡、友方过滤沿用原生公共入口。11两幕及12第一幕没有自定义选敌。

Roselet颜色使用源默认 `ColorConfig.ArenaObject=0xff0080ff`，即RGBA(1,128/255,0,1)；Primary为默认红色。场地、轮廓和AOE继续使用公共适配；Layout4Quads复用此前已按源导出的四孔、166三角形，不新增简化几何。

14753的Enemy标志投影与原始caster列表分开，死亡/不可选中只撤Enemy标志，Pointless不撤标志；只要组件和读条仍存在，就可继续显示Stun!。当前MP没有消费本场打断/眩晕标志的自动动作执行器，维持旧桥实际范围，不借攻略增加技能、持续移动、停手或面向控制。

## 首次raw确认的共用修复

首次packet锁点A、随后同代首次raw锁点B时，原Carnivale分支缺少BarPassage已有的差异确认，会永久保留A。本批把相同确认路径用于Carnivale：未Observed的packet与首raw字段真实不同时先Finish再Start，几何改为B；后续只有Actor.Position/Rotation变化、CastInfo不变时继续锁B。模型及实际DLL以两个扇形的不同Loc/Rotation同时验证；既有三类打断及之前所有狂欢回放也集中回归。

## 验证和待验边界

`tests/CarnivaleBombPlantRegression.cs`有8组新场景，覆盖四模块身份、纯枚举边界、群体/默认Primary差异、HP0、同OID全部marker、精确两扇、首次raw、纯召唤提示、仅眩晕、Pointless和Object色、生产SafeZone适配及owner清理。

`tests/ScriptCompilation/NativeDungeonReplayCarnivaleBombPlant.cs`由真实ScriptManager、ReplayController及输出组件驱动，验证三种无AOE场次、14583通用躲避精确抑制与玩家/未知动作隔离、Pending交接、四孔场地选择、两个扇形首raw确认与持续锁点、两个Hint组件真实Finish、Roselet颜色与选敌、反向Seek、机制停用和回放执行隔离。测试用预览完整ID后缀区分实体1与10，避免前缀匹配误把两者合并。

证据日志为 `.tmp/halatali-verify/native-carnivale-bomb-plant-*`；最终227项共用模型、34条实际DLL专项、生产构建0错误/4条既有Crucible警告、实际DLL全目录659条PASS/0失败；审计生成及一致性检查后登记。本批没有新增真实物理移动或原生动作，显示和源提供的实际AOE/场地导航分别验证；真实游戏像素、宿主加载与GreenMove仍待实机，回放不发送原生控制。
