# 扎杜诺尔高原原生机制

## 范围与区域隔离

源为 `MechanicRuntime/Upstream/Modules/Shadowbringers/Foray/CriticalEngagement/`，当前CE51ThereWouldBeBlood十组件、CE63WornToShadow五组件、CE52TimeToBurn四组件、CE41WithDiremiteAndMain十组件、CE44FamiliarFace九组件、CE62LooksToDieFor十一组件、CE53HereComesTheCavalry十四组件、CE54NeverCryWolf十一组件、CE64FeelingTheBurn八组件、CE42FromBeyondTheGrave十五组件完成离线验收。入口 `Presets/Foray/Entries/Zadnor.cs`，GUID `masterplayer.foray.zadnor`，模型复用 `Presets/Foray/Bozja/` 原生组件；生产不创建BossModule/MechanicSession。客户端真实表双向核T975↔CFC778，名称「扎杜诺尔高原」，DynamicEvent24「异界废王——汉比」，主OID0x319A来自源枚举。所有源AID CastTimeExtra本机核零，不把注释或visual当实际组件。

Definition显式Territory975，Region只构造本区模型，GetMechanisms只列本区，独立GUID隔离owner和持久开关，Owns必须同时匹配Territory与PrimaryOID。975放南方主怪不会激活南方模型，920不接管CE51，Soul及未迁CE不在ownership；BozjaScript类型继续保留全区域压桥例外，只在精确完成模块处禁桥。两区入口空配置默认启用，显式关闭与刷新仍保留，两区设置互不影响。本轮同时修复南方旧入口漏登记默认启用的问题，不以专项显式Enable代替默认验收。

## CE51 标准家族与普通凝视

固定中心(-390,230)、真实Circle R25，pull要求源玩家在R25且Primary Targetable/InCombat；默认DeathPhase按Primary Dead/Destroyed/HP0结束，离战不额外完成。500码加载与pending、原Actor代际、主怪同ID重绑、冷激活逐组件遍历世界NPC及热事件输入沿用Bozja已验合同。完整真实Feed对照10组件冷激活、无新Actor输入下一Framework、原始Finish和全部输出清理。

| 激活顺序 | 组件/动作 | 几何或输出 |
|---|---|---|
| 1 | CloudOfLocusts23568 | Circle R15 |
| 2 | PlagueOfLocusts23569 | Donut6–40 |
| 3 | DivestingGale23570 | Circle R5 |
| 4 | Camisado23469 | 原caster当前Target动态Tankbuster诊断，全局提示 |
| 5 | DreadWind23470 | 原caster动态NPC钟、活Party mask、Raidwide诊断，全局提示 |
| 6 | GaleCannon21475 | 前长30/后0/全宽12 |
| 7 | FlightOfTheMaleficCone23579 | R30、全角90° |
| 8 | FlightOfTheMaleficCenter24322 | Circle R6 |
| 9 | TempestOfAnguish23572 | 前长55/后0/全宽10 |
| 10 | TragicalGaze23573 | 普通CastGaze，range10000、非反转、Forward0、全部Eyes |

七种SimpleAOEs Start锁spell.LocXZ/rotation/NPC判定钟，按回调追加，Risky=true/Color0，watched Finish按casterID删首，Effect/cancel/timeout不清。重复Start和不同caster异钟保留；原始列表与宿主Normalize的Activation RefSort分开对照。标准形状复用既有模型/几何/转换，不另写同形算法。四Flight visual、SummonDarkness、Electrocution、Teleport没有激活组件，不猜未来形状或补deathwall。

单体/全屏保存原caster引用，Finish删首原引用；每次查询重新读取当前CastInfo和精确源NPCRemainingTime，单体自target回退当前TargetID、未知目标保留零mask；Raidwide mask按当前活Party槽。全局/个人提示分流与源分别对照，显示面板再合并。诊断不生成治疗策略或AOE。

普通CastGaze Start锁LocXZ/Activation/casterID，不跟Soul当前坐标；Finish只删首相同casterID，不能套耶鲁全清或sticky反转，也不带耶鲁adds选敌。提示dot>=.707107、范围10000；范围外仍画eye符号，只省略个人弧与ForbiddenDirection。按原顺序精确比较Eyes与原始ForbiddenDirections，覆盖9999/10000/10001距离、双caster/重复Start/无TTL。面向规划继续采用既有MP DungeonFacingDirections联合求解和0.75秒窗口，是明确的MP适配；**不宣称等于旧桥Normalize后最后方向覆盖**。回放仅展示计划，实际Facing/Target/ACR请求均被禁执行。

## 验证证据与未覆盖边界

前缀 `native-zadnor-ce51-20261125-`：model06为135相关场景PASS/0FAIL（112 Bozja+16 Hydatos+7 controls），build02为0错4既有警告，同DLLreplay03南方/札杜诺联合109条PASS，catalog02无筛选1536条行首PASS/0FAIL、294入口独立编译，全部exit0。SHA256 `7b89388e6cd28269139b7c7236f4af555a0d88fd70f1d33924b5db577b2a747b`。实际DLL对每种真实尺寸、source gate、原始watched Finish、无TTL、R25边界、真实solver R15脱险无fallback、普通多眼面向计划、禁执行、逆Seek、模块关闭与owner清理验收。根SDK11未验，本机使用隔离SDK10.0.303；游戏实时采样/像素/执行仍待验。

保留失败：model01在客户端名称占位检查失败，真实表返回汉比，修正正式名；model02新Feed测试缺using未编译，补MasterPlayer.MechanicHost后model06全通过。catalog01在新入口独立编译CS0122失败（使用内部BozjaCatalog常量）；入口改用同一已核975常量值，build02/replay03/catalog02通过。没有降低断言、修改统计口径或把未完成候选登记。

## 12模块语义试点

已只读核8札杜诺CE（41/44/51/52/53/54/62/63）与Dahu/Phantom普通零式，共110个实际激活组件类型实例。分组为普通AOE/伤害、LoS blocker、Concentric/Exaflare/Rotating缓存、状态/Created/EAnim/ModelState预测、分阶段启停；共用数据不共用错误的事件结束合同。CE51先完成标准基线，后续CE63为复杂状态/重演分支。DRS3/5源有明确TODO尾段，不能补造。当前完成12/12候选（CE51、CE63、CE52、CE41、CE44、CE62、CE53、CE54、DRN2Dahu、DRN4Phantom、DRS5Phantom、DRS3Dahu；女王古殿见NativeDelubrum.spec.md）；缺完整分段工时，不报告加速比，不以一个入口覆盖其余模块。

本轮CE51未从最初的源码审查起独立计时，不能追补准确审查/编码耗时或据此宣称已达到加速比。保留各唯一日志、文件备份及编译耗时作为证据；后续批次记录阶段边界后再作可比统计，不能用测试数量或行数代替速度。复用收益目前仅可证：七标准AOE、两动态诊断和普通凝视共享既有生产模型，避免重新实现几何与状态输入。


补充独立审查后的反例：model04/05在“销毁旧代只删首项”精确眼睛时钟对比失败，输出明确为源103.3秒/原生100.3秒。夹具在世界推进100秒后手动重复Start，源旧ActorCastInfo.ElapsedTime仍0而原生已封顶；将源ElapsedTime按真实世界封顶至TotalTime后，model06保留严格时钟与顺序相等通过，进一步覆盖同ID新代Finish消费旧眼、watched Effect不清队列。replay03增加实际MechanicHintsText严格等于Tankbuster/Raidwide断言。最终新增反例由model06/replay03执行，同一生产DLL的无筛选catalog02保留。未迁CE“继续旧桥”由精确ownership负例和Host静态分支支持；新增专项没有启动一个具体未迁CE旧桥模块发布输出，不扩大该动态验证声明。


