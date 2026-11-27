# 巴尔德西昂兵武塔原生机制

## 绝对的美德：原生合同

入口 `Presets/Foray/Entries/BaldesionVirtue.cs`，GUID `masterplayer.foray.baldesion.virtue`，T827/CFC639、主 OID25DC。BNpcName7976 的实际客户端名为「绝对的美德」。本项单独按 Key 创建模型，不代表 BA4ProtoOzma 或整个 T827 已迁。

- DeathPhase 只有一阶段；旧 Manager Party0 存在、主怪 Targetable 且 InCombat 时拉起，不继承亚特／欧文100码限制。Dead、Destroyed 或 HP0 完成。保留8槽旧 Party 提交顺序、60个真实状态槽、原 Actor 引用与代际、冷激活逐组件回补。冷激活不重造历史 Effect 或动画。
- 真实 resolved 场地中心为 `(-174.8375,0,313.95)`、采样半径30，保留源圆形加减多边形轮廓。`--baldesion-arenas` 导出五份实际场地，不用静态(-175,314)或R30圆替代。
- 十常驻依次是 Meteor、MedusaJavelin、AuroralWind、ExplosiveImpulse、AernsWynavExplosion、BrightDarkAurora、AstralUmbralRays、BrightDarkAuroraExplosion、DarkAuroraTether、BrightAuroraTether；两个 Counter 仅按时间线动态启停。生产不创建源 BossModule／MechanicSession。

| 组件 | 原生消费合同 |
|---|---|
| Meteor | 14233 全队预测及全局提示，原 NPC 动态钟；普通 watched Finish |
| MedusaJavelin | 14235 长65.4、半角45°锥；普通 watched Finish |
| AuroralWind | 14234 浮点半径重载的 centerAtTarget=true、R5 tankbuster bait。保存原 source/target，显示与 Hint 按目标ID区分，导航按目标引用区分。显示／提示／导航用 raw 目标当前位置；Dead target 和 Destroyed target 保留，活动列表只排 Dead source。本人避开其他队员，他人避开原目标，source==target不添本人避让。伤害mask按原目标ID，钟取原完整列表第一项，即使该项source已Dead。Effect仅源计数、不结束；普通Finish按sourceID删首项。全局保留 Tankbuster cleave |
| ExplosiveImpulse | 14227/14228 两组R18，源 SimpleAOEGroups 的分组与普通Finish消费；不能用客户端proximity半径覆盖源R18 |
| AernsWynavExplosion | 14676 timed CastHint，原文 `Aerns Wyvnav is enraging!` 与动态剩余秒；无额外伤害预测。小范围复用提取的 TimedCastHint 同时验证莱丁消费者，未重构既有 OwainCastHint |
| BrightDarkAurora | 14218/14231 只在施法者真实1711状态时追加；14217/14230只在施法者1710时追加。锁 spellLoc/rotation，长30、宽100，原 NPC 钟。仅Bright的普通Finish跨source弹首FIFO；Dark Finish、Effect与过期不清 |
| AstralUmbralRays | 14222/14223查主Boss1711，14220/14221查主Boss1710；符合R16，否则R8。锁spellLoc和初始形状，主Boss后来状态变化不重写。任一对应普通Finish清全部来源 |
| BrightDarkAuroraExplosion | 所有tether ID均加入 `(原source,targetID)` 列表；Untether精确tuple删首。viewer是任一target时隐藏自身项，其余Risky=false，否则全部Risky=true。R8中心量化原source当前位置、default钟。重复own目标保留源default AOE尾项，不能去重或填造原点圆 |
| DarkAuroraTether | Created OID1EAA4A、TID1 |
| BrightAuroraTether | Created OID1EAA49、TID2 |

两种塔各持独立列表：Created保存raw位置、R2、min=max1、Now+20、空Allowed；20秒过期和Destroy不自动删，40008动画按精确位置删首。Tether保存查询时原target引用，缺失可保留null；Untether重新查当前世界对象再按引用移除，同ID换代不能错误删旧tuple。每个Update为同组件全部塔替换同一原target集合，不过滤Dead/Destroyed。自己的对应tether覆盖普通提示为 `Stand in a tower of opposite tether element!`，进入任一Allowed塔即非Risky；没有该组件tether时不产普通塔提示。正常前景顺序为塔outline→source连线→source R2圈；旧桥不消费的ActorMarker不扩造。塔导航沿既有开放塔逻辑保留 SDIntersection；伤害mask是viewer是否Allowed，再标记塔内存活Party成员，不按各member的Allowed判断，钟取首塔。

