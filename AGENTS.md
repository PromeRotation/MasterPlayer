# PromeRadar — 项目总文档（AGENTS.md）

面向后续 AI 代理与开发者的项目权威文档。上手步骤见 [docs/快速开始.md](docs/快速开始.md)；机制问题的标准工作流（ARR 录像解析 → 机制实现 → 离线模拟 → 实机验收）见 [docs/ARR解析与模拟流程.md](docs/ARR解析与模拟流程.md)；按日期的修复与验证记录见 [docs/开发记录.md](docs/开发记录.md)。动手写代码前先读完本文；涉及 PromeRotation、Dalamud（卫月）或 FFXIVClientStructs（FFCS）的 API 签名与用法，**一律查阅对应仓库源码**（路径见下）。

## 当前设置与控制契约

- 产品名称为 **PromeRadar**。保留 `masterplayer` 插件 ID、`MasterPlayer.dll`、命名空间和现有配置/脚本/回放目录，维持 PR 启用状态与外部脚本兼容；窗口显示新名但保留 ImGui ID。
- `EnableSmartMovement` 是唯一移动总开关，取代 `EnableAutoDodge` / `EnableFollow`。接近目标只向同一规划器注册目标，已移除直线跟随执行分支。关闭时立即撤销脚本移动/朝向、斗兽控制、糖豆人原生控制和 runtime owner 请求；绘制、提示及安全区规划继续。执行层 `SafeZoneSetting.MovementAllowed` 阻断 runtime 请求绕过总开关，回放继续禁止移动。
- `EnableMechanics` 统一控制脚本、模块及原处理器；移除 `EnableScripts` / `EnableMechanicModules`。`TerritoryEnabled` 按区域默认启用，同时门控实时/回放的脚本与模块；脚本原逐项开关保留。模块禁用或被专属脚本压制后不得继续压制通用 AOE，恢复时重建会话以丢弃旧状态。
- 旧移动开关按 OR 迁移；旧脚本/模块总开关按 OR 迁移。保存新配置不再写旧字段，首次覆盖旧格式前保留 `MasterPlayer.json.before-promeradar.bak`。配置格式字段 `SettingsVersion=1`。
- `/prpr` 开关面板，`/prpr ai on/off` 控制智能走位；`rec` / `replay` 保留。旧 `/prmp` 兼容。DTR `PromeRadar AI：开/关` 左键切换，同 UI/IPC 共用插件 setter；IPC 请求入队后在 Framework 线程应用，通道与状态字段见 `docs/PromeRadarIPC.md`。
- 脚本增加 `protected virtual OnMovementDisabled()`；宿主关闭智能走位时先清 Move/Hints 门面，再调用此 hook。脚本额外持有原生控制时必须覆盖并撤销。不得停止其他来源的 GreenMove。

下文旧开关/界面描述与本节冲突时，以本节及当前源码为准；参考仓库、主线程纪律及机制规格继续有效。

## 1. 项目定位

MasterPlayer 是 **PromeRotation（PR）的卫星插件**，不是独立 Dalamud 插件：

- **不实现 `IDalamudPlugin`**，不被 Dalamud 直接加载；实现的是 PR 的 `IPromePlugin` 契约，由 PR 插件管理器（`PromePluginManager`）在游戏内反射加载、启用、热重载。
- 功能组件：**雷达悬浮窗（RadarOverlayWindow，2D 俯视小地图）+ 真实世界绘制（RadarRenderer，3D 世界范围提示）+ 智能躲避（Dodge）+ 战斗中跟随（Follow）+ 智能机制处理（Mechanics）+ 机制脚本框架（Scripting）+ 录制回放（Replay，事件录制与时间轴回放，用于测试脚本）**。术语约定：「雷达」专指雷达悬浮窗；游戏内 3D 世界范围提示叫「真实世界绘制」。
- **安全区完整实现由插件持有**（`src/Spatial/SafeZoneReborn`、所需 `SafeZone` 数据与注册层、`Geometry`），所有脚本、跟随、雷达与诊断使用 `MasterPlayerPlugin.Instance.SafeZone`。PR 本体提供 GreenMove 移动执行、DrawManager 世界绘制、事件和移速信息；安全区不再调用本体解算器。来源与接口边界见 `src/Spatial/README.md`。
- Dalamud 服务不经构造函数注入（卫星插件没有 `[PluginService]`），统一走 ECommons 静态门面 `Svc`（`ECommons.DalamudServices.Svc`：`Svc.Framework.Update`、`Svc.PluginInterface.UiBuilder.Draw`、`Svc.Log`、`Svc.Objects` 等）。

## 2. 参考仓库

| 仓库 | 路径 | 用途 |
|---|---|---|
| PromePainter | `C:\Users\xiaos\Documents\GitHub\PromePainter` | 插件模板：同为 PR 卫星插件，入口/csproj/设置持久化/Dispose 模式照抄它；脚本系统（Roslyn 编译 + PromeEventBus owner 作用域 + 描述符注册表）也照抄它 |
| PromeRotation 本体源码 | `C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork`（本体项目在 `PromeRotation\` 子目录） | PromeRotation API 签名的唯一权威来源 |
| Dalamud（卫月）主仓库 | `C:\Users\xiaos\Documents\GitHub\Dalamud` | 查询卫月本体的服务接口、事件、插件接口及 API 用法，以接口定义、实现和源码注释为准 |
| FFXIVClientStructs（FFCS） | `C:\Users\xiaos\Documents\GitHub\FFXIVClientStructs` | 查询游戏原生结构、字段布局、指针及成员函数等互操作 API 的定义与用法 |

查询 API 时按归属查阅对应仓库：PR 封装查 PromeRotation，卫月服务与接口查 Dalamud，游戏原生结构与函数查 FFCS。核对具体签名、类型和调用约束，不凭记忆臆造成员；同时确认源码版本与当前编译引用及运行环境匹配。

> **警告：编译必须引用用户实际运行的那份 PromeRotation.dll**（默认自动发现单个已安装版本，可用 `PromeRotationPath` 指定 DLL 目录，或经 `PromeRotationRoot` 引用本体仓库的 `bin\Debug`）。PR 本体更新后必须重新编译本插件，否则运行时出现 `MissingMethodException` / 类型身份不一致。分发时目标用户的 PR 版本必须与编译引用版本一致。

## 3. 目录结构与共享契约

```
MasterPlayer/
├── MasterPlayer.csproj           # 构建配置（引用 PR 本体 bin，产物直出到 PR 插件目录）
├── AGENTS.md / README.md
├── src/                          # 插件本体（命名空间仍为 MasterPlayer.*，与目录无关）
│   ├── Core/
│   │   ├── MasterPlayerPlugin.cs # 插件入口（IPromePlugin + [PromePlugin]）
│   │   ├── Settings/             # MasterPlayerSettings（MasterPlayer.json）、SafeZoneActivation、AoeViewSettings
│   │   └── Integration/          # 智能走位开关、IPC
│   ├── Spatial/                  # 插件自有几何、注册表、快照、Reborn 解算器与移动调度（SafeZoneRuntime / HostMovement）
│   ├── Movement/
│   │   ├── Dodge/                # 通用读条躲避、危险区外扩、AOE 形状推断
│   │   └── Follow/               # 战斗中跟随（Reborn 动态目标）
│   ├── Mechanics/
│   │   ├── MechanicDispatcher.cs / IMechanicHandler.cs / Handlers/
│   │   └── Host/                 # 机制运行库宿主桥：输入（M3）、绘制（M4）、导航（M5）、时间轴（M6）、回放（M7）
│   ├── Scripting/                # 脚本框架：Roslyn 热编译、描述符注册表、owner 上下文、三门面；Visual/ 可视化脚本
│   ├── Replay/                   # .mpreplay.jsonl 录制、回放世界、时间轴与多日志对比
│   └── UI/
│       ├── Radar/                # 雷达悬浮窗、RadarView、真实世界绘制、可见性注册表
│       └── Windows/              # 主面板、回放窗口、可视化编辑器、糖豆人路网编辑器、PluginUi
├── Presets/                      # 内置预设：每个区域一个目录，实现 + 数据 + Entries/ 脚本入口
│   ├── Crucible/                 # 斗兽奇弈：框架（Engine/Output/Model…）、Source/ 各组组件、Entries/ 五盘入口
│   ├── Blunderville/             # 金碟巨豆中心：规划器、路网预设、Entries/ 入口
│   ├── Dungeons/                 # ARR 地城：场地轮廓、技能表、Entries/ 三个入口
│   └── Examples/                 # 示例脚本（编译进 DLL 做冒烟，不作为内置脚本加载）
├── MechanicRuntime/              # 机制运行库（独立项目，打包为 MasterPlayer.MechanicRuntime.dll）
├── tests/                        # 回归（MasterPlayer.RegressionTests）、脚本冒烟、界面兼容、运行库测试；Fixtures/ 录像夹具
├── tools/arr/                    # ARR 录像解析（opcode 映射、事件解码）
├── docs/                         # 快速开始、ARR 流程、脚本指南、开发记录、reference/ 机制规格
└── ThirdPartyNotices/            # 第三方许可
```

新增区域机制时：在 `Presets/<区域>/` 放实现，在 `Presets/<区域>/Entries/` 放 `[MechanicScript]` 入口（命名空间 `MasterPlayer.Scripts.*` 才会被发现为内置脚本），并在 `tests/MasterPlayer.RegressionTests.csproj` 按现有区域的写法链接实现文件。

### 3.1 跨模块共享契约（逐字一致，引用他模块时严格按此签名，不得臆造成员）

通用约定：

- 根命名空间 `MasterPlayer`；程序集名 `MasterPlayer`。
- 引用本体统一全限定写 `PromeRotation.Plugin.Instance`。**不要 `using PromeRotation;`**（避免 `Plugin` 等类名冲突）；可用 using 别名，如 `using PRPlugin = PromeRotation.Plugin;`。
- 代码风格参照 PromePainter：文件作用域命名空间、`Nullable` 开、尽量无注释。

**MasterPlayerPlugin**（`MasterPlayerPlugin.cs`，命名空间 `MasterPlayer`）

```csharp
[PromePlugin(id: "masterplayer", name: "MasterPlayer", author: "XSZ",
    description: "雷达悬浮窗 + 真实世界绘制 + 智能躲避 + 战斗中跟随 + 机制脚本框架", version: "0.1.0.0")]
