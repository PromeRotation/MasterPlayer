# PromeRadar — 项目总文档（AGENTS.md）

面向开发者与 AI 代理的项目说明。上手见 [快速开始](docs/快速开始.md)，机制开发按 [ARR 解析与模拟流程](docs/ARR解析与模拟流程.md) 完成「录像分析 → 实现 → 离线模拟 → 实机验收」。历史修复和验证证据见 [开发记录](docs/开发记录.md)。

## 当前设置与控制契约

- `Svc.ClientState.IsPvP` 为全插件区域门控（包含狼狱停船厂）。进入 PvP 释放全部运行组件、事件订阅、原生 hook、绘制及控制请求，停止录制和回放；PvP 内不初始化运行组件，不接受功能指令。离开后由 Framework 更新按原设置重建，不改写用户功能开关。

- 产品名称为 **PromeRadar**；插件 ID `masterplayer`、`MasterPlayer.dll`、命名空间、配置/脚本/回放目录及 ImGui ID 沿用原标识，保持已有配置和外部脚本兼容。
- `EnableSmartMovement` 是唯一移动总开关。接近目标和跟随只向同一规划器注册目标；关闭时撤销本插件的移动、朝向、原生控制和 runtime owner 请求，绘制、提示与规划继续。执行层通过 `SafeZoneSetting.MovementAllowed` 门控移动。
- `EnableMechanics` 统一控制脚本、模块及处理器；`TerritoryEnabled` 默认按区域启用，同时作用于实时和回放，脚本逐项开关保留。模块停用或被专属脚本压制时释放通用 AOE 压制，恢复时重建会话。
- 无正式机制场地时使用局部地形：`EnableTerrainCollision=true`、`TerrainScanRadius=20`、`RadarShowTerrain=true`。主线程采集有效碰撞模型，后台 Recast 构建不可变边界；正式场地优先，未知/墙体/落差不可通行。ARR/离线回放不采样实时地形，当前录像格式也不包含地形数据。
- 地形规划图 `LocalTerrain` 与显示历史 `TerrainDisplay` 分离。场景、支撑层、深宫楼层或布局变化按契约失效；战斗结束保留地形。近身优先发布、路线期间延迟发布、同层块缓存、材质及实时碰撞复核规则见 [地形规格](docs/reference/LocalTerrainCollision.spec.md)。
- 配置格式 `SettingsVersion=1`；旧移动开关及旧脚本/模块开关分别按 OR 迁移，保存后使用新字段，首次迁移保留 `MasterPlayer.json.before-promeradar.bak`。
- `/prpr` 开关面板，`/prpr ai on/off` 控制智能走位，`rec` / `replay` 控制录制与回放；旧 `/prmp` 兼容。UI、DTR、IPC 共用插件 setter，IPC 请求入队后在 Framework 线程应用。
- 遥控为 PR3 功能，通过 `PromeRotation.Core.Core.MyPrLevel() >= 3` 判断；`MasterPlayerPlugin.RemoteControl` 提供主线程 XSZToolbox 客户端，移动受总开关控制，回放禁止发送。房间网络操作仍在工具箱 UI，接口见 [IPC 契约](docs/PromeRadarIPC.md)。
- 脚本统一通过 `CurrentPlayer` / `CurrentPlayerId` 获取视角角色，通过 `CurrentTime` 获取时钟；ARR 与 MP 离线回放均只规划和绘制。视角切换、死亡事件与回放隔离约定见 §5.6.2。

## 1. 项目定位

PromeRadar 是 **PromeRotation（PR）的卫星插件**，实现 `IPromePlugin`，由 PR 插件管理器反射加载、启用和热重载；不作为独立 `IDalamudPlugin` 加载。

- 功能包括雷达悬浮窗、真实世界绘制、智能走位、机制脚本、机制运行库和录制回放。「雷达」指 2D 悬浮窗，「真实世界绘制」指游戏内 3D 范围提示。
- 安全区完整实现由插件持有，消费者统一使用 `MasterPlayerPlugin.Instance.SafeZone`。PR 提供 GreenMove 执行、DrawManager 世界绘制、事件及移速信息；空间层边界见 [src/Spatial/README.md](src/Spatial/README.md)。
- Dalamud 服务统一使用 `ECommons.DalamudServices.Svc`，例如 `Svc.Framework`、`Svc.PluginInterface.UiBuilder`、`Svc.Log`、`Svc.Objects`。

## 2. 参考仓库与 API 查证

涉及 PromeRotation、Dalamud 或 FFXIVClientStructs（FFCS）的签名、字段及调用方式，查阅对应源码，并核对源码版本与编译引用、运行 DLL 的一致性。下表路径相对于各仓库根目录，开发者可自行安排检出位置。

| 仓库 | 源码入口 | 用途 |
|---|---|---|
| PromeRotation（本项目使用的 fork 为 `PromeRotation-1.0-XSZFork`） | `PromeRotation/` | PR 插件加载、事件、GreenMove、DrawManager 等 API 的权威来源 |
| Dalamud | 服务接口及实现 | 卫月服务、事件、插件接口与线程约束 |
| FFXIVClientStructs | 原生结构及成员函数定义 | 字段布局、指针、原生函数 |
| PromePainter | 插件入口、csproj、`src/Scripting/` | 卫星插件、设置持久化、Roslyn 编译及 owner 生命周期参考 |

本地开发默认引用实际运行的 PR DLL；发布使用固定版本的 `PromeRotation.SDK.CNGL`，发布清单记录 PR 版本与 Dalamud API 级别。宿主或 SDK 更新后重新构建并验证兼容性。

## 3. 目录结构与共享契约

