# BMR 机制移植报告

日期：2026-09-23。来源：BossModReborn-7.56 提交 `f6914f3e85145b5c0390d2e53376b4ffd957cca6`。本报告是移植范围的结论性文档；过程记录见 [BMR全量迁移记录](BMR全量迁移记录.md)，组件设计见 [MechanicRuntime.spec.md](reference/MechanicRuntime.spec.md) 与 [MechanicHost.spec.md](reference/MechanicHost.spec.md)。

## 一、总体进度

| 口径 | 数量 | 状态 |
|---|---:|---|
| 清单总行数（[BMRModuleInventory.csv](reference/BMRModuleInventory.csv)） | 1,259 | 已清点，Roslyn 语法树+基类链解析 |
| 斗兽棋（用户指定排除出运行库） | 45 | **已由手写移植承载**（Scripts/Crucible 五盘入口，2026-09-18/19 多轮实战修复） |
| 目标模块（首领 1,060 + 区域 154） | 1,214 | **全部接入机制运行库**：注册/构造/初始更新 100% 通过（[MechanicRuntimeCoverage.csv](reference/MechanicRuntimeCoverage.csv)） |
| 其中手写移植并逐组件核验 | 8 | 首批三个 ARR 地城（沙斯塔夏/塔姆塔拉墓园/铜铃铜山，7 首领+1 区域），规格 ARRDungeons01.spec.md |
| 其中运行库承载（原源码保真运行） | 1,206 | 实时输入/绘制/导航/时间轴/回放链路已通；逐组件行为未全部验收（见第四节） |

资料片分布（1,060 首领候选）：ARR 98、HW 99、SB 122、ShB 186、EW 211、DT 273、Global 71。类别：Dungeon 287、Trial 34、Extreme 24、Unreal 4、Raid 26、Savage 28、Alliance 88、Chaotic 1、Ultimate 8、Foray 134、DeepDungeon 49、VariantCriterion 64、TreasureHunt 71、MaskedCarnivale 60、Hunt 66、FATE 6、Quest 103、HallOfTheNovice 5、PVP 1、GoldSaucer 1。来源成熟度：Verified 716、Contributed 329、AISupport 102、WIP 112（全清单口径，含区域与斗兽棋）。

**一句话结论**：BMR 当前全部 1,214 个目标模块已随原源码运行库完整进入 MasterPlayer 并在插件内运行（构造/激活/绘制/导航/时间轴/回放），内部测试工具全部通过；手写移植部分（斗兽棋 45 + 首批地城 8）保持逐组件核验口径。逐模块的完整战斗行为验收与实机核验仍是开放项（第四、五节），不宣称已完成。

## 二、已实现（本轮交付）

### 2.1 运行库（MechanicRuntime/，里程碑提交 6fd8905）

- 导入上游 3,316 份 C# 源码、416 份障碍位图、1 份地图数据库；排除原插件入口与斗兽棋。独立编译，目标 net10.0-windows。
- 全部 1,060 首领 + 154 区域模块可注册、构造、初始更新；FTM Extreme 四个类型补显式主 OID；Calcabrina 迟加载、CLL 动态列表、Enuo 塔名单隔离、冰面输入等 7 项本阶段修复。
- 源码适配纪律：16 个登记文件逐文件固定修改前后 SHA-256（SourceChanges.json），反例测试拒绝未登记改动；嵌入资源名保留 `BossModReborn.Pathfinding.ObstacleMaps.*`。
- 命名整改（提交 21c2f86）：引入层零 BMR 命名；上游 `BossMod` 命名空间与来源说明性文字保留。

### 2.2 输入桥（MechanicHost/，提交 3197c85）

- `MechanicWorldFeed`（运行库纯逻辑）：宿主世界快照/事件 → WorldState.Operation 差分（先删后建、读条/状态/连线/标点/队伍、12 类事件），同值帧零冗余 Op。
- 实时采样 `LiveWorldSampler`（全对象表 + FFCS 原生补读 + 敌我判定 + 队伍槽 + CFC 解析）。
- 完整动作效果包：`ActionEffectHandler.Receive` 纯观察 hook（只入队不拦截、不改动画锁），提供 PR 摘要缺失的 MainTargetID/逐目标效果数组/SourceSequence/包朝向；hook 失败一次性降级。
- 会话编排：成熟度门槛内存覆盖（默认 Verified，不落盘 BMR 配置）、区域脚本压制（MP 区域脚本运行时桥让位）、回放门控、诊断属性。