## CE63 状态预测与影子重演

客户端T975↔CFC778、DE28「有角凶鸟——阿尔科诺斯特」，源SHA256 `0371c57d5dcdafcdd988c0d01d204aa0b9a385a84512de7ea06af634c913f99a`；16唯一源AID（6499、24121–24135）全部有行、CastTimeExtra=0。主OID31D0，中心(-480,-690)、32边形R29.5，源玩家R30圆拉起；五组件按Stormcall、BladedBeak、NihilitysSong、Fantod、Foreshadowing顺序激活。24123单体与24124全屏沿用原caster动态NPC钟/真实Party mask/全局提示，24127为R3标准圈；视觉24126与实际全屏Effect24125不替代源watched action。

Stormcall：任意Actor获得2234状态即追加，Extra1E为当前钟+9.7f，其余+19.9f，按原位置与中心的方向投影30码，不量化原Actor或投影。零长度方向保持零；WDir除法先舍入倒数再乘分量，候选Vector2直接除法会在具体R35边界差1 ULP，已精确修复。仅恰3项按源RefSort排序，第四项只追加；始终公开前min(count,2)。24122 Start按ID替换首项的包位置/钟，不追加、不重排；Finish按ID删首，Effect、Lose、Dead、无Cast销毁和超时不清。新同ID代际仍可修正/删除旧标量ID项。真实60槽、冷Start先于Status、冷Pet与热Pet过滤分别验收。

Foreshadowing：Boss24130/31/32分别为Donut8–25、R36全角130°扇形、R20圈。Boss Start设当前shape并追加自身AOE，只填已有记录的null shape；24129保存原Actor引用和当时shape（可null），同ID/重复记录不去重。当前Boss shape非null且记录恰4时，每个符合回调都再次追加4预测；钟取触发回调的NPC CastFinishAt+float11.1f转double，复制初始化时原Actor的Quantized位置与当前朝向。四记录可来自同一Actor；第五记录不初始化。旧Actor销毁并同ID重建不重绑原引用。恰5个AOE只公开首项，4/6/9/10等数量全公开，绘图/提示/导航共用同一Visible。

Boss形状Finish清当前Boss shape、按ID删首AOE但保留影子记录；影子实际24133/34/35 Finish按ID删首并清全部记录，即使无AOE匹配也清记录，不清Boss shape。24129 Finish与所有Effect不处理。前三影子保留donut、Boss Finish、第四记录null、新Boss cone的路径只填第四项null，形成混合形状。没有当前24133–35或Enemies扫描恢复已缺历史，不补造预测；Load/Director/Clear清全部专属状态。

最终证据前缀 `native-zadnor-ce63-20261125-`：model07为150相关场景PASS（127 Bozja+16 Hydatos+7 controls），build02为0错4既有警告，replay02为119联合专项PASS，catalog03为1546行首PASS（1537测试行、9汇总行）、294独立入口、0FAIL，全部exit0。DLL SHA256 `b90944bc6fb40ab2d71b24d42fd0e0da47e09c89bdc0aab177a1f9cf56c7b530`。实际DLL验证真实状态槽、精确ULP圈边界、三形状5隐藏→4晋升→9再初始化、旧Actor代际、真实Finish、诊断及缺历史、owner/逆Seek/回放禁执行；真实SafeZoneSolver从R20危险圈逃至多边形内无least-danger fallback，隐藏四影子不入导航。源真实Feed另比较两种冷Actor顺序与不同Total/Elapsed、逐事件原始隐藏列表/钟/位置/ID。SDK仍隔离10.0.303，未部署；实时采样、像素与执行待游戏内确认。

失败保留：model01缺AOEInstance类型引用未编译；model02为7 PASS/4 FAIL，三项由Stormcall错误提前量化导致，一项为旧影子夹具未量化预期，分别修生产与夹具后model03/04通过；独立复核发现倒数乘法ULP缺口，model06新增精确反例为14 PASS/1 FAIL，保留失败后修生产，model07及新DLL通过。旧build01/replay01/catalog01–02不得替代最终DLL证据。

计时：实施起点monotonic421307.390、首轮验证起点421578.718，初稿编码墙钟271.328秒。最终验收窗口结束422866.265，后续验证/返工窗口1287.547秒，包含新增反例、独立复核等待及并行CE52准备，不能当纯验证耗时或净开发工时；前置审查无完整起点。没有可比基线，不能据此宣称达到加速比。复用三标准组件与现有状态槽/时钟/输出，只新增本模块状态/重演差异；未重构既有已验模块。


## CE52 时间、炸弹与时钟

T975↔CFC778、DE26「复制魔人——第四军团贝利亚斯」，源SHA256 `b6154ac1948df2d062af5abc98742146df9d78075e58cea3a54a638b2a195897`；13唯一源AID（6499、23953–23963、24809）均有行有名，ExtraCastTime全部0。主OID31C8，中心(-550,0)、边长59正方形，pull半宽30。OnlyLoadIfTargetable必须先pending，targetable才加载，不只是Active门控。实际四组件依次TimeEruptionBombReproduce、Eruption23960 R8、FireTankbuster23961、FireRaidwide23962；23963实际Effect不是源watched Raidwide。后三个复用现有标准家族。

主组件危险组按Bomb timer非零→Cyclone非空→EruptionStart非零互斥，Bomb缓存空仍压住后两者；独立R8不受影响。只有Primary原引用23957 Start置当前钟+23.2d，缓存只取OID1EB17B，激活时建、Created追加、Destroyed按原引用移除，Dead不筛；显示当前原位置/朝向不量化。23958任意合格NPC Effect清timer，Start/Finish/销毁不清。Cyclone23959 Start锁LocXZ/rotation/NPC钟追加前60后0全宽20，23959任意合格NPC Effect跨来源FIFO删首，普通Finish/销毁/超时不清。空FIFO与源同样报ArgumentOutOfRangeException。

时钟OID1EB17A动画10002/10020分别9.7d/11.7d，每次有效动画追加原回调位置，不按死亡/Targetable/Actor类型筛、不去重。仅恰9时List.Sort(delay)，末两位置追加safe背景；1/2全显、3显1、9显7、10显8。23955任意施法者Start置当前Clock，可重定时；23956 Effect先清全部safe，再按caster当前位置各轴差≤1删首记录（含隐藏项），不读TargetPosition，仅全空清start。旧位置不随移动/销毁改变。safe方块为centered20×20独立背景，未Start也画、Bomb/Cyclone期间也画，标Safe且不生成Goal/AI。视觉PauseTime/Reproduce无新增行为。

源组件激活不重播历史EAnim。快照动画首次/变化与真实包为独立路径，真实包不改快照去重字典；Actor缺失不删旧动画值，明确null才重置。Framework先ApplyFrame，再将快照事件追加到已排真实包后；Flush成功才清队列。失败后包括未消费快照在内保留原队列，恢复时旧旋风Effect→旧clear-safe Effect→第9动画必须最后生成2格。普通ReplayPlayback逐记录更新，先World记录再真实包时钟顺序为11.7→9.7；另用真实fixed batch验证包→snapshot为9.7→11.7，不能混淆两种驱动。

