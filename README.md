# PromeRadar

PromeRadar（原 MasterPlayer）是 FFXIV 插件 **PromeRotation（PR）** 的卫星插件，提供雷达、世界绘制、智能走位、机制库和录制回放。

- **雷达悬浮窗**：圆形 HUD 式 2D 俯视雷达（完整显示脚本注册的场地边界，可随摄像机旋转；解锁状态下拖拽移动、滚轮缩放、右键快捷菜单），显示安全区快照、解算路径与敌我散点，仅在有机制脚本的区域自动出现

聊天输入 `/prpr` 或在 PR 插件管理器中点「打开 PromeRadar 设置面板」即可开关设置面板。
- **真实世界绘制**：AOE 预告 / 安全点等游戏内 3D 世界范围提示（DrawManager）
- **智能走位**：统一接近目标、避险和机制站位。所有跟随目标交给同一个安全区规划器；机制站位优先于攻击距离。关闭后仅保留绘制、提示和路线规划，撤销插件持有的移动与朝向控制，禁止脚本自动竞速、跳跃及交互。
- **机制库**：统一管理内置机制模块和自定义脚本，在「副本与区域」按副本启停；自定义脚本保留逐脚本设置、可视化编辑器和保存路网。同区域专属脚本优先于内置模块。

`/prpr ai on` 开启智能走位，`/prpr ai off` 关闭。DTR 的 `PromeRadar AI：开/关` 显示同一状态，左键切换。`/prpr rec` 开停录制，`/prpr replay` 打开回放；旧 `/prmp` 指令继续可用。其他插件可通过 [IPC 接口](docs/PromeRadarIPC.md) 查询状态、设置开关并订阅状态变化。

开启雷达、世界绘制或智能走位时启用本地解算，只有智能走位允许自动移动。关闭某副本会停用该区域的脚本与内置机制。旧设置自动迁移，首次保存前备份为 `MasterPlayer.json.before-promeradar.bak`。为保持已安装插件身份、外部脚本与配置兼容，插件 ID、程序集、命名空间和数据目录仍保留 `MasterPlayer` 命名。

插件的几何、配置、注册表、快照、解算器与调度器均位于 `MasterPlayer.Spatial`，与 PR 本体独立。最终移动共用 GreenMove；PR 旧版或 Reborn 自动移动开启或持有控制权时，插件让位。发生冲突时，可在「总览」页点击「关闭 PR 本体安全区自动移动」。

绘制和躲避只针对已识别并注册的危险区；普通读条由 AOE 数据识别，复杂机制需要运行对应脚本。设置面板的「诊断」页可查看当前危险区数量与运行中脚本数量。

安全区实现完整迁自 PromeRotation SafeZoneReborn，后续解算修复只需更新并重载 MasterPlayer。PR 继续提供 GreenMove 移动执行、DrawManager 世界绘制、事件总线与移速信息。迁移来源及接口边界见 [Spatial/README.md](Spatial/README.md)。

## AOE 显示设置

设置面板的「绘制外观」页分别配置真实世界和雷达：危险区／安全区的填充、边框颜色与透明度，以及圆形、月环、扇形、矩形等形状的显示开关。颜色的 A 值为不透明度（0 完全透明，1 完全不透明）；启用「危险预告逐渐加深」时，A 值为填充的最大不透明度。毒池、地雷等不能穿越的区域也按危险区显示。

下方列表为每个已识别技能或脚本 AOE 分组提供「真实世界」「雷达」两个独立开关。新 AOE 首次注册后加入列表，编辑后自动保存。斗兽棋脚本按读条技能、毒池、地雷、引导安全点等分别分组；旧脚本未指定分组时按整个脚本控制。隐藏绘制不会移除危险区，也不会关闭躲避。脚本的连线、路线标记等辅助提示仍由脚本自身控制。

雷达用本地 `GeometryHelper.WorldToLocal` 投影本地世界轮廓，固定朝向时北（−Z）向上、东（+X）向右；跟随摄像机时与 PR 的相机朝向约定一致。真实世界绘制在接口边界转换几何后交给 PR `DrawManager`。直线读条使用从施法者向前延伸的 `RayRectangleTemplate`，中心矩形仍使用 `RectangleTemplate`。

## 构建前置

