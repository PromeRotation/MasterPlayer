# 达尔里阿达原生机制规格

本规格记录 BMR `Modules/Shadowbringers/Foray/TheDalriada/` 到 MP 的逐组件迁移。生产使用 `Presets/Foray/Bozja/` 原生模型和 `Presets/Foray/Entries/Zadnor.cs`，沿用 GUID `masterplayer.foray.zadnor`；没有生产 `BossModule` / `MechanicSession`。同区域未迁首领继续保留桥，不能用单个首领完成宣称整个达尔里阿达完成。

## 铁胆狱火 萨托瓦尔

源目录 `DAL1Sartauvoir/`，快照 SHA256：

| 文件 | SHA256 |
|---|---|
| DAL1Sartauvoir.cs | `84c3898f2fe5f04bd349c8782ccf9b33ea206866d51d1860af5230d07a6a0693` |
| DAL1SartauvoirEnums.cs | `f90f4db662dbb09c65071f787da56031e55976b54bcd8fe36c77f25b8ee7747d` |
| DAL1SartauvoirStates.cs | `941f5f21bd0da47f847fbb4fb7b84c40cd439f4c512ec668b75bfee65a804329` |
| Pyroclysm.cs | `d061488ddadab65d86c9999ff62f4f211c4d77bb2e4cd1ad1c5fd595ba8e84667` |

本机中文客户端表：CFC778「扎杜诺尔高原」唯一双向映射 T975，内部 `n4b6`、PlaceName3662；BNpcName9384「铁胆狱火 萨托瓦尔」。Boss3250、Ignis3253、Huma3251、Helper3252/233C在BNpcBase，塔1EB214及1EB213在EObj。源全40个唯一AID均存在，extra全部0；25022及24185/24190/24193正式名为空，不以空名认作缺动作。此次未证明OID3250到NameID9384的独立数据映射。查询使用 `tests/ScriptCompilation/bin/Debug/net10.0-windows/` 下Lumina及Lumina.Excel，哈希分别为 `67da75cefa468277972c8c8c27264b1d276fe2047213c223d1ecdced435d4e05`、`af1ac061ed0b5378a180113cf91969cd40c3b90a25e526d1fc3d35fc0b3ca5fa`；表与CastTimeExtra另由可复跑模型核对。

固定中心(631,157)、正方形半边19。源默认CheckPull只要求primary可选中且InCombat，不添加进场距离；模块加载/退出仍保源管理器500码范围。原生Catalog追加索引28，不改既有0–27，SampleRadius19。

十三组件按源状态机顺序：

| 组件 | 行为 |
|---|---|
| PyrokinesisAOE | 24189动态原NPC伤害诊断及全局Raidwide提示 |
| TimeEruption | 24176/77/74/75，共享前方20×20矩形 |
| ThermalGustAOE | 24181，前方44×10矩形 |
| GrandCrossflameAOE | 24187，长40/半宽9十字 |
| Flamedive | 24179，前方55×5矩形 |
| BurningBlade | 24208原caster当前目标单体伤害诊断及全局Tankbuster |
| MannatheihwonFlameRW | 24200动态原NPC伤害诊断及全局Raidwide |
| MannatheihwonFlameRect | 24201，前方50×8矩形 |
| MannatheihwonFlameCircle | 24202，R10圆 |
| Brand | 24204/24203共享R40半圆，方向取原spell.Rotation |
| Pyroclysm | Created塔R4、min=max1、当前钟+8.7d |
| Pyrocrisis | 24207，原目标R6分散、导航R7 |
| Pyrodoxy | 24206，原目标R6分摊、min8/max∞ |

普通AOE锁Start位置、方向和NPCRemainingTime钟，Effect不删，超时不删，watched Finish按caster ID删首。Brand为Grouped Finish：不核动作，按caster ID删首；默认99条起RefSort。TimeEruption是同类的参数变体：前三条保输入序，第四条及以后每次追加执行同源右pivot/严格小于排序，始终仅发布前两条，隐藏项保留待Finish晋升，不能把其余条变为仅显示或提前清理。新增内部参数 `MaxCasts` / `ExpectedNumCasters` 默认保留既有消费者行为。

塔复用既有开放世界输出，单独实现Created与Effect语义。冷激活按每组件、逐Actor的Created→当前Cast顺序；排除Player/Pet/Chocobo/Buddy，但EObj允许。仅1EB214生成，每次回调均追加，不去重；锁创建时量化位置和当前钟+8.7d，冷激活也不恢复出生历史。1EB213、同OID Player不生成，塔视觉对象移动/销毁、任意cast Start/Finish、超过预测钟均不清塔。真实24184或24183从任意有效NPC source读取**当前caster位置**，按X/Z各轴误差≤1首匹配删一条，忽略Effect目标位置。

每塔首次查询独立捕获当前世界全部OID0原Actor引用，含Dead、不限Party；后来加入或同ID重建不取得旧塔资格，已销毁原Actor仍留缓存。提示、背景/前景双色、首塔钟、R4、所有候选的反转圆交集、viewer资格决定伤害mask等保留源GenericTowersOpenWorld合同。反转圆交集仍经现有有限采样，不擅自改为直接安全Goal。

分摊/分散各用独立列表，Start时找原target，未找到不在以后补造。目标死亡仅隐藏、复活恢复，销毁后同ID新代不替换原引用；Finish按target ID删首，不按完成cast的source删；重复target分别计数。分摊至少8人的提示与目标站位、分散当前目标量化显示和未量化导航保留源规则，不新增伤害预测或自动治疗策略。十三组件联合导航及伤害按源Normalize全局排序，执行仍由MP的移动总开关、owner和回放门控管理。

