# 卓异的悲寂深想战原生机制

本轮日期：2026-11-25。来源为 `MechanicRuntime/Upstream/Modules/Dawntrail/Quantum/Q1FinalVerse/`。本机客户端表核对 Territory **1311**、CFC **1063**、Primary OID **0x48EE**、Eater OID **0x48EF**、NameID **14037**。入口为 `Presets/Dungeons/Entries/QuantumFinalVerse.cs`，GUID `masterplayer.quantum.final-verse`。模型由 `QuantumEncounter` 持有，经 `QuantumOutput → DungeonOutput → MP SafeZone/双视图` 发布，不调用旧模块或桥。

本项仍保留 `bmr_pending`。下述已实现和已验证范围不能代表完整深想战已验收。没有迁移 `AutoPotion`，也没有任何探索、开箱、机关、自动前进、道具或层间推进。

2026-11-25续验：修正新建Pet的实际Gain及Destroy Lose错误绕过热回调过滤，冷组件补播仍允许Pet。`native-kefka-20261125-quantum-pet-model-01.log`定向1项PASS；最终生产`native-kefka-20261125-build-05.log`0错4既有警告，同DLL合批`replay-05.log`46条相关专项与`catalog-01.log`948条全目录PASS/0FAIL（均为相同批次前缀，包含其他模块）。此前119项集中模型与本次补验分开记录，不重复计数。缺方向历史分支仍未闭合，因此状态不升级。

## 来源与实际激活范围

状态机实际激活九个机制组件：LightAndDark、ScourgingBlaze、BoundsOfSinSmall、BoundsOfSinEnd、LightDarkNeutralize、BoundsOfSinTowers，以及三个普通自目标圆组件。光暗组件在模块构造时激活；其他组件按源状态节点激活/撤销。Q15–35的分支及Q40首轮之后，源本身只有永久Unsupported或“???”节点，未根据枚举补造后续机制。

| 输入/机制 | 锁定及连续状态 | 实际结束与MP消费者 |
|---|---|---|
| 45117、44098、45200 | 原始Cast Loc/Rotation及独立F，三个R6组件各自存储 | 真实CastInfo结束按组件与来源删首；Effect/预计时间不清。双视图及普通危险导航 |
| 44797/44799、44798/44800；Crystal 0x1EBE70；44118/44119 | 方向效果改变下一出生水晶的方向；Created冻结方向/位置；44118匹配逐轴1码的首水晶，向两侧建4码步长、1秒间隔、R5地火。每线保原始方向、剩余次数、Next及独立判定钟 | 首爆逐包NumCasts++、反序消费当前位置匹配且lateness&lt;1秒的所有线；后续按包落点及caster朝向±1度只推进首匹配线。Finish不推进；显示开关和缓存版本独立 |
| 44122 Small | R3队列无限存储；超过六项时前六危险色，全部显示；真实Finish锁定消费时caster姿态，决定End圈或环 | Small真实Finish推进计数；恰12次Effect退出Small。End使用消费钟+6秒，44123/44124真实Effect清理 |
| Bounds Map 00/0C/17/1B等 | 场地按源Map状态选择原24柱索引，每柱64边形；还原场地为源Round外扩0.5的24顶点轮廓，非40×30矩形 | Map分别管理柱、塔和恢复，取消/时间不代替Map；场地与三角填充保留孔，双视图与硬边界共用 |
| Source图标78 | 重复图标独立追加；保存目标原Actor引用及原时钟，R2恰两人 | VFX40仅推进状态机次数，不自行删除列表；组件退出时清理。目标销毁冻结，重建同ID不重绑；死亡暂隐藏 |
| 光暗状态/44129、44134、44132、44130 | 真实60槽、源Param与旧Party槽mask；保Drain两个独立钟和严格窗口，光暗五组岛及补集 | 以状态和真实Cast结束消费。颜色池仅显示；进入所需色池及避另一色经独立危险/障碍消费者导航 |
| 塔 | 初始化按包含死者的源Party槽捕获Forbidden；实际人数只数存活且允许的成员，R2恰一人 | Map/组件退出清理；禁止踩、缺人、超员各自显示/提示。首塔Activation、符合条件的实际塔内成员Raidwide预测 |