时间线完整展开92状态，保留cast start/end原始条件、duration、hint和16项预测窗口。两轮HostileAspect2及RelativeVirtues进入条件时新建BrightDarkAuroraCounter，真实14217/18/30/31 Effect计数，条件精确等于2/4/6，Exit停用；不能用AOE数、Finish或`>=`代替。最终14700的CastEnd Enter才创建MeteorEnrageCounter，14703 Effect为1和2的条件。最后状态250020无后继，永久保留，不新增???或超时结束。

### 源异常与 MP 显式适配

- 重复own连线：源数组只减一却跳过全部own项，产生Shape=null/Risky=false的default尾项。原始Visible保留该项及顺序；源提示和导航不消费它，源background绘图会空引用。MP只跳过该空形状的绘制并显示诊断，不把它升级为整个脚本异常，其他合法显示保持。模型实际调用源Draw证实异常。
- 塔Allowed含null：模型保留原集合，原NumInside仍可复现源空引用。viewer非Allowed时，源背景的短路绘图及塔内GTFO提示仍合法，MP保留；Allowed混有viewer和null时，源foreground先在base计数失败，派生连线／source圈后缀不可达，MP不发布该后缀。故障组件导航和伤害预测暂停，显示明确诊断，其他组件仍可发布。
- 上一条的“其他组件继续导航”是本次MP适配，不能声称旧桥等价：源AI组件循环没有逐组件catch，旧MechanicNavigationHost异常后Sleep撤销整个宿主导航；源绘图才有逐组件background／foreground捕获。此处保留实际机制输入和已知有效输出，不虚构缺失目标或塔资格。
- 不把源模块AernsWynav优先级1扩大为旧桥未消费的PR选敌执行。伤害仍只诊断，不新增治疗策略；ARR/MP回放只规划和显示。

### 本轮验证现场

日志统一前缀 `.tmp/halatali-verify/native-virtue-20261125-`。export01导出五份真实场地、exit0。model01为缺Catalog using与测试错误字符串拼接导致的编译失败；model02为11PASS/7FAIL：客户端名和缺Wind全局提示是生产缺陷，Counter基类转换及把有效Explosion误判为空是夹具错误。model03为18PASS；model04为667相关PASS/0FAIL。随后补双塔、全部原tether列表、null短路和前景后缀复核，model07为668相关PASS。

replay01/02没有放宽塔导航条数断言：实际 DLL 发现旧 GeometryGridSampler 拒绝覆盖场地超过65%的补集，正确的 raw SDF 在转换后变成空输出。仅本模块的塔 Join 改用 `BozjaVirtueTowerGeometry`，Contains保留源距离符号，有限外圆只限定栅格范围，正式场地仍独立裁剪；危险余量继续由解算层应用一次。模型保留旧转换确实为空的反例，并在正式场地内密网比较新Contains(0)与源SDF。

独立审查又发现两个R2安全圈重叠时，完整内圆轮廓会在另一安全圈内部制造假边界。model08先复现FAIL：圆心相对(-1,0)/(1,0)，安全线段(-1.2,0)→(-.8,0)被错误判交。修复为安全圈并集的差集轮廓，缓存默认轮廓，线段按真实圆交点分段检查源Contains；不是把距离判定改成多边形。model09增加安全整段、危险整段、轮廓无内部假顶点和缓存断言，实际DLL另验双塔注册补集、该线段和真实solver。此问题由输入可确定构造，未宣称实际录像中已出现重叠布局。

replay03前13专项通过后，停用脚本夹具仍调用强制要求Running的Seek helper，已改直接controller.Seek并断言停用；replay04为16专项加3公共PASS，replay05为371相关PASS。build01新增nullable、build05新增主构造捕获警告均已修；最终build06只有4个既有Crucible警告。

| 最终日志后缀 | 结果 |
|---|---|
| model09 | 669相关场景PASS/0FAIL，含21个AV专项 |
| build06 | 生产构建0错4既有Crucible警告，8.63秒 |
| catalog02 | 同DLL无筛选2005行首PASS（1996测试及9汇总），300入口独立编译，17个AV专项实际命中 |

以上日志均`.exit=0`。最终DLL SHA256 `0c9b6d11880696dda1a224cf32c9e46f88ebcdfa4c7e44c4e4d8e546c4303b14`。同DLL覆盖真实solver、原生导航注册、机制/脚本开关、非Party、逆Seek、与莱丁同区域双owner双向释放、回放禁执行；根SDK11和游戏实时采样/像素/执行仍未验，未部署。

源五文件SHA256：

```text
BA3AbsoluteVirtue.cs       f6306c118fd67b6188fe28e92d349b5cef96b6a4a9c78a5a8c4fa4a3cdfae163
BA3AbsoluteVirtueEnums.cs  697b231639226047cbeaa08e07d70d9f5bd7bc29ee78c362240e7308b837b4f4
BA3AbsoluteVirtueStates.cs 11f024f32ff564e95003c0ef0fa0ef98f2ab31ffe395a8e8346d0a2b00f88f8b
DarkBrightAuroraTowers.cs  0501f1837011f32cf0049191bfdafbebc629b81d0a6c11f55f43025d32b018f8
UmbralAstralAspectAOEs.cs  3a2ae57bc6c388e84657a66241bbac313aca1bbb20be983d09f2713ee40d9ed3
```

