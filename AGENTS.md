# MasterPlayer — 项目总文档（AGENTS.md）

面向后续 AI 代理与开发者的项目权威文档。动手写代码前先读完本文；涉及 PromeRotation、Dalamud（卫月）或 FFXIVClientStructs（FFCS）的 API 签名与用法，**一律查阅对应仓库源码**（路径见下）。

## 1. 项目定位

MasterPlayer 是 **PromeRotation（PR）的卫星插件**，不是独立 Dalamud 插件：

- **不实现 `IDalamudPlugin`**，不被 Dalamud 直接加载；实现的是 PR 的 `IPromePlugin` 契约，由 PR 插件管理器（`PromePluginManager`）在游戏内反射加载、启用、热重载。
- 功能组件：**雷达悬浮窗（RadarOverlayWindow，BMR 式 2D 俯视小地图）+ 真实世界绘制（RadarRenderer，3D 世界范围提示）+ 智能躲避（Dodge）+ 战斗中跟随（Follow）+ 智能机制处理（Mechanics）+ 机制脚本框架（Scripting）**。术语约定：「雷达」专指雷达悬浮窗；游戏内 3D 世界范围提示叫「真实世界绘制」。
- 定位类似 BossModReborn，但一切能力基于 **PromeRotation 本体 API** 实现：安全区解算（SafeZone）、GreenMove 移动、DrawManager 世界绘制、事件总线。本插件只做机制判断与编排，不重复造解算/移动/绘制轮子。
- Dalamud 服务不经构造函数注入（卫星插件没有 `[PluginService]`），统一走 ECommons 静态门面 `Svc`（`ECommons.DalamudServices.Svc`：`Svc.Framework.Update`、`Svc.PluginInterface.UiBuilder.Draw`、`Svc.Log`、`Svc.Objects` 等）。

## 2. 参考仓库

