# 正统优雷卡 20 层：克隆节点

适用 CFC 898、Boss OID 0x3E77。模块为 BMR DD20CloningNode，由 MP 机制宿主调用；无需外部脚本。

## ARR 证据

来源：2026-11-23 04:06:12 的 11–20 层 ARR，build 14251465。匿名夹具 `tests/Fixtures/EurekaOrthos/eo20-040612.json` 包含原文件 SHA-256 与 Boss 相对时钟；188 条指令、预告、施法、判定事件。实际空间判定为 55 次 FlameBreath (32864) 与 7 次 PiercingLaser (32547)。普攻 32817 不是站位可躲机制。

## 本地适配

- 自身施法使用施法者世界位置和锁定的 CastRotation，避免 native TargetLocation 为零造成偏移。
- 保留 BMR 外圈龙到内圈 helper 的位置推算（含西北 -315.5 偏移）。喷火预告按开场前两次进攻指令 9.6s、后续 8.5s、接力 11.4s 预测；同帧预告先于指令时补校正。
- 实际 helper 读条以真实位置、施法朝向、CastFinishAt 替换相应预测；丢失预告时也创建实际危险区。两波接力均交给时序规划器。
- 同来源去重；正常 native 读条结束到实际 effect 之间保留危险；effect 按来源清理。取消、死亡、销毁、判定后 0.6s 超时均回收。
- 原始绘制保持 50 码、30 度扇形与 40×5 码直线。扇形导航后移顶点获得 0.5 码侧边余量，且提前 0.2s 激活，以覆盖 ARR 中实际 effect 早于 cast+0.3 的抖动；不改公共解算器。

## 验证

设置 `FFXIV_SQPACK` 到实际 game/sqpack；使用仓库指定的 .NET SDK，或在仓库外调用绝对项目路径。

```powershell
dotnet run --project "<仓库路径>/tests/MasterPlayer.RegressionTests.csproj" -c Release -p:CI=true -- --filter=EO20 --keep-going
```

9 项定向测试。62/62 实际判定均有相应原点和时间的危险区。真实组件 → AIHints → MP 几何转换 → 注册/快照 → Solver → Dispatch → FakeMove 闭环；黑魔 2.8s 循环读条、6y/s，100/300ms worker 延迟，中心与四向起点按原始 BMR 点判定均为 0 次可躲命中，未出场地。effect 朝向已从原 ARR ActionEffectHeader.rotation (0x1a) 独立核对。没有把 ARR 原目标数/原角色移动当成成功证据。

保留并重跑 EO10 8 项及 temporal refuge lock 2 项。离线模拟不包括实时帧调度、网络丢包、宿主移动竞争，不能代替实机通关验收。