错误处理明确适配MP宿主：旧桥Feed异常Sleep全部输出并保留事件/模型，脚本宿主则会停止逃逸异常脚本并清历史。因此仅捕获带TimeSequenceError诊断的确定空FIFO异常，LogError去重、撤整个Region绘图/导航/场地/控制、保队列/history并return；新真实旋风Start可让旧Effect消费并恢复。错误CE即使非Selected，也撤销Selected CE51输出。模型仍抛异常供源差分；不吞未知异常、不虚构空Effect成功。专项先断言预期LogError，再停止并SetError空，以隔离持久诊断；不修改生产ScriptManager或放宽PT99断言。

矩形精确适配：本族既有Ray保留源AOEShapeRect.Check与直接SDRect原点投影，模型/提示/当前solver发布保持该源边界。旧桥中心转换还会额外舍入；具体origin=(-550,1e-7)、rotation0、point=(-550,0)在源Check/Ray之外，距离+1e-7，但旧桥float(originZ+30)=30会把该点含入。新增反例实际调用源及MechanicHintsConvert确认两者；本次明确选择源边界，不宣称Ray与旧桥任意浮点边界逐bit等价，也不放宽容差。这项不扩展执行范围。

最终证据前缀 `native-zadnor-ce52-20261125-`：model05为160相关场景PASS（137 Bozja、16 Hydatos、7 controls），build03为0错4既有警告，replay07为130联合专项PASS，catalog03无筛选1557行首PASS（1548测试行、9汇总行）、294独立入口，全部exit0/0FAIL。DLL SHA256 `4bd1b977db16144c55e69b5ce614e9d8f5f4b5773b268e258cf86a621d65a644`。真实solver从R8逃离并位于正方形无fallback，逆Seek、owner、模块开关、原生Effect、回放禁执行通过。仍用隔离SDK10.0.303，根SDK11、游戏实时采样/像素/执行未验，未部署。CE41此时只有未编入该DLL的候选，不计入此批。

失败原样保留：replay01夹具误用旧LogSystemEffect，修完整NativeActionEffectEvent；replay02异常逃逸使宿主停脚本，修上述错误隔离；replay03把逐记录更新误当单Framework，修夹具并增fixed batch双断言；replay05-before-fix在旧DLL精确复现非Selected错误未撤输出，build03/replay06/07通过。catalog01/02均在PT99捕获检查报告1个错误，定位为前面预期LogError持久残留；测试先确认准确诊断后清除，catalog03保持PT99原断言通过。model05补源Ray/旧桥中心舍入边界。

计时起点monotonic422377.390，最终登记边界424623.109；其中包含并行CE63收尾、独立复核、追加反例与CE41准备，无完整独立审查/编码/验证分段，不能作为净工时或加速比。复用标准组件、原Actor/时钟/公共输出；只为当前模块新增动画输入和FIFO错误隔离，未开发通用转换器。


## CE41 晶体、连续地碎与图标诱导

T975↔CFC778、DE21「焦土魔蝎——赫德提特」，主OID31CC，中心(-220,530)、R29.5的32边形，pull圆R30。源SHA256 `3ebaf277620e381914a1eb2a767ebb9c74bf74b3140479fb7259a31eb4c9864b`。21唯一源AID逐项真实核表均有行、CastTimeExtra=0；Teleport24090名称为空但行/extra存在，不以空名当缺行。实际十组件顺序为CrystallineFracture24072 R4、ResonantFrequencyDim24073 R6、ResonantFrequencyCorrupted24074 R6、CrystallineStingers24077、AetherialStingers24078、Subduction24080 R8、Earthbreaker24082/83/84、CrystalNeedle24085、Shardstrike24087、Hailfire图标79–82/Effect24089。Subduction源只有R8 AOE，注释击退不扩为自动站位；CrystalNeedle沿用原caster动态NPC钟与Party mask诊断，不新增治疗或执行。

两套LoS各有原caster列表、当前Origin和Safezone FIFO，只取对应OID31CD/31CE缓存中的非Dead晶体；不筛Targetable/EventState。敌人缓存按原引用Created追加、Destroyed移除，死亡保留；Draw观察及激活前初始化维持源惰性缓存边界。Start追加原caster、严格最小当前RemainingTime选Origin，同钟保留先者，再以本次Start的NPC钟建立冻结Custom polygon。错误引用Finish即使Casters.Remove未找到，仍Refresh并pop首safe；旧safe不随刷新、移动、死亡或销毁重建。Safezone仅公开首项；Hide提示用当前Origin距玩家严格<60且不在冻结safe。两组件互不混用caster/晶体。

几何复用已验BozjaMeteorGeometry，仅参数化当前批次需要的外径60及不扣hitbox，旧CE33默认R30/扣hitbox保持并经公共消费者验证。阴影从distance+.2开始，safeInsideHitbox=true，不新增Obstacle。模型Check、源Clipper顶点、显示外环及有限导航采样分别对照；危险填充>65%时旧桥不发布导航，仍保safe绘图，DLL分别验证半场shadow与稀疏shadow，不改采样器或补Goal。普通Ray继续沿用本规格CE52明确的源式精确边界适配。

Earthbreaker仅24082 Start追加初始Circle10序列；24083/84 Start和当前冷Cast不恢复历史。Finish按阶段0/1/2、原始Loc每轴≤1匹配首序列，与caster身份或spell朝向无关，下一钟FinishNow+2d；错误位置/阶段仍增已知阶段counter，不匹配不推进。第三段删除。Update仅count/version变化才发布缓存，回调与Update之间仍是旧形状；不按Clock排序、不按TTL或Effect推进。多条重叠序列、轴1/1.001/.9对角、错误caster/阶段、pre/post Update及全部真实Finish通过。

Shardstrike保留原Target引用、重复项、原始列表和当前Active列表的差别：Dead仅隐藏Active，Destroyed不等于Dead，同ID重建不重绑；普通Finish按TargetID删首并计数。本人判断用raw原引用，固定风险文字Spread, avoid intersecting cage hitboxes!，不继承普通Spread文字。父类导航要至少2活Party：其他Active目标量化位置R6，本人避未被Active标记的活队友量化位置R6；raw本人再追加全部晶体原位置R6.6单一SDUnion，含Dead晶体，Clock用raw Spreads[0]而非本人/首活项。先保持单Union参与Normalize，再按Dim整组→Corrupted整组展开，不提前拆圈改变同钟RefSort顺序。源Party按槽遍历，本批两处队友导航显式Slot升序，不改其他已验消费者。绘图所有Active目标量化R5 outline，raw本人另画全部晶体实际hitbox outline。没有分散伤害预测。

Hailfire仅真实Icon79–82追加当时Primary原引用及回调SourceId所指Actor原引用，忽略TargetId；Clock=Now+8.2d+(icon-79)*2.1d，按绝对Activation List.Sort，重复保留，过钟不清。图标冷激活不恢复。24089任意合格NPC Effect弹raw首项，空Effect no-op，普通Finish无效。原source/target动态位置给前40后0全宽4矩形；仅source Dead隐藏，Target Dead/Destroyed不隐藏，Source Destroyed不重绑。绘图按目标ID分他人background/本人outline；导航按原Target引用分实际矩形或本人对每活队友的R100/asin(2/d)锥，未Clamp，NaN沿用旧桥空转换。不输出Bait伤害或Tankbuster全局文字。已实证旧target Destroyed未Dead、同ID新viewer：显示仍本人outline，导航为他人危险矩形。

