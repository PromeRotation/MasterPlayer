# MasterPlayer — 项目总文档（AGENTS.md）

面向后续 AI 代理与开发者的项目权威文档。动手写代码前先读完本文；涉及 PromeRotation、Dalamud（卫月）或 FFXIVClientStructs（FFCS）的 API 签名与用法，**一律查阅对应仓库源码**（路径见下）。

## 1. 项目定位

MasterPlayer 是 **PromeRotation（PR）的卫星插件**，不是独立 Dalamud 插件：

- **不实现 `IDalamudPlugin`**，不被 Dalamud 直接加载；实现的是 PR 的 `IPromePlugin` 契约，由 PR 插件管理器（`PromePluginManager`）在游戏内反射加载、启用、热重载。
- 功能组件：**雷达悬浮窗（RadarOverlayWindow，BMR 式 2D 俯视小地图）+ 真实世界绘制（RadarRenderer，3D 世界范围提示）+ 智能躲避（Dodge）+ 战斗中跟随（Follow）+ 智能机制处理（Mechanics）+ 机制脚本框架（Scripting）+ 录制回放（Replay，BMR 式事件录制与时间轴回放，用于测试脚本）**。术语约定：「雷达」专指雷达悬浮窗；游戏内 3D 世界范围提示叫「真实世界绘制」。
- 定位类似 BossModReborn，但一切能力基于 **PromeRotation 本体 API** 实现：安全区解算（SafeZone）、GreenMove 移动、DrawManager 世界绘制、事件总线。本插件只做机制判断与编排，不重复造解算/移动/绘制轮子。
- Dalamud 服务不经构造函数注入（卫星插件没有 `[PluginService]`），统一走 ECommons 静态门面 `Svc`（`ECommons.DalamudServices.Svc`：`Svc.Framework.Update`、`Svc.PluginInterface.UiBuilder.Draw`、`Svc.Log`、`Svc.Objects` 等）。

## 2. 参考仓库

| 仓库 | 路径 | 用途 |
|---|---|---|
| PromePainter | `C:\Users\xiaos\Documents\GitHub\PromePainter` | 插件模板：同为 PR 卫星插件，入口/csproj/设置持久化/Dispose 模式照抄它；脚本系统（Roslyn 编译 + PromeEventBus owner 作用域 + 描述符注册表）也照抄它 |
| PromeRotation 本体源码 | `C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork`（本体项目在 `PromeRotation\` 子目录） | PromeRotation API 签名的唯一权威来源 |
| Dalamud（卫月）主仓库 | `C:\Users\xiaos\Documents\GitHub\Dalamud` | 查询卫月本体的服务接口、事件、插件接口及 API 用法，以接口定义、实现和源码注释为准 |
| FFXIVClientStructs（FFCS） | `C:\Users\xiaos\Documents\GitHub\FFXIVClientStructs` | 查询游戏原生结构、字段布局、指针及成员函数等互操作 API 的定义与用法 |
| BossModReborn（BMR） | `C:\Users\xiaos\Documents\GitHub\BossModReborn-7.56` | 机制迁移与运行时推断参考：通用 AOE 形状推断（`BossMod/BossModule/AIHintsBuilder.cs` 的 CastType→形状映射、Omen 路径 fanXXX 开角/月环内径解析、VFX 25 视线过滤）、施法者敌我判定（`WorldStateGameSync` 的 `ActionManager.ClassifyTarget != Enemy` 即盟友）、斗兽棋模块（`BossMod/Modules/Dawntrail/Foray/Crucible/`，CrucibleHigh1 脚本原型） |

查询 API 时按归属查阅对应仓库：PR 封装查 PromeRotation，卫月服务与接口查 Dalamud，游戏原生结构与函数查 FFCS。核对具体签名、类型和调用约束，不凭记忆臆造成员；同时确认源码版本与当前编译引用及运行环境匹配。

> **警告：编译必须引用用户实际运行的那份 PromeRotation.dll**（csproj 经 `PromeRotationRoot` 指向本体仓库的 `bin\Debug`）。PR 本体更新后必须重新编译本插件，否则运行时出现 `MissingMethodException` / 类型身份不一致。分发时目标用户的 PR 版本必须与编译引用版本一致。

## 3. 目录结构与共享契约

```
MasterPlayer/
├── MasterPlayerPlugin.cs      # 插件入口（IPromePlugin + [PromePlugin]）
├── MasterPlayer.csproj        # 构建配置（引用 PR 本体 bin，产物直出到 PR 插件目录）
├── AGENTS.md                  # 本文档
├── Settings/
│   ├── MasterPlayerSettings.cs    # 全局设置单例（MasterPlayer.json，System.Text.Json）
│   └── SafeZoneActivation.cs      # 功能开关到 PR 安全区/自动移动开关的生命周期联动
├── Radar/
│   ├── RadarOverlayWindow.cs      # 雷达悬浮窗：窗口壳 + 显示门槛 + 交互（内容渲染委托 RadarView）
│   ├── RadarView.cs               # 可嵌入雷达视图：给定屏幕圆心/半径绘制全套雷达内容（悬浮窗与回放窗口共用）
│   └── RadarRenderer.cs           # 真实世界绘制：DrawManager 3D 世界范围提示（AOE 预告/安全点等）
├── Follow/
│   └── FollowController.cs        # 战斗中跟随：跟随队友/当前目标（自动躲避开启时作为 Reborn 动态目标，与危险区统一解算）
├── Dodge/
│   ├── DodgeController.cs         # 智能躲避：读条事件（PromeEventBus 主线程派发）→ 危险区注册 → SafeZone 解算走位
│   ├── DangerMarginInflator.cs    # 危险区几何外扩（GeometryTemplate 按 DodgeDangerMargin 膨胀）
│   └── AoeShapeDatabase.cs        # ActionId → AOE 形状查表（静态表 + Lumina CastType 推断，映射对齐 BMR）
├── Mechanics/
│   ├── MechanicDispatcher.cs      # 机制事件分发器：订阅事件源 → 入队 → 主线程派发给 Handler
│   └── IMechanicHandler.cs        # 机制处理器接口（每个具体机制一个实现类）
├── Scripting/                     # 机制脚本框架：Roslyn 热编译 + 描述符注册表 + 每脚本 owner 上下文
│                                  # （ScriptManager / MasterPlayerScript / IScriptHostContext / SafeZone·Move·Draw 三门面等，契约见 §3.1；
│                                  #   事件订阅经 IScriptEventRouter 抽象：LiveScriptEventRouter 实时转发 PromeEventBus，回放走 ReplayScriptEventRouter）
├── Replay/
│   ├── ReplayRecording.cs         # 录制文件模型 + JSON Lines 读写（.mpreplay.jsonl；纯逻辑不依赖游戏服务，tests 可链接）
│   ├── ReplayScriptEventRouter.cs # 回放事件路由：脚本订阅落本地注册表，Raise 按类型派发 + 简单事件映射补发
│   ├── ReplayRecorder.cs          # 录制器：PromeEventBus.OnLogEvent 全量订阅写文件；手动或战斗自动开停
│   ├── ReplayRecordFilter.cs      # 录制过滤：跳过玩家（PC）/玩家召唤物来源的动作·状态·读条（纯逻辑）
│   ├── ReplayController.cs        # 回放控制器：回放会话编排 + 主线程时钟派发（播放/暂停/变速/Seek/停止）
│   ├── ReplayTimeline.cs          # 时间轴模型：读条区间（取消/打断截断）+ 泳道分类打点 + 实体名映射（纯逻辑）
│   └── ReplayComparison.cs        # 多日志对比：按读条 ActionId 聚合次数/首用时间/日志间差异（纯逻辑）
├── Examples/
│   ├── ExampleGenericScript.cs    # 通用机制示例脚本（编译进插件 DLL 做冒烟检查）
│   └── ExampleZoneScript.cs       # 区域限定示例脚本
├── Scripts/
│   └── CrucibleHigh1.cs           # 斗兽棋·高一机制脚本（迁移自 BossModReborn；编译进 DLL 冒烟，亦可复制到脚本目录热加载）
├── docs/
│   ├── 脚本开发指南.md            # 面向脚本开发者的中文指南
│   ├── BMR机制迁移指南.md         # BMR→MasterPlayer 机制迁移的语义对照/流程/踩坑（配合 .agents/skills/bmr-mechanic-migration 技能）
│   └── reference/
│       └── CrucibleHigh1.spec.md  # 斗兽棋·高一迁移规格（该脚本内容的唯一事实来源）
└── Windows/
    ├── MainWindow.cs              # ImGui 主面板（总开关、脚本库、模块状态、录制回放入口、调试信息）
    └── ReplayWindow.cs            # 录制回放独立窗口（上内嵌雷达 + 下事件时间轴 / 多日志对比）
