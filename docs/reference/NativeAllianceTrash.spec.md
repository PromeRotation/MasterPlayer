# 联盟杂兵原生迁移

本规格覆盖朱诺：第一巡行四个杂兵模块及荣华神域塔利亚三个杂兵模块。生产使用 MP 原生 `BozjaEncounter` 语义模型、公共输出适配及七个 `BozjaScript` 入口；BMR 只作测试对照，生产不创建 BossModule 或 MechanicSession。其余联盟首领保留原桥。

后继批次增加桑多利亚：第二巡行的探测器、锯耳 杰克和克拉肯。三个独立入口已完成源差分、实际 DLL 专项与完整目录，正式登记为 mp_native_offline_verified。双狮已完成原生离线验收并登记；拉尔戈神使也已完成原生离线验收并登记。

## 桑多利亚后继批次

客户端核表见 `.tmp/alliance-next-table-review/report.json`。五候选共50个唯一源AID均存在，其中拉尔戈神使的28892视觉读条 ExtraCastTime 为1000ms，其余为零；该视觉动作不被本次三个模块消费。身份只核 ModuleInfo.NameID 对应 BNpcName 和 CFC 对应 Territory，不宣称核验了 OID 到 BNpcBase 的映射。

| 入口后缀 | 名称 | Primary OID | Territory / CFC | BNpcName |
|---|---|---|---|---|
| detector | 探测器 | 0x4880 | 1304 / 1058 | 14072 |
| jack | 锯耳 杰克 | 0x4875 | 1304 / 1058 | 14080 |
| kraken | 克拉肯 | 0x4885 | 1304 / 1058 | 14071 |

三个真实 resolved Polygon 场地由 `--alliance-next-arenas` 导出，包含探测器场地的多个孔洞；不以包围圆代替。仍使用原 Party0、原 Primary 代际、同OID pending 顺序及回放执行隔离合同。

- 探测器按源顺序激活 Electray（43576，长40、半宽2.5矩形）、Electroswipe（43559，R50、半角60度）、同动作带单个施法者姓名的 Interrupt。两个 OID 全部死亡或销毁才完成。
- 杰克只激活 DustCloud（43554，R10、半角60度），不从枚举补造其他没有激活的动作。七个 OID 全部死亡或销毁才完成。
- 克拉肯组件顺序为 MightyStrikes（43575，带姓名 Interrupt）、PowerAttack（43569，R20、半角60度）、CatapultParalyzeIIIBanishga（43567/43568/43562/43570，Grouped R6）、ImpactRoar（43566，RaidwideCast）、Flood、Sucker。完整单阶段要求 P1 的六个OID全部销毁且 P2 的三个OID全部死亡或销毁；P1只有死亡仍阻止完成。
- Flood 仅在43791 Start覆写唯一 R8 圆，锁 spell.Loc，判定钟为原 NPC Finish + 3.5d。43792 Start不生成新项；任意 source 的43792 Finish清空。43791自身Finish、预测钟到期均不清 Flood。
- Sucker是独立的15码拉近，多个来源顺序串联；显示和个人提示受真实60槽免疫影响，自定义AI无视免疫，仅给第一个source的R23禁区。落点危险只看当前Flood，不叠加Arena外判定。
- ImpactRoar沿用原CastHint引用及重复Start：读取原Actor当前raw CastInfo，包括后续非Spell读条。与SingleTargetCast不同，CastInfo为空时Raidwide仍保留默认判定钟的诊断。完整伤害列表按源AIHints.Normalize的Activation比较器排序，不按入队顺序；等钟使用同一List.Sort语义。

日志前缀为 `.tmp/halatali-verify/native-alliance-next-20261125-`：

- `export01`：四个真实场地导出成功，包含后续双狮；导出不代表双狮已验。
- `model01`：61 PASS/2 FAIL，复现Raidwide重复Start残项在null CastInfo下被错误隐藏；按源默认fallback修正。
- `model02`：63 PASS/0 FAIL；`model03`：733相关PASS/0 FAIL。
- `model04`：独立复核的双ImpactRoar后发先至反例，0 PASS/1 FAIL，命中真实源判定钟顺序差异。
- `catalog01`：因已确认排序缺口主动中止，不能计作完整目录通过。
- `model05`：734相关PASS/0 FAIL，含65联盟模型专项；补四技能分组98/99/100排序、任意Finish及两个原施法者伤害排序，exit0。
- `build02`：0错4既有Crucible警告，8.80秒，exit0。
- `replay02`：61联盟专项加3公共PASS/0FAIL，exit0；覆盖真实solver/开关/逆Seek/owner/回放隔离以及Flood覆盖、不同完成条件、raw非Spell/default诊断与两个施法者排序。
- 当前DLL SHA256：`e1df87c6671d58bc58c3cc14638bcde916e99470d556370980fb0b5d39388e9f`。`catalog02`无筛选2067行首PASS/0FAIL、310入口独立编译、61联盟专项，exit0。精确三项ownership生成CSV并执行--check后为813 verified/400 pending/1 review/45 excluded，1214目标不变。

## 客户端身份

本机中文客户端表核对见 `.tmp/alliance-trash-table-review/report.json`，36 个唯一源 AID 均存在且 ExtraCastTime 为零。源的形状与实际激活组件决定迁移范围，不按 Action 表中所有动作补造机制。

| 入口后缀（`masterplayer.alliance.`） | 名称 | Primary OID | Territory / CFC | BNpcName |
|---|---|---|---|---|
| vanguard | 先锋寻路人 | 0x4689 | 1248 / 1015 | 13599 |
| aquarius | 宝瓶蟹 | 0x468F | 1248 / 1015 | 13605 |
| groundskeeper | 坐镇巨像 | 0x4691 | 1248 / 1015 | 13607 |
| despot | 专制者 | 0x4692 | 1248 / 1015 | 13608 |
| serpent | 塔利亚海蛇 | 0x4010 | 1178 / 962 | 12478 |
| angelosfirst | 神的使者·第一组 | 0x4013 | 1178 / 962 | 12481 |
| angelossecond | 神的使者·第二组 | 0x40E1 | 1178 / 962 | 12481 |

## 激活组件与阶段

- 先锋寻路人按源顺序激活 Seismostomp（41652，R5）、BombToss（41655，R3）。四 OID 全部死亡或销毁才完成。
- 宝瓶蟹按顺序激活 WaterIII（41666，R7）、CursedSphere（41656，R3）、BubbleShower（41664，R6、半角30度）、Scoop（41663，R15、半角60度）、Beatdown（41662，长9、半宽1.5）、SpiderWeb（41659，R6）、HundredFists（40648，带单个施法者姓名的 Interrupt 全局提示）、Agaricus（41661，R5）。九 OID 全部死亡或销毁才完成。
- 坐镇巨像激活 IsleDrop（41670，R6）、MysteriousLight（41667，凝视）。凝视使用构造默认范围10000，不采用技能注释中的20；眼锁定原 spell.Loc，普通对应 Finish 按施法者 ID 删除首项。
- 专制者顺序为 IsleDrop（41699，R6）、WingCutter（41672，R6、半角60度）、ScraplineTyphoon、ScraplineStorm、Panzerfaust（41698，SingleTargetCast 全局 Tankbuster 提示和伤害诊断）、PanzerfaustHint（同动作，独立带姓名 Interrupt 全局提示）。
- 海蛇 P0 仅 WaterIII（35438，R8）；全部0x4010死亡或销毁（空列表亦真）进入 P1，停用旧组件并按新组件顺序回补当前读条。P1 为 PelagicCleaver（35439/35852，R40、半角30度）、两个独立固定 Interrupt 提示、WaterDivineFlood（35442/35440，R6）。最终要求三个其他 OID 全部死亡或销毁，且海蛇列表为空；死亡但尚未销毁的海蛇仍阻止完成。
- 神的使者第一组依次为 RingOfSkylight（35444，8–30环）、对应 Interrupt、SkylightCross（35445，长60、半宽4十字）、对应 Interrupt、Skylight（35446，R6）。完成条件为原 Primary 死亡或销毁且全部0x4014死亡或销毁，不要求其他0x4013死亡。
- 第二组仅 RingOfSkylight 和 SkylightCross，没有 Interrupt 或 Skylight；全部0x40E1死亡或销毁才完成。

