# 哈利卡纳苏斯普通：MP 原生机制合同

日期：2026-11-25。源为 `MechanicRuntime/Upstream/Modules/Stormblood/Raid/O3NHalicarnassus/`；生产实现位于 `Presets/Raids/Halicarnassus/`，入口 `Presets/Dungeons/Entries/HalicarnassusNormal.cs`，GUID `masterplayer.raid.halicarnassus.normal`。生产不创建 BossModule 或 MechanicSession。

## 元数据与接管

源 GroupID=CFC254，NameID5633，主 OID0x1E14；本机客户端表双向核对 CFC254↔T693，正式内容名为“欧米茄时空狭缝 德尔塔幻境3”，NPC名为“哈利卡纳苏斯”。核表证据位于 `.tmp/omega-normal-table-review/`；O1/O3合计40源AID均存在，额外读条均为0。本轮未证明 OID 与客户端 BNpcBase 的直接关联。

默认启用、显式关闭经刷新保留，仅精确 T693/OID1E14 接管旧桥。不将枚举中的 Holy、PlayingField、PanelSwap 或 Dragon 等未激活机制补造出来。

## 八个实际激活组件

| 激活顺序 | Action | 语义 |
|---|---:|---|
| Ribbit | 9318 | R19.5、总角150° |
| SpellbladeThunderIII | 9314 | 前长60、半宽3矩形 |
| SpellbladeBlizzardIII | 9313 | R9 |
| SpellbladeFireIII | 9312 | 内4、外15圆环 |
| CrossReaper | 9323 | R10 |
| TheQueensWaltz1 | 9327 | R34、总角20° |
| TheQueensWaltz2 | 9329 | 12格绝对棋盘 |
| TheGame | 9325 | 16格职业棋盘 |

六普通AOE各自按原组件顺序输出，Start锁定原 cast.Location/Rotation，使用 float NPCRemainingTime 后一次转换DateTime。真实 watched raw Finish 按来源ID只删除首项；普通 Effect、取消包和预测到期都不清除。

Waltz 按 z外/x内遍历 `[-15,-5,5,15]²`，只追加 `x != z` 的12个10×10格；每次Start追加，默认朝向和default判定钟，映射即时持久危险。真实 NPC Native Spell Effect9329清全部Waltz，Finish、9330/9331等不清。Game同样追加16格，但始终只查询前16项，真实Effect9325清全部Game。

Game安全格由当前观看原Actor.Class的真实Role动态决定，不能读取PartyRole猜测：Tank索引6、Healer索引9、Melee/Ranged索引5和10，其余职业无安全格；包括BLU36与BST43。棋盘是危险导航，不是安全goal或未来读条完成点。

## 生命周期与公共消费

保留源默认CheckPull、CheckReset=false、单Trivial死亡阶段。脱战及观看者死亡不重置全局棋盘。Party[0]缺Actor时冻结模块Update；500码按三维距离，Pending逆序晋升先于旧模块退休。未激活超距Loaded退回Pending，同ID原主体重建即时重绑；事件扫描只同步输入，不能提前推进退休和晋升。

即时ActorAdded加载不筛Dead，Pending晋升先筛Dead；未开场Loaded的HP0/Dead不消费死亡阶段，Active之后才消费，Destroyed始终可退休。Director0005卸载Loaded并置团灭屏障，Pending可加载但0011前禁止激活；0006不能替代0011。上述两处复审缺陷及一次主线程追加的即时Dead边界均已加入反例。

缺观看Actor时保留全局历史，撤销个人输出；非Party仍可显示但不导航。源Party0缺失时保守撤导航。真实SafeZoneSolver使用40×40场地与即时危险求解；用户余量、双视图和owner经既有DungeonOutput统一适配。没有新增伤害预测、面向或特殊动作执行；ARR/MP回放执行隔离沿用统一门控。

## 本机验证与边界

命令从 `.tmp/halatali-verify/` 运行，采用该目录已有SDK10.0.303选择，未验证根SDK11。`FFXIV_SQPACK` 与 `PALACE_GAME_DATA` 均指向真实本机游戏sqpack，无缺数据跳过。

- `native-halicarnassus-20261125-model-03.log`：52相关场景PASS（5个O3、16个M4、24个新月岛、7个控制）。
- 最终 `model-05.log`：5个O3场景PASS，85真实源组件事件/职业边界、851550独立几何点精确差分；包含追加的死亡、团灭和即时加载反例，退出0。
- 最终 `build-03.log`：0错误、4条既有Crucible警告，退出0。
- `replay-01.log`：14条PASS/0FAIL，退出0；为build-02中间DLL专项，不作为最终即时Dead修改证据。
- 最终同DLL无筛选 `catalog-02.log`：1408条PASS/0FAIL，其中291个入口逐文件独立编译，退出0；包含全部O3专项、真实即时棋盘逃离、逆Seek、原生Effect过滤、BattleEnded、Director屏障、机制关闭/恢复及owner清理。
- 最终 `MasterPlayer.dll` SHA256：`2ed7238969a2853a778b173a6a81c5c1deb980b081f4c0546aed7b4b6981fb17`。

所有日志位于 `.tmp/halatali-verify/`。`model-01`的缺using编译失败及中间build/catalog均保留；未以放宽容差或减少断言处理失败。备份位于 `.tmp/omega-normal-20261125-backup/`。源码审查跨上下文，未补造完整审查耗时；`phase-times.txt`仅保存实际记录的时间点。

本轮仅离线验证，不部署、不修改PR。真实游戏事件采集、最终像素与实际宿主执行仍待实机。