Icon输入已核匹配PR：引用1.5.10.4实际DLL与sibling Release SHA256相同，相关ActorControlHook/Capture/Events源码与该DLL内嵌PDB文档SHA256相符；Hook param1→MarkerId、sourceId→SourceId、param2→TargetId无变换。Factory原样传入，当前BMR IDScramble.Delta为const0，因此原生直接消费MarkerId，不重复归一化。未检查游戏进程shadow-copy版本。

模块DrawEnemies虽生成Actor marker并影响缓存建立，但实际旧MechanicDrawConvert没有该case；新增真实DrawEnemies+Convert回归证明含越界夹取标记均skip、无shape/line/extra输出。候选曾加三角标记，独立复核后撤回以保持已消费范围；分散晶体hitbox outline仍保留。不能把未消费的Actor glyph算作已验像素或自动导航。

最终证据前缀 `native-zadnor-ce41-20261125-`：model05为175相关场景PASS（152 Bozja+16 Hydatos+7 controls）；build02为0错4既有警告；同DLLreplay05为143联合专项PASS；catalog02无筛选1570行首PASS（1561测试行+9汇总行）、294独立入口。全部exit0/0FAIL。DLL SHA256 `a5f4cae28547cd93086e6e1786f46cb13af5124bc71477e38a7f2416afb3955c`。真实Feed十组件冷激活、图标冷丢失/SourceId、完整多组件Normalize→Union展开、原caster/target代际、真实SafeZoneSolver从R10逃到多边形内无fallback、逆Seek、模块开关、owner与回放无面向/选敌/ACR执行均通过。SDK为隔离10.0.303，根11未验，未部署，实时采样/像素/执行待游戏内确认。

失败保留：model01为10 PASS/1 FAIL，错误caster夹具Add后漏Sync；model03新增DrawSink字段误用Rotation未编译，改为真实Direction；model04-before-slot-fix为13 PASS/1 FAIL，独立审查指出乱序Party后用真实源先复现，再局部修Slot排序，保留精确顺序与Normalize断言。replay01/02在LoS danger>0失败，实际为稀疏shadow触发既有65%采样省略边界；重新分两种布局同时严格断言有导航与无导航，未降低断言或改生产采样。旧build01/replay03/04/catalog01仅中间候选，不替代最终marker范围/槽顺序修复证据。

计时：precode monotonic424382.265，最终验证收尾已到425813.812，窗口1431.547秒，含CE52登记、源码/独立复核等待、返工及CE44准备，非净编码或验证工时；首稿与各阶段边界未完整捕获，不宣称加速比。复用了四标准AOE和单体、现有LoS多边形/原Actor/时钟/输出；新代码只表达两独立LoS状态、Earth序列、专属Spread与Icon bait差异。


## CE44 塔洞、地火与延迟伤害

真实客户端核T975↔CFC778、DE29「复制统治者——第四军团哈修马利姆」，源主OID2DD2、中心(330,390)、R29.5的32边形、pull圆R30。源SHA256 `57f19f888772466e11599615e935b176d98f0b0f0d6fe794fd98104dbf1c21d7`。22唯一源AID逐项本机核表全部存在、extra0、空名0，包含visual；不补造Sanction/TowerRound/ControlTower额外全屏或FallingTower提前预测。实际九组件按源顺序：TectonicEruption23826 R6、RockCutter23827、AncientQuake23828→23829、Roxxor23823 R6、ControlTowerAppear23830/Finish23818、Towerfall23818前40宽10、ExtremeEdge23822/21前60宽36、IntractableLand23819/20 R8、Hammerfall Created1EB17F/Finish23825。

RockCutter复用SingleTargetDelayable语义：Start self-target回退当前TargetId，目标缺失不追加、非Party保slot-1，冻结原caster/target引用、当时实际首Party槽与NPC钟。死亡、销毁、换队、普通Finish与过钟均不清或重算；23827实际Effect按MainTargetId跨source删首，未匹配也计数。AncientQuake保单钟覆盖，Delay严格由.8f提升double，watched visual Finish不清，首次23829实际Effect清；第二次不补预测。只诊断，不发治疗。普通Roxxor保原target引用/Dead隐藏/Destroyed保留/Finish按TargetId删首；量化R6 outline、R7导航、活Party不足2不发FZ，普通Spread!风险及GTFO文字，无晶体Union或分散伤害。四标准AOE沿用锁Loc/包rotation/NPC钟，ExtremeEdge共表≥99 RefSort、任意合法Spell Finish按casterID删首；不补注释击退。

ControlTowerAppear Start按LocXZ精确去重R6，不更新旧钟；Finish先按Loc删首AOE，再以Loc距既有塔中心每轴≤.1去重，实际新洞取caster当前位置，R5.5/20边形。Loc=P/Actor=Q可重复建立Q洞。Towerfall真实Finish先全清出现AOE，再按caster当前位置精确删首洞；无匹配仍保全清效果，新旧塔重叠只删首个旧洞，最后恢复DefaultArena。当前Tower Actor、销毁、死亡与时间不能恢复或清除洞。

新BozjaControlArena复用现有Clipper2与20位二进制定点缩放，模型保真实差集孔洞；单独HostBoundary严格取差集结果顶点最多的exterior，保边缘缺口，不另建内部孔洞Obstacle。源Bounds重新计算重心但MiniArena.Bounds赋值不改MiniArena.Center：模型Contains按固定(330,390)+重心化局部点，旧Nav则加custom.Bounds.Center恢复世界外环，两条路径分别对照。边缘落塔反例首先在model02精确失败后修复，未改源或放宽包含断言。源Border真实捕获ComplexPoly包含Holes；旧DrawConvert白框整条skip、红风险框只消费Exterior且不拆洞线；新增真实capture+Convert断言。原生沿用已有场地外框预览，不新增源旧桥未消费的孔洞轮廓/底板或自动避洞。

IntractableLand仅23819 Start建线：起点caster原位置，Advance=8*spell.rotation方向，NPC首钟，8爆、最多4、步时.8d。23819 Effect取实际caster位置，23820取NativeActionEffectEvent.TargetPosition原始区域坐标，逐轴≤1首匹配；Next=实际点+Advance、NextExplosion=EffectNow+.8d，归零删线。未匹配保队列并计诊断；缺原始区域坐标只诊断，不能用MainTarget当前位置猜测，这是旧录像缺字段的显式输入限制。Finish、时间、销毁不推进。Update仅count/version变化重建；先所有线future再所有线current，future基钟max(NextExplosion,Now)仅重建时计算，输出量化位置，原内部Next不量化；future颜色源AOE、current源Danger，全部Risky。组件raw/绘图顺序与Host Normalize禁区钟序分别验证，不把显示缓存重新排序。

Hammer Created即锁量化原位置、回调Now+12.6d；冷激活从当前回调钟建立，不恢复出生钟，不排Dead/Targetable。最多公开前2，总数>1首Danger，两项均Risky；普通23825 Finish由任意合格NPC全局FIFO删首，空安全，Effect无效。无cast销毁/死亡/过钟不删，带cast销毁仍先触发真实Finish。保持原始无限队列。

独立复核闭合三项：CE44整批禁区按源RefSort Normalize；合并伤害按源List.Sort钟序，构造Quake早于Rock反例及实际DLL断言；延迟target选择最小实际槽，保热Start旧Party/冷Start当前Party，CE44/CE34重复槽3在DTO首、槽0在后均严格对照。普通CE41 CrystalNeedle同类raw动态槽边界同步局部修复并加真实源反例。实时OnUpdate现在每Framework Scan(true)，旧宿主也是每Framework Capture→ApplyFrame→Flush→Update；MP replay仍按Version/事件/.1缓存采样，无新世界时每Framework更新缓存生命周期。此处是源码接入核实，未以离线回放声称实机采样已验。

