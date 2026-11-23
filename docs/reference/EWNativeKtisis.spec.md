# 创造环境极北造物院 MP 原生迁移

2026-11-22。本机匹配版本客户端表：CFC 787 → Territory 974；入口 `Presets/Dungeons/Entries/KtisisHyperboreia.cs`，GUID `masterplayer.dungeon.ktisis`。接管 OID 3323、3425、348A；生产通过 `DungeonEncounter` 和 MP SafeZone/Draw/Hints 输出，不创建 BossModule 或机制会话。

## 机制与来源

来源为 `MechanicRuntime/Upstream/Modules/Endwalker/Dungeon/D04KtisisHyperboreia/` 三个 Boss 文件。

- Lyssa：精确 32 边场地及入口差集；25179 R4，25375 按源实际长度 40、宽 4。HeavySmash 开始重定时当前冰柱，保留重定时至冰柱自身结束。六种 map 位置和角度按源量化，首轮原事件 +13 秒、后续 +16 秒，25177 结束消费。25180 R6 分摊保留结算硬约束；支援 NPC 队伍使用固定点 (-139,47) R5，撤销通用跟随点名目标，避免冲突。
- Ladon：39×39 场地；2812/2813/2814 按获取顺序保存，快照按状态槽恢复且尊重近期移除事件。六种双头顺序生成 R40/120° 首段与 +2.1 秒第二段；首段结束消费一项，25737–39 真实效果清队列。十字按源长 50、宽 4；分摊与死刑预测迁入 MP。
- Hermes：精确 64 边 21.5→20 场地，25886 结束 +0.5 秒的 20–22 环保持至 map 8/20001。25891 使用源实际 R10；27837 按稳定到达次序仅发布前四条并递补。27836 选择首个完好陨石，生成宽为 hitbox 直径、起点 +0.2 到 +50 的矩形遮挡；显示安全区并强制进入补集安全处。此机制源配置为矩形遮挡，不使用扇影；陨石不额外设障碍。三个破损陨石且无爆炸读条时靠近最后完好陨石。
- 25887 按原事件 +5.7 秒动态引导，25900 真实效果清；218 点名 +5.1 秒 R4 龙卷死刑，25905 消费、死亡目标清。来源和目标移动、切视角均重新生成个人约束。
- 同一次施法先扫描恢复、后权威 packet 修正位置/朝向时，同步修正派生吐息和陨石矩形，保留原锁定时钟且不重复新增阶段。
- 25907 打断按坦克 7538／物远 7551、3／25 码加双方 hitbox 选择；实时执行前复核实际读条、可打断、职业与 CanCast，受 MP 移动总开关和回放门控。

三源没有护盾禁攻、面向禁向或停手机制，不按名称增加这些行为。雷达与世界预览使用相同 MP 几何；显示几何与结算导航约束分别注册。

## 验证

在 `.tmp/halatali-verify` 使用 SDK 10.0.303：

```powershell
dotnet run --project ../../tests/MasterPlayer.RegressionTests.csproj -p:CI=true -- --filter=Ktisis
dotnet build ../../MasterPlayer.csproj -p:CI=true
dotnet run --project ../../tests/ScriptCompilation/MasterPlayer.ScriptSmoke.csproj -p:CI=true -- ../.. ../../bin/Debug/MasterPlayer.dll --native-dungeons
```

定向回归 35 项通过，包含精确场地、两视图几何采样、六方向时钟、六双头顺序、支援队伍与普通分摊真实解算、矩形遮挡真实解算、风线前四与递补、引导真实效果清理、打断选择，以及状态滞后和 packet 修正。

实际 DLL 合成回放覆盖三 Boss 的地图事件、冰柱重定时、支援队伍集合、双头推进、完整陨石选择、引导、缩场、打断规划、Seek 与 Stop。日志：`.tmp/halatali-verify/ktisis-regression.log`、`ktisis-build.log`、`ktisis-native-smoke.log`。

离线验证不等同于 ARR 实录校准、最终游戏内像素或实机 PR 打断执行；这些仍待游戏内验收。
