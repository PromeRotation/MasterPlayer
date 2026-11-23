# ARR 后续原生地城

本规格只覆盖仓库实际存在的 D12–D17、D26、D27、D29 九个目录。生产入口位于 `Presets/Dungeons/Entries/`，继承 `DungeonScript`；事件、状态、危险/目标注册和预览均由 MP 原生实现，不创建 BMR 会话。按 Territory 与 Boss OID 固定禁用对应桥，脚本关闭也不恢复桥；其他未接管对象保留原有路由。

| 来源目录 | Territory | 原生机制与控制 |
|---|---:|---|
| D12AurumVale | 1331 | 三层减益选择清洁果实，移动至交互范围并联动停手/禁读条；果实、角色死亡和净化后清理；Coincounter 的扇形/月环扇区保留包朝向 |
| D13CastrumMeridianum | 1043 | 精确场地、六辅助体上限及晋升；Livia 中圈结束才显示与规划侧圈，保留辅助体各自判定时间 |
| D14Praetorium | 1044 | Nero 地图事件撤环并缩场；击退落点、分身出生朝向锁定、Terminus Est 效果清理及狂暴倒计时 |
| D15WanderersPalace | 159 | Inhale 与延迟 Grief 同时预测；读条自然结束保留后续，效果/取消分别清理；风筝点名、选敌优先级 |
| D16Amdapor | 167 | 灵魂术士两阶段与小怪优先级；Anantaboga 视线按真实完整柱体计算影区并排除柱洞，以补集登记导航危险 |
| D17PharosSirius | 160 | 四个模块；晶体三阶段成长、状态 Param 三层后 2.5 秒爆炸与效果锁，鸟蛋精确禁攻，海妖分摊/月环及治疗驱散提示 |
| D26Snowcloak | 1062 | Northern Bateleur 道中风墙与地图边界；Wandil 雪飘判定前 0.75 秒持续走位并禁读条；Yeti 钢铁/月环与冰柱；Fenrir 仅完整冰柱遮挡，读条后 4.5 秒碎裂 |
| D27SastashaHard | 387 | 源码仅 Karlabos、Captain Madison；点名与可驱散状态、真实 Rive 范围、小怪读条；实时净化通过 PR CanCast/UseAction，0.5 秒节流，回放禁执行 |
| D29KeeperOfTheLake | 1063 | 三个首领；喷火六个不同效果包，Antipathy 三段保留 +2 秒预测，Mirage 时间线 +12 秒，Akh Morn 四击分摊；多目标重复行不推进次数 |

危险形状显示原尺寸，解算应用一次 `DodgeDangerMargin`。导航句柄不再重复绘制；显示分组和过滤不改变危险登记。面向视线使用 PR owner 租约，禁攻/停手使用 `Hints` 汇总至 PR ACR，关闭智能移动与 ARR/MP 回放禁止实际控制。

## 验证与限制

对应定向测试为 `AurumValeRegression`（15）、`CastrumMeridianumRegression`（19）、`PraetoriumRegression`（18）、`WanderersPalaceRegression`（7）、`AmdaporRegression`（16）、`PharosSiriusRegression`（16）、`SnowcloakRegression`（17）、`SastashaHardRegression`（6）、`KeeperRegression`（18）。使用生产状态、注册表和世界几何转换检查边界、取消、角色切换及各连续阶段；关键导航使用真实时序解算器。

`tests/ScriptCompilation/NativeDungeonReplayRegression.cs` 从编译后的真实 DLL 创建脚本，合成世界帧和事件，检查首领/区域发现、两视图共用预览、状态参数、对象动画与消失、效果清理、Seek 重建及退出释放。`clock-targeted.log`、`clock-smoke.log` 和后续 `vault-native-smoke.log` 保留离线证据。合成回放不等于完整战斗录像校准，更不证明 PR ACR、面向、移动及世界绘制在游戏内实际执行通过。