public sealed class MasterPlayerPlugin : IPromePlugin
{
    public static MasterPlayerPlugin? Instance { get; private set; }
    public Spatial.SafeZoneRuntime SafeZone { get; }
    internal Mechanics.MechanicDispatcher? Mechanics { get; }   // 仅供 MainWindow 读取机制处理器列表
    internal MechanicHost.MechanicHostController? MechanicHost { get; }  // 仅供 MainWindow 读取 机制模块宿主诊断
    internal Scripting.ScriptManager? Scripts { get; }          // 仅供 MainWindow 读取脚本管理器
    public void Initialize();
    public void DrawConfigUI();
    public void Dispose();
    private void OnFrameworkUpdate(IFramework framework);
}
```

- `Initialize()`：设 `Instance` → 触发 `Settings.MasterPlayerSettings.Instance` 加载 → 创建 `_safeZone` 与切区/死亡/战斗结束订阅 → 构造 `SafeZoneActivation` 并应用持久化的功能开关 → 依次构造 `_radar = new Radar.RadarRenderer()`、`_dodge = new Dodge.DodgeController()`、`_mechanics = new Mechanics.MechanicDispatcher(this)`、`_mechanicHost = new MechanicHost.MechanicHostController()`、`_scripts = new Scripting.ScriptManager()`（随即 `RefreshCatalog()`）、`_replayRecorder = new Replay.ReplayRecorder()`、`_replayController = new Replay.ReplayController(_scripts, _replayRecorder, _mechanicHost)`、`_replayWindow = new Windows.ReplayWindow()`、`_radarOverlay = new Radar.RadarOverlayWindow()`、`_follow = new Follow.FollowController()`、`_mainWindow = new Windows.MainWindow()` → `Svc.Framework.Update += OnFrameworkUpdate` → `Svc.PluginInterface.UiBuilder.Draw += _mainWindow.Draw` → `+= _radarOverlay.Draw` → `+= _replayWindow.Draw` → 注册聊天指令 `/prmp`（`Svc.Commands.AddHandler`，无参开关设置面板，`/prmp rec` 或 `/prmp record` 开始/停止录制，`/prmp replay` 开关录制回放窗口）。
- `OnFrameworkUpdate(IFramework framework)`：先取 `deltaTime`（秒），由 `SafeZoneActivation.Update` 应用开关，再按 `_mechanicHost` → `_dodge` → `_mechanics` → `_replayRecorder` → `_replayController` → `_scripts` → `_follow` → `_safeZone` → `AoeVisibility` → `_radar` 更新。`_mechanicHost` 在 `_dodge` 之前（机制模块当帧输出供后续组件消费）；回放激活时桥由 ReplayController 经 OnReplayEvent/OnReplayTick 驱动独立回放会话（实时采样不跑）；当前区域有 MP 区域专属脚本运行时桥自身跳过。回放控制器先还原对象帧和事件，并用录制时钟更新回放脚本；普通脚本更新不再重复推进回放。解算在实时脚本与跟随注册区域之后，绘制读取本地结果。`SafeZoneRuntime.Update` 接收 PR 移速倍率和回放移动门控，回放禁止移动但按录制时钟回收限时区域。**EnableScripts 总开关门控在 ScriptManager 内部**。另有 internal 属性 `Recorder` / `Replay` / `ReplayWindow` / `MechanicHost` 供 UI 使用。
- `DrawConfigUI()`：ImGui 按钮 `"打开 MasterPlayer 设置面板"` 调 `_mainWindow.Toggle()`，旁边提示 `/prmp` 指令。
- `Dispose()`：退订上述事件（Framework.Update、UiBuilder.Draw 三处）→ `Svc.Commands.RemoveHandler("/prmp")`（独立 try/catch）→ `SafeDispose` 十二个组件，顺序 `_replayWindow` → `_mainWindow` → `_follow` → `_radarOverlay` → `_replayController`（其 Dispose 先停回放会话）→ `_replayRecorder`（其 Dispose 先停录制）→ `_scripts`（其 Dispose 内部先 `StopAll`）→ `_mechanicHost`（退订事件 → capture.Dispose → session.Dispose，逐项 try/catch）→ `_mechanics` → `_dodge` → `_radar` → `_safeZoneActivation` → 安全区事件订阅 → `_safeZone` → `Instance = null`。
- `SafeZoneRuntime` 在所有消费者前创建；订阅切区、死亡与战斗结束以清空本地区域并取消旧解，这三个实时回调在回放期间跳过，保护录制场景。死亡期间禁止解算与下发，回放期间禁止移动。消费者和开关联动释放后，退订这三个事件并释放运行时，最后才清空 `Instance`。
- `SafeZoneActivation` 注入本地 `SafeZone.Setting`。雷达悬浮窗、真实世界绘制、智能躲避任一开启时启用其 `EnableSafeZone`；仅智能躲避请求 `EnableAutoMovement`。关闭/卸载仅撤销自己开启的本地开关，手动关闭不每帧覆写，可在诊断页恢复。功能联动不修改 PR 本体安全区。

**Settings.MasterPlayerSettings**（`src\Core\Settings\MasterPlayerSettings.cs`）

```csharp
public sealed class MasterPlayerSettings
{
    public static MasterPlayerSettings Instance { get; }   // 静态初始化时从 JSON 加载
    public bool EnableRadar = true;             // 真实世界绘制（3D 范围提示）总开关
    public bool EnableAutoDodge = false;
    public bool EnableMechanics = true;
    public bool DebugMode = false;

    // 雷达悬浮窗（RadarOverlayWindow）
    public bool EnableRadarOverlay = false;     // 悬浮窗默认关
    public int RadarCenterMode = 0;             // 0=场地基准（场地边界中心） 1=玩家摄像机基准
    public bool RadarRotateWithCamera = true;   // 画面随摄像机方位角旋转
    public float RadarScale = 1.0f;             // 悬浮窗大小倍率，完整适配场地
    public bool RadarOverlayLock = false;       // 锁定位置（鼠标穿透）
    public float RadarBackgroundOpacity = 0.55f;    // 圆盘背景不透明度，0 = 不要圆盘背景
    public bool RadarShowCompass = true;            // 罗盘方位字母
    public bool RadarShowRangeRings = true;         // 距离参考环
    public bool RadarPulseDanger = true;            // 危险区即将判定时描边脉冲
    public bool RadarOnlyWithScript = true;         // 仅在有机制脚本的区域显示
    public bool ShowModuleTimeline = true;          // 悬浮窗下方显示机制模块预测时间轴条

    public AoeViewSettings WorldAoe = new();
    public AoeViewSettings RadarAoe = AoeViewSettings.CreateRadarDefaults();
    public Dictionary<string, AoeVisibility> AoeVisibility = new(); // 来源键 → 名称、World/Radar 开关

    // 战斗中跟随（FollowController）
    public bool EnableFollow = false;
    public int FollowSlot = 0;                  // 0=不指定小队槽位；1~7=PartyHelper.GetParty() 下标
    public bool FollowTargetWhenNoSlot = true;  // 无有效槽位时回退跟随当前目标（玩家或战斗 NPC）
    public float FollowDistance = 1.5f;
    public CombatDistanceMode CombatDistance = CombatDistanceMode.Melee;
    public float RangedAttackRange = 25f;
    public bool FollowDuringCombat = true;
    public bool FollowOutOfCombat = false;

    // 智能躲避
    public float DodgeDangerMargin = 0.5f;      // 危险区注册时的外扩余量（DangerMarginInflator）

    public bool EnableScripts = true;
    public Dictionary<string, bool> ScriptEnabled = new();                                  // 脚本 guid → 是否启用
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> ScriptSettings = new();    // 脚本 guid → [UserSetting] 值
    public Dictionary<string, HashSet<string>> MechanismEnabled = new();                    // 脚本 guid → 关闭/开启的机制集合

    // 机制模块宿主（MechanicHost）
    public bool EnableMechanicModules = true;
    public int MechanicModuleMinMaturity = 3;  // BossMod.BossModuleInfo.Maturity 枚举值：0=Dummy 1=WIP 2=Contributed 3=Verified 4=AISupport（>= 语义）

    // 录制回放（Replay）
    public bool AutoRecordOnBattleStart = false;    // 战斗开始自动录制、战斗结束自动停止（仅自动开启的录制被自动停止）
    public void Save();
}
```

- 存储路径 `Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer.json")`（`ConfigDirectory` 是 `DirectoryInfo`，拼路径取 `.FullName`，同 PromePainter 的 `PainterSettings`），序列化用 `System.Text.Json`。

**Radar.RadarRenderer**（`src\UI\Radar\RadarRenderer.cs`）——真实世界绘制（3D 范围提示）

```csharp
public sealed class RadarRenderer : IDisposable
{
    public RadarRenderer();
    public void Update();
    public void Dispose();
}
```

**Radar.RadarOverlayWindow**（`src\UI\Radar\RadarOverlayWindow.cs`）——雷达悬浮窗

```csharp
public sealed class RadarOverlayWindow : IDisposable
{
    public RadarOverlayWindow();
    public void Draw();      // 挂 Svc.PluginInterface.UiBuilder.Draw（主线程），不再经 Framework.Update
    public void Dispose();
}
```

- 窗口壳只管显示门槛（EnableRadarOverlay/RadarOnlyWithScript）、拖拽/滚轮/右键菜单交互与防抖保存；内容渲染委托 `RadarView`（悬浮窗与回放窗口共用同一组件）。机制模块激活且 `ShowModuleTimeline` 开启时，窗口向下扩展一条预测时间轴条（当前状态行 + 后续至多 3 条 + 未决分支「分支?」，数据来自 `MechanicHostController.CurrentTimeline`/`MechanicClockUtc`，行模型由纯逻辑 `src/Mechanics/Host/MechanicTimelineView.cs` 生成，伤害类型着色 Tankbuster/Raidwide/Knockback），条区随窗口一起拖拽，右键菜单有开关。

**Radar.RadarView**（`src\UI\Radar\RadarView.cs`）——可嵌入雷达视图

```csharp
public sealed class RadarView
{
    public RadarView();
    public bool Draw(ImDrawListPtr drawList, Vector2 screenCenter, float radiusPx, bool hovered = false);
    // 返回 false = 未绘制（无场地边界且玩家不存在）。内部完成快照/中心/旋转/缩放计算与全套图层。
}
```

**Windows.ReplayWindow**（`src\UI\Windows\ReplayWindow.cs`）——录制回放独立窗口

```csharp
public sealed class ReplayWindow : IDisposable
{
    public void Draw();      // 挂 Svc.PluginInterface.UiBuilder.Draw（主线程）
    public void Toggle();
    public void Dispose();
}
```

- 「MasterPlayer 录制回放」窗口（`/prmp replay` 或主面板「录制回放」页按钮开关）：顶部录制/回放工具栏 + 录制文件列表（「回放」加载播放 + 对比勾选）；「时间轴回放」页签 = 上内嵌 RadarView + 下事件时间轴（泳道分类、滚轮缩放、左键 Seek、悬停详情、播放头跟随回放时钟、泳道显隐）；「日志对比」页签 = 对齐按钮（战斗开始/首次读条/清零）+ 每日志偏移调节 + 技能汇总表（出现 x/N、每日志次数与首用、首用最大差）+ 叠加读条泳道。技能名经 Lumina Action 表解析并缓存（仅 UI 层）。


