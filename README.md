# MasterPlayer

MasterPlayer 是 FFXIV 插件 **PromeRotation（PR）** 的卫星插件（PR 子插件，不是独立 Dalamud 插件），提供：

- **雷达悬浮窗**：BMR 式 2D 俯视小地图（场地 / 玩家摄像机两种中心模式，可随摄像机旋转），显示安全区快照、解算路径与敌我散点
- **真实世界绘制**：AOE 预告 / 安全点等游戏内 3D 世界范围提示（DrawManager）
- **智能躲避**：经本体日志系统事件（PromeEventBus 主线程派发）监听读条并注册危险区（带可调安全余量外扩），由 PR 安全区解算 + GreenMove 自动走位躲避
- **战斗中跟随**：跟随小队成员或当前目标（GreenMove 执行，躲避接管时自动让位）
- **智能机制处理**：监听战斗事件（读条、连线、点名图标等），按机制分发处理器
- **机制脚本框架**：Roslyn 热编译的单文件机制脚本（`.cs`），按副本区域自动启停

定位类似 BossModReborn，但全部构建在 PromeRotation 本体 API 之上（SafeZoneRegistry 安全区注册、GreenMove 移动、DrawManager 绘制、CombatEventManager / PromeEventBus 事件总线），不做独立的机制解算。

## 构建前置

- 已编译的 **PromeRotation 本体**（需要提供 `PromeRotation.dll` / `PromeRotation.Scripting.dll` / `ECommons.dll` 等运行时程序集）
- 本机 XIVLauncherCN 的 Dalamud 开发环境（`%AppData%\XIVLauncherCN\addon\Hooks\dev\`，提供 Dalamud / ImGui / FFXIVClientStructs / Lumina 等引用）
- 所有引用均 `Private=false`（不随输出拷贝），运行时由 PR 本体与 Dalamud 提供
- 若本体不在默认路径，可用命令行属性覆盖：
  ```
  dotnet build -p:PromeRotationRoot="D:\path\to\PromeRotationRepo\"
  ```

## 构建命令

```
dotnet build
```

（默认 Debug 配置；`-c Release` 亦可，输出路径相同。CI 环境下设 `CI=true` 时输出回退到本地 `bin\<Configuration>\`。）

## 部署方式

构建输出目录即 PR 的插件目录：

```
%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\Plugins\MasterPlayer\
```

构建后 `MasterPlayer.dll` 与 `PromeRotation.Scripting.dll`（由构建后拷贝 Target 自动复制）位于该目录。在游戏内打开 PromeRotation 的插件管理器，启用 MasterPlayer 即可；重新构建覆盖输出后可在插件管理器中热重载，无需重启游戏。

## 机制脚本

插件自带机制脚本框架：把单个 `.cs` 脚本（继承 `MasterPlayerScript`，标注 `[MechanicScript]`）放进脚本目录即可被 Roslyn 动态编译、热加载，无需重新构建插件本体。脚本开发细节见 `docs/脚本开发指南.md`。

以仓库自带的斗兽棋·高一脚本为例：

1. 把 `Scripts/CrucibleHigh1.cs` 复制到脚本目录：
   ```
   %AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\MasterPlayer\Scripts\
   ```
   （**不要**放进 `Plugins\MasterPlayer\` 插件程序集目录；面板「脚本」区的「打开脚本目录」按钮可直达。）
2. 文件保存后约 500ms 防抖自动热加载，也可在 MasterPlayer 面板「脚本」区点「刷新脚本」手动触发。
3. 在面板勾选启用该脚本；脚本的机制开关（按机制分组）在脚本详情的「机制开关」区逐个控制，启用状态与开关选择都会持久化到 `MasterPlayer.json`。

该脚本为通用脚本（不限区域，按敌人 NameID 扫描自动激活对应组的机制处理）；区域限定脚本则进本自动启动、出本自动停止。