最终证据前缀 `native-zadnor-ce44-20261125-`：model07为192相关场景PASS（169 Bozja+16 Hydatos+7 controls），build02为0错4既有警告，同DLLreplay05为155联合专项PASS，catalog02无筛选1582行首PASS（1573测试行+9汇总行）、294独立入口，全部exit0/0FAIL。DLL SHA256 `720855c4df197730f1c3cb97a7208a668fa1551e4a79082a704699c09147126c`。真实Feed九组件冷激活/当前Actor顺序/下一Framework、全部禁区Normalize与伤害序列、真实DrawConvert孔洞消费、实际SafeZoneSolver从R6逃出且位于32边形内无fallback、精确975/2DD2 ownership与未迁桥保留、逆Seek/机制开关/owner释放及回放无PR面向/目标/ACR执行通过。SDK仍为隔离10.0.303，根11、实时游戏采样/像素/执行待验，未部署。

保留失败：model01夹具Span.Single未编译；model02为8PASS/4FAIL，边缘塔洞中心处理及禁区Normalize是生产差异，地火改CastLoc后的额外Start是夹具差异，future源显式AOE颜色需与默认色0区别对照；修正后model03十二项全过。model04为187PASS/2FAIL：新增CE后旧Region数仍4、Border.ShapeSimplified未经ScreenHalfSize初始化，分别更新准确五CE数和源真实绘图初始化；未削减断言。replay01遗漏csproj显式Include未编译；replay02/03与catalog01是最终实时采样/普通slot修复之前的中间DLL，不能替代最终证据。最后的额外ownership断言在replay05运行，生产DLL未再改变。

计时：precode monotonic426229.531；登记边界427587.546。这包含审查等待、返工、验证及并行CE62准备，无完整独立分段，不能当净工时或加速比。标准Cast/原Actor/延迟伤害/普通Spread复用；复杂塔洞和地火分别表达来源差异，未建立通用转换框架。


## CE62 Lightning、Cloud 与 ModelState Burn

真实客户端核T975↔CFC778、DE30「光辉虹蛇——阿依达」，主OID31C9、中心(-200,-580)、CircleR20/pullR20。源SHA256 `f5a5ced399223aeb8097b0c84ccb859d42bec9fe9984a24e2962bac6eec30ede`。24唯一源AID全部有行、ExtraCastTime均0；23965与24711真实名称为空，不能当缺行。实际十一组件按源顺序：Thundercall23964、LightningBoltDistantClap23968/69、TwistingWinds23970、CloudToGround23972/73、Flame23974、Burn23976、Lash23977/78、Charybdis23979、Roar23981、Levinbolt23984、SerpentsEdge23985。

标准部分复用既有原生合同：23970前80宽10，Lash R40半角90°共表99项排序，无Backlash额外旋转、不补注释击退；三Raidwide保原caster动态NPC钟及当前真实Party mask，Thundercall精确文字`Raidwide + summon lighting orbs`、Flame为`Raidwide + summon flame orbs`，23979纯`Set hp to 1`提示，无伤害预测；23985普通动态单体；23984普通分散R6显示/R7导航、targetID Finish。未知visual Effect、Deathwall不扩造输出。

Lightning Start23968锁Loc/包方向/NPC钟，重复原点保留；23968实际Effect取Native TargetPosition逐轴≤1首匹配，原索引替换Donut4–10，Origin取实际Effect位置、rotation0、当时Now+6.1d。重复Effect可命中已有donut并重置首项钟。23969实际Effect则取caster当前位置找首项删除，忽略TargetPosition。Finish、Destroy、时间不清；缺TargetPosition只诊断，旧录像不能猜MainTarget坐标。

Cloud首次23972锁caster原位置及包方向，Advance5、4爆、最多显示2、间隔1.1d。23972与23973实际Effect均取caster当前位置逐轴≤1首匹配，Next重新锚定实际点+Advance，下一钟当前Now+1.1d；不使用TargetPosition。未匹配只诊断；无Finish/超时清理。复用CE44的参数化Exaflare发布缓存，原内部点不量化、输出量化，count/version变化才重建，先所有future后所有current。CE44仍8步/8爆/最多4/.8d，首Effect取caster、rest取TargetPosition，联合源对照及实际DLL专项通过。

Burn只消费完整ModelState三字段变化快照，先更新观察基线再分发，基线即使Inactive/Pet也推进；回调仅排Pet，不能排Player/Chocobo/Buddy。Active且OID31CB且animState1=1时每次回调追加量化位置R8、当前Now+5d，另两字段变化也重复追加；源直接同值MDLS操作仍各算一次，与Feed快照去重分开。冷激活没有ModelState重演，旧录像未录字段与真实全零不可区分，采样间0→1→0不可恢复。可见列表为raw连续前缀、严格activation < head+1d，遇第一个不满足即停止，不排序、不全表筛选；后续更早钟仍会隐藏。23976任意合格NPC实际Effect全局RemoveAt(0)，空抛ArgumentOutOfRangeException，不改成no-op。

空FIFO按旧Feed保整个packet批次（包含已经执行成功的前项）、撤整个Region边界/导航/显示/预测/控制，下一Framework先真实ModelState输入再重试整批。反例验证前项Lightning重新以当前钟执行，真实新Burn被后项消费后恢复。失败source下帧消失时，源ActorState操作跳过回调，整批成功；`CompleteEventBatch`仅在全部pending事件成功后清瞬态Burn/Time诊断，避免恢复后仍锁输出，失败不清队列。独立审查两条链与源Feed/ActorState核实。

补充preScan交错反例：非更新日志扫描仅采样、无pending时设置needsFrameworkScan，随后Framework必先正式快照（Inactive消耗tuple基线）再Update冷激活，不能先激活后把旧tuple补成Burn；相同tuple下一帧仍零。真实游戏每Framework采样接线保持，MP replay保Version/事件/.1规则；不以离线场景声明实机采样已验。

最终证据前缀`native-zadnor-ce62-20261125-`：model03为203相关场景（180 Bozja+16 Hydatos+7 controls），build01为0错4既有警告，同DLLreplay04为170联合专项PASS，catalog02无筛选1597条行首PASS（1588测试行+9汇总）、294独立入口，全部exit0/0FAIL。DLL SHA256 `f518e0a0543110a0d299b622fa5122de97e8d972a01d440c9817729fc46de8db`。真实Feed十一组件/冷热输入/Normalize禁区与合并伤害源对照、实际SafeZoneSolver离开前向矩形留R20内且无fallback、975/31C9精确ownership、未迁桥保留、Seek/owner/回放无执行通过。SDK隔离10.0.303，根11、实时游戏采样/像素/实际执行仍待验，未部署。

保留中间失败：replay01/02均exit1，最终手工批次的Lightning钟保持4.3而非当前3+6.1；定位为夹具默认JsonSerializer丢System.Numerics.Vector3字段，真实TargetPosition变零并错失首匹配，生产ReplayJson.Options已有专用转换器。Aida手工Apply/Effect改用生产选项，新增原坐标严格断言，未放宽任何钟或队列断言；CE52同类一处世界帧/两处Effect一并改用生产options，核Actor.Position、Cast.Location与TargetPosition保真；联合replay04及最终catalog02重验。replay03/catalog01在CE52夹具修正前，不替代最终。model01十一项/02联合203项是两项时序独立修正之前，不替代model03与最终DLL。