证据前缀 `native-dal1-20261125-`：model03为364相关场景PASS/0FAIL，最终model04为13个DAL1定向PASS。后者补延迟12秒冷激活塔，明确采用激活钟+8.7；同OID热Player不能建塔。真实Feed按完整世界逐Actor回调、旧Party与十三组件更新，比较有序原始AOE、文字、Normalize钟、实际旧桥转换几何及伤害。塔对照含重复锁点、Effect目标位置不能替代caster位置、原视觉Destroy残留、惰性资格与同ID新代；分摊/分散直接源差分覆盖重复target删首、死亡/复活/Destroy及min8。独立静态复核无剩余确定差异。

生产build01为0错4既有警告，exit0，8.35秒。DLL SHA256 `5032f4573897cca9ac810f371e90ea86731780a432b4d8768cb01130d3b3f00d`；之后仅改测试、导出器及文档，生产未变。同DLLreplay04三地区加DAL1联合312条PASS，catalog01无筛选1732条行首PASS/0FAIL与296入口独立编译，全部exit0。DAL1新增18项实际DLL专项，覆盖十一动作形状/普通Finish、TimeEruption隐藏晋升、R4塔真实solver无fallback、动态伤害/分摊分散、目标量化移动、开关、逆Seek与owner释放；回放仅规划和绘制。根SDK11及游戏实时采样/像素/执行仍待验，本批使用隔离SDK10.0.303，未部署。

失败历史保留：model01为正式中文名候选错误、圆形raw比较误用非圆辅助距离路径，以及生产联合Normalize遗漏；核表修名、明确圆形原始距离，并补生产导航/伤害Normalize后model02通过。新增target代际和公共消费者后model03通过，最后冷塔/Player反例由model04通过。replay01/02把仍应显示的场地轮廓误判为机制残留，原始输出明确Casts=0、damage=0、仅bozja/arena；最终严格要求仅剩一个场地预览。replay03目标显示直接用未量化位置预期，已按源WPos.Quantized独立算格点并保持精确等值，replay04及catalog01通过；未放宽容差或改生产量化合同。

精确源ownership登记后审计为787 verified /426 pending /1 review，另45 excluded，1214目标不变。达尔里阿达其他首领及Zone没有借本源完成状态清退桥。

## 第四军团魔导巨兵

源 `DAL04thLegionColossusRubricatus.cs` SHA256 `74acfe11f7f8b361eb442355d8c7c884b859bfdcb0012b001dd780643c2da6cc`。本机BNpcName9432为「第四军团魔导巨兵」，BNpcBase326C存在（ModelChara254）；两行分别核验，不推断独立名称映射。沿用CFC778/T975；全6唯一源AID均存在且extra0：6497、24226、24225、24227、24224、24221。最后的MagitekField仅枚举，没有实际激活组件，不新增其输出。

Catalog追加索引29，前0–28不变。两组件按源顺序CeruleumVent→MagitekSlash。场地沿用源三矩形真实布尔合并，由`--dalriada-arenas`确定性导出`DalriadaArenas.cs`，中心(650,-551.07495)、半径23.485046、12顶点凹多边形；不能用外包矩形。默认Pull不加本人距离。入口Zadnor沿用GUID，版本1.11.0。

24224为原NPC动态Raidwide伤害诊断及全局提示，普通Finish按原引用删首，不额外制造R40危险圆。旋转Start24225为−60°、24226为+60°，允许零初始方向；每次追加独立六击队列，原点取原spell.LocXZ，初钟取NPCRemainingTime，间隔1.6d。24225/24226/24227真实Effect只推进index0，不匹配source/位置/方向；空队列仍加NumCasts，Finish/Destroy/超时不消费。

只复用既有PublishRotations算法；独立缓存以序列数量或NumCasts变化失效。每序列未来先当前后；未来钟max(next,now)+1.6，当前保原钟，最后一击Future色仍Risky。导航只有实时序列非空才消费发布缓存，并追加实时首项原点R5安全Goal及首项钟。首次Start尚未Update仅有Goal；最后Effect后Update前旧绘图仍可见但导航全空；期间新Start可恢复旧缓存加新Goal；首队列耗尽而第二仍在时Goal立即晋升、旧Cone到Update再变。没有把查询与缓存更新合并。

证据前缀`native-dal0-20261125-`：export03成功；model03为370相关PASS/0FAIL，最终model04六个DAL0定向场景PASS；完整MechanicSession/Feed两实际组件按逐事件世界比较有序AOE字段、真实Check、全局提示、Normalize钟、实际桥转换几何和伤害。包含两旋向零角、两不同source/钟队列、跨source只推进首项、空Effect计数、Finish忽略/锁点/过时缓存，四个Update前窗口。冷Rest用全新真实Session及Region，按源不猜缺失First历史。

生产build01为0错4既有警告，exit0，8.76秒；DLL SHA256 `086de723468af9f09928bea324fa47e418ec4f5d5e5da6482fb52fed6d6230aa`。同DLLreplay02三地区加DAL0/DAL1联合316条PASS；catalog01无筛选1736条行首PASS/0FAIL，296入口独立编译，全部exit0。DAL0新增四项实际DLL专项覆盖两旋向/真实扇形/凹场地/真实solver无fallback、R5注册、Player Effect门槛、六Effect和空计数、动态伤害、冷Rest、机制开关/逆Seek/owner。最终replay03仍同DLL，明确移动旋转caster ID1后精确验证Loc锁点；回放仅规划绘制。两轮独立静态审查无剩余确定生产差异，SDK11与游戏实时采样/像素/执行仍待验，未部署。

失败与修正：export01缺完整类型名分支抛NullReference，补真实GetType分支后export03完成半径及顶点；model01两个测试编译错误（nullable SeString拼接、Span无Any）修正后model02通过。独立源码审查发现live空序列尚未Update时导航应整体为空，生产增加guard并以真实源AddAIHints反例验证；model04补首队列切换及新Start窗口。独立复核指出DLL runner先前移动的是Helper而非旋转Boss，最终改为真实Boss移动，replay03通过；未放宽断言或容差。