## 范围与入口

本规格只记录已实现的遭遇战机制合同。T827 同时包含丰水之地；不能按区域整体停用旧桥，也不能把一个 Boss 的完成计作整个兵武塔完成。源 `BA1Art` 对应 `Presets/Foray/Entries/BaldesionArt.cs`，GUID `masterplayer.foray.baldesion.art`；`BA1Owain` 对应 `BaldesionOwain.cs`，GUID `masterplayer.foray.baldesion.owain`。另有莱丁入口`BaldesionRaiden.cs`，GUID `masterplayer.foray.baldesion.raiden`，以及上文绝对的美德入口。四个入口按Key各自只创建一个遭遇模型，避免同T827重复注册输出。其他兵武塔主 OID 继续由既有未迁入口处理。生产不创建源 BossModule 或 MechanicSession。

客户端表核对：CFC639→T827，BNpcName7968「亚特」、主 OID `0x265A`。源 17 个唯一 AID 均存在且 CastTimeExtra=0；14678 自动攻击空名合法。14628/14629 未被源实际激活组件消费，不增加额外狂暴预告。BNpcBase 中有 265A/265B/2662/265C，1EA1A1 是 EObj，不能强制要求在 BNpcBase 中存在。

## 亚特的生命周期与输入

- 保留实际 resolved 场地：64 边 R29.5 多边形减去源六个形状，中心 `(-128.98, 0, 747.835)`；不替换为圆。`BaldesionArenas.cs` 由 `--baldesion-arenas` 从源运行库导出，同时包含欧文的独立场地数据。
- 拉怪要求旧 Manager 的 Party0 对象存在、主怪可选中且战斗中，Party0 至中心距离严格小于100。启动后距离、战斗标志、可选中状态不触发重置；主怪死亡、Destroyed 或 HP0 结束一次死亡阶段。
- 阶段激活顺序固定为 Thricecull、AcallamNaSenorach、LegendMythSpinnerCarver、DefilersDeserts、DefilersDesertsPredict、Pitfall、LegendaryGeasAOE、LegendaryGeasStay、GloryUnearthed、PiercingDark。阶段内不随每个时间线状态重建十组件。
- 保留原 Actor 引用及创建代际；Party 提交沿旧 Feed 的8槽合同。状态输入保留真实60槽。动画、Icon、Effect 不由冷激活重造，已有 Spell Start 按源组件激活时回补。
- NPC 初始判定钟是原始 TotalTime+.3f−Elapsed；亚特保留0和小于.05的 TotalTime，`.01→.02` 也是实际 Finish→Start。Elapsed 初始化及逐帧上限使用同一原始 Total。其他既有 Bozja 消费者仍沿其现行合同，未借本次改动全局改写。

## 亚特十组件

| 组件 | 合同 |
|---|---|
| Thricecull | 14644 单目标预测，普通 watched Finish 清理；不新增治疗策略 |
| AcallamNaSenorach | 14645 旧 Party 提交顺序的全队预测，动态 NPC 钟，普通 Finish 清理 |
| LegendMythSpinnerCarver | 14631 置一次 Mythcall；14632 为 R15，14633 为内7外22环。先追加 caster Loc，再在 Mythcall 时按原 Orlasrach 懒列表追加量化位置与原钟+2.6f，并清标志。恰五项时查询将首项颜色永久改为 Danger。14632–14635 任意来源真实 Effect 弹出首项；Finish、枪 Destroy 和钟过期不清队列 |
| DefilersDeserts | 14643 长35.5、半宽4的矩形，普通 Finish 清理 |
| DefilersDesertsPredict | 14642 先追加45°再0°的两个长35.5半宽4 Cross，钟为原 NPC finish+6.9d。多次 Start 累加，仅任意来源14643 Start 全清；Finish、Effect和过期不清 |
| Pitfall | 14639 保留源 R20 安全站位消费；不把客户端 range80直接替换源机制半径 |
| LegendaryGeasAOE | 14642 R8，独立普通 Finish；不能随预测 Cross 共清 |
| LegendaryGeasStay | 1EA1A1 且 X/Z 分别与(-135,750)差不超过1；10002填全部64个 Stay2 状态，40008全清。`Don't move!`、NoMovement只是现有诊断，允许攻击。源前帧位置自比较只在 NaN 时 Risky，死亡槽在 Hint 产出后清；Update不清 |
| GloryUnearthed | 下节的原引用追踪器、原始 Effect 与显示/导航分离 |
| PiercingDark | 14641 跟随原目标的 R6 spread；活动列表只排除 Dead，不把 Destroyed 合并为 Dead。普通 watched Finish，使用现有分散圈轮廓及原生导航 |