计时precode monotonic427659.578；登记边界428921.562。含独立复核、夹具返工及验证，缺完整分段，不报告净工时或加速比。标准AOE/Spread/动态伤害与小范围Exaflare参数复用，Lightning和Burn保持独立消费；不改既有已验模块总架构。


## CE53 Charge、两种击退与 RawSteel 原引用连线

客户端T975↔CFC778、DE22「钢铁人马——魔导骑兵大队」，主OID31C7、中心(-750,790)、CircleR25/pullR25；源CheckPull不要求Targetable。24源AID全部有行且ExtraCastTime=0；源SHA256 `ee6aee39911fa5f850ed49d4f314eadc6c6fb140e7745d5fcedbde8f8d0d917f`。十四组件按StormSlash23931、MagitekBurst23932、BurnishedJoust23936、GustSlash23934、FireShot23937、AirborneExplosion24872、RideDownAOE23939、RideDownKnockback23940、CallRaze23948、RawSteel23943、CloseQuarters23945、FarAfield23947、CallControlledBurn23951、MagitekBlaster23952顺序接入。RideDownAOE使用visual23939，实际前60宽10而非注释width60；CallRaze用23948及精确Multi raidwide。其余标准扇/圆/环、Raidwide动态原NPC钟、普通分散R6/R7和R8分摊复用现有模型，不补不存在的visual输出。

BurnishedJoust先用原caster位置到Loc算长和方向，再量化Origin；保重复、跨caster队列及Finish首ID。GustSlash向前35，ignoreImmunes、最多一次；RideDown每Start替换为场地中心两半场R30半角90°、包方向±90°、推12，shape命中后再计max1，任意watched Finish全清。两者源不提交AI禁区，生产只投影线和边界危险文本，不把落点扩成自动导航。FireShot复用Juice小范围参数化：cast动态目标/Loc、实际Effect锁当时目标/TargetPosition、真实对象量化位置三列表；delay0、OID1EB1D3仅排state7，不排Dead；Update每对象仅消费首个2码内预测，没有TTL；Finish仅清cast首项。旧CE33仍用1E972C/.9d并联合验证。

RawSteel继承合同逐项保留：默认bad57/good1、半宽2、延迟5d、拉长阈值15。真实Op每次旧Untether→赋值→新Tether，相同包也回调；仅直接组件callback可以无Untether重复追加。双方由回调时旧Party存活原source是否包含决定，不固定NPC→玩家；热Created初始tether先于Shared Dead，冷激活使用最终Dead。原目标引用的5秒缓存仅在Activation+1<=Now时到期，不自动删bait/线。Untether按当前两侧原引用删全部bait，线只删world.Find(tether.Target)对应首项，反向线保不对称。实际23943任意合格NPC均计数，再按MainTargetID删首bait；Finish不清，未匹配也计数。Update才重算矩形长，方向动态；Active只排Source.Dead，允许Target死亡或Destroyed。damage mask按当前槽合并Active目标，但钟仍取raw头，即使头source已死。

导航先所有bait：别人矩形、自己按存活Party槽序的R100 Asin(2/d)锥，再第二轮自己的source中心R15；Asin不钳位、NaN角经既有采样为空。背景/outline按targetID，导航/线按原引用，bad线优先good；文本保Stretch专用合同与额外raw bait-away，不混入GenericBait整套文字。NaN距离保持原>=225分支；sin/cos与Asin按源double再转float，乱序DTO槽不改变输出顺序。真实Feed十四组件冷热输入、Normalize禁区/伤害及转换几何差分，直接重复callback/跨代际/死头/旧钟均覆盖。

独立复核发现事件preScan提前提交世界导致晚创建target、同帧Party、多preScan不同输入与提前判定钟、跨CE出生序/同优先级选择偏差。最终Region preScan仅Advance，不提交source、actors、出生序、Loaded/Selected或机制回调；正式Framework统一逐Actor→Party→Reconcile→FlushEvents→Update。HandlesCast使用同Version最新采样Actor的NativeNpc谓词，仍由正式Active/开关/actions决定接管，新helper无须提前进入模型。真实Feed差分覆盖preScan先见CE53、正式帧CE51在先，正式选择必须为CE51；模型与实际DLL新helper门控、旧Party/晚目标/Seek均严格验收。公共改动重验南方与札杜诺全部消费者。

MagitekBlaster本人为目标时源生成R4队友反向约束；既有有限采样对>65%填充返回空，因此实际导航为零，不补造新goal。模型保raw约束并与源真实Convert对照；专项严格检查该raw队友约束、适配空输出、另一个目标的R8真实goal及Finish。最初replay01–03均因错误的“一定有发布区域”断言失败；未放宽几何/钟断言。公共提交调整后replay07失败于注册表旧夹具只Scan(false)却立刻断言正式输入，replay08定位另一Apply误提前Update；最终手工帧先preScan、按具体用例执行真实OnUpdate，生产JSON options保Actor.Position/Cast.Location，deadline及冷激活测试仍在Framework前严格断言不激活。

最终证据`native-zadnor-ce53-20261125-`：model10为221场景PASS（198 Bozja+16 Hydatos+7 controls），build05为0错4既有警告，同DLLreplay10联合186 PASS、catalog02无筛选1613行首PASS（1604测试+9汇总）及294入口独立编译，全部exit0/0FAIL。DLL SHA256 `8a1c13cd69033c5cbfca9f236deb3eee95504e99604fd936a8816d5cbf25ea4f`。实际SafeZoneSolver离开R15且留R25内无fallback、975/31C7精确ownership及反例、普通watched Finish无TTL、双视图预览/线序、机制关闭、owner、逆Seek与回放无控制通过。中间model01–09、build01–04、replay01–09/catalog01仅为迭代证据，不能替代最终。SDK10.0.303隔离运行；根SDK11、游戏实时采样/像素/实际执行仍未验，未部署。

计时precode monotonic429062.953，登记边界431280.984；缺完整分段，不能报告净编码工时或加速比。复用标准形状/伤害/stack/spread、Juice三列表与状态输入；ChargeTether及两种击退保独立事件结束合同。下一项CE54十一组件需独立LunarCry私有冰柱原引用、旋转缓存与ThermalGust snapshot Finish异常，仍pending。


## CE54 冰柱视线、六旋转队列与 snapshot Finish 失败

真实客户端T975↔CFC778、DE25「邪恶冰狼——恶名苍狼」，主OID319C，中心(-830,190)、Square半宽24/pull半宽24，pull须Targetable/InCombat。28唯一源AID均有行、ExtraCastTime=0；23621/22/23真实空名不作缺行。源SHA256 `e4102314e2e4b352cc26a924f3fdea2e8b454dde8d5ec96f8a759e4ef3449bbc`。十一组件顺序为IcePillar23582 R4、PillarPierce23583前80宽4、Shatter23584 R8、BracingWind23586、LunarCry23588、ThermalGust23589、AgeOfEndlessFrost23590/91/92/93、StormWithout23594 Donut10–40、StormWithin23596 R10、AncientGlacier23601 R6、Glaciation23602 Raidwide。watched ID不能替为注释辅助体23595/97/23603；未实现Enrage不补造。