七项均覆盖源的 TrivialPhase 完成条件，因此不能使用默认 HP0、不可选中或脱战结束。默认拉起要求原 Primary 可选中且 InCombat；专制者改为三个 Trash OID 任意对象 InCombat，不额外排除 Dead、不可选中或原 Primary 死亡。CheckReset 为 false。

源 Enemies 只按 OID 枚举全部 Actor，不按 BattleNpc、敌对或存活筛选。Created/Destroyed 更新列表；Actor 同 ID 换代保持旧引用与新对象分别可见的生命周期。使用旧 Party0 的加载/创建合同和新 Party 的 Update，不用观看角色取代 Manager 的 Party0。

同 OID 多 Primary 保持原 Manager 的 pending 顺序：一次仅加载同 OID 的一个模块；pending 逆序选择，前一个卸载后下一帧才晋升。七项没有构造期组件，公共层可用待加载原 Actor 队列表示这些独立空构造模块；不将此结论推广到有构造输出的其他模块。差分覆盖三个第一组主怪逐次交接、第二组原 Primary 销毁及同 ID 重新绑定。

## 专制者自定义机制

40650 Start 锁 spell.Loc，追加 R10 圆（NPC Finish+2.1d）及8–40环（Finish+5.6d）两项，只显示队首。41393/41902 Finish 不限制来源，删除首项；40650 自身 Finish 不消费两项，超时不清除。

ScraplineStorm 独立记录40650的10码拉近，依序推进多个来源，零向量与非正距离跳过。显示/个人危险提示检查真实60槽免疫；落点危险只查询 Typhoon 队首，不把 Arena 外加入该自定义判定。AI override 与基础击退不同：无视免疫，只给首个拉近 source 的20码危险圆及其 Activation；不得统一成多个来源或全部免疫过滤。

## 普通组件与原始 NPC 时钟

普通 SimpleAOEs 保留重复 Start；只在对应 WatchedAction Finish 按施法者 ID 删除首项。海蛇 P1 两个 SimpleAOEGroups 接受任意同施法者 Finish，达到99项才按 Activation 排序，100项后继续排序。Interrupt 和 SingleTargetCast 保留原 Actor 引用并按引用删除首项；多个组件消费同一动作时分别维护列表及提示。

NPC 时钟保留 TotalTime=0、0.01等原值，先 float 运算 `Total + 0.3f - Elapsed`，再加 double delay 到当前时刻；Elapsed 按真实帧增量累加并封顶 Total。不得把封顶后的余数简化成常量0.3。0.01→0.02须产生 Finish→Start；只修改记录的起止绝对钟不会重置源当前 CastInfo。

SingleTargetCast 诊断取原施法者当前 CastInfo，self-target 时回退当前 TargetID，未知目标保留零 mask。重复 Start 留下的条目在后续非 Spell 读条期间也能产生预测；当前 CastInfo 为 null 时隐藏预测但保留条目。为此 Alliance 复用既有 raw Cast 通道，伤害诊断不从只接受 Spell 的 watched Cast 通道取值。仍不新增治疗策略或 PR 自动打断。

## 场地、显示、导航及执行

朱诺四个真实世界 Polygon 及第一组神的使者的三矩形并集减四个方孔、两个圆孔，由现有源场地导出器确定性生成。海蛇为中心(-800,-800)、R20圆；第二组为中心(800,909.75)、半边19.5方形。半径使用导出的 Bounds.Radius，不将 polygon 误替换成包围圆。源差分逐点比较实际 resolved Arena，包含岛及孔。

模型保留源原始几何；公共适配分别输出显示、精确几何导航及诊断。源导航 Normalize 顺序沿用既有确定性排序。凝视使用原始数学与精确范围阈值，图形眼/瞳及方向弧沿用公共层。通用躲避只接管实际 NPC 的匹配动作，按 Primary OID 和真实 Territory 划分桥接 ownership；其余联盟模块不被扩展接管。

所有实际移动、朝向、PR ACR、原生动作和遥控继续由统一门控控制；ARR/MP回放仅规划与显示。脚本、机制开关、逆 Seek 和 owner 停止必须撤销场地、危险区、预览及其他输出。缺 Party0 时不加载/更新模块，不从观看角色补造队伍。

## 验证证据

七项已完成原生离线验收，正式清单登记为 mp_native_offline_verified；游戏实时采样、像素与执行仍待实机验收。

- `native-alliance-trash-20261125-model01`：继承失败38 PASS/3 FAIL，发现个人提示范围和不可达夹具问题。
- `model02`：未设置游戏数据环境变量，41项全部报明确输入错误，未静默跳过；修正环境后重跑。
- `model03`：41 PASS/0 FAIL，个人提示范围修正为源全局提示，冷激活重排后真实覆盖。
- `model04`：45 PASS/1 FAIL；额外硬编码的封顶时钟预期错误，源差分本身通过；修正为精确 float 运算顺序。
- `model05`：715相关场景 PASS/0 FAIL、exit0。
- `model06`：独立复核的重复Start→非Spell原引用反例，0 PASS/1 FAIL（真实源与MP伤害数量不一致）；随后修正生产 raw Cast 消费。
- `replay01/02/03/05`：分别修正场地轮廓额外预览、凹场地外的求解起点、同动作AOE+Interrupt联合输出预期，以及Seek重建丢失故意重复回调的夹具；重复回调改为原会话提交后续世界帧。
- `replay04`：实际目录发现新七项默认未启用；生产登记精确ID，保留显式关闭优先级。
- `replay06`：真实第一组场地多轮廓触发旧适配器Single异常；生产改为保留local外轮廓/孔/三角填充数据的高度转换，模型逐点差分和实际DLL带17.25高度/方孔/圆孔/安全通路回归。
- `replay07`：十字一逻辑区域在真实注册表按既有适配拆成两个矩形句柄，夹具改为精确检查两句柄；模型仍与源的一项SDF逐点比较。
- `model08`：716相关场景 PASS/0 FAIL（47个Alliance专项），exit0。
- `build03`：0错4既有Crucible警告，11.18秒，exit0。
- `replay08`：42个Alliance专项加3公共 PASS/0 FAIL，exit0。

- `catalog01`：无筛选2045行首PASS/0FAIL，307入口独立编译、42联盟专项实际命中，exit0；包含加强后的精确raw非Spell时钟断言。
- 最终DLL SHA256：`45b6adc889aae2ab53f74ec82e8a0ab58df169edb547f887633486c97ce6af9c`，生产build03与replay08/catalog01一致。
- 生成器登记七个精确module/source/入口映射后生成CSV并执行`--check`：810 verified、403 pending、1 review、45 excluded，1214目标不变。

根SDK11、游戏实时采样、实战像素与执行仍待实机验证；本机用隔离SDK10.0.303，CI构建，不部署。