```

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
    internal Mechanics.MechanicDispatcher? Mechanics { get; }   // 仅供 MainWindow 读取机制处理器列表
    internal Scripting.ScriptManager? Scripts { get; }          // 仅供 MainWindow 读取脚本管理器
    public void Initialize();
    public void DrawConfigUI();
    public void Dispose();
    private void OnFrameworkUpdate(IFramework framework);
}
```

- `Initialize()`：设 `Instance` → 触发 `Settings.MasterPlayerSettings.Instance` 加载 → 构造 `SafeZoneActivation` 并应用持久化的功能开关 → 依次构造 `_radar = new Radar.RadarRenderer()`、`_dodge = new Dodge.DodgeController()`、`_mechanics = new Mechanics.MechanicDispatcher(this)`、`_scripts = new Scripting.ScriptManager()`（随即 `RefreshCatalog()`）、`_replayRecorder = new Replay.ReplayRecorder()`、`_replayController = new Replay.ReplayController(_scripts, _replayRecorder)`、`_replayWindow = new Windows.ReplayWindow()`、`_radarOverlay = new Radar.RadarOverlayWindow()`、`_follow = new Follow.FollowController()`、`_mainWindow = new Windows.MainWindow()` → `Svc.Framework.Update += OnFrameworkUpdate` → `Svc.PluginInterface.UiBuilder.Draw += _mainWindow.Draw` → `+= _radarOverlay.Draw` → `+= _replayWindow.Draw` → 注册聊天指令 `/prmp`（`Svc.Commands.AddHandler`，无参开关设置面板，`/prmp rec` 或 `/prmp record` 开始/停止录制，`/prmp replay` 开关录制回放窗口）。
- `OnFrameworkUpdate(IFramework framework)`：先取 `deltaTime`（秒，`IFramework.UpdateDelta.TotalSeconds`），由 `SafeZoneActivation.Update` 应用功能开关变化，再执行 `_radar.Update(); _dodge.Update(); _mechanics.Update(); _replayController.Update(deltaTime);`（回放时钟派发，必须在脚本 Update 之前）`_scripts.Update(deltaTime); _follow.Update();`。**EnableScripts 总开关门控在 `ScriptManager.Update` 内部**（插件无条件调用；关闭时 ScriptManager 先 `StopAll` 再提前返回）。另有 internal 只读属性 `Recorder` / `Replay` / `ReplayWindow` 供 MainWindow/ReplayWindow 访问录制回放组件。
- `DrawConfigUI()`：ImGui 按钮 `"打开 MasterPlayer 设置面板"` 调 `_mainWindow.Toggle()`，旁边提示 `/prmp` 指令。
- `Dispose()`：退订上述事件（Framework.Update、UiBuilder.Draw 三处）→ `Svc.Commands.RemoveHandler("/prmp")`（独立 try/catch）→ `SafeDispose` 十一个组件，顺序 `_replayWindow` → `_mainWindow` → `_follow` → `_radarOverlay` → `_replayController`（其 Dispose 先停回放会话）→ `_replayRecorder`（其 Dispose 先停录制）→ `_scripts`（其 Dispose 内部先 `StopAll`）→ `_mechanics` → `_dodge` → `_radar` → `_safeZoneActivation` → `Instance = null`。
- `SafeZoneActivation`：注入宿主 `SafeZoneRebornSetting`。雷达悬浮窗、真实世界绘制、智能躲避任一开启时启用其 `EnableSafeZone`（关闭时快照会隐藏危险区）；仅智能躲避请求其 `EnableAutoMovement`。功能关闭/卸载时只撤销本插件从 false 开启的 Reborn 开关；运行中手动关闭不会被每帧覆写，可在诊断页恢复。功能开关联动不修改旧版开关。

**Settings.MasterPlayerSettings**（`Settings\MasterPlayerSettings.cs`）

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

    // 录制回放（Replay）
    public bool AutoRecordOnBattleStart = false;    // 战斗开始自动录制、战斗结束自动停止（仅自动开启的录制被自动停止）
    public void Save();
}
```

- 存储路径 `Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer.json")`（`ConfigDirectory` 是 `DirectoryInfo`，拼路径取 `.FullName`，同 PromePainter 的 `PainterSettings`），序列化用 `System.Text.Json`。

**Radar.RadarRenderer**（`Radar\RadarRenderer.cs`）——真实世界绘制（3D 范围提示）

```csharp
public sealed class RadarRenderer : IDisposable
{
    public RadarRenderer();
    public void Update();
    public void Dispose();
}
```

**Radar.RadarOverlayWindow**（`Radar\RadarOverlayWindow.cs`）——雷达悬浮窗

```csharp
public sealed class RadarOverlayWindow : IDisposable
{
    public RadarOverlayWindow();
    public void Draw();      // 挂 Svc.PluginInterface.UiBuilder.Draw（主线程），不再经 Framework.Update
    public void Dispose();
}
```

- 窗口壳只管显示门槛（EnableRadarOverlay/RadarOnlyWithScript）、拖拽/滚轮/右键菜单交互与防抖保存；内容渲染委托 `RadarView`（悬浮窗与回放窗口共用同一组件）。

**Radar.RadarView**（`Radar\RadarView.cs`）——可嵌入雷达视图

```csharp
public sealed class RadarView
{
    public RadarView();
    public bool Draw(ImDrawListPtr drawList, Vector2 screenCenter, float radiusPx, bool hovered = false);
    // 返回 false = 未绘制（无场地边界且玩家不存在）。内部完成快照/中心/旋转/缩放计算与全套图层。
}
```

**Windows.ReplayWindow**（`Windows\ReplayWindow.cs`）——录制回放独立窗口

```csharp
public sealed class ReplayWindow : IDisposable
{
    public void Draw();      // 挂 Svc.PluginInterface.UiBuilder.Draw（主线程）
    public void Toggle();
    public void Dispose();
}
```

- 「MasterPlayer 录制回放」窗口（`/prmp replay` 或主面板「录制回放」页按钮开关）：顶部录制/回放工具栏 + 录制文件列表（「回放」加载播放 + 对比勾选）；「时间轴回放」页签 = 上内嵌 RadarView + 下事件时间轴（泳道分类、滚轮缩放、左键 Seek、悬停详情、播放头跟随回放时钟、泳道显隐）；「日志对比」页签 = 对齐按钮（战斗开始/首次读条/清零）+ 每日志偏移调节 + 技能汇总表（出现 x/N、每日志次数与首用、首用最大差）+ 叠加读条泳道。技能名经 Lumina Action 表解析并缓存（仅 UI 层）。


**Follow.FollowController**（`Follow\FollowController.cs`）

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
- 自动躲避模式只撤销自己的目标句柄，不直接操作 GreenMove；本体 Reborn 被手动关闭时不会降级成直线跟随绕过危险检查。直接跟随的移动纪律（铁律 3）：只有 `_hasControl`（自己下发过 MoveTo）才 `Stop()`；旧版 `SafeZoneMovementStatus.HasControl` 或 `SafeZoneRebornMovementStatus.HasControl` 为 true 时让位——不发跟随指令，也不 Stop 对方路线；关闭或卸载跟随时同样检查控制权。
- 通用避险优先级：玩家读条、脚下危险扣除宿主开始余量后不足 1 秒、或处于硬障碍内时，撤销普通跟随目标，保持危险解算运行。普通目标保留宿主 0.5 米硬余量，危险结束后的 0.3 秒余量也纳入排除；按原目标覆盖范围统计安全占比，小于 25% 时停用其 GoalWindow，不能只给空几何（PR 的目标启发式仍会朝空区域中心移动）。仅目标启停递增一次版本，使旧追击解算失效；目标/危险几何正常动态更新仍不递增。精确机制 GoalPoints 不受此普通偏好过滤。

