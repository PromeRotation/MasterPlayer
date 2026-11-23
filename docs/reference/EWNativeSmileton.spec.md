# 笑笑镇 MP 原生迁移

2026-11-22。本机客户端表 CFC 794 → Territory 976「乐园都市笑笑镇」。入口 `Smileton.cs`，GUID `masterplayer.dungeon.smileton`；按 OID 34CF/34D1/34D3 隔离。来源 `MechanicRuntime/Upstream/Modules/Endwalker/Dungeon/D07Smileton/` 三文件，全部标记 WIP。

## 实际迁移范围

- Face：40×40 方场，27742 按锁定朝向定向击退 8 码，按来源计算遇墙停止与角色 hitbox 余量，多个来源按到达顺序连续投影；免疫判定使用状态到期时间，真实结束撤销。源只提供击退投影，没有因此额外制造危险区。分散 R5 与全屏伤害预测迁入 MP。
- Frameworker：R20 圆场，Boss 与打印体的 R15 跳跃独立维护，40 码、总角 45° 扇形按来源读条清理，支持同一打印体下一轮重用；全屏伤害预测迁入 MP。
- Big Cheese：29×20 矩形场，R16 爆弹、30×10 左右直线、R8 Excavated 按读条维护。电弧按实际组件取 R6（源注释写 R8），四人分摊提供硬约束，首目标取队友 R3；R6 分散与按读条结束的单体死刑迁入 MP。
- 三场均没有源中已实现的护盾禁攻、背对视线或停手机制，未虚构状态触发器。

## WIP 缺口

源 Face 没有实现 FrownyFace 26422、SmileyFace 26423、MixedFeelings 26424 的颜色/状态线路。本次没有把这些机制称为完成，也不将这些 Action 加入通用躲避压制。该副本三个已有 BMR 模块的输出已原生化，不代表游戏全机制已补齐。

## 验证

`tests/SmiletonRegression.cs` 12 项通过：三场地/两视图、七种来源形状、16 朝向墙交点严格对照、连续击退/免疫/释放、四人分摊真实解算器、打印体独立结束及重用、伤害预测和未实现 Action 不压制。

实际 DLL `NativeDungeonReplaySmileton.cs` 三 Boss 合成回放通过，覆盖读条帧、独立阶段、状态免疫、原生绘制/导航、Seek 和停止。未部署；ARR 实录校准、游戏最终绘制和 PR 控制实机待验。