源精确ownership登记后审计788 verified /425 pending /1 review，另45 excluded。其余模块及Zone仍pending并保桥，不借本模块完成清退；DAL2的后续验收见下节。

## 第四军团丘库雷因

源目录`DAL2Cuchulainn/`，五个源文件SHA256：

| 文件 | SHA256 |
|---|---|
| DAL2Cuchulainn.cs | `f65e5608776de37f751d9283c3ae845f1aaec1b22ad5ab703c62e401c9998890` |
| DAL2CuchulainnEnums.cs | `114f97944e9f26d67f8b1d18eba08bb6fdb20c17e878644304dae6fe942da7c4` |
| DAL2CuchulainnStates.cs | `ee591d7d32eb87b64592e2ac3bbdccab8e50815e16b0ae402d1cf92823584b68` |
| FleshyNecromass.cs | `61eb915ad2c00b3ad54d5d7bcde8751af11055e995aebf67c78bd065a556f48c` |
| BurgeoningDread.cs | `bc000f54ee2fa4747423a274654cfd19d5624e8fb98524fb4a06ccd0af791d85` |

本机CFC778/T975双向映射沿用前节；BNpcName10004正式名「第四军团丘库雷因」，OID31AB。19个唯一源AID存在、CastTimeExtra全部0：6499、23693/94、23695/96、23688/89、24909/10、23691/92、23682/83、24953、23684/85、23698、23686/87。Catalog追加索引30；入口Zadnor版本1.12.0，GUID不变。场地由`--dalriada-arenas`工具导出源R25.199正48边形减上下双矩形，实际中心(650,-187.5)、半径25.198975、44顶点，不能以圆替代。不额外添加Pull距离。

十组件按源激活顺序为FleshNecromass、FleshNecromassJumps、FellFlowAOE、FellFlowBait、GhastlyAura、AmbientPulsation、NecroticBillow、BurgeoningDread、MightOfMalice、PutrifiedSoulBurgeoningDreadGhastlyAura。三个标准AOE分别为23691/R50半角60°、23694/R12、23687/R8，普通Finish结束、Effect不结束。Ambient最多显示前6项；仅列表超过3项时前三项Danger色，所有显示项仍Risky，隐藏项依真实Finish晋升。

四个固定圆心为(637.27,-174.667)、(662.71,-174.667)、(662.71,-200.133)、(637.27,-200.133)，每个R5.676正40边形。首次23695 Start创建普通常驻区，钟为原NPC剩余时间+.2d；之后不重锁、不重算钟、不按Finish或超时删除。23682 Start启用反转区，钟为原NPC剩余时间，**不重置NumCasts**。Gelatinous2543按回调当时旧Party槽置位/清位，无引用计数；active且当前观看槽无bit时显示Safe色反转区，但Risky仍true，Check/Draw仍是原四块polygon，只有Distance反转。此时提示固定“Move into a voidzone to turn into pudding!”；普通分支使用原GTFO提示。active期间23683/24953真实Effect恰第五次归零并退出变形，实际第六/七次不再计数。

`BozjaOrderTemplate`只增加明确半径/边数参数，CE32默认5/20不变。距离保留源双Clipper规范化、prev→current、世界起点加法、局部边差、1e-8f及scalar投影顺序；源SpatialIndex只查询世界5码cell内覆盖边，空cell返回`sqrt(float.MaxValue)`，不能改成全局最近边。反转polygon继续走旧桥有限网格采样；高填充会拒绝导航，不另造四个安全Goal。普通区仍能发布实际危险图。

Jump23683/24953以真实TargetXZ覆盖单个R12、当前钟+1d，保留至23684真实Effect；23685、Finish、超时不清。Icon40每次追加原Primary和原target，查找失败回退Icon actor，当前钟+5.2d；重复图标不去重，23692从任意合格NPC source全局FIFO只弹一项，Finish不消费。Update只清原target Dead，Destroyed且未Dead的原引用保留，同ID新Actor不替换；本人绘制outline、他人填充，绘图/提示按ID，导航按原引用。本人导航为面向活Party队友的R100锥，其他人避R50半角15°。预测mask取活bait，钟取原列表[0]；保持诊断范围。

GhastlyAura只有24909产生cast hint；1422/2936/3694/3909共用旧Party槽bit，任一Lose即清，即使另一匹配状态仍在。冷激活回放真实60槽并包含Pet，热状态回调排Pet；冷Pet读条不触发Cast组件。只输出“Misdirection：生效中”诊断，不添加真实控制器。真实Feed覆盖冷Pet槽0/59、热Lose排除、两个匹配状态单Lose，以及active变形期间同帧Gain与Party重排、下一帧Lose改变反转区选择；oracle按`World.Party.FindSlot`查询当前观看槽。

行军2161/2163/2162/2164为相对0/90/180/270，1257/3629为Forced；每段3秒、速度6.6。保留逐段raw float钟与`wallDistance-(.5f-.499f)`，允许负微距离，不钳0。危险落点只查固定四polygon，不依赖普通区是否已创建或2543。ForbiddenDirections仅首pending，距离严格6<d<25，方向用pending相对方向，不叠玩家朝向。源Normalize同钟右pivot严格小于排序保留，旧桥.75秒窗口取最后center+π而不消费halfwidth。只规划面向、绘制线和提示，不新增Goal或回放执行。

