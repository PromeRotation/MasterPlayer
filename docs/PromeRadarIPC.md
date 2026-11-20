# PromeRadar IPC v1

所有接口通过 Dalamud `PluginInterface.GetIpcSubscriber` 调用。PromeRadar 是 PR 的卫星插件，通道只有启用 PromeRadar 后可用，调用方应处理 IPC 不可用异常。

| 通道 | 类型 | 含义 |
|---|---|---|
| `PromeRadar.GetApiVersion` | `Func<int>` | 当前为 1 |
| `PromeRadar.IsReady` | `Func<bool>` | 插件已初始化且未卸载 |
| `PromeRadar.GetSmartMovementEnabled` | `Func<bool>` | 用户选择的智能走位开关 |
| `PromeRadar.SetSmartMovementEnabled` | `Action<bool>` | 请求设置开关并保存配置 |
| `PromeRadar.SetFsdSessionActive` | `Action<bool>` | NewFolder3 获取/释放整轮 FSD 会话；获取时启用智能走位和 PT30 内置脚本 |
| `PromeRadar.SmartMovementChanged` | `Action<bool>` 通知 | 首次初始化及开关变化时发布 |
| `PromeRadar.GetState` | `Func<string>` | 最近一次主线程采样的 JSON 状态 |
| `PromeRadar.IsMechanicMovementActive` | `Func<bool>` | 实时机制移动占用，包含等待撤离及安全点等待，不受 JSON 发布节流影响 |
| `PromeRadar.StateChanged` | `Action<string>` 通知 | 状态变化与卸载时发布 JSON |

设置接口可从任意线程调用，在下一次 Framework 更新执行。调用返回表示请求已入队，使用查询或变更通知确认应用。会话操作先于普通开关操作，普通开关代表用户选择，取消会话对移动开关的恢复权限。会话获取/释放幂等；整轮运行（含普通楼层、Boss、失败重进）保持开启，释放时只恢复本会话临时开启的移动/脚本开关。通知在游戏主线程同步执行，订阅者应快速返回，不抛异常；卸载调用方时退订相同委托。

```csharp
var enabled = pluginInterface.GetIpcSubscriber<bool>("PromeRadar.GetSmartMovementEnabled").InvokeFunc();
pluginInterface.GetIpcSubscriber<bool, object>("PromeRadar.SetSmartMovementEnabled").InvokeAction(false);

var channel = pluginInterface.GetIpcSubscriber<bool, object>("PromeRadar.SmartMovementChanged");
Action<bool> changed = value => { /* 更新本插件状态 */ };
channel.Subscribe(changed);
// 调用方卸载时：channel.Unsubscribe(changed);
```

JSON 字段：

| 字段 | 类型 | 含义 |
|---|---|---|
| `ApiVersion` | int | 状态格式版本，当前为 1 |
| `Ready` | bool | 插件就绪 |
| `SmartMovementEnabled` | bool | 持久化的智能走位开关 |
| `CapturedAtUtc` | UTC timestamp | 最多每 100ms 发布一次缓存，消费者检查区域和三秒新鲜度 |
| `FsdSessionActive` | bool | MP 已在主线程接受 FSD 会话 |
| `Pt30ScriptAvailable` / `Pt30ScriptReady` | bool | 内置脚本存在 / 正在接管 Boss 且场地输出就绪 |
| `Pt30ScriptVersion` / `Pt30State` / `Pt30ChaseRemaining` | string / string? / int | PT30 契约版本、机制状态、追踪剩余次数 |
| `Pt30PreviewCount` | int | 实际注册的 PT30 危险区预览数；未来光球预测只参与导航，勿与 `DangerZoneCount` 混为绘制数量 |
| `Pt99TargetControlActive` | bool | 99 层 / 卓异的悲寂歼灭战共用脚本已接管选敌：实时交战、当前玩家存活且在场内；不依赖智能走位开关 |
| `Pt99TargetId` | ulong | 接管期间选定的对象 ID（优先 EntityId），0 为明确停火；未接管时为 0 |
| `ScriptError` | string? | 脚本框架错误；脚本未启动时不要只检查机制库错误 |
| `FrameMilliseconds` | double | 本帧 Framework 间隔，供性能实测关联，不等于 MP 自身 CPU 耗时 |
| `RecordingActive` / `RecordingPath` | bool / string? | MP 自身事件录制状态；与 ARR 分开 |
| `DangerZoneCount` / `ObstacleCount` / `GoalZoneCount` | int | PT30 脚本活动时报告其输出数，否则报告机制库导航桥数量 |
| `CanMove` | bool | 当前允许执行智能走位：开关开启、玩家存活、非回放、本地解算和自动移动有效、PR 本体未占用移动 |
| `HasMovementControl` | bool | 本插件的安全区当前持有移动控制权 |
| `MechanicMovementActive` | bool | MP 正在独占机制移动；实际未移动的等待阶段仍可为 true |
| `MovementPhase` | string | 当前调度阶段的中文说明 |
| `ReplayActive` | bool | 正在回放 |
| `TerritoryId` | uint | 实时区域 ID |
| `MechanicsEnabled` | bool | 机制库总开关 |
| `RunningScriptCount` | int | 运行中的脚本数量 |
| `ActiveModule` | string/null | 当前激活且未被禁用/压制的内置模块 |
| `BuildMvid` | string | MP 构建标识，用于确认实际加载版本 |
| `MechanicRuntimeBuildMvid` | string | 实际加载的机制运行库构建标识，单独更新适配或模块时也须核对 |
| `NavigationActive` / `CaptureActive` | bool | 自有脚本或模块导航输出就绪 / 效果包捕获可用；与执行资格、实际移动控制权分别判断 |
| `MechanicError` | string/null | 机制宿主最近错误 |
| `ChaseStatus` | string/null | PT30 自有脚本状态，包含 Divine Favor 连续走位方向、剩余攻击数和目标 |

