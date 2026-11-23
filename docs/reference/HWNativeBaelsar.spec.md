# 巴埃萨长城 MP 原生脚本

客户端表核对：CFC 219 → Territory 1114。入口为 `Presets/Dungeons/Entries/BaelsarsWall.cs`，GUID `masterplayer.dungeon.baelsars-wall`。接管 `1938/193A/193D` 三个 Boss OID，按区域固定排除对应 BMR 模块。脚本状态、绘制及规划由 MP 实现，不实例化运行库模块。

## 机制对照

来源为 `MechanicRuntime/Upstream/Modules/Heavensward/Dungeon/D17BaelsarsWall/` 三份模块及其激活组件。两处方场半宽 19.5；狮鹫场地按源码精确多边形导出，雷达和世界转换逐点比对。

| Boss | 原生行为 |
|---|---|
| Magitek Predator | 7347 长 42.94、宽 6 射线；7346 死刑；562 点名按原始事件时间 +5 秒预测半血伤害，7348 命中解除，同一状态快照不重建预测；增援 1939 优先攻击 |
| Armored Weapon | 7356 全屏、31469/7355 半径 5、193B 的 7360 长 40.94、宽 2；1132 极度警戒按状态到期前 3 秒提示、前 1 秒停止动作和移动，实际移除释放；刷新到期、缺失到期、死亡及切人均单独处理 |
| The Griffin | 7364 背对视线通过 PR 面向、7365 半圆、7362 死刑、7363 全屏；九把及以上全活剑时抑制 7372，任一死亡后恢复剩余剑的半径 9 危险；中心剑按 X/Z 各 ±5 的方形判定优先级 |
| 项圈 | icon1、3421 和项圈自己的 7369 读条共同维护拘束；7368 视觉读条结束不提前清除；193E 攻击范围为 hitbox+2.6，范围之外登记强制危险补集，真实解算器进入救援距离 |
| Big Boot | icon22 只对当前观看角色输出径向 15 码击退，忽略抗击退、止于场地墙；自然读条结束保留至 7367 实际效果，取消清理；显示轨迹和落点，并检查同时危险 |

1132 使用 `Hints.SetActionRestrictions` 联动 PR ACR 停手、禁读条、取消当前读条和停止原生自动攻击，使用 `Hints.SetMovementHold` 暂停 MP 执行；绘制和规划继续。回放禁止发布 PR 控制。普通危险只外扩一次，预览保留原尺寸。

上游状态机未激活 `Lionshead` 组件，因此未将 7370 视觉动作擅自扩成额外伤害机制。

## 验证

- `BaelsarRegression` 与真实 `ScriptHintsApi` 控制回归共 **20 项通过**，涵盖双视图几何、时钟、余量、真实救援解算、状态刷新、取消、死亡、视角和停止。
- 主项目 CI 构建成功。
- **49 个内置入口独立编译通过**，实际 DLL 原生地城合成回放通过。长城覆盖三 Boss、点名原时间、极度警戒窗口、九剑、击退、项圈/视线组合、Seek 重建、回放执行隔离和停止清理。
- 日志：`.tmp/halatali-verify/baelsar-regression.log`、`baelsar-build.log`、`baelsar-native-smoke.log`。

合成回放与源码对照不能替代录像延迟校准和游戏内验收；本批尚无实机绘制、PR 执行及闭环走位验收。