## 源码指纹

| 来源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A10Trash/A10Aquarius.cs` | `d3519581254d15da6357152e2377557a10f074fd9c0650dfdb85a1bd735d1586` |
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A10Trash/A10Despot.cs` | `5c45ad3a30efcae19252dd7f4417090f4ffd02736a0e0ec6919e924eefacd645` |
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A10Trash/A10Groundskeeper.cs` | `27a5ba6065243fbf61fffa7028018d0f2eff8e565498eaccc3dd9bfd7b802956` |
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A10Trash/A10VanguardPathfinder.cs` | `a5a15d45440a49db9b79db7194aa6ff573e13cd2624f91ac47bfd66dbbd0ff95` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A30Trash/A30Trash1.cs` | `a3eebb28eacb43b960631289ba9a77bed306efb14f82dc61d51c823075288268` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A30Trash/A30Trash2.cs` | `7b294897b2138ce3e8f663d010a069747930fe3e393db812b3d27a0da85ae905` |

## 桑多利亚源码指纹

| 源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A20Trash/A20Detector.cs` | `c2e4b422290e95632f37faa946293ef751e1f713044edf45dbbc57f2c39d16ef` |
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A20Trash/A20JaggedyEaredJack.cs` | `a4a224945ac93e195dc070ad382d7ecfdaa2ba38d2e8d29cd7ad30cf5742e764` |
| `MechanicRuntime/Upstream/Modules/Dawntrail/Alliance/A20Trash/A20Kraken.cs` | `1537bec06b9ffed39406614836d7a11681a04e5ce10628fc7081c8086d2c44d8` |

## 阿格莱亚双狮

入口 `masterplayer.alliance.lions`，T1054/CFC866、Primary OID 0x38DA、NameID11294。场地为真实148边凹轮廓减两个矩形，不用24.5圆替代。三个组件按 DoubleImmolation、SlashAndBurn、RoaringBlaze 顺序激活。

- DoubleImmolation 29143保留原Actor重复Start及当前raw非Spell/null默认钟，整队伤害仍按源判定钟排序。
- SlashAndBurn 的29136/29141/29376是R14圆，29138/29142/29377是6–30环；每次追加对全列表采用源右pivot非稳定排序，只发布首项。任意来源任一监视动作Finish都弹全局首项，不按来源匹配。
- RoaringBlaze 的29134/29135/29375是R50半角90度锥；超过一项才排序，只发布首项。Finish按caster ID在完整列表删首个匹配项，能够消费隐藏项及旧代际残项。与Slash独立，不统一队列规则。
- 缓存首个未销毁母狮原引用，不因同ID新生替换。框架先检查完成，再执行模块Update缓存；首次pull的原Primary已Dead且母狮缓存为空时直接完成。HP0、脱战及不可选中不代替源Dead/Destroyed。
- 600秒SimpleState没有超时转换，1000秒后仍为state0。故意不调用公共Timeline.Update的默认HP0死亡分支，完成时显式Stop。

日志前缀 `.tmp/halatali-verify/native-alliance-lions-20261125-`：

- model01：78联盟PASS；model02：747相关PASS；最终model03：749相关PASS/0FAIL，含80联盟模型专项，exit0。两队列19项混合/相同钟逐项源差分、隐藏晋升/跨source、原引用代际、冷热及null raw均覆盖。
- build01：0错4既有Crucible警告，9.35秒，exit0。
- replay01：76联盟专项加3公共PASS/0FAIL，exit0。实际solver、开关、owner、逆Seek、同区域及回放执行隔离通过。
- catalog01：无筛选2092行首PASS/0FAIL（2083测试加9汇总），311入口独立编译、76联盟专项，exit0。独立复核未发现剩余确定差异；建议的组合边界已加入最终模型验证。

验收DLL SHA256 `de7993bb588038c7d30051871f022d98a1fd24cac9aa7be375ed0c11d9501af7`。正式审计814 verified/399 pending/1 review，另45 excluded，1214目标不变。SDK11、游戏实时采样/最终像素及宿主执行待验，未部署。

| 源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10Lions/A10Lions.cs` | `8cb4e12c5aad7d242642221ecdf39fe2b41ca2d5b2b7384621ed33e7ca0f3202` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10Lions/A10LionsEnums.cs` | `a47d7a05a0610ff05ea6529fe161940b0d80921d090af3c4e871a8652c4e630a` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10Lions/RoaringBlaze.cs` | `38cc52bfb53c7f571b4bca19f4080dc0bf920ec9c3fec5c63ca0cd76efee45d7` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10Lions/SlashAndBurn.cs` | `bbfd256c8ef083b1bc8e95ddf803725ed43e33158f385a79b5571303ad8ef14d` |

## 阿格莱亚拉尔戈神使

入口 `masterplayer.alliance.rhalgr`，T1054/CFC866、Primary OID0x38FA、NameID11274。真实场地由同一 `--alliance-next-arenas` 导出，148边形减两个矩形；不以圆替代。客户端证据沿用 `.tmp/alliance-next-table-review/report.json`，28892视觉动作extra1秒，其余extra0。

六组件顺序为 LightningBolt、Boltloop、BoltsFromTheBlue、DestructiveStrike、DestructiveStatic、DestructiveCharge：

- LightningBolt28898为R6，DestructiveStatic28896为R50半角90度，普通WatchedAction Finish。
- Boltloop28893只从当前Spell Loc追加R10序列；28893/4/5 Finish分别推进R10、10–20、20–30阶段。非空序列先增加独立计数，再按阶段和X/Z各轴<=1寻找首项，与来源无关；未匹配也计数，空列表不计。两侧Rotation恒0，不放宽源0.05角差。后续钟为实际Finish+2d，发布缓存只依赖数量/版本。复用Earthbreaker，不用效果钟或超时消费。
- Strike28889复用DahuBait，R13半角60度，Start只在目标存在时锁原Source/Target；Finish不清，真实Effect先增独立计数、再按source ID删首项。Source Dead/Destroyed或Target Dead在Update清；Target仅Destroyed保留，Source/Target同引用时取source朝向。伤害为有效目标并集，钟取原列表首项。目标本人避队友R100锥，其他人避实际R13锥；本人轮廓、他人填充，不补额外选敌。
- Blue28900只有真实Effect计数，不制造RaidwideCast预测或全局提示。视觉28899仅由状态链观察。
- Charge在Map index0x25、state0x00080004/-45度或0x00100004/+45度时，每次追加两个R25半角45度反向锥；锁当时Primary量化位置，判定钟Now+16.1d。重复Map不去重；28891真实NPC Effect计数并清全部，空Effect仍计数；28890视觉、Finish或预测到时均不消费。冷激活不猜历史Map。
- 32状态以源SinglePhase为权威；CastStart接受任意当前raw Cast并诊断错误技能，CastEnd等raw为空；组件条件允许提前触发，未满足时expected+1秒逾期推进。ResetComp在退出时先删除再末尾冷激活，保留顺序变化，只重播当前NPC Spell，不重播历史Map/Effect。三次Charge、两次Blue及Boltloop重建均覆盖完整正常事件链及逾期链；永久10秒???没有超时退出。
- 默认Primary Dead/Destroyed/HP0完成，与双狮不同。Stop可能触发当前Reset出口，随后统一清理规则、Casts、Earthbreaker缓存和Dahu原目标，HP0/Director在Reset出口不遗留重播项。

日志前缀 `.tmp/halatali-verify/native-alliance-rhalgr-20261125-`：

