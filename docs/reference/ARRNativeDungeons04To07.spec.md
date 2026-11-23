# ARR 原生地城：Halatali 至 Brayflox

2026-11-22。四个副本的 11 个首领由 `DungeonScript`、`DungeonEncounter`、`DungeonOutput` 实现，入口位于 `Presets/Dungeons/Entries/`。生产脚本不创建 BossModule/MechanicSession，也不消费 BMR 导航或绘制结果。

| 副本 | CFC | Territory | 脚本 ID |
|---|---:|---:|---|
| 魔兽领域日影地修炼所 | 7 | 1245 | `masterplayer.dungeon.halatali` |
| 监狱废墟托托·拉克千狱 | 1 | 1039 | `masterplayer.dungeon.totorak` |
| 名门府邸静语庄园 | 6 | 1040 | `masterplayer.dungeon.haukke` |
| 休养胜地布雷福洛克斯野营地 | 8 | 1041 | `masterplayer.dungeon.brayflox` |

区域映射已从本机国服客户端 `ContentFinderCondition.TerritoryType` 查询。新脚本默认启用，已保存的关闭设置优先；按 Boss OID 与区域固定屏蔽已迁移 BMR 首领输出，包括脚本启动前一帧及关闭脚本时。非首领阶段的专属脚本不压制尚未迁移的模块。原有前三本同步采用相同的 Boss 接管边界和默认启用策略。

## 机制与时序

| 首领 | MP 实现 |
|---|---|
| Firemane `0x4643` | 40592：落点 R5；40588/40589：60 码、完整 45°扇形，按独立判定时间排序，最多显示并规划前 4 个，完成后晋升后续辅助体；40591 单体提示与伤害预测。40587/40590 仅视觉动作。 |
| Thunderclap Guivre `0x4644` | 40595：落点 R6；41113：真实场地减去两个安全岸区，辅助体读条结束撤销；40594 单体提示。40593 仅视觉。 |
| Tangata `0x4645` | 40599 → 40600 → 40601：R10 圆 → 10–20 环 → 20–30 环；由首领建立序列，按不同辅助体的自然读条结束推进。只显示和规划当前阶段，下一阶段按上游结束时刻 +3 秒预测；重复 effect 不推进。41123 为 R3 落点圆，40602 单体提示。4175 以精确目标禁攻处理，40596/40597/40598 不虚构空间危险。 |
| Graffias `0x103` | 701：向前 18×4 矩形；730：茧的 R7.05 爆炸；698/702 为延迟茧/持续毒区提示；`0x1E8614` 对象存在期间产生 R10 持续毒区，随对象更新与消失。尾部和小怪优先级保留。 |
| Manor Claviger `0x110` | 855 R5、705 R9.4；SweetSteel 为 R7.4、120°顺劈轮廓，方向取首领到当前目标。非目标玩家避开真实顺劈；目标坦克按队友方向注册 100 码禁站扇形，避免向队友引导。 |
| Manor Steward `0x112` | 860 R9；优先攻击 Manor Jester `0x111`。 |
| Lady Amandine `0x38A8` | 28646 R9；28649 强制靠近提示；28645 可打断死刑提示与预测；28648 视线不生成空间危险，显示眼睛位置标记，在判定前 0.75 秒联合计算所有有效禁向，通过 PR 发布共同可行面向。优先攻击 Handmaiden `0x38AA`。 |
| Great Yellow Pelican `0x1A7` | 506 R9.2、120°扇形；VioletBack > SableBack > 首领。 |
| Inferno Drake `0x3DE` | BurningCyclone 实际组件半径为 **8.6**，完整角度 120°；采用顺劈目标/坦克队友避让与仅轮廓绘制。Templest Biast 优先。源码注释中的 9.6 没有作为实现值。 |
| Hellbender `0x1AB` | 982 R8、448 R8/120°；980 保留原 SimpleAOEs 读条来源，不为无读条效果虚构预测。Bind 280 标出被束缚队友的 R1.5 轮廓并提示救援，优先攻击 Queer Bubble。 |
| Aiatar `0x38C5` | 28659 死刑；28657 辅助体 R2；28658 辅助体 R10，最多显示/规划前 4 个，完成后晋升其余；28660 向前 30×8 矩形；28656 仅视觉。 |