**Dodge.DodgeController**（`Dodge\DodgeController.cs`）

```csharp
public sealed class DodgeController : IDisposable
{
    public DodgeController();
    public void Update();
    public void Dispose();
}
```

- 事件源为 `PromeEventBus.OnStartCast`（主线程派发，无队列，回调里直接注册危险区）；雷达悬浮窗、真实世界绘制、智能躲避任一开启即识别读条，避免只开启绘制时没有危险区数据。注册前模板经 `DangerMarginInflator.Inflate` 按 `DodgeDangerMargin` 外扩；`Update()` 只做失效句柄簿记清理。
- **施法者敌我过滤**（对齐 BMR `AIHintsBuilder.OnCastStarted`）：只响应 `ObjectKind.BattleNpc` 且游戏原生 `ActionManager.ClassifyTarget(Character*) == TargetCategory.Enemy` 的读条——玩家/宠物/坐骑/事件 NPC/友方战斗 NPC（剧情、Trust 等同盟单位）一律跳过；对象表无法解析的施法者（多为场外辅助体）放行，避免丢预告。命中时把施法者 `HitboxRadius` 传给形状推断。
- **脚本压制去重**：`ScriptManager.IsGenericDodgeSuppressed(actionId)` 为 true（运行中脚本已用 `SuppressGenericDodge` 声明接管该技能）时跳过，避免通用躲避与脚本重复注册同一危险区。
- **错峰判定转场**：由 PR 本体 `PromeRotation.Spatial.SafeZoneReborn.Solver.SafeZoneSolver` 的安全时间区间搜索处理，允许先在后判定区提前站位、等待，再进入已结束判定的区域；只向 GreenMove 下发首次等待前的路段，周期重解接续。旧版 `Spatial/SafeZone/` 实现保持不变。依赖重新编译并加载的本体 DLL，不能只热重载 MasterPlayer；回归入口为 `dotnet run --project tests/MasterPlayer.RegressionTests.csproj`。

高一 B31 四连放毒与预备点使用 R0.2、Center 目标圈；依赖 PR Reborn 小目标网格细化及按目标内部余量收紧移动容差。更新必须同时重编并加载 PR 本体与 MasterPlayer，仅刷新脚本无法获得 0.2y 到点精度。

持续危险区与硬障碍覆盖玩家时，Reborn 必须立即逃离、不等读条；硬障碍仅对起点包含玩家的区域允许退出，边界和其他硬障碍仍阻挡。每个持续区域独立禁止退出后重入，逃离路径保留搜索原始线段，并按网格分辨率收紧到达容差，避免停在区域内；无出口返回 `NoReachableSafeCell`。此逻辑在 PR 本体，回归同样走 `tests/MasterPlayer.RegressionTests.csproj`，更新后须同时重编并加载本体与 MasterPlayer。

**Dodge.DangerMarginInflator**（`Dodge\DangerMarginInflator.cs`）

```csharp
public static class DangerMarginInflator
{
    public static GeometryTemplate Inflate(GeometryTemplate template, float margin);
}
```

- `margin <= 0` 原样返回；Circle 半径 +margin；Donut 内径 -margin（下限 0）/外径 +margin；Fan 半径 +margin；Rectangle 宽 +2×margin / 长 +margin；RayRectangle 两侧与长各 +margin；多边形模板与未知子类原样返回。

**Dodge AOE 形状数据库**（`Dodge\AoeShapeDatabase.cs`）

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

- 静态表查不到时走 **Lumina Action 表推断**，映射逐条对齐 BMR `AIHintsBuilder.GuessShape`：2=落点/自身圆（不加 hitbox，`TargetArea`=落点型）；3=扇形（半径+hitbox，总开角取 Omen 路径 `fanXXX` 度数，缺省 180°）；4=直线矩形（长+hitbox，`XAxisModifier`=全宽）；5=自身圆（半径+hitbox）；8=冲锋矩形；10=月环（内径取 Omen 路径 `sircle_`/`sicle_`/`circle` 后两位数字，缺省 3）；12=矩形、13=扇形（均不加 hitbox）。跳过：1=单体、6/7、11=十字（无对应模板）、2/5 且 `EffectRange ≥ 30`（全屏）、`VFX.RowId == 25`（视线类）。

**Mechanics.MechanicDispatcher**（`Mechanics\MechanicDispatcher.cs`）

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

**Mechanics.IMechanicHandler**（`Mechanics\IMechanicHandler.cs`）

```csharp
public interface IMechanicHandler : IDisposable
{
    string Id { get; }
    string DisplayName { get; }
    bool Enabled { get; set; }
    void Attach(MechanicDispatcher dispatcher);
}
```

**Windows.MainWindow**（`Windows\MainWindow.cs`）

```csharp
public sealed class MainWindow : IDisposable
{
    public void Draw();
    public void Toggle();
    public void Dispose();
}
```

- 面板为分页签布局：总览（五个功能模块开关 + 状态/警告）、雷达悬浮窗、绘制外观（真实世界/雷达独立配色 + 按技能来源的显示表格）、自动化（跟随 + 智能躲避余量）、脚本（目录管理与脚本详情）、录制回放（精简入口：录制/回放状态行 + 打开独立窗口按钮 + 自动录制开关）、诊断（调试模式、本体状态、机制处理器）。聊天指令 `/prmp` 开关面板（注册于 `MasterPlayerPlugin`，PR 已占用 `/pr`，勿冲突），`/prmp rec` 开停录制，`/prmp replay` 开关录制回放窗口。

**Scripting 脚本框架**（`Scripting\*.cs`，命名空间统一 `MasterPlayer.Scripting`，含 `ScriptSettingStore`）