```text
MasterPlayer/
├── MasterPlayer.csproj          # 插件构建；默认输出到 PR 子插件目录
├── src/
│   ├── Core/                   # 入口、设置、IPC/遥控集成
│   ├── Spatial/                # 几何、注册表、地形、Reborn 解算与移动调度
│   ├── Movement/               # Dodge 通用躲避、Follow 跟随目标
│   ├── Combat/                 # 战斗动作序列
│   ├── Mechanics/              # 机制分发；Host/ 连接独立运行库
│   ├── Scripting/              # Roslyn 脚本、门面、Visual/ 可视化脚本
│   ├── Replay/                 # 录制、回放世界、时间轴及日志对比
│   └── UI/                     # Radar/ 与 Windows/
├── Presets/                    # 每区域的实现、数据及 Entries/ 脚本入口
│   ├── Crucible/               # 斗兽奇弈
│   ├── Blunderville/           # 金碟巨豆中心
│   ├── Dungeons/               # ARR 地城
│   ├── PilgrimsTraverse/       # 朝圣路 Boss
│   ├── PalaceOfTheDead/        # 死者宫殿 151–200
│   └── Examples/               # 编译冒烟示例，不作为内置脚本加载
├── MechanicRuntime/            # 独立机制运行库
├── tests/                      # 定向回归、脚本冒烟、Fixtures/ 录像夹具
├── tools/arr/                  # opcode 映射、ARR 解码与夹具工具
├── docs/                       # 开发指南、reference/ 机制规格与开发记录
└── ThirdPartyNotices/          # 第三方许可
```

新增区域实现放在 `Presets/<区域>/`，`Entries/` 中放 `[MechanicScript]` 入口；内置发现要求 `MasterPlayer.Scripts` 或其子命名空间、公共无参构造。测试在 `tests/MasterPlayer.RegressionTests.csproj` 或区域定向项目中按现有方式链接生产实现。

### 3.1 跨模块共享契约

- 根命名空间和程序集名为 `MasterPlayer`，使用文件作用域命名空间并开启 Nullable。
- PR 实例使用 `PromeRotation.Plugin.Instance` 或 `using PRPlugin = PromeRotation.Plugin;`，避免引入整个命名空间造成类名冲突。
- 插件设置由 `MasterPlayerSettings` 经 `System.Text.Json` 保存到 `ConfigDirectory\MasterPlayer.json`；Dalamud `GetPluginConfig` 对应 PR 宿主配置，卫星插件使用自己的配置文件。外部脚本在 `ConfigDirectory\MasterPlayer\Scripts\`，录像在 `ConfigDirectory\MasterPlayer\Replays\`。`ConfigDirectory` 为 `DirectoryInfo`，拼路径取 `.FullName`。

| 组件 | 入口及职责 |
|---|---|
| `MasterPlayerPlugin` | `src/Core/MasterPlayerPlugin.cs`；创建组件、应用开关、串联更新与释放 |
| `SafeZoneRuntime` | 先于消费者创建；区域注册、后台解算、控制权与宿主移动门控 |
| `DodgeController` / `FollowController` | 通用读条危险区 / 动态跟随目标 |
| `MechanicDispatcher` / `IMechanicHandler` | 分发器逐帧 `Update`；处理器通过 `Attach(dispatcher)` 订阅并以 `Dispose` 释放，接口含 `Id` / `DisplayName` / `Enabled` |
| `MechanicHostController` | 实时与回放会话、模块输入、绘制、导航、提示和时间轴 |
| `ScriptManager` | 脚本发现、编译、启停、设置及 owner 清理，公共使用方式见 §5.6 |
| `RadarRenderer` / `RadarOverlayWindow` / `RadarView` | 世界绘制、悬浮窗外壳、悬浮窗与回放共用雷达内容 |
| `ReplayRecorder` / `ReplayController` / `ReplayWindow` | 录制、回放驱动及时间轴界面，见 §5.8 |

**更新与生命周期**：Framework 中先由机制宿主提供模块输出，再处理通用躲避和机制事件；回放还原对象/事件后更新脚本，跟随注册目标后才运行安全区解算，绘制读取已发布结果。具体组件顺序以 `OnFrameworkUpdate` 为准。切区/死亡清理实时安全区，战斗结束保留地形；回放场景由独立会话管理。卸载按依赖关系先释放消费者，再释放安全区与订阅。

**跟随**：`FollowSlot` 1–7 选择队友，0 或成员不可用时按 `FollowTargetWhenNoSlot` 回退当前可选中且存活的玩家/战斗 NPC；非法槽位不跟随。玩家距离用 `FollowDistance`，怪物距离为攻击距离减 0.5 码再加双方 hitbox；近战 3 码，远程由 `RangedAttackRange`（5–25）决定。跟随仅注册本地目标，机制站位优先；安全读条时暂停可选追击，必须脱险时保留安全攻击目标，无可站立攻击位置时停用目标窗口。目标几何动态更新不逐帧取消解算。

**通用躲避**：订阅主线程 `PromeEventBus.OnStartCast`；雷达、世界绘制或智能走位任一启用即识别读条。已解析施法者仅处理敌对 BattleNpc，未解析辅助体放行；技能形状由 `AoeShapeDatabase` 查表/推断，并按 `DodgeDangerMargin` 外扩。脚本通过 `SuppressGenericDodge`、激活模块通过 `HasActiveModule` 接管，避免重复注册。

**机制宿主**：`LiveWorldSampler` / `ActionEffectCapture` 将对象帧与事件转为 Feed DTO，`MechanicSession` 更新后由绘制桥、导航桥和时间轴消费。模块使用 `EnableMechanics` 与区域开关；专属脚本接管时暂停对应输出。`MechanicModuleMinMaturity` 为最低成熟度（0=Dummy、1=WIP、2=Contributed、3=Verified、4=AISupport，按 `>=` 筛选），只在内存应用，不写运行库配置。回放接入见 §5.8；转换细节见 [运行库规格](docs/reference/MechanicRuntime.spec.md)、[宿主规格](docs/reference/MechanicHost.spec.md)，模块清单见 `docs/reference/BMRModuleInventory.csv`。

宿主桥的 DTO 转换避免在 MasterPlayer 类型字段或闭包中保存 BossMod 值类型（如 WPos/WDir）；值类型布局会将运行库依赖带入主程序集类型加载。

## 4. 开发模式 / 构建部署

### 4.1 构建前置

1. 安装 `global.json` 指定的 .NET 11 SDK；主项目目标框架为 `net10.0-windows`。
2. 准备 Dalamud CN 开发 DLL（默认 `%AppData%\XIVLauncherCN\addon\Hooks\dev\`）及实际运行的 PR DLL。`Directory.Build.props` 自动发现单个已安装 PR 版本；多个版本时指定 `PromeRotationPath`。
3. 通过命令行属性或已忽略的 `Directory.Build.local.props` 设置 `XIVLauncherRoot` / `DalamudRoot` / `PromeRotationPath`。使用本体源码构建产物时，`PromeRotationRoot` 指向其仓库根目录，引用其 `PromeRotation\bin\Debug\`。

```powershell
dotnet build MasterPlayer.csproj -p:CI=true -p:PromeRotationPath="<PR DLL目录>" -p:DalamudRoot="<Dalamud开发DLL目录>"
```

### 4.2 构建、定向验证与部署

```powershell
# 编译验证，输出到仓库 bin/Debug/
dotnet build MasterPlayer.csproj -p:CI=true

