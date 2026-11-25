# 假面狂欢第06场及第07场第三幕 MP 原生规格

## 来源与身份

来源为 `MechanicRuntime/Upstream/Modules/Global/MaskedCarnivale/Stage06EyeSociety/Stage06Act1.cs`、`Stage06Act2.cs`及`Stage07AChorusSlime/Stage07Act3.cs`。三模块共12个实际激活组件、8条逐幕规则、5种Action；未激活的14695 Stone、14696 Detonation、14711 Object130不因枚举或攻略而增加消费。

本机sqpack核对第06场「铁鹰战士」CFC616、Content35006，第07场「粘液怪合唱团」CFC617、Content35007，Territory均为796。主OID25CD/26FF/2706，NameID8090/8092/8095；沿用GUID `masterplayer.carnivale.stage01`及逐幕机制开关。

## 模型与生命周期

06 Act1构造激活攻略、TearyTwirl及DemonEye，开战撤攻略并激活ColdStare；Act2构造激活DemonEye、TearyTwirl和常驻攻略，开战激活ColdStare及DreadGaze。构造组件从模块创建起接收事件，包括Pending；晋升Loaded不能重新播放或覆盖其历史。战斗组件有独立补播标志，只在真正拉战后补播当前读条与状态。

07 Act3构造攻略与SlimeExplosion，拉战才激活LowVoltage。三模块由各自Trash集合任意成员InCombat触发，全组DeadOrDestroyed结束；HP0不代替Dead。06 Act1画全部25CD及橙色2700；Act2仅画固定Primary、全部25CE及橙色2701；07 Act3画全部2706/2707。06按全部合法PotentialTargets无条件覆盖优先级：Act1本体1、其他0；Act2本体及眼1、其他0，不保留PendingDead的基础-1。

| 机制 | 原始模型 | 当前输出 |
|---|---|---|
| 14691 DemonEye | 锁CastInfo位置、默认10000范围、真实Finish按source首项；SID571的独立Raid槽mask | 真凝视眼形/瞳孔/视角弧及文字，开战后的PR面向；不生成地面避让圈 |
| 14692 ColdStare | 锁CastInfo位置与朝向，R42.53、总90°；独立571 mask | 普通扇形双视图和导航，保留源TODO，不擅自改成PR凝视 |
| 14694 DreadGaze | Act2专用，R7.35、总90°；独立571 mask | 普通扇形双视图和导航，不套真凝视 |
| 14693 TearyTwirl | 原目标Actor引用、R6.3，真实Finish按目标ID删第一项；独立571 mask | 目标动态轮廓及源自定义文字；单人无接近/分摊导航，无通用Stack!或伤害预测 |
| 14710 LowVoltage | 首次Start锁caster Actor位置，与spell落点分离；Layout2CornersBlockers视线缓存，risk提前99秒 | 立即危险的复杂视线双视图及导航；无35码截断，不套LastSong特殊结束条件 |
| OID2707 SlimeExplosion | 构造重播及后续Created保原Actor引用，Death删除、Destroyed冻结、复活不重加 | R7.6轮廓和脚下文字；无爆炸导航、拉怪、推怪、引爆或自动动作 |

三种场地直接使用源CircleBig、LayoutBigQuad和Layout2Corners的真实轮廓。视线运算复用当前公共多边形可见性适配；普通几何、预览、障碍与危险区继续经 `DungeonEncounter → DungeonOutput → SafeZone` 输出，不依赖BMR运行结果或搜索器内部节点。

## 状态、身份与撤销

SID571不以预计过期钟合成Lose。四组件各有独立mask及原生状态槽差分，保留重复状态槽、换源、参数/实际续期与移除语义。假面为单人内容；生产路径以实时Objects.LocalPlayer或录像LocalPlayerId作为源Raid0，PR可配置优先级槽与观看角色不能重排此位。没有为假想多人假面添加新录制字段。

源Feed先提交Actor变化、后提交队伍表。构造组件的首次状态回放使用上一帧已提交roster，普通状态差分也使用旧roster；开战新组件使用本帧roster。冷启动首帧已有571且立即入战时，Demon/Teary未收到有效槽Gain，Cold/Dread可在开战激活重播中收到；若玩家roster早已提交、模块稍后出现，构造组件应收到已有571。Pending构造组件同样维护这段历史。

Teary保持目标销毁后的旧Actor引用，同ID新代不能替换旧圈。接受同代packet目标更正时原子更新Cast与目标；ID不变保留旧引用。目标FIFO已消费某来源的圈后，该来源的后续correction不能重新播放Start。未解析目标不创建圈，不将最终坐标代替目标身份。

五种动作纯StartedAt/FinishAt饱和漂移不重建代际或时钟。普通扇形、真凝视、分摊及视线按真实Finish/取消/销毁清理；效果包和deadline经过不代替真实结束。预战尚未激活的Voltage真实取消建立墓碑，不允许旧raw在拉战时复活。切展示、反向Seek、机制关闭及停止按原owner清理。预战仅显示，不发布导航或面向。

## 验证与覆盖边界

`tests/CarnivaleEyesVoltageRegression.cs`覆盖17组模型场景，包含非零扇形朝向、四mask、真实结束、目标FIFO、packet更正、销毁冻结、饱和时钟、冷启动/晚构造及Pending历史。

`tests/ScriptCompilation/NativeDungeonReplayCarnivaleEyesVoltage.cs`通过真实ScriptManager、ReplayController、门面和生产绘制适配验证三种场地各5694点源Contains，8个Voltage起点各7569点源可见性与实际世界三角填充；同时覆盖双视图、普通扇形导航、solo无分摊导航、PR面向模型、反向Seek与owner撤销。ARR/MP回放不发送原生控制。

最终集中模型、生产编译、实际DLL专项及全目录证据分别见 `.tmp/halatali-verify/native-carnivale-eyes-voltage-authoritative-model.log`、`native-carnivale-eyes-voltage-authoritative-build.log`、`native-carnivale-eyes-voltage-authoritative-replay.log`及`native-carnivale-eyes-voltage-authoritative-catalog.log`。244项集中模型、生产0错误/4项既有Crucible警告、24条实际DLL专项及854条全目录PASS，0失败。

显示与源已有的普通扇形/视线导航离线验证；凝视沿现有PR面向消费者输出。三个模块无特殊移动模型、禁攻或停手消费者，不因攻略增加推拉、交互、自动引爆或主动引导失明。真实PR面向执行、GreenMove与游戏最终像素仍需实机验收，离线显示证据不代替执行验收。