| 仓库 | 路径 | 用途 |
|---|---|---|
| PromePainter | `C:\Users\xiaos\Documents\GitHub\PromePainter` | 插件模板：同为 PR 卫星插件，入口/csproj/设置持久化/Dispose 模式照抄它；脚本系统（Roslyn 编译 + PromeEventBus owner 作用域 + 描述符注册表）也照抄它 |
| PromeRotation 本体源码 | `C:\Users\xiaos\Documents\GitHub\PromeRotation-1.0-XSZFork`（本体项目在 `PromeRotation\` 子目录） | PromeRotation API 签名的唯一权威来源 |
| Dalamud（卫月）主仓库 | `C:\Users\xiaos\Documents\GitHub\Dalamud` | 查询卫月本体的服务接口、事件、插件接口及 API 用法，以接口定义、实现和源码注释为准 |
| FFXIVClientStructs（FFCS） | `C:\Users\xiaos\Documents\GitHub\FFXIVClientStructs` | 查询游戏原生结构、字段布局、指针及成员函数等互操作 API 的定义与用法 |

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
│   ├── RadarOverlayWindow.cs      # 雷达悬浮窗：BMR 式 2D 俯视小地图（UiBuilder.Draw 主线程 ImGui 绘制）
│   └── RadarRenderer.cs           # 真实世界绘制：DrawManager 3D 世界范围提示（AOE 预告/安全点等）
├── Follow/
│   └── FollowController.cs        # 战斗中跟随：跟随队友/当前目标（GreenMove 执行，躲避接管时让位）
├── Dodge/
│   ├── DodgeController.cs         # 智能躲避：读条事件（PromeEventBus 主线程派发）→ 危险区注册 → SafeZone 解算走位
│   ├── DangerMarginInflator.cs    # 危险区几何外扩（GeometryTemplate 按 DodgeDangerMargin 膨胀）
│   └── AoeShapeDatabase.cs        # ActionId → AOE 形状查表（静态数据库）
├── Mechanics/
│   ├── MechanicDispatcher.cs      # 机制事件分发器：订阅事件源 → 入队 → 主线程派发给 Handler
│   └── IMechanicHandler.cs        # 机制处理器接口（每个具体机制一个实现类）
├── Scripting/                     # 机制脚本框架：Roslyn 热编译 + 描述符注册表 + 每脚本 owner 上下文
│                                  # （ScriptManager / MasterPlayerScript / IScriptHostContext / SafeZone·Move·Draw 三门面等，契约见 §3.1）
├── Examples/
│   ├── ExampleGenericScript.cs    # 通用机制示例脚本（编译进插件 DLL 做冒烟检查）
│   └── ExampleZoneScript.cs       # 区域限定示例脚本
├── Scripts/
│   └── CrucibleHigh1.cs           # 斗兽棋·高一机制脚本（迁移自 BossModReborn；编译进 DLL 冒烟，亦可复制到脚本目录热加载）
├── docs/
│   ├── 脚本开发指南.md            # 面向脚本开发者的中文指南
│   └── reference/
│       └── CrucibleHigh1.spec.md  # 斗兽棋·高一迁移规格（该脚本内容的唯一事实来源）
└── Windows/
    └── MainWindow.cs              # ImGui 主面板（总开关、脚本库、模块状态、调试信息）
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

- `Initialize()`：设 `Instance` → 触发 `Settings.MasterPlayerSettings.Instance` 加载 → 构造 `SafeZoneActivation` 并应用持久化的功能开关 → 依次构造 `_radar = new Radar.RadarRenderer()`、`_dodge = new Dodge.DodgeController()`、`_mechanics = new Mechanics.MechanicDispatcher(this)`、`_scripts = new Scripting.ScriptManager()`（随即 `RefreshCatalog()`）、`_radarOverlay = new Radar.RadarOverlayWindow()`、`_follow = new Follow.FollowController()`、`_mainWindow = new Windows.MainWindow()` → `Svc.Framework.Update += OnFrameworkUpdate` → `Svc.PluginInterface.UiBuilder.Draw += _mainWindow.Draw` → `Svc.PluginInterface.UiBuilder.Draw += _radarOverlay.Draw`。
- `OnFrameworkUpdate(IFramework framework)`：先由 `SafeZoneActivation.Update` 应用功能开关变化，再执行 `_radar.Update(); _dodge.Update(); _mechanics.Update(); _scripts.Update(deltaTime); _follow.Update();`（deltaTime 秒，取自 `IFramework.UpdateDelta.TotalSeconds`）。**EnableScripts 总开关门控在 `ScriptManager.Update` 内部**（插件无条件调用；关闭时 ScriptManager 先 `StopAll` 再提前返回，热重载防抖仍照常处理）。
- `DrawConfigUI()`：ImGui 按钮 `"打开 MasterPlayer 面板"` 调 `_mainWindow.Toggle()`。
- `Dispose()`：退订上述三个事件（Framework.Update、UiBuilder.Draw 两处）→ `SafeDispose` 八个组件，顺序 `_mainWindow` → `_follow` → `_radarOverlay` → `_scripts`（其 Dispose 内部先 `StopAll`）→ `_mechanics` → `_dodge` → `_radar` → `_safeZoneActivation` → `Instance = null`。
- `SafeZoneActivation`：雷达悬浮窗、真实世界绘制、智能躲避任一开启时启用宿主 `EnableSafeZone`（关闭时快照会隐藏危险区）；仅智能躲避请求 `EnableAutoMovement`。功能关闭/卸载时只撤销本插件从 false 开启的开关，保留本体原先已启用的功能；运行中手动关闭宿主开关不会被每帧覆写，面板显示原因。

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
    public float RadarScale = 1.0f;             // 缩放倍率
    public bool RadarOverlayLock = false;       // 锁定位置（鼠标穿透）
    public bool RadarOverlayTransparent = true; // 透明背景

    public AoeViewSettings WorldAoe = new();
    public AoeViewSettings RadarAoe = AoeViewSettings.CreateRadarDefaults();
    public Dictionary<string, AoeVisibility> AoeVisibility = new(); // 来源键 → 名称、World/Radar 开关

    // 战斗中跟随（FollowController）
    public bool EnableFollow = false;
    public int FollowSlot = 0;                  // 0=不指定小队槽位；1~7=PartyHelper.GetParty() 下标
    public bool FollowTargetWhenNoSlot = true;  // 无有效槽位时回退跟随当前目标（仅 PC）
    public float FollowDistance = 1.5f;
    public bool FollowDuringCombat = true;
    public bool FollowOutOfCombat = false;

    // 智能躲避
    public float DodgeDangerMargin = 0.5f;      // 危险区注册时的外扩余量（DangerMarginInflator）

    public bool EnableScripts = true;
    public Dictionary<string, bool> ScriptEnabled = new();                                  // 脚本 guid → 是否启用
    public Dictionary<string, System.Text.Json.Nodes.JsonObject> ScriptSettings = new();    // 脚本 guid → [UserSetting] 值
    public Dictionary<string, HashSet<string>> MechanismEnabled = new();                    // 脚本 guid → 关闭/开启的机制集合
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

**Follow.FollowController**（`Follow\FollowController.cs`）

```csharp
public sealed class FollowController : IDisposable
{
    public FollowController();
    public void Update();
    public void Dispose();
}
```

- 跟随对象：`FollowSlot` 1~7 取 `PromeRotation.Helpers.PartyHelper.GetParty()` 下标；0 或槽位无效时按 `FollowTargetWhenNoSlot` 回退当前目标（仅 `ObjectKind.Pc`）。战斗状态经 `PromeRotation.Core.GameData.IsInCombat()` 判定，按 `FollowDuringCombat` / `FollowOutOfCombat` 门控。
- 移动纪律（铁律 3）：只有 `_hasControl`（自己下发过 MoveTo）才 `Stop()`；`SafeZoneMovementStatus.HasControl`（安全区自动移动接管中）时让位——不发跟随指令，也不 Stop 对方路线。

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
    AoeShapeKind Kind, float Param1, float Param2, bool FollowCaster, float ExtraDelaySeconds);

public static class AoeShapeDatabase
{
    public static bool TryGetShape(uint actionId, out AoeShapeDescriptor? descriptor);
}
```

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