**Follow.FollowController**（`src\Movement\Follow\FollowController.cs`）

```csharp
public sealed class FollowController : IDisposable
{
    public FollowController();
    public void Update();
    public void Dispose();
}
```

- 跟随对象：`FollowSlot` 1~7 取 `PromeRotation.Helpers.PartyHelper.GetParty()` 下标；槽位为 0 或指定成员不可用时按 `FollowTargetWhenNoSlot` 回退当前目标（存活且可选中的 `ObjectKind.Pc` / `ObjectKind.BattleNpc`）；槽位超出 0~7 则不跟随。**`FollowTargetWhenNoSlot = false` 时禁止当前目标回退（槽位 0 同样生效），有效小队槽位仍可跟随**。本地玩家不存在或已死亡时停止跟随。战斗状态经 `PromeRotation.Core.GameData.IsInCombat()` 判定，按 `FollowDuringCombat` / `FollowOutOfCombat` 门控。
- 战斗距离：`CombatDistanceMode.Melee` 使用 3 码攻击距离，`Ranged` 使用 `RangedAttackRange`（5～25，默认 25）；怪物到达半径为攻击距离减 0.5 码停步余量，再加双方 `HitboxRadius`。玩家跟随独立使用 `FollowDistance` 中心间距（0.2～10）。旧配置默认近战，保留原玩家跟随距离；远程已在射程内且安全时不主动贴脸。
- 自动躲避开启时，跟随只注册 Reborn 动态目标，危险排除保留结束安全余量。紧急躲避期间仍保留安全攻击位置，让同一寻路器选择可达的近战侧；安全读条期间暂停可选跟随，读条中必须脱险时恢复安全目标。有效机制站位始终优先。可用安全区按网格站立空间判断，不再要求占原攻击圈 25%；完全无安全攻击位置时停用目标窗口，危险结束后恢复，绝不直线强行返回。几何快照不可变，目标动态更新不每帧取消后台解算。
- 关闭自动躲避时，直接跟随的路线结束或停止后仍在距离外则重新下发；路线运行中仅在目标位置、战斗距离或目标圈大小变化时更新，不每帧重置读条等待。距离算法与规划模式一致。
- 自动躲避模式只撤销自己的本地目标句柄，不直接操作 GreenMove；本地解算关闭时不会降级为直线跟随。直接跟随只有自己下发过 MoveTo 才 Stop；插件安全区或 PR 旧版/Reborn 持有控制权时让位，关闭与卸载同样保护对方路线。回放中禁止跟随。
- 通用避险优先级：玩家读条、脚下危险扣除宿主开始余量后不足 1 秒、或处于硬障碍内时，撤销普通跟随目标，保持危险解算运行。普通目标保留宿主 0.5 米硬余量，危险结束后的 0.3 秒余量也纳入排除；按原目标覆盖范围统计安全占比，小于 25% 时停用其 GoalWindow，不能只给空几何（PR 的目标启发式仍会朝空区域中心移动）。仅目标启停递增一次版本，使旧追击解算失效；目标/危险几何正常动态更新仍不递增。精确机制 GoalPoints 不受此普通偏好过滤。

**Dodge.DodgeController**（`src\Movement\Dodge\DodgeController.cs`）

```csharp
public sealed class DodgeController : IDisposable
{
    public DodgeController();
    public void Update();
    public void Dispose();
}
```

- 事件源为 `PromeEventBus.OnStartCast`（主线程派发，无队列，回调里直接注册危险区）；雷达悬浮窗、真实世界绘制、智能躲避任一开启即识别读条，避免只开启绘制时没有危险区数据。注册前模板经 `DangerMarginInflator.Inflate` 按 `DodgeDangerMargin` 外扩；`Update()` 只做失效句柄簿记清理。
- **施法者敌我过滤**：只响应 `ObjectKind.BattleNpc` 且游戏原生 `ActionManager.ClassifyTarget(Character*) == TargetCategory.Enemy` 的读条——玩家/宠物/坐骑/事件 NPC/友方战斗 NPC（剧情、Trust 等同盟单位）一律跳过；对象表无法解析的施法者（多为场外辅助体）放行，避免丢预告。命中时把施法者 `HitboxRadius` 传给形状推断。
- **脚本压制去重**：`ScriptManager.IsGenericDodgeSuppressed(actionId)` 为 true（运行中脚本已用 `SuppressGenericDodge` 声明接管该技能）时跳过，避免通用躲避与脚本重复注册同一危险区。
- **机制模块压制去重**：`MasterPlayerPlugin.Instance.MechanicHost?.HasActiveModule == true`（机制模块已激活、ForbiddenZones 经机制导航宿主注册）时跳过（有激活模块即停用自动 AOE 推断），避免通用推断与模块危险区重复。
- **错峰判定转场**：由插件内 `MasterPlayer.Spatial.SafeZoneReborn.Solver.SafeZoneSolver` 的安全时间区间搜索处理，允许先在后判定区提前站位、等待，再进入已结束判定的区域；只向 GreenMove 下发首次等待前的路段，周期重解接续。修复只需构建并重载 MasterPlayer；回归入口为 `dotnet run --project tests/MasterPlayer.RegressionTests.csproj`。

高一 B31 四连放毒与预备点使用 R0.2、Center 目标圈；插件 Reborn 会细化小目标网格并按目标内部余量收紧移动容差。修改解算后须重新构建并加载 MasterPlayer，仅刷新脚本无法更新解算器。

持续危险区与硬障碍覆盖玩家时，Reborn 必须立即逃离、不等读条；硬障碍仅对起点包含玩家的区域允许退出，边界和其他硬障碍仍阻挡。每个持续区域独立禁止退出后重入，逃离路径保留搜索原始线段，并按网格分辨率收紧到达容差，避免停在区域内；无出口返回 `NoReachableSafeCell`。此逻辑在插件内，回归同样走 `tests/MasterPlayer.RegressionTests.csproj`。

**Dodge.DangerMarginInflator**（`src\Movement\Dodge\DangerMarginInflator.cs`）

```csharp
public static class DangerMarginInflator
{
    public static GeometryTemplate Inflate(GeometryTemplate template, float margin);
}
```

- `margin <= 0` 原样返回；Circle 半径 +margin；Donut 内径 -margin（下限 0）/外径 +margin；Fan 半径 +margin；Rectangle 宽 +2×margin / 长 +margin；RayRectangle 两侧与长各 +margin；多边形模板与未知子类原样返回。

**Dodge AOE 形状数据库**（`src\Movement\Dodge\AoeShapeDatabase.cs`）

```csharp
public enum AoeShapeKind { Circle, Donut, Fan, Rectangle, RayRectangle }

public sealed record AoeShapeDescriptor(
    AoeShapeKind Kind, float Param1, float Param2, bool FollowCaster, float ExtraDelaySeconds)
{
    public bool UseCastRotation { get; init; }
    public bool IsCharge { get; init; }   // CastType 8 冲锋：长=施法者→落点距离，锚定施法者朝向落点
}

public static class AoeShapeDatabase
{
    public static bool TryGetShape(uint actionId, out AoeShapeDescriptor? descriptor);
    public static bool TryGetShape(uint actionId, float casterHitboxRadius, out AoeShapeDescriptor? descriptor);
}
```

- 静态表查不到时走 **Lumina Action 表推断**，映射如下：2=落点/自身圆（不加 hitbox，`TargetArea`=落点型）；3=扇形（半径+hitbox，总开角取 Omen 路径 `fanXXX` 度数，缺省 180°）；4=直线矩形（长+hitbox，`XAxisModifier`=全宽）；5=自身圆（半径+hitbox）；8=冲锋矩形；10=月环（内径取 Omen 路径 `sircle_`/`sicle_`/`circle` 后两位数字，缺省 3）；12=矩形、13=扇形（均不加 hitbox）。跳过：1=单体、6/7、11=十字（无对应模板）、2/5 且 `EffectRange ≥ 30`（全屏）、`VFX.RowId == 25`（视线类）。

**Mechanics.MechanicDispatcher**（`src\Mechanics\MechanicDispatcher.cs`）

```csharp
public sealed class MechanicDispatcher : IDisposable
{
    public MechanicDispatcher(object owner);
    public void Update();
    public void Dispose();

    public event Action<LogSystemCastStartEvent>? CastStart;
    public event Action<LogSystemTetherEvent>? Tether;
    public event Action<LogSystemTargetIconEvent>? TargetIcon;
}
```

- 事件 record 命名空间以本体 `LogSystem\LogSystemEvents.cs` 为准：**`PromeRotation.LogSystem`**（`LogSystemCastStartEvent` / `LogSystemTetherEvent` / `LogSystemTargetIconEvent` 均为 `sealed record : LogSystemEvent`）。
- 注意 `LogSystemCastStartEvent.ActionId` 是 **`ushort`**（而 `LogSystemActionEffectEvent.ActionId` 是 `uint`）；传给 `AoeShapeDatabase.TryGetShape(uint, ...)` 时会发生隐式 widening，无需强转。

**Mechanics.IMechanicHandler**（`src\Mechanics\IMechanicHandler.cs`）

```csharp
public interface IMechanicHandler : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    bool Enabled { get; set; }
    void Attach(MechanicDispatcher dispatcher);
}
```

**MechanicHost.MechanicHostController**（`src\Mechanics\Host\MechanicHostController.cs`）——机制输入（M3）+ 绘制（M4）+ 导航（M5）宿主，逐字契约：