## 追踪器的事件消费

参数为显示R10、步长6.5、间隔1.5d、五发、ResetTargets=true、Icon92、辅助 Activation=Icon钟+5.1d。

1. Icon 将事件 source 对应原 Actor 加入 Targets，不去重；TargetsMask 独立使用事件 targetID 的旧 Party 槽。
2. 14636 Start 对 Targets 当前 XZ 至 cast Loc 取严格 `<` 最近项，等距保首，无距离上限。与 cast.TargetId 无关。删除一个 Targets 原引用并清该 Actor 在旧 Party 中的当前槽；新 Chaser 保存原 Source/Target、Loc、MoveDistance0、Remaining5和原 NPC 判定钟。
3. 预测读取原 Target 当前 XZ，按源 `MoveDistance*offset` 后乘倒数的顺序计算，再量化位置；方向由量化预测点减 Previous 得到，不能改浮点运算次序。
4. 14636首发与14637续发 Effect 都消费。MainTarget等于caster时用caster量化位置，否则优先当前世界同ID对象的原始位置，缺失才用包 TargetPosition。由这个落点在全部 Chasers 的当前预测点中严格取最近首项，不按目标身份直接选项。
5. 每个 Effect 增计数；匹配项 Remaining减一，未结束时 Previous改实际落点、MoveDistance改6.5、钟改Now+1.5。第五发删除该项；Effect后列表空才清 Targets/Count，保留Mask与辅助Activation。
6. Update 只移除已消费至少一发且目标 Dead/Destroyed 的项；首次前死亡仍保留。Update清空列表不重置Targets/Count；普通Finish、caster Destroy、预计钟均不消费。

前景线从 raw Previous 连至原 Target 当前 XZ，两端取观看者Y。同ID重建不重定向原引用。导航按本人项再他人项发布：本人中心为预测点沿本人朝向后退10、R16.5；他人中心为预测点、R11。显示圈始终R10；不把显示直接当导航，不扩大特殊位移执行。

## 时间线与输出适配

源全部333状态逐项保留 ID、名称、预计秒数和 flags，十轮使用源逐项累积 ID，永久尾为 `0x6720000 / 10 / ???`，十秒不会自动结束。状态 CastStart 读取主怪 raw Cast，非Spell和未知动作仍按源推进并记录诊断；CastEnd只等待 raw Cast为空。组件条件满足立即推进，否则在 expected+1 后 overdue，同帧允许多次转移，每次 entered=Now。

Geas条件读取槽0；Glory条件读取真实 Chasers；Spread条件保留原引用的 Dead 筛选。时间线先更新，随后 Chaser.Update 清死项，因此本帧末清空要到下一帧才被时间线条件观察到。Phantom的特殊ID flags不适用于同ID的亚特状态。

旧宿主绘图有两次 Hint 查询；Framework先捕获 NoMovement，第一次查询可清死槽，第二次公共文本已无 Don't move，但当帧仍保留捕获的诊断。普通事件 Render 保留该发布，下一 Framework才清。两视图都休眠时不查询 Hint，死亡槽保留至恢复绘制；非Party观看者撤全部消费者。

`BozjaCatalog.Owns` 与 `HydatosCatalog.Owns` 分别按主OID判断。两入口可以在T827同时Running，机制/脚本关闭只释放自身owner；不得整区压制未迁移BA模块。回放仍只规划和绘制，不发PR面向、目标、ACR、GreenMove、遥控或原生动作。

## 源版本与本机验证

源四文件 SHA256：

```text
BA1Art.cs                 7D79741C165232BA529D30727F1CE2B502B4971FAB133464699E39FF99EEE71C
BA1ArtEnums.cs            AAA577DC1017F32CF820BE59F7EE27C68A357FA9C7FAC3451524FF76EB8C092D
BA1ArtStates.cs           EEA3127EBAAE8AF974D7BEBFE4EB961289006B16EF1457F31522B7289E658CC4
LegendMythSpinnerCarver.cs 2A5CBDF6FB1E39FEA96297F1EF2A24193C39E6AB0B8F7CFF079B3D403CAC241F
```

客户端读取库 SHA256：Lumina `67DA75CEFA468277972C8C8C27264B1D276FE2047213C223D1ECDCED435D4E05`，Lumina.Excel `AF1AC061ED0B5378A180113CF91969CD40C3B90A25E526D1FC3D35FC0B3CA5FA`。