LunarCry私有pillar保存原Actor引用，Created319E追加、23583 Start按原引用移除；Destroyed/Dead不清理，同ID新代追加而不重绑。冷激活按逐Actor Created→Start，boss先于pillar不补造安全区，pillar自身先有23583时先移除。原caster列表选当前Remaining最小者，以其Cast.Location为Origin，Start用当前柱位置/hitbox冻结R80且excludeHitboxes=false；Finish删caster→Refresh→弹safe首项，不按caster精准删safe。只显示首safe，个人提示距离严格小于80；任意Bracing caster仅抑制Lunar AI，保绘图/文字。Created只消费NativeNpc，排Player/Pet/Chocobo/Buddy，真实源冷热类型反例通过。

Bracing每Start追加，普通Finish按ID删首，Effect不清。前60宽12命中且免疫expiry严格大于activation时才判免疫，成功移动后才计max1，沿packet向前40；全部casters的AI不看免疫/shape，原SDRect前48后-8宽12由旧桥转换为中心前28长40宽12。逐字段保实际距离/方向归一化；只显示位移线/危险文字，不新增特殊移动执行。

EndlessFrost先收23590/91的-40°/+40°及23592角列表，恰六角且increment非零才建立六序列，共用最后23592的量化位置及NPC钟。每序列7发、间隔2d、最多显示2；23592/93普通Finish按XZ各轴≤1且normalized角差≤.05首匹配，成功才计数/旋转/下一钟Now+2，最后移除，不绑定caster。仅count或成功计数变化重建缓存，每序列future→imminent再下一序列；future钟max(next,now)+2只在重建取，全部Risky，最后一发仅改Future颜色。future独立Generation防preview覆盖。方向/六角输入两序、7角不建、共同末点钟、±π及.05/.0501、1/1.001边界与跨钟缓存均真实源对照。

Thermal Created319F锁量化位置/原方向/当前Now+6.5d；Cast Start不追加，任意合格NPC23589 Finish全局RemoveAt0，Effect、过期、死亡/销毁本身不清。空FIFO为ArgumentOutOfRangeException，发生于snapshot输入而非packet Flush；旧Cast与失败前Destroyed/Untether副作用保留，后置Actor/Party不提交，同快照重复仍失败，source删除也须先Finish。恢复快照必须在失败Actor之前创建可消费项，随后成功Finish及后置Actor才提交。Location变化先成功Finish、Rotation变化再次空失败并保旧Rotation；Created类型过滤与源一致。

为这个已复现反例将Region每个Actor拆成全模型Prepare→全模型Finish→全模型Complete，删除同样三阶段；CE53先Loaded、CE54后Loaded与相反顺序均不能提前清前序模型旧Cast或泄漏后置tether。完成整个snapshot才清Thermal错误，CompleteEventBatch不能误清；脚本仅捕获已诊断SequenceError、撤整个Region输出并重试。独立复核确认Finish屏障及Untether不重复；**不据此宣称Start/Status逐事件全订阅者屏障完整等价**。公共改动重验南方/札杜诺全部消费者。

最终日志前缀`native-zadnor-ce54-20261125-`：model06为239相关场景（216 Bozja+16 Hydatos+7 controls）PASS，model07补Loaded首项严格断言后2定向PASS；全量build02日志0错4既有警告，重接时进程句柄丢失，增量build03明确exit0且DLL哈希未变。replay02联合197 PASS、catalog02无筛选1624行首PASS（1615测试+9汇总）及294入口独立编译，测试均exit0/0FAIL。DLL SHA256 `4c22d64c3a43d7cc7ff80668de0d4e37fb5591cec63f6401601c72ea8822e3a1`。实际DLL十二旋转preview/hazard及顺序、LoS绘图/导航分流、真实SafeZoneSolver、精确ownership、snapshot失败撤销与恢复、机制开关/owner/逆Seek/回放无执行均通过。SDK隔离10.0.303；根SDK11、游戏实时采样/像素/执行仍未验，未部署。

中间失败保留：model01两处源API编译错误；model02 Bracing第二caster夹具位置不实际命中，修真实命中位置而非放宽断言；model04把ActorTypes.Player错误构造0x100而非0x104，修Kind1/SubKind4。build01/replay01/catalog01在最终删除Finish屏障前，不替代最终证据。计时precode monotonic431440.921、登记边界433382.640；缺完整分段，不能报告净工时或加速比。后续Dahu/Phantom四源仍pending。


## CE64 黑色燃焰：图标、追踪炮与开放弱点

客户端T975↔CFC778、DynamicEvent18「试制型飞行型魔导装甲——黑色燃焰」、BNpcName9945「黑色燃焰」；
Boss31A0/Escort31A1与32FD/Helper233C有真实表行，不用BNpcBase行猜NameID映射。
18唯一源AID均存在且CastTimeExtra=0，24281/24319为空名；Boss炮为24658/24659。
中心(-240,-230)、方场半宽24，pull含边界方角；源SHA256
`ff39a239500f15cb7e6341131d987f0ad43a8c1ac58acc7e04dae82ed107ad1f`。
按源顺序激活DiveFormation、AntiPersonnelMissile、ChainCannonEscort、ChainCannonBoss、
SurfaceMissile、SuppressiveMagitekRays、Analysis、PreciseStrike八组件。
标准AOE保留普通watched Finish；Raidwide监听Boss23616而非Helper23617。

Icon261锁原回调位置与Now+11d，保留完整FIFO但只发布前二；
23610任意真实NPC Effect全局弹首，无超时消费，源计时TODO不补造。
Escort状态2056每次Gain追加原Actor/首个ExpireAt，Lose/Dead/Destroy不移除tracker，
新同ID不替换旧引用。23612 Start清tracker并锁包位置/方向/NPC钟；23614仅停tracking。
23613按当前Effect caster位置逐轴<=.1匹配首项，每条独立六击删项，普通Finish不消费。
动态tracking为额外Draw，不增加ActiveAOEs或个人GTFO提示；背景先锁点线再动态线，
本人目标tracking仅前景outline。AI逐caster重读参与者缓存Count，首次空缓存填表但本caster迭代0；
同次第二caster已看到缓存。参与者为OID0且方场24内原Actor，不缩成Party或替换代际。
Framework PrepareNavigation是唯一有副作用的导航查询，Render消费缓存。

Boss24658 Start覆盖单项但保留击数，24659前三次从当前Effect caster量化位置/当前方向重建Now+1，
第四次清项并归零；无Start的Effect也可重建。形状复用源Escort全宽5，不按客户端宽6擅改。
弱点2644前向0、1709后向π按ID覆盖；任一匹配Lose均清ID，即使其他槽仍存。
原caster列表允许重复Start，watched Finish按原引用只删首；残项随后观察任意raw非Spell CastInfo，
按当前位置和当前raw朝向（无raw取Actor方向）选严格最小RemainingTime，同钟保先者。
raw身份与真实Feed字段一致，保留原Total0/短Total，float逐帧累加后
`(Total+.3f)-Elapsed`再一次转ticks；与普通回放读条的.05下限分离。
Destroy保留原引用残项但清raw；新同ID代不接管。眼为inverted+Forward，专用弱点提示，
PreciseStrike矩形不加入危险区。面向沿既有规划门面，回放禁止实际执行。

