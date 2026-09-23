# 机制运行库迁移规格（BMR 原源码导入）

来源版本：`f6914f3e85145b5c0390d2e53376b4ffd957cca6`。目标是保留原模块行为，在 MasterPlayer 内完成事件、回放、绘制及控制适配；斗兽棋不在本次范围。库代码位于 `MechanicRuntime/`，不是新的独立 Dalamud 插件。

## 范围与计数

- 首领模块 1,060 个；区域模块 154 个，其中原 `Modules/` 下 57 个、`QuestBattle/` 下另有 97 个。
- 45 个斗兽棋模块仅留在清单的排除行，不导入新运行库；Demo 类型保留作原核心的构建依赖，不计为需要移植的战斗。
- 原清单只限定 `Modules/`，漏掉 97 个任务区域模块；新清点器扫描整个源工程的继承关系，再排除抽象类及 Demo。清单共 1,259 行，其中 1,214 行纳入目标。
- 清点器的源类型集合必须与编译后反射出的非抽象 BossModule／ZoneModule 集合一致。编译／构造／启动检查与完整机制验收分别报告。

## 已建立的宿主边界

1. 引用本机实际运行的 Dalamud／FFCS／Lumina，目标与 MP 一致为 `net10.0-windows`，仅本库启用上游所需的 `preview` 语言。
2. 显式绑定游戏数据和日志，然后初始化原配置及动作定义；不会创建原 Plugin、WorldStateGameSync、MovementOverride、ActionManagerEx。
3. 保留原 `WorldState.Execute` 和 `OpFrameStart`，让对象变化、内部事件、当前时钟与组件生命周期仍按上游顺序推进。
4. `MechanicSession` 持有原 BossModuleManager 和 ZoneModuleManager，清理幂等，不能在旧世界上倒退时间。区域模块、首领模块或状态退出回调抛异常时，仍清理后续资源和共享配置订阅，最后汇总报告异常。
5. 保留原状态机的估计时长与事件条件：超过估计读条开始时间不代表实际读条已经出现；实际开始／结束事件才推进对应状态。时间轴投影保留伤害类型及未知分支；`ExpectedEnd` 是从当前时刻预测的日期，按上游 `StateMachineWindow` 使用钳制后的状态已过时间，不让后续技能留在过去。`ElapsedInState` 保留实际已等待时间，状态机本身的原估计规则不变。
6. 障碍地图嵌入资源名保留 `BossModReborn.Pathfinding.ObstacleMaps.*`，不能因程序集改名而丢失资源。
7. 原模块的变化逐文件记录于 `MechanicRuntime/SourceChanges.json`，每项固定规范化原文修改前后的 SHA-256。除这些适配外，原文仅允许 BOM／换行表示差异；地图资源要求逐字节一致。额外改动已适配文件、改动来源基线或增加文件均由反例测试确认会被拒绝。

## 本阶段修复

| 触发 | 修复与验证 |
|---|---|
| 超魔之塔 Extreme 四个类型默认主 OID 均为 0 | 使用原 OID 枚举值 `0x4C18`／`0x4D7E`／`0x4BE7`／`0x4B67` 显式注册；保留 WIP，不提升成熟度或补猜分组 |
| 相同／缺失 OID 导致基类构造另一模块的 States | 先按实际类型查注册信息，再允许 OID 回退；全类型构造核对非空状态机 |
| Calcabrina 合体首领未加载，构造 bait 即访问空列表 | 按当前敌人列表解析来源；缺失时无 bait，出现后响应图标；来源或玩家目标死亡／销毁清理扇形及视线，迟到图标不复活已死来源；视线与引导使用同一实际来源，旧尸体不抢占原点 |
| CLL 第一战只观察到一侧首领或穿山甲尚未加载 | HP 与连线接取组件均读取动态列表；未知 HP 明示未知，未知连线终点不伪造导航区域 |
| Enuo 塔禁入名单残留到另一个世界 | 从静态字段改为组件实例字段；两独立世界的名单互不污染 |
| 冰面组件读取原 MovementOverride／Camera 单例 | 改读世界中的移动模式与镜头；标准模式取角色朝向，传统模式取镜头朝向加 180°；离线不需要签名扫描 |
| 某个模块释放或状态退出回调抛异常 | 其他模块、组件及共享订阅仍清理，异常全部报告；用区域模块与首领同时失败的用例验证 |