源差分由真实 MechanicWorldFeed→MechanicSession 驱动，逐事件比较十组件、形状场内采样、原始SDF、位置/方向/钟/顺序、颜色/Risky、预测伤害、Hint和内部追踪状态。oracle显式恢复旧ReplayFeedAdapter丢失的IsSpell、原始Total/Elapsed和真实状态槽；对象重建使用缺席Destroy帧再Create，不把模型不存在的GameObjectId假设当测试输入。

失败历史：model01场地导出缺ArtRadius；model02的GameObjectId夹具未触发源Feed重建；model03引用了不可访问的源派生组件类型，改由公开基类检查。replay01漏统计既有`bozja/spread/`轮廓键；replay02跨Seek保存引用，而Seek会重建会话，改在同一实例Apply缺席/新对象验证原引用。失败日志保留，不降低断言或容差。

最终验收日志位于 `.tmp/halatali-verify/native-art-20261125-`：

| 日志后缀 | 结果 |
|---|---|
| export02 | 从实际运行库导出 Art/Owain 真实场地，exit0 |
| model06 | Bozja、Hydatos、script controls 联合605真实场景PASS/0FAIL，其中19个Art专项，exit0 |
| build01 | CI生产构建0错4个既有Crucible警告，14.33秒，exit0 |
| replay03 | 同一DLL的16个Art专项加3个公共输入场景PASS，exit0 |
| catalog01 | 无筛选1952行首PASS：1943个`PASS `场景加9个`PASS:`汇总；297入口独立编译、16个Art专项全部实际命中，exit0 |

最终 DLL SHA256 为 `041ba9eed3e7ec3f26e9cb11a8b24419ebe59b80be54aa583c3619cf7de12d0a`。实际DLL承重场景包括全部标准组件的Finish/独立预测、Myth跨来源FIFO、Geas持久与Start清除、Stay2允许攻击/死亡双Hint/发布缓存/双视图休眠、冷历史不恢复、原引用前景线/缺席Destroy再Create/首发14636和四次14637、自身量化落点与缺失目标包落点、R10显示与本人16.5后退10/他人11的真实snapshot内外采样、真实solver、非Party撤消费者、严格拉怪/HP0、开关/逆Seek/owner和回放禁执行。最近竞争目标及三路落点选择由源事件差分承重。Hydatos双GUID实际Running，精确主OID归属且不整区压桥，两边停用各不清另一方输出。

验证使用隔离SDK10.0.303；根SDK11、游戏实时采样、实际像素与执行效果仍待实机验收，未部署。

## 欧文的生命周期与十一种组件

客户端表：BNpcName7970「欧文」、主OID `0x265D`、CFC639/T827；枚举中每个AID均存在且CastTimeExtra=0。实际场地为R29.5的64边形减六形状，解析后中心 `(128.98,0,747.9875)`，不由亚特场地镜像推算。拉怪及独立死亡阶段、旧8槽Party提交、NPC raw Total/Elapsed同亚特；不会等待另一侧Boss死亡。

十常驻按源顺序激活，临时LegendaryImbas追加末尾：

| 组件 | 原生合同 |
|---|---|
| Thricecull | 14661单目标预测，真实watched Finish |
| AcallamNaSenorach | 14662全队预测，动态raw NPC钟 |
| ElementalMagicks | 14650火/14651冰开始时，按原Munderg `0x265E`懒列表、真实1783/1784状态追加量化枪位置，再追加boss spell.Loc；R13、同NPC钟；不排Dead/Targetable，不去重/排序。任意来源14650–14653 Effect只删首项；Finish、过期与枪Destroy不清 |
| PiercingLight1/2 | 14655/14660，原目标R6 spread；普通watched Finish。Dead仅隐藏输出，时间线检查原始列表Count |
| Pitfall | 14669实际源R20，不将枚举range80替换为组件几何 |
| Spiritcull | 下节Icon、原目标列表和绘图突变合同 |
| IvoryPalm | 所有tether ID、原source/当时解析的target tuple；首个target引用等于viewer且source未Dead的反向眼。default钟、Forward0、Range10000，不排Destroyed/610/Targetable |
| IvoryPalmExplosion | 14658定时全局文本`Ivory Palm is enraging!`，不新增raidwide伤害预测 |
| EurekanAero | 每次查询原Palm懒列表，排Dead或真实610，允许Untargetable/读条和Dead目标；方向raw source→当前TargetID；显示/导航量化source、文本只量化viewer并保留raw source。非主目标R6半角60°，主目标对每个其他活Party成员发布R100同角扇形；default钟，Effect不清持续扇形 |
| LegendaryImbas | 14656临时raidwide，在Spiritcull raw CastStart进入CastEnd状态时激活并冷补当前Spell。随后Casters非空或overdue条件退出时停用；不是常驻，也不是等Finish才启动 |