普通读条判定沿用上游 NPC +0.3 秒补偿。扫描发现读条在预计原始结束时刻附近消失时，立即按自然结束处理，支持没有 effect 包的序列推进；提前取消不推进后续阶段。Tangata 的 +3 秒是源码预测，尚没有本轮 ARR 录像校准证据。

## 绘制、控制与生命周期

所有场地保留真实轮廓、入口凹口和高度。Halatali/Haukke 直线轮廓由 `tools/export_arr_dungeon_geometry.py` 从上游数据导出，电水池差集生成一个包含两处岸区凹口的真实多边形；无运行时 Shapely 依赖。Brayflox 从编译运行库的已解析 Polygon 导出，保留 Aiatar 圆场地减入口矩形的结果：

```text
dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- --brayflox-arenas "<MasterPlayer.MechanicRuntime.dll>" "Presets/Dungeons/Brayflox/BrayfloxArenas.cs"
```

危险区导航副本设为 NavigationOnly；原尺寸几何单独发布至 MP `AoeVisibilityRegistry`，供雷达与游戏世界消费，避免余量污染预览及重复填充。持续毒区随对象更新，顺劈/束缚/眼睛标记按各自 ID 清理；视线标记是显示元素，不改变移动可站立空间。精确面向由 PR `MechanicControl` 消费。

BurningWard 禁攻所有存活、可选中的护盾敌人，保留其他目标与治疗。公共 Hints 门面合并目标、面向与动作限制并续期 500ms 租约，同时停止相应目标已开启的原生自动攻击。状态事件即时更新，短暂的旧扫描不覆盖最新获得/移除事件；之后以权威对象快照为准。停止、死亡、失去首领、离战、禁用、回放重建释放本 owner 的区域、预览和控制。

当前玩家 ID、朝向、状态与时间全部使用脚本统一视角/回放入口。ARR 和 MP 离线回放继续规划与绘制，禁止执行移动、面向、动作及原生自动攻击控制。

## 定向验证与限制

- `HalataliRegression`：三场场地和电水池与 BMR 逐点比较、主机世界几何转换、逆序辅助体、后续波次晋升、三段时序、无 effect 包推进、取消/死亡、护盾多目标及立即清理。
- `ArrDungeonAdvancedRegression`：Haukke 三场场地逐点比较、Totorak 矩形与持续毒区、小怪优先级、坦克/非坦克顺劈、多视线共同禁向、单体提示分类。
- `BrayfloxRegression`：四场场地与世界转换、入口差集、范围尺寸与独立余量、8.6 顺劈、Burst 辅助体晋升、Bind 标记及选敌优先级。
- 实际 `SafeZoneSolver` 验证电水池脱离与 Tangata 三段的各阶段脱险，检查路径留在场内并终止于当前安全区。
- `ScriptControlRegression`：目标/面向/动作合并、续期、精确原生停攻请求、回放/总开关门控、多个 owner 与异常清理。
- `--native-dungeons` 使用实际构建的 MP DLL、ScriptManager、ReplayController、SafeZone/Draw/Hints 门面，验证新脚本发现与默认启用、Halatali 三场、持续毒区、视线、Burst、Seek 和停止清理；实时对象读取与移动执行一旦发生即失败。

本批输入是源码对照和合成回放，不是实机录像。游戏内 PR ACR、自动攻击、面向执行和两视图最终像素效果仍需实机验收。这里仅说明四个新增副本的原生实现与离线覆盖，不表示其余 BMR 类型已经迁移。