- .NET 10 SDK（只安装 .NET Runtime 无法编译）；可使用 `winget install --id Microsoft.DotNet.SDK.10 --exact` 安装
- 与实际运行版本一致的 PromeRotation 程序集（`PromeRotation.dll` / `PromeRotation.Scripting.dll` / `ECommons.dll` 等）；仍需公开 GreenMove、事件、绘制和安全区控制状态接口，但不依赖本体的解算修复
- 本机 XIVLauncherCN 的 Dalamud 开发环境（`%AppData%\XIVLauncherCN\addon\Hooks\dev\`，提供 Dalamud / ImGui / FFXIVClientStructs / Lumina 等引用）
- 所有引用均 `Private=false`（不随输出拷贝），运行时由 PR 本体与 Dalamud 提供
- `Directory.Build.props` 默认使用当前用户的 `%AppData%\XIVLauncherCN`，并在只有一个 PR 安装版本时自动引用 `installedPlugins\PromeRotation\<版本>\`。主项目、机制运行库和测试项目共用这些路径。多个 PR 版本并存时，必须指定实际启用的版本，避免引用错误的 DLL：
  ```powershell
  dotnet build -p:PromeRotationPath="D:\path\to\installedPlugins\PromeRotation\1.5.10.1"
  ```
- 也可继续通过本体源码仓库指定 `PromeRotationRoot`（引用该仓库 `PromeRotation\bin\Debug\`）：
  ```
  dotnet build -p:PromeRotationRoot="D:\path\to\PromeRotationRepo\"
  ```
- 非默认启动器或 Dalamud 路径可通过 `XIVLauncherRoot`、`DalamudRoot` 覆盖。重复使用的本机配置可以保存在仓库根目录 `Directory.Build.local.props`（已被 Git 忽略），例如：
  ```xml
  <Project>
    <PropertyGroup>
      <PromeRotationPath>D:\path\to\installedPlugins\PromeRotation\1.5.10.1</PromeRotationPath>
      <DalamudRoot>D:\path\to\addon\Hooks\dev\</DalamudRoot>
    </PropertyGroup>
  </Project>
  ```
- 宿主 Reborn 移动接口按可用性检测；旧版 PR 仍检查原有安全区的开关与控制权，新版同时检查 Reborn，插件自己的解算器保持独立。

## 构建命令

```
dotnet build
```

（默认 Debug 配置；`-c Release` 亦可，输出路径相同。CI 环境下设 `CI=true` 时输出回退到本地 `bin\<Configuration>\`。）

仅编译到仓库、暂不部署到游戏目录时：

```powershell
dotnet build -p:CI=true
```

回归直接编译插件内的安全区源码，覆盖跟随、移动让位、插件与本体隔离、错峰 AOE、生命周期、绘制转换与机制处理，无需启动游戏：

```
dotnet run --project tests/MasterPlayer.RegressionTests.csproj
```

内置脚本编译与实际插件 DLL 的脚本库冒烟检查：
```powershell
dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- . bin/Debug/MasterPlayer.dll
```

界面开关的 PR 版本兼容检查：`dotnet run --project tests/UiCompatibility/MasterPlayer.UiCompatibility.csproj -- Windows/PluginUi.cs bin/Debug/MasterPlayer.dll "实际PR目录/PromeRotation.dll"`。该检查覆盖不同 ToggleRow 签名、缺失方法回退和布尔值回写，并确认编译产物没有直接绑定该宿主方法。

机制运行库测试还需本机游戏数据，指定真实 `game\sqpack` 目录：

```powershell
dotnet run --project tests/MechanicRuntime/MasterPlayer.MechanicRuntime.Tests.csproj -- . "D:\path\to\FFXIV\game\sqpack"
```

## 部署方式

构建输出目录即 PR 的插件目录：

```
%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\Plugins\MasterPlayer\
```

构建后 `MasterPlayer.dll` 与 `PromeRotation.Scripting.dll`（由构建后拷贝 Target 自动复制）位于该目录。在游戏内打开 PromeRotation 的插件管理器，启用 PromeRadar 即可；重新构建覆盖输出后可在插件管理器中热重载，无需重启游戏。

## 机制脚本

仓库 `Scripts/` 下使用 `MasterPlayer.Scripts` 及其子命名空间的机制脚本随 DLL 内置加载，无需复制到配置目录。斗兽奇弈五盘入口已内置；更新并重载插件后，在「机制脚本」页勾选所需脚本，进入对应区域自动启动。详情显示脚本来源，启用状态、设置与机制开关仍保存在 `MasterPlayer.json`。

「金碟巨豆中心（糖豆人）」已内置，区域1165。按娱乐竞速场景运行，无需进入战斗；已核对十二份 ARR 中的两种第一关、搬砖与喷风跑圈第二关及决赛。支持自动走位、消失桥换道跳跃、水晶领取投放、箭头与强制移动处理，并提供读条范围、实时滚动球/旋转长条及五列波浪预测。进本前启用脚本，「自动竞速（走位、跳跃与搬运）」默认开启；测试须使用配套 PR/MP DLL，并关闭 PR 本体自动躲避，避免宿主移动接管。MP 不修改该宿主设置。回放仅绘制和模拟；实机待验项目见 [Blunderville 规格](docs/reference/Blunderville.spec.md)。定向离线验证：`dotnet run --project tests/MasterPlayer.RegressionTests.csproj -- --filter=Blunderville`，测试引用须指向配套 PR DLL。

### 糖豆人第一、二关：车道路网

第一关A/B、第二关圆环与搬运各有一张路网。未保存时使用内置路网，与原固定路线一致；第一关B在直道末端（Z=-189）可换到另一侧。到分叉节点时按当前机关预测选择最早能到终点（搬运为目标投放区）的分支，没有预测时走最短连线。节点之间的躲避、跳跃、消失桥、黏液坡、转盘与搬运走廊仍由各自规划器负责；“跳跃节点”交给跳跃规划器，Y 为节点所在平台高度。配置为 `MasterPlayer.json/BlundervilleCourseGraphs`，可在编辑器中删除已保存路网以恢复内置路网。

### 糖豆人第三关：机制路网与手工编辑（1.14.35）

在 `/prmp` →「机制脚本」→「糖豆人路网（一二三关）」打开节点与连线编辑器，顶部切换第三关与第一关A/B、第二关圆环/搬运。你负责定义路网，程序到分叉节点后根据机关预测，从该节点所有已连接的后继中选路；不生成新连线，不跳到旁边分支。可以分叉、汇合、显式回头，线条相交不会自动连通。机制名称只是分组高亮，不把路网限制成一条线。

- 「选择／拖动节点」：左键选择并拖动，右键删除节点及其连线；删除不会补连邻居。
- 「画连线／分支」：右键节点设为连线起点，左键空白处新增并连接，左键已有节点连到它。回到同一节点可反复画出多个分支，箭头表示行进方向。
- 「在线上插入节点」：在已有连线上插入节点后，可以从它继续画分支；「删除连线」只删除点中的线。
- 台阶入口可设多个：在 Z=155.2 新增节点，自定 X；虚线仅示意入口方向。保存时检查入口与首阶支持面，上台阶时逐阶选择同侧起跳和落点，允许横移、斜跳与不同跳跃距离。

可用「载入按机制铺设路网」载入可撤销的草稿：第一窄道内外通道、第二窄道四通道、圆形机关交界、波浪换列及中央缺口两侧、末段交替圆与八个台阶入口。它只是可编辑节点与连线，不会在运行中自动生成新路。

点击「保存路网」生效。旧版已保存的分段路点按原顺序转成节点和连线，保留原几何；没有已保存路线时，只展示可修改底稿。配置为 `MasterPlayer.json/BlundervilleFinalRoute/Graph`，旧字段保留历史数据。编辑草稿不改变当前执行路网。

最多256节点、1024条有向连线，每个节点都须从起点可达，并最终可到达一个有效台阶入口。保存检查整段地形，虚拟等待点只在你画的线内部切分，不连接交叉线。行走中保留当前连线；在当前段结束前3秒开始预计算后续，按实际位置与时间复核后连续行走。无可行分支时等待或提示；坡道路网偏离当前连线约0.4米以上则暂停。台阶按各层实际支持面和危险窗口选择起落点，保留着地确认与跳跃冷却。

定向验证：`dotnet run --project tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter="Blunderville graph"`。覆盖旧数据迁移、分叉改选、汇合/有环图、有界等待、禁止隐式连通、实际移动、新台阶入口及录像危险复核。测试不等于游戏内界面或整局通关验收。

自定义 `.cs`、`.dll` 或 `*.mechanic.json` 仍放在配置目录：

```
%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\MasterPlayer\Scripts\
```

配置目录中成功加载的脚本若与内置脚本**名称或 GUID 相同**（忽略大小写），优先使用配置目录版本，只保留一份；名称指 `[MechanicScript]` 的 `name`，不是文件名。即使外部版本停用，也不会启动内置版本。保留相同 GUID 可沿用设置；移除外部文件并点击「刷新脚本」后恢复内置版本。编译失败会显示错误，并保留可用的内置版本。

外部脚本修改后需手动点击「刷新脚本」；内置脚本修改后需重新构建并重载插件。**不要**把外部脚本放进 `Plugins\MasterPlayer\` 插件程序集目录。开发细节见 [脚本开发指南](docs/脚本开发指南.md)。

旧自定义脚本的 `PromeRotation.Spatial.SafeZone` / `.Geometry` 引用需改为 `MasterPlayer.Spatial.SafeZone` / `.Geometry`；直接使用注册表时改为 `MasterPlayerPlugin.Instance.SafeZone.Registry`。外部脚本 DLL 需要重新编译，PR 的绘制样式和移动点类型保持使用宿主 API。