状态按真实60槽采集；1783/1784、610存在即有效，不凭ExpireAt自行删除。真实1753 Lose按任意非Pet Actor触发Spirit清理。热连线按非零Target派发；冷回补按非零ID派发，包括目标为0时的null tuple；ID0非零Target只有热包有效。Untether按当时当前世界解析的target原引用及source删首，同ID新代不替换旧tuple。

凝视文本固定`Face the hand to petrify it!`，精确点积阈值`.707107f`，自定义文本不设距离门槛；AI仅在10000范围内发布反向135°禁止方向。面向计划保留旧桥`center+PI`消费，不以通用角度求解器保留偏离中心的安全角。石化只停Aero，不停眼；回放不执行计划面向。

## Spiritcull与输出顺序

- Icon55追加事件source原引用，忽略targetID，允许重复；仅首次建R1.5、Min24/Max24、Now+5.1d的stack。冷激活不补Icon；Finish/Effect/过期不清。
- 只有Icon目标执行此组件Hint/AI；Party伙伴仍按普通活成员参与。自己是stack目标时人数必须24；其他目标已在唯一stack内时保留`Stack!`的Risky=false，不以总人数不足24改为危险。默认ForbiddenPlayers为0时源不发stack伤害预测，MP亦不增加。
- 源Feed只提交旧8槽，24容量边界在真实输入不可达；保留Min24/Max24数值，不缩小到8，也不宣称人为24/25人是实际桥输入覆盖。
- 只有前景绘制查询最近非自身原引用，严格小于保首项，不排Dead/Destroyed；找不到则用自身。它修改stack目标而不改Activation。双视图都关闭时不查询、不修改。
- Framework先捕获旧目标的AI导航/伤害/面向，之后前景改目标、公开Hint与圈使用新目标；下一Framework导航才切换。事件Render不会提前运行该副作用或刷新prepared导航；非Party视角及owner停用撤销缓存和所有输出。

## 欧文完整状态链与验证现场

保留全部457状态及累计ID，十二轮中的ElementalShiftSpiritcull使用`id +`，其他累加保持源`id +=`。永久尾为`0x8910000`，不是十秒超时。raw CastStart/End可由非Spell主怪Cast推进；组件输入仍按Spell过滤。条件可以提前满足，默认overdue为期望时长加1秒。LegendaryImbas重激活清旧缓存后按原Actor顺序冷补，不影响Light1/2原始列表。

源六文件SHA256：

```text
BA1Owain.cs       c807503f25eb79d9c96931a9590ae953e795143df2cc543a550ceb3552727354
BA1OwainEnums.cs  0f419d057d1067a0e413941cb67ddce00c90063ef669fdaf42ccb58dceef9e4e
BA1OwainStates.cs 9bb653102de07e80143ea7f49b6daaacb69a611487efe175cb6371910db7671c
ElementalMagicks.cs c6029cc412b9b0f28da5a0e8254cff60a8525b834ca60e2003d3169cf1cc76b7
IvoryPalm.cs      c187fcb07991c8f41a237b367372c27aaaa85fb5cc844bd4daf73ab0288cd284
Spiritcull.cs     7d02e51cbfc15038fb213bce8f00df2d071663dc8b60648b83f34e3b228b0102
```

日志前缀`.tmp/halatali-verify/native-owain-20261125-`。model02/03保留三处FAIL：公共状态采集门控漏IsOwain，使元素枪漏1783、Spirit漏Lose1753、Aero漏610；补gate后model04为33PASS。model05/06的ID0热包被下一快照正常撤销，范围断言前补可表达的非零ID快照；保留原冷热差异。model08先以真实源复现raw cleave Hint和cold null tether两FAIL，分别修raw Hint原点、冷ID门槛。model09为602 Bozja真实场景PASS/0FAIL，其中20个欧文专项。build01发现生产Render模式变量stack命名冲突，改为spiritStack；build02为0错4既有警告、10.47秒，exit0。

实际DLL失败历史：replay01在六标准组件、元素、Spirit后出现runner设置类型/字段反射错误，已改为实际Settings命名空间及字段。replay02仅编译警告后退出-1且无进一步诊断，不作为验证通过。replay03/04的凝视连线在下一快照被正常移除，夹具补入与热包一致的tether快照。replay05把Boss与helper的Finish分成两次完整Update，源条件已经提前满足；改为同帧清两个cast后才检验overdue。均保留日志，没有放宽断言。独立核验后，前景/导航增加互斥位置断言，三owner场景先在亚特侧拉怪再换至欧文侧，保证两Boss与刻托确实同时有输出，并验证双向停用及GUID归属。

最终验证（同前缀，各`.exit=0`）：

