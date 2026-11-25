# 天青斗场第4场第二幕、第8场第二幕与第10场 MP 原生规格

## 来源与身份

来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/` 下的 `Stage04GentlemanPreferSwords/Stage04Act2.cs`、`Stage08BombedyOfErrors/Stage08Act2.cs` 和 `Stage10ALittleKnightMusic/Stage10.cs`，共19个实际激活的组件。目录中的其他幕不因共享 Territory 或入口而自动接管。

本机匹配 Lumina 与中文 sqpack 已核对：CFC614「怪力铁巨人——克利俄斯」↔Instance35004、CFC618「青之牙、红之牙」↔Instance35008、CFC620「王者骑士——幽黑曲偶」↔Instance35010，三项 ContentType27、Territory796。主对象 OID 分别25D5、270B、2717；源 NameID8087/8098/8100 对应克利俄斯、斗场爆弹之母、幽黑曲偶。Territory796反向CFC610只代表共享地图，不用于按场次识别。源 Stage10 没有 Act2。为兼容已有设置，沿用 `masterplayer.carnivale.stage01` GUID 和此前场次名称；本地场次名称与正式CFC标题的差异不作为身份依据。

生产模型在 `Presets/MaskedCarnivale/CarnivaleCatalog.cs`、`DungeonCarnivale.cs`、`CarnivaleModuleState.cs` 和 `CarnivaleInterruptCast.cs`；沿用 `DungeonEncounter → DungeonOutput → SafeZone/Draw`，不依赖BMR捕获输出或求解器内部节点。

## 生命周期与构造组件

| 模块 | 准备阶段 | 拉战与结束 | 敌人绘制与选敌 |
|---|---|---|---|
| 04 Act2 | 构造Hints含Primary.Name、弱雷、六甲虫和冰冻/即死组合；不激活AOE/打断 | 默认Primary可选中且InCombat拉战；Primary Dead、Destroyed或HP0结束，不等甲虫；开战移除Hints、改为Hints2弱雷/可冰冻说明 | 固定Primary加所有25D6；全合法候选甲虫1，其余0，保留Pointless不可覆盖 |
| 08 Act2 | 构造Hints及Selfdetonations，两者开战均保留 | 270B/270C/270D任意交战拉战；三类全DeadOrDestroyed结束，HP0不替代Dead；死Primary后仍维护其他组件及小怪 | 固定Primary、全部270C/270D；所有270B优先级1，其余0，保留Pointless |
| 10 | 构造Hints含Primary.Name、King's Will/Black Nebula说明及中性成就提示；开战整体移除 | 默认Primary可选中且InCombat拉战；Primary Dead、Destroyed或HP0结束 | 只画固定Primary；没有自定义选敌，不由攻略增加优先级 |

Loaded/Pending、冷首帧倒序、旧Party[0]直接加载、500码三维距离、同OID占位、Pending死亡丢弃、wipe保Pending沿用[第7/8场规格](NativeCarnivale07And08.spec.md)。普通展示切换只执行 `CarnivaleUnselect`，不执行全局Reset：后台已取消读条的收据、raw观察基线、模块意图和packet宽限均保留。真实wipe仍卸载Loaded并Reset。模块完成后没有新出生事件时，不由复活或recommence重新构造同一owner。

## 动作与几何

下列普通AOE均在拉战时激活，回放当时已有施法；锁定 `CastInfo.Loc/Rotation`，不是Actor.Position/Rotation。后续Actor移动不移动AOE；raw字段真实替换按旧Finish→新Start重建。匹配Finish、取消、打断或施法者销毁撤销；预计时间或Effect本身不清理。

| 模块/Action | BMR组件和精确参数 | MP输出 |
|---|---|---|
| 04/14366 | GrandStrike，Rect前长77.5、半宽2，即全宽4 | 真矩形危险导航及双视图；不按注释宽2缩半 |
| 04/14368 | MagitekRay，R6 | 独立落点圆危险 |
| 04/14369 | MagitekField，默认CastInterruptHint | 完整caster/代际/起止钟/打断意图与Interrupt!，无AOE、伤害或自动技能 |
| 08/14708 | Sap，R8 | 落点圆危险，与爆炸显示分离 |
| 08/14680 | Burst，默认CastInterruptHint | 完整打断意图及Interrupt!，不从50码注释生成AOE |
| 10/14725/26/27/28 | IronJustice I–IV，R9/9.5/10/10.5，总角120° | 四个独立锁定扇形，不按Actor当前hitbox推算半径 |
| 10/14722 | Cloudcover，R6 | 独立落点圆危险 |
| 10/14719/20/21 | King's Will I/II/III，各自独立CastInterruptHint | 三组打断caster，未按次数猜测增大范围 |
| 10/14724 | BlackNebula，独立CastInterruptHint | Interrupt!，不是团伤、全屏危险或伤害预测 |

6497/6499、14362、14730/14731及14723只有枚举，未激活对应消费组件，不新增爆炸判定、伤害或自动动作。

08的Selfdetonations保存全部270C/270D创建引用：每项原始位置R6黄色轮廓，不可选中不隐藏，Death删除该引用，Destroyed冻结，同ID新生另加、复活不重入；圈内只给一条 `In bomb explosion radius!`。没有主对象R10圈。此组件无导航、推拉或引爆动作，攻略只作文字。

## 打断模型与实际消费边界

源 `Components/CastHint.cs:22–35,77–98` 的caster列表与Enemy投影分开：匹配Start追加引用，匹配Finish删除；不靠预计结束、Effect、死亡或不可选中删除caster。默认可打断、不可眩晕、不显示名字。四种第10场动作各有独立组件；全局文字按非空Action组输出。

`CarnivaleModuleState.Interrupts`保存每个所属模块的 `CarnivaleInterruptCast(Generation, AlphaActorState Caster, DungeonPendingCast Cast)`。原对象引用保留身份；同代packet补正更新Cast且保留Generation；raw即使时间元组相同，只要Loc/Rotation/Target/TotalTime真实变化，经Finish后也分配新Generation。`DungeonPendingCast.FinishEventBoundary`保留结束事件下界，同代补正不改；首次接收使用真实StartedAt，只有已知raw替换才以新观察时刻建立边界。带显式旧时间戳的Finish不能消费更晚观察到的新代；首次Start和同代Cancel一同延迟排队时，不能按主线程处理时钟拒绝真实Cancel。清理仍按被接受的Cast引用执行，不单凭Source ID或时间元组判断。

`ShouldBeInterrupted`仅投影当前合法Enemy：可选中、未Dead、IsEnemy、ObjectIndex小于200且为偶数；不按HP、仇恨、当前选中或Priority≥0筛选，HP1的Pointless仍可有标志。`ShouldBeStunned=false`。死辅助caster或不可选中caster关闭Enemy标志但仍可保留原始列表及Interrupt!。Primary死亡结束所属模块的情况单独服从上表阶段规则。

旧MP桥会计算ShouldBeInterrupted，但 `MechanicSession`没有创建Autorotation消费者；`MechanicNavigationHost`也没有转发敌方打断标志或动作队列。`DungeonScript.TryKtisisInterrupt`只接受Ktisis/Meso/Coil及25907/44856/1447/1216，不接本批动作。故本批保留原意图、身份及时钟，并维持实际不执行打断的既有范围，不新增BLU自动Flying Sardine或职业技能。PR面向、ACR停手、移动控制不是本批来源机制，不以文案增设请求。

## 08 Act2精确场地

`Layouts.Layout2Corners`是R25的64边形减四条墙，再整体Round内缩0.5；不是R24.5圆扣简单扩大矩形。墙的输入为中心(90,94.75)/(110,94.75)、半尺寸5×0.25，以及中心(94.75,92)/(105.25,92)、半尺寸3.01134109×0.25、旋转89.98°/90°。`Layout2CornersBlockers`不是本场地的替代。

`--carnivale-arenas`从已解析源Polygon导出 `CarnivaleArenas.Layout2Corners`：外圈64点、两孔39/33点、137个源三角形。独立只读反射核对136个轮廓顶点和411个三角形顶点，共547组XZ，float位值零差异。准备时只显示场地，拉战后才发布正式导航边界；公共适配保留全部孔和三角形，不改变求解模型。

## 验证与待验边界

`tests/CarnivaleInterruptRegression.cs`的14组新定向场景覆盖三个来源边界、默认与群体生命周期、六个打断组件、八个AOE、预战已有读条激活、未知动作替换、取消raw回灌、并发caster、死亡/不可选中/Pointless、同代补正及同时间raw换代、展示切换后的后台取消与packet宽限、迟到旧Finish、爆炸原引用、精确场地探针、输出及owner清理。

`tests/ScriptCompilation/NativeDungeonReplayCarnivaleInterrupt.cs`由生产ScriptManager与ReplayController驱动，另在既有SlimeBomb回放中加入270B。覆盖全部14个Action的真实输出、双视图注册、导航/仅显示分离、背景切换、代际及旧Finish、反向Seek、机制停用、回放执行隔离及owner清理；场地源Contains oracle为48,841网格点加10个薄墙/89.98°/外圈探针，共48,851点。

最终证据在 `.tmp/halatali-verify/native-carnivale-interrupt-*`：218项共用模型、28条实际DLL专项通过；生产构建0错误、4条既有Crucible警告。实际DLL全目录653条PASS、0失败，审计生成及一致性检查通过后登记。扩大筛选曾触及无本批修改的FirstB低速反馈仿真失败；该失败场景随后单独重跑1项通过，日志 `native-carnivale-interrupt-unrelated-firstb-retry.log`；未据此修改巨豆实现。

显示及来源提供的实际导航已分层验证。Interrupt标志没有当前技能执行消费者；爆炸只有显示和提示。边界上的浮点epsilon、真实游戏像素、PR宿主加载及GreenMove仍待实机验收，离线回放不发送原生动作或移动；统一寻路试点不在本批范围。