```csharp
public sealed class MechanicHostController : IDisposable
{
    public MechanicHostController();
    public void Update();      // 主线程，OnFrameworkUpdate 中排在 _dodge 之前
    public void Dispose();
    public bool IsAvailable { get; }        // MechanicRuntimeEnvironment + 会话就绪
    public bool IsSuppressed { get; }       // 当前区域有 MP 区域专属脚本运行 → 桥暂停（不采样不更新模块）
    public bool CaptureActive { get; }      // 效果包 hook 可用
    public string? LastError { get; }
    public int LoadedModuleCount { get; }
    public int PendingModuleCount { get; }
    public string? ActiveModuleName { get; }
    public string? ActiveZoneModuleName { get; }
    public IReadOnlyList<(string Text, bool IsRisk)> CurrentHints { get; }   // 激活模块 GlobalHints(false) + 玩家 TextHints
    public IReadOnlyList<MechanicRadarExtra> DrawExtras { get; }                  // 雷达 extras（Eye/Text/Sign），门控关闭时为空
    public string? DrawExtrasKey { get; }                                    // extras 来源键 `module/{moduleTypeName}`
    public string? DrawModuleName { get; }                                   // 当前绘制来源模块名
    public int CaptureErrorCount { get; }                                    // 绘制捕获异常累计
    public string? DrawCaptureError { get; }
    public bool HasActiveModule { get; }     // BossModules.ActiveModule?.StateMachine.ActivePhase != null（DodgeController 压制用）
    public bool NavigationActive { get; }    // 导航桥正在注册（模块激活 && EnableAutoDodge && 非回放）
    public int NavDangerZoneCount { get; }
    public int NavGoalZoneCount { get; }     // 补集语义/强 GoalZones 转换出的目标安全区数量（2026-09-27）
    public int NavObstacleCount { get; }
    public bool NavBoundaryActive { get; }
    public string? SpecialModeHint { get; }  // ImminentSpecialMode 文本（Pyretic/NoMovement/Freezing/Misdirection）
    public string? PredictedDamageText { get; }
    public MechanicTimelineSnapshot? CurrentTimeline { get; }  // 激活模块状态机预测（每帧 MechanicTimeline.Capture，门控关闭/无模块为 null）
    public DateTime MechanicClockUtc { get; }                  // 会话世界时钟（默认回退 DateTime.UtcNow）

    // 回放会话（M7，由 ReplayController 接线驱动；实时会话独立保留）
    internal void EnterReplaySession(ushort territory);      // 回放开始：新建回放 MechanicSession+Feed+ReplayFeedAdapter，两座 SwitchSession
    internal void RestartReplaySession();                    // Seek：销毁重建回放会话，从目标点完整快照重喂
    internal void ExitReplaySession();                       // 停止：销毁回放会话，两座切回实时会话
    internal void OnReplayEvent(LogSystemEvent ev);          // 回放事件 → 适配器 TryConvertEvent → 回放 Feed（先于脚本路由器）
    internal void OnReplayTick(ReplayWorldState world);      // 回放帧：BeginFrame(录制时钟) → ConvertFrame → Flush → session.Update → 导航/绘制/时间轴（先于 UpdateReplayScripts）
}
```

- 构造：`MechanicRuntimeEnvironment.Initialize(Svc.Data.GameData, Svc.Log)`（IDataManager.GameData 属性拿 Lumina.GameData）→ 内存覆盖 `BossModuleManager.Config.MinMaturity`（int 直接转 `BossModuleInfo.Maturity`，Dummy=0/WIP=1/Contributed=2/Verified=3/AISupport=4，`>=` 语义；**不触发 `Modified.Fire`，运行库配置不落盘**）→ `new MechanicSession(gameVersion)`（版本串取 Lumina `Repositories["ffxiv"].Version`，回退 `"live"`）→ `new MechanicWorldFeed(session)` → `new ActionEffectCapture()` → PromeEventBus 订阅（owner=this，逐个 IDisposable 退订）。初始化异常 → IsAvailable=false + LastError，其余组件照常。
- 事件→DTO：Tether/TetherCancel/TargetIcon（IconId=MarkerId 原值，Feed 内减 IDScramble.Delta）/TargetVfx（TargetId 恒 0，PR 事件无此字段）/Marker（MarkerId=param1+1，减 1 得 Sign；TargetId=0xE0000000 为清除）/MapEffect（State=Param1|Param2<<16）/EObjAnimation/EObjSetState/DirectorUpdate/NpcYell/SystemLog（Args 恒空）/PlayActionTimeline；Waymark 无总线方法，走 `OnLogEvent<LogSystemWaymarkEvent>`，只更新控制器内 8 槽状态随帧下发。回调在主线程直接 `feed.Enqueue`；门控（总开关/压制/回放）关闭时回调直接丢弃，队列不积压。
- `Update()` 门控：IsAvailable && EnableMechanicModules && !IsSuppressed && !ReplayModeActive 才执行；跳过帧清空 capture 队列并 `MechanicNavigationHost.Sleep()` + `MechanicDrawHost.Sleep()`。执行序：`LiveWorldSampler.Capture` → `capture.Drain(feed)` → `session.BeginFrame(DateTime.UtcNow)` → `feed.ApplyFrame` → `feed.Flush` → `session.Update` → `_navBridge.Update()` → `_drawBridge.Update()`。
- `LiveWorldSampler`（internal static）：全对象表遍历（SpawnIndex=表下标；EntityId 0/0xE0000000 回退 GameObjectId，0xFF 高字节丢弃）；IsAlly 用原生 `ActionManager.ClassifyTarget != Enemy`（非 Character 恒 true）；Cast 取 CastInfo（ActionType==1 为 Spell，Rotation 用 Character.CastRotation 弧度）；Statuses 逐槽带下标；逐角色扫描原生 `ActionEffectHandler.IncomingEffects` 32 槽（击退/效果数组 + TargetConfirmed），帧级稀疏上报非零槽进 `MechanicWorldFrame.IncomingEffects`（喂养器按槽差分发 OpIncomingEffect/OpEffectResult；回放无此通道）；队伍槽 0=本人（ContentId 先 Svc.Party 按 EntityId 匹配、回退 Svc.PlayerState），1-7 取 `PartyHelper.GetParty()` 顺序（含本人，跳过 [0]）；CFC 由 Lumina TerritoryType.ContentFinderCondition 解析并按区域缓存。
- `ActionEffectCapture`：`Svc.Hook.HookFromAddress<ActionEffectHandler.Delegates.Receive>(Addresses.Receive.Value, Detour)`；detour 只读包（NumTargets 截到 32，逐目标 8×ulong 原始效果）入 `ConcurrentQueue` 后必须原样调 `Original`，绝不改动画锁/不拦截动作；hook 失败一次性警告并降级（OnEventCast 不受影响）。
- **绘制桥（M4，`MechanicDrawHost`）**：`EnableRadarOverlay || EnableRadar` 且 `BossModules.ActiveModule` 与队伍槽 0 存在时，每帧 `MechanicModuleDrawCapture.Capture(module, 0, Angle(相机方位角 DirH+π))` 捕获绘制命令，经 `MechanicDrawConvert` 转换后按稳定槽位 id `module/{moduleTypeName}/{i}` 注册进 `AoeVisibilityRegistry`（填充/描边→`AddPreview` 500ms 自过期，线→`AddLine`，帧末差集移除未刷新 id）；双视图全关、无激活模块、压制或回放时 `Sleep()` 清注册（`RemovePreviews("module/")`）并清空 extras/提示。捕获异常按模块 5 秒限频记 `LastCaptureError` 与计数。文字提示聚合 `CalculateGlobalHints()` + `CalculateHintsForRaidMember(0, pc)`，经控制器 `CurrentHints` 暴露给诊断页。
- **`MechanicDrawConvert`**（internal static 纯逻辑，不引用 Dalamud/PR，tests 直接链接）：Circle/Donut/Fan 直转 MP 几何；Cone(inner>0)/Capsule/ArcCapsule/ComplexPoly（只取外轮廓，洞忽略）/ZoneTriangles（逐三角形）→ `WorldPolygonGeometry`；Cross → 双 `RectangleGeometry`（臂长 2×Range、宽 2×HalfWidth）；Rect 换算中心 `Origin + dir×(LenFront−LenBack)/2`、宽 2×HalfWidth、长 LenFront+LenBack；BMR `Angle.Rad`/`WDir.ToAngle().Rad` 与 MP 弧度约定一致（0=+Z，π/2=+X）。安全色判定用 ColorConfig 默认 ABGR 硬编码对照表（SafeFromAOE 0x80008000 / Safe 0xff00ff00 → safe，其余默认危险）；场地边框 ComplexPoly（Border 白 0xffffffff）跳过，由 MP 边界层绘制。IconWorld/ActorMarker/CardinalNames 跳过（MP 雷达已有角色/罗盘）；Eye/TextWorld/SignMarker 转 `MechanicRadarExtra`（记录 Kind/世界坐标/文本/危险标志/原色 ABGR）。Y 高度取队伍槽 0 玩家 `PosRot.Y`。
- **雷达 extras 图层**：`RadarView.DrawMechanicExtras`（DrawAreas 后、解算路径前）经 `RadarProjection` 投影，Eye 画双弧+圆点（仿 Components/Gaze.cs，配色用用户危险/安全描边色），Text 用模块原色（ABGR 与 ImU32 同字节序直接使用），Sign 画标记字母（Attack1→"1"、Bind2→"B2"、□/○/×/△）；整体按 `module/{moduleTypeName}` 键过 `AoeVisibilityRegistry.FindRule` 可见性。
- **导航桥（M5，`MechanicNavigationHost`）**：自持 `AIHintsBuilder(session.World, BossModules, ZoneModules, rsr: null)`（构造即订阅 WorldState，Dispose 释放）与 `AIHints`；仅 `BossModules.ActiveModule?.StateMachine.ActivePhase != null` 时 `builder.Update(hints, 0, false)`（有激活模块时运行库自动 AOE 推断自然停用，无模块时不消费通用推断——与 MP 通用躲避不重复），无模块/失活时清空全部注册。ForbiddenZones 经 `MechanicHintsConvert` 转模板后由 `MechanicNavAreaDiffer` 按帧差集注册到本地安全区（键 `source/activation.Ticks/序`，同键同参不动、参数变走 `Manager.UpdateArea(incrementVersion:false)`、消失 Remove；activation 未来→`AddDelayedHit(0.6s)`、default/已过→`AddPersistentDanger`；StaticTransform 快照不绑定实体，避免组件每帧重建造成双重移动；危险区按 `DodgeDangerMargin` 外扩，障碍不外扩）。补集语义（SDInverted 族/SDOutsideOfUnion/SDPrecisePosition）经 `TryConvertComplement` 取内侧安全形状走同一差集的目标通道（`IsGoal`）：注册为优先级 1、Nearest 偏好的目标安全区（不外扩），SDInvertedUnionOffset 仍跳过。TemporaryObstacles → `AddObstacle` 同 diff；场地障碍位图 `PathfindMapObstacles` → 连通域外轮廓多边形 → 障碍（位图引用+矩形缓存，静态场地零每帧成本）；机制级强 GoalZones（峰值权重 ≥100）0.5m 采样轮廓 → 目标区（500ms 节流，弱偏好不拖拽）；Teleporters → `SafeZoneRuntime.SetTeleporters` 发布解算传送边（内容签名去重，失活清空）。PathfindMapBounds 变化才 `SetArenaBoundary`、失活 `ClearArenaBoundary`；ForbiddenDirections 在 EnableAutoDodge 且判定 ≤0.75s 时经反射宿主 `PromeRotation.Hosting.MechanicControl` 请求背对（朝向=禁向中心+π，接口缺失一次提示并跳过，反射宿主 `src/Mechanics/Host/MechanicFacingControl.cs` 与脚本 Hints 门面共用）；ImminentSpecialMode/PredictedDamage 仅写诊断。仅 `EnableAutoDodge` 且非回放注册；绘制不受该开关限制。来源键 `module-nav/{moduleTypeName}` 供「按机制显示」关闭。
- **时间轴（M6）**：控制器每帧（`session.Update()` 后、模块激活且 `StateMachine.ActivePhase` 非空时）`MechanicTimeline.Capture(module)` 产出 `CurrentTimeline` 快照（条目含名称/ExpectedEnd/StateHint 伤害标记，未决分支只有分支 ID 不猜选），异常限频隔离；门控关闭/无模块/Dispose 置 null。消费侧为纯读：雷达悬浮窗时间轴条（`RadarOverlayWindow` 壳层、RadarView 之后追加，窗口高度计入条高，右键菜单开关 `ShowModuleTimeline`）与诊断页摘要共用纯逻辑 `src/Mechanics/Host/MechanicTimelineView.cs`（空名状态隐藏、剩余秒钳制非负、伤害着色优先级 Tankbuster>Raidwide>Knockback>Cast>Attention>Neutral、后续至多 3 条、分支行「分支?」）。回放会话的数据接入属 M7（回放激活时控制器整体跳过，快照为 null；ReplayWindow 预测泳道未做，记入 spec）。
- **`MechanicHintsConvert`**（internal static 纯逻辑，tests 直接链接）：ShapeDistance 运行时识别——SDCircle/SDDonut 直转、SDCone/SDDonutSector 由存储法线还原朝向与半角（coneFactor<0 时分角线反向）、SDRect/SDCross/SDTri/SDConvexPolygon/SDCapsule/SDArcCapsule 字段反射直转或多边形近似、SDPolygonWithHoles/SDComplexPolygonInvertedContains 取外轮廓（洞忽略）、SDUnion 全成员可识别才拆分；补集语义经 `TryConvertComplement` 取内侧安全形状（SDInverted 族直转、SDOutsideOfUnion 取成员并集、SDPrecisePosition 转单元格矩形、SDInvertedUnionOffset 跳过）；其余（击退二值场、Intersection、DeepDungeonLOS 等）0.5 码网格采样（范围取模块边界半径、60 码封顶，填充率 >65% 判为补集丢弃）→ 连通域 → 角点网格 marching-squares 边链接外轮廓（对角捏合 cross=+1 拆环，孔洞环随最大环丢弃）→ WorldPolygonTemplate。`ConvertObstacleBitmap`：PathfindMapObstacles 位图区域 → 连通域外轮廓多边形（锚点=rect 中心对场心像素，≤8 组件、<4 格忽略）。`ConvertGoalZones`：仅消费峰值权重 ≥100 的机制级强目标（0.5m 总和采样、峰值 50% 以上轮廓、覆盖率 >65% 丢弃）。`MapActivation`：default/已过→持续，未来→延迟秒数。`ConvertBounds`：Circle/ABRect(Rect/Square)/Custom（最大 part 外轮廓）→ MP 边界几何。`MechanicNavAreaDiffer` 差集含危险区与目标区（`IsGoal` 通道：优先级 1、Nearest、不外扩）+ 障碍三路，离线可测（SafeZoneRegistry/Manager 可脱机构造；快照在 `SafeZoneSetting.EnableSafeZone=false` 时过滤危险区）。敌人优先级/ActionsToExecute 不消费（缺口记录）；OpIncomingEffect/OpEffectResult 由 LiveWorldSampler 原生槽扫描接入（非本转换层）。注意：**BossMod 值类型（WPos/WDir/Bitmap.Rect 等）不得存入本程序集类型的字段或闭包**——值类型布局会把 MechanicRuntime 程序集依赖引入 MasterPlayer.dll 的类型加载（曾致 ScriptManager 内置发现 GetTypes 失败；引用类型字段无碍）。

