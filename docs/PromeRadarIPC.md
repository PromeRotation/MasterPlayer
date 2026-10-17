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
