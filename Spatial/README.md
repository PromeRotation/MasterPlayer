# 插件内安全区

2026-09-21 从 PromeRotation 迁入完整 `Spatial/SafeZoneReborn`，并复制其依赖的 `Spatial/SafeZone` 数据与注册实现、`Spatial/Geometry`、`Helpers/GeometryHelper.cs` 和 `Helpers/MathHelper.cs`。旧版 SafeZone 的求解器和移动系统不在本插件中使用。

来源仓库：https://github.com/PromeRotation/PromeRotation-1.0

基准提交：`40bfed9049b6378c2d62ce3e724a91f767456915`，包含本地发布延迟修复 `bbb33a6`（上游 [PR #80](https://github.com/PromeRotation/PromeRotation-1.0/pull/80)）。保留原项目 GNU AGPL v3 许可，全文见 [PromeRotation.LICENSE](../ThirdPartyNotices/PromeRotation.LICENSE)。本目录的迁移修改包括命名空间、宿主输入采样、插件生命周期和取消结果检查。

`SafeZoneRuntime` 独立持有设置、注册表、区域管理器和异步解算/调度实例。所有消费者使用 `MasterPlayerPlugin.Instance.SafeZone`；几何、区域句柄、时间窗和解算结果也使用本地类型，不能与宿主同名类型混用。

插件每帧按事件及脚本更新 → 跟随目标更新 → 本地解算/下发 → 绘制的顺序运行。后台任务只读取不可变快照；主线程接收后按最新区域校验路线。切区、死亡、战斗结束清空本地区域和路线；已取消的完成结果也不得重新接收。死亡期间不解算、不下发。回放进入/退出清空旧会话；回放中保留预览和解算数据，但禁止自动、跟随及脚本移动，实时通用读条也不写入回放区域。

宿主边界：

- `PromeEventBus` 提供事件，`Svc` 提供游戏状态，PR 移速服务提供标量输入。
- `HostMovement` 读取 PR 旧版/Reborn 的自动移动开关和控制权，避免同时指挥 GreenMove；仅用户点击面板按钮时关闭这两套自动移动。
- `IGreenMoveSystem` 执行最终路线。新路线立即提交，后续逐帧推进由宿主负责，不依赖修改 PR 的回调订阅顺序。GreenMove 本身没有通用 owner token，控制权让位只覆盖可观察的安全区状态。
- 雷达直接使用本地几何。世界绘制经 `Radar/HostDrawingGeometry` 转成宿主具体几何类型后调用 DrawManager；保留月环孔洞、非对称矩形和多边形顶点。

安全区修复只需重新构建并重载 MasterPlayer。外部源码脚本应改用 `MasterPlayer.Spatial.SafeZone`、`MasterPlayer.Spatial.Geometry`，外部 DLL 应重新编译；绘制样式/绑定仍用 `PromeRotation.Spatial.Drawing`，移动仍用 `PromeRotation.GreenMoveSystem`。

验证入口：`dotnet run --project tests/MasterPlayer.RegressionTests.csproj`。定向生命周期与几何检查可加 `-- --local-safezone`。