| 日志后缀 | 结果 |
|---|---|
| model10 | Bozja/Hydatos/script controls联合625场景PASS/0FAIL，含20欧文专项 |
| build02 | 生产构建0错4既有Crucible警告，10.47秒 |
| replay06 | 同DLL相关125条PASS，含13欧文、16亚特专项 |
| catalog01 | 无筛选1966行首PASS：1957个测试及9汇总；298入口独立编译，含13欧文专项实际命中 |
| audit-generate01 / audit-check01 | 生成与核对正式清单；801 verified /412 pending /1 review /45 excluded |

该批生产DLL SHA256 `2779bbd0acd0435ccd772419e9a261ce0427eeec69011a706002ddb3a79ee863`。真实solver、双视图睡眠、旧AI/新前景/下一Framework导航、状态Lose、temporary Imbas、实际Effect、owner、开关、逆Seek、非Party及回放禁执行均已离线覆盖；根SDK11与游戏实时采样/像素/执行仍未验，未部署。该批源类型数801与独立入口298分开计数；Raiden的后续进度见下节。

## 莱丁：双阶段与组件重建

入口 `Presets/Foray/Entries/BaldesionRaiden.cs`，GUID `masterplayer.foray.baldesion.raiden`，独立 Key `BA2Raiden`。源六文件位于 `BA2Raiden/`。真实客户端 BNpcName7973 为「莱丁」，不是初稿译名“雷电”；CFC639/T827、主 OID2605、全部源 AID 存在且 CastTimeExtra=0 均由模型读取本机数据核对。

起始场地是源 R34.6/80 边多边形减三个矩形后的真实 resolved 轮廓，中心 `(0.27999878,0,457.825)`，采样半径34.42502。10002墙动画将 Bounds 改成 R29.93/64 边 resolved 轮廓；MiniArena.Center 保留旧中心，所以生产只更换 local points，不把中心改到 `(0,458)`。`--baldesion-arenas`导出器保留Art/Owain并增加两份Raiden轮廓。

P1按序激活 ArenaChange、SpiritsOfTheFallen、Shingan、AmeNoSakahoko、WhirlingZantetsuken。源没有亚特/欧文的严格100码拉怪门槛，仍要求主怪可选中且战斗中。P1只在 Dead、Destroyed 或不可选中时转阶段；HP0但未Dead且仍可选中保留P1。转P2时仅移除ArenaChange，其余四组件及原cast实例原样保留，不重新冷补。随后依次激活Shock、LateralZantetsuken、LancingBlowSpread、LancingBlowAOE、CloudToGround、BitterBarbs、Levinwhorl、BoomingLament、SilentLevin、ForHonor、UltimateZantetsuken。每个组件独立完成Created→已有Spell→非零ID tether冷回放后才激活下一组件；P2按死亡阶段的Dead/Destroyed/HP0结束。

Reset遵循源Deactivate→Activate：移除该规则并清其局部状态、追加到活动列表末尾，然后冷回补。成对Reset必须按源先后逐个完成：Spread→Silent、Silent→Chains。不能先全清再统一回放。时间线先于IconStackSpread.Update，因此死亡标记可阻挡本帧的条件，到下一帧才看到空列表。Stop仍执行当前state.Exit，结束恰落在带Reset的状态时会临时回补，随后完整清理。

## 莱丁十六组件合同

| 组件 | 输入与消费 |
|---|---|
| ArenaChange | 14463覆盖唯一Donut30–35，钟为原NPC finish+3.4d；正常Finish、过期不清；OID1EA1A1的10002动画缩场并清警告，P2移除组件 |
| SpiritsOfTheFallen / Shingan | 14458动态全队预测、14459动态单目标预测；四Keep之一，正常watched Finish |
| AmeNoSakahoko / WhirlingZantetsuken | 14441 R25、14442 Donut5–60；四Keep之一，原cast列表，不重建 |
| Shock / LateralZantetsuken | 14445 R8；14443/14444长75.4宽39 Ray，SimpleAOEGroups默认99，不提前排序，按caster ID任意动作Finish删首 |
| LancingBlowSpread | Icon138取事件source的原Actor，忽略targetID，不去重，钟Now+6；2607 Created清全部；14455按MainTarget ID删首，未命中回退删首；Update只删原Actor Dead，不合并Destroyed；本人Spread提示后仍可并存避让其他Spread提示 |
| LancingBlowAOE | 2607 Created追加量化锁点R10、Now+1，移动/Destroy/Finish/过期不清；14455真实Effect计数并清全部AOE，计数不随新Created重置 |
| CloudToGround | 14449按spell Loc和Rotation相对当前Center的前后35、半宽15/20闭区间选择8/6/4发；line使用raw caster位置，Advance8、间隔1.1d、maxShown10；14449/14450按caster当前位置逐轴abs≤1匹配首line，推进到实际位置+Advance；未知位置静默保留；普通Finish不推进 |
| BitterBarbs | TID18先锁存Assigned，即使目标缺失；伙伴存当时旧Party槽与原Actor引用，Untether按回调当时槽清，不清Assigned；导航为raw partner位置、两人距离+1半径、Now+10；显示独立线条，保留raw两端 |
| Levinwhorl / BoomingLament / SilentLevin / ForHonor | 14447全队预测、14461 R10、14451 R5、14460 R11.4；普通Finish。Silent时间线计数只由真实14451 Effect增加，Reset时清 |
| UltimateZantetsuken | 14456普通CastHint，`Enrage, kill the adds!`附动态剩余秒数；不补造全队伤害或治疗策略 |