`CuchulainnGeometry`为此固定凸场地缓存原生行查询：保留32行、AABB、DDA、逐行x hull、首full查询的补齐SIMD、后续新边scalar、上端点排除及共线路径。当前44顶点/42非水平边的每行最多6条，后续new/ending/single都少于8；不推广为其他场地通用解算器。AVX512分母和分子融合；AVX2分母不融合、分子按FMA支持决定；标量不融合。源每行少量水平边跳过下界二分，直接检查t，不能额外用舍入后的Y下界筛掉。真实源差分覆盖21×21内外点、全部顶点及各轴邻ULP、独立AVX2分母反例、每点64朝向，单段以及Forced+三段串联逐端点精确相等；AVX512、AVX2+FMA和scalar实际分支分别通过。不改变共享Dahu墙查询。

Raidwide源数组实际为`[23695,23695,23688,23689,24909,24910]`；23695同原Actor添加两项、Effect计数+2、Finish删两次，23696完全不消费。23698为单体动态诊断。两者复用原Actor当前raw CastInfo/NPCRemainingTime及当前target回退；不新增范围AOE、治疗策略或自动动作。

冷激活仅按源恢复当前Cast/状态；没有已结束23695/23682、历史Icon或Jump时保持空，不猜历史。真实Feed十组件逐事件比较有序AOE字段/风险色、提示、Normalize伤害与钟、实际旧桥采样几何；独立组件另比raw Check/Distance、重复状态/图标、原引用换代、实际Effect消费与无TTL。

证据前缀`native-dal2-20261125-`：export01成功；model17为382相关PASS/0FAIL，最终model21为12个DAL2定向PASS，包含冷Pet匹配读条排除。墙查询model17为实际AVX512，model19为实际scalar，model20为实际AVX2+FMA，均exit0；model18设置的旧AVX512F环境名没有关闭分支，输出仍AVX512，故不将其计为AVX2覆盖。

最终生产build02为0错4既有警告、exit0、7.99秒；DLL SHA256 `1bb71fd014308dae781e94227d7185f640d3395666d281e4600fe7253cd11435`。同DLLreplay03三地区加DAL0/1/2联合330条行首PASS，catalog01无筛选1750条行首PASS/0FAIL与296入口独立编译，全部exit0。新增14项实际DLL专项覆盖三个标准AOE及真实solver无fallback、永久/反转四圆与采样拒绝、五Effect/真实Jump、Icon原target代际/R100、状态0/59与面向计划、七个伤害动作/重复23695，以及冷空历史、机制开关、逆Seek和owner释放。回放只规划绘制；根SDK11及游戏实时采样/像素/执行待验，本批使用隔离SDK10.0.303，未部署。

失败记录保留：model01为事件DTO类型名编译错误；model02为真实Feed ActionType夹具错误及raw Distance差异；model03定位源5码cell而非全局最近边。model04在Distance通过后因夹具只激活Flesh却把native Jump提示混入比较而失败；model05将源Jump接入并逐组件汇总完整提示后通过。独立审查的水平射线反例由model07真实复现；model08进一步复现首row SIMD，model11复现场外entry差异，补完整row查询后model12通过。model13/14/15的多段反例为源少量水平边跳过下界二分，按源t判断修复后model16通过；独立复核另补AVX2分母及水平硬件分支，三个实际硬件路径均验证。未放宽容差、删断言或改变输入范围掩盖差异。

精确源ownership登记后审计789 verified /424 pending /1 review，另45 excluded，1214目标不变。DAL3双首领、DAL4及Gauntlet/Zone仍pending并保桥；CE22/DRN5/Quantum/DD190缺口不变。

## 桑尼恩与达温

源目录`DAL3SaunionDawon/`，八个文件SHA256：

| 文件 | SHA256 |
|---|---|
| DAL3SaunionDawon.cs | `df6f344ca7d7b2a74aacce2e146878e0f4ce668a0d9d0e3ae84175edc64469a7` |
| DAL3SaunionDawonEnums.cs | `60cff4ad35ebde2f549f612ea5deb66d05614d8c7bc56224897398443f31ecbc` |
| DAL3SaunionDawonStates.cs | `10c718ab38e811fec9992d0c86cc3119bfb4b3ffb5547bfb74ecfbca4a685631` |
| MobileHaloCrossray.cs | `30a4cd95ed9909576c50798457378c1c33e8b6c8062e586d4816f5ec3bdb0c02` |
| Obey.cs | `21f17e32a8d23c8449eeedb2a14435fb384175e4c586a54eb59156b471168f0b` |
| OneMind.cs | `bceb97933ff69aa6f515c800392301373082784a83609446d80ea5d126b4e5e9` |
| SpiralScourge.cs | `48ef3f65f527d6b0ba3f6d5155d07602a11b4c1fa017761a9034e6717a3a3028` |
| VerdantPlumeVermilionFlame.cs | `16dfd7fc83a2da95eebcfea81f7f3a175a4f987e7631c730c5b5379087f49b78` |

本机BNpcName10192正式名「桑尼恩」、9452「达温」；CFC778/T975双向映射沿用前节，41个唯一源AID存在且extra0。没有证明OID31DD/31DE至上述NameID的独立实例映射。Catalog追加索引31，PrimaryOID31DD；中心(650,-659)、正方形半边26.5。Zadnor入口版本1.13.0，GUID不变。

P0顺序为MobileHaloCrossray、AntiPersonnelMissile、MagitekHalo、MagitekCrossray、MissileSalvo、SurfaceMissile、Touchdown、HighPoweredMagitekRay。P0退出条件仅Primary Destroyed或缓存达温Targetable；保留其中Mobile、AntiPersonnel、MissileSalvo、Surface、HighPowered五个原实例及相对顺序，再依次激活VerdantPlumeVermilionFlame、FrigidPulse、SwoopingFrenzy、Pentagust、ToothAndTalon、WildfireWinds、Obey、SpiralScourge、OneMind，共十七种实际组件。P1结束只看两Boss Dead/Destroyed，不看HP0；达温缺失也满足其结束条件。

