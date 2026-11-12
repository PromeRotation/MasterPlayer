# PromeRadar IPC v1

所有接口通过 Dalamud `PluginInterface.GetIpcSubscriber` 调用。PromeRadar 是 PR 的卫星插件，通道只有启用 PromeRadar 后可用，调用方应处理 IPC 不可用异常。

| 通道 | 类型 | 含义 |
|---|---|---|
| `PromeRadar.GetApiVersion` | `Func<int>` | 当前为 1 |
| `PromeRadar.IsReady` | `Func<bool>` | 插件已初始化且未卸载 |
| `PromeRadar.GetSmartMovementEnabled` | `Func<bool>` | 用户选择的智能走位开关 |
| `PromeRadar.SetSmartMovementEnabled` | `Action<bool>` | 请求设置开关并保存配置 |
| `PromeRadar.SmartMovementChanged` | `Action<bool>` 通知 | 首次初始化及开关变化时发布 |
| `PromeRadar.GetState` | `Func<string>` | 最近一次主线程采样的 JSON 状态 |
| `PromeRadar.StateChanged` | `Action<string>` 通知 | 状态变化与卸载时发布 JSON |

设置接口可从任意线程调用，请求按顺序在下一次 Framework 更新执行。调用返回表示请求已入队，使用查询或变更通知确认应用。通知在游戏主线程同步执行，订阅者应快速返回，不抛异常；卸载调用方时退订相同委托。

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
| `CanMove` | bool | 当前允许执行智能走位：开关开启、玩家存活、非回放、本地解算和自动移动有效、PR 本体未占用移动 |
| `HasMovementControl` | bool | 本插件的安全区当前持有移动控制权 |
| `MovementPhase` | string | 当前调度阶段的中文说明 |
| `ReplayActive` | bool | 正在回放 |
| `TerritoryId` | uint | 实时区域 ID |
| `MechanicsEnabled` | bool | 机制库总开关 |
| `RunningScriptCount` | int | 运行中的脚本数量 |
| `ActiveModule` | string/null | 当前激活且未被禁用/压制的内置模块 |

`CanMove` 表示执行资格，不代表此刻需要移动；`HasMovementControl` 表示实际接管。回放中即使 `SmartMovementEnabled=true`，也不会移动。卸载前发送保留原字段的状态，将 `Ready`、`CanMove`、`HasMovementControl` 设为 false、`MovementPhase` 设为“已卸载”；调用方应优先检查 `Ready`。卸载不改变用户保存的开关。

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
