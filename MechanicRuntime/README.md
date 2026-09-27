# 机制运行库（BMR 原源码导入）

此库保留 BMR 模块、组件、状态机、配置、动作定义、几何及障碍地图，供 MasterPlayer 接入。它不实现 `IDalamudPlugin`。源码导入、独立构建与离线模块启动验收已完成；宿主桥（`MechanicHost/`，M3–M7）已完成实时世界喂入、效果包捕获、双视图绘制、安全区导航、机制时间轴与回放驱动，本库随插件一同部署。逐模块完整战斗行为验收仍未完成，不能据此宣称全副本可用。

来源：`BossModReborn-7.56` 提交 `f6914f3e85145b5c0390d2e53376b4ffd957cca6`。`Upstream/` 包含其 `BossMod/` 下 C# 源码与 `Pathfinding/ObstacleMaps/` 资源，排除：原 `Framework/Plugin.cs`、`Modules/Dawntrail/Foray/Crucible/`、构建产物，以及 2026-09-27 移除的寻路引擎与 AI/移动执行控制（`Pathfinding/` 的 Map/ThetaStar/NavigationDecision/MapVisualizer、`AI/`（仅保留 AIConfig）、`Framework/` 的 ActionManagerEx/MovementOverride/IPCProvider/DTRProvider/MultiboxManager/WorldStateGameSync、`BossModule/AIHintsVisualizer.cs`、`Debug/`、`Autorotation/MiscAI/`、`Autorotation/UIRotationWindow.cs` 及 3 个模块侧 AIRotationModule 子类——MP 使用自有 SafeZoneReborn 解算与 PR GreenMove 移动，这些上游文件无构造者；排除清单以 `tests/MechanicRuntime/SourceIntegrity.cs` 为准）。障碍位图加载器（ObstacleMapDatabase/ObstacleMapManager）与 417 个位图资源保留，供后续 MP 场地障碍接入。保留支持类型是为了闭合模块、配置和动作定义的依赖；保留某类不等于启用对应控制器。

所有 C# 文件为 UTF-8 无 BOM。除 BOM／换行表示以外，已知语义改动集中在 [SourceChanges.json](SourceChanges.json) 的 25 个文件；源完整性测试对照原仓库，检查文件集合、全部资源字节及未登记文件的原文，并核对每个适配文件修改前后的 SHA-256。哈希以去除 BOM、统一 LF 后的 UTF-8 原文计算，适配文件中额外改动也会被拒绝。原始许可见 [BossModReborn.LICENSE](../ThirdPartyNotices/BossModReborn.LICENSE)，内置第三方源码保留原版权头。

初始化顺序：真实 `Lumina.GameData` 与日志 → `MechanicRuntimeEnvironment.Initialize` → `MechanicSession`。配置只初始化一次，避免已缓存配置的原模块持有旧实例。环境不拥有游戏数据的生命周期；不同数据实例不能在同一已初始化程序集内替换。

每帧先 `BeginFrame`，按实际顺序 `Execute(WorldState.Operation)`，最后 `Update`。停止／后跳新建会话；暂停可以重复同一时间，禁止倒退世界时钟。`Dispose` 清理首领、区域模块和事件订阅；某一环节抛异常仍清理后续资源，最后汇总报告异常，重复释放不重做。`MechanicTimeline.Capture` 保留当前阶段、状态、原始时长和伤害标记；超时等待按上游界面钳制剩余时间，未来节点预测随当前时刻顺延，实际已等待时间单独保留。遇到未决分支返回分支 ID，不挑选一个分支假装已确定。

独立构建：

```powershell
rtk dotnet build MechanicRuntime/MasterPlayer.MechanicRuntime.csproj
```

真实数据验收（第二个位置参数也可用 `FFXIV_SQPACK` 环境变量提供；第四个参数开启原文及资源完整性检查）：

```powershell
rtk dotnet run --project tests/MechanicRuntime/MasterPlayer.MechanicRuntime.Tests.csproj -- "C:/Users/xiaos/Documents/GitHub/MasterPlayer" "C:/Games/最终幻想XIV/game/sqpack" "C:/Users/xiaos/Documents/GitHub/MasterPlayer/docs/reference/MechanicRuntimeCoverage.csv" "C:/Users/xiaos/Documents/GitHub/BossModReborn-7.56"
```

测试以真实客户端数据表和原源码类型执行。全清单只覆盖发现、构造、初始更新及异常，完整战斗机制需更多事件序列；每个类型的结果见 [MechanicRuntimeCoverage.csv](../docs/reference/MechanicRuntimeCoverage.csv)。`active_phase=-1` 表示合成的初始场景未满足该模块的开战条件，不能计为该场战斗机制通过。专项用例另验普通读条结束、P1S 时间轴、伴随对象迟加载、Enuo 会话隔离、冰面方向及宿主会话清理。

仍需完成的适配与验收见 [MechanicRuntime.spec.md](../docs/reference/MechanicRuntime.spec.md) 和 [迁移报告](../docs/BMR全量迁移记录.md)。本库构建警告（17 条 CS8618、2 条 CS0649、6 条 CS0618，均为上游预存或随执行层移除产生的良性项）已在规格中列明，未屏蔽。