**Scripting 脚本框架**（`Scripting\*.cs`，命名空间统一 `MasterPlayer.Scripting`，含 `ScriptSettingStore`）

```csharp
public sealed record ScriptMetadata(string Id, string Name, uint[] Territorys, string Version, string Author, string Note, string Region, string SourcePath);

public enum ScriptSource { LocalSource, LocalDll }

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
    void Log(string message);
    void LogError(string message);
    IDisposable Subscribe(string eventName, Delegate handler);
    // + 全套强类型事件方法（照抄 PromePainter IScriptHostContext，35 个；含泛型 OnLogEvent<TEvent>；无 OnBalloonTalk / OnBattleTalk2，本体总线未提供）
}

public sealed class ScriptSafeZoneApi { /* internal 构造；AddDelayedHit/AddTimedDanger/AddPersistentDanger/AddPersistentSafeArea/AddTimedSafeArea/AddObstacle/Remove(SafeZoneAreaHandle)（签名镜像本体 SafeZoneRegistry，见 §5.2）；Add*Danger 注册前按 DodgeDangerMargin 经 DangerMarginInflator 外扩（SafeArea/Obstacle 不外扩）；internal void Cleanup() */ }
public sealed class ScriptMoveApi { /* internal 构造；IsRunning/GetStatus()/MoveTo(GreenMovePoint)/MoveTo(IReadOnlyList<GreenMovePoint>)/Append(GreenMovePoint)/Stop()/ClearQueue()/SkipCurrent()；internal void Cleanup() */ }
public sealed class ScriptDrawApi { /* internal 构造；Add/AddBound/AddTether/Remove(string id)（签名镜像本体 DrawManager 的 id 重载，见 §5.2），id 自动加 "MasterPlayer_Script_{guid}_" 前缀；internal void Cleanup() */ }

// ScriptSafeZoneApi.BeginDrawingGroup(string name) → IDisposable：为本作用域注册的区域指定 AOE 显示分组，嵌套名称组合，退出恢复。
// ScriptDrawApi.AddAoe(string id, IGeometryShape geometry, bool safe = false, float durationMs = 0f)：只供显示的世界几何，使用当前分组，不注册到解算器。

public sealed class ScriptManager : IDisposable
{
    public ScriptManager();
    public IReadOnlyList<ScriptDescriptor> Scripts { get; }
    public IReadOnlyDictionary<string, RunningScript> Running { get; }
    public string StatusMessage { get; }
    public string ErrorMessage { get; }
    public string ScriptsDirectory { get; }
    public void RefreshCatalog();
    public bool StartScript(string id);
    public void StopScript(string id);
    public void StopAll();
    public void Update(float deltaTime);
    public bool IsScriptEnabled(string id);
    public void SetScriptEnabled(string id, bool enabled);
    public bool IsMechanismEnabled(string scriptId, string mechanism);
    public void SetMechanismEnabled(string scriptId, string mechanism, bool enabled);
    public void SetStatus(string message);
    public void SetError(string message);
}
```