# 只运行当前任务相关的回归
dotnet run --project tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter="<相关场景>"

# 实际 DLL 冒烟示例：普通卓异/99层区域匹配和选敌
dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- "." "<编译产物>/MasterPlayer.dll" --pt99-targeting

# 部署：直接输出到 PR 子插件目录
dotnet build MasterPlayer.csproj -c Release
```

- 默认部署目录为 `$(XIVLauncherRoot)\pluginConfigs\PromeRotation\Plugins\MasterPlayer\`；`-p:CI=true` 改为仓库 `bin/<Configuration>/`。部署前备份现有 DLL/PDB，随后在游戏内 PR 插件管理器重载；首次未发现时点「刷新全部」。
- PR 使用 shadow copy 与可收集 AssemblyLoadContext，游戏运行时可构建覆盖。内置脚本、解算器及插件 API 改动需要重编并重载；外部 `.cs` 脚本使用面板「刷新脚本」，无需文件监听。
- 验证选择定向项目或筛选参数。时序/性能问题使用相同场景比较基线；报告离线覆盖与仍需实机确认的部分。区域入口见下文，运行库测试见 ARR 流程文档。

## 5. 核心 API 速查

以下为常用调用面；具体重载及可用成员以匹配版本的源码为准（参考仓库见 §2）。

### 5.1 插件入口契约（`PromeRotation.Plugins`）

`Plugins/IPromePlugin.cs` 定义生命周期，`Plugins/PromePluginAttribute.cs` 定义反射发现元数据：

```csharp
public interface IPromePlugin : IDisposable
{
    void Initialize();
    void DrawConfigUI();
}
```

入口类使用公共无参构造及 `[PromePlugin(id, name, author, description, version)]`；插件管理器扫描 `pluginConfigs\PromeRotation\Plugins\` 的子目录，创建实例并调用 `Initialize()`，卸载/重载调用 `Dispose()`。

### 5.2 本地安全区与宿主执行接口

区域注册、场地边界、快照与开关统一使用 `MasterPlayerPlugin.Instance.SafeZone`。其 `Registry`、`Manager`、`Setting` 使用 `MasterPlayer.Spatial.SafeZone` 类型，解算类型位于 `MasterPlayer.Spatial.SafeZoneReborn.Solver`，移动状态接口位于 `MasterPlayer.Spatial.SafeZoneReborn`。PR 的同名类型不兼容，句柄必须交回原注册表。

最终移动共用 PR GreenMove；PR 旧版或 Reborn 自动移动开启或持有控制权时，插件让位并保护宿主路线。只有用户点击「关闭 PR 本体安全区自动移动」按钮才关闭本体两套自动移动。新路线立即提交，后续逐帧推进由宿主负责，不假定卫星插件回调早于 GreenMove。

`SafeZoneActivation` 只联动本地 `Setting`：雷达、世界绘制或智能走位任一启用时请求 `EnableSafeZone`，智能走位请求 `EnableAutoMovement`。关闭/卸载只撤销自身开启的状态；手动关闭后不逐帧重开，可在诊断页恢复。

| 属性 | 类型 | 说明与关键成员 |
|---|---|---|
| `SafeZone.Registry` | `SafeZoneRegistry` | 本地注册便利层，相对秒数转绝对 UTC，全部仅主线程。危险区：`AddPersistentDanger` / `AddTimedDanger` / `AddDelayedHit`（默认持续 0.6s）/ `AddDelayedDanger` / `AddDangerSequence`；目标：`AddPersistentSafeArea` / `AddTimedSafeArea` / `AddDelayedSafeArea` / `AddSafeAreaSequence`；障碍/边界：`AddObstacle` / `SetArenaBoundary` / `ClearArenaBoundary`；清理：`Remove` / `ClearDangerAreas` / `ClearGoalAreas` / `ClearObstacles` / `ClearAll` |
| `SafeZone.Manager` | `SafeZoneManager` | 本地区域与快照：`AddDangerArea` / `AddGoalArea` / `AddObstacle` / `UpdateArea` / `RemoveArea` / `ContainsArea` / `ClearAllAreas` / `BuildSnapshot(DateTime utcNow)` |
| `SafeZone.Setting` | `SafeZoneSetting` | 本地内存配置：`EnableSafeZone`、`EnableAutoMovement`、`OnlySolveInRegisteredRegion`、`DebugArenaCenter` / `DebugArenaRadius`；持久功能选择由 MasterPlayerSettings 管理 |
| `SafeZone.MovementStatus` | `ISafeZoneRebornMovementStatus` | 本地状态：`LatestSolution`、`HasControl`、`PhaseText`、`DispatchCount`、`LastDispatchUtc`、`IsAsyncSolveRunning`、`AsyncSolveStartedCount/AcceptedCount/CancelledCount/DiscardedCount` |
| `SafeZone.Teleporters` / `SetTeleporters(list)` | `IReadOnlyList<SolverTeleporter>` | 解算传送边发布通道（机制导航桥写入，仅主线程）：入口圈内 A* 扩展出口后继，耗时按移速折算进 g；空列表清除。`SolverTeleporter(Entrance, Exit, Radius, Bidirectional, UseTimeSeconds)` 位于 `MasterPlayer.Spatial.SafeZoneReborn.Solver`，经 `SolveOptions.Teleporters` → `RasterizedMap` 注入 |
| `GreenMoveSystem` | `IGreenMoveSystem` | 移动执行。`IsRunning`、`GetStatus()`、**`MoveTo(GreenMovePoint)` / `MoveTo(IReadOnlyList<GreenMovePoint>)`（替换当前路线，last-write-wins）**、`Append(...)`（追加到路线尾）、`Stop()`、`ClearQueue()`（清空后续点保留当前点）、`SkipCurrent()` |
| `DrawManager` | `DrawManager` | 世界绘制注册入口（主线程）。`Add(...)` / `AddBound(...)`（跟随实体）/ `AddTether(...)` 多重重载 → 返回 `DrawHandle`；`Remove(DrawHandle)` / `Remove(string id)`；**`RemoveByRegex(string pattern)` → int**；`Clear()` |

表中 `GreenMoveSystem` 和 `DrawManager` 仍属于 `PromeRotation.Plugin.Instance`。世界绘制通过 `HostDrawingGeometry` 转换本地几何；雷达和安全区直接使用本地几何。

配套几何类型（`MasterPlayer.Spatial.SafeZone`，`GeometryTemplate.cs` / `TransformBinding.cs`）：

- `GeometryTemplate` 派生：`CircleTemplate(float Radius)`、`DonutTemplate(float InnerRadius, float OuterRadius)`、`FanTemplate(float Radius, float AngleRadians)`、`RectangleTemplate(float Width, float Length)`、`RayRectangleTemplate(MinLocalX, MaxLocalX, Length)`（另有 `RayRectangleTemplate.Symmetric(width, length)`）、`LocalPolygonTemplate(IEnumerable<Vector2>)`。
- `TransformBinding` 派生：`StaticTransform(Vector3 Position, float Rotation)`（静态）、`DynamicTransformBinding(MissingEntityPolicy)`（动态绑定基类）；动态子类：`FollowEntityPosition(uint EntityId, Vector2 WorldOffsetXZ, float Rotation, MissingEntityPolicy)`、`FollowEntityPositionAndRotation(uint EntityId, Vector2 LocalOffsetXZ, MissingEntityPolicy)`、`FixedPositionFaceEntity(Vector3 Anchor, uint TargetEntityId, float FallbackRotation, MissingEntityPolicy)`、`BetweenTwoAnchors(AnchorSource, AnchorSource, MissingEntityPolicy)`；锚点 `AnchorSource`：`EntityAnchor(uint EntityId)` / `WorldAnchor(Vector3 Position)`。`MissingEntityPolicy`：`DisableArea` / `KeepLastResolved` / `RemoveArea`。
- FFXIV 弧度约定：0 = +Z，π/2 = +X；世界坐标 X/Z 为地面平面，Y 为高度。

### 5.3 事件源（两个，按场景选用）

**(a) `PromeEventBus`（`PromeRotation.Hosting`，静态类）——owner 作用域强类型事件，推荐首选**

```csharp
IDisposable sub = PromeEventBus.OnStartCast(owner, e => { ... });   // Action<LogSystemCastStartEvent>
PromeEventBus.OnTether(owner, e => { ... });                        // Action<LogSystemTetherEvent>
PromeEventBus.OnTargetIcon(owner, e => { ... });                    // Action<LogSystemTargetIconEvent>
PromeEventBus.OnStatusAdd(owner, e => { ... });                     // Action<LogSystemStatusAddEvent>
PromeEventBus.OnBattleStarted(owner, () => { ... });                // Action
// ... OnCancelCast / OnInterruptCast / OnEObjAnimation / OnEObjSetState / OnMapEffect /
//     OnMarker / OnTetherCancel / OnTerritoryChanged(Action<ushort>) / OnWeatherChanged(Action<uint>) 等
PromeEventBus.UnsubscribeAll(owner);   // 一次退订该 owner 的全部订阅
```

- 每次订阅返回 `IDisposable`；owner 用于批量管理（`MechanicDispatcher(object owner)` 构造参数即为此准备）。
- LogSystem 系列事件经 `Plugin.Instance.LogSystem.Events` 分发；事件 record 全在 **`PromeRotation.LogSystem`** 命名空间（`LogSystem\LogSystemEvents.cs`）。常用字段：
  - `LogSystemCastStartEvent`：`ActionId`（ushort）、`CastTime`（float 秒）、`DurationMilliseconds`、`SourceId` / `TargetId`（**ulong**，传给实体 API 时 `(uint)` 强转）、`CanInterrupt`、`EffectPosition`、`SourcePosition`（`Vector3?`）、`SourceRotation`（float）
  - `LogSystemTetherEvent`：`SourceId`、`TargetId`、`Category`（`ActorControlId`）、`TetherId`、`SourcePosition` / `TargetPosition`（`Vector3?`）
  - `LogSystemTargetIconEvent`：`SourceId`、`TargetId`、`MarkerId`

**(b) `CombatEventManager`（`PromeRotation.Managers.CombatEventManager`，静态类）——更底层的静态事件**

```csharp
CombatEventManager.OnStartCast += e => { ... };          // Action<StartCastEvent>
CombatEventManager.OnActorControlTargetIcon += ...;      // Action<ActorControlTargetIconEvent>
CombatEventManager.OnActorControlTether += ...;          // Action<ActorControlTetherEvent>
CombatEventManager.OnActorControlCancelCast += ...;
// 另有 OnActionEffect / OnActionEffectEntry / OnCreateObject / OnNpcYell / OnEnvControl(uint, ushort, ushort) 等
```

- 事件类型在 `PromeRotation.Managers.CombatEventManager.Events` 命名空间（`StartCastEvent` 等，与 LogSystem 事件是**两套不同 record**）。
- 事件由 hook/网络线程触发，回调入队后由主线程操作 SafeZone/DrawManager（见 §6 第 1 条）。
- 退订使用 `-=` 和原委托实例；推荐 `(a)` 的 owner 作用域管理。

### 5.4 GreenMove 路径点（`PromeRotation.GreenMoveSystem.GreenMovePoint`）

```csharp
public readonly record struct GreenMovePoint(
    Vector3 Position,              // 世界坐标目标点
    float Tolerance = 0.2f,        // 到达判定容差
    float SpeedMultiplier = 1.0f,  // 该点移动的瞬态速度倍率
    bool IgnoreY = true,           // true=忽略高度（目标 Y 用玩家当前 Y 替换后再判定）
    int MaxCastWaitMs = 0,         // 仅对 MoveTo/Append 启动瞬间已在进行的读条：最多等它这么多毫秒再起步；0=不等
    bool WaitForQueuedCast = false // true=移动途中检测到排队/即将开始的读条时暂停移动，让读条完成
);
```

匹配版本的宿主还提供 init 属性 `ArrivalMode`：`GreenMoveArrivalMode.Auto`（默认，中间点可经过、终点须进入容差）、`StopAtPoint`（精确站位/等待点须进入容差）、`PassThrough`（终点也可由本帧轨迹经过容差后完成）。该属性不改变上述构造与解构签名，使用前按 §2 核对宿主版本。

### 5.5 共享程序集与依赖打包

PR 共享程序集规则见 `Managers/PromeExAssemblyLoader.cs` 的 `IsSharedAssembly`；常用共享依赖包括 PR、Dalamud、ImGui、FFCS、Lumina、ECommons 和 Microsoft.CodeAnalysis。共享引用使用 `<Private>false</Private>`，版本与宿主保持一致。

- 本地构建通过 `CopyPromeRotationScripting` 复制宿主的 `PromeRotation.Scripting.dll`；SDK 构建由宿主加载器解析。
- `MasterPlayer.MechanicRuntime.dll` 随包输出；地形依赖 `DotRecast.Core` / `DotRecast.Recast` 由 `CopyTerrainDependencies` 复制，许可文件随构建输出。
- `src/Scripting/HostDependencyProbe.cs` 按「插件目录 → 宿主 PR 安装目录」探测其他依赖。新增依赖时核对加载方式和 csproj 打包规则。

### 5.6 脚本系统（`src/Scripting/`）

机制脚本继承 `MasterPlayerScript`，使用 `[MechanicScript(name, territorys, guid, version, author, note="")]`；`guid` 为设置和启停主键，`territorys` 空数组表示通用脚本。完整示例及 API 见 [脚本开发指南](docs/脚本开发指南.md)。

| API | 使用方式 |
|---|---|
| 生命周期 | `OnStart()` 订阅，`OnUpdate(float deltaTime)` 逐帧运行，`OnStop()` 收尾 |
| 视角/时钟 | `CurrentPlayer`、`CurrentPlayerId`、`CurrentTime`、`IsReplay`、`ReplayWorld`，语义见 §5.6.2 |
| `OnCurrentPlayerChanged(ulong previous, ulong current)` | 先于当帧 `OnUpdate` 通知，刷新个人输出 |
| `OnMovementDisabled()` | 宿主先清 Move/Hints，再调用此 hook；脚本在此撤销额外持有的原生控制 |
| 事件 | `OnStartCast` 等强类型方法、`OnLogEvent<TEvent>` 或 `Subscribe`，经 `IScriptHostContext` 管理 owner |
| 机制开关 | `GetMechanisms()` 提供列表，`IsMechanismEnabled(name)` 消费设置 |
| 通用躲避接管 | `SuppressGenericDodge(params uint[] actionIds)`；可视化按事件条件接管 |
| 用户设置 | `[UserSetting]` 标注 public 属性，支持 bool/int/float/string/enum/ScriptColor；`Settings.Get<T>` / `Set` 及类型化访问器 |
| 日志 | `Log(string)` / `LogError(string)` |

| 门面 | 常用接口与语义 |
|---|---|
| `SafeZone` | `AddDelayedHit` / `AddTimedDanger` / `AddPersistentDanger` / `AddDangerSnapshot` / `AddPersistentSafeArea` / `AddTimedSafeArea` / `AddObstacle` / `Remove`；跟踪本脚本句柄，默认安全区偏好 `Nearest` |
| `Move` | `IsRunning` / `GetStatus` / `MoveTo` / `Append` / `Stop` / `ClearQueue` / `SkipCurrent`；受移动开关、回放和控制权门控 |
| `Draw` | `Add` / `AddBound` / `AddTether` / `Remove`；ID 自动加 `MasterPlayer_Script_{guid}_` 前缀；`AddAoe` 只显示、不参与解算 |
| `Hints` | `SetFacing` / `ClearFacing`、`SetTarget`（PR 优先攻击目标，0 撤销）、`SetActionRestrictions` / `ClearActionRestrictions`（PR ACR 停手/禁读条/禁攻目标，同时停止相应原生自动攻击）、`SetMovementHold`（暂停 MP 移动）；PR 请求每帧续期，回放禁执行，停止时按 owner 清理 |

`SafeZone.BeginDrawingGroup(string name)` 为作用域内的 AOE 指定显示分组。危险区门面默认经 `DangerMarginInflator` 应用 `DodgeDangerMargin`，安全区/障碍不外扩；使用 owner 精确几何的区域按对应规格在解算层应用一次余量，预览保持原尺寸。场地边界由场地实现通过本地 Registry 管理。

**发现、覆盖与刷新**：内置脚本从当前程序集发现（目录与命名约定见 §3）；外部目录递归加载 `.cs`、`.dll`、`*.mechanic.json`。成功加载的外部脚本按名称或 GUID（忽略大小写）覆盖内置版本，停用仍保留覆盖，移除并刷新后恢复内置；加载失败保留错误及内置版本，同目录重复 GUID 报错并保留先加载者。区域匹配、总开关、区域开关及脚本开关共同决定启停。

`ScriptManager.ReloadScripts()` 停止旧实例、清理订阅/门面、重扫重编译，再按区域与启用状态启动并保留用户设置。每个上下文持独立 owner；脚本事件与 `OnUpdate` 在主线程运行，直接调用门面，通过脚本订阅 API 管理事件。裸 `CombatEventManager` 订阅会绕过 owner 和回放路由，脚本使用 §5.3(a) 对应的封装。单脚本编译或回调异常隔离到该脚本。

**未运行设置**：`GetScriptSettings` / `GetScriptMechanisms` 允许未运行时编辑。预览实例只绑定 `UnstartedSettings`，不创建 Host/owner 或调用 `OnStart`；构造函数、设置 getter 和 `GetMechanisms()` 仅提供元数据，默认值可经 `Settings.GetBool(key, true)` 读取。描述符替换或刷新时淘汰旧预览和回调，运行 UI 使用实际 store。验证见 `tests/ScriptCompilation/ScriptSettingsRegression.cs`。

**编译引用**：流式加载可能使 `Assembly.Location` 为空。`ScriptCompiler` 按「Location → 配置目录插件包 → 宿主 PR 安装目录」定位引用，CS0234 等错误可从引用数量、探测目录及未解析程序集日志定位；目录发现、覆盖和刷新使用实际 DLL 冒烟验证。

### 5.6.1 可视化机制脚本

- 编辑入口为「机制库 → 机制脚本 → 可视化编辑器」，实现位于 `src/Scripting/Visual/` 与 `src/UI/Windows/VisualScriptEditor.cs` / `VisualAoePreview.cs`；使用和扩展见 [编辑器文档](docs/可视化脚本编辑器.md)。
- `*.mechanic.json` 经描述符工厂创建 `VisualMechanicScript`，沿用区域匹配和 owner 生命周期；保存仅重载当前文档。同区域、同施法者可有多个 GUID。
- 规则按 Action ID、可选施法者 Data ID（Dalamud `BaseId`）筛选，绑定使用 Entity ID。事件级 `IsGenericDodgeSuppressed` 只接管实际匹配规则。
- 运行器经安全区门面登记危险区，按事件时间扣除派发延迟，清理取消、打断、过期和停止的区域。跟随位置/普通朝向使用局部偏移，其他模式使用世界偏移；预览和运行复用模板。
- `AngleOffsetDegrees` 在 `RotationOffsetTemplate.Resolve` 叠加，不改变绑定及局部位置偏移；正角左转、负角右转。危险余量递归应用于内层模板。

### 5.6.2 脚本当前玩家与回放视角

- `MasterPlayerScript` / `IScriptHostContext` 提供 `ReplayActorState? CurrentPlayer`、`ulong CurrentPlayerId`。快照含身份、位置、朝向、职业、血量、读条、状态和连线；缺失返回 null / 0。队伍分工按该 ID 从原队伍表查找。
- 正常游戏取本人；ARR 优先取当前选中玩家（含队伍列表点选），否则取观看角色；MP 离线回放只取 `ReplayWorld.LocalPlayer`。解析集中在 `src/Core/DutyReplayState.cs` / `PlayerPerspective.cs`，脚本消费统一入口，不另行读取本地玩家或判断 ARR。
- 切人先调用 `OnCurrentPlayerChanged`，再调用当帧 `OnUpdate`，无效对象以 ID 0 通知。清理个人输出及缓存，保留战斗事件和按角色维护的历史；模块提示按观看角色槽位计算，解算撤销旧路径。
- `IsReplay` 表示 ARR 或 MP 回放，`ReplayWorld != null` 仅表示 MP 离线回放。ARR 保留实时事件采样、绘制和规划；MP 使用录制世界及 `CurrentTime`，暂停冻结、Seek 重建。两种模式都由宿主阻止实际移动、朝向、自动动作和遥控。
- ARR 不转发录制者的 `OnPlayerDied` / `OnPlayerRevived`；观看角色生死取 `CurrentPlayer.Dead`。`ScriptManager.ReplayModeActive` 专用于 MP 离线会话。
- 外部旧脚本需改用统一 API 并重新编译。定向验证见 `tests/PlayerPerspectiveRegression.cs`、脚本冒烟及 `tests/RemoteControl/`；实机切人效果需游戏内确认。

### 5.7 雷达悬浮窗（`src/UI/Radar/`）

`RadarOverlayWindow` 由 `UiBuilder.Draw` 驱动，管理显示、交互及保存，内容委托共用 `RadarView`。悬浮窗使用独立 `EnableRadarOverlay`，真实世界绘制使用 `EnableRadar`。

- **显示**：`RadarOnlyWithScript` 开启时，有运行的区域专属脚本，或 `RadarShowTerrain` 开启且有局部地形图时显示；通用脚本不满足区域门槛。
- **场地和投影**：场地模式按正式场地真实边界（斗兽可用 `DisplayBounds`）适配；局部地形以玩家为中心，按 `TerrainViewRadius`（对应扫描半径）固定比例显示，其他局部视图取 `ILocalViewBoundary.ViewRadius`，其余玩家视图默认 25 码。投影用 `RadarProjection`，跟随相机旋转取 `DirH + π`，固定北向取 `π`；世界轮廓不重复平移。`RadarCanvas` / `RadarClipRegion` 按真实凹轮廓裁剪。
- **数据**：安全区快照提供边界、危险区、障碍及目标，`AoeVisibilityRegistry` 提供仅显示的预览，`MovementStatus.LatestSolution.ExecutablePath` 提供路径；模块 extras 和时间轴读取宿主快照。
- **显示独立于规划**：世界/雷达分别保存 `AoeViewSettings`，按 `action/{id}`、`script/{guid}/{group}` 等来源键过滤。`Draw.AddAoe` 同 ID 覆盖，过期/移除/脚本停止时清理；显示过滤不删除解算区域。
- **交互**：锁定和鼠标穿透、滚轮缩放、右键菜单、背景/罗盘/参考环及危险脉冲由相应 Radar 设置控制。窗口位置保留原 ImGui ID；`ShowModuleTimeline` 控制机制预测条。

### 5.8 录制回放（`src/Replay/`）

录制经 `PromeEventBus.OnLogEvent` 订阅，MP 回放通过 `ReplayScriptEventRouter` 直送脚本和独立机制会话，不向 PR 总线发布合成事件。

- **格式**：`.mpreplay.jsonl` 为 JSON Lines，新写 v2、兼容读取 v1；保存事件、CastStart `packetFacing`、零时刻完整世界及后续增量。Raw 对象图不序列化，未知事件类型跳过计数；字段与损坏数据处理见 [录制回放格式](docs/录制回放格式.md)。
- **采样与过滤**：`ReplayWorldCapture` 每 0.1 秒、保留事件前及停止时采样。玩家/召唤物来源的动作、读条和状态事件被过滤，聊天不录；对象快照仍含玩家/宠物，连线、点名、场地、战斗和 NPC 台词保留。EAnim 历史从插件加载后已观察事件建立。
- **控制 API**：`Load(path)`、`Play()`、`Pause()`、`SetSpeed(...)`、`Seek(seconds)`、`StopSession()`。按时间及文件行序还原帧和事件，暂停冻结，Seek 从开头重建，末尾暂停保留场景，停止后恢复实时；录制与回放互斥。
- **驱动顺序**：ReplayController 先于普通脚本更新；派发时先 `OnReplayEvent` 再发脚本路由，帧更新先 `OnReplayTick` 再 `UpdateReplayScripts`。模块回放暂停实时采样，保留绘制和时间轴；专属脚本接管规则与实时一致。
- **接入边界**：相对时间、实体绑定、AOE 预览使用录制时钟/世界，缺对象不回退实时。旧 v1 无法补回未录制的对象/方向，直接读取实时服务或 PR 原始 Draw 的外部脚本需改造。录制未包含的 IncomingEffects 槽位等通道维持降级语义。
- **事件与分析 API**：路由按订阅类型派发，映射可还原的 Battle/Countdown/Weather/Territory 等简单事件；无对应录制数据的事件不补造。`ReplayTimeline.Build` 构建读条及分类泳道，`ReplayComparison.Compare` 按 ActionId 聚合次数、对齐后首用及时间差。
- **界面**：ReplayWindow 共用 RadarView，支持时间轴 Seek、泳道和机制预测；日志对比可按战斗开始/首次读条或手动偏移对齐。Lumina 名称解析限 UI 层。
- **回归**：`tests/ReplayRecordingRegression.cs`、`ReplayTimelineRegression.cs`、`ReplayWorldRegression.cs` 覆盖格式、顺序、世界与时钟；`tests/ScriptCompilation/ScriptCatalogRegression.cs` 用实际 DLL 覆盖暂停、速度、Seek、实时隔离及退出清理。

#### ARR 分析与离线模拟

ARR `.dat` 保存原始服务器封包，用于机制证据；MP `.mpreplay.jsonl` 用于插件会话复现。完整流程及封包格式见 [ARR 解析与模拟流程](docs/ARR解析与模拟流程.md)，解析工具只依赖 Python 3 标准库。

```powershell
# 使用录制客户端 build 对应的 opcode 映射；build 在 ARR 头 0x10
python tools/arr/opcode_map.py --game "<游戏目录>/game" --arr "<录像>.dat"
python tools/arr/arr_decode.py "<录像>.dat" --out "analysis/<任务目录>/"
```

1. 按 Boss OID、生成、读条及死亡划定战斗窗口，检查问题前后的 cast、effect、状态、场地和玩家运动。
2. 按 Action ID 和施法来源区分 Boss/helper，形状尺寸查客户端 Action 表及 `AoeShapeDatabase`；锁定位置/朝向核对 cast 的 `loc` / `rotation` 与 effect，实际命中时刻以 effect 为准，读条后延迟按录像测量，不统一套用固定偏移。
3. 对应 `docs/reference/*.spec.md` 记录形状、时序、锁定方式和录像时间点，区分测量与推断。普通锁定读条使用落点快照和 `CastRotation.TryGet`：主线程优先取包内方向，实时事件缺失时匹配当前读条读取原生 CastRotation，回放不回退实时对象，失败不以普通朝向替代。区域须覆盖取消、打断、施法者死亡及生效后的清理；显示原尺寸与导航余量的约定见 §5.6。
4. 裁剪匿名夹具到 `tests/Fixtures/<区域>/`，记录来源录像与 SHA-256；用真实生产组件复现，再修改并跑相关场景。
5. 验证走位时用录像时钟和真实解算器，加入实测移动限制、刷新/解算/起步延迟，检查真实判定时刻的危险区命中与路线完成。
6. 分别报告原位置重放、闭环模拟和实机结果；离线覆盖后重录实战验收，保留未覆盖分支及证据。

### 区域机制与定向入口

| 区域 | 实现与规格 | 定向验证 |
|---|---|---|
| 斗兽奇弈 | `Presets/Crucible/`；[总规格](docs/reference/CrucibleAll.spec.md)、[高一规格](docs/reference/CrucibleHigh1.spec.md) | 主回归按相关斗兽场景筛选 |
| ARR 地城 | `Presets/Dungeons/`；[规格](docs/reference/ARRDungeons01.spec.md)；重制区域 1036/1037/1038，区域 ID 与 CFC 分开 | 主回归 `--filter="ARR dungeons"` |
| 金碟巨豆中心 | `Presets/Blunderville/`；[规格](docs/reference/Blunderville.spec.md)；第三关使用用户显式路网，分叉/汇合/回头按连接边执行 | 主回归 `--filter="Blunderville graph"` 及相关模拟场景 |

### 朝圣路 Boss 脚本 / PT30 / NewFolder3 联动

- `Presets/PilgrimsTraverse/Entries/` 接管 10–99 层各 Boss，脚本 ID 为 `promeradar.pt<层数>`，区域 1281–1290；默认启用且保留用户关闭设置，状态机在 `Bosses/`、宿主在 `Hosting/`。普通楼层保留 BMR，MP 持有的 Boss 输出由 `PtBossCatalog` 隔离；范围、尺寸和事件表见 [Boss 规格](docs/reference/PilgrimsTraverseBosses.spec.md)。
- 普通「卓异的悲寂歼灭战」区域 1333/CFC 1065 共用 `promeradar.pt99`，深想战 1311/CFC 1063 独立。模型/录像测试为 `tests/PilgrimsTraverse/MasterPlayer.PilgrimsTraverse.Tests.csproj -- --final-verse`，生产 DLL 冒烟用 `--pt99-targeting`。
- 朝圣路使用原始危险几何及预览，owner 只在解算层应用一次用户 `DodgeDangerMargin`，场地额外余量为 0。读条锁定与技能例外按 Boss 规格及 `AoeShapeDatabase` 处理。
- PT30（区域 1283、`promeradar.pt30`）由 `Pt30Encounter` 维护光轮、Grip/Reach、静止/运动球及 Divine Favor；`Pt30Arena` 提供真实场地与洞口障碍。运动球以二十个 0.5 秒时片覆盖十秒运动包络，预览只画实际球与当前机制，精确时窗、转场及余量见 [PT30 规格](docs/reference/PilgrimsTraverse30.spec.md)。
- Divine Favor 从 44918 首击预告开始追踪，197 点名只登记对象；方向按危险分布锁定，效果以 `(source, GlobalSequence)` 去重，八次或超时清理。Helper 零坐标原生采样不覆盖读条包锁定落点/方向，Grip→Reach 按时间窗提前规划。
- NewFolder3 通过 `PromeRadar.SetFsdSessionActive(Action<bool>)` 获取/释放运行，主线程幂等应用并只恢复本次临时开启的开关；交接使用新鲜 `HasMovementControl`，PT30 检查脚本就绪和版本。状态缓存、通知和控制接口见 [IPC 契约](docs/PromeRadarIPC.md)；`FrameMilliseconds` 为 Framework 间隔，插件自身耗时查 `[MP-PERF]`。
- PT30 定向项目为 `tests/PilgrimsTraverse30/MasterPlayer.PilgrimsTraverse30.Tests.csproj`；`--recording "<MP录像>"` 复核原始位置与生产模型，主回归 `--pt30-recording "<MP录像>"` 检查真实解算，时序/通道测试见 `tests/Pt30TemporalRegression.cs`、`tests/Pt30PassageRegression.cs`。复核原位置不等于闭环躲避成功。

### 死者宫殿151–200层脚本

- `Presets/PalaceOfTheDead/Entries/Palace151To200.cs`，ID `promeradar.potd151-200`，默认启用；区域 603–607/CFC 214–218，道中及 160/170/180/190 Boss 由 MP 接管，停用旧 BMR 宿主。
- 普通读条锁定包内落点和方向，160 冲锋与击退独立处理，180 停手仅禁攻 Boss，保留治疗/自保。机制清单、ARR 覆盖缺口和复跑命令见 [规格](docs/reference/PalaceOfTheDead151To200.spec.md)。
- 定向项目为 `tests/PalaceOfTheDead/MasterPlayer.PalaceOfTheDead.Tests.csproj`；脚本冒烟传入仓库根目录、实际 DLL 路径及 `--palace`。`PALACE_GAME_DATA` 指向游戏 sqpack，可复跑录制读条的生产路由。

## 6. 核心开发约定

1. **主线程**：SafeZone 注册/快照和 DrawManager 操作在 Framework 或 UiBuilder 主线程执行；事件选择及裸事件入队方式见 §5.3。
2. **Dispose / 热重载**：幂等释放事件、绘制及自身控制，清理各环节独立处理异常。脚本状态放实例字段，`OnStop` 收尾；刷新实例不等于重置程序集静态状态。
3. **移动指令**：GreenMove `MoveTo` 替换整条路线，只在路线变化时提交；`Stop` / `ClearQueue` / `SkipCurrent` 仅操作自身仍持有的路线。脚本直接移动还须让位于本地自动移动；宿主让位由 `HostMovement` 适配，规则见 §5.2。回位也经规划器，回放执行门控见 §5.6.2。
4. **绘制 ID**：世界绘制使用 `MasterPlayer_` 前缀；卸载通过 `DrawManager.RemoveByRegex("^MasterPlayer_")` 清理，脚本使用独立 GUID 前缀。
5. **设置与区域**：持久化统一走 `MasterPlayerSettings`（§3.1），区域使用 Territory ID，与 CFC 分开。
6. **脚本输出**：使用宿主上下文和门面，owner 管理事件/区域/绘制/控制；机制状态与视角、执行权限分别处理（§5.6）。
7. **解算行为**：持续危险和覆盖起点的硬障碍立即逃离，各区域退出后不重入；其他障碍与场地边界仍阻挡。错峰判定允许等待后转场，只提交首次等待前的路线并周期重解，无出口返回 `NoReachableSafeCell`。精确小目标、容差和有效期按真实解算器定向验证。

`src/UI/Windows/PluginUi.Toggle` 适配 PR 不同参数签名的 `PromeUi.ToggleRow`，无兼容方法时用 ImGui Checkbox；`tests/UiCompatibility` 检查实际 DLL 无直接 ToggleRow 引用。