状态机更新先于UpdateModule首次缓存达温。`Enemies(31DE)`首次按当前世界字典顺序建表，空表也缓存；后续Created追加、Destroyed移除原Actor，不按BirthOrder重排。一旦缓存达温，死毁或同ID新代均不重绑。Primary也保原代际。冷激活按组件逐Actor执行Created→当前Spell Start→真实60状态槽；Created/Start排Player/Pet，冷状态包含Pet；热状态排Pet。热EAnim不加NPC类型过滤。

| 组件 | 原生合同 |
|---|---|
| MobileHaloCrossray | 23993–96为9–60环、23997–24000为长60/半宽8.5十字；四个18码偏移，量化固定场地中心，NPC钟+2.1d；Start覆盖，只有23990/23992真实Effect清空 |
| MagitekHalo / MagitekCrossray / SurfaceMissile | 23989环9–60、23991十字60/8.5、24004圆R6；普通WatchedAction Finish按casterID删首，Effect/TTL不删 |
| AntiPersonnelMissile / MissileSalvo | 24002原target分散R6、24003分摊R6/min2；Finish按targetID删首，独立列表及原引用，不新增伤害预测 |
| Touchdown | 24006锁LocXZ、30码径向击退、stopAtWall；仅匹配Finish按casterID删首，24007Effect不删；真实hitbox减.499f、严格expire>activation免疫；只画非零位移线，无AI危险区 |
| HighPoweredMagitekRay | 24005的float半径重载，目标中心R5；默认Finish结束，目标死毁不主动清，查询只排source Dead；提示/绘图按targetID、本人导航按原引用，其他Party按真实0–63槽序 |
| VerdantPlumeVermilionFlame | Created OID31E1为3–12环、31E0为R10圆，锁量化位置，激活/创建当时钟+14.4d；任意24014/15 Finish清全部，Effect/Destroy/超时不清 |
| FrigidPulse / SwoopingFrenzy / Pentagust | 24701环12–60、24016圆R12、24018扇形R50半角10°，普通Finish结束 |
| ToothAndTalon / WildfireWinds | 24020单体、24013全屏，原NPC动态剩余钟及现有伤害诊断，不制造AOE或治疗策略 |
| Obey | 热marker1EB1E8/1EB1E7及三种EAnim追加十字50/7或环12–60；首钟Now+16.2d，后项取当前队首钟+6.4d×Count；只公开首项，24011/12真实Effect跨source FIFO删一项 |
| SpiralScourge | 8套272点确定性导出，只匹配前6套，首点逐轴误差≤1；Start归零但不清旧队列，整套钟NPC+.8d+.6d×patternIndex；23987Effect无条件加计数，仅查询当前首项原地染红，保旧色，公开6项且全部Risky |
| OneMind | 任意2553 Gain置true、Lose置false，无refcount；两Boss当前Name用于文案；观看者恰被其中一Boss选中时，绕另一Boss原位发布当前距离+2圆，原钟DateTime.MaxValue |

Obey首方向使用量化marker减达温未量化原位，后方向减队尾锁点；重复动画追加，冷无历史不造队列，marker Destroy不消费，达温死毁在Update清队列。Spiral两个marker1EB216/217 Destroy只清表不清计数；不匹配Start可重新暴露已染色旧项。Script缺有效视角/Party槽时撤销输出并停止查询，避免在隐藏期间提前改变颜色。OneMind沿用本项目已批准的绝对危险窗口适配（见NativeBozjaSouth），真实注册`DangerFrom == DateTime.MaxValue`且当前不激活，不转即时永久危险、不新增Goal。

HighPowered预测mask只取活source的targets，钟固定取raw首项，即使首source已Dead；原target死亡仍保留。Touchdown保源倒数乘法、Square AABB slab与NaN操作数顺序、多caster追加顺序串联；低hitbox的源Math.Clamp异常不静默钳成.5。标准形状共享既有生产实现，只补本模块差异，未建立运行时BMR包装。

证据前缀`native-dal3-20261125-`：export01输出8套272点并逐点等值；model08为402个相关PASS/0FAIL，含20个DAL3场景。完整MechanicSession/Feed十七组件及双phase逐事件比较有序AOE字段、Check、颜色/Risky、提示、Normalize钟/伤害和旧桥实际转换几何。另覆盖冷Pet槽59/热Lose、Player marker EAnim、Obey弹首后立即追加、原target及Dawon代际、乱序Party/非法槽、死亡raw首bait钟，以及精确击退内外点/各边ULP/不同hitbox和无关Finish。

生产build02为0错4既有警告、exit0、8.15秒；DLL SHA256 `0c80586ef924b3b16c1cec54ab9c3f9402a4fa128ae9653ca7a7c3d9b9a56e2d`。同DLLreplay08三地区与DAL0/1/2/3联合349条PASS；最终replay12为20项DAL3专项加3项公共检查，全部exit0。实际DLL覆盖六标准形状/真实solver无fallback、Mobile跨阶段、冷Plumes、Spiral六风险/缺视角颜色/逆Seek、Obey重复FIFO、原目标及动态预测、真实线注册和零线过滤、OneMind真实窗口及双Boss死亡后的全部输出撤销。回放只规划和绘制，根SDK11及游戏实时采样/像素/执行待验，未部署。

失败证据保留：model01–02为完成判定夹具/缺Circle raw分支/Hitbox与Name输入不一致；model05真实复现Party顺序及KB倒数乘ULP差异，修复后model06通过401项。model07真实Feed复现空Enemy缓存后的热Created顺序错误（source40/native41），修复惰性列表后model08通过402项。replay03在旧DLL复现缺视角仍显示并查询Spiral；新DLLreplay04已通过该反例。replay04–06的Obey首动画发生于P1激活前、KB读取AOE而非Line注册表，均为runner输入/检查通道错误，保持断言修正后replay07通过；replay01–02则分别为Cross绘图与导航数量混淆、Effect source不存在。新增阶段清理的catalog01/replay09–11发现源点零方向及跨Seek旧模型引用夹具错误，最终replay12使用非零起点并检查Seek重建后的真实region模型，清理断言全部通过；未放宽容差或删除机制分支。