- export01：五个真实场地导出，exit0。
- model01：测试类型名/基类访问编译错误；model02为9 PASS/1 FAIL，夹具移除了固定视角100而比较器仍索引100；改用101验证目标销毁保留，未改生产或降低断言。
- model03：759相关PASS/0FAIL。model04：11个本模块PASS，补完整正常技能链及全部原始Strike列表/计数逐项差分。最终model05：761相关PASS/0FAIL，含12本模块专项及Reset出口HP0/Director组合，exit0。
- build01：0错4既有Crucible警告，8.77秒，exit0。
- replay01：新增runner误占既有Quest同名文件导致编译错误；已恢复既有原文件并验证无diff，新增测试更名为NativeDungeonReplayAllianceRhalgr.cs。
- replay02：Map夹具使用了错误的Index/State字段；按PR真实Position/Param1/Param2序列化修正。replay03：双owner夹具初始玩家距另一首领超过500码未加载；按真实加载门槛先后靠近两场地，再保留双方活跃，原“两owner双向释放”断言不变。
- replay04：85联盟专项加3公共PASS/0FAIL，exit0；其中9项Rhalgr实际测试覆盖solver、普通AOE/三段圈、两向重复Map、真实Native Effect、自己轮廓/他人填充、HP0、冷历史缺失、双owner双向释放、逆Seek与回放执行隔离。
- catalog01：无筛选2102行首PASS/0FAIL（2093测试加9汇总），312独立入口、85联盟专项，exit0。独立静态复核未发现确定偏差，补强的正常链和清理组合已动态验证。

验收DLL SHA256 `baacbc6767fdf8489c3fb478a482e35e88e174cfad62b46c55ff73f90766cff2`。815 verified/398 pending/1 review，另45 excluded；SDK11与实时采样/像素/宿主执行待验，未部署。

| 源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10RhalgrEmissary/A10RhalgrEmissary.cs` | `a5a1718994b8c2e85e6d61b23d648b8ee31567178f3b0c11107d030646471d62` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10RhalgrEmissary/A10RhalgrEmissaryEnums.cs` | `fb95bdbacd015bedfd81ac4505066f9a2c22efc7bbe3497bfe1ce10dd585dc1a` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10RhalgrEmissary/A10RhalgrEmissaryStates.cs` | `33f865cf8d4444fca7816ebcb56e82dc7585c9f51a39aa39c1b35bbb7af423b9` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10RhalgrEmissary/Boltloop.cs` | `30a7804d85a0cecc3edd454f7ee9371c49db245d0813ca00fbbc5b4c38093c10` |
| `MechanicRuntime/Upstream/Modules/Endwalker/Alliance/A10RhalgrEmissary/DestructiveCharge.cs` | `edce511b54d45da30bc02c468ddf5a16b8bbcd16230a1eba8afeebab0cc7d98a` |


## 人偶军事基地重型陆战装置

入口 `masterplayer.alliance.heavyartillery`，T917/CFC736、OID0x2E83、NameID9656；客户端证据 `.tmp/alliance-artillery-table-review/report.json`。本批24唯一AID均存在且extra0；只核NameID→BNpcName和CFC↔Territory，不宣称OID→BNpcBase。实际激活顺序为ArenaChanges、Energy、ManeuverHighPoweredLaser。

- 初始场地按模块构造时Primary Z选择：Z>-262为center(200,-216.25)/半宽11.5半高44.25；否则Z>-362为center(200,-316.532)/半高44.468；其余center(200,-416.5)/半高44.5并减Z=-405/-435的半高1横栏。后者由源几何确定性导出为多轮廓，不用外包矩形代替。
- Map只判断index：0D切3b只留-435横栏，0E切3c半高44.468矩形；任意state都生效，保留原Arena.Center。完成按Bounds身份：初始Arena1只等Z>-262的Boss，初始Arena2只等Z>-362的Boss，其他Bounds等全部Boss；Dead/Destroyed完成，HP0不够。Pull为任意可选Barricade或任意combat Boss。
- 管理器同OID只有一个Loaded，其余模块已构造但保持pending。每个原Actor单独冻结初始stage；晋升不按当前位置重建。inactive超500卸载后重新构造，后续pending移动仍不改重构值；已Loaded同ID换代只重绑原模块，Map与Center保留；pending同ID换代构造新模块。Director只清当前Loaded构造态，完整Clear才清所有pending。
- Laser20657为长90半宽4矩形，Grouped时间窗只取列表连续前缀，Activation>=首钟+1秒时立即break，不跨过隐藏项找更早钟。98项保插入序，99起按全列表非稳定右pivot排序。任意Action Finish按caster ID删首匹配，不猜点名目标。
- Energy仅由原Actor Created/Destroyed/20658 Effect增删，无Dead过滤、无TTL。显示当前raw位置/朝向的R1/L3 capsule；导航分为当前R1圈union和查询钟Now+1.1d的R1.5/L3 capsule union。两钟分开发布，无墙障碍；source SDF逐点精确差分，MP只在输出适配为有限网格。
- 源默认Activation表示立即；模型保留default，公共Bozja输出适配在注册时换为CurrentTime，避免真实solver预备缓冲从DateTime.MinValue下溢。这一处公共修改由同DLL全目录覆盖原Bozja/CLL/BA/联盟消费者。未改核心solver或扩大动作执行。

日志前缀 `.tmp/halatali-verify/native-alliance-heavy-20261125-`：

- export01成功导出两个真实多轮廓场地，exit0。model01为10 PASS；model02中pending冻结为真实生产失败，另一个“两同OID同时Loaded”断言误解BMM的pending排斥规则，按源修正夹具；model03保留该错误断言的失败。未以活跃实例池扩大实现。
- model04为773相关PASS。model05为15 PASS/1 FAIL：新增pending换代夹具只改ObjectIndex，但源ReplayFeedAdapter丢弃ObjectIndex且MechanicWorldFeed未执行Destroy/Create，造成输入不等价；model06保留其诊断失败。改为两侧真实remove/add帧后，model07为777相关PASS/0FAIL，含16本模块专项，全部exit0。
- build01为0错4既有Crucible警告。replay01前两个Heavy专项通过，Energy真实solver触发默认立即钟DateTime下溢；保留异常日志，修复输出适配。
- build02为0错4既有警告；replay02为94联盟专项加3公共PASS/0FAIL，9个Heavy实际DLL专项覆盖真实solver、三场地Map横栏/保中心、Energy两钟/raw移动/死亡/无TTL/原Effect/Destroy换代、冷激活与历史缺失、开关/owner/逆Seek与回放禁执行。

无筛选catalog01为2112行首PASS（2103测试加9汇总），313入口独立编译，全部exit0。验收DLL SHA256 `551873c4705bc6ac04314a42df6bc4003397484abb71878e922cbbcddafaf155`。816 verified/397 pending/1 review，另45 excluded；轻型仍pending。SDK11及实时采样/像素/宿主执行待验，未部署。