**Windows.MainWindow**（`src\UI\Windows\MainWindow.cs`）

```csharp
public sealed class MainWindow : IDisposable
{
    public void Draw();
    public void Toggle();
    public void Dispose();
}
```

- 面板为分页签布局：总览（六个功能模块开关 + 状态/警告）、雷达悬浮窗、绘制外观（真实世界/雷达独立配色 + 按技能来源的显示表格）、自动化（跟随 + 智能躲避余量）、脚本（目录管理与脚本详情）、录制回放（精简入口：录制/回放状态行 + 打开独立窗口按钮 + 自动录制开关）、诊断（调试模式、本体状态、机制模块、机制处理器）。聊天指令 `/prmp` 开关面板（注册于 `MasterPlayerPlugin`，PR 已占用 `/pr`，勿冲突），`/prmp rec` 开停录制，`/prmp replay` 开关录制回放窗口。

**Scripting 脚本框架**（`src\Scripting\*.cs`，命名空间统一 `MasterPlayer.Scripting`，含 `ScriptSettingStore`）

```csharp
public sealed record ScriptMetadata(string Id, string Name, uint[] Territorys, string Version, string Author, string Note, string Region, string SourcePath);

public enum ScriptSource { LocalSource, LocalDll, Visual, BuiltIn }

public sealed class ScriptDescriptor
{
    public ScriptDescriptor(ScriptMetadata metadata, ScriptSource source, string sourcePath, Func<MasterPlayerScript> factory);
    public ScriptMetadata Metadata { get; }
    public ScriptSource Source { get; }
    public string SourcePath { get; }
    public MasterPlayerScript CreateInstance();
}

public sealed class RunningScript
{
    public ScriptDescriptor Descriptor { get; }
    public MasterPlayerScript Script { get; }
    public IScriptHostContext Context { get; }
    public ScriptSettingStore Settings { get; }
    public bool IsReplaySession { get; }        // 回放会话启动的脚本为 true（事件来自回放而非实时总线）
}

public abstract class MasterPlayerScript
{
    public IScriptHostContext Host { get; internal set; }
    public DateTime CurrentTime { get; }           // 实时UTC或录制时钟；暂停冻结
    public Replay.ReplayWorldState? ReplayWorld { get; } // 回放世界，实时为null
    public ScriptSafeZoneApi SafeZone { get; }      // => Host.SafeZone
    public ScriptMoveApi Move { get; }              // => Host.Move
    public ScriptDrawApi Draw { get; }              // => Host.Draw
    public ScriptHintsApi Hints { get; }            // => Host.Hints（朝向引导门面，回放空操作）
    public ScriptSettingStore Settings { get; }     // => Host.Settings
    protected virtual void OnStart();
    protected virtual void OnUpdate(float deltaTime);
    protected virtual void OnStop();
    internal void StartInternal();
    internal void UpdateInternal(float dt);
    internal void StopInternal();                   // finally 里 Dispose 上下文
    public virtual IEnumerable<string> GetMechanisms();
    public bool IsMechanismEnabled(string mechanism);
    protected void SuppressGenericDodge(params uint[] actionIds); // 声明接管这些读条技能，运行期间通用躲避跳过
    protected void Log(string message);
    protected void LogError(string message);
    // + 全套 protected 事件订阅方法（OnStartCast(Action<LogSystemCastStartEvent>) 等 35 个强类型 + Subscribe 兜底；
    //   含泛型 OnLogEvent<TEvent>(Action<TEvent>) where TEvent : LogSystemEvent；
    //   相较 PromePainterScript 移除 OnBalloonTalk / OnBattleTalk2 —— 本体 PromeEventBus 无此二事件）
}

// 特性（命名空间 MasterPlayer.Scripting）：
// [MechanicScript(name, territorys, guid, version, author, note="")]（MechanicScriptAttribute）
// [UserSetting(displayName)]（UserSettingAttribute，标注 public 属性，bool/int/float/string/enum/ScriptColor）
// ScriptColor（V4: Vector4，可隐式转 Vector4）

public interface IScriptHostContext
{
    DateTime CurrentTime { get; }                  // 默认实现DateTime.UtcNow，保持外部实现兼容
    Replay.ReplayWorldState? ReplayWorld { get; }   // 默认实现null，回放上下文提供独立世界
    ScriptSafeZoneApi SafeZone { get; }
    ScriptMoveApi Move { get; }
    ScriptDrawApi Draw { get; }
    ScriptHintsApi Hints { get; }                   // 机制提示门面（朝向引导；回放空操作）
    ScriptSettingStore Settings { get; }
    bool IsMechanismEnabled(string mechanism);
    void SuppressGenericDodge(params uint[] actionIds);
    void Log(string message);
    void LogError(string message);
    IDisposable Subscribe(string eventName, Delegate handler);
    // + 全套强类型事件方法（照抄 PromePainter IScriptHostContext，35 个；含泛型 OnLogEvent<TEvent>；无 OnBalloonTalk / OnBattleTalk2，本体总线未提供）
}

public sealed class ScriptSafeZoneApi { /* internal 构造；AddDelayedHit/AddTimedDanger/AddPersistentDanger/AddPersistentSafeArea/AddTimedSafeArea/AddObstacle/Remove(SafeZoneAreaHandle)（签名基于本地 SafeZoneRegistry，见 §5.2；**SafeArea 落点偏好默认 Nearest 贴边，与本体默认 Center 不同**）；Add*Danger 注册前按 DodgeDangerMargin 经 DangerMarginInflator 外扩（SafeArea/Obstacle 不外扩）；internal void Cleanup() */ }
public sealed class ScriptMoveApi { /* internal 构造；IsRunning/GetStatus()/MoveTo(GreenMovePoint)/MoveTo(IReadOnlyList<GreenMovePoint>)/Append(GreenMovePoint)/Stop()/ClearQueue()/SkipCurrent()；internal void Cleanup() */ }
public sealed class ScriptDrawApi { /* internal 构造；Add/AddBound/AddTether/Remove(string id)（签名镜像本体 DrawManager 的 id 重载，见 §5.2），id 自动加 "MasterPlayer_Script_{guid}_" 前缀；internal void Cleanup() */ }
public sealed class ScriptHintsApi { /* internal 构造 (Func<bool> isReplay)；SetFacing(float? headingRadians)（FFXIV 弧度，null 撤销）/ ClearFacing()；经 MechanicHost.MechanicFacingControl 反射宿主 MechanicControl（接口缺失一次警告跳过），回放期间空操作，脚本停止 Cleanup 撤销自身朝向 */ }

// ScriptSafeZoneApi.BeginDrawingGroup(string name) → IDisposable：为本作用域注册的区域指定 AOE 显示分组，嵌套名称组合，退出恢复。
// ScriptDrawApi.AddAoe(string id, IGeometryShape geometry, bool safe = false, float durationMs = 0f)：只供显示的世界几何，使用当前分组，不注册到解算器。
// CastRotation.TryGet(LogSystemCastStartEvent e, out float rotation)：仅主线程，优先读取实际 PR StartCastCaptureRaw.PacketFacing；缺失或非法时才匹配当前读条读取 FFCS Character.CastRotation；失效返回 false，不回退到普通朝向。

public sealed class ScriptManager : IDisposable
{
    public ScriptManager();
    public IReadOnlyList<ScriptDescriptor> Scripts { get; }
    public IReadOnlyDictionary<string, RunningScript> Running { get; }
    public string StatusMessage { get; }
    public string ErrorMessage { get; }
    public string ScriptsDirectory { get; }
    public void RefreshCatalog();
    public void ReloadScripts();            // 面板「刷新脚本」：停止全部 → 重扫目录 → 按启用状态重启（无文件监听，纯手动）
    public bool StartScript(string id);
    internal bool StartScript(string id, IScriptEventRouter? router);   // router 非空 = 回放模式启动
    public void StopScript(string id);
    public void StopAll();
    public void Update(float deltaTime);
    internal bool ReplayModeActive { get; }     // 回放会话门控：Update 跳过实时区域启停及脚本更新
    internal void UpdateReplayScripts(float deltaTime); // 由ReplayController按录制时钟调用
    internal void EnterReplayMode();
    internal void ExitReplayMode();
    public bool IsScriptEnabled(string id);
    public void SetScriptEnabled(string id, bool enabled);
    public bool IsMechanismEnabled(string scriptId, string mechanism);
    public void SetMechanismEnabled(string scriptId, string mechanism, bool enabled);
    public bool IsGenericDodgeSuppressed(uint actionId);    // 任一运行中脚本接管了该读条技能（供 DodgeController 去重）
    public bool IsGenericDodgeSuppressed(LogSystemCastStartEvent cast); // 可视化规则还检查施法者、启用状态、绑定可解析性和剩余判定时间
    public void SetStatus(string message);
    public void SetError(string message);
}
```