最终证据前缀`native-zadnor-burn-20261125-`：model07为324相关PASS/0FAIL，model10最终11定向；
build02为0错4既有警告；同DLLreplay03联合270条PASS，catalog01无筛选1699条行首PASS
（1690测试+9汇总）与296独立入口，均exit0。SHA256 `bbd7286228b757c86e8bea0ce55881c668c881471e28f25ccc884e1071213943`。
实际DLL覆盖全部八组件、真实R6求解脱险无fallback、显示/导航/面向计划分流、
Framework缓存和绘图顺序、owner/逆Seek/回放禁执行。完整Feed八组件用60槽快照，
额外raw Feed直接覆盖MechanicCastSnapshot检验非Spell/零短Total，未让legacy转换器裁剪。
不宣称已有实机录制证明本CE产生短Total；该项为实时输入合同反例。

失败保留：model01缺事件参数；model02漏主怪SubKind5；model03单侧多查AI造成测试自身缓存失步；
model06旧区域数量断言仍8，分别修正确输入/同步两侧查询/更新为实际9后通过。
model09新增raw Feed的ReadOnlySpan误用Single导致编译失败，转数组后model10通过。
replay02停于CE54旧ownership负例仍指向新迁CE64，改为未迁CE42后replay03通过；
其余断言保留。独立复核促成raw时钟、Framework唯一查询和先锁点后动态绘制修复。
旧build01及replay01不能替代最终修复DLL。SDK仍使用隔离10.0.303，SDK11未验；
实时采样、像素与执行待游戏内确认，未部署。


## CE42 第四军团谢米哈扎：死亡行军与双咏唱

源SHA256 `0eb51c4a6161fc5ffeecb110e806b1e3911f91d07095b98fe2810b4f4468707f`。
客户端T975↔CFC778唯一双向映射、DE20「复制告密者——第四军团谢米哈扎」、
BNpcName9931「第四军团谢米哈扎」；32唯一源AID全部存在、无空名且ExtraMS全0。
11个OID分别查表：Boss2E35、DeathwallHelper2EE8、ShockSphere3232、WarWraith3233、
Hernais3234、Dyunbu3235、Llofii3236、Monoceros3237、Helper233C存在BNpcBase，
Deathwall1EB173与LivingCorpseSpawn1EB07A存在EObj；不推断OID→NameID。
中心(-60,800)、32边形R29.5、pull R30含边界。十五组件按源顺序：
ArenaChange、DevourSoul、Blight、PurifyingLight、GallowsMarch、ShockSphere、SoulPurge、
CrimsonBlade、BloodCyclone、Aethertide、MarchingBreath、TacticalAero、EntropicFlame、
DarkFlare、SoulSacrifice。默认主怪DeathPhase，不要求清完小怪。

六普通AOE复用已有Start锁包位置/方向/NPC钟与watched Finish按casterID删首：
PurifyingLight24115圈12为安全色/Risky=false，不注册安全目标；
CrimsonBlade24103扇50/半角90°、BloodCyclone24104圈5、
TacticalAero24109前40/宽8、EntropicFlame24111前60/宽8、DarkFlare24112圈8。
DevourSoul24093与Blight24094分别用原caster动态单体/全屏诊断，不改监听Helper24095；
Aethertide24106复用原目标R8分散及独立Finish。MarchingBreath24107与SoulSacrifice24113
各自保留Casters、原引用Finish和回调时caster名，公共中断提示改为按rule保存，诊断不扩自动动作。

GallowsMarch24096在初始场地发布环25–30，钟为NPC结束+.8d；
仅Deathwall1EB173有效Created回调将场地换R25、量化当前中心、清环。
冷初始化逐组件/逐Actor先Created再Start，两种Actor顺序均实测；
重复Created仍按源重复量化，之后Gallows不再生成环，不按Finish/TTL清环。

行军状态按Actor ID保存：2161/2163/2162/2164映射0/90/180/270°，
Gain逐次追加3秒pending并按Activation执行List.Sort，Lose删首同方向；
1257/3629的Gain覆盖ForcedEnd并递增计数，任一Lose清ForcedEnd并递减计数。
真实60槽与Source变更走源同样的Lose/Gain，冷Pet回补与热过滤保留。
从当前角色位置/方向开始，先真实ForcedEnd剩余段，再逐条累加相对方向；
6.6f速度、double SinCos缩窄、逐段float加法，不贴墙、不截TTL。
仅最后一段非零且落点未包含于任一PurifyingLight时提示「Aim for safe spot!」；
零圈返回安全，多圈要求交集，不叠加Arena判断。
源ActorProjected标记未被旧桥消费；迁移只输出旧桥实际消费的线，不生成安全目标、自动转向或移动。
Primary真实24096提供全局「Apply doom & march debuffs」。

ShockSphere OID3232为R7、默认钟、原位置方向，惰性Enemies缓存保留原Actor；
热Created缓存按OID加入所有Actor类型，再单独过滤组件Created回调。
Dead/Targetable/EventState不筛，Destroyed移除旧引用，新同ID代追加。
显示/提示为voidzone，导航仅临时障碍，不再叠危险区。
SoulPurge24098设置dual，24099圈10/24101环10–30追加；dual额外追加相反形状，第二钟为NPC结束+float2.1，
随后清dual。列表不限2项，每次查询原地改首Risky=true、其余false，count>1把首染Danger且之后不恢复旧色。
24099/24100/24101/24102任意NPC来源真实Effect只弹首，不检形状/来源，普通Finish和超时均不清。
冷缺24098历史只恢复当前一项，不猜dual。

行军线DLL严格对照暴露共享线注册表Dictionary槽复用导致两段顺序倒置；
`AoeVisibilityRegistry`现让线与AOE一样接受owner显式顺序，只置换该owner已占位置。
新增三线owner逆序/夹入其他owner反例与清理验证；当前多段坐标、顺序、逆Seek和owner释放均通过。
不据该修复宣称跨不同owner有统一绘制顺序。

最终证据前缀`native-zadnor-grave-20261125-`：model03为339相关PASS/0FAIL（含双中断公共消费者）；
model04最终15定向含非NPC球体热加入、死亡墙两冷顺序、DrawSink精确线；
build02为0错4既有警告，同DLLreplay05为15个CE42/排序专项+3基础、replay06三地区联合285条PASS；
catalog01无筛选1714行首PASS（1705测试+9汇总）、296独立入口，全部exit0。
SHA256 `4ae762a1976a8b0d71dca039509c759658d65987e7e777ee9d040225af3c2e5f`。真实DLL含R5 SafeZoneSolver脱险无fallback、输出通道/精确ownership、
机制关闭、owner、逆Seek与回放无Facing/ACR执行。真实Feed十五组件覆盖60槽、无Chain历史、
缩圈、全部标准动作、伤害、提示、导航/障碍及FIFO。原始距离幅值不扩已有转换合同。

失败保留：model01为12 PASS/2 FAIL：测试额外手调Created使源中心二次量化，
另在无输入过期测试只让native重采样，造成额外Gain；分别改为单次真实W事件及双方仅推进时钟，
保留严格坐标/状态断言；model02剩重复Created，model03/04通过。
replay01反射错写Obstacles，改用真实HardObstacles；replay02–04发现真实线枚举逆序，
先排除预期一次合并float加法差异，再保留逐段精确断言并修共享排序，build02/replay05–06通过。
旧build01/replay01–04不能作为最终DLL证据。SDK10.0.303隔离验证，SDK11未验；
实时采样、像素、游戏内执行待确认，未部署。

补充复核：同一build02 DLL的replay07退出0、18条PASS，新增两个显式owner交错加未排序线、既有其他输出全局槽位保持，以及第二段起点等于第一段终点的严格断言。没有因此重编生产DLL；catalog01仍为同一SHA的完整目录证据。