- 脚本目录：`Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "MasterPlayer", "Scripts")`（即 `pluginConfigs\PromeRotation\MasterPlayer\Scripts\`，**严禁放 `Plugins\MasterPlayer\` 内**）。
- 脚本上下文 `MasterPlayerScriptContext` 为 `internal sealed : IScriptHostContext, IDisposable`，internal 构造 `(ScriptManager manager, ScriptMetadata metadata, ScriptSettingStore settings)`。
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
- **脚本层开发无需构建**：改 `pluginConfigs\PromeRotation\MasterPlayer\Scripts\` 下的 .cs 保存 → ScriptManager 文件监听 500ms 防抖自动重新编译热重载（或面板「脚本」区点「刷新脚本」手动触发）；区域不匹配的脚本自动停止。
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

| 属性 | 类型 | 说明与关键成员 |
|---|---|---|
| `SafeZoneRegistry` | `SafeZoneRegistry` | 安全区注册便利层（相对秒数自动转绝对 UTC；**所有方法仅主线程**）。危险区：`AddPersistentDanger` / `AddTimedDanger` / `AddDelayedHit`（单次命中，默认 `DefaultHitDurationSeconds = 0.6f`）/ `AddDelayedDanger` / `AddDangerSequence`；目标区：`AddPersistentSafeArea` / `AddTimedSafeArea` / `AddDelayedSafeArea` / `AddSafeAreaSequence`（含 `priority`、`PositionPreference`）；障碍/边界：`AddObstacle` / `SetArenaBoundary` / `ClearArenaBoundary`；清理：`Remove(SafeZoneAreaHandle)` / `ClearDangerAreas` / `ClearGoalAreas` / `ClearObstacles` / `ClearAll` |
| `SafeZoneManager` | `SafeZoneManager` | 区域注册中心与快照发布者（底层，Registry 的委托对象）。`AddDangerArea` / `AddGoalArea` / `AddObstacle` / `UpdateArea` / `RemoveArea` / `ContainsArea` / `ClearAllAreas`；**`BuildSnapshot(DateTime utcNow)`** 构建解算快照（主线程） |
| `SafeZoneSetting` | `SafeZoneSetting` | 运行时配置（仅内存无持久化）：`EnableSafeZone`（解算总开关）、**`EnableAutoMovement`（允许安全区系统接管自动移动——本插件发移动指令前必须看它，见铁律 3）**、`OnlySolveInRegisteredRegion`、`DebugArenaCenter` / `DebugArenaRadius` |
| `SafeZoneMovementStatus` | `ISafeZoneMovementStatus` | 自动移动只读状态（画配置/诊断 UI 用）：`LatestSolution`（`SafeZoneSolution?`）、`HasControl`、`PhaseText`、`DispatchCount`、`LastDispatchUtc`、`IsAsyncSolveRunning`、`AsyncSolveStartedCount/AcceptedCount/CancelledCount/DiscardedCount` |
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
- **热重载**：`FileSystemWatcher` 监听脚本目录，500ms 防抖后 `RefreshCatalog()`；运行中脚本先 `StopScript`（`OnStop` + 上下文 Dispose）再以新编译的程序集重启；也可面板「刷新脚本」手动触发。
- **owner 清理**：每个脚本上下文持独立 ownerId（`PromeOwnerRegistry.CreateScriptSessionOwnerId()`），事件订阅全部经 `PromeEventBus` 挂在该 owner 下；停止时 `PromeOwnerRegistry.MarkClosing(ownerId)` + `PromeEventBus.UnsubscribeAll(ownerId)` + 三门面各自 `Cleanup()`（SafeZone 移除本脚本全部区域、Move 若接管过则 `Stop()`、Draw `RemoveByRegex("^MasterPlayer_Script_{guid}_")`）。
- **异常隔离**：单脚本编译失败进 `ErrorMessage`（面板红字可滚动）；运行中 `OnUpdate`/事件回调异常只停止该脚本，不影响其他脚本与插件本体。
- **编译引用定位**：部分程序集被流式加载（`Assembly.Location` 为空，如本插件自身，可能还包括宿主体）。`ScriptCompiler` 的引用解析回退链为：程序集 `Location` → `ConfigDirectory\Plugins\<程序集名>\` 包目录 → 宿主 PR 安装目录（`Svc.PluginInterface.AssemblyLocation`）；首次构建引用集时会 Info 日志记录引用数量、探测目录与未能解析的程序集名——脚本编译报 CS0234 类错误时先看这条日志。
- **三门面纪律**：`ScriptSafeZoneApi` / `ScriptMoveApi` / `ScriptDrawApi` 只是 `PromeRotation.Plugin.Instance` 对应系统的转发 + 每脚本句柄簿记，全部主线程调用；绘制 id 由门面自动加 `MasterPlayer_Script_{guid}_` 前缀；Move 门面遵守铁律 3（`EnableAutoMovement` 让位、`MoveTo` last-write-wins、自己接管才 `Stop()`）。

### 5.7 雷达悬浮窗（`Radar\RadarOverlayWindow.cs`）

BMR MiniArena 式 2D 俯视小地图，原生 ImGui 窗口，由 `UiBuilder.Draw`（主线程）逐帧驱动，不受 `EnableRadar`（真实世界绘制开关）影响，独立开关 `EnableRadarOverlay`（默认关）。

- **中心点两模式**：`RadarCenterMode == 0`（场地基准）且快照存在 `ArenaBoundary` 时，以边界中心为原点、按边界包围盒（`GeometryBounds.Width/Height` 较大者 + 3 码 padding）定视野半径；模式 1（玩家摄像机基准）或无边界时以玩家为中心、固定 25 码视野。
- **旋转**：`RadarProjection` 直接调用 PR `GeometryHelper.WorldToLocal(world, center, rotation)`，再乘比例、加屏幕中心。局部 +X 向右、−Y 向前；跟随摄像机时 `rotation = CameraManager.Instance()->GetActiveCamera()->DirH + π`，与 PR 移动相机约定一致；固定北向上和相机不可用时用 `π`。玩家朝向用 PR `PointFromRotationDistanceXZ` 后走相同投影。`GetContours()` 已是世界坐标，不二次平移旋转。
- **数据源**：`SafeZoneManager.BuildSnapshot`（边界描边、障碍与危险区按危险配色、目标区按安全配色；可选随 `DangerWindow.TelegraphProgressAt` 加深填充）及 `AoeVisibilityRegistry` 的仅供显示预览。多边形填充用 PR `PolygonTriangulator.TryTriangulate`，月环用双轮廓三角带；解算路径来自 `SafeZoneMovementStatus.LatestSolution.ExecutablePath`，队友来自 `PartyHelper.GetParty()`，敌人来自对象表。
- **显示配置**：`AoeViewSettings` 为真实世界／雷达独立保存危险和安全区的填充／边框 RGBA、形状过滤及类别开关。`AoeVisibilityRegistry` 关联区域句柄与 `action/{id}` 或 `script/{guid}/{group}` 来源键；未知来源归 `other`。所有显示过滤只发生在两种视图，不能删除解算区域。预览由 `Draw.AddAoe` 登记，同 id 覆盖，按时间／`Draw.Remove`／脚本停止清理；辅助 `Draw.Add/AddBound/AddTether` 保持直接世界绘制语义。
- 窗口外观：`RadarOverlayLock`（锁定 + 鼠标穿透）、`RadarOverlayTransparent`（无背景）、`RadarScale`（0.5~3 缩放）。

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
   - 本体 `SafeZoneSetting.EnableAutoMovement == true`（安全区自动移动已接管）时本插件**让位**，不要与之抢移动权；可用 `SafeZoneMovementStatus.HasControl` / `PhaseText` 观测。
4. **绘制 id 统一 `MasterPlayer_` 前缀**：所有 `DrawManager.Add*` 的 id 必须以 `MasterPlayer_` 开头。Dispose 用 `RemoveByRegex("^MasterPlayer_")` 一键清干净，也避免与其他插件/脚本的绘制 id 冲突。
5. **设置持久化走自己的 `MasterPlayer.json`**：不用 Dalamud 的 `GetPluginConfig`（那是 PR 宿主自己的配置通道，卫星插件调用会拿到宿主的配置）。本插件设置一律经 `MasterPlayerSettings`（`System.Text.Json`）读写 `ConfigDirectory\MasterPlayer.json`。
6. **脚本回调与 OnUpdate 均在主线程执行**（PromeEventBus 事件由主线程派发），脚本内可直接调 SafeZone / Move / Draw 三门面，无需自行入队；脚本不得再订阅 `CombatEventManager` 裸事件（那是 hook/网络线程，绕过了 owner 管理）。
7. **脚本不得持静态状态跨热重载**：每次热重载是全新编译的程序集 + 全新实例，`static` 字段会在旧程序集中残留副本并与新实例并存（重复订阅、状态错乱）。状态一律放实例字段，`OnStop` 做收尾。