最终同DLL catalog02无筛选1770条行首PASS/0FAIL，296入口逐文件独立编译，exit0。精确源ownership登记后正式审计790 verified /423 pending /1 review，另45 excluded；1214目标与525元数据组不变。DAL4、DAL1Gauntlet及其Zone尚未迁完并保桥；后续独立模块继续推进，不借本模块完成宣称全达尔里阿达验收。


## 迪亚布罗魔兵

本机BNpcName10007为「迪亚布罗魔兵」，CFC778/T975、Boss OID31B3。源51唯一AID全部存在且CastTimeExtra=0；分别核验OID与名称表，不据此虚构独立映射。源目录`DAL4DiabloArmament/`六文件SHA256：

| 文件 | SHA256 |
|---|---|
| Aetheroplasm.cs | `9cda317780c45db286408095d2988b699aee60648fb3d939fdc3d8ab96d370da` |
| ArenaChange.cs | `f47532de526813da86ae6925df1b7127f376a00867dbbe41944cad8245b85468` |
| DAL4DiabloArmament.cs | `bbbb8aa9e4951ef16aa2e1ef0800f3795cfd4f533b4ea72c817d3b1b44973229` |
| DAL4DiabloArmamentEnums.cs | `540ac7ffbae957c18659aac4dffd36c0f1ff84ef9ef069b7245fe2c8682be7d6` |
| DAL4DiabloArmamentStates.cs | `eb1450c84eac1d69f8f4106c2a84f8fa4edf2221b036d8ea0f4d71b7195b33a3` |
| RuinousPseudoomen.cs | `4cdf44aec23547f7bb26b3ab8a0478c8bf1982c554972b8eee485b12edd88562` |

原生生产仍由`Presets/Foray/Bozja/`模型与札杜诺入口承担，Catalog追加索引32、16组件；入口1.14.0。DefaultArena用源导出正48边形R29.5，不以真圆替代；Map33/20001缩R17并清预警、量化中心，80004恢复原中心多边形且保留预警。`AdvancedDeathRay`只有声明，没有实际激活，不输出、不接管通用识别。

| 实际激活组件 | 输入与保留行为 |
|---|---|
| ArenaChange | 23711环17–30，判定finish+9.2；只有缩场Map清预警 |
| RuinousPseudoomen | 23712先追加rect100/半12，再按四OID原列表固定8/7索引选门；二段+4、三段+4.1；24911独立追加；24995/24908/24911任意caster Finish弹首，Effect/TTL不清 |
| AethericBoomExplosionDiabolicGateVoidSystemsOverload | 23732/23751/25028/23736原NPC动态伤害钟及全局提示 |
| AdvancedNox | 23744/45，R10，步10、1.1秒、五击；方向取原Actor.Rotation；实际Effect按caster当前位置逐轴≤1匹配；只Update发布缓存，无Finish/Destroy清除 |
| Aetheroplasm | Created31B5保原Actor，Destroy保留，23733/34实际Effect删原caster；安全R1.5轮廓仅绘制；AI为位置+.5朝向的世界轴半尺寸.5矩形反向交集，Now+5，经有限采样，不造Goal/不执行Sprint |
| AssaultCannon | 23726 rect100/半3，普通watched Finish |
| DeadlyDealingKB | 23747，30码stopAtWall；精确hitbox-.499及原墙算法，联合完整Cannon列表和已发布Nox判落点；只提示/线，无新增AI |
| DeadlyDealingAOE | 23746 R6 |
| Explosion | 九AID rect60/半11，expected99排序；仅activation>=first+1且rotation精确相同截断；GroupedFinish不查AID按caster弹首 |
| LightPseudopillar | 23730 R10 |
| PillarOfShamash | cone70/半10度、expected9；普通activation>=first+1窗口截断 |
| UltimatePseudoterror | 23715 donut15–70 |
| AdvancedDeathIV | 23728 R10 |
| PillarOfShamashBait | Icon23、23740实际Effect、5.1秒、rect70/半2、原target；Dead Update删、Destroyed保留，默认Raidwide伤害预测 |
| PillarOfShamashStack | 23741 marker/23742 resolve、6秒、70/半4；Effect替target后按target消费，Start/Finish不消费；源TODO min8不补造 |
| AccelerationBomb | 2657旧Party槽，Gain覆盖/Lose清；冷60槽含Pet，热排Pet；近2.5秒Stop everything，Risky按原Actor.Cast非null（含Ability）或TargetID非0；Dead在Hint后清，Pyretic仅诊断 |

Ruinous门缓存按首次真实字典顺序获取，空表缓存、Created追加、Destroyed删原引用；拼接临时列表与破坏原pair严格分离。缺门/耗尽异常保留首AOE及已发生副作用，不guard、不回滚。最多显示前2；只有多项查询才更新前两Risky和首Danger；剩单项保留历史，不能按超时晋升。

输入事务必须区分Feed已提交快照和世界Actor物理Cast。Region用前者统一决定是否发生Cast操作；所有模型旧Finish成功之后、任何新Start之前先统一写入物理Cast；全部回调成功才提交Feed输入。热Start异常撤销脚本全部绘图、导航及场地，保留模型中间状态；旧Actor重试，新Actor初始tracked已写入而不重试。恢复原已提交输入不覆盖失败物理Cast；随后clear只Finish物理Cast。真实Feed及同DLL均覆盖DAL4先、CE54后、23589→23712失败→重试→恢复23589→clear，验证无重复Thermal消费。冷Begin失败按源卸载原Boss、retire，不激活后续Orb等组件，诊断留存且不阻断别模块。

