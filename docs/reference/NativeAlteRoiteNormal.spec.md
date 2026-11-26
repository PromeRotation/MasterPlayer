# 普通老者（O1N）原生脚本

日期：2026-11-25。源目录为 `MechanicRuntime/Upstream/Modules/Stormblood/Raid/O1NAlteRoite/`。生产实现位于 `Presets/Raids/AlteRoite/`，入口 `Presets/Dungeons/Entries/AlteRoiteNormal.cs`，GUID `masterplayer.raid.alteroite.normal`。生产不实例化或消费 BMR 模块；源组件仅用于测试对照。

## 区域与实际激活

本机客户端表双向确认 Territory691 ↔ CFC252，正式内容名“欧米茄时空狭缝 德尔塔幻境1”；源主 OID `0x1A6F`、NameID5629“老者”，火球 `0x1A71`、龙卷风 `0x18D6`。表证据保留在 `.tmp/omega-normal-table-review/`，O1/O3 共40源动作均存在且 extra 为0。没有据此宣称 OID 与 BNpcBase 的直接关联已经证明。

状态机按以下顺序激活12组件：Roar、ThinIce、Charybdis、TwinBoltTetheredBuster、BreathwingKB、ClampAOE、ClampKB、DownburstKB、DownburstTornado、FireOrbsTimedFollowAOE、TwinBoltAOE、BlazeLevinStackSpread。

## 输入、生命周期与原引用

- 主线程扫描原生 NPC Spell cast、真实60状态槽及原队伍提交顺序。每个组件冷激活按世界 Actor 顺序逐个执行 Created→Cast→Status，不能改成先全部 Created 再全部 Cast。热回调保留 Player/Pet 等原过滤。
- 主怪 Targetable/InCombat 拉起；Party0 缺失冻结生命周期。保留500码三维加载、Pending 逆序晋升、即时加载与 Pending 的不同 Dead 过滤。Loaded 阶段 HP0/Dead 不等于已完成。
- Director0005 卸载并建立团灭屏障；Pending 可加载，0011 前不激活，0006 不解除该屏障。战斗结束保留历史，机制关闭/区域退出/owner 停止清理。
- AOE/图标保存原 Actor 引用和代际；同 ID 重建不会把旧目标缓存转绑到新对象。普通组件以 watched raw Finish 删除首项，没有统一 Effect、取消包或超时清理。
- `BossModule.Enemies` 是按 OID 首次查询建立、随后创建追加/销毁移除的原引用列表。火球和龙卷风各自保留独立列表；龙卷风即使主怪尚未读 Downburst，也先执行该查询。字典删除后复用空位不能改变输出次序。模块卸载淘汰缓存。

## 十二组件合同

| 组件 | 原生合同 |
|---|---|
| Roar9180 / Charybdis9179 | 全队存活掩码预测，时钟每次取原施法者当前 NPCRemainingTime；动态钟只作一次 DateTime 转换，保留浮点尾差 |
| TwinBolt9175 | 单体预测；自目标时动态回退施法者 TargetID。仅文字提示留意连线，不猜测实际连线目标或新增连线 |
| ClampAOE9186 | 长度为组件构造时 `9 + Primary.HitboxRadius`，全宽10；锁包落点、方向与判定钟 |
| TwinBoltAOE9176 | R5，独立普通 AOE 队列 |
| BreathwingKB9182 / ClampKB9186 / DownburstKB7896 | 三独立20码投影；前两者按锁定方向，后者径向；Clamp 使用自身矩形筛选。每类顺序连续投影，raw Finish 按来源 ID 删除首项。免疫严格 `ExpiresAt > Activation`。源没有 AddAIHints，投影不增加硬导航 |
| ThinIce911 | 6码，标准模式取角色朝向、legacy取 camera+π；按源 hitbox 与圆墙精确裁剪。override不调用基类，不能用普通击退免疫消除滑行。真实旧 Party 槽更新 mask |
| DownburstTornado | 仅看主怪 raw cast ID7896，使用缓存 OID18D6；未量化位置距中心≤2筛选，输出量化位置R5、Actor方向、default判定钟、Danger黄色。不筛 Dead/Targetable/Combat/EventState |
| FireOrbsTimedFollowAOE | R8，判定前5秒开始显示、随球量化位置移动，无预测超时删除；原主怪引用 Start 更新 driver；实际球9173/9188 Start锁NPC钟，Finish/Destroyed删除，死亡和Effect不删除 |
| BlazeLevinStackSpread | Source Actor图标62/108追加，忽略TargetId，5d延迟，重复保留，无冷补图标。分摊R6/min2/maxint，分散显示R6、导航R7。分散Effect9177，9178不消费；分摊9185独立计数，MainTarget首匹配，否则删首 |