Spread显示/导航持有原Actor；诊断伤害的mask另按该原目标ID查当前Party槽，不要求当前世界中还有同一个引用，也不按新代Actor死亡过滤。最早activation保留所有活动Spread，包括非Party目标的钟；同ID新代不能重定向原Spread。通用Party提交仍为既有8槽合同，状态保留真实60槽。没有增加源Module级选敌执行；T827其他主OID仍沿原有桥。

Cloud未来/当前AOE保持源count/version缓存：没有输入时不因时钟过去而重算未来钟。模型逐事件比较内部line的Next、Advance、NextExplosion、剩余数、间隔和显示数，不能只以开场形状相同判定等价。Chains全部64个伙伴槽及原引用、Spread原列表/钟/Destroyed、Silent/Lancing计数也逐帧与真实源比较。

## 莱丁时间线与验证证据

P1为17状态，P2为94状态，共111；保留ID、预计时长、名称、Group/Cast/Downtime/Raidwide/Tankbuster flags及16项发布窗口。P1名称`P1`，P2名称`Boss death`，P2基址1000000。所有组件条件立即检查，未满足才在expected+1转移；raw CastStart接受Ability/未知动作并诊断，CastEnd等待raw Cast为空，Targetable不自动超时。尾`1FF0000/10/???`永久等待。Lancing计数不重置导致后轮条件提前成立，是原语义，未自行修正。

源文件 SHA256：

```text
ArenaChange.cs          ccb424104a87b290f2f6bbc7bcae89fbec8cdd4d88432b77bc49cbe2721df966
BA2Raiden.cs            abe43dca6b56f245cd82d6fe7269c532ad6d76b7ce51a46bb6d72a65b2b61758
BA2RaidenEnums.cs       93c3ba4ae870e7553496a37eb4c9241db24735382f9b2ee31205c9f4b54cb605
BA2RaidenStates.cs      c0ccefd6e1ee3e1dc7cbbc924a57ca896b9473a2b2a7dcc2ae33a762c18aa3a1
CloudToGround.cs       969a7a944a719844c6e0ac3c849c0989931f1a41d607691a0277bb8319c54fba
LancingBlow.cs         e30fc1c64415cf95f018eac2b9e62e850bbfd9c1742a982f4335f4e34fb604ac
```

日志前缀 `.tmp/halatali-verify/native-raiden-20261125-`。model01为动画构造缺参数；model02的动画两ushort顺序、固定slot0查询和少第二Spread提示分别复现差分；model03从真实客户端确认莱丁正式名。model04/05新增原引用换代mask、完整内部队列、早条件/成对Reset/Stop等场景，修正夹具尚未发送不可选中却期待越过Targetable的错误。独立复核指出mask必须按ID，新增真实缺席/同ID重建差分。replay01初始不可选中无法拉怪，改为真实可选中P1→不可选中P2；replay02将ArenaBoundary误作包装对象，改读真实Geometry；replay03的Cloud下一点误用了Z轴，改为输入的.47弧度方向；replay04将line当AOE查，改查真实GetLines并检查两端坐标和清理。失败日志保留，无放宽容差或降低断言。

已完成的离线验证：model06为648个Bozja/Hydatos/script controls场景PASS/0FAIL（含23个莱丁专项）；build01为0错4既有Crucible警告，8.94秒；replay05为同DLL20专项加3公共PASS。生产DLL SHA256 `abfee7c5c83a849d50b2e6fc0c7a0b5161ad6e9588b67921b509a2bf82e18a17`。同DLL replay06为355相关PASS；catalog01无筛选1987行首PASS（1978测试及9汇总）、299入口独立编译，实际命中20莱丁专项。独立复核后replay07仍为20专项加3公共PASS，并加强活跃StreakLightning无选敌请求及仍持活跃链线的owner双向释放。audit-generate01已写正式802 verified/411 pending/1 review/45 excluded；audit-check01核对生成清单。各日志exit0。SDK10.0.303隔离验证，根SDK11和游戏实时采样/像素/执行未验，未部署。