证据前缀`native-dal4-20261125-`：arenas03源导出成功；model11为420相关场景PASS/0FAIL，三轮独立复核闭合异常提交缺口。模型覆盖16实际组件的真实Feed、旧Party/冷Pet真实60槽、原Actor代际、逐事件有序形状/原钟/颜色Risky及原始SDF；墙覆盖内外点、顶点ULP和64方向，Orb/矩形bait等与源逐点比较，低hitbox源Clamp异常保留。

最终生产build03为0错4既有警告，exit0，8.83秒；DLL SHA256 `7622d0837706083bc4ef3c22a57c6e5ee2b875e15d1d40bad8ba16e078c02de9`。同DLLreplay04三地区与DAL0–4联合374条行首PASS；catalog01无筛选1803条行首PASS/0FAIL、296入口独立编译，全部exit0。最后加强测试后replay05为24个DAL4专项加3公共检查，含真实solver无fallback、全部常规形状与Finish、Map恢复/缩场、八门三段/隐藏项晋升历史、Nox发布消费、原Orb换代、图标/线分摊、动态伤害、Bomb、冷热失败/恢复旧输入/校正elapsed/跨CE54清除、实际Loaded顺序、逆Seek/禁用/owner/回放不执行PR面向目标动作限制。测试加强后生产未改。

失败与修正：arenas01/02参数位置错误；model01夹具编译错误；model02–04修正源量化中心、Elapsed、helper结束和hitbox夹具，未降低断言；model05地区计数旧断言更新15；model06/07为中间通过证据。model08过滤参数被shell拆开，实际只命中413场景，不能当script controls覆盖；model09为420通过但发生在最终统一差分前。model10的IndexOutOfRange来自把Catalog索引用于地区过滤后的数组，改以PrimaryOid319C选择，不降低断言；model11才是最终420通过。replay01/02/03属于中间DLL与逐步补边界的日志，最终依据build03/replay04/catalog01/replay05。

精确源路径ownership登记后审计791 verified /422 pending /1 review，另45 excluded；1214目标与525元数据组不变。DAL1Gauntlet及Zone仍pending并保桥；CE22、DRN5、Quantum和DD190缺口不借其他PASS关闭。根SDK11及游戏实时采样/像素/执行未验，本批用隔离SDK10.0.303，未部署。

## 第四军团黑色燃焰（DAL1Gauntlet）

沿用T975/CFC778和札杜诺GUID，入口1.15.0。客户端BNpcName10212正式名「第四军团黑色燃焰」及源枚举全AID存在/extra0由可复跑测试核实。Catalog追加索引33，Primary32BF；固定中心(222,-689)、初始方形半边29.5，Map31/20001仅在P0缩为23并清预警，无恢复分支。六模块合计74个激活组件，不把它当作74个独立脚本。

十一源文件SHA256：

| 文件 | SHA256 |
|---|---|
| ArenaChanges.cs | `8b9f92a1dfa415fb861cf68d0b5b08d032ee78669897d7f7c0d893424a6d6d13` |
| BallisticImpact.cs | `847ec3b7808469ce15f4fc8b33cb063fc653339cf32e3621aed88f01f68fd551` |
| DAL1Gauntlet.cs | `83fc3e7ff06174fec1ae9434a03520c910b298b199cc355bcf0177bffd9d884d` |
| DAL1GauntletEnums.cs | `b9ce91494a19ceb8fe14bd3e3768744b4e54443b632f9d5cf77b837972182bc8` |
| DAL1GauntletStates.cs | `2f61c32495e6c5971dc488bc29c08d9d247cad81f72941015c3f9863424a0a5c` |
| NorthSouthwind.cs | `2a1fe7560059db10de1e674f37c896ffc029ae17279795872e7efbf99b030cdd` |
| PainStormFrigidPulsePainfulGust.cs | `b3d8c0480ee03375aae9876bb20a1cbc3db35a5b9b3bc696686ceaeb90f984b4` |
| Pyroclysm.cs | `a412270bfeba7baa99f2c17dd3dae34ba1b7fa872895ea8a84504409d1c4488d` |
| Stormcall.cs | `fa4a200cbe5222fcce414d233e195a080da663096aa1b3d8e2664c948fb3eabe` |
| TerminusEst.cs | `9afa2d284c8e1b7c60a59fcafa9f9f0d98bbce280da94fd66b0ccab47bdddb50` |
| Turbine.cs | `739383967f80ca3d6ff8def5cd13e5929ca139a9121311aad142029ac1043b9b` |

三阶段完全退旧建新，无Keep：P0为ArenaChange、BallisticImpact、SurfaceMissile、TerminusEst、TerminusEstUnseen、CeruleumExplosion；P1为Pyroclysm、SeventyFourDegrees、FlamingCyclone、Turbine、SanctifiedQuakeIII；P2为Stormcall、NorthSouthwind、NihilitysSong、PainStormFrigidPulsePainfulGust、BroadsideBarrage。先StateMachine连续while转阶段，再UpdateModule惰性查BossAugur，P1起查Alkonost/Crow。保存首次Enemies列表及原Boss引用，同ID新代不能替代；缺Boss可同帧连跳完成。每组件完整遍历世界后才激活下一组件，Created→Spell Start→Tether→60状态槽；冷Pet状态允许、热Pet过滤沿源。所有阶段最终输出撤销另经实际DLL验证。

