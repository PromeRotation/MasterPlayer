# PromeRadar

> FFXIV 插件 **PromeRotation（PR）** 的卫星插件：雷达、世界绘制、智能走位、机制脚本与录制回放。
> 项目的核心工作流是**由 AI 解析实战录像（ARR），实现机制绘制与躲避，再用离线模拟验证**，见 [ARR 解析与模拟流程](docs/ARR解析与模拟流程.md)。

为保持已安装插件、外部脚本与配置兼容，插件 ID、程序集、命名空间和数据目录仍沿用 `MasterPlayer` 命名。

---

## 功能

| 功能 | 说明 |
|---|---|
| **雷达悬浮窗** | 2D 俯视雷达，完整显示场地轮廓，可随镜头旋转；显示危险区、安全区、解算路径与敌我位置。解锁后可拖动、滚轮缩放、右键菜单。默认只在有机制脚本的区域出现 |
| **真实世界绘制** | 游戏内 3D 显示 AOE 预告、安全点与连线（PR `DrawManager`） |
| **智能走位** | 接近目标、避险和机制站位交给同一个安全区规划器；机制站位优先于攻击距离。关闭后只保留绘制、提示和路线规划 |
| **机制库** | 内置机制脚本与机制模块，按副本启停；自定义脚本支持逐项设置、可视化编辑器和路网编辑。同区域专属脚本优先 |
| **录制回放** | 录制战斗事件与对象快照，在回放窗口用雷达和时间轴复现，并驱动脚本检查绘制 |

内置机制：斗兽奇弈五盘（B01–B45）、金碟巨豆中心（糖豆人）、沙斯塔夏浸蚀洞窟、塔姆塔拉墓园、铜铃铜山。

## 指令

| 指令 | 作用 |
|---|---|
| `/prpr` | 打开 / 关闭设置面板 |
| `/prpr ai on` · `/prpr ai off` | 开关智能走位 |
| `/prpr rec` | 开始 / 停止录制 |
| `/prpr replay` | 打开回放窗口 |

旧指令 `/prmp` 仍可用。服务器信息栏（DTR）的 `PromeRadar AI：开/关` 左键切换智能走位。其他插件可通过 [IPC 接口](docs/PromeRadarIPC.md) 查询状态、修改开关并订阅变化。

## 工作方式

- 开启雷达、世界绘制或智能走位时启用本地解算；只有智能走位允许自动移动。
- 绘制和躲避只针对已识别并注册的危险区：普通读条按技能数据识别，复杂机制由对应脚本处理。「诊断」页可查看当前危险区和运行中脚本数量。
- 几何、注册表、快照、解算器与调度器都在插件内（`MasterPlayer.Spatial`），与 PR 本体独立；最终移动共用 PR 的 GreenMove。PR 本体自动移动开启或持有控制权时插件让位，冲突时可在「总览」页点击「关闭 PR 本体安全区自动移动」。
- 关闭某个副本会同时停用该区域的脚本与内置机制。旧设置自动升级，首次保存前备份为 `MasterPlayer.json.before-promeradar.bak`。

### AOE 显示设置

「绘制外观」页为真实世界和雷达分别设置危险区／安全区的填充、边框颜色与透明度，以及圆形、月环、扇形、矩形等形状的显示开关。颜色的 A 值即不透明度；开启「危险预告逐渐加深」时为最大不透明度。

下方列表为每个技能或脚本 AOE 分组提供「真实世界」「雷达」两个独立开关，新 AOE 首次出现后自动加入。**隐藏绘制不会移除危险区，也不会关闭躲避。**

---

## 构建

### 前置

- **.NET 10 SDK**（仅装 Runtime 无法编译）：`winget install --id Microsoft.DotNet.SDK.10 --exact`
- **与实际运行版本一致的 PromeRotation 程序集**（`PromeRotation.dll`、`PromeRotation.Scripting.dll`、`ECommons.dll` 等）
- **XIVLauncherCN 的 Dalamud 开发环境**：`%AppData%\XIVLauncherCN\addon\Hooks\dev\`

`Directory.Build.props` 默认使用当前用户的 `%AppData%\XIVLauncherCN`，只装了一个 PR 版本时自动引用它。多个版本并存时必须指定实际启用的那个，常用配置可写进已被 Git 忽略的 `Directory.Build.local.props`：

```xml
<Project>
  <PropertyGroup>
    <PromeRotationPath>D:\path\to\installedPlugins\PromeRotation\1.5.10.1</PromeRotationPath>
    <!-- 可选：<PromeRotationRoot>本体源码仓库\</PromeRotationRoot>、<XIVLauncherRoot>、<DalamudRoot> -->
  </PropertyGroup>
</Project>
```

所有引用均为 `Private=false`，运行时由 PR 与 Dalamud 提供。PR 本体更新后必须重新编译本插件。

### 命令

```powershell
dotnet build MasterPlayer.csproj -c Release   # 编译并直接部署到 PR 插件目录
dotnet build MasterPlayer.csproj -p:CI=true   # 只输出到仓库 bin\，不部署
```

### 测试

```powershell
# 回归（安全区、走位、绘制转换、各机制录像模拟），无需启动游戏
dotnet run --project tests/MasterPlayer.RegressionTests.csproj -c Release -- --keep-going
.\run-tests.ps1 -Filter Blunderville           # 多进程分片

# 内置脚本编译 + 实际 DLL 冒烟
dotnet run --project tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -- . bin/Debug/MasterPlayer.dll

