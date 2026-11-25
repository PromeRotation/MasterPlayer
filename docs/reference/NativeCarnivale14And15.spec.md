# 假面狂欢第14、15场 MP 原生规格

## 来源与接管

源分别为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage14BlobsInTheWoods/Stage14Act1.cs`、`Stage14Act2.cs` 和 `Stage15TheMeNobodyNodes/Stage15.cs`，以及它们实际激活的组件及基类。三个源模块共14个组件实例：第14场每幕构造攻略、LastSong、LastSongHint各一项；第15场构造攻略及七个战斗组件。未从动作枚举或攻略文字补造额外组件。

匹配本机简体中文游戏表：

| 模块 | CFC | InstanceContent | Territory | 主OID / NameID | 正式名称 |
|---|---:|---:|---:|---|---|
| Stage14Act1 | 624 | 35014 | 796 | 271D / 8108 | 陪葬·第一幕 |
| Stage14Act2 | 624 | 35014 | 796 | 271E / 8108 | 陪葬·第二幕 |
| Stage15 | 625 | 35015 | 796 | 26F9 / 8109 | 来自亚拉戈的威胁——斗兽系统 |

共同地图反查CFC仍为610，不把CFC当区域ID。主入口为 `Presets/MaskedCarnivale/Entries/Stage01.cs`，接管按Territory796及精确Boss OID进行。生产机制状态由 `DungeonEncounter` 持有，不创建BossModule/MechanicSession，不读取BMR桥输出。普通和连续输出均经 `DungeonOutput → SafeZone/Draw`。

## 第14场：视线可见域

两幕任意同OID对象入战即启动；全部同OID对象DeadOrDestroyed且独立 `LastSongCasting=false` 才完成，HP0不代替Dead。`LastSongHint`是一个bool：任意14756 Start置true，任意匹配Finish置false，即使仍有另一施法者的可见域，也不能把bool改成“列表非空”。当前幕完成后当帧重选后台幕，保留后台仍有效的共享14756记录和提示状态。

场地分别复用源 `Layouts.Layout2Corners`、`LayoutBigQuad` 的完整正式轮廓。LoS使用独立的 `Layout2CornersBlockers`、`LayoutBigQuadBlockers`，不能拿内缩正式场地代替。两套遮挡域的轮廓点数分别64/12/8和64/4，由源DLL导出到 `CarnivaleArenas.cs`。第一幕保留89.98°墙，第二幕保留半边4.9、旋转45°方块。

14756 Start锁定当时 **Actor.Position**；不是Cast.Loc，也不是包内SourcePosition。每幕各自的 `CarnivaleLineOfSightCast` 保存来源Actor引用、代际、完整接受的cast、原点、遮挡轮廓及缓存可见域。后台幕新激活时，在当帧raw起止对账后补播仍有效的接受记录，使用激活时Actor位置，原模块锁点不变。预计截止、Effect、死亡不清；真实Finish、取消和来源销毁按原生命周期撤销。

`DungeonPolygonVisibility`保留源的顶点射线角、微扰、近点/共线处理、近水平射线分支，以及2^20整数量化和NonZero自交归一。场外原点可能形成多个part，全部保留；不投回场内、不截掉末端、不只取最大part。归一复用仓库已有第三方 `Clipper2Lib`，未使用BossMod模型或桥；该纯库当前随 `MasterPlayer.MechanicRuntime.dll` 打包，程序集层面的纯库拆分尚未实施。若出现当前输入未观测到的孔或归一/三角化失败，明确报错，不返回假空域或静默省略绘制。

源默认maxRange=0，不按注释增加60码裁圆。14757 Syrup仅枚举，不接管。攻略中的Loom仅文字，不自动传送。全局文字分别为：

- `Take cover behind a barricade!`
- `Use the cube to take cover!`

可见域为危险区，导航避让可见部分；不把阴影另外注册成虚构强制安全点。每条仍有效施法发布独立危险与雷达/世界预览。公共世界几何适配通过 `ITriangulatedDrawingGeometry` 将 `PolygonRegionGeometry.FillPolygons` 交给 `MeshBuilder`，使用所有part的填充片；贴地投影也复用同一网格入口。边界轮廓与填充片分开，不将凹域重新做首轮廓扇形填充。

缺raw推断结束与真实Cancel分开保留收据：晚到首个raw可以恢复同一代，保留原Finish事件边界；真实Cancel无论先于还是晚于恢复到达，都终止这一代。真实raw换代仍抬高边界，迟旧Finish不能清新代。观看角色缺失时隐藏输出，继续处理其他演员的Start/Finish，保留独立bool及输入代际，恢复视角后正常对账。

## 第15场：Ballast三段与普通机制

采用源默认Primary拉战：26F9可选且InCombat；仅Shabti/Serpent入战不启动。Primary死亡、销毁或HP0即结束，不等待adds或Ballast。场地为中心(100,100)、R16.01379、32边形CircleSmall。绘制Primary及全部26FA/26FB，adds为Object橙色。目标优先级Shabti=2、Serpent=1、其余0，保留Pointless=-1。构造攻略在开战时撤销，不增加自动睡眠、自动打断或禁攻。

| 输入 | 原生模型与输出 |
|---|---|
| 14890 HighVoltage | 保存真实施法身份与时钟，仅Interrupt意图及文字，无独立全屏AOE/伤害预测 |
| 14893 BallastVisual1 | 锁Cast.Loc及Rotation，追加独立三段序列；首activation=已含NPC补偿的FinishAt+3.6秒 |
| 14894 / 14895 / 14896 Finish | 分别要求当前阶次0/1/2；逐轴位置差≤1、归一角差≤0.05，跨source消费首匹配；下一activation=实际消费钟+0.6秒 |
| 14891 PiercingLaser | 前32.3、半宽4矩形 |
| 14892 RepellingCannons | R12.3圆 |
| 14970 Superstorm | 内8外20环 |
| 14968 Spellsword | R7.1、总120°扇 |
| 14899 Disseminate | R7.2圆；死亡Serpent仍允许观察施法 |

Ballast三个形状是R5.5总270°扇、5.5–10.5总270°环扇、10.5–15.5总270°环扇。源 `showall=true`：始终输出所有剩余3/2/1段，共享当前activation，全部Risky并参与导航，不将未来段灰化或额外错开时钟。

`CarnivaleBallastSequence`独立保留代际、视觉cast、锁点、方向、activation及阶次。每模块另存当前视觉输入→Generation收据；同source、同StartedAt的多条历史序列不发生补正串代。已推进阶段不因packet补正复活，真实raw变化仍可创建新序列。视觉Finish/取消/销毁、14955和Effect不推进或清理；helper真实Finish/取消/销毁均按匹配推进，不以completed过滤，不按预计到期清理。

6497、14897、14955、14971没有独立激活消费；14894–14896注释中的15码击退没有对应源组件，未据此增加位移、免疫或击退导航。

## 验证边界

模型测试在 `tests/CarnivaleLastSongRegression.cs`、`CarnivaleBallastRegression.cs`；实际DLL在 `tests/ScriptCompilation/NativeDungeonReplayCarnivaleLastSong.cs`、`NativeDungeonReplayCarnivaleBallast.cs`，支持独立过滤 `--native-replay=CarnivaleLastSong,CarnivaleBallast`。

- 第14场：两幕各8个施法原点，各60,552点对照源最终归一可见域；实际DLL同时调用世界 `MeshBuilder`，逐点核对其填充三角形并集。包含场外多part、锁点与包落点分离、双caster独立bool、后台激活及同帧交接、旧Finish、取消、迟raw、缺视角完整起止、反向Seek和owner清理。
- 第15场：三个270°形状共20,619点源oracle；验证3/2/1全剩余导航、共享判定钟、视觉取消保留、helper取消/销毁推进、顺序/逐轴/角差、真实raw新代与packet补正身份；普通AOE、死亡Serpent、阶段、颜色、优先级和owner清理另有定向用例。
- 公共世界贴地回归覆盖坡面、内孔、大角扇、多层/台阶、真实缺口和缓存生命周期。回放只发布规划及显示，没有原生移动、面向、ACR动作或自动技能执行。

显示数据、实际世界网格与当前导航已离线验证；GPU最终像素、真实移动及宿主执行仍待实机。雷达实际裁剪像素不由Contains oracle替代。源未提供的自动动作、特殊移动导航及统一寻路试点均未扩展。

最终64项斗场集中模型、生产0错误/4项既有Crucible警告、14条实际DLL专项、681条全目录PASS/0失败，世界贴地8组通过。证据在 `.tmp/halatali-verify/native-carnivale-song-nodes-model-final.log`、`native-carnivale-song-nodes-build-final.log`、`native-carnivale-song-nodes-focused-final.log`、`native-carnivale-song-nodes-catalog-final.log` 及 `native-carnivale-song-world-ground.log`。三个模块在生成审计及一致性检查后登记为离线已验，不代表实机验收完成。