### 2.3 绘制桥（提交 5f3c024）

- MiniArena 世界坐标层捕获（已登记适配）：Zone 填充族在形状参数层截获，描边/线/多边形/文字/角色标记全覆盖；Gaze 眼睛与头顶标记旁路一并转发；组件异常逐环节隔离；无汇路径与原文逐字节等价。
- MP 侧 `MechanicDrawConvert` 形状转换（圆/月环/扇/环扇/矩形/十字/胶囊/弧胶囊/三角汤→本地几何），经 AoeVisibilityRegistry 进雷达悬浮窗与真实世界绘制双视图（来源键 `module/{模块}`，用户配色与按机制开关生效）。
- 雷达 extras 图层：眼睛标记、世界文字、点名标记字母；文字提示进诊断页。

### 2.4 导航桥（提交 a58cebf）

- 激活模块驱动 AIHintsBuilder 聚合（无模块时通用推断不接管，DodgeController 仍是通用路径；模块激活时 DodgeController 让位）。
- ForbiddenZones → 本地安全区危险区：ShapeDistance 具体类型识别直转 + 0.5 码 marching-squares 采样兜底；activation→延迟判定/持续映射；帧 diff 生命周期（同键同参不重建，不重置后台解算）。
- TemporaryObstacles→障碍；PathfindMapBounds→场地边界；ForbiddenDirections→宿主可选 MechanicControl 背对（接口缺失降级提示）；特殊模式/伤害预测进诊断。
- 仅智能躲避开启时注册；回放中不注册（铁律 3）。余量体系不叠加（BMR activation 原样传入，MP 余量照吃，结果更保守）。

### 2.5 时间轴（提交 0920510）

- `MechanicTimeline.Capture` 每帧快照激活模块状态机预测；雷达悬浮窗内容下方时间轴条（当前状态+后续≤3 条、伤害类型着色、未决分支显示「分支?」不猜选；右键菜单开关 `ShowModuleTimeline`）；MainWindow 诊断页摘要；回放窗口机制预测条（M7 接线）。

### 2.6 回放接入（提交 cdd2187）

- 回放会话独立 MechanicSession：`ReplayFeedAdapter` 把录制世界帧/事件转 Feed DTO（实时/回放共用 `MechanicEventFactory` 防语义漂移）；状态类事件由帧通道覆盖不重复派发；ActionEffect 摘要降级合并并计数。
- ReplayController 生命周期挂接（进入/Seek 重建/退出恢复实时）；回放中绘制与时间轴照常、移动注册禁用。**既有录像即机制模块的内部验收工具。**

### 2.7 设置与界面

- 新设置：`EnableMechanicModules`（默认开）、`MechanicModuleMinMaturity`（默认 Verified）、`ShowModuleTimeline`（默认开）。主面板总览第六个模块开关「机制模块」；诊断页机制模块区块（加载/激活/压制/捕获/导航计数/提示列表/时间轴摘要）。

## 三、验收证据（内部测试工具，2026-09-23 工作区实测）

| 检查 | 结果 |
|---|---|
| 主项目构建（-p:CI=true） | 0 错误 0 警告 |
| 运行库构建 | 0 错误（18 条上游预存 CS8618，规格列明不屏蔽） |
| 全量回归 tests/ | **700 场景全过**（含绘制转换 8、导航转换与 diff 生命周期 9、时间轴行模型 3、回放适配 4） |
| 运行库测试 tests/MechanicRuntime（真实 sqpack） | **2,546 断言 0 失败**；模块启动覆盖 1,060/1,060 + 154/154；输入桥 8 场景、绘制捕获 5 场景、源完整性（含未登记改动拒绝反例）全过 |
| 斗兽棋上游同步回归 `--upstream-sync` | 40 场景全过 |
| 脚本冒烟与实际 DLL 回放生命周期 | 全过 |
| 部署 | CI=false 构建输出至插件目录，MasterPlayer.dll + MasterPlayer.MechanicRuntime.dll 就位，无旧程序集残留 |

验证命令见 [MechanicRuntime/README.md](../MechanicRuntime/README.md) 与迁移记录「验证与再生成」节。