分摊导航保持旧MP桥的实际消费范围：进入任一合法成员圈。源 `SDOutsideOfUnion.Distance` 对重叠区的“恰一”与其Contains不一致；旧 `MechanicHintsConvert` 已统一为union。本次不新增源桥未实际提供的交叠硬禁区。

## 输入、生命周期与缺失证据

模型按源逐Actor差分：先销毁缺席对象、Finish旧读条、Lose旧状态，再按输入顺序Created→Cast→Status；帧末提交新Party。模块构造发生在Primary Created内，当时仍使用旧Party。Pending晋升不重播状态。来源Pet在热状态回调中排除，冷组件补播保留；非法Source 0xE0000000归0。

raw读条判据包含Action/Target/Total/Loc/Rotation，不比较StartedAt/FinishAt。存量elapsed先按float累计并封顶，再应用raw差分，晚激活组件读取存量float钟；已发布队列锁原F。九个实际输入Action的本机ExtraCastTime均为0。Status同ID/Source/Param且新Expire−旧Expire≤1秒仅更新槽，不重发Gain；大幅缩短也遵守这个单向判据。

来源Actor引用、销毁和同ID新生分开。同ID/OID的Primary在下一Update前重建时按源BMM换绑Primary，保阶段及旧组件引用；真正完成/Director卸载后的旧对象不重新开怪。冷激活、缺视角、切人、死亡、Seek重建和owner撤销保持独立边界。

**尚未闭合的输入分支**：在前置方向Effect缺失而水晶/读条已存在的冷激活中，源方向为零，RayAABB返回float.MaxValue，源unchecked转换可能产生异常巨量预测。MP保留原零方向线、Remaining和clock，不猜方向，不物化该线数亿个AOE；输出缺方向历史诊断。该分支的定向显示/导航保留待验，不能把普通方向完整回放通过作为此分支完成证据。缺失的历史Map/Icon/VFX同样不能从当前帧捏造。需要包含前置方向及相关历史的真实录制，或另行明确的输入恢复合同。

## 显示、导航与执行

- 同一MP状态发布池、当前/未来地火、真实带孔场地、塔、分摊及敌人标记。未来/当前颜色、原始判定钟和输出顺序保留；两视图最终像素尚无实机验收。
- 导航只在ActivePhase且当前视角属于源Party合法槽时发布。预战仍显示；退队撤销个人危险/障碍，恢复名单后重新发布。颜色池取得使用生产SafeZoneSolver验证，未使用最少危险回退。
- 场地、普通AOE、光暗补集、塔和分摊采用当前生产门面。没有新增传送、自动道具、GreenMove控制循环或宿主API。原HP/目标优先级规则只作用于源已定义的消费范围。
- ARR/MP回放只绘制及规划，停用、切区、Seek、异常和停止按owner清理。PR/GreenMove真实执行、游戏物理及最终双视图像素仍待实机。

## 本机验证证据

证据目录 `.tmp/halatali-verify/`，本轮唯一前缀 `native-quantum-q40-20261125-`，不覆盖前批日志。

- `model-08.log`：17场模型PASS/0FAIL，含真实时序、输入差分、状态槽、旧Party、同ID重建、未知方向保护、塔预测和公共输出清理。
- `build-04.log`：生产编译0错误、4项既有Crucible警告，产物 `bin/Debug/MasterPlayer.dll`。使用工作区临时SDK选择下的.NET10.0.303，未改根SDK配置。
- `replay-06.log`：9条PASS/0FAIL（含2条公共检查），退出0；本轮生产DLL合成输入专项覆盖预战/退队门控、真实raw结束、连续Map/重复Source图标/VFX、已知方向完整Effect地火、反向Seek、owner及真实解算。
- `catalog-02.log`：同一生产DLL全目录932条PASS/0FAIL，退出0。首轮`catalog-01.log`因未设置`FFXIV_SQPACK`在天宫输入场景停止；补上本机路径复跑，无生产改动。
- 几何独立对照使用真实源运行库：24单柱、初始Round外扩及两整环，共205,200点Contains与实际FillPolygons覆盖；Light/Dark各23,852点源五岛与原生池/补集对照。退化三角形不作为有效填充。

`replay-04/05`是新夹具失败证据：TargetPosition最初按默认JSON序列化将Vector3写成空对象，修正为显式X/Y/Z后重跑。没有为通过夹具改变生产方向匹配。`model-06`因源零方向异常巨量预测被主动终止，不列为通过。