`CanMove` 表示执行资格，不代表此刻需要移动；`HasMovementControl` 表示实际接管。回放中即使 `SmartMovementEnabled=true`，也不会移动。卸载前发送保留原字段的状态，将 `Ready`、`CanMove`、`HasMovementControl` 设为 false、`MovementPhase` 设为“已卸载”；调用方应优先检查 `Ready`。卸载不改变用户保存的开关。

解算诊断附加字段：`SolveStatus`（成功或明确失败原因）、`SolveTimeUtc`（快照时钟）、`SolutionTemporal/Partial/Fallback`（时间路径/分段逃离/最少危险降级，未解算时为 null）、`PathWaypointCount`、`PlannedWaitUntilUtc`（当前阶段首个等待点的解禁时间）、`EscapeDeadlineUtc`（读条让位的逃离截止）。这些字段与 `HasMovementControl`、`EscapeWaitBlock` 联合检查，可区分正常等待、无路、等待读条和执行器未接管；`SolveStatus=Success` 本身不能证明实际执行或最终无伤。

NewFolder3 入场前通过 `SetFsdSessionActive(true)` 获取会话，任务结束以 `false` 释放；死亡重开属于同一会话。移动交接独立于该会话：NewFolder3 注册 `NewFolder3.Movement.SetPromeRadarControl`（`Func<bool, bool>`，仅游戏主线程）。MP 在下发路线前设置实时占用并同步调用 `true`；NewFolder3 先关闭自身输出权限、取消自己的路线、暂停原任务后返回 `true`。交接失败时 MP 不下发路线。等待撤离、后台解算以及逃出后相关危险窗口尚未结束时仍保持占用；远处危险及普通攻击目标不单独延长占用。MP 停止自己的执行器后调用 `false`，FSD 从当前位置重新验证并继续同一个开箱尝试，暂停时间不消耗开箱努力或等待预算。关闭、死亡、切区、回放及卸载撤销占用；重载时通过实时标量重新对齐。FSD 会话要求两端同时更新，不能使用缓存 `HasMovementControl=false` 作为恢复依据。

PT30 检查同区域、三秒内采样、非回放、`FsdSessionActive/SmartMovementEnabled/CanMove/NavigationActive/Pt30ScriptReady=true` 和 `Pt30ScriptVersion="1.0.0"`，由 MP 自有脚本处理；该 Boss 的 BMR 绘制和导航已停止。用户中途关闭时不每帧重开。会话调用请求入队不代表已经应用，需查询确认。

PT99 选敌消费者使用更短的 500ms 新鲜度，检查 `Ready/ApiVersion`、区域 1290、非回放及 `Pt99TargetControlActive=true`。`Pt99TargetId=0` 或指定目标死亡、不可选、HP≤1 时必须停火，不得回退通用选敌。NewFolder3 在攻击及导航共用的选敌链首位采用此指令，最多每 100ms 读取一次；没有有效接管指令时才使用原有机制/默认 provider。该契约只选目标，不下发攻击技能；两端须同时更新。

普通「卓异的悲寂歼灭战」区域 1333 复用同一脚本，也发布上述 `Pt99Target*` 字段，字段名为兼容现有消费者保留。MP 自身直接执行选敌；NewFolder3 的深宫 provider 仍只消费区域 1290，不将普通歼灭战当作深宫 Boss 楼层。深想战区域 1311 不由本脚本接管。

脚本开发：使用 `SafeZone` 注册规划约束、`Move` 执行手动移动、`Hints` 请求面向，均遵守智能走位开关。持有额外原生控制的脚本须覆盖 `OnMovementDisabled()` 立即撤销自身控制，保留机制和绘制状态。此机制不对任意外部脚本代码做沙箱隔离。

## XSZToolbox 遥控客户端