```csharp
public sealed record ScriptMetadata(string Id, string Name, uint[] Territorys, string Version, string Author, string Note, string Region, string SourcePath);

public enum ScriptSource { LocalSource, LocalDll, Visual }

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
    public ScriptSafeZoneApi SafeZone { get; }      // => Host.SafeZone
    public ScriptMoveApi Move { get; }              // => Host.Move
    public ScriptDrawApi Draw { get; }              // => Host.Draw
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
    ScriptSafeZoneApi SafeZone { get; }
    ScriptMoveApi Move { get; }
    ScriptDrawApi Draw { get; }
    ScriptSettingStore Settings { get; }
    bool IsMechanismEnabled(string mechanism);
    void SuppressGenericDodge(params uint[] actionIds);
    void Log(string message);
    void LogError(string message);
    IDisposable Subscribe(string eventName, Delegate handler);
    // + 全套强类型事件方法（照抄 PromePainter IScriptHostContext，35 个；含泛型 OnLogEvent<TEvent>；无 OnBalloonTalk / OnBattleTalk2，本体总线未提供）
}

public sealed class ScriptSafeZoneApi { /* internal 构造；AddDelayedHit/AddTimedDanger/AddPersistentDanger/AddPersistentSafeArea/AddTimedSafeArea/AddObstacle/Remove(SafeZoneAreaHandle)（签名镜像本体 SafeZoneRegistry，见 §5.2；**SafeArea 落点偏好默认 Nearest 贴边，与本体默认 Center 不同**）；Add*Danger 注册前按 DodgeDangerMargin 经 DangerMarginInflator 外扩（SafeArea/Obstacle 不外扩）；internal void Cleanup() */ }
public sealed class ScriptMoveApi { /* internal 构造；IsRunning/GetStatus()/MoveTo(GreenMovePoint)/MoveTo(IReadOnlyList<GreenMovePoint>)/Append(GreenMovePoint)/Stop()/ClearQueue()/SkipCurrent()；internal void Cleanup() */ }
public sealed class ScriptDrawApi { /* internal 构造；Add/AddBound/AddTether/Remove(string id)（签名镜像本体 DrawManager 的 id 重载，见 §5.2），id 自动加 "MasterPlayer_Script_{guid}_" 前缀；internal void Cleanup() */ }

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
    internal bool ReplayModeActive { get; }     // 回放会话门控：Update 跳过区域自动启停（回放脚本仍照常 UpdateInternal）
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
- **事件路由抽象**：`internal interface IScriptEventRouter : IDisposable { IDisposable Subscribe(object owner, string eventName, Delegate handler, Type? logEventType = null); }`（`Scripting\IScriptEventRouter.cs`）。上下文的 35 个订阅方法统一经 `_router.Subscribe(this, "<事件名>", handler, typeof(<事件record类型>))` 落地；事件名与 `PromeEventBus.Subscribe` 字符串名一致。`LiveScriptEventRouter`（`Scripting\LiveScriptEventRouter.cs`，单例，Dispose 为 no-op）：logEventType 命中内置类型表则调对应 `PromeEventBus.OnXxx` 强类型方法，否则反射走泛型 `OnLogEvent<TEvent>`，简单事件直接转发字符串名 API。脚本上下文 Dispose 时在 TryCleanup 链中顺带 Dispose 路由器（回放路由借此清空本地注册表）。
- `ScriptSettingStore` 公共面照抄 PromePainter：`RegisteredSettings` / `GetPropertyType` / `GetPropertyValue` / `SetPropertyValue` / `Get<T>` / `Set` / `GetBool` / `GetInt` / `GetFloat` / `GetString` / `Contains`（MainWindow 据此画设置控件，不依赖反射）。

## 4. 开发模式 / 构建部署

### 4.1 构建前置

1. **PromeRotation 本体已构建**：本体仓库（默认 `C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork\`）下已存在 `PromeRotation\bin\Debug\` 产物（`PromeRotation.dll`、`PromeRotation.Scripting.dll`、`ECommons.dll` 等）。csproj 直接引用这些 DLL。
2. **Dalamud CN 开发 DLL 就位**：`%AppData%\XIVLauncherCN\addon\Hooks\dev\` 下有 `Dalamud.dll`、`Dalamud.Bindings.ImGui.dll`、`FFXIVClientStructs.dll`、`Lumina.dll` 等（装了 XIVLauncherCN 开发者模式即有）。
3. 本体在其他路径时用命令行覆盖 `PromeRotationRoot` 属性（注意以 `\` 结尾）：
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

### 5.2 宿主访问：`PromeRotation.Plugin.Instance`（`Plugin.cs`）

MasterPlayer 的区域注册、脚本边界、快照、绘制与开关联动统一使用以下 Reborn 入口。旧版 `SafeZoneRegistry` / `SafeZoneManager` / `SafeZoneSetting` / `SafeZoneMovementStatus` 继续供旧脚本使用；两套各自持有区域、边界、配置与移动状态。注册与几何类型复用，但句柄必须交回原注册表，不能跨两套系统混用。Reborn 解算类型位于 `PromeRotation.Spatial.SafeZoneReborn.Solver`，移动状态接口位于 `PromeRotation.Spatial.SafeZoneReborn`。

两套共用 GreenMove；宿主按旧版 → Reborn → GreenMove 的顺序更新。旧版解算和自动移动都开启，或旧版仍持有控制权时，Reborn 让位且不停止旧版路线；MasterPlayer 面板显示原因，只有用户点击「关闭旧版自动移动」按钮时才关闭旧版自动移动，不自动修改旧版开关。

| 属性 | 类型 | 说明与关键成员 |
|---|---|---|
| `SafeZoneRebornRegistry` | `SafeZoneRegistry` | Reborn 安全区注册便利层（相对秒数自动转绝对 UTC；**所有方法仅主线程**）。危险区：`AddPersistentDanger` / `AddTimedDanger` / `AddDelayedHit`（单次命中，默认 `DefaultHitDurationSeconds = 0.6f`）/ `AddDelayedDanger` / `AddDangerSequence`；目标区：`AddPersistentSafeArea` / `AddTimedSafeArea` / `AddDelayedSafeArea` / `AddSafeAreaSequence`（含 `priority`、`PositionPreference`）；障碍/边界：`AddObstacle` / `SetArenaBoundary` / `ClearArenaBoundary`；清理：`Remove(SafeZoneAreaHandle)` / `ClearDangerAreas` / `ClearGoalAreas` / `ClearObstacles` / `ClearAll` |
| `SafeZoneRebornManager` | `SafeZoneManager` | Reborn 区域注册中心与快照发布者（底层，Registry 的委托对象）。`AddDangerArea` / `AddGoalArea` / `AddObstacle` / `UpdateArea` / `RemoveArea` / `ContainsArea` / `ClearAllAreas`；**`BuildSnapshot(DateTime utcNow)`** 构建解算快照（主线程） |
| `SafeZoneRebornSetting` | `SafeZoneSetting` | Reborn 运行时配置（仅内存无持久化）：`EnableSafeZone`（解算总开关）、**`EnableAutoMovement`（允许 Reborn 接管自动移动，旧版优先）**、`OnlySolveInRegisteredRegion`、`DebugArenaCenter` / `DebugArenaRadius` |
| `SafeZoneRebornMovementStatus` | `ISafeZoneRebornMovementStatus` | Reborn 自动移动只读状态（画配置/诊断 UI 用）：`LatestSolution`（Reborn `SafeZoneSolution?`）、`HasControl`、`PhaseText`、`DispatchCount`、`LastDispatchUtc`、`IsAsyncSolveRunning`、`AsyncSolveStartedCount/AcceptedCount/CancelledCount/DiscardedCount` |
| `GreenMoveSystem` | `IGreenMoveSystem` | 移动执行。`IsRunning`、`GetStatus()`、**`MoveTo(GreenMovePoint)` / `MoveTo(IReadOnlyList<GreenMovePoint>)`（替换当前路线，last-write-wins）**、`Append(...)`（追加到路线尾）、`Stop()`、`ClearQueue()`（清空后续点保留当前点）、`SkipCurrent()` |
| `DrawManager` | `DrawManager` | 世界绘制注册入口（主线程）。`Add(...)` / `AddBound(...)`（跟随实体）/ `AddTether(...)` 多重重载 → 返回 `DrawHandle`；`Remove(DrawHandle)` / `Remove(string id)`；**`RemoveByRegex(string pattern)` → int**；`Clear()` |

配套几何类型（`PromeRotation.Spatial.SafeZone`，`GeometryTemplate.cs` / `TransformBinding.cs`）：

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

例外：**仅 `PromeRotation.Scripting.dll` 需要随包拷贝到插件目录**（PR 加载器在该目录解析子插件依赖，本体版本一致无冲突），由 csproj 里 `AfterTargets="Build"` 的拷贝目标自动完成（照抄 PromePainter.csproj 的 `CopyPromeRotationScripting` target）。

**白名单之外的依赖不进包**（csproj 保持 `Private=false`）：PR 加载器只解析白名单到宿主，其余依赖 miss 后由 `Scripting/HostDependencyProbe.cs` 的 ALC `Resolving` 兜底——按「插件自身目录 → 宿主 PR 安装目录」顺序探测同名 DLL 并流式加载（宿主目录里的依赖版本天然与运行中本体一致）。该探针现在作为白名单方案的额外兜底保留。新增非白名单依赖无需改 csproj；仅当两处目录都没有该 DLL 时才会加载失败（报原始 `FileNotFoundException`）。

### 5.6 脚本系统（`Scripting\`，模式照抄 PromePainter `Scripting\`）

- **目录**：`ConfigDirectory\MasterPlayer\Scripts\`（即 `pluginConfigs\PromeRotation\MasterPlayer\Scripts\`；可用面板「打开脚本目录」直达）。递归扫描 `.cs`（Roslyn 编译）与 `.dll`（直接加载）；**严禁放 `Plugins\MasterPlayer\` 插件目录内**（会被 PR 当子插件程序集扫描）。
- **元数据**：`[MechanicScript(name, territorys, guid, version, author, note="")]`；`guid` 是身份主键（启停/设置/机制开关/去重都以它为准），同 guid 重复只保留一个。`territorys` 空数组 = 通用脚本。
- **区域匹配**：`Territorys.Length == 0 || Territorys.Contains(Svc.ClientState.TerritoryType)`；进入匹配区域自动启动已启用脚本，离开自动停止（照抄 PromePainter `ScriptManager.IsTerritoryMatched`）。
- **手动刷新**：脚本目录**不做文件监听**；面板「刷新脚本」调 `ScriptManager.ReloadScripts()`：停止全部运行中脚本（`OnStop` + 上下文 Dispose）→ `RefreshCatalog()` 重扫重编译 → 按启用状态重启。
- **owner 清理**：每个脚本上下文持独立 ownerId（`PromeOwnerRegistry.CreateScriptSessionOwnerId()`），事件订阅全部经 `PromeEventBus` 挂在该 owner 下；停止时 `PromeOwnerRegistry.MarkClosing(ownerId)` + `PromeEventBus.UnsubscribeAll(ownerId)` + 三门面各自 `Cleanup()`（SafeZone 移除本脚本全部区域、Move 若接管过则 `Stop()`、Draw `RemoveByRegex("^MasterPlayer_Script_{guid}_")`）。
- **异常隔离**：单脚本编译失败进 `ErrorMessage`（面板红字可滚动）；运行中 `OnUpdate`/事件回调异常只停止该脚本，不影响其他脚本与插件本体。
- **编译引用定位**：部分程序集被流式加载（`Assembly.Location` 为空，如本插件自身，可能还包括宿主体）。`ScriptCompiler` 的引用解析回退链为：程序集 `Location` → `ConfigDirectory\Plugins\<程序集名>\` 包目录 → 宿主 PR 安装目录（`Svc.PluginInterface.AssemblyLocation`）；首次构建引用集时会 Info 日志记录引用数量、探测目录与未能解析的程序集名——脚本编译报 CS0234 类错误时先看这条日志。
- **三门面纪律**：`ScriptSafeZoneApi` / `ScriptMoveApi` / `ScriptDrawApi` 只是 `PromeRotation.Plugin.Instance` 对应系统的转发 + 每脚本句柄簿记，全部主线程调用；绘制 id 由门面自动加 `MasterPlayer_Script_{guid}_` 前缀；Move 门面遵守铁律 3（`EnableAutoMovement` 让位、`MoveTo` last-write-wins、自己接管才 `Stop()`）。

### 5.6.1 可视化机制脚本

- 编辑入口为「机制脚本」→「可视化编辑器」，源码位于 `Scripting/Visual/` 与 `Windows/VisualScriptEditor.cs` / `VisualAoePreview.cs`；使用、UI 与扩展约定见 `docs/可视化脚本编辑器.md`。
- 递归加载 `*.mechanic.json`（新建默认存入脚本目录的 `Visual/`）；带格式版本的定义经描述符工厂创建 `VisualMechanicScript`，沿用区域匹配与独立 owner 生命周期。同区域、同施法者允许多个 GUID；保存只重载当前文档，不刷新其他脚本。
- 规则按 Action ID 与可选施法者 Data ID（Dalamud `BaseId`）筛选；绑定用事件的 Entity ID。可视化规则通过事件级 `IsGenericDodgeSuppressed` 条件接管，不能使用全局 Action ID 压制，否则会漏掉其他施法者的同技能。
- 运行器只使用安全区门面注册危险区，不操作 GreenMove 或场地边界；按事件时间扣除派发延迟，清理取消、打断、过期和停止的区域。位置与普通朝向跟随使用局部偏移，其他模式使用世界偏移；预览复用同一 PR 模板。
- 角度偏移 `AngleOffsetDegrees` 默认 0，在 `RotationOffsetTemplate.Resolve` 中叠加，不修改绑定朝向，避免同时转动局部位置偏移；`DangerMarginInflator` 递归外扩内层模板，保留角度与安全余量。正角度左转、负角度右转。

### 5.7 雷达悬浮窗（`Radar\RadarOverlayWindow.cs`）

随实际场地轮廓显示的 2D 俯视小地图，原生 ImGui 窗口（无标题栏、无边框、`NoBackground`），由 `UiBuilder.Draw`（主线程）逐帧驱动，不受 `EnableRadar`（真实世界绘制开关）影响，独立开关 `EnableRadarOverlay`（默认关）。

- **显示门槛**：`RadarOnlyWithScript`（默认开）开启时，仅当存在运行中的**区域专属脚本**（`Territorys` 非空且匹配当前区域；通用脚本不算，未启用/未运行不算）才显示——即「只在有脚本的区域出现」。
- **中心点两模式**：`RadarCenterMode == 0`（场地基准）且快照存在 `ArenaBoundary` 时优先读取当前斗兽入口与该导航边界对应的 `DisplayBounds` 和固定场心，否则用宿主边界。`RadarArenaLayout` 按镜头旋转后的轮廓最大轴向范围等比缩放到视口，保留4像素边框空间；不会用外接圆缩小长方形、填平异形凹口或给所有地图统一外扩。模式 1（玩家摄像机基准）或无边界时以玩家为中心、固定25码圆形视野。
- **旋转**：`RadarProjection` 直接调用 PR `GeometryHelper.WorldToLocal(world, center, rotation)`，再乘比例、加屏幕中心。局部 +X 向右、−Y 向前；跟随摄像机时 `rotation = CameraManager.Instance()->GetActiveCamera()->DirH + π`，与 PR 移动相机约定一致；固定北向上和相机不可用时用 `π`。玩家朝向用 PR `PointFromRotationDistanceXZ` 后走相同投影。`GetContours()` 已是世界坐标，不二次平移旋转。
- **场地裁剪（`Radar/RadarCanvas.cs`、`Radar/RadarClipRegion.cs`）**：背景和内容按真实轮廓与视口相交；凹多边形由PR三角化后逐片裁剪，线段合并连续区间但不跨越凹口。面积计算先平移到局部原点并忽略退化三角形，避免窗口位置造成精度误差。边框随场地，方向文字置于轮廓外侧，距离参考环仍表示实际距离；悬浮窗和回放按当前字号保留方向文字与阴影的边距。
- **数据源**：`SafeZoneRebornManager.BuildSnapshot`（边界描边、障碍与危险区按危险配色、目标区按安全配色；可选随 `DangerWindow.TelegraphProgressAt` 加深填充）及 `AoeVisibilityRegistry` 的仅供显示预览。危险区描边带辉光双层描边，`RadarPulseDanger` 开启且预告进度 ≥ 0.7 时描边脉冲；解算路径来自 `SafeZoneRebornMovementStatus.LatestSolution.ExecutablePath`，画成向终点流动的虚线 + 终点菱形扩散圈；队友来自 `PartyHelper.GetParty()`，敌人来自对象表（带深色描边，当前目标有高亮环），玩家自身为双层箭头 + 光晕。
- **显示配置**：`AoeViewSettings` 为真实世界／雷达独立保存危险和安全区的填充／边框 RGBA、形状过滤及类别开关。`AoeVisibilityRegistry` 关联区域句柄与 `action/{id}` 或 `script/{guid}/{group}` 来源键；未知来源归 `other`。所有显示过滤只发生在两种视图，不能删除解算区域。预览由 `Draw.AddAoe` 登记，同 id 覆盖，按时间／`Draw.Remove`／脚本停止清理；辅助 `Draw.Add/AddBound/AddTether` 保持直接世界绘制语义。
- **窗口外观与交互**：`RadarOverlayLock`（锁定 + 鼠标穿透）、`RadarScale`（0.3~3 悬浮窗大小；场地始终适配内容视口）、`RadarBackgroundOpacity`、`RadarShowCompass`、`RadarShowRangeRings`、`RadarPulseDanger`。解锁状态下整个视口即拖拽把手（InvisibleButton），滚轮直接调 `RadarScale`（0.8s 防抖后落盘），右键弹快捷菜单（锁定/不透明度/罗盘/距离环/脉冲/隐藏雷达）。窗口位置由 imgui.ini 按窗口名「MasterPlayer 雷达」持久化。

### 5.8 录制回放（`Replay\`，对齐 BMR `BossMod/Replay/` 的 Operation 思路）

机制事件的录制与时间轴回放，用于复现战斗、测试脚本运行状态。**PR 本体的 PromeEventBus/LogSystem 只订阅不发布（无公开 Raise），合成事件因此只在 MasterPlayer 脚本层派发**：录制走 `PromeEventBus.OnLogEvent` 全量订阅，回放经每脚本 `ReplayScriptEventRouter` 直喂，不进总线，DodgeController/Follow/其他插件不响应回放事件。

- **录制文件**（`.mpreplay.jsonl`，JSON Lines）：首行 header `{"v":1,"territory":…,"territoryName":…,"startedUtc":…,"pluginVersion":…}`（territoryName 为后加字段，旧文件缺省空串、新文件对旧读取器兼容）；之后每行 `{"t":秒,"kind":"CastStart","type":"CLR 全名","data":{…}}`。序列化配置（`ReplayJson.Options`）：剔除 `LogSystemEvent.Raw`（运行态对象图不可序列化，回放后恒为 null）与 get-only `Kind`；自带 `Vector3JsonConverter`（`Vector3?` 同用）。未知类型/损坏行跳过并计入 `ReplayRecording.SkippedEvents`。
- **ReplayRecorder**：手动（面板按钮或 `/prmp rec`）或 `AutoRecordOnBattleStart` 自动（战斗开始录、结束停）。目录 `ConfigDirectory\MasterPlayer\Replays\`，文件名 `rec_yyyyMMdd_HHmmss_t{territory}_{区域名}.mpreplay.jsonl`（区域名取 Lumina TerritoryType：优先副本名 ContentFinderCondition、回退 PlaceName，非法文件名字符替换为 `_`，取不到则省略）。`IsRecording` / `CurrentFilePath` / `EventCount` / `FilteredCount` / `Elapsed` / `StatusMessage` 供面板轮询；录制与回放互斥。**来源过滤**：`ReplayRecordFilter.ShouldRecord(ev, isPlayerOwnedSource)` 跳过来源为玩家（ObjectKind.Pc）或玩家召唤物（OwnerId 属于 PC，录制器侧 0.5s 对象表缓存）的动作/状态/读条（CastStart/CancelCast/InterruptCast/ActionEffect/StatusAdd/StatusRemove），且聊天日志（ChatLog）一律不录（NpcYell/InstanceContentText 战斗台词不属于聊天，保留）；SourceId 为 0 或无法判定一律放行，连线/点名/场地/战斗等事件不过滤。
- **ReplayController**：`Load(path)` → `Play()/Pause()/SetSpeed(0.25~4)/Seek(秒)/StopSession()`；`Update(deltaTime)` 在插件主循环中先于 `_scripts.Update` 调用。会话启动 = `ScriptManager.StopAll()` → `EnterReplayMode()` → 对「已启用 && 区域匹配**录制区域**」的描述符 `StartScript(id, new ReplayScriptEventRouter())`；时钟按 `deltaTime × Speed` 推进，到期事件对每个 router `Raise`。`Seek` 为 BMR 式「重置 + 快进」：重启回放脚本后从开头补发至目标时刻（脚本须确定性重放，契合铁律 7）。回放脚本被外部停光（刷新脚本/关总开关）时自动 `StopSession`；`StopSession` 只停仍是回放会话的脚本，之后下一帧区域自动启停恢复实时脚本。
- **ReplayScriptEventRouter**：强类型 LogSystem 订阅按 `registeredType.IsAssignableFrom(ev.GetType())` 派发（`LogEvent` 兜底/泛型天然覆盖）；`LogSystemBattleStarted/Ended/CountdownStarted/WeatherChanged/Territory/ChatLog` 映射补发同名简单事件（含负载：WeatherId/(ushort)TerritoryId/Sender+Message+LogKind）。EventManager/LogMessageManager 独有的简单事件（PlayerDied/PlayerRevived/PlayerMoveStateChanged/SystemLog/BattleLog/ShowMessage）无 LogSystem 对应记录，回放不触发。逐订阅者异常隔离（对齐 LogSystemHub），异常计入 `ErrorCount`/`LastError`。
- **边界**：不录 actor 位置快照、不重建 WorldState——脚本经 `Svc.Objects` 直读游戏状态在回放中仍是实时值；事件自带的位置/朝向字段（SourcePosition/EffectPosition/SourceRotation 等）可正常回放。回放中脚本照常注册 SafeZone 区域（雷达/真实世界绘制可见），`OnUpdate` 照常按真实帧时间 tick。
- **ReplayTimeline / ReplayComparison**（纯逻辑，tests 可链接）：`ReplayTimeline.Build(ReplayRecording)` 产出读条区间（同 SourceId 的取消/打断事件截断横条并标记）、泳道分类打点（Cast/Effect/Tether/Status/Mark/Arena/Battle/Misc）与实体名映射（ObjectCreate/ActionEffect）；`ReplayComparison.Compare(IReadOnlyList<ComparisonLog>)` 按读条 ActionId 聚合每日志次数、对齐后首用时间与首用最大差，`FirstCastTime`/`FirstBattleTime` 供对齐锚点。名称解析（Lumina）只在 UI 层做。
- **ReplayWindow**（独立窗口，契约见 §3.1）：时间轴回放页签上内嵌 `RadarView`（回放中实时呈现脚本注册的机制范围）下时间轴（点击 Seek 调 `ReplayController.Seek`）；日志对比页签支持按战斗开始/首次读条对齐与手动偏移，归纳共同/差异技能。
- 回归：`tests/ReplayRecordingRegression.cs`（序列化往返 + 路由派发 + 异常隔离/退订 + 玩家来源过滤）与 `tests/ReplayTimelineRegression.cs`（时间轴构建 + 对比对齐），经 tests csproj 源码链接 `ReplayRecording.cs` / `ReplayRecordFilter.cs` / `ReplayScriptEventRouter.cs` / `ReplayTimeline.cs` / `ReplayComparison.cs` / `IScriptEventRouter.cs`（均不依赖 Svc/PromeEventBus）。

## 6. 铁律（违反任何一条都等于 bug）

1. **主线程纪律**：SafeZone 注册/快照（`SafeZoneRegistry` 全部方法、`SafeZoneManager.BuildSnapshot`）与 `DrawManager` 全部操作**只能在游戏主线程**（`Svc.Framework.Update` 回调或 `UiBuilder.Draw`）执行。事件处理首选 **`PromeEventBus`（主线程派发）**，回调里可直接注册危险区/绘制——`DodgeController` 已迁移至此模式（订阅 `OnStartCast`，回调直注册，无队列）。`CombatEventManager` 裸事件由 hook/网络线程触发：回调里**只做入队**（`ConcurrentQueue` 之类），在自己的 `Update()`（主线程）里统一出队处理，严禁在回调里直接碰 SafeZone/DrawManager。
2. **Dispose / 热重载纪律**：PR 插件管理器可随时卸载重载本插件。`Dispose()` 必须做到：
   - 退订全部事件源（`Svc.Framework.Update -=`、`UiBuilder.Draw -=`、PromeEventBus 订阅逐个 `Dispose()` 或 `UnsubscribeAll(owner)`、CombatEventManager 静态事件 `-=`）；
   - 清绘制：`PromeRotation.Plugin.Instance.DrawManager.RemoveByRegex("^MasterPlayer_")`；
   - 停止**自发起**的 GreenMove（自己接管过才 `Stop()`，见铁律 3）；
   - **每个环节独立 try/catch**（参照 PromePainter 的 `SafeDispose` 模式），一个环节抛异常绝不能跳过后续清理；Dispose 幂等（`_disposed` 标记）。
3. **移动指令纪律**：`GreenMoveSystem.MoveTo` 是 **last-write-wins**（替换当前路线），**无优先级**概念——后发的路线直接顶掉先发的，谁的都可能被顶。因此：
   - 不要每帧重发相同路线（每发一次都重置路线状态与读条看门狗）；只在路线**变化**时 `MoveTo`，到达/失效时 `Stop()`；
   - **只有自己接管过移动才调用 `Stop()`**，绝不停掉本体或其他插件发起的移动；
   - 脚本手动移动在旧版 `SafeZoneSetting.EnableAutoMovement` 或 Reborn `SafeZoneRebornSetting.EnableAutoMovement` 开启，或任一安全区系统 `HasControl` 时让位；自动躲避开启时，跟随只注册 Reborn 动态目标区，机制目标优先，禁止在 HasControl 释放后直接回位；自动躲避关闭时的直接跟随仍在任一安全区系统持有控制权时让位。`Stop` / `ClearQueue` / `SkipCurrent` 也不能修改安全区接管后的路线。
4. **绘制 id 统一 `MasterPlayer_` 前缀**：所有 `DrawManager.Add*` 的 id 必须以 `MasterPlayer_` 开头。Dispose 用 `RemoveByRegex("^MasterPlayer_")` 一键清干净，也避免与其他插件/脚本的绘制 id 冲突。
5. **设置持久化走自己的 `MasterPlayer.json`**：不用 Dalamud 的 `GetPluginConfig`（那是 PR 宿主自己的配置通道，卫星插件调用会拿到宿主的配置）。本插件设置一律经 `MasterPlayerSettings`（`System.Text.Json`）读写 `ConfigDirectory\MasterPlayer.json`。
6. **脚本回调与 OnUpdate 均在主线程执行**（PromeEventBus 事件由主线程派发），脚本内可直接调 SafeZone / Move / Draw 三门面，无需自行入队；脚本不得再订阅 `CombatEventManager` 裸事件（那是 hook/网络线程，绕过了 owner 管理）。
7. **脚本不得持静态状态跨热重载**：每次热重载是全新编译的程序集 + 全新实例，`static` 字段会在旧程序集中残留副本并与新实例并存（重复订阅、状态错乱）。状态一律放实例字段，`OnStop` 做收尾。


### 2026-09-18 界面与高一复核补充

- 主面板按总览、显示与绘制、自动走位、机制脚本、录制回放、诊断组织；设置页、回放、编辑器及雷达菜单使用实际 PR 的 `PromeUiTheme.PushWindowStyle`，动态颜色令牌跟随明暗与主题色。`Windows/PluginUi.cs` 统一提示、满宽滑条与窄区域开关说明换行。
- 绘制页内分雷达、世界绘制、按机制显示；危险/安全填充和边框分别提供颜色与 0–100% 不透明度滑条，持久化仍为原 RGBA，不迁移或重置用户配置。隐藏绘制不删除危险区。
- 脚本页搜索名称/区域 ID，筛选全部/当前区域/运行中；左列表右详情，原有脚本启停、设置、机制开关与可视化编辑入口保留。编辑器保存为主操作，复制/重新读取/删除收入“更多”，原有未保存和删除确认保留。
- 回放录制与播放分行、文件支持搜索和折叠，场地预览默认收起；事件类型收进折叠项，隐藏泳道不占空白行。文件列表在开启、手动刷新和录制结束时更新，空目录不每帧扫描。
- `RadarViewportLayout` 将 RadarScale 用于悬浮窗整体大小，限制到屏幕可用大小；`RadarArenaLayout` 只按实际半径与旋转轮廓适配，不再二次放大。覆盖用户 1.152 倍、0.3–3 倍、多字号与异形回归。
- 高一 B22–B31 对照 BMR 70 个组件，无遗漏。魔界花/巨像击退正常读条结束不撤落点约束，命中、取消、死亡或 +2s 超时清理。免疫状态匹配 BMR，要求状态结束严格晚于判定。
- 石像鬼钢铁/月环是分阶段解算的局部例外：同时保留两段显示预告，第一击前只导航钢铁与外围待机；判定/原有超时后撤旧区并导航内圈。实战默认危险余量 0.5m + PR 空间/时间余量不能重复封死两秒转场。具体时序和测试见 `docs/reference/CrucibleAll.spec.md`。

### 2026-09-18 高二躲避延迟修复补充

- 最新 23:02:30 ARR 是 B32 佛劳洛斯；51.218s 三条49194命中，不能将该文件当作 B33 夺心魔。分析及源码/部署备份在 `F:/project/卫月插件/analysis/masterplayer-high2-20260918-230230/`。
- 本地 PR Reborn 修复解析光栅化、无目标安全点提前完成、严格搜索剪枝、异步路线期限与重算保持、折线撤离预算、路径平滑和异形边界复核。常规求解失败时仅对1.5s内脚下待判定伤害重试一次0.25s提前余量；真实危险时间与空间余量不变，禁止全局推迟伤害。
- `tests/LateDodgeRegression.cs` 18项专项与完整311项回归通过；测试须引用本轮修复的实际宿主候选，不能拿旧PR DLL验证新执行器。PR本体与MasterPlayer必须成对更新并重载；只刷新机制脚本不生效。
- `DiagnoseMovement` 现覆盖全部斗兽组，每秒限频，记录脚下危险未接管时的期限、快照/结果年龄、异步计数与当前技能。离线回归不替代游戏内实战验收。

### 2026-09-19 夺心魔实战反馈

- 对照BMR，B33包含场地、普通AOE、小怪策略、菌床、水点名、雷点名、麻痹/毒全屏提示共8组件，无整项遗漏。最新可用ARR为 `2026.09.18 23.58.25.dat`，分析在 `F:/project/卫月插件/analysis/masterplayer-mindflayer-20260919/`。
- **覆盖上一节的1.5s限制**：普通解算已经失败时立即允许既有0.25s提前余量重试，不能等倒计时降到1.5s才开始。重算必须保留正在逃离待判定危险的紧急余量，避免半路恢复0.5s后停步；真实时窗与空间余量未修改。
- `CrucibleEngine.Cast`去重须比较锁定朝向和目标ID，同位置同技能不能丢弃事件包对本地扫描朝向的修正；已由同源旋转十字回归确认。日志同时记录持有/执行状态及各读条的朝向、落点，覆盖HasControl=true但GreenMove已停的情况。
- `MindflayerLiveRegression`4项新增回归（四激光采用当前0.5m用户余量、50ms起步延迟及连续重算），完整315项通过，五盘入口编译通过。十字形状与BMR距离函数一致；原回放缺运行时采样朝向，不能声称已唯一确定本次十字受击原因。


### 2026-09-19 高二与劳妲复核补充

- 高二B32–45共73组件与BMR一致。劳妲锁线按波次释放引导，迟到连线不能重开；已落地飞刀与投放helper生命周期分开。普通AOE/剑线/烙印/视线正常读条结束要等待真实效果，取消和死亡另处理。
- 5555/5341状态可能乱序或无时长，禁止对DateTime.MaxValue直接AddSeconds；效果朝向用事件携带值，不能把后续扫描朝向当锁定依据。
- `AddAoe`公开签名保留。内置机制通过`AddAoePreview`/`AddLinePreview`向双视图提供描边、紧迫度及连线；精准预览拥有显示时，通过SetNavigationOnly仅隐藏同几何的解算绘制副本，不删除危险区。保留用户配色/透明度/来源开关和owner清理。
- `High2MigrationRegression`覆盖全部已配置高二读条的朝向修正与生命周期，并用旧劳妲ARR时序验证三剑撤离、40m到20m击退转场。在线简介不作为精确尺寸真源；自动面向/停手/交互等既有降级没有被本次绘制补全替代。详见CrucibleAll.spec及本地analysis/masterplayer-high2-lauda-20260919。

- 本轮新增50项、完整365项通过，Release编译与5盘smoke通过；01:16已备份替换MP，MVID `a77e577e-8462-44d3-901e-c6d4568d40c2`。部署时游戏未运行，下次启动加载；PR本体未动。

### 2026-09-19 11:29 ARR 后续修复

- 杜尔迦三大圈的半路停步可用200ms异步发布延迟复现。PR `Spatial/SafeZoneReborn/SafeZoneRebornMovementSystem` 拒绝候选/收到失败时须先按GreenMove当前索引和最新场地、障碍、危险窗复核正在执行的路线；仍安全则继续，保留其目标和紧急余量。不得仅因候选过时而取消有效逃离路线，也不得无条件延续被新危险封锁的旧路。
- 夺心魔地面水来自真实EObj `2005400/0x1E9998`，不是未结算的水icon；按存活与EventState!=7扫描并描边。只有雷点名者受放置约束，采用R8水池+R6雷圈完全分离的保守策略，不能把水池设为永久禁区或把自己移动的点名圈当危险追逐。
- High2CrucibleSpread重复icon不推迟期限；水49199/雷49205视觉取消撤对应点名，正常结束等真实效果；效果、超时、死亡、换组清理。水池视觉边缘及整圈分离策略仍待实战核对，详见CrucibleAll.spec。
- 新增19项回归，完整384项通过；本轮构建测试须引用 `analysis/masterplayer-high2-20260919-112903/host/` 中实际PR候选。MP Release与五盘smoke通过。本体与MP配套部署，不能仅刷新脚本。

### 2026-09-19 罗盘路线绘制卡死

- `RadarView.DrawSolutionPath` 原来的浮点游标while循环在虚线边缘会因加法舍入不再前进，普通路径即可复现无限循环。寻路回归没有执行这段ImGui绘制，不能据寻路全绿排除绘制卡死。
- `RadarPathDashes` 以double距离及整数虚线索引生成线段，每帧最多2048个虚线区间；过滤非有限输入、重复点和退化段。保留折线相位、动画与透明度渐变，实时/回放罗盘共用。禁止恢复浮点累加直到到达端点的无界循环。
- 新增9项回归（5组旧循环卡死数值、60秒×144帧、无效/极大坐标、静止路径），完整393项通过。Windows AppHangB1记录及原日志在 `analysis/masterplayer-freeze-20260919-1218/`；原进程已被关闭且无堆栈，死循环是确定缺陷，但不能声称已唯一证明实战卡死归因。

### 2026-09-19 普通二回放与用户僵尸策略

- 新证据 `analysis/masterplayer-low2-20260919-123703/`：B07/B09/B10。虚灵法师补707死亡驱动移动预览→48182固定圆、2161/1257精神控制全路径箭头及5424层数提示；未自动接管面向。
- 用户明确要求僵尸先击杀、禁止一直拖着跑：`CrucibleZombies`不注册身体/移动危险或障碍，仅击杀提示。后续不得恢复旧BMR僵尸自动避让；瘴气与其他技能继续躲避。
- 双足飞龙龙卷按观测位移预测，不用攻击转向当速度；停止/瞬移清预测，解析圆矩形替代逐体全场采样。台风落点校验包含龙卷与相邻判定AOE。
- 本体Reborn不因每次危险快照变化取消在途解算。变版本成功路线只作为候选，必须通过最新快照路径校验；已在持续伤害中仅准初始退出、禁止再入或危险终点。MP和本体要配套编译部署。详情和限制见CrucibleAll.spec。

### 2026-09-19 斗兽技能备用资料

- 资料入口：[CrucibleSkills/README.md](docs/reference/CrucibleSkills/README.md)。五盘45组、122条官方敌人名录、430个已知Action与49个关联状态，含中英文名称、分盘技能表、中文攻略要点、网页来源及待核验项。
- 本资料只供后续开发参考；网页攻略、本地客户端字段和现有代码记录已分别标注。视频仅收集简介和分P目录，不视为已核验视频机制；不要将表读条直接当伤害判定时刻，也不要将接管列表当机制正确性证明。

### 2026-09-19 本地BMR机制同步

- 以本地工作区（含未提交机制）为准，45组253组件逐项核对；新增B03移动冰、B08牛头魔链条、B15鱼人/壳/本体转火、B17逐眼背对、B19分波爆炸、B21异形地板/引导/双向洪流、B28攻击转交、B30熔毁与三连击退。保留MP水雷分离、移动龙卷、取消/效果生命周期和雷达死循环修复。
- B08月环接黑球、B30三连击退必须分阶段导航；后续波仍绘制。B08仅该局部月环不重复外扩，B30窄就位区采用缓存的0.1m快照及明确目标。详见 `docs/reference/CrucibleAll.spec.md`，不得把所有未来波无条件当成步行危险区。
- 僵尸先击杀，不躲身体；追人火/雪爆弹只预览，不绕行、不强制转火；石像鬼拉线维持用户特化策略。
- 自动转火/背对经可选宿主 `PromeRotation.Hosting.MechanicControl` 接口；只有开启自动躲避与走位引导才生效，回放禁用。旧宿主缺接口或签名不兼容时一次提示并保留绘制/走位。禁止硬依赖未发布本体接口，禁止直接覆写用户锁角/目标设置。
- 新回归入口 `--bmr-local-sync`，可配 `--filter=<用例名称片段>` 定点复现；全量回归也包含这些用例。构建与移动回归须引用配套本体。
- 本轮新增40项、完整455项回归通过，MasterPlayer Release 0警告0错误，五盘独立脚本编译通过；17:28配套本体/MP已备份替换并核对哈希。测试及宿主补丁在工作区 `analysis/masterplayer-bmr-sync-20260919/`；本体适配未包含在MasterPlayer源码提交中。

### 2026-09-19 通用撤离时机优化

- 自动走位 → 智能躲避新增默认开启的 `OptimizeDodgeTiming`。仅开启智能躲避即可使用，不依赖选中 Boss、跟随或近战/远程模式。配置保存在 MasterPlayer 自身 JSON。
- `SafeZoneActivation` 每帧在 Reborn 实际自动移动开启时刷新 `SafeZoneSetting.AttackUptimeExpiresAt`，有效期 0.5 秒；关闭或卸载只撤销自身最后写入的请求。此字段表示请求生命周期，不是预定出发时间；需要配套编译、部署本体与 MP。
- 本体 `SafeZoneRebornMovementSystem.Uptime.cs` 复用既有危险时间窗、剩余折线路线复核与滑步窗口；普通 AOE 有余量时等待，按当前移速验证延后出发后的整条路线，保留原危险起止余量及 0.15 秒起步余量。读条判断按实际离开危险的时间，不再把无害的后半段路线算作逃离耗时。主线程不新增寻路搜索。
- 已起步、当前危险、紧急缩减余量路径、分阶段转场、激活的机制目标及未知/冻结约束不做起步延后。跟随的可选攻击目标优先级为 -1；非负优先级用于必须完成的机制站位。负优先级不改变现有光栅目标权重。
- 回归入口 `--filter='Dodge uptime'`，匿名录像 fixture 为 `tests/Fixtures/Crucible/uptime-low1-205342.json`；证据在 `F:/project/卫月插件/analysis/masterplayer-dodge-uptime-20260919/`。用户更正后的录像是普通一盘 20:53:42，不能用高一 15:46:32 代替此次出发时机验证。

### 2026-09-19 连续波次撤离精修

- 普通一盘 21:29:27 ARR 记录 B02 第三条肢解 46879（207.474s）和 B04 第一圈地面隆起 46902（270.226s）命中，旧配套本体在真实组件/移动链路离线重现原地不动。
- 本体安全时间区间搜索在完整时间路线暂不可行时，返回最近、且安全时间严格优于原地的临时站位；保留全部危险/障碍/场地检查及有限等待期限。`IsPartialEscape` 标识只验证到当前避险阶段，不能当作整套机制已经规划完成。
- 调度复核临时路线仍安全、快照版本未变时保持该阶段；新事件、动态危险改变、期限到达会失效重算。禁止每次 TTL 都把临时站位推向更远一波。等待及绕行仍由本体负责，MP 不直接竞争移动控制。
- 错峰未来判定不使用单次 AOE 贪输出等待：不同判定时间超过 0.1 秒时优先起步。保留普通单次技能的撤离优化，不全局修改命中时间或放宽安全余量。
- B04 地面隆起的外围准备约束由 R8 改为 R10；原 R8 与首圈经过组件/user/PR 余量后没有可达待机带。伤害绘制、各圈真实半径与判定时间不变。
- 回归入口 `--filter='Recorded dodge refinement'`，fixture 为 `tests/Fixtures/Crucible/dodge-low1-212927.json`；覆盖两套连续波次、跟随开关、80/300ms 后台延迟与80ms移动启动延迟。证据目录 `F:/project/卫月插件/analysis/masterplayer-dodge-refine-20260919-212927/`。本体与 MP 必须配套编译部署。
