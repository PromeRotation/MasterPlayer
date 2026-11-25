# 天青斗场第7场前两幕与第8场第一幕原生脚本

2026-11-24。来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage07AChorusSlime/Stage07Act1.cs`、`Stage07Act2.cs` 和 `Stage08BombedyOfErrors/Stage08Act1.cs`。生产模型位于 `Presets/MaskedCarnivale/`，沿用 `Entries/Stage01.cs` 的 GUID 和独立机制开关。第7场第三幕的 LOS、第8场第二幕的打断及其他未迁主对象继续保桥，不按共享地图或动作枚举扩大接管。

## 身份与场地

本机匹配 Lumina 表确认 CFC617「粘液怪合唱团」↔ InstanceContent35007、CFC618「青之牙、红之牙」↔ InstanceContent35008；两者 `ContentLinkType=1`、`ContentType=27`、`InstanceContentType=13`（MaskedCarnivale）、`TerritoryType=796`。Territory796 自身反指 CFC610，不能反向要求等于617/618，也不能据此把同地图所有场次接管。AOZContent7/8的场次数值另有表证据，本规格不把行号算术关系当成外键。

| 幕 | 接管 Primary／Trash 集合 | 场地与敌人标记 |
|---|---|---|
| 07 Act1 | 2703；2703/2702 | 中心(100,100)，源R24.5正64边形；固定Primary及全部2702 |
| 07 Act2 | 2705；2705/2704 | 中心(100,100)，精确Layout4Quads；全部2705和2704 |
| 08 Act1 | 2708；2708/2709/270A | 源R24.5正64边形；固定Primary及全部2709/270A |

`CarnivaleArenas.cs` 通过现有 `NativeDungeonArenaExport --carnivale-arenas` 从源已解析多边形导出，不依赖运行时 BMR 输出。Layout4Quads 是 R25正64边形减四个中心(±10,±10)、半边1.9、旋转45°方孔，再对完整差集 Round 内缩0.5；外圈内缩、孔扩大并形成圆角。最终是64外顶点、四个各24顶点孔和166三角形，外圈最大半尺寸24.499397。以 `PolygonRegionGeometry` 保留孔和填充三角形；不近似成R24.5圆减扩大方孔。

预战和战斗均消费同一精确几何，预战经 `DungeonShapeTemplate` 只显示边界；开战后 `DungeonOutput` 发布正式导航边界。实际 DLL 对照源 Contains 的48,841个网格点和6个外圈/孔边/圆角定向点通过；这些有限点对照不表示本地与源的默认 epsilon 在所有贴边浮点输入上相同。

## 构造期、加载及连续生命周期

六个已迁斗场模块共用本地 Loaded/Pending 模型。每个 `CarnivaleModuleState` 从构造起保存 Primary 出生引用、是否已启动及自己的爆炸引用列表；候补模块的组件历史不会等到被选中才创建。

Loaded、Pending 和选中项保存独立模块引用，不以 Entity ID 合并模块。没有Party[0]时旧Pending销毁后同ID新生，仍是两个构造实例，后续清理旧实例不能删除新Loaded。Loaded只允许同OID且同ID的新Actor重绑Primary；同ID跨OID新生创建独立模块，保留旧组启动状态，切换定义时按模块引用清理旧Active。

输入保留旧 Party[0] 的逐对象位置。已知玩家500码三维范围内且无同 OID 已加载模块时直接加载，否则进入 Pending；冷首帧在应用队伍表前创建对象，因此 Pending 按倒序晋升。晋升先于 Loaded 更新/卸载，同 OID 已加载对象阻塞兄弟。Pending 的 Primary 死亡或销毁即丢弃模块，随后仅复活不会重新构造；真实新出生允许重建。未启动 Loaded 主对象销毁才卸载，候补下次更新晋升，沿用候补原有构造组件，保留前主对象未死即销毁的冻结圆。

任意 Trash `InCombat` 启动，全部 Trash `DeadOrDestroyed` 才结束，不附加 Primary HP、敌对或可选性条件。启动后 Primary 死亡、销毁、HP0、不可选或脱战均不替代群体结束。Loaded 使用源展示优先级及平级保留规则；尚未选中的已启动场次保留自身历史，只输出当前选中场次。wipe 卸载 Loaded、保留 Pending；recommence 只解除门控，不从原样旧对象重建已卸载模块。

取消与未知动作仍经过公共原始读条观测，保持“未知动作后真实出现已知动作可以重新开始、未变化旧 raw 不复活”的代际合同。普通读条输出按当前选中定义过滤，避免后台场次输出泄漏。

## 机制与实际消费

| 幕/组件 | 模型、准备与结束 | 显示、导航、执行 |
|---|---|---|
| 07 Act1 SlimeExplosion | 固定Primary引用、动态原始位置、R7.6；仅按Dead隐藏，未死即销毁仍冻结 | 源黄色轮廓及圈内 `In slime explosion radius!`；不注册危险、目标、伤害预测或推拉动作 |
| 07 Act1 Hints→Hints2 | 构造提示Sticky Tongue/Snort及禁火说明；开战替换为含Primary.Name的远距离攻击说明 | 只保留原全局文字，不补自动攻击或动作请求 |
| 07 Act2 SlimeExplosion | 全部2705 Created引用列表；随原实例位置更新，Death删除该引用；Destroyed冻结，同ID新生另加一项，旧代不重绑 | 每项R7.6黄色轮廓，命中任意圈只给一条个人提示；空Stacks/Spreads的源不产生导航 |
| 07 Act2 Hints | 构造起持续保留推拉到Ice Sprites后远距离引爆说明，开战不撤 | 无特殊移动或攻击动作意图 |
| 08 Act1 Selfdetonation | 固定Primary未死时动态R10；2709/270A各自Created引用列表动态R6、Death移除，Destroyed冻结 | 黄色轮廓及一条 `In bomb explosion radius!`；不按Targetable过滤圈、不加爆炸预测或导航 |
| 08 Act1 Hints→Hints2 | 准备提示下一幕Flying Sardine及禁火；开战替换为远距离击中Cherry Bomb | 本幕没有CastInterruptHint，不从下一幕建议添加打断执行 |
| 08 Act1选敌 | 所有合法候选2708优先级1，其余OID优先级0；保留源setter对Pointless(-1)的不可覆盖规则 | 不仅限枚举Bomb/Snoll；高差/仇恨基础值可以被覆盖，原生合法槽、死亡/友方过滤不变 |

14696、14709、14687、14689、14688 仅为源动作枚举，本批没有激活对应读条组件。爆炸轮廓使用原引用位置，不套敌人 marker 的死亡/可选性过滤，也不量化圆心。模型保存原始机制引用及半径，公共 `DungeonAmbientAoe → DungeonOutput → Draw` 发布雷达和游戏世界预览；正式场地独立进入 SafeZone。

## 验证与边界

`tests/CarnivaleSlimeBombRegression.cs` 的9组定向场景覆盖三幕来源边界、48,841点场地oracle、构造组件、HP0与群体结束、固定Primary、引用销毁/同ID新生/Death后复活、Pointless优先级、只显示输出、候补原历史、冷首帧顺序、晋升先于卸载、wipe保Pending，以及无Party[0]同ID多代和Loaded跨OID新生。旧04/05和01的定向场景同步验证共用加载契约；需固定首对象的夹具明确先建立Party[0]，不把冷启动倒序的差异写成生产逻辑。

`tests/ScriptCompilation/NativeDungeonReplayCarnivaleSlimeBomb.cs` 经生产 ScriptManager、ReplayController、真实输出验证黄色轮廓、原引用代际、准备/战斗交接、群体完成、精确场地48,847点、选敌、候补死亡丢弃/销毁后交接、冷启动顺序、反向Seek、机制停用和owner清理。最终215项集中模型通过；生产构建0错误、4项既有Crucible警告；实际DLL全目录641条PASS、0失败。证据为 `.tmp/halatali-verify/native-carnivale-seven-eight-model-final.log`、`native-carnivale-seven-eight-build-final.log`、`native-carnivale-seven-eight-focused-final.log` 和 `native-carnivale-seven-eight-catalog-final.log`。

显示及源实际提供的场地导航已按模型、公共适配和实际DLL分层验证；本批爆炸没有导航或执行意图，不将轮廓当成导航成功。回放不发送移动、面向或动作。真实游戏像素、GreenMove及宿主运行仍待实机验收，统一寻路模型试点不属于本次改动。

第8场第二幕随后已独立迁移，见[04/08第二幕及10场规格](NativeCarnivale04Act2And08Act2And10.spec.md)。当前共用扫描还验证了没有新对象帧时Pending继续逐帧晋升；Act1的下一幕打断攻略仍不在本幕产生打断意图。