设置面板的「遥控」页通过 Dalamud IPC 消费 XSZToolbox，不需要引用或打包 XSZToolbox.dll。安装并启用工具箱的遥控模块后，可查看房间和成员、发送控制、创建或刷新附近玩家虚拟房间。页面仅在 `PromeRotation.Core.Core.MyPrLevel() >= 3` 时显示；每帧重新判断，掉级时退回总览并清除页面房间快照。客户端每次调用也检查等级，卸载后的旧实例不可再使用。

`MasterPlayerPlugin.Instance?.RemoteControl` 提供 `MasterPlayer.Integration.RemoteControlClient`，供主线程脚本复用。查询与控制均为同步 IPC；只能在主线程调用。状态刷新失败写入 `StatusError`；职能查询与控制结果通过返回值、`LastOperation`、`LastOperationSucceeded` 报告。调用完成只代表工具箱接口已接受调用，不保证远端已执行；旧 Action 接口没有执行回执。页面打开时每秒刷新一次状态，不轮询隐藏页面。

消费接口（未列出的通道不在本次集成范围内）：

| 通道，默认前缀 `XSZToolbox.RemoteControl.` | 类型与用途 |
|---|---|
| `GetRoomId` / `IsConnected` / `GetMemberCount` / `GetOnlineMemberCount` | 无参查询：`string?` / `bool` / `int` / `int` |
| `GetRoomSnapshotJson` | `Func<string>`：房间与成员快照，无房间为空字符串 |
| `CreateVirtualRoomFromParty` | `Func<string, string>`：附近玩家虚拟房间名称 → 快照 |
| `RefreshVirtualRoomFromParty` | `Func<string>`：刷新附近玩家快照 |
| `SetPos` / `MoveTo` | `Action<string, Vector3>`：职能选择器、位置 |
| `LockPos` | `Action<string, Vector3, int>`：位置锁定毫秒 |
| `SlideTp` / `MoveManaged` / `SetPosManaged` / `SlideMoveDelay` / `SlideMoveTimeout` | `Action<string, Vector3, long>`：滑动时长 / 相对当前战斗时间的最晚延迟 / 绝对战斗时间，单位毫秒；Managed ≤ 0 不设截止时间 |
| `SetMoveAssemble` / `SetMoveAssembleDelay` | `Action<string, string, Vector3>` 模式 `None/Target/Position` / `Action<string, int>` 补偿毫秒 |
| `SetRot` | `Action<string, float, int>`：弧度、锁定毫秒；页面以度输入后转换 |
| `MoveStop` / `Kick` | `Action<string>` |
| `Stop` / `Jump` | `Action<string, bool>`；`Stop(false)` 不恢复操作，`Jump(false)` 不取消跳跃，二者均为空操作 |
| `UseSkill` / `SetTarget` | `Action<string, uint>` |
| `UseSkillWithTarget` | `Action<string, uint, uint>`：技能、目标实体 ID |
| `Echo` / `Cmd` / `SetRole` | `Action<string, string>`：消息 / 聊天命令 / 新职能 |
| `GetRoleByPlayerName` / `GetRoleByPlayerCID` / `GetNamebyRole` | `Func<string, string?>`；通道大小写按工具箱定义 |

旧工具箱缺少房间快照时仍可使用基础状态与控制；页面明确提示升级后启用成员查看和虚拟房间管理。工具箱卸载、接口异常或快照损坏时清除旧状态并显示错误，可在工具箱重载后直接刷新；远程传输断线时如工具箱仍保留房间，则按工具箱快照展示离线状态。

远程房间创建、加入、关闭及 PR 专用控制留在 XSZToolbox 原界面：当前工具箱 IPC 在主线程读取游戏对象后同步等待网络，调用者不能安全移到后台线程，也无法避免主线程等待网络。旧控制 Action 接口由工具箱异步发送，虚拟房间创建与刷新在本地同步完成，不存在这项网络等待。成员退出也没有公开 IPC，`CloseRoom` 仅允许房主关闭房间。暂不消费 TreasureRuntime、通用插件桥接和实际只在本机执行的绕圈/连跳接口。

选择器按 `jobRole`（MT/ST/H1/H2/D1…）匹配正则；快照的 `role` 是 `Host/Leader/Member` 房间权限。`.*` 代表全部已分配职能的成员，不包含无职能成员。客户端拒绝空选择器，避免工具箱把空值解释为广播；页面点选成员会生成锚定且转义的职能表达式，同职能多人仍会一起收到指令。

移动、传送、集合、朝向和发起跳跃受智能走位总开关控制；停止移动、停止操作不受此开关限制。回放期间全部遥控控制指令禁发。遥控是手动发送到工具箱的指令，关总开关阻止后续移动调用；已发送到远端的锁定或延后动作由工具箱持有，关闭智能走位、停止移动或停止操作不能保证撤销这些任务，应按工具箱各动作的持续时间与实际取消能力处理。客户端卸载不关闭工具箱房间或停止其他来源控制。

定向回归：`dotnet run --project tests/RemoteControl/MasterPlayer.RemoteControl.Tests.csproj -p:CI=true`。