Terminus只在Casters恰10时统计前两rotation，A多于B才以A为多数，平票B；少数方向50×8矩形锁量化点、当前钟+10。第11剑不重算，Destroy不删原列表，24327 Finish清AOE而保Casters。Unseen对1708/1709/1707分别90/180/270度，Gain覆盖ID、任一Lose删ID；首次方向状态时列表空则按当时CommonAngle从原剑筛选，不等十剑且不随后续多数变化重选。选中shape命中且raw Remaining最小者，平值保首；空cast的6.5只用于选择，判定钟仍default。原Actor任意Ability/Spell raw cast逐帧float推进，钟按源两次float运算；不将原引用替换新代。显示眼/瞳孔/弧和原生联合面向已接入，仍使用既有MP方向求解器；不称其为旧桥center+π消费的逐值等价。

Ballistic图标261锁原点、24×24、当前+12，重复允许，显示前2，24334实际Effect全局弹首，Finish/超时不删。塔仅1EB213 Created，R4且min=max1、当前+9d；不与萨托瓦尔1EB214/+8.7d混用。24349/24348从有效NPC当前位置按逐轴≤1删首塔，原对象销毁不删，原Actor资格懒缓存沿公共开放世界塔。Donut图标288保原target可重复，当前+9，背景原位置4–8环、无前景；提示用4/3半径/min2，导航对非viewer原Stacks做R1反圆交集、首Stack钟，无人数门槛/Goal。24343按MainTargetID删首，未匹配清全；只有Update Dead删除，Destroy原目标保留，同ID新代独立。覆盖AddAI不调base，不造Shared预测，独立塔的伤害诊断仍保留。

Turbine24341 Away15、Finish按caster ID删首。导航仅首项，不免疫时p+15*Normalize(p-origin)，方半19及Flaming R9判危险；raw危险0/安全1，零向量Normalize为0。显示/提示按源成功movement最多1：免疫或零方向跳过后继续下一项；实际落点查Arena及Flaming R10，不截墙。North24355覆盖单项Forward40、finish+.1；普通Finish保留，24814/24815实际Effect清。AI方半22，Pain非空时联合全部并忽略Storm，否则只Storm首R35，再否则仅方；提示同时查两组。North存在且非免疫时抑制Storm/Pain普通导航，显示保留，免疫恢复。免疫严格expire>activation，并保真实槽Refresh规则。Storm Tether4锁量化source+48*forward，32CC+20.9d/其他+13.7d；只交换前两钟，显示首1，24359 Start替换首个同source，watched Finish全局弹首。Pain六AID分别donut8–25、35/半65度cone、R20，每组两个动作；任意Spell Finish按caster ID删首。

标准Surface24337/R6、Flaming24345/R10、Broadside24357/前40半20，watched Finish结束，Effect及超时保留。Sanctified24352与Nihility24361保动态NPC伤害和全局Raidwide；Ceruleum24326为动态「Enrage! x.xs left」，未造危险圆。

**明确的MP转换修正与保留边界。** Arena原始形状/Check/Distance为外方30减内23；保源inside0及5码cell边查询、无可用近边时sqrt(float.MaxValue)，顶点/相邻ULP精确对照。真实旧ConvertForbiddenZone只取自定义polygon外轮廓，会把中心安全区注册成危险；原生模型、两视图与导航均保真实孔，规格明确为MP转换修正，未伪称旧桥消费等价。`GauntletArenaEdges`实际执行旧转换并断言旧中心危险、原始源及native中心安全。缩场后SampleRadius必须随方形变23；偏心Turbine(+20,+20)真实旧转换与native均产生非空几何，避免原29.5采样误触65%门槛丢区。其余大面积KB禁区仍按现有采样器拒绝，原始Distance相同不意味着存在注册导航；模型逐事件实际Convert比较确认此边界，实际DLL分别验中心Turbine/North拒绝与缩场偏心Turbine保留。没有扩特殊位移为直接执行。

证据均在`.tmp/halatali-verify/`，前缀`native-gauntlet-20261125-`：model11为446相关场景PASS/0FAIL；最后model12为26定向PASS，增加实际转换比较。build02为0错4既有警告、exit0，最终DLL SHA256 `108c1c3f9afcec60d3cda9141d1ab54c86f4760b0fe2b41a77becbec1fc3337c`；replay05为13个Gauntlet专项加3公共，replay06三地区及DAL0–4/Gauntlet联合387条行首PASS，catalog01无筛选1816条行首PASS（1807测试+9汇总）/0FAIL、296入口独立编译，全部exit0。实际solver无fallback、source gate、三阶段/最终完成、弱点面向计划和回放无可执行PR请求、开关/逆Seek/owner全撤销已验。独立复核两轮闭合raw钟/眼绘制及缩场半径，最后只读复核无新确定差异。

失败保留：model03自定义Distance及Normalize错误；model05错误的全局提示/狂暴Kind已修，早状态和免疫夹具分别补真实Create帧、Lose/Gain（源不会因到期缩短而刷新）。model06剩早状态顺序，model07为25中间PASS。model08单项筛选暴露Fixture初始化先后，修后model09准确复现raw眼钟漂移FAIL；Advance补Gauntlet后model10仅旧地区15计数失败（其余444通过），改16后model11通过。replay01在lead窗口外误要求面向，改为窗口内检查；replay02错误保留Seek前Actor字典引用，改为读取重建后字典；replay03把塔预测误认为Donut新增预测，改为明确禁Shared且保塔；replay04对源采样拒绝误要求非空，model12补真实源转换后分别验证拒绝与偏心保留，replay05全部通过。未放宽几何、时钟、队列、owner或源差分断言。

精确ownership生成并核对后为792 verified /421 pending /1 review，另45 excluded；1214目标/525元数据组/296独立入口口径不变。未迁Zone/其他Foray保桥，CE22/DRN5/Quantum/DD190原缺口保留；根SDK11及游戏实时采样/像素/执行仍待验，使用隔离SDK10.0.303，未部署。