火球首个 Created 清空先前 driver 和环锚点；间隔≥2秒为外圈。外圈优先已有 Downburst+12.18f，其次 Breath+9.08f，均无则 spawn+16.84f；内圈固定 spawn+16.84f。不能按 driver 最新时间排序。实际球判定锁定后不被后续 driver 改写。

分摊计数仅当恰等于1且成功删除一项时归零；空列表先收到 Effect 会保留计数，后续可超过1而不再消费。保留该源行为。死亡 spread 在 Update 永久移除；死亡 stack 只隐藏并保留，复活可恢复。默认 ForbiddenPlayers 为空，源不输出 Shared 伤害预测；不新增治疗策略。

## 显示、导航与执行消费

普通危险、火球、龙卷风以及未点名者加入R6分摊保留实际导航。击退/ThinIce 仅保留模型、显示及源提示，没有新增自动位移。

自己被点名时，源模型提供接近未点名队友R3的补集约束。真实源 `IconStackSpread.AddAIHints` 产生1条 FZ，但当前 `MechanicHintsConvert.ConvertForbiddenZone` 在本场R20采样中因覆盖率返回0、kind=Failed；原生公共 `DungeonOutput` 使用同样有限采样边界，保留模型和显示而不发布该硬导航。这个边界与未点名者加入R6不同，不能要求求解器对被拒绝的约束返回路线，也不能默默扩大原消费范围。

ThinIce 的18组标准/legacy、场内/墙边/场外真实源案例都产生1条FZ，经实际转换得到0、kind=Failed。模型投影逐点精确匹配源。尚未执行 ThinIce 完整 MechanicNavigationHost→SafeZone→solver 端到端，18组结果不能冒充该证据。

非Party、缺视角及Party0缺失撤导航，保留符合源语义的显示；开关、owner、Seek 与回放隔离经过生产 DLL 专项。所有实际执行仍由移动总开关、宿主控制权、PvP 和 ARR/MP 回放门控管理。

## 验证与失败证据

使用 `.tmp/halatali-verify/global.json` 选择本机 SDK10.0.303，主项目 net10.0-windows；根要求的SDK11未验。测试必须设置真实 `FFXIV_SQPACK`，不缺数据跳过。

`tests/AlteRoiteRegression.cs` 将真实源组件与原生输出逐事件比较有序列表、几何、位置、方向、判定钟、风险/颜色、目标和伤害类型。`tests/ScriptCompilation/NativeDungeonReplayAlteRoite.cs` 用实际DLL、ReplayController、ScriptManager、公共输出和真实求解器核验，筛选名 `--native-replay=AlteRoite`。

证据前缀 `.tmp/halatali-verify/native-alteroite-20261125-`：

- model-04：两FAIL，分别复现火球与龙卷风字典空位复用导致的缓存顺序差异；不覆盖失败日志。
- model-05：修正缓存后35相关场景PASS/0FAIL、退出0，包含O1/O3/M4/脚本控制。火球15事件边界、ThinIce18实际转换案例。
- model-06、07：各7PASS/1FAIL。新增伤害差分先发现夹具未清源Actor.CastInfo；修正后进一步定位到生产缺陷：重复Start仅删除首项、原施法已结束时，源Raidwide的空cast判定钟为default，原生误回退为now。已按源CastFinishAt空参合同修正，另跑最终模型与生产构建。
- build-02：生产0错误、4条既有Crucible警告，退出0，21.63秒。
- replay-01至03：错误预期被点名者R3补集会通过当前转换，求解器实际空成功；已用真实源转换明确消费边界。
- replay-04：未点名者R6真实求解器成功；随后“冷激活不补图标”夹具误用Seek重放了历史图标，改为连续播放更新验证，不改变生产逻辑。

最终证据：

- model-09：8个O1场景PASS/0FAIL、退出0，含真实冷激活后新增外圈、非Downburst首次缓存、18组伤害目标/钟/类型边界及上述实际转换。
- build-03：生产0错/4条既有Crucible警告、退出0，21.15秒。该次修正空cast钟后的DLL才是最终生产版本。
- replay-05：13条PASS/0FAIL、退出0，包括3条共用生产输入专项和10条O1专项。
- catalog-01：同DLL无筛选1428条PASS/0FAIL，292个入口逐文件独立编译，退出0。
- 最终DLL SHA256：`b1f1e7a80c8ec4d3b5eee4e639a196f98e6f858ab083c1f85d1ba1e6828ad766`。

审计生成器按唯一源文件精确登记；不会以12个组件换算12个源。实机像素、实际击退/冰面物理、宿主执行仍待游戏内验收；离线通过不代表已部署。