| 源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Shadowbringers/Alliance/A20Trash/A20HeavyArtilleryUnit.cs` | `0690ba0c25a3240805098581a28fd6b7991a2975abb43c99b37f8c5a0fd305e0` |


## 人偶军事基地轻型陆战装置

入口 `masterplayer.alliance.lightartillery`，T917/CFC736、OID0x2EE4、NameID9652。核表沿用 `.tmp/alliance-artillery-table-review/report.json`。七组件顺序FireVoidzone、SurfaceMissileImpact、HomingMissileImpact、LongBarreledLaser、VoltArray、ManeuverMartialArm、InitiateSelfDestruct；正方场中心(-230,-15)，半宽24.5。

- Fire保留源懒查询Enemies列表：首次查询初始化，后续Created尾插、Destroyed按原引用删除；不重新枚举Dictionary空槽。OID1E8D9B且EventState!=7，R6，raw位置/朝向，无Dead过滤，显示与TemporaryObstacles独立，无预测伤害。
- Surface21007圆6，Laser21010长70/半宽4，普通WatchedAction Finish。Volt21009带caster名字的Interrupt，不生成Raidwide。Martial21011为Global Tankbuster，原caster当前raw目标、自目标fallback、NPC动态钟。
- Homing21008共享原生CircleBait（美德AuroralWind同步消费并已全目录覆盖）。centerAtTarget=true/R6；默认允许dead target，只过滤source.Dead；原source/target引用及全列表首钟保留，Effect只计数。绘制own按ID，导航own按原引用；source==target跳过基类避队友；own额外即时边缘矩形中心正Z、前后及半宽23。普通Finish只删首source ID匹配项。
- 自毁21013只计Effect，恰好3结束；同批4次不满足。其他情况等全部Boss Destroyed，Dead/HP0不足。普通Primary targetable+combat拉战。
- 模型逐事件比原BMR列表、原target代际、首钟、颜色/风险、SDF/导航和伤害。实际DLL覆盖双视图、真实solver、回放禁执行、设置开关/owner、反向Seek及同T917重型/轻型双owner双向释放。

日志前缀 `.tmp/halatali-verify/native-alliance-light-20261125-`：

- model01两处失败定位Martial缺GlobalHint，已按源补齐；model02为791相关PASS；model03新Fire缓存顺序专项真实失败，修复后model04为792相关PASS/0FAIL，含15本模块专项，exit0。
- build01/02均0错4既有警告；replay01反射字段误写Obstacles，改为实际HardObstacles；replay02在四次同批Effect失败，原因ReplayPlayback对每条事件立即Update，第三次后已退出。专项改用实际router.Raise排入整批并断言pendingEvents未消费，再调用真实UpdateReplayScripts一次，前进用DispatchUpTo保会话，未改生产精确等号。replay03全部通过，包含10轻型实际专项。
- 同一build02 DLL的无筛选catalog01为2123行首PASS（2114测试+9汇总），314入口独立编译、104联盟专项，exit0。DLL SHA256 `f61602bf095ade261782ba1b84d77c80606eb9449bf3485aba8979fc5b1276a1`；正式817/396/1，45 excluded。SDK11和实时采样/像素/宿主执行待验，未部署。

| 源 | SHA256 |
|---|---|
| `MechanicRuntime/Upstream/Modules/Shadowbringers/Alliance/A20Trash/A20LightArtilleryUnit.cs` | `d929297582546f7d52806fa7b89189d6fd921c8317a1926908dce545981d795c` |


## 联盟标准组件七模块批次

这批配置来自逐组件继承/override和真实状态机审查，不由类名自动推断。生产入口均为 `masterplayer.alliance.<slug>`，共享原生 `BozjaEncounter`；生产不创建BossModule或MechanicSession。审定manifest和客户端report在 `.tmp/alliance-standard-pilot/`；report的7项、50唯一AID均存在且extra0，OID来自源枚举，不宣称已核BNpcBase关联。源SHA见manifest。

| 源模块 | slug | CFC | OID | 实际组件 |
|---|---|---:|---|---:|
| `A13Cuchulainn` | `cuchulainn` | 120 | `0x1413` | 7 |
| `A22Forgall` | `forgall` | 168 | `0x1627` | 6 |
| `A23Headstone` | `headstone` | 168 | `0x166F` | 3 |
| `A32FerdiadHollow` | `ferdiadhollow` | 220 | `0x1928` | 17 |
| `A33ProtoUltima` | `protoultima` | 220 | `0x1967` | 8 |
| `A35Diabolos` | `diabolos` | 220 | `0x1907` | 5 |
| `A30Trash` | `paradigmtrash` | 779 | `0x3193` | 4 |

- T508/556/627/966分别对应CFC120/168/220/779。除A30Trash要求OID3193/3194全部Destroyed外，其他沿默认Primary Dead/Destroyed/HP0完成。全部TrivialPhase不因12000秒超时完成。源WIP仅在测试oracle临时允许，Dispose恢复；未改运行库成熟度。
- AOE按WatchedAction普通Finish和casterID删首，无TTL；Charge用raw Loc-caster方向/长度与量化原点，锁定原形状，不套Cll3的排序/延迟。分摊三种均min8/max无限、RaidwideOnResolve=false，原target保代际，Finish按TargetID删首并计数。Raidwide/Tankbuster用原caster当前raw CastInfo和动态float NPCClock，保默认/非Spell、零Total和Party提交序。Gaze普通Finish、Loc锁眼和10000范围。A30 LightfastBlade前二可见、首项Danger、visible均Risky、hidden按插入顺序晋升；Effect仅计数不删除。未激活类不补造。
- Forgall/Headstone调用真实BuildArena导出完成并差集后的relative polygon。Headstone源 `AdjustForHitboxInwards=true`是固定-.5偏移，已在导出轮廓内，不额外按玩家hitbox缩小。局部 `AllianceArenaGeometry` 保源binary32 bbox、逐行hull、候选边、EPS水平查询方向、scalar与SIMD/FMA跨度；AtHeight保relative坐标，不加中心后再重建。显示和规划同一几何。既有MP raster保守cell规则仍适用，不宣称原BMR寻路网格等价或所有owner/回放均零额外margin。
- 实际DLL揭示ownStack的SDIntersection补集被公共65%采样阈值丢弃。仅本批ownStack复用既有 `BozjaVirtueTowerGeometry` 的精确SDF、补集轮廓和线段判定，保留队友重叠安全内部；不扩大其他已验消费者。非ownStack沿既有MP适配发布Goal circle union。模型逐事件源SDF仍独立于适配几何；目标/非目标均经实际注册表和solver。这个修复明确修正旧桥采样缺口，不宣称旧桥输出逐点等价。
- 实际DLL覆盖50规则冷热/Finish、显示/真实solver、三分摊双方输出、七生命周期、无Party0、开关/逆Seek、同T627两活跃owner双向释放及回放PR执行隔离。直接solver使用显式SolveOptions不作为自动移动门控或全部模式margin设置的证明；回放不执行动作。

证据前缀 `.tmp/halatali-verify/native-alliance-standard-20261125-`：

- model01源WIP未加载、model02基类Polygon访问编译失败均已修；model03两个真实角点FAIL后修relative Contains，model04为873相关PASS。独立复核补五条row hull反例，model05真实2FAIL；补逐行数据和候选边后model06为873相关PASS/0FAIL（81本批），exit0。
- arena-avx2-02实际上打印AVX512F=true，不算AVX2分支证据。修正控制键为EnableAVX512后，arena-avx2-03打印False/True/True；arena-scalar-01打印False/False/False，各真实命中2场景且exit0。arena-nofma-01的EnableFMA控制未生效，仍打印False/True/True，不作为AVX2非FMA分支证据；该组合未运行。默认分支AVX512F=true亦有日志；没有把环境变量设置本身当作分支通过。
- replay01–04在首个分摊navigation处失败，replay04直接确认模型1项而实际0项。没有更改为接受0项；生产局部修补集后replay05为68本批实际专项、104既有联盟及3公共PASS，exit0。大于场地的FlareStar夹具用合法偏心Loc保证真实存在安全点，未缩小R31。
- build02为0错4既有Crucible警告，9.44秒；同DLL无筛选catalog01为2198行首PASS（2189测试+9汇总）、321入口独立编译，exit0。DLL SHA256 `4750326503f1cd2d11bb711fa25805925f0c47f5af792ee0420a8f8b32b5b3a7`。正式824/389/1，另45 excluded；实机、SDK11待验，未部署。

试点耗时记录：旧阶段没有完整人工计时，不能补造审查/编码/验证/返工的独占工时或加速比。按已落盘文件mtime间隔，manifest→生成Catalog约4分20秒；首轮model01结束→model06结束约16分50秒，包含并行审查、验证和返工，不能拆开相加。全目录与分支核验另见日志。七模块仅为首段，后续三个击退变体合成十模块试点时继续记录实际边界；数量不替代语义验收。


## 三个标准击退变体与十模块试点收尾

`AllianceKnockbackCatalog` 的三个原生入口为 `masterplayer.alliance.cetus/echidna/calofisteri`，
对应CFC120/T508的刻托斯（OID13F7/NameID4613）、艾奇德娜（13F3/4631）和CFC168/T556的卡洛菲斯提莉（1661/4897）。
共29实际激活组件、27唯一AID，客户端report全部存在且extra0。审定规则、源哈希和report在 `.tmp/alliance-knockback-pilot/`。
卡洛菲斯提莉场地通过真实BuildArena导出，延用relative Polygon精确查询；不以近似圆代替。

- 复用已审普通AOE、Charge、Raidwide/Tankbuster、Gaze。三个SimpleKnockbacks只发布前景位移线和源文本：
  源没有AddAIHints覆盖，因此不补KB导航或自动位移。同AID的独立R14 AOE继续规划。
- Cetus5160击退30、Echidna5144击退5均stopAtWall，Calofisteri6178为50码pull且MinDistance0。
  Start锁Loc/rotation/NPC时钟，重复按插入顺序逐条从上一终点计算；Finish按watched AID和casterID删首，
  包含同ID不同原Actor对象，无TTL；Effect只计数不删记录。
- 原圆RayCircle二次式与hitbox clamp保运算顺序；裁剪后不二次过滤负距。pull保normalize和乘加，不直接赋origin。
  源对小于.5的hitbox可能Clamp异常，未擅改源数学；有效玩家夹具使用.5/1/3，64方向逐端点精确相等。
- 三免疫类（3054/160/1209，1722/2663，2345）、严格expiry>activation、60真实槽、同类覆盖/Lose清整类、
  旧Party槽热提交顺序和冷热Pet过滤均源差分。冷激活白名单此前漏新kind，model02暴露后补齐。
  原始ExpiresAt经真实float remaining-time输入重编码会改变ticks；model03的失败由精确比较日志确认，
  物理Feed对照保留，另在两侧真实callback显式同钟验证严格等号，不放宽容差。
- 四个Raidwide/Tankbuster逐项验证原Actor引用、当前raw非Spell/default/零Total以及双来源不同raw时钟排序，
  过结束钟后仍保源动态预测与独立Finish。外来同ID Finish、无关AID、Effect计数均有独立断言。
- 实际DLL38本批专项覆盖29规则、三生命周期、三非Party及三slot59免疫；真实发布位移线逐端点对模型、
  同AID AOE保留、KB没有额外danger/goal，开关/Stop/Finish清线，真实solver、逆Seek与回放禁执行。
  同T627双活跃owner公共消费者仍命中；未另宣称新增T508双KB并存专项。

证据前缀 `.tmp/halatali-verify/native-alliance-knockback-20261125-`：
model01有效hitbox夹具缺失、model02冷免疫接入缺陷、model03物理float重编码等号夹具、model04 readonly构造编译均保留失败日志。
model05为914相关PASS/0FAIL（41本批），最终model06新增独立核验建议后55专项PASS/0FAIL。
build01为0错4既有Crucible警告/8.31秒；replay01为213 PASS（38本批+68标准+104既有联盟+3公共）；
同DLL无筛选catalog01为2239行首PASS（2230测试+9汇总）、324入口独立编译/38本批实际专项，全部exit0。
DLL SHA256 `04473bffa66e1146cc3c4d77016d220ce4d5ac730dc4ee8171c6338f5e7bc69d`。正式827/386/1，另45 excluded。SDK10.0.303验证，SDK11、实机采样/像素/执行待验，未部署。

七标准加三击退变体共十模块79实际组件构成首个试点。确定性接线与配置复用后，差分和实际注册表仍抓到真实生产缺陷；
未把生成入口或通过开场作为验收。审查/编码/验证/返工交叠，缺完整独占人工计时，不补造阶段工时或加速比。
后续转入动态场地、双Boss或定制机制，保持按真实激活和依赖划分批次。


## 禁忌城邦玛哈·奥兹玛

入口 `masterplayer.alliance.ozma`，T556/CFC168、OID1560、NameID4896。
`.tmp/ozma-pilot/report.json`核对1模块、3唯一AID全部存在且extra0；源文件SHA见同目录source-manifest.json。
实际只激活ArenaChanges、MeteorImpact、HolyKB、Holy、ExecrationAOE、AccelerationBomb六组件，不补未激活机制。

- Meteor6453为R20，Execration6185为长40.5/半宽5；普通watched AID+caster ID Finish删首，无TTL。
  Holy6190分别持有独立SimpleKnockbacks和Raidwide队列，KB Away3只发布线/边界文本，不加导航。
  伤害保原Actor及当前raw NPC浮点钟；Effect不替代Finish。
- ArenaChanges按24个Party槽存历史，共用一个Arena；换人不清槽，槽值相同不重置其他槽已经改变的场地。
  Split区域(300,260)±(34,19)，Shade(301.5,205.5)±31.5，两区域无重叠；初始区(280,-404.5)±40。
  三套场地调用源真实BuildArena及两个实际Handle方法导出：Initial 2轮廓/433三角形，
  Split 3轮廓/72三角形，Shade 2轮廓/125三角形。Split固定inward已经包含在导出数据。
  公共AllianceArenaGeometry扩展为多轮廓同一全局row/EPS/FMA奇偶索引；原单轮廓调用保留。
  RadarArenaLayout、RadarView和HostDrawingGeometry使用生成三角形填充，避免把孔填实；AtHeight不重算relative数据。
- Framework先准备AI及旧场地导航，再Capture首次Hints清死亡槽、Draw背景改变场地，最后公开第二次Hints；
  新场地导航下一帧发布。双视图休眠不运行两次Hints或背景切换。
  事件Render仅查询不消费Ozma死槽，保证唤醒时Framework仍先获得Pyretic。
  SID1072按Party槽Gain覆盖/Lose清除；过expiry不自动删。真实60槽公共输入与冷热Pet、旧Party热提交顺序均保留；
  Bomb定向测试重点为槽59与槽0，未宣称逐60槽单独枚举。
  Stop everything默认1000秒阈值，Risky保源PrevPosition自比较的NaN、raw Cast和TargetID条件。
  仅保Stay/Pyretic诊断，不新增动作执行。
- 完成要求当前Primary Destroyed AND（Party0在两个完成方形外 OR CFC!=168）；Dead/HP0不够。
  源Manager同OID同InstanceID ActorAdded会重绑Primary；不能继续用旧销毁Actor提前完成。
  缺Party0时Manager不Update。CFC复用LiveWorldSampler.ResolveCfc，真实客户端表验证回放168；
  实时分支静态共用此调用，录像不含独立CFC字段，不宣称动态CFC录制恢复。
  真实Feed的CFC改变先触发ZoneInit卸载模块，模型CFC谓词专项采用初帧override，不伪造保留模块的动态变更。

证据前缀 `.tmp/halatali-verify/native-ozma-20261125-`：

- export01漏csproj链接、export02源Config未初始化，均保留真实失败；export03初始化后导出成功。
- model01发现错误Primary旧引用保留及夹具CFC缓存不一致；前者撤销生产例外、后者改同一初帧输入。
  model02为151 PASS，model03含ColdPet为391 PASS。model04未传FFXIV_SQPACK失败；
  model05设置真实数据路径后391相关PASS/0FAIL（16专项），exit0。
- build01为uint到ushort编译错，build02修正通过；事件Render消费修复后build03为0错4既有警告/8.75秒。
- replay01缺jsonOptions编译失败；02未注入真实Data服务导致CFC0；03–05夹具施法Loc与玩家重合，
  源SimpleKnockbacks零方向不生成线。修正Loc，不改变期望。
  replay06–07的单帧断言误用Seek多次Update，改真实World.Apply+单次生产OnUpdate；逆Seek断言保留。
  replay08九专项通过，replay09新增同T556双owner并覆盖公共CLL1/3/4及Owain，共311 PASS/0FAIL（10奥兹玛），exit0。
- 同DLL无筛选catalog01为2250行首PASS（2241测试+9汇总）、
  325独立入口和10奥兹玛实际专项，exit0。DLL SHA256 `3d8aca4b48490ab17958771c12619e84255d5333cb6a37026235dffe82ebb7ab`。

实际DLL覆盖三AID冷激活/Finish、三套边界与world三角形、真实solver不入孔、slot59免疫Lose及独立伤害、
Bomb死亡两查询/准备钟/休眠复活/保持死亡事件唤醒、缺Party0、自定义完成、双owner双向释放、开关/清理/逆Seek和回放禁执行。
Primary同ID换代证据为源差分模型，没有冒称实际runner也覆盖该分支。
正式828 verified/385 pending/1 review，另45 excluded；SDK10.0.303已验，SDK11和实机采样/像素/执行待验，未部署。


## 魔航船虚无方舟·天柱树（Irminsul/Sawtooth）

入口 `masterplayer.alliance.irminsulsawtooth`，T508/CFC120、OID13F1、NameID4623。客户端证据
`.tmp/irminsul-pilot/report.json` 覆盖7唯一AID，extra全部0；3源码SHA见同目录source-manifest.json。
只迁WhiteBreath/MeanThrash/MeanThrashKnockback/MucusBomb/MucusSpray/Rootstorm/Ambush/ShockwaveStomp
八实际组件；注释掉的AmbushKnockback不补。5208 R28/总120锥，5209 R12/总120锥并独立Away10 StopWall，
5205 R10分散，5472 Donut6–20，5212群伤，5172 R9，5213 LOS70。普通watched Finish消费，不改成Effect。

- Primary默认Targetable+Combat激活，Dead/Destroyed/HP0完成，Sawtooth活着不阻止；非“双Boss全死”。
  动态伤害保当前raw NPC float钟、原引用和Party序。60真实状态槽免疫沿独立KB合同，无KB导航。
- 场地调用真实源Donut8–35正规化结果导出，2轮廓/90三角形，不用解析圆替代墙查询。
  原row索引射线保四组边顺序、padded首次与后续新边、DDA、dx=slope*dy、512/256/scalar FMA差异、
  scalar收紧u、共线括号和尾段入口冻结上界；scalar直接赋商保可增1ULP的源行为。
- LOS caster是原Actor列表，当前raw CastInfo（含非Spell）RemainingTime严格最早者决定Origin；
  safe snapshot是独立FIFO，仅显示首项。Start先Add/Refresh后以incoming Activation建冻结影区；
  Finish原引用Remove一次/Refresh/全局safe出首，不按caster匹配safe；Effect无消费。
  blockers缓存当前同OID原Actor列表，Created/Destroyed维护；只滤Dead，不滤HP0或Targetable，
  Refresh只发生Start/Finish。Load及full Clear置空cache；Director清组件不额外改敌人缓存合同。
  专项覆盖重复残留caster变非Spell、外来原引用Finish、销毁非Spell旧代和同ID新代并存。
- Custom polygon SDF按源5码SpatialIndex候选边计算（空格保float.MaxValue开方），避免全边最近距离替代。
  该模式只为天柱树启用，CE33/41/54等原消费者仍走原路径。SafeFromAOE颜色和Risky=true保留。
  提示仅在不在safe且到当前Origin距离严格小于70时出现。
- 实际DLL发现旧65%填充采样将LOS导航整块丢失。新增限定IrminsulSightDangerGeometry直接保
  ForbiddenDistance<=epsilon，不把源零值当空区；以SampleRadius+1圈限栅格范围，完整safe contours做Clipper差集。
  此圈不是Dodge余量；执行层仍使用ExactDangerSnapshot/NavigationOnly及solver统一膨胀。
  默认64边外轮廓完整覆盖正式R35场地；外界解析圆与其内接多边形在约35.9566–36区间有接口近似，
  不宣称场外或任意低segments逐点一致。场内逐点SDF分类、暴露玩家真实solver绕孔逃影区均通过。
  线段另验两端安全中段危险和整段安全，margin0/.3/.5；不将其称为任意正余量的形式证明。

证据前缀 `.tmp/halatali-verify/native-irminsul-20261125-`：

- client01/02及export01 exit0；model01两FAIL分别为错误统一Casts断言和源SpatialIndex距离差异，
  修双列表断言并加限定索引；model02漏using编译失败，均保留。model03为16专项PASS。
- 独立复核纠正射线scalar赋商及尾段上界，model04为455相关PASS。
  新直接补集model05错误假定影区会形成内部洞，实际影区贯穿外界，应为裁剪轮廓；以完整分类和真实segment检查验证，
  model06为17专项PASS，model07为457相关PASS/18专项，model08追加线段两类及三余量后1真实命中PASS。
- arena-avx2-02和arena-scalar-02各6真实命中PASS，分别INFO False/True/True和False/False/False；
  默认model07为True/True/True。arena-avx2-nofma-01虽exit0但INFO仍FMA=True，不能计入非FMA分支。
- build01/02通过；replay01发现默认启用漏登记；replay02/03证实LOS AOE1/danger0，未放宽为零输出。
  修默认登记及限定直接补集后build03为0错4既有警告/8.95秒。replay04为12专项+3公共PASS，
  包括7AID冷激活/Finish、圆环孔洞/world三角形、exposed真实solver、免疫槽59Lose、缺Party0、Primary-only完成、
  开关/逆Seek/owner清理/同T508 Cetus双owner双向释放和回放无PR执行。
- 同一DLL无筛选catalog01为2263行首PASS（2254测试+9汇总），326独立入口/12本批专项，exit0。
  公共CLL1/3/4、Owain及CE33/41/54也在无筛选目录执行。DLL SHA256 `616b8f1d8979baf1d7d97e262bca3747c92795e84e9d50ed723515394bdc7718`。

正式829 verified/384 pending/1 review，另45 excluded。SDK10.0.303离线验证；SDK11、
实时采样/像素/原生执行仍需实机，未部署。Director重新Load专项未单独覆盖，不将静态审查当动态通过。

## 禁忌城邦玛哈·阿剌克涅

入口 `masterplayer.alliance.arachneeve`，T556/CFC168、OID1637、NameID4871。
`.tmp/arachne-pilot/report.json`核对11唯一AID，extra全部0；5源码SHA见同目录source-manifest.json。
十实际组件按源顺序为SpiderWeb、DarkSpike、SilkenSpray、ShadowBurst、SpiderThread、FrondAffeared、
TheWidowsEmbrace、TheWidowsKiss、Pitfall、Tremblor，不补枚举中未激活的动作。

- 普通组件复用已审标准：6179单体伤害及全局Tankbuster提示；6180 R24.5/总60锥；
  6200分摊R6/min8、6201分散R6；6202凝视默认10000；6280 Pull18和6210 Pull4两独立StopWall组件；
  6181实际R20，不按客户端描述扩为R60。普通WatchedAction Finish删首，原Actor和动态raw NPC钟保留。
  两KB没有额外导航或自动执行，60状态槽免疫/旧Party/冷热Pet/原引用Finish沿既有合同逐项源差分。
- SpiderWeb每24个Party槽一个bool，共用一个Arena；只在固定(16,-55) R30内，
  严格Y>10从false变true、Y<10从true变false；恰10、圆外、当前槽值不变均保历史。
  不绑定Actor身份；不同ID换入同slot继承旧历史。死亡viewer也运行绘图，导航仍按死亡门控。
  冷激活不从高度、状态或Icon补造历史；只有Framework背景绘图消费变化。
  两视图休眠不切场，单独唤醒任一视图在Framework切场，事件Render不额外触发背景。
- 真实源BuildArena/BuildSpiderweb导出Ground为1轮廓/84三角形，
  Web为18正规化轮廓/467三角形，非24个独立孔。中心与半径使用源正规化结果。
  首帧先发布旧Ground导航，再切Web背景并按新墙绘制KB；次帧导航跟上。
  实际fixture明确验证Ground/Web端点不同；端点发布对模型，墙求交正确性另由独立源模型差分支持。
  真实solver由R20危险圈迫使移动，逐路径顶点Contains并以每段Raycast检查不穿孔。
- Tremblor只有6199 Start创建序列；冷6198/6197不造首段。
  Finish按阶段及位置找首项，不按caster；队列非空时错误位置仍增NumCasts，未知action不增。
  下一段钟为Now+2d，形状为Circle10.5、Donut10.5–20.5、Donut20.5–30.5；
  仅Update发现Count/NumCasts变化后更新发布缓存。Effect、超时不推进或删除。
  多序列、跨来源Finish、位置错误、缓存发布前后均有源差分。
- 默认Primary Targetable+Combat激活，Dead/Destroyed/HP0完成。
  Director清槽/序列/输出，不在解锁时复活retired Primary；新Primary Load恢复Ground，
  清历史并按源ActivateComponent冷回补仍存在的施法。实际专项验证新LoadVersion、
  恢复R10.5首圈/原点/将来判定钟/Num0，不把旧NPC当前cast误当残留队列。

证据前缀 `.tmp/halatali-verify/native-arachne-20261125-`：

- client01、export01均exit0。model01的2FAIL为DarkSpike遗漏GlobalHint，另1FAIL是Ground射线漏水平切边。
  model02及arena-scalar01复现同一射线失败；独立核验定位源少于8条水平边从头扫描，
  本地无条件Y下界过滤因2e-6量级舍入剔除合法命中。修为<8从头、>=8 lower_bound，保源t检查。
  model03为18专项PASS；model04为132相关PASS，最终model05为345相关PASS/18专项、0FAIL。
- arena-avx2-01的环境参数未生效（INFO仍True/True/True），不计AVX2覆盖。
  最终arena-avx2-03及arena-scalar02各3真实命中PASS，含阿剌克涅和天柱树射线，
  INFO分别False/True/True和False/False/False；默认model04为True/True/True。
- build01为0错4既有Crucible警告/8.54秒。replay01错误期待新Load后零AOE，
  源激活回补证明旧NPC仍6199，改为精确检查新Load冷恢复首圈；replay02含既有联盟共255 PASS/20本批。
  独立复核后加强真实移动路径段、切场端点区别与Director新Load断言；
  replay03内部Raycast动态绑定失败、replay04集合表达式编译失败均保留，
  最终replay05为20本批加3公共PASS/0FAIL，exit0。
- 同DLL无筛选catalog01为2275行首PASS（2266测试+9汇总）、
  327入口独立编译及20本批实际专项，exit0；其后加强断言由replay05验证。
  公共CLL1/3/4、Owain与CE33/41/54等均在无筛选目录执行。
  DLL SHA256 `04864146a5271022022540f0d075b7b28931dc731bac9fd6a7603fa77f6118f1`。

正式830 verified/383 pending/1 review，另45 excluded；源类型数与327独立入口分别计数。
SDK10.0.303已验，SDK11、实时采样/像素/原生执行仍待实机，未部署。

## 虚空死亡凝视（A31DeathgazeHollow）

T627/CFC220，Primary OID0x190F，NameID5507；客户端核表 `.tmp/deathgaze-pilot/report.json` 的9唯一AID均存在且ExtraCastTime为零。只迁实际9组件；注释掉的BoltOfDarkness3不加入。固定矩形中心(300,410)、半宽30/半高15、SampleRadius30；默认Primary Targetable+Combat启动，Dead/Destroyed/HP0完成。

绿色图标70使用事件Actor当前slot，7308有效NPC Spell Effect用MainTarget当前slot清位；Player/Pet等由源BossModule过滤。mask不按Actor身份保存，不随死亡/超时清除；换人继承slot历史。源Hints仅活着0..23槽、排除viewer本人，R5含边界；前景取活着0..63，绿色outline本人2/他人1。紫色mask源无写入，不补造。圈不参与导航/伤害预测。显式24..63源PartyState通过测试补录，旧Feed本身仍只8槽；实时CaptureParty提供0..23，不能宣称实时友方NPC64端到端验收。

灰色R0.5点作为MP的Interesting优先级显示适配；使用源192/255灰色、跳本人/不可选中/Destroyed/死亡，场外夹到矩形墙。它不等于源Actor箭头像素，且不进入导航。真实preview及共享雷达/世界Style厚度、无填充已验，游戏像素仍待实机。五个SimpleKnockbacks分别保持列表、原Actor、真实60状态槽免疫和WatchedAction Finish；左右使用锁定施法方向。RayRect按源RayAABB的比较次序保留平行NaN与signed zero，覆盖世界边界相邻float；不新增源没有的击退导航或位移执行。

日志前缀 `.tmp/halatali-verify/native-deathgaze-20261125-`：model05 23专项PASS；model06 354相关PASS；model07同帧Party/事件1PASS，均exit0。build01 0错4既有警告/9.23秒；DLL SHA256 `a3500d4f891e26b45ee5f7ad4abf9f22703e62728bbc5b306992e4f27989a3c0`。replay02 19本批+3公共PASS；最终catalog02无筛选2304行首PASS（2295测试+9汇总）、328入口独立编译，exit0，含加强的公开Hint/slot发布顺序。真实solver普通AOE脱险、冷Icon、Director retired/fresh Load、双owner双向释放、禁用、逆Seek及回放禁执行均覆盖。replay01夹具超500码、catalog01反射新增可选参数缺项的真实失败已保留并修复；model01..04失败历史保留。源哈希与验收映射见 `.tmp/deathgaze-pilot/source-manifest.json`。SDK10.0.303，本机SDK11与游戏实机未验，未部署。
