# MasterPlayer

MasterPlayer 是 FFXIV 插件 **PromeRotation（PR）** 的卫星插件（PR 子插件，不是独立 Dalamud 插件），提供：

- **雷达悬浮窗**：圆形 HUD 式 2D 俯视雷达（完整显示脚本注册的场地边界，可随摄像机旋转；解锁状态下拖拽移动、滚轮缩放、右键快捷菜单），显示安全区快照、解算路径与敌我散点，仅在有机制脚本的区域自动出现

聊天输入 `/prmp` 或在 PR 插件管理器中点「打开 MasterPlayer 设置面板」即可开关设置面板。
- **真实世界绘制**：AOE 预告 / 安全点等游戏内 3D 世界范围提示（DrawManager）
- **智能躲避**：经本体日志系统事件（PromeEventBus 主线程派发）监听读条并注册危险区（带可调安全余量外扩），由 PR 安全区 Reborn + GreenMove 自动走位躲避；支持按不同 AOE 的判定时间暂避、等待并转入已结束判定的区域
- **战斗中跟随**：跟随小队成员或当前选中的玩家、怪物；超出设置距离时自动靠近，路线停止后可恢复，躲避接管时自动让位。怪物距离按双方目标圈边缘计算，玩家按中心计算。在「自动化」页启用跟随、将槽位设为 0 并勾选「无有效槽位时跟随当前目标」与「战斗中生效」，即可在战斗中跟随当前目标。
- **智能机制处理**：监听战斗事件（读条、连线、点名图标等），按机制分发处理器
- **机制脚本框架**：Roslyn 热编译的单文件机制脚本（`.cs`），按副本区域自动启停

开启雷达悬浮窗、真实世界绘制或智能躲避时，插件会启用 PR 安全区 Reborn（关闭时不会提供危险区快照）；开启智能躲避还会启用 Reborn 自动移动。只开启绘制不会启用自动移动。关闭对应功能或卸载插件时，只撤销本插件开启的 Reborn 开关；手动关闭后不会每帧强行打开，可在「诊断」页恢复。

Reborn 与旧版安全区的解算代码、配置实例、区域注册表和场地边界隔离，MasterPlayer 的脚本和绘制均使用 Reborn。两套共用 GreenMove，旧版自动移动开启时 Reborn 让位，面板显示原因；要使用本插件的智能躲避，请在 MasterPlayer「总览」页点击「关闭旧版自动移动」。

绘制和躲避只针对已识别并注册的危险区；普通读条由 AOE 数据识别，复杂机制需要运行对应脚本。设置面板的「诊断」页可查看当前危险区数量与运行中脚本数量。

定位类似 BossModReborn，但全部构建在 PromeRotation 本体 API 之上（SafeZoneRebornRegistry 安全区注册、GreenMove 移动、DrawManager 绘制、CombatEventManager / PromeEventBus 事件总线），不做独立的机制解算。

## AOE 显示设置

设置面板的「绘制外观」页分别配置真实世界和雷达：危险区／安全区的填充、边框颜色与透明度，以及圆形、月环、扇形、矩形等形状的显示开关。颜色的 A 值为不透明度（0 完全透明，1 完全不透明）；启用「危险预告逐渐加深」时，A 值为填充的最大不透明度。毒池、地雷等不能穿越的区域也按危险区显示。

下方列表为每个已识别技能或脚本 AOE 分组提供「真实世界」「雷达」两个独立开关。新 AOE 首次注册后加入列表，编辑后自动保存。斗兽棋脚本按读条技能、毒池、地雷、引导安全点等分别分组；旧脚本未指定分组时按整个脚本控制。隐藏绘制不会移除危险区，也不会关闭躲避。脚本的连线、路线标记等辅助提示仍由脚本自身控制。

雷达直接用 PR `GeometryHelper.WorldToLocal` 投影 PR 提供的世界轮廓，固定朝向时北（−Z）向上、东（+X）向右；跟随摄像机时与 PR 的相机朝向约定一致。真实世界绘制将相同世界几何交给 PR `DrawManager`。直线读条使用从施法者向前延伸的 `RayRectangleTemplate`，中心矩形仍使用 `RectangleTemplate`。

## 构建前置

- 已编译并加载支持 **SafeZone Reborn** 的 PromeRotation 本体（需要提供 `PromeRotation.dll` / `PromeRotation.Scripting.dll` / `ECommons.dll` 等运行时程序集）；首次升级必须重载本体，仅热重载 MasterPlayer 不足以加载新 API
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

跟随目标筛选、距离边界、路线恢复、移动让位，以及新旧安全区隔离、错峰 AOE、开关联动、绘制与机制回归验证使用同一份 PR DLL，无需启动游戏：

```
dotnet run --project tests/MasterPlayer.RegressionTests.csproj
```

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
   （**不要**放进 `Plugins\MasterPlayer\` 插件程序集目录；面板「脚本」页的「打开脚本目录」按钮可直达。）
2. 文件保存后约 500ms 防抖自动热加载，也可在 MasterPlayer 面板「脚本」页点「刷新脚本」手动触发。
3. 在面板勾选启用该脚本；脚本的机制开关（按机制分组）在脚本详情的「机制开关」区逐个控制，启用状态与开关选择都会持久化到 `MasterPlayer.json`。

该脚本为通用脚本（不限区域，按敌人 NameID 扫描自动激活对应组的机制处理）；区域限定脚本则进本自动启动、出本自动停止。