## 四、未实现 / 已知缺口

### 4.1 影响个别机制正确性（按影响排序）

1. **OpEffectResult/IncomingEffects 未接**：击退预测（PendingKnockbacks）与待定 HP/状态确认缺通道；HP/状态由帧扫兜底，击退类组件的提前量降级。实时效果包 hook 已提供原始数据，剩余是接 OpEffectResult 包（PR 无对应事件，需扩展捕获）。
2. **补集语义丢弃**：SDInverted 族/SDPrecisePosition/SDOutsideOfUnion 及采样填充率 >65% 的禁入区不注册（「禁止在 X 之外」在本地危险区模型无表达），有诊断计数；受影响模块的该类机制无导航约束（绘制侧正常）。
3. **采样兜底精度**：不可识别距离场按 0.5 码网格角点轮廓近似，孔洞忽略、每组件只保留最大环；直转覆盖常见形状（圆/月环/扇/环扇/矩形/十字/三角/凸多边形/胶囊/弧胶囊/带孔多边形）。
4. **仅区域模块场景的导航提示不消费**（无首领模块时 AIHints 不聚合，避免与通用躲避重复）；这些区域仍有绘制与通用躲避。
5. **扩展世界状态缺口**：团队/友方 NPC 队伍槽 8–63、仇恨表 OpHateChange、FocusTarget、OpMount/OpForayInfo/OpVisibility/OpAggroPlayer、深层迷宫状态、DutyActions 未接；PR 侧 SystemLog 无参数、TargetVfx 缺目标 ID（本体未提供，不修本体）。
6. **任务模块导航**（QuestBattle 的 Condition/vnavmesh IPC/原生退本）：未转宿主控制，103 个任务模块可加载但其自动导航不生效。
7. **EAnim 历史**：插件加载前的动画不可知（录制同样只存加载后历史）。

### 4.2 只影响输出/自动化（机制提示正确性不受影响）

- GoalZones 权重函数（uptime/身位/AOE 命中数）不映射为安全区目标；PredictedDamage 不做减伤/治疗调度；敌人优先级不做自动转火（MechanicControl 可用时例外）；ActionsToExecute/疾跑/交互不执行；ImminentSpecialMode（火毒/定身/混乱）仅文字提示不门控移动；Teleporters 无解算器对应。
- 绘制保真取舍：双视图用 MP 用户配色（不还原 BMR 逐模块配色/线宽）；ComplexPoly 孔洞在预览忽略（解算不受影响）；屏幕空间直画的个别原语无世界坐标可还原时跳过。
- 模块级配置 UI（BMR 原配置窗/逐模块开关/角色分配）未移植；当前为全局开关+成熟度门槛。

### 4.3 回放边界

- 回放禁移动（铁律），走位输出不能用回放验收，靠 `--upstream-sync` 与录制夹具的离线求解；回放 ActionEffect 无效果数组（v2 录制未存，实时 hook 不受影响）；回放队伍 ContentId/SpawnIndex/LayoutID 无存档字段（适配层填近似值）；旧 v1 录像不可补。

## 五、实机待办（离线无法替代，沿用 U05 口径）

1. 游戏内热重载后核对：效果包 hook 安装、模块激活与压制切换、双视图绘制观感、导航注册与躲避联动、背对控制、时间轴条显示。
2. U01：国服客户端区域 ID 与场地高度映射实测（已 XIVAPI 正反查，未实测）。
3. 用 AutoRecordOnBattleStart 录制各副本战斗后回放验收机制输出（回放链路已就位）。
4. 逐模块完整行为验收：启动覆盖不等于战斗阶段正确；优先按用户常打内容（当前版本讨伐/零式/联盟）抽查。

## 六、里程碑提交

| 提交 | 内容 |
|---|---|
| 8d1d87b（codex） | 首批 ARR 地城手写移植 + 完整对象回放 |
| 6fd8905 | 机制运行库导入，全清单构造验证（1,060+154） |
| 3197c85 | 输入桥：实时世界接入运行库 |
| 5f3c024 | 绘制桥：MiniArena 捕获接入双视图 |
| a58cebf | 导航桥：AIHints 接入安全区解算 |
| 21c2f86 | 命名整改：引入层零 BMR 命名 |
| 0920510 | 机制时间轴 |
| cdd2187 | 回放驱动机制会话 |