## 必须继续完成，不能计作已经移植

| 接口 | 原语义与当前缺口 |
|---|---|
| 实时输入 | 从 MP 对象／事件桥生成原 Operation；补状态槽位、SpawnIndex／LayoutID、CFC、完整玩家／同盟／NPC 队伍与运动历史。避免采样和瞬时事件重复派发 |
| 动作效果包 | PR LogSystem 的逐目标摘要缺 MainTargetID、完整 Effects、SourceSequence、动画锁及包朝向；不能用首个受击目标或对象朝向代替。需要新的捕获及可录制 DTO，优先在 MP 内完成 |
| 扩展世界状态 | Waymarks、Client DutyActions／FATE、深层迷宫房间／地图／道具／陷阱、SystemLogMessage 等；当前 MP v2 回放还不包含全部这些输入 |
| 冰面输入 | `LegacyMovement` 已在原世界模型建立；MP 的实时采样和录制／还原接入尚未完成 |
| 绘制 | 捕获全部原背景／前景／敌人绘制，包括颜色、线宽、文字、眼睛、三角网格和带孔填充；`ActiveAOEs` 不是完整绘制入口，不能只枚举 AOE |
| 几何与导航 | 将原距离场、持续约束、复合场地、连续 GoalZones 评分映射到 MP；不能用大圆代替多边形或把所有目标分数压成同优先级绿圈 |
| AI 输出 | 消费目标优先级、禁向、伤害预测、技能／交互／移动请求。PR 的现有移动与控制能力优先；缺少的宿主能力需真实实现或明确记录 |
| 任务导航 | 原 QuestBattle 仍含 Condition、vnavmesh IPC、原生退本及原 AI 单例门控。运行库的当前数据／日志绑定不会触发它们，但实时接入前必须转为宿主控制，不能将当前离线 noop 路径算作任务自动执行 |
| UI 与配置 | 原机制配置、角色分配、模块开关、预测时间轴及诊断信息接入 MP，并确认热重载后的依赖程序集身份 |
| 完整行为验收 | 全量启动测试不能证明每个后续阶段、机制或绘制已工作；继续用事件序列／回放覆盖所有组件及上述桥接能力 |

## 当前内部验收边界

真实本机 sqpack 已通过校验和检查读取 Action／Item／Trait／CFC／Territory 等表。使用新编译的运行库执行全清单构造和初始更新，禁止把原已编译 BMR DLL 的成功当成新库验证结果。输出文件 [MechanicRuntimeCoverage.csv](MechanicRuntimeCoverage.csv) 包含每个类型的注册、构造、初始更新、阶段数量和激活阶段。

2026-09-22 内部测试原始摘要：

```text
PASS upstream source and obstacle resources have only documented adaptations
Module startup coverage: 1060/1060 bosses, 154/154 zones; does not certify full encounter behavior
PASS P1S timeline waits for actual casts and preserves damage flags
PASS host session discovers boss and zone modules from world operations
PASS host session releases all managers and subscriptions after cleanup failures
PASS: 2491 assertions; 0 failures
```

库首次完整构建还有 18 条诊断：17 条导入源码的 `CS8618` 空值初始化警告、1 条移除原入口后 Camera 单例未赋值的 `CS0649`。没有用统一屏蔽来隐藏它们，需在相关能力接入／行为核验时逐项处理。主插件与既有测试仍独立构建，新增运行库尚未进入已部署的 MasterPlayer.dll。