# 界面接口兼容（不同 PR 版本的 ToggleRow 签名）
dotnet run --project tests/UiCompatibility/MasterPlayer.UiCompatibility.csproj -- Windows/PluginUi.cs bin/Debug/MasterPlayer.dll "PR目录/PromeRotation.dll"

# 机制运行库（需要本机游戏数据）
dotnet run --project tests/MechanicRuntime/MasterPlayer.MechanicRuntime.Tests.csproj -- . "D:\path\to\game\sqpack"
```

### 部署

构建输出目录就是 PR 的插件目录：

```
%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\Plugins\MasterPlayer\
```

在游戏内 PR 插件管理器中启用 PromeRadar；重新构建后在插件管理器热重载即可，无需重启游戏。替换前建议先给旧 DLL 加时间戳备份。

---

## 机制脚本

仓库 `Scripts/` 下的脚本随 DLL 内置加载，在「机制脚本」页勾选后进入对应区域自动启动；启用状态、设置与机制开关保存在 `MasterPlayer.json`。

自定义 `.cs`、`.dll` 或 `*.mechanic.json` 放在配置目录（**不要**放进插件程序集目录）：

```
%AppData%\XIVLauncherCN\pluginConfigs\PromeRotation\MasterPlayer\Scripts\
```

- 配置目录中的脚本与内置脚本**名称或 GUID 相同**（忽略大小写）时，优先使用配置目录版本；移除文件并点击「刷新脚本」后恢复内置版本。
- 外部脚本修改后点击「刷新脚本」生效；内置脚本修改后需重新构建并重载插件。
- 旧脚本中的 `PromeRotation.Spatial.SafeZone` / `.Geometry` 引用改为 `MasterPlayer.Spatial.SafeZone` / `.Geometry`，外部 DLL 需重新编译。

开发说明见 [脚本开发指南](docs/脚本开发指南.md) 与 [可视化脚本编辑器](docs/可视化脚本编辑器.md)。

### 金碟巨豆中心（糖豆人）

区域 1165，按竞速场景运行，无需进入战斗。支持自动走位、消失桥换道跳跃、水晶领取投放、箭头与强制移动处理，并显示读条范围、实时滚动球、旋转长条和五列波浪预测。进本前启用脚本，「自动竞速（走位、跳跃与搬运）」默认开启；测试时关闭 PR 本体自动躲避，避免宿主移动接管。

**第一、二关车道路网**：第一关 A/B、第二关圆环与搬运各有一张路网，未保存时使用内置路网。到分叉节点时按机关预测选择最早到达终点的分支；节点之间的躲避、跳跃、消失桥、黏液坡、转盘与搬运走廊由各自规划器负责。配置在 `MasterPlayer.json/BlundervilleCourseGraphs`。

**第三关机制路网**：在「机制脚本」→「糖豆人路网（一二三关）」打开节点与连线编辑器。

- 你负责定义路网；程序只在已连接的后继中按机关预测选路，不生成新连线、不跳到旁边分支，可以分叉、汇合、显式回头。
- 「选择／拖动节点」左键选择拖动、右键删除；「画连线／分支」右键设起点、左键新增或连接；「在线上插入节点」在已有连线上插入。
- 台阶入口可设多个（Z=155.2，自定 X），上台阶时逐阶选择起跳和落点。
- 「载入按机制铺设路网」载入可撤销的草稿；点击「保存路网」生效。最多 256 节点、1024 条连线，每个节点须从起点可达并最终到达台阶入口。

定向验证：`--filter="Blunderville graph"`。完整规格与实机待验项目见 [Blunderville 规格](docs/reference/Blunderville.spec.md)。

---

## 第三方组件与许可

- 插件自身代码（安全区与解算器、雷达、绘制、录制回放、脚本框架，以及斗兽、糖豆人、地城等机制实现）均为本项目编写。
- `MechanicRuntime/` 是随插件打包的机制运行库，只用于「机制模块」功能：在没有专属脚本的副本中提供机制绘制与躲避。它以 BSD 3-Clause 许可分发，版权声明与许可全文见 [ThirdPartyNotices/BossModReborn.LICENSE](ThirdPartyNotices/BossModReborn.LICENSE)，分发插件时须保留该文件。
- `Spatial/` 安全区来源与许可见 [Spatial/README.md](Spatial/README.md) 和 [ThirdPartyNotices/PromeRotation.LICENSE](ThirdPartyNotices/PromeRotation.LICENSE)。

## 文档索引

| 文档 | 内容 |
|---|---|
| [AGENTS.md](AGENTS.md) | 架构、模块契约、开发纪律（AI 代理先读） |
| [ARR 解析与模拟流程](docs/ARR解析与模拟流程.md) | 录像解析、机制分析、夹具与离线模拟、实机验收闭环 |
| [脚本开发指南](docs/脚本开发指南.md) | 脚本 API、事件、三门面 |
| [可视化脚本编辑器](docs/可视化脚本编辑器.md) | `*.mechanic.json` 规则编辑 |
| [录制回放格式](docs/录制回放格式.md) | `.mpreplay.jsonl` 格式 |
| [IPC 接口](docs/PromeRadarIPC.md) | 外部插件调用 |
| [开发记录](docs/开发记录.md) | 按日期的修复与验证记录 |
| [docs/reference/](docs/reference/) | 各区域机制规格（斗兽、糖豆人、地城、运行库、宿主桥） |
| [Spatial/README.md](Spatial/README.md) | 安全区与解算器 |