- 脚本目录：`Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer", "Scripts")`（即 `pluginConfigs\PromeRotation\MasterPlayer\Scripts\`，**严禁放 `Plugins\MasterPlayer\` 内**）。
- 脚本上下文 `MasterPlayerScriptContext` 为 `internal sealed : IScriptHostContext, IDisposable`，internal 构造 `(ScriptManager manager, ScriptMetadata metadata, ScriptSettingStore settings, IScriptEventRouter? router = null)`；router 缺省为 `LiveScriptEventRouter.Instance`（实时转发 PromeEventBus），回放会话由 ReplayController 注入 `ReplayScriptEventRouter`。
- **事件路由抽象**：`internal interface IScriptEventRouter : IDisposable { IDisposable Subscribe(object owner, string eventName, Delegate handler, Type? logEventType = null); }`（`src\Scripting\IScriptEventRouter.cs`）。上下文的 35 个订阅方法统一经 `_router.Subscribe(this, "<事件名>", handler, typeof(<事件record类型>))` 落地；事件名与 `PromeEventBus.Subscribe` 字符串名一致。`LiveScriptEventRouter`（`src\Scripting\LiveScriptEventRouter.cs`，单例，Dispose 为 no-op）：logEventType 命中内置类型表则调对应 `PromeEventBus.OnXxx` 强类型方法，否则反射走泛型 `OnLogEvent<TEvent>`，简单事件直接转发字符串名 API。脚本上下文 Dispose 时在 TryCleanup 链中顺带 Dispose 路由器（回放路由借此清空本地注册表）。
- `ScriptSettingStore` 公共面照抄 PromePainter：`RegisteredSettings` / `GetPropertyType` / `GetPropertyValue` / `SetPropertyValue` / `Get<T>` / `Set` / `GetBool` / `GetInt` / `GetFloat` / `GetString` / `Contains`（MainWindow 据此画设置控件，不依赖反射）。

## 4. 开发模式 / 构建部署

### 4.1 构建前置

1. **.NET 10 SDK 与实际运行的 PR DLL 就位**：`Directory.Build.props` 在当前用户仅安装一个 PR 版本时自动引用 `%AppData%\XIVLauncherCN\installedPlugins\PromeRotation\<版本>\`；多个版本时必须指定 `PromeRotationPath`。设置 `PromeRotationRoot` 时仍使用该源码仓库的 `PromeRotation\bin\Debug\` 产物。
2. **Dalamud CN 开发 DLL 就位**：`%AppData%\XIVLauncherCN\addon\Hooks\dev\` 下有 `Dalamud.dll`、`Dalamud.Bindings.ImGui.dll`、`FFXIVClientStructs.dll`、`Lumina.dll` 等（装了 XIVLauncherCN 开发者模式即有）。
3. `XIVLauncherRoot` / `DalamudRoot` / `PromeRotationPath` 可通过命令行属性或已忽略的根目录 `Directory.Build.local.props` 覆盖，主项目及测试共用。源码仓库引用仍可覆盖 `PromeRotationRoot`：
   ```bash
   dotnet build -p:PromeRotationRoot=D:\path\to\PromeRotationFork\
   ```

### 4.2 构建与部署（开发循环）

```bash
dotnet build MasterPlayer.csproj
```

- csproj 的 `OutputPath` 直接指向 **`%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\Plugins\MasterPlayer\`**（PR 子插件目录），构建即部署，无需手动拷贝。
- 开发循环：改代码 → `dotnet build` → 游戏内打开 **PR 插件管理器** → 对 MasterPlayer 执行热重载（禁用再启用，或「刷新全部」）→ 验证。
- **脚本层开发无需构建**：改 `pluginConfigs\PromeRotation\MasterPlayer\Scripts\` 下的 .cs 保存 → 面板「脚本」区点「刷新脚本」重扫重编译（无文件自动监听）；区域不匹配的脚本自动停止。
- PR 用 shadow copy + 可收集 `AssemblyLoadContext` 加载子插件，DLL 不锁文件，游戏运行中可直接覆盖构建。
- 首次加载后若插件管理器列表里没有 MasterPlayer，点「刷新全部」让它重扫 `Plugins\` 目录。
- **PR 本体升级后必须重新构建本插件**（见第 2 节警告）。
- 环境验证使用 `dotnet build -p:CI=true` 输出到仓库 `bin\Debug\`，避免验证阶段覆盖已安装插件。宿主 `SafeZoneRebornSetting` / `SafeZoneRebornMovementStatus` 由 `src/Spatial/HostMovement.cs` 缓存反射探测；旧版无该接口时仍检查旧版控制权，新版两者均检查。回归 fixture 仅在宿主提供 Reborn 接口时测试宿主 Reborn 所有权，插件本地 Reborn 测试始终执行。

## 5. 核心 API 速查

以下签名均从本体源码（`PromeRotation-1.0-XSZFork\PromeRotation\`）逐行核对。若本体更新导致出入，以源码为准并更新本节。

### 5.1 插件入口契约（`PromeRotation.Plugins`，`Plugins\IPromePlugin.cs` / `PromePluginAttribute.cs`）

```csharp
public interface IPromePlugin : IDisposable
{
    void Initialize();      // 启用时由 PR 插件管理器调用
    void DrawConfigUI();    // PR 插件管理器配置面板里的 ImGui 区段
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PromePluginAttribute : Attribute
{
    public PromePluginAttribute(string id, string name,
        string? author = null, string? description = null, string? version = null);
    public string Id { get; } public string Name { get; }
    public string? Author { get; } public string? Description { get; } public string? Version { get; }
}
```

- 插件类必须**公共、无参构造**（管理器反射 `Activator` 创建）。
- 生命周期：`Plugin` 构造 → 网络认证成功 → `PromePluginManager.Initialize()` → 扫描 `pluginConfigs\PromeRotation\Plugins\` 各子目录 → 加载程序集 → 找 `[PromePlugin]` 类 → 构造 → `Initialize()`。卸载/重载走 `Dispose()`。

### 5.2 本地安全区与宿主执行接口

区域注册、场地边界、快照与开关统一使用 `MasterPlayerPlugin.Instance.SafeZone`。其 `Registry`、`Manager`、`Setting` 使用 `MasterPlayer.Spatial.SafeZone` 类型，解算类型位于 `MasterPlayer.Spatial.SafeZoneReborn.Solver`，移动状态接口位于 `MasterPlayer.Spatial.SafeZoneReborn`。PR 的同名类型不兼容，句柄必须交回原注册表。

最终移动共用 PR GreenMove；PR 旧版或 Reborn 自动移动开启或持有控制权时，插件让位并保护宿主路线。只有用户点击「关闭 PR 本体安全区自动移动」按钮才关闭本体两套自动移动。新路线立即提交，后续逐帧推进由宿主负责，不假定卫星插件回调早于 GreenMove。

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
- **这些事件由 hook/网络线程触发**：回调里只做入队，严禁直接碰 SafeZone/DrawManager（见铁律 1）。
- 退订必须 `-=` 同一个委托实例；用 `(a)` 的 owner 作用域管理更省心。

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

### 5.5 共享程序集白名单（`Managers\PromeExAssemblyLoader.cs` → `IsSharedAssembly`）

子插件的以下依赖由宿主/进程复用解析，**插件包（输出目录）不得携带这些 DLL**——csproj 中全部 `<Private>false</Private>`：

`PromeRotation`、`PromeRotation.Scripting`、`Dalamud`、`Dalamud.Bindings.ImGui`、`FFXIVClientStructs`、`InteropGenerator.Runtime`、`Lumina`、`Lumina.Excel`、`TerraFX.Interop.Windows`、`ECommons`、`ECommons.ExcelServices`、`Microsoft.CodeAnalysis`、`Microsoft.CodeAnalysis.CSharp`

> **注意**：`Microsoft.CodeAnalysis` 两项是 2025-09 月为支持子插件 Roslyn 热编译新加入白名单的（本体 fork `PromeExAssemblyLoader.cs` 本地修改），**依赖重新构建并部署后的本体 DLL 才生效**（部署前该版本本体仍按旧白名单运行）。

例外：**仅 `PromeRotation.Scripting.dll` 需要随包拷贝到插件目录**（PR 加载器在该目录解析子插件依赖，本体版本一致无冲突），由 csproj 里 `AfterTargets="Build"` 的拷贝目标自动完成（照抄 PromePainter.csproj 的 `CopyPromeRotationScripting` target）。**`MasterPlayer.MechanicRuntime.dll` 同样随包输出**（csproj `ProjectReference` 默认 Private=true；MechanicRuntime 自身的 Dalamud/FFCS/Lumina 等引用全是 Private=false 白名单共享程序集，不拷包）。

**白名单之外的依赖不进包**（csproj 保持 `Private=false`）：PR 加载器只解析白名单到宿主，其余依赖 miss 后由 `src/Scripting/HostDependencyProbe.cs` 的 ALC `Resolving` 兜底——按「插件自身目录 → 宿主 PR 安装目录」顺序探测同名 DLL 并流式加载（宿主目录里的依赖版本天然与运行中本体一致）。该探针现在作为白名单方案的额外兜底保留。新增非白名单依赖无需改 csproj；仅当两处目录都没有该 DLL 时才会加载失败（报原始 `FileNotFoundException`）。

### 5.6 脚本系统（`src\Scripting\`，模式照抄 PromePainter `src\Scripting\`）

- **内置与目录**：仓库 `Scripts/` 使用 `MasterPlayer.Scripts` 及其子命名空间的具体脚本类（公共无参构造）由当前插件程序集反射发现，来源为 `BuiltIn`，不依赖 `Assembly.Location`，不加载 `Presets/Examples/`。配置目录 `ConfigDirectory\MasterPlayer\Scripts\` 仍递归扫描 `.cs`、`.dll`、`*.mechanic.json`；外部脚本严禁放 `Plugins\MasterPlayer\` 插件目录内。
- **机制运行库与地城**：模块清单见 `docs/reference/BMRModuleInventory.csv`（测试数据，文件名保留）；首批三个普通地城的唯一规格为 `docs/reference/ARRDungeons01.spec.md`。运行库（MechanicRuntime/）承载全部 1,214 个目标模块，宿主桥（src/Mechanics/Host/）负责实时/回放输入、双视图绘制、安全区导航与时间轴；规格见 `docs/reference/MechanicRuntime.spec.md`、`docs/reference/MechanicHost.spec.md`。重制区域 ID 为1036/1037/1038，不能使用旧区域157/164/161或CFC分组ID。新地城运行时与斗兽棋分离，AOE保存读条落点/朝向快照，绘制原尺寸与导航余量分离；定向回归筛选 `ARR dungeons`。
- **元数据与覆盖**：`[MechanicScript(name, territorys, guid, version, author, note="")]`；配置目录成功加载的脚本按名称或 GUID（忽略大小写）覆盖内置版本，停用不取消覆盖，移除并刷新后恢复内置。外部加载失败保留错误与内置版本；可视化保存、改名、删除即时重算内置覆盖。`guid` 仍为启停/设置/机制开关主键，配置目录内同 GUID 重复报错并只保留先加载者。`territorys` 空数组 = 通用脚本。
- **区域匹配**：`Territorys.Length == 0 || Territorys.Contains(Svc.ClientState.TerritoryType)`；进入匹配区域自动启动已启用脚本，离开自动停止（照抄 PromePainter `ScriptManager.IsTerritoryMatched`）。
- **手动刷新**：脚本目录**不做文件监听**；面板「刷新脚本」调 `ScriptManager.ReloadScripts()`：停止全部运行中脚本（`OnStop` + 上下文 Dispose）→ `RefreshCatalog()` 重扫外部目录并发现内置脚本 → 按启用状态重启。内置脚本改动须重新构建并重载插件；目录发现及覆盖回归由 `tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj` 运行实际编译 DLL 验证。
- **owner 清理**：每个脚本上下文持独立 ownerId（`PromeOwnerRegistry.CreateScriptSessionOwnerId()`），事件订阅全部经 `PromeEventBus` 挂在该 owner 下；停止时 `PromeOwnerRegistry.MarkClosing(ownerId)` + `PromeEventBus.UnsubscribeAll(ownerId)` + 三门面各自 `Cleanup()`（SafeZone 移除本脚本全部区域、Move 若接管过则 `Stop()`、Draw `RemoveByRegex("^MasterPlayer_Script_{guid}_")`）。
- **异常隔离**：单脚本编译失败进 `ErrorMessage`（面板红字可滚动）；运行中 `OnUpdate`/事件回调异常只停止该脚本，不影响其他脚本与插件本体。
- **编译引用定位**：部分程序集被流式加载（`Assembly.Location` 为空，如本插件自身，可能还包括宿主体）。`ScriptCompiler` 的引用解析回退链为：程序集 `Location` → `ConfigDirectory\Plugins\<程序集名>\` 包目录 → 宿主 PR 安装目录（`Svc.PluginInterface.AssemblyLocation`）；首次构建引用集时会 Info 日志记录引用数量、探测目录与未能解析的程序集名——脚本编译报 CS0234 类错误时先看这条日志。
- **三门面纪律**：SafeZone 门面注册到插件本地运行时；Move / Draw 门面调用 PR GreenMove / DrawManager，Draw 在边界转换几何。三者均维护每脚本句柄，全部主线程调用；绘制 id 自动加 `MasterPlayer_Script_{guid}_` 前缀；移动遵守铁律 3。

### 5.6.1 可视化机制脚本

- 编辑入口为「机制脚本」→「可视化编辑器」，源码位于 `src/Scripting/Visual/` 与 `src/UI/Windows/VisualScriptEditor.cs` / `VisualAoePreview.cs`；使用、UI 与扩展约定见 `docs/可视化脚本编辑器.md`。
- 递归加载 `*.mechanic.json`（新建默认存入脚本目录的 `Visual/`）；带格式版本的定义经描述符工厂创建 `VisualMechanicScript`，沿用区域匹配与独立 owner 生命周期。同区域、同施法者允许多个 GUID；保存只重载当前文档，不刷新其他脚本。
- 规则按 Action ID 与可选施法者 Data ID（Dalamud `BaseId`）筛选；绑定用事件的 Entity ID。可视化规则通过事件级 `IsGenericDodgeSuppressed` 条件接管，不能使用全局 Action ID 压制，否则会漏掉其他施法者的同技能。
- 运行器只使用安全区门面注册危险区，不操作 GreenMove 或场地边界；按事件时间扣除派发延迟，清理取消、打断、过期和停止的区域。位置与普通朝向跟随使用局部偏移，其他模式使用世界偏移；预览复用同一 PR 模板。
- 角度偏移 `AngleOffsetDegrees` 默认 0，在 `RotationOffsetTemplate.Resolve` 中叠加，不修改绑定朝向，避免同时转动局部位置偏移；`DangerMarginInflator` 递归外扩内层模板，保留角度与安全余量。正角度左转、负角度右转。

### 5.7 雷达悬浮窗（`src\UI\Radar\RadarOverlayWindow.cs`）

随实际场地轮廓显示的 2D 俯视小地图，原生 ImGui 窗口（无标题栏、无边框、`NoBackground`），由 `UiBuilder.Draw`（主线程）逐帧驱动，不受 `EnableRadar`（真实世界绘制开关）影响，独立开关 `EnableRadarOverlay`（默认关）。

- **显示门槛**：`RadarOnlyWithScript`（默认开）开启时，仅当存在运行中的**区域专属脚本**（`Territorys` 非空且匹配当前区域；通用脚本不算，未启用/未运行不算）才显示——即「只在有脚本的区域出现」。
- **中心点两模式**：`RadarCenterMode == 0`（场地基准）且快照存在 `ArenaBoundary` 时优先读取当前斗兽入口与该导航边界对应的 `DisplayBounds` 和固定场心，否则用宿主边界。`RadarArenaLayout` 按镜头旋转后的轮廓最大轴向范围等比缩放到视口，保留4像素边框空间；不会用外接圆缩小长方形、填平异形凹口或给所有地图统一外扩。模式 1（玩家摄像机基准）或无边界时以玩家为中心、固定25码圆形视野。
- **旋转**：`RadarProjection` 直接调用 PR `GeometryHelper.WorldToLocal(world, center, rotation)`，再乘比例、加屏幕中心。局部 +X 向右、−Y 向前；跟随摄像机时 `rotation = CameraManager.Instance()->GetActiveCamera()->DirH + π`，与 PR 移动相机约定一致；固定北向上和相机不可用时用 `π`。玩家朝向用 PR `PointFromRotationDistanceXZ` 后走相同投影。`GetContours()` 已是世界坐标，不二次平移旋转。
- **场地裁剪（`src/UI/Radar/RadarCanvas.cs`、`src/UI/Radar/RadarClipRegion.cs`）**：背景和内容按真实轮廓与视口相交；凹多边形由PR三角化后逐片裁剪，线段合并连续区间但不跨越凹口。面积计算先平移到局部原点并忽略退化三角形，避免窗口位置造成精度误差。边框随场地，方向文字置于轮廓外侧，距离参考环仍表示实际距离；悬浮窗和回放按当前字号保留方向文字与阴影的边距。
- **数据源**：`MasterPlayerPlugin.Instance.SafeZone.Manager.BuildSnapshot`（边界描边、障碍与危险区按危险配色、目标区按安全配色；可选随 `DangerWindow.TelegraphProgressAt` 加深填充）及 `AoeVisibilityRegistry` 的仅供显示预览。危险区描边带辉光双层描边，`RadarPulseDanger` 开启且预告进度 ≥ 0.7 时描边脉冲；解算路径来自 `MasterPlayerPlugin.Instance.SafeZone.MovementStatus.LatestSolution.ExecutablePath`，画成向终点流动的虚线 + 终点菱形扩散圈；队友来自 `PartyHelper.GetParty()`，敌人来自对象表（带深色描边，当前目标有高亮环），玩家自身为双层箭头 + 光晕。
- **显示配置**：`AoeViewSettings` 为真实世界／雷达独立保存危险和安全区的填充／边框 RGBA、形状过滤及类别开关。`AoeVisibilityRegistry` 关联区域句柄与 `action/{id}` 或 `script/{guid}/{group}` 来源键；未知来源归 `other`。所有显示过滤只发生在两种视图，不能删除解算区域。预览由 `Draw.AddAoe` 登记，同 id 覆盖，按时间／`Draw.Remove`／脚本停止清理；辅助 `Draw.Add/AddBound/AddTether` 保持直接世界绘制语义。
- **窗口外观与交互**：`RadarOverlayLock`（锁定 + 鼠标穿透）、`RadarScale`（0.3~3 悬浮窗大小；场地始终适配内容视口）、`RadarBackgroundOpacity`、`RadarShowCompass`、`RadarShowRangeRings`、`RadarPulseDanger`。解锁状态下整个视口即拖拽把手（InvisibleButton），滚轮直接调 `RadarScale`（0.8s 防抖后落盘），右键弹快捷菜单（锁定/不透明度/罗盘/距离环/脉冲/隐藏雷达）。窗口位置由 imgui.ini 按窗口名「MasterPlayer 雷达」持久化。

### 5.8 录制回放（`src\Replay\`）

机制事件的录制与时间轴回放，用于复现战斗、测试脚本运行状态。**PR 本体的 PromeEventBus/LogSystem 只订阅不发布（无公开 Raise），合成事件因此只在 MasterPlayer 脚本层派发**：录制走 `PromeEventBus.OnLogEvent` 全量订阅，回放经每脚本 `ReplayScriptEventRouter` 直喂，不进总线，DodgeController/Follow/其他插件不响应回放事件。

- **录制文件**（`.mpreplay.jsonl`，JSON Lines）：新文件header为v2，仍读v1；事件行使用t/kind/type/data，CastStart额外保存packetFacing，WorldFrame行保存初始完整快照与后续增量。Raw对象图不序列化，读回CastStart仅重建ReplayCastData标记；缺朝向的旧事件也不会回退实时对象。v2要求零时刻完整快照、拒绝损坏JSON及对象数据，未知事件类型跳过计数。格式和字段见 `docs/录制回放格式.md`。
- **ReplayRecorder**：手动（面板按钮或 `/prmp rec`）或 `AutoRecordOnBattleStart` 自动（战斗开始录、结束停）。目录 `ConfigDirectory\MasterPlayer\Replays\`，文件名 `rec_yyyyMMdd_HHmmss_t{territory}_{区域名}.mpreplay.jsonl`（区域名取 Lumina TerritoryType：优先副本名 ContentFinderCondition、回退 PlaceName，非法文件名字符替换为 `_`，取不到则省略）。`IsRecording` / `CurrentFilePath` / `EventCount` / `FilteredCount` / `Elapsed` / `StatusMessage` 供面板轮询；录制与回放互斥。**来源过滤**：`ReplayRecordFilter.ShouldRecord(ev, isPlayerOwnedSource)` 跳过来源为玩家（ObjectKind.Pc）或玩家召唤物（OwnerId 属于 PC，录制器侧 0.5s 对象表缓存）的动作/状态/读条（CastStart/CancelCast/InterruptCast/ActionEffect/StatusAdd/StatusRemove），且聊天日志（ChatLog）一律不录（NpcYell/InstanceContentText 战斗台词不属于聊天，保留）；SourceId 为 0 或无法判定一律放行，连线/点名/场地/战斗等事件不过滤。
- **ReplayWorldCapture**：每0.1秒、保留事件前和停止时采样当前对象表，包含玩家／宠物状态（事件过滤不影响对象快照）；队伍排序和本人时间轴职能分开记录。EAnim从插件初始化起缓存已观察到的历史，插件加载前未知；切区或对象移除清理。
- **ReplayController**：`Load(path)` → `Play()/Pause()/SetSpeed(正有限值)/Seek(秒)/StopSession()`（UI提供0.25~4倍）；先于普通脚本Update运行。以录制区域启动脚本，所有router共享独立ReplayWorld。Playback按t和文件行序应用帧／派发事件，推进录制时钟和脚本更新；暂停冻结，Seek从开头重建世界与脚本，末尾暂停保留最终场景。Stop撤销时钟／实体位置覆盖后恢复实时；刷新脚本先完整退出回放，普通Update再按实际区域启动。构造注入 `MechanicHostController?`（可为空）：StartSession 时 `EnterReplaySession(录制区域)`、Seek 的 RestartSessionScripts 时 `RestartReplaySession()`、StopSession 时 `ExitReplaySession()`；AdvanceTo 的 dispatch 回调先 `OnReplayEvent` 再派发给脚本路由器，update 回调先 `OnReplayTick(World)` 再 `UpdateReplayScripts`。
- **ReplayScriptEventRouter**：强类型 LogSystem 订阅按 `registeredType.IsAssignableFrom(ev.GetType())` 派发（`LogEvent` 兜底/泛型天然覆盖）；`LogSystemBattleStarted/Ended/CountdownStarted/WeatherChanged/Territory/ChatLog` 映射补发同名简单事件（含负载：WeatherId/(ushort)TerritoryId/Sender+Message+LogKind）。EventManager/LogMessageManager 独有的简单事件（PlayerDied/PlayerRevived/PlayerMoveStateChanged/SystemLog/BattleLog/ShowMessage）无 LogSystem 对应记录，回放不触发。逐订阅者异常隔离（对齐 LogSystemHub），异常计入 `ErrorCount`/`LastError`。
- **脚本接入边界**：首批地城和可视化脚本通过CurrentTime／ReplayWorld读取录制状态；雷达、SafeZone相对时间和实体绑定、AddAoe预览使用录制时钟／对象，缺实体绝不回退实时对象。旧v1文件无法补回对象与朝向；外部脚本直读Svc.Objects／DateTime.UtcNow或使用PR原始Draw接口仍需单独改造。斗兽棋机制本轮不改。
- **机制模块桥回放接入（M7）**：回放会话激活时控制器暂停实时采样，`MechanicHostController` 持有独立回放 `MechanicSession`（区域=录制区域），ReplayFeedAdapter 把 ReplayWorldState 全量帧与瞬态事件转成 Feed DTO（CastStart/状态/对象增删等帧覆盖类事件返回 null 防双喂，ActionEffect 摘要按 GlobalSequence 合并为降级包，EAnim 变化补发 EObjAnimation）；回放中移动注册保持禁用（导航桥判 ReplayModeActive 不注册），绘制捕获与时间轴照常（ReplayWindow 时间轴页顶部显示机制预测条）；回放区域有运行中的 MP 区域脚本时不激活模块（与实时压制一致）。
- **ReplayTimeline / ReplayComparison**（纯逻辑，tests 可链接）：`ReplayTimeline.Build(ReplayRecording)` 产出读条区间（同 SourceId 的取消/打断事件截断横条并标记）、泳道分类打点（Cast/Effect/Tether/Status/Mark/Arena/Battle/Misc）与实体名映射（ObjectCreate/ActionEffect）；`ReplayComparison.Compare(IReadOnlyList<ComparisonLog>)` 按读条 ActionId 聚合每日志次数、对齐后首用时间与首用最大差，`FirstCastTime`/`FirstBattleTime` 供对齐锚点。名称解析（Lumina）只在 UI 层做。
- **ReplayWindow**（独立窗口，契约见 §3.1）：时间轴回放页签上内嵌 `RadarView`（回放中实时呈现脚本注册的机制范围）下时间轴（点击 Seek 调 `ReplayController.Seek`）；日志对比页签支持按战斗开始/首次读条对齐与手动偏移，归纳共同/差异技能。
- 回归：`tests/ReplayRecordingRegression.cs`、`ReplayTimelineRegression.cs`、`ReplayWorldRegression.cs`覆盖数据、事件、时间顺序、快照、时钟、绑定与旧文件边界；`tests/ScriptCompilation/ScriptCatalogRegression.cs`加载实际DLL验证暂停／速度／跳转／区域过期／实时隔离／刷新及退出清理。

## 6. 铁律（违反任何一条都等于 bug）

`src/UI/Windows/PluginUi.Toggle` 不得直接绑定 `PromeUi.ToggleRow`，PR 运行版本可能有 4/5/6 参数签名。缓存检测前四参数与可选尾参数，调用后回写 `ref bool`；无兼容方法时回退 ImGui Checkbox。更新上游须保留这项本地兼容修复，并运行 `tests/UiCompatibility` 检查实际 DLL 无直接 ToggleRow 引用。

第三关使用用户手工路网（BlundervilleFinalRouteSettings.Graph）：节点可分叉、汇合及显式回头，从所有已连通后继择路，验证通过的后继可预选并连续执行，不要求每到分叉停下重算；不是唯一折线或整关预设。未画的边不得生成，相交线不自动连接，行走中不可跳分支。台阶入口可多个、自定X，按实测平台校验后执行该入口横向位置的四阶动作，不锁±4。旧Sections按原几何迁移成边。后台时序搜索保留边界、危险及执行复核；保存替换快照。验证 --filter="Blunderville graph"；实机待验收。详见docs/reference/Blunderville.spec.md。

1. **主线程纪律**：SafeZone 注册/快照（`SafeZoneRegistry` 全部方法、`SafeZoneManager.BuildSnapshot`）与 `DrawManager` 全部操作**只能在游戏主线程**（`Svc.Framework.Update` 回调或 `UiBuilder.Draw`）执行。事件处理首选 **`PromeEventBus`（主线程派发）**，回调里可直接注册危险区/绘制——`DodgeController` 已迁移至此模式（订阅 `OnStartCast`，回调直注册，无队列）。`CombatEventManager` 裸事件由 hook/网络线程触发：回调里**只做入队**（`ConcurrentQueue` 之类），在自己的 `Update()`（主线程）里统一出队处理，严禁在回调里直接碰 SafeZone/DrawManager。
2. **Dispose / 热重载纪律**：PR 插件管理器可随时卸载重载本插件。`Dispose()` 必须做到：
   - 退订全部事件源（`Svc.Framework.Update -=`、`UiBuilder.Draw -=`、PromeEventBus 订阅逐个 `Dispose()` 或 `UnsubscribeAll(owner)`、CombatEventManager 静态事件 `-=`）；
   - 清绘制：`PromeRotation.Plugin.Instance.DrawManager.RemoveByRegex("^MasterPlayer_")`；
   - 停止**自发起**的 GreenMove（自己接管过才 `Stop()`，见铁律 3）；
   - **每个环节独立 try/catch**（参照 PromePainter 的 `SafeDispose` 模式），一个环节抛异常绝不能跳过后续清理；Dispose 幂等（`_disposed` 标记）。
3. **移动指令纪律**：`GreenMoveSystem.MoveTo` 是 **last-write-wins**（替换当前路线），**无优先级**概念——后发的路线直接顶掉先发的，谁的都可能被顶。因此：
   - 不要每帧重发相同路线（每发一次都重置路线状态与读条看门狗）；只在路线**变化**时 `MoveTo`，到达/失效时 `Stop()`；
   - **只有自己接管过移动才调用 `Stop()`**，绝不停掉本体或其他插件发起的移动；
   - 脚本手动移动在插件或 PR 旧版/Reborn 开启自动移动或持有控制权时让位；自动躲避开启时，跟随只注册本地动态目标，机制目标优先，禁止释放控制后直接回位；直接跟随同样让位于任一安全区控制权。`Stop` / `ClearQueue` / `SkipCurrent` 不能修改被接管的路线。回放期间三条移动入口均禁用。
4. **绘制 id 统一 `MasterPlayer_` 前缀**：所有 `DrawManager.Add*` 的 id 必须以 `MasterPlayer_` 开头。Dispose 用 `RemoveByRegex("^MasterPlayer_")` 一键清干净，也避免与其他插件/脚本的绘制 id 冲突。
5. **设置持久化走自己的 `MasterPlayer.json`**：不用 Dalamud 的 `GetPluginConfig`（那是 PR 宿主自己的配置通道，卫星插件调用会拿到宿主的配置）。本插件设置一律经 `MasterPlayerSettings`（`System.Text.Json`）读写 `ConfigDirectory\MasterPlayer.json`。
6. **脚本回调与 OnUpdate 均在主线程执行**（PromeEventBus 事件由主线程派发），脚本内可直接调 SafeZone / Move / Draw 三门面，无需自行入队；脚本不得再订阅 `CombatEventManager` 裸事件（那是 hook/网络线程，绕过了 owner 管理）。
7. **脚本不得持静态状态跨热重载**：脚本重启创建新实例，内置脚本刷新复用当前程序集，静态状态不会重置；外部脚本旧程序集也可能残留（重复订阅、状态错乱）。状态一律放实例字段，`OnStop` 做收尾。

---

历史修复与验证记录已移至 [docs/开发记录.md](docs/开发记录.md)。
